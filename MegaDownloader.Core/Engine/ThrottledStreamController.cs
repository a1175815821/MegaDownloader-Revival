using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaDownloader
{
    public class ThrottledStreamController
    {

        #region Región Shared
        private static object Mutex = new object();

        private static ThrottledStreamController _Controller;
        public static ThrottledStreamController GetController()
        {
            lock (Mutex)
            {
                if (_Controller is null)
                {
                    _Controller = new ThrottledStreamController();
                }
            }
            return _Controller;
        }

        #endregion

        #region Región privada
        // Streams activas
        // P-08: Contains() is O(1) here. With List it made every recalculation O(N_streams^2),
        // and AddStream/RemoveStream fire once per chunk on reconnects.
        private HashSet<ThrottledStream> _StreamList;
        private Dictionary<string, List<ThrottledStream>> _htId;
        private bool _lastAssignmentHadLimit;

        // Velocidad máxima
        private Dictionary<string, long> _htMaxSpeed;
        private long _globalMaxSpeed;

        // SDK transfers do not run through ThrottledStream: the native bridge reads its
        // own byte allocation from this budget.
        private readonly HashSet<AccountDownloadBudget> _accountDownloads = new HashSet<AccountDownloadBudget>();

        public sealed class AccountDownloadBudget
        {
            internal int Transfers;
            private long _bytesPerSecond;

            public long BytesPerSecond
            {
                get => System.Threading.Volatile.Read(ref _bytesPerSecond);
                set => System.Threading.Volatile.Write(ref _bytesPerSecond, value);
            }
        }

        private ThrottledStreamController()
        {
            lock (Mutex)
            {
                _StreamList = new HashSet<ThrottledStream>();
                _htId = new Dictionary<string, List<ThrottledStream>>();
                _htMaxSpeed = new Dictionary<string, long>();
                _globalMaxSpeed = 0L;
            }
        }

        private void _RecalcularVelocidad()
        {
            // La velocidad asignada es la menor entre la global y la individual

            // Ejemplos:
            // * Global: 500, individual 50 a 2 IDs, cada uno con 2 streams, total 6 streams:
            // ** Stream 1: 25
            // ** Stream 2: 25
            // ** Stream 3: 25
            // ** Stream 4: 25
            // ** Stream 5: 200 -> Stream sin velocidad individual
            // ** Stream 6: 200 -> Stream sin velocidad individual

            // * Global: 500, individual 150 a 3 IDs, cada uno con 2 streams, total 10 streams:
            // ** La global es menor a la individual (75 vs 50), aplicamos el límite global
            // ** Stream 1: 50
            // ** Stream 2: 50
            // ** Stream 3: 50
            // ** Stream 4: 50
            // ** Stream 5: 50
            // ** Stream 6: 50
            // ** Stream 7: 50 -> Stream sin velocidad individual
            // ** Stream 8: 50 -> Stream sin velocidad individual
            // ** Stream 9: 50 -> Stream sin velocidad individual
            // ** Stream 10: 50 -> Stream sin velocidad individual

            var str = Log.IsDebugEnabled ? new System.Text.StringBuilder() : null;

            lock (Mutex)
            {

                int participants = _StreamList.Count + _accountDownloads.Count;
                if (participants == 0)
                    return;

                bool hasLimit = _globalMaxSpeed > 0L || _htMaxSpeed.Values.Any(speed => speed > 0L);
                if (!hasLimit && !_lastAssignmentHadLimit &&
                    _StreamList.All(stream => stream.MaximumBytesPerSecond == 0L) &&
                    _accountDownloads.All(budget => budget.BytesPerSecond == 0L))
                    return;
                _lastAssignmentHadLimit = hasLimit;

                // Velocidad maxima global de cada stream
                long SpeedGlobalIndividual = (long)Math.Round(_globalMaxSpeed / (double)participants);

                long MaxSpeed;


                long SpeedAsignada = 0L;
                var StreamsConLimiteIndividual = new HashSet<ThrottledStream>();
                foreach (string Id in _htMaxSpeed.Keys)
                {
                    MaxSpeed = _htMaxSpeed[Id];
                    if (MaxSpeed > 0L & _htId.ContainsKey(Id))
                    {

                        int i = 0;
                        foreach (ThrottledStream stream in _htId[Id])
                        {
                            if (_StreamList.Contains(stream))
                            {
                                i += 1;
                            }
                        }
                        if (i == 0)
                            continue;

                        // Se aplica el limite de velocidad

                        // Velocidad maxima individual de cada stream
                        MaxSpeed = (long)Math.Round(MaxSpeed / (double)i);

                        // Miramos que límite aplicar, si el individual o el global
                        if (MaxSpeed > SpeedGlobalIndividual & SpeedGlobalIndividual > 0L)
                            MaxSpeed = SpeedGlobalIndividual;

                        foreach (ThrottledStream stream in _htId[Id])
                        {
                            if (_StreamList.Contains(stream))
                            {
                                StreamsConLimiteIndividual.Add(stream);
                                stream.MaximumBytesPerSecond = MaxSpeed;
                                SpeedAsignada += MaxSpeed;
                            }
                        }

                    }
                }

                int StreamsRestantes = participants - StreamsConLimiteIndividual.Count;
                if (StreamsRestantes > 0)
                {

                    MaxSpeed = (long)Math.Round((_globalMaxSpeed - SpeedAsignada) / (double)StreamsRestantes);
                    if (MaxSpeed < 0L)
                        MaxSpeed = 0L;

                    foreach (ThrottledStream stream in _StreamList)
                    {
                        if (!StreamsConLimiteIndividual.Contains(stream))
                        {
                            stream.MaximumBytesPerSecond = MaxSpeed;
                        }
                    }
                    foreach (AccountDownloadBudget budget in _accountDownloads)
                    {
                        budget.BytesPerSecond = MaxSpeed;
                    }

                }


                if (str is not null)
                {
                    foreach (ThrottledStream stream in _StreamList)
                    {
                        if (str.Length > 0)
                            str.Append(", ");
                        str.Append("[").Append((int)Math.Round(stream.MaximumBytesPerSecond / 1024d)).Append("]");
                    }
                }

            }

            if (str is not null)
                Log.WriteDebug("Speed limit recalculated: " + str.ToString());
        }
        #endregion

        #region Región pública

        /// <summary>Registers one native SDK transfer in the shared speed allocation.</summary>
        public void BeginAccountDownload(AccountDownloadBudget budget)
        {
            lock (Mutex)
            {
                budget.Transfers++;
                if (budget.Transfers > 1)
                    return;
                _accountDownloads.Add(budget);
                _RecalcularVelocidad();
            }
        }

        public void EndAccountDownload(AccountDownloadBudget budget)
        {
            lock (Mutex)
            {
                budget.Transfers--;
                if (budget.Transfers > 0)
                    return;
                _accountDownloads.Remove(budget);
                _RecalcularVelocidad();
            }
        }

        // Llamar cuando creamos un stream nuevo
        public void AddStream(ref ThrottledStream Stream, string Id)
        {
            if (string.IsNullOrEmpty(Id) | Stream is null)
            {
                throw new ArgumentNullException();
            }

            lock (Mutex)
            {
                if (!_StreamList.Contains(Stream))
                {
                    _StreamList.Add(Stream);
                    if (!_htId.ContainsKey(Id))
                    {
                        _htId[Id] = new List<ThrottledStream>();
                    }
                    if (!_htMaxSpeed.ContainsKey(Id))
                    {
                        _htMaxSpeed[Id] = 0L;
                    }
                    _htId[Id].Add(Stream);
                }
            }
            _RecalcularVelocidad();
        }

        // Llamar cuando dejamos de usar un stream
        public void RemoveStream(ref ThrottledStream Stream)
        {
            if (Stream is null)
            {
                throw new ArgumentNullException();
            }


            lock (Mutex)
            {
                if (_StreamList.Contains(Stream))
                {
                    _StreamList.Remove(Stream);
                    string RemoveKey = null;
                    foreach (string key in _htId.Keys)
                    {
                        if (_htId[key].Contains(Stream))
                        {
                            _htId[key].Remove(Stream);
                            if (_htId[key].Count == 0)
                            {
                                RemoveKey = key;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(RemoveKey))
                    {
                        _htId.Remove(RemoveKey);
                    }
                }
            }
            _RecalcularVelocidad();
        }

        // Llamar cuando se establece velocidad máxima global
        public void SetMaxGlobalSpeed(long Bps)
        {
            lock (Mutex)
                _globalMaxSpeed = Bps;
            _RecalcularVelocidad();
        }

        // Llamar cuando se establece velocidad máxima individual
        public void SetMaxSpeed(string Id, long Bps)
        {
            lock (Mutex)
                _htMaxSpeed[Id] = Bps;
            _RecalcularVelocidad();
        }

        // Llamar cuando se elimina un fichero de la cola
        public void RemoveId(string Id)
        {
            if (Id is null)
            {
                throw new ArgumentNullException();
            }


            lock (Mutex)
            {
                if (_htMaxSpeed.ContainsKey(Id))
                {
                    _htMaxSpeed.Remove(Id);
                }
            }
            _RecalcularVelocidad();
        }

        // Llamar cuando se pausen o detengan las descargas, o se cierre el programa
        // Simplemente aborta las pausas introducidas para adecuar la velocidad
        public void Abortar()
        {
            lock (Mutex)
            {
                foreach (ThrottledStream stream in _StreamList)
                    stream.Abort();
            }
        }

        // Llamar cuando se haga una pausa individual
        // Simplemente aborta las pausas introducidas para adecuar la velocidad
        public void Abortar(string Id)
        {
            if (string.IsNullOrEmpty(Id))
            {
                throw new ArgumentNullException();
            }

            lock (Mutex)
            {
                if (_htId.ContainsKey(Id))
                {
                    foreach (ThrottledStream stream in _StreamList)
                    {
                        if (_htId[Id].Contains(stream))
                        {
                            stream.Abort();
                        }
                    }
                }
            }
        }

        // Llamar cuando se vuelven a iniciar las descargas 
        // (para reiniciar el bit de "abortar", sino se queda siempre activo!)
        public void Continuar()
        {
            lock (Mutex)
            {
                foreach (ThrottledStream stream in _StreamList)
                    stream.Continue();
            }
        }

        public void Continuar(string Id)
        {
            if (string.IsNullOrEmpty(Id))
                throw new ArgumentNullException("Id");
            lock (Mutex)
            {
                if (_htId.ContainsKey(Id))
                {
                    foreach (ThrottledStream stream in _htId[Id])
                        stream.Continue();
                }
            }
        }

        #endregion

    }
}