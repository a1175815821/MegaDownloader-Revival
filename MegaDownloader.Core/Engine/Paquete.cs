using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MegaDownloader
{

    public class Paquete : IDescarga
    {

        public Paquete()
        {
        }

        public string Nombre;

        public string RutaLocal;

        public bool CrearSubdirectorio;

        public bool PendienteNombrePaquete;

        public List<Fichero> ListaFicheros;

        public bool ExtraccionFicheroAutomatica;

        public string ExtraccionFicheroPassword;

        public int Prioridad;

        private Estado EstadoDescarga;

        public decimal Porcentaje;

        public long TamanoBytes;

        private decimal VelocidadKBs;

        private string TiempoEstimadoDescarga;

        internal void ApplyVerifiedName(Fichero file, bool firstStart)
        {
            lock (Mutex.ListaDescargas)
            {
                if (!PendienteNombrePaquete || ListaFicheros is null || !ListaFicheros.Contains(file))
                    return;
                string name = ListaFicheros.Count == 1 ? file.ObtenerNombreSinExtension() : null;
                if (string.IsNullOrWhiteSpace(name))
                    name = Language.GetText("New package");
                name = PathGuard.SanitizeFileName(name, "package");
                if (ListaFicheros.Count == 1 && CrearSubdirectorio && firstStart)
                {
                    string folder = PathGuard.GetSafePathUnderRoot(RutaLocal, name, allowRoot: false);
                    string destination = PathGuard.GetSafePathUnderRoot(folder, file.RutaRelativa ?? "", allowRoot: true);
                    Directory.CreateDirectory(destination);
                    RutaLocal = folder;
                    file.RutaLocal = destination;
                }
                Nombre = name;
                PendienteNombrePaquete = false;
            }
        }

        private bool HayFicheros()
        {
            lock (Mutex.ListaDescargas)
                return ListaFicheros is not null && ListaFicheros.Count > 0;
        }

        public void AgregarFichero(Fichero Fichero)
        {
            lock (Mutex.ListaDescargas)
            {
                if (ListaFicheros is null)
                {
                    ListaFicheros = new List<Fichero>();
                }
                ListaFicheros.Add(Fichero);
            }
        }

        /// <summary>
    /// 返回本包文件引用的快照，供 430ms 后台刷新循环使用：短锁拷贝后，
    /// 逐文件的重活（Downloader 属性读取、ETA 格式化）在锁外执行，
    /// 不再把全局 ListaDescargas 锁 holds 住几百个文件的全程。
    /// 快照与实时列表最多差一个刷新周期，计数类 UI 本就按节拍刷新，可接受。
    /// </summary>
        public List<Fichero> SnapshotFicheros()
        {
            lock (Mutex.ListaDescargas)
            {
                if (ListaFicheros is null)
                    return new List<Fichero>();
                return new List<Fichero>(ListaFicheros);
            }
        }

        public void ActualizarDatosDescarga()
        {

            if (!HayFicheros())
            {
                EstadoDescarga = Estado.EnCola;
                Porcentaje = 0m;
                TamanoBytes = 0L;
                VelocidadKBs = 0m;
            }
            else
            {

                decimal TotalVelocidad = 0m;
                bool HayDescargando = false;
                bool HayCompletado = false;
                bool HayEncola = false;
                bool HayErroneos = false;
                bool HayGenerandoLocal = false;
                bool HayPausados = false;
                bool HayDescomprimiendo = false;
                bool HayEsperandoVolumenes = false;
                bool HayComprobandoMD5 = false;
                bool HayVerificando = false;
                long Total = 0L;
                long TotalDesc = 0L;

                lock (Mutex.ListaDescargas)
                {
                    foreach (Fichero fichero in ListaFicheros)
                    {
                        Total += fichero.DescargaTamanoBytes();
                        TotalDesc += (long)Math.Round(fichero.DescargaTamanoBytes() * (fichero.DescargaPorcentaje() / 100m));
                        switch (fichero.DescargaEstado())
                        {
                            case Estado.Descargando:
                                TotalVelocidad += fichero.DescargaVelocidadKBs();
                                HayDescargando = true;
                                break;
                            case Estado.Completado:
                                HayCompletado = true;
                                break;
                            case Estado.EnCola:
                                HayEncola = true;
                                break;
                            case Estado.Pausado:
                                HayPausados = true;
                                break;
                            case Estado.ComprobandoMD5:
                                HayComprobandoMD5 = true;
                                break;
                            case Estado.Descomprimiendo:
                                HayDescomprimiendo = true;
                                break;
                            case Estado.EsperandoVolumenes:
                                HayEsperandoVolumenes = true;
                                break;
                            case Estado.Verificando:
                                HayVerificando = true;
                                break;
                            case Estado.CreandoLocal:
                                HayGenerandoLocal = true;
                                break;
                            default:
                                HayErroneos = true;
                                break;
                        }
                    }
                }
                if (HayDescargando)
                {
                    EstadoDescarga = Estado.Descargando;
                }
                else if (HayPausados)
                {
                    EstadoDescarga = Estado.Pausado;
                }
                else if (HayErroneos)
                {
                    EstadoDescarga = Estado.Erroneo;
                }
                else if (HayGenerandoLocal)
                {
                    EstadoDescarga = Estado.CreandoLocal;
                }
                else if (HayEncola)
                {
                    EstadoDescarga = Estado.EnCola;
                }
                else if (HayComprobandoMD5)
                {
                    EstadoDescarga = Estado.ComprobandoMD5;
                }
                else if (HayDescomprimiendo)
                {
                    EstadoDescarga = Estado.Descomprimiendo;
                }
                else if (HayEsperandoVolumenes)
                {
                    EstadoDescarga = Estado.EsperandoVolumenes;
                }
                else if (HayVerificando)
                {
                    EstadoDescarga = Estado.Verificando;
                }
                else
                {
                    EstadoDescarga = Estado.Completado;
                }

                TamanoBytes = Total;
                VelocidadKBs = Math.Round(TotalVelocidad, 2);

                if (TamanoBytes == 0L)
                {
                    Porcentaje = 0m;
                }
                else
                {
                    Porcentaje = (decimal)(100L * TotalDesc / (double)Total);
                }
            }

            if (VelocidadKBs == 0m)
            {
                TiempoEstimadoDescarga = " --- ";
            }
            else
            {
                long BytesRestantes = TamanoBytes - (long)Math.Round(Porcentaje * TamanoBytes / 100m);
                double SegundosRestantes = (double)(BytesRestantes / (1024m * VelocidadKBs));
                var t = TimeSpan.FromSeconds(SegundosRestantes);

                if (SegundosRestantes > 3600d)
                {
                    TiempoEstimadoDescarga = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", t.Days * 24 + t.Hours, t.Minutes, t.Seconds);
                }
                else
                {
                    TiempoEstimadoDescarga = string.Format("{0:D2}m:{1:D2}s", t.Minutes, t.Seconds);
                }
            }

        }

        public int DescargaPrioridad()
        {
            return Prioridad;
        }

        public int SetDescargaPrioridad
        {
            set
            {
                Prioridad = value;
            }
        }

        public void set_SetDescargaExtraccionAutomatica(string Password, bool value)
        {
            ExtraccionFicheroAutomatica = value;
            ExtraccionFicheroPassword = Password;
        }

        public string DescargaNombre()
        {
            return Nombre;
        }

        public Estado DescargaEstado()
        {
            return EstadoDescarga;
        }

        public bool DescargaExtraccionAutomatica()
        {
            return ExtraccionFicheroAutomatica;
        }

        public string DescargaExtraccionPassword()
        {
            return ExtraccionFicheroPassword;
        }

        public string DescargaTiempoEstimadoDescarga()
        {
            return TiempoEstimadoDescarga;
        }

        public decimal DescargaPorcentaje()
        {
            return Porcentaje;
        }

        public long DescargaTamanoBytes()
        {
            return TamanoBytes;
        }

        public decimal DescargaVelocidadKBs()
        {
            return VelocidadKBs;
        }


        internal static List<Paquete> CargarStartupQueue(CancellationToken cancellationToken)
        {
            return CargarDesdeRutaCancelable(ObtenerRutaFicheroDescargas(), cancellationToken);
        }

        internal static List<Paquete> CargarDesdeRuta(string Fichero)
        {
            return CargarDesdeRutaCancelable(Fichero, CancellationToken.None);
        }

        internal static List<Paquete> CargarDesdeRutaCancelable(string Fichero, CancellationToken cancellationToken)
        {
            return ReadQueue(Fichero, AtomicFile.StartupAttempts, AtomicFile.StartupRetryDelayMs, cancellationToken, recovering: false);
        }

        private static List<Paquete> ReadQueue(string filePath, int attempts, int delayMs, CancellationToken cancellationToken, bool recovering)
        {
            string fullPath = Path.GetFullPath(filePath);
            List<Paquete> packages = null;
            var primary = ReadQueueFile(fullPath, attempts, delayMs, cancellationToken, ref packages);
            var backup = AtomicFile.XmlReadState.Missing;
            if (primary != AtomicFile.XmlReadState.Loaded && primary != AtomicFile.XmlReadState.Blocked)
            {
                backup = ReadQueueFile(fullPath + ".bak", attempts, delayMs, cancellationToken, ref packages);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                DisposeLoadedQueue(packages);
                cancellationToken.ThrowIfCancellationRequested();
            }
            bool loadedBackup = backup == AtomicFile.XmlReadState.Loaded;
            bool blocked = primary == AtomicFile.XmlReadState.Blocked || backup == AtomicFile.XmlReadState.Blocked;
            bool invalid = primary == AtomicFile.XmlReadState.Invalid || backup == AtomicFile.XmlReadState.Invalid;
            lock (Mutex.GuardarDownloadList)
            {
                if (packages is not null)
                {
                    _PreserveBackupOnNextSave = loadedBackup;
                }
                else if (blocked || invalid || recovering)
                {
                    bool corrupt = invalid && !blocked;
                    if (!_LoadTransientError || corrupt != _LoadCorruptError)
                    {
                        Log.WriteWarning(corrupt ? "Download list could not be read as a valid queue. Writes are suspended." : "Download list is unavailable. Writes are suspended.");
                    }
                    Volatile.Write(ref _LoadCorruptError, corrupt);
                    Volatile.Write(ref _LoadTransientError, true);
                    return recovering ? null : new List<Paquete>();
                }
                else
                {
                    _PreserveBackupOnNextSave = false;
                }
                if (!recovering)
                    ClearQueueWriteProtection();
            }

            if (packages is null)
                return new List<Paquete>();
            if (loadedBackup)
                Log.WriteWarning("Download list restored from backup file.");
            MarcarFicherosComoParados(ref packages);
            return packages;
        }

        private static AtomicFile.XmlReadState ReadQueueFile(string filePath, int attempts, int delayMs, CancellationToken cancellationToken, ref List<Paquete> packages)
        {
            XmlDocument xml = null;
            var state = AtomicFile.ReadXml(filePath, ref xml, attempts, delayMs, cancellationToken);
            if (state != AtomicFile.XmlReadState.Loaded)
                return state;
            if (xml.DocumentElement is null || xml.DocumentElement.Name != "ListaPaquetes" || xml.DocumentElement.SelectSingleNode("*[not(self::Paquete)]") is not null)
                return AtomicFile.XmlReadState.Invalid;
            var loaded = new List<Paquete>();
            try
            {
                foreach (XmlNode node in xml.DocumentElement.SelectNodes("Paquete"))
                {
                    var package = new Paquete();
                    loaded.Add(package);
                    package.CargarXMLCancelable(node, cancellationToken, 4);
                }
                cancellationToken.ThrowIfCancellationRequested();
                packages = loaded;
                return AtomicFile.XmlReadState.Loaded;
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException || ex is InvalidDataException)
            {
                DisposeLoadedQueue(loaded);
                return AtomicFile.XmlReadState.Invalid;
            }
            catch
            {
                DisposeLoadedQueue(loaded);
                throw;
            }
        }

        private static void MarcarFicherosComoParados(ref List<Paquete> ListaPaquetes)
        {
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete Paquete in ListaPaquetes)
                {
                    foreach (Fichero Fichero in Paquete.ListaFicheros)
                    {
                        if (Fichero.DescargaEstado() == Estado.Descargando | Fichero.DescargaEstado() == Estado.Pausado | Fichero.DescargaEstado() == Estado.Verificando | Fichero.DescargaEstado() == Estado.Descomprimiendo | Fichero.DescargaEstado() == Estado.EsperandoVolumenes | Fichero.DescargaEstado() == Estado.CreandoLocal | Fichero.DescargaEstado() == Estado.ComprobandoMD5)





                        {
                            // Verificando 是下载前的瞬态(EstadoAnterior 已丢失),Descomprimiendo 是下载完成但解压未完成:
                            // 两者都不能标 Completado,否则“一字节没解也报成功”。统一回 EnCola,靠断点续传继续,
                            // 已完整的文件只会做快速校验,不会从零重下。
                            // CreandoLocal/ComprobandoMD5 同为瞬态且持久化后会被原样读回:漏掉它们时,
                            // 关机窗口(API 验证 60s 超时/强杀/断电)内存盘的任务重启后既不会被调度
                            // (调度只认 EnCola)又占着 DescargasSimultaneas 名额,ForzarDescarga 也救不回,
                            // 队列永久停摆;ComprobandoMD5 还会被当成"已完成"。必须一并归一化。
                            Fichero.SetDescargaEstado = Estado.EnCola;
                        }
                        // ③:存量 KB→控制器字节(×1024),否则历史限速全部 1/1024。
                        ThrottledStreamController.GetController().SetMaxSpeed(Fichero.FileID, Fichero.LimiteVelocidad * 1024L);
                    }
                }
            }
        }

        private static string _LastSavedXML = null;
        private static bool _LoadTransientError;
        private static bool _LoadCorruptError;
        private static bool _TransientSkipLogged;
        private static bool _PreserveBackupOnNextSave;

        public static bool HasTransientLoadError
        {
            get
            {
                return Volatile.Read(ref _LoadTransientError);
            }
        }

        public static bool HasCorruptLoadError
        {
            get
            {
                return Volatile.Read(ref _LoadCorruptError);
            }
        }

        public static List<Paquete> TryReloadAfterTransientLock()
        {
            if (!HasTransientLoadError)
                return null;
            return TryReloadFromPath(ObtenerRutaFicheroDescargas());
        }

        internal static List<Paquete> TryReloadFromPath(string filePath)
        {
            if (!HasTransientLoadError)
                return null;
            return ReadQueue(filePath, 1, 0, CancellationToken.None, recovering: true);
        }

        public static void ArmTransientWriteProtection()
        {
            lock (Mutex.GuardarDownloadList)
                Volatile.Write(ref _LoadTransientError, true);
        }

        private static void ClearQueueWriteProtection()
        {
            _LastSavedXML = null;
            _TransientSkipLogged = false;
            Volatile.Write(ref _LoadCorruptError, false);
            Volatile.Write(ref _LoadTransientError, false);
        }

        public static void CommitTransientRecovery()
        {
            lock (Mutex.GuardarDownloadList)
                ClearQueueWriteProtection();
        }

        public static void GuardarEnFichero(List<Paquete> ListaPaquetes)
        {
            GuardarEnRuta(ObtenerRutaFicheroDescargas(), ListaPaquetes);
        }

        internal static void GuardarEnRuta(string filePath, List<Paquete> packages)
        {
            if (HasTransientLoadError)
            {
                lock (Mutex.GuardarDownloadList)
                {
                    if (!_TransientSkipLogged)
                    {
                        _TransientSkipLogged = true;
                        Log.WriteWarning("Skipping download list save until the on-disk queue can be recovered.");
                    }
                }
                return;
            }

            string snapshot = GuardarXML(packages, false).DocumentElement.OuterXml;
            lock (Mutex.GuardarDownloadList)
            {
                if (_LoadTransientError || (_LastSavedXML ?? "") == (snapshot ?? ""))
                    return;
            }
            var xml = GuardarXML(packages, true);

            lock (Mutex.GuardarDownloadList)
            {
                if (_LoadTransientError || (_LastSavedXML ?? "") == (snapshot ?? ""))
                    return;
                try
                {
                    AtomicFile.SaveXml(xml, filePath, preserveBackup: _PreserveBackupOnNextSave);
                    _PreserveBackupOnNextSave = false;
                    _LastSavedXML = snapshot;
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error saving download list XML: " + Log.SafeException(ex));
                }
            }
        }

        private static XmlDocument GuardarXML(List<Paquete> ListaPaquetes, bool IncluirDatosCifrados)
        {
            var XML = new XmlDocument();
            XML.AppendChild(XML.CreateElement("ListaPaquetes"));

            if (ListaPaquetes is not null)
            {
                foreach (Paquete Paquete in ListaPaquetes)
                    XML.DocumentElement.AppendChild(Paquete.GuardarXML(XML, IncluirDatosCifrados));
            }
            return XML;
        }


        private static string ObtenerRutaFicheroDescargas()
        {

            string PathLog = Path.Combine(ApplicationDataDirectory.GetLegacyRoot(), "Config");

            if (!Directory.Exists(PathLog))
            {
                Directory.CreateDirectory(PathLog);
            }
            PathLog = Path.Combine(PathLog, "DownloadList.xml");
            return PathLog;
        }




        public void CargarXML(XmlNode XML)
        {
            CargarXMLCancelable(XML, CancellationToken.None, 4);
        }

        internal void CargarXMLCancelable(XmlNode XML, CancellationToken cancellationToken, int maxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string argPath = "Nombre";
            Nombre = LeerNodo(ref XML, ref argPath, "");
            string argPath1 = "RutaLocal";
            RutaLocal = LeerNodo(ref XML, ref argPath1, "");
            string argPath2 = "CrearSubdirectorio";
            bool.TryParse(LeerNodo(ref XML, ref argPath2, "false"), out CrearSubdirectorio);
            string argPath3 = "PendienteNombrePaquete";
            bool.TryParse(LeerNodo(ref XML, ref argPath3, "false"), out PendienteNombrePaquete);
            string argPath4 = "ExtraccionFicheroAutomatica";
            bool.TryParse(LeerNodo(ref XML, ref argPath4, "false"), out ExtraccionFicheroAutomatica);
            string argPath5 = "ExtraccionFicheroPassword";
            string tempPass = LeerNodo(ref XML, ref argPath5, "");
            if (!string.IsNullOrEmpty(tempPass))
            {
                ExtraccionFicheroPassword = Criptografia.AES_DecryptString(tempPass, "passZIP");
            }


            string argPath6 = "Prioridad";
            int.TryParse(LeerNodo(ref XML, ref argPath6, "0"), out Prioridad);
            string argPath7 = "Porcentaje";
            decimal.TryParse(LeerNodo(ref XML, ref argPath7, "0"), out Porcentaje);
            string argPath8 = "TamanoBytes";
            long.TryParse(LeerNodo(ref XML, ref argPath8, "0"), out TamanoBytes);

            var nodes = XML.SelectNodes("ListaFicheros/Fichero");
            var loaded = new Fichero[nodes.Count];
            int concurrency = Math.Max(1, Math.Min(4, Math.Min(Environment.ProcessorCount, maxConcurrency)));
            try
            {
                if (nodes.Count < 128 || concurrency == 1)
                {
                    for (int index = 0, loopTo = nodes.Count - 1; index <= loopTo; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        loaded[index] = new Fichero();
                        loaded[index].CargarXML(nodes[index]);
                    }
                }
                else
                {
                    // Each worker owns its DOM and secure buffers; preserve the original
                    // file order and publish the list only after every worker has finished.
                    var isolated = new XmlNode[nodes.Count];
                    for (int index = 0, loopTo1 = nodes.Count - 1; index <= loopTo1; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var owner = new XmlDocument();
                        owner.AppendChild(owner.ImportNode(nodes[index], true));
                        isolated[index] = owner.DocumentElement;
                    }
                    Parallel.For(0, nodes.Count, new ParallelOptions() { MaxDegreeOfParallelism = concurrency, CancellationToken = cancellationToken }, index =>
    {
        loaded[index] = new Fichero();
        loaded[index].CargarXML(isolated[index]);
    });
                }
                cancellationToken.ThrowIfCancellationRequested();
                ListaFicheros = new List<Fichero>(loaded);
            }
            catch
            {
                foreach (Fichero @file in loaded)
                    DisposeLoadedFile(@file);
                throw;
            }


        }

        internal static void DisposeLoadedQueue(IEnumerable<Paquete> packages)
        {
            if (packages is null)
                return;
            foreach (Paquete package in packages)
            {
                if (package.ListaFicheros is null)
                    continue;
                foreach (Fichero @file in package.ListaFicheros)
                    DisposeLoadedFile(@file);
            }
        }

        private static void DisposeLoadedFile(Fichero @file)
        {
            if (@file is null)
                return;
            @file.Dispose();
            if (@file._FileID is not null)
                @file._FileID.Dispose();
            if (@file._FileKey is not null)
                @file._FileKey.Dispose();
            if (@file._URL is not null)
                @file._URL.Dispose();
            if (@file._URLFichero is not null)
                @file._URLFichero.Dispose();
        }

        public XmlNode GuardarXML(XmlDocument XML, bool IncluirDatosCifrados)
        {
            XmlNode NodoPaq = XML.CreateElement("Paquete");

            NodoPaq.AppendChild(XML.CreateElement("Nombre")).InnerText = Nombre;
            NodoPaq.AppendChild(XML.CreateElement("RutaLocal")).InnerText = RutaLocal;
            NodoPaq.AppendChild(XML.CreateElement("CrearSubdirectorio")).InnerText = CrearSubdirectorio.ToString();
            NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroAutomatica")).InnerText = ExtraccionFicheroAutomatica.ToString();
            // 密文(随机 IV,每次不同)不得进入 IncluirDatosCifrados=False 的去重比对快照,
            // 否则 _LastSavedXML 恒不相等,队列每 5 秒全量落盘(WriteThrough+fsync+Replace+.bak)。
            // 与 Configuracion 的 B2-⑦/P0-4 处理一致:只写真盘,不参与比对。
            if (IncluirDatosCifrados && !string.IsNullOrEmpty(ExtraccionFicheroPassword))
            {
                string encryptedZipPass = Criptografia.AES_EncryptString(ExtraccionFicheroPassword, "passZIP");
                if (encryptedZipPass is not null)
                {
                    NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroPassword")).InnerText = encryptedZipPass;
                }
            }
            if (!IncluirDatosCifrados)
            {
                NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroPasswordHash")).InnerText = Utils.SecretFingerprint(ExtraccionFicheroPassword);
            }
            NodoPaq.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString();
            NodoPaq.AppendChild(XML.CreateElement("Porcentaje")).InnerText = Porcentaje.ToString();
            NodoPaq.AppendChild(XML.CreateElement("TamanoBytes")).InnerText = TamanoBytes.ToString();
            if (PendienteNombrePaquete)
                NodoPaq.AppendChild(XML.CreateElement("PendienteNombrePaquete")).InnerText = PendienteNombrePaquete.ToString();


            // P-01 follow-up: GuardarEnFichero now serializes outside Mutex.ListaDescargas.
            // AgregarFichero mutates ListaFicheros under that lock, so a bare For Each here
            // could throw "Collection was modified" while a package was being imported,
            // killing the periodic disk-save worker for the rest of the session. Take a
            // short locked reference snapshot; the (slower) per-file work stays outside.
            List<Fichero> ficherosSnapshot = null;
            lock (Mutex.ListaDescargas)
            {
                if (ListaFicheros is not null)
                {
                    ficherosSnapshot = new List<Fichero>(ListaFicheros);
                }
            }

            if (ficherosSnapshot is not null)
            {
                var nodoListaFic = NodoPaq.AppendChild(XML.CreateElement("ListaFicheros"));
                foreach (Fichero Fichero in ficherosSnapshot)
                    nodoListaFic.AppendChild(Fichero.GuardarXML(XML, IncluirDatosCifrados));
            }

            return NodoPaq;

        }

        private static string LeerNodo(ref XmlNode NodoXML, ref string Path, string ValorDefecto)
        {
            var nodo = NodoXML.SelectSingleNode(Path);
            if (nodo is null)
            {
                return ValorDefecto;
            }
            else
            {
                return nodo.InnerText;
            }
        }

    }
}