using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml;

namespace MegaDownloader
{

    public class StreamingLibraryManager
    {

        private static StreamingLibrary _StreamingLibrary;
        private static readonly object _StreamingLibraryLock = new object();

        public static StreamingLibrary StreamingLibrary()
        {
            var existing = System.Threading.Volatile.Read(ref _StreamingLibrary);
            if (existing is not null)
                return existing;
            if (_StreamingLibrary is null)
            {
                // First touch can come from concurrent HTTP threads; without this two instances
                // get built and one is discarded, splitting a single operation across both.
                lock (_StreamingLibraryLock)
                {
                    if (_StreamingLibrary is null)
                    {
                        var instance = new StreamingLibrary();
                        instance.LoadXML();
                        // Publish only once fully loaded, so no other thread can observe a
                        // half-initialised instance.
                        System.Threading.Volatile.Write(ref _StreamingLibrary, instance);
                    }
                }
            }
            return System.Threading.Volatile.Read(ref _StreamingLibrary);
        }

        /// <summary>B2: 只在实例已被真正使用过时报告降级状态,避免恢复计时器为了检查
	/// 	''' 状态反而创建出一个从未被用户打开的媒体库实例。</summary>
        public static bool HasPendingRecovery()
        {
            var existing = System.Threading.Volatile.Read(ref _StreamingLibrary);
            return existing is not null && existing.LoadTransientError;
        }

        public static bool TryRecoverPending()
        {
            var existing = System.Threading.Volatile.Read(ref _StreamingLibrary);
            return existing is not null && existing.TryRecoverFromTransientLock();
        }

        public static LibraryElement ProcessByNameAndAddElement(string Name, string Description, string Comments, string Poster, string Link, bool LinkVisible, string IMDB, string Allocine, string Filmaffinity)
        {
            // Movie info services (IMDB/Allocine/Filmaffinity) have been removed because of API changes.
            // Only the ID values are kept for manual cataloging.

            return AddElement(Name, Description, Comments, Poster, Link, LinkVisible, IMDB, Allocine, Filmaffinity);
        }

        public static LibraryElement AddElement(string Name, string Description, string Comments, string Poster, string Link, bool LinkVisible, string IMDB, string Allocine, string Filmaffinity)
        {
            var e = new LibraryElement();
            e.Name = Name;
            e.Description = Description;
            e.Comments = Comments;
            e.IMDB = IMDB;
            e.Allocine = Allocine;
            e.Filmaffinity = Filmaffinity;
            e.Poster = Poster;
            e.LastModification = DateTime.Now;
            e.LinkVisible = LinkVisible;
            if ((Link ?? "") != LibraryElement.HIDDEN_LINK_DESC)
            {
                e.Link = Criptografia.ToSecureString(Link);
            }
            else
            {
                e.Link = new SecureString();
            }

            StreamingLibrary().AddNewElement(e);
            StreamingLibrary().SaveXML();
            return e;
        }

        public static LibraryElement ModifyElement(string ID, string Name, string Description, string Comments, string Poster, string Link, bool LinkVisible, string IMDB, string Allocine, string Filmaffinity)
        {

            var l = from e in StreamingLibrary().Elements()
                    where e.ID == ID
                    select e;
            if (l.Count() > 0)
            {
                var e = l.ToArray()[0];
                e.Name = Name;
                e.Description = Description;
                e.Comments = Comments;
                e.IMDB = IMDB;
                e.Allocine = Allocine;
                e.Filmaffinity = Filmaffinity;
                e.Poster = Poster;
                e.LastModification = DateTime.Now;
                if ((Link ?? "") != LibraryElement.HIDDEN_LINK_DESC)
                {
                    e.Link = Criptografia.ToSecureString(Link);
                    e.LinkVisible = LinkVisible;
                }

                StreamingLibrary().SaveXML();

                return e;
            }
            else
            {
                return null;
            }

        }

        public static void RemoveElement(string ID)
        {
            var l = from e in StreamingLibrary().Elements()
                    where e.ID == ID
                    select e;
            if (l.Count() > 0)
            {
                StreamingLibrary().RemoveElement(l.ToArray()[0]);
                StreamingLibrary().SaveXML();
            }
        }

        public static LibraryElement GetElementByID(string ID)
        {
            var l = from e in StreamingLibrary().Elements()
                    where e.ID == ID
                    select e;
            if (l.Count() > 0)
            {
                return l.ToArray()[0];
            }
            return null;
        }

