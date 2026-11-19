using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public class StreamingLibrary
    {

        private List<LibraryElement> _LibraryElementList;
        private int _NextID;
        private readonly object _sync = new object();
        private readonly object _loadSync = new object();
        private readonly object _saveSync = new object();
        private readonly string _sourcePath;
        private string _transientBlockedSource;
        /// <summary>加载时文件被瞬时占用(杀软/备份/同步盘)而不是真的损坏。磁盘上的库可能
    /// 完好,此时内存里却是空库 —— 任何写盘都会用"空库+新增一条"覆盖掉用户的整库,
    /// 原件还会被 File.Replace 挤进 .bak,下次保存连 .bak 一起毁掉。
    /// 置位后 SaveXML 一律拒写,保住磁盘文件(与本轮 Configuracion/Paquete 的 P0-3 同策略)。</summary>
        private bool _LoadTransientError;


        public StreamingLibrary() : this(null)
        {
        }

        internal StreamingLibrary(string sourcePath)
        {
            _sourcePath = sourcePath is null ? null : Path.GetFullPath(sourcePath);
            Init();
        }

        public void Init()
        {
            lock (_sync)
            {
                _LibraryElementList = new List<LibraryElement>();
                _NextID = 1;
            }
        }

        public List<LibraryElement> Elements()
        {
            lock (_sync)
                return new List<LibraryElement>(_LibraryElementList);
        }

        // Elements() deliberately hands back a copy, so mutating through it is a silent no-op
        // that SaveXML would then persist as "nothing changed". Callers must use these instead.
        public void AddElement(LibraryElement element)
        {
            lock (_sync)
                _LibraryElementList.Add(element);
        }

        public void AddNewElement(LibraryElement element)
        {
            lock (_sync)
            {
                element.ID = _NextID.ToString();
                _NextID += 1;
                _LibraryElementList.Add(element);
            }
        }

        public bool RemoveElement(LibraryElement element)
        {
            lock (_sync)
                return _LibraryElementList.Remove(element);
        }

        public void LoadXML()
        {
            lock (_loadSync)
            {
                string filePath = _sourcePath ?? ObtenerRutaFicheroConfiguracion();
                XmlDocument document = null;
                bool recovered = false;
                bool transientFailure = false;
                lock (Mutex.GuardarConfig)
                {
                    if (!AtomicFile.TryLoadXmlWithRecoveryInfo(filePath, ref document, ref recovered, ref transientFailure, ref _transientBlockedSource, AtomicFile.StartupAttempts, AtomicFile.StartupRetryDelayMs))
                    {
                        lock (_sync)
                            System.Threading.Volatile.Write(ref _LoadTransientError, transientFailure);
                        if (transientFailure)
                            Log.WriteError("Streaming library is locked; writes are suspended: " + filePath);
                        return;
                    }
                }
                if (recovered)
                {
                    Log.WriteWarning("Streaming library restored from backup.");
                }
                var nextID = default(int);
                var elements = ParseLibrary(document, ref nextID);
                lock (_sync)
                {
                    _LibraryElementList = elements;
                    _NextID = nextID;
                    System.Threading.Volatile.Write(ref _LoadTransientError, false);
                }
            }
        }

        private static List<LibraryElement> ParseLibrary(XmlDocument document, ref int nextID)
        {
            var elements = new List<LibraryElement>();
            nextID = 1;
            var idAttribute = document.DocumentElement.SelectSingleNode("Elements/@nextID");
            if (idAttribute is not null && Information.IsNumeric(idAttribute.Value))
                nextID = Conversions.ToInteger(idAttribute.Value);
            foreach (XmlNode node in document.DocumentElement.SelectNodes("Elements/Element"))
            {
                var element = new LibraryElement();
                element.LoadXML(node, false);
                elements.Add(element);
            }
            return elements;
        }

        /// <summary>B2: 主窗体恢复计时器用来判断媒体库是否仍处于加载降级状态。</summary>
        public bool LoadTransientError
        {
            get
            {
                return System.Threading.Volatile.Read(ref _LoadTransientError);
            }
        }

        public bool TryRecoverFromTransientLock()
        {
            return RecoverUsing(() =>
                {
                    XmlDocument document = null;
                    string filePath = _sourcePath ?? ObtenerRutaFicheroConfiguracion();
                    AtomicFile.TryReloadAfterTransientFailure(filePath, _transientBlockedSource, ref document);
                    return document;
                });
        }

        internal bool RecoverUsing(Func<XmlDocument> loadDocument)
        {
            lock (_loadSync)
            {
                if (!LoadTransientError)
                    return false;
                try
                {
                    var document = loadDocument();
                    if (document is null)
                        return false;
                    var nextID = default(int);
                    var elements = ParseLibrary(document, ref nextID);
                    // Merge the current live list at commit time. HTTP requests can add
                    // entries while the persisted document is being read and parsed.
                    lock (_sync)
                    {
                        foreach (LibraryElement element in _LibraryElementList)
                        {
                            element.ID = nextID.ToString();
                            nextID += 1;
                            elements.Add(element);
                        }
                        _LibraryElementList = elements;
                        _NextID = nextID;
                        _transientBlockedSource = null;
                        System.Threading.Volatile.Write(ref _LoadTransientError, false);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("Streaming library recovery failed: " + Log.SafeException(ex));
                    return false;
                }
                SaveXML();
                Log.WriteInfo("Streaming library recovered after the on-disk file was unlocked.");
                return true;
            }
        }

        public void SaveXML()
        {
            // Serialize writers before taking a snapshot. A save prepared during
            // recovery must never overwrite the merged collection with an older view.
            lock (_saveSync)
            {
                var document = new XmlDocument();
                var root = document.AppendChild(document.CreateElement("XML"));
                var elementList = root.AppendChild(document.CreateElement("Elements"));
                lock (_sync)
                {
                    if (_LoadTransientError)
                    {
                        Log.WriteWarning("Skipping streaming library save: the persisted library is still locked.");
                        return;
                    }
                    elementList.Attributes.Append(document.CreateAttribute("nextID")).Value = _NextID.ToString();
                    foreach (LibraryElement element in _LibraryElementList)
                        element.SaveXML(ref elementList, false);
                }
                lock (Mutex.GuardarConfig)
                {
                    try
                    {
                        AtomicFile.SaveXml(document, _sourcePath ?? ObtenerRutaFicheroConfiguracion());
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error saving streaming library: " + Log.SafeException(ex));
                    }
                }
            }
        }

        private static string ObtenerRutaFicheroConfiguracion()
        {

            string PathLog = Path.Combine(ApplicationDataDirectory.GetLegacyRoot(), "Library");

            if (!Directory.Exists(PathLog))
            {
                Directory.CreateDirectory(PathLog);
            }
            PathLog = Path.Combine(PathLog, "StreamingLibrary.xml");
            return PathLog;
        }


    }
}