        public static List<LibraryElement> GetElements()
        {
            return (from e in StreamingLibrary().Elements()
                    orderby e.Name
                    select e).ToList();
        }

        public static bool IsImportedLibrary(string data)
        {
            return GetImportedLibrary(data).Count > 0;
        }

        public static List<string> GetImportedLibrary(string data)
        {
            // Detecta:
            // 1) el código entre "<ExportedXML>" Y "</ExportedXML>" (con los tags incluidos)
            // 2) el código entre "{MEGALIBRARY}" y "{:MEGALIBRARY}" (sin los tags)
            var Libraries = new List<string>();


            var regx = new Regex(@"<(?<Tag>ExportedXML)>(?<Content>.*?)</\1>", RegexOptions.IgnoreCase);
            var matches = regx.Matches(data);

            foreach (Match match in matches)
            {
                string valor = "<ExportedXML>" + match.Groups["Content"].Value + "</ExportedXML>";
                Libraries.Add(valor);
            }

            regx = new Regex(@"[<|{](?<Tag>MEGALIBRARY)[>|}](?<Content>.*?)[<|{][/|:]\1[>|}]", RegexOptions.IgnoreCase);
            matches = regx.Matches(data);

            foreach (Match match in matches)
            {
                string valor = match.Groups["Content"].Value;
                try
                {
                    byte[] bytes = Criptografia.base64urldecodeBytes(valor);
                    valor = UnCompressString(bytes);
                    Libraries.Add(valor);
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error trying to import library [GetImportedLibrary] - error " + ex.ToString() + " - Data: " + valor);
                }

            }
            return Libraries;
        }

        public static int ImportLibrary(string Data)
        {
            int Imported = 0;
            foreach (string library in GetImportedLibrary(Data))
            {
                try
                {
                    // Cada library es un nodo  "<ExportedXML>" ... "</ExportedXML>"
                    // Reject DTDs: imported libraries come from untrusted links and
                    // XmlDocument.LoadXml would otherwise resolve external entities.
                    var xml = new XmlDocument();
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                    using (var reader = XmlReader.Create(new StringReader(library), settings))
                        xml.Load(reader);

                    foreach (XmlNode element in xml.DocumentElement.SelectNodes("Elements/Element"))
                    {
                        var ele = new LibraryElement();
                        ele.LoadXML(element, true);

                        var l = from e in StreamingLibrary().Elements()
                                where Criptografia.ToInsecureString(e.Link).ToUpper().Trim() == Criptografia.ToInsecureString(ele.Link).ToUpper().Trim()
                                select e;
                        if (l.Count() > 0)
                        {
                            continue; // Already exists
                        }

                        ele.LastModification = DateTime.Now;
                        StreamingLibrary().AddNewElement(ele);
                        Imported += 1;
                    }
                    StreamingLibrary().SaveXML();
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error trying to import library [ImportLibrary] - " + Log.SafeException(ex));
                }

            }
            return Imported;
        }

        public static int ImportLinks(ref Configuracion Config, List<string> URLs)
        {

            // Convertimos los links de MegaFolder a links individuales
            var URLs2 = new List<string>();
            foreach (string URL in URLs)
            {
                try
                {
                    if (URLExtractor.IsMegaFolder(URL))
                    {
                        string FolderID = URLExtractor.ExtraerFileID(URL);
                        string FolderKey = URLExtractor.ExtraerFileKey(URL);
                        string SubFolderID = URLExtractor.ExtraerSubFolderID(URL);
                        string SubFileID = URLExtractor.ExtraerSubFileID(URL);
                        foreach (var FileURL in MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey, SubFolderID, SubFileID))
                            URLs2.Add(FileURL.URL);
                    }
                    else if (URLExtractor.IsELC(URL))
                    {
                        Exception ELC_exc = null;
                        foreach (string FileURL in ServerEncoderLinkHelper.ServerDecode(URL, ref Config, ref ELC_exc))
                        {
                            if (URLExtractor.IsMegaFolder(FileURL))
                            {
                                string FolderID = URLExtractor.ExtraerFileID(FileURL);
                                string FolderKey = URLExtractor.ExtraerFileKey(FileURL);
                                string SubFolderID = URLExtractor.ExtraerSubFolderID(FileURL);
                                string SubFileID = URLExtractor.ExtraerSubFileID(FileURL);
                                foreach (var FileURL2 in MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey, SubFolderID, SubFileID))
                                    URLs2.Add(Fichero.HIDDEN_LINK + FileURL2.URL);
                            }
                            else
                            {
                                URLs2.Add(Fichero.HIDDEN_LINK + FileURL);
                            }
                        }
                        // N-P2-5 follow-up: ServerDecode never throws - it swallows the failure into
                        // ELC_exc (ByRef) and returns whatever it managed to decode, possibly nothing.
                        // Without this check a bad ELC imported from the web interface vanished with no
                        // trace at all, so N-P2-5's "fail loudly" only held for the URLProcessor path.
                        // Throwing here lands in the per-link Catch below: it logs the real reason and
                        // keeps the rest of the batch. Links already collected above stay in URLs2.
                        if (ELC_exc is not null)
                            throw ELC_exc;
                    }
                    else
                    {
                        URLs2.Add(URL);
                    }
                }
                catch (Exception ex)
                {
                    // 单条坏链(过期 ELC、读不出文件的文件夹)只跳过该条,不中断整批:
                    // 与 URLProcessor.ProcessURLs 的按条隔离一致。否则一条坏文件夹链接
                    // 抛出的异常会穿过无 Try/Catch 的 ProcesoAjax,导致整个导入请求无响应
                    // (此前 RetrieveLinksFromFolder 永不抛,此处也从不抛)。
                    Log.WriteWarning("ImportLinks: skipping failed link, keeping the rest of the batch: " + Log.Redact(URL) + " - " + Log.SafeException(ex));
                }
            }
            URLs = URLs2;

            // Importamos
            int imported = 0;
            foreach (string URL in URLs)
            {

                string URLFile = URL;
                bool Visible = true;
                if (!string.IsNullOrEmpty(URL) && URL.Contains(Fichero.HIDDEN_LINK))
                {
                    Visible = false;
                    URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "");
                }

                if (string.IsNullOrEmpty(URLFile) || string.IsNullOrEmpty(Fichero.ExtraerFileID(URLFile)))
                {
                    continue;
                }

                string URL2 = URLFile;
                var l = from e in StreamingLibrary().Elements()
                        where Criptografia.ToInsecureString(e.Link).ToUpper().Trim() == URL2.ToUpper().Trim()
                        select e;
                if (l.Count() > 0)
                {
                    continue; // Already exists
                }

                string FileID = Fichero.ExtraerFileID(URLFile);
                string FileKey = Fichero.ExtraerFileKey(URLFile);

                var info = Conexion.ObtenerInformacionFichero(Config, FileID, FileKey, false);
                if (info.Err == Conexion.TipoError.SinErrores)
                {
                    ProcessByNameAndAddElement(info.Nombre, "", "", "", URLFile, Visible, "", "", "");
                }
                else
                {
                    AddElement("Imported element #" + imported, "", "", "", URLFile, Visible, "", "", "");
                }
                imported += 1;

            }
            return imported;
        }



        public static string ExportElements(List<string> IDs, bool HideLinks, bool PlainText)
        {

            var Xml = new XmlDocument();
            var Root = Xml.AppendChild(Xml.CreateElement("ExportedXML"));
            var ElementList = Root.AppendChild(Xml.CreateElement("Elements"));

            foreach (LibraryElement ele in StreamingLibrary().Elements())
            {
                if (IDs.Contains(ele.ID))
                {
                    bool vis = ele.LinkVisible;
                    if (HideLinks)
                        ele.LinkVisible = false;
                    ele.SaveXML(ref ElementList, true);
                    ele.LinkVisible = vis;
                }
            }

            string str = Xml.DocumentElement.OuterXml;
            if (PlainText)
                return str;

            return "{MEGALIBRARY}" + Criptografia.base64urlencode(CompressString(str)) + "{:MEGALIBRARY}";

        }

        private static byte[] CompressString(string str)
        {
            using (var mem = new MemoryStream())
            {
                using (var gz = new System.IO.Compression.GZipStream(mem, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
                {
                    using (var sw = new StreamWriter(gz, System.Text.Encoding.UTF8))
                    {
                        sw.Write(str);
                    }
                }
                return mem.ToArray();
            }
        }

        private static string UnCompressString(byte[] bytes)
        {
            using (var mem2 = new MemoryStream(bytes.ToArray()))
            {
                using (var gz = new System.IO.Compression.GZipStream(mem2, System.IO.Compression.CompressionMode.Decompress))
                {
                    using (var sr = new StreamReader(gz, System.Text.Encoding.UTF8))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }

    }
}