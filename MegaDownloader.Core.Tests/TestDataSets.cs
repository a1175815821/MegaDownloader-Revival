using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace MegaDownloader.Core.Tests
{
    /// <summary>
    /// The three fixed data sets every platform acceptance run starts from. The same
    /// definitions produce each platform's own queue and configuration, because the
    /// encrypted fields are written by the running build rather than copied between
    /// machines.
    /// </summary>
    public static class TestDataSets
    {
        public const int QueuePackageCount = 23;
        public const int QueueFileCount = 679;
        public const int ShareFolderCount = 41;
        public const int ShareFileCount = 500;

        /// <summary>Writes the empty profile used to check a first start.</summary>
        public static void WriteEmptyProfile(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "Config"));
            File.WriteAllText(Path.Combine(root, "ui-settings.json"), "{\"HasSeenGuide\":false}",
                new UTF8Encoding(false));
        }

        /// <summary>
        /// Writes a queue with <see cref="QueueFileCount"/> tasks in
        /// <see cref="QueuePackageCount"/> packages. Sizes, states and progress are fixed,
        /// so a baseline recorded on one platform can be compared with another.
        /// </summary>
        public static void WriteQueueProfile(string root, string downloadFolder)
        {
            Directory.CreateDirectory(Path.Combine(root, "Config"));
            File.WriteAllText(Path.Combine(root, "ui-settings.json"), "{\"HasSeenGuide\":true}",
                new UTF8Encoding(false));
            WriteConfiguration(root, downloadFolder, new Dictionary<string, string>
            {
                ["RutaDefecto"] = downloadFolder,
                ["AnalizarPortapapeles"] = "False",
                ["CrearDirectorioPaquete"] = "True",
                ["ExtraerAutomaticamente"] = "False",
                ["ServidorWebActivo"] = "False",
                ["ServidorStreamingActivo"] = "False",
                // The synthetic links point at nothing; a start-at-launch would turn the
                // fixture into 679 failing tasks instead of a queue to look at.
                ["ComenzarDescargando"] = "False"
            });
            File.WriteAllText(Path.Combine(root, "Config", "DownloadList.xml"), BuildQueueXml(downloadFolder),
                new UTF8Encoding(false));
        }

        /// <summary>
        /// The text to paste into Add Links for the multi-level share. The link itself is
        /// supplied by the operator (a real share is needed); this describes the shape the
        /// preflight must reproduce: <see cref="ShareFolderCount"/> folders and
        /// <see cref="ShareFileCount"/> files, including two identically named subfolders
        /// and non-ASCII file names.
        /// </summary>
        public static string ShareShapeDescription() => string.Join(Environment.NewLine, new[]
        {
            "Expected share shape for the preflight check:",
            "  " + ShareFolderCount.ToString(CultureInfo.InvariantCulture) + " folders, " +
                ShareFileCount.ToString(CultureInfo.InvariantCulture) + " files",
            "  depth 4 (root / season / disc / extras)",
            "  two subfolders named \"Season 1\" under different parents",
            "  file names with spaces, Chinese characters, and one 180-character name",
            "  one folder with a single file and one folder with no files",
            "  one file named like a folder (\"Extras.mkv\") and one folder named like a file (\"sample.mp4\")",
            "Select one second-level folder, clear one file inside it, then compare the",
            "preflight count with the number of tasks the queue received."
        });

        /// <summary>The public single-file link and hash to use for a real transfer run.</summary>
        public static string TransferCaseDescription() => string.Join(Environment.NewLine, new[]
        {
            "Record for the real transfer run:",
            "  link:      <a public single-file MEGA link>",
            "  SHA-256:   <hash of the file, 64 hex characters>",
            "  size:      <bytes>",
            "Then run Tests/Test-PublishedTransfer.ps1 (Windows) or the same checks by hand",
            "on macOS/Linux: queue Complete, progress 100%, one final file, size and hash equal."
        });

        private static void WriteConfiguration(string root, string downloadFolder, Dictionary<string, string> values)
        {
            var document = new XmlDocument();
            document.AppendChild(document.CreateElement("Configuration"));
            foreach (KeyValuePair<string, string> pair in values)
                document.DocumentElement.AppendChild(document.CreateElement(pair.Key)).InnerText = pair.Value;
            string path = Path.Combine(root, "Config", "Configuration.xml");
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (XmlWriter writer = XmlWriter.Create(path, settings))
                document.Save(writer);
        }

        private static string BuildQueueXml(string downloadFolder)
        {
            var document = new XmlDocument();
            document.AppendChild(document.CreateElement("ListaPaquetes"));
            int remaining = QueueFileCount;
            for (int packageIndex = 1; packageIndex <= QueuePackageCount; packageIndex++)
            {
                int files = Math.Min(40, Math.Max(1, remaining - (QueuePackageCount - packageIndex)));
                remaining -= files;
                string packageName = "Release " + packageIndex.ToString("00", CultureInfo.InvariantCulture);
                XmlElement package = document.CreateElement("Paquete");
                document.DocumentElement.AppendChild(package);
                Append(package, document, "Nombre", packageName);
                Append(package, document, "RutaLocal", Path.Combine(downloadFolder, packageName));
                Append(package, document, "CrearSubdirectorio", "True");
                Append(package, document, "ExtraccionFicheroAutomatica", "False");
                Append(package, document, "Prioridad", packageIndex.ToString(CultureInfo.InvariantCulture));
                Append(package, document, "Porcentaje", "0");
                Append(package, document, "TamanoBytes", "0");
                XmlElement list = document.CreateElement("ListaFicheros");
                package.AppendChild(list);
                for (int fileIndex = 1; fileIndex <= files; fileIndex++)
                {
                    string state;
                    decimal percent;
                    switch (fileIndex % 4)
                    {
                        case 1:
                            state = "Completado";
                            percent = 100m;
                            break;
                        case 2:
                            state = "EnCola";
                            percent = 0m;
                            break;
                        case 3:
                            state = "Pausado";
                            percent = 42.5m;
                            break;
                        default:
                            state = "EnCola";
                            percent = 0m;
                            break;
                    }
                    long bytes = 1024L * 1024L * (5 + fileIndex % 97) + packageIndex * 7919L;
                    XmlElement file = document.CreateElement("Fichero");
                    file.SetAttribute("v", "2");
                    list.AppendChild(file);
                    Append(file, document, "FileID", SyntheticHandle(packageIndex, fileIndex));
                    Append(file, document, "URL", "https://mega.nz/file/" +
                        SyntheticHandle(packageIndex, fileIndex) + "#" + SyntheticKey(packageIndex, fileIndex));
                    Append(file, document, "NombreFichero", string.Format(CultureInfo.InvariantCulture,
                        "{0} part {1:000}.bin", packageName.Replace(" ", "-"), fileIndex));
                    Append(file, document, "RutaLocal", Path.Combine(downloadFolder, packageName));
                    Append(file, document, "RutaRelativa", "disc " + ((fileIndex - 1) / 10 + 1).ToString(CultureInfo.InvariantCulture));
                    Append(file, document, "TamanoBytes", bytes.ToString(CultureInfo.InvariantCulture));
                    Append(file, document, "Porcentaje", percent.ToString(CultureInfo.InvariantCulture));
                    Append(file, document, "EstadoDescarga", state);
                    Append(file, document, "DescargaComenzada", (state != "EnCola").ToString());
                    Append(file, document, "DescargaProcesada", "True");
                    Append(file, document, "Prioridad", "0");
                }
            }
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using var buffer = new MemoryStream();
            using (XmlWriter writer = XmlWriter.Create(buffer, settings))
                document.Save(writer);
            return settings.Encoding.GetString(buffer.ToArray());
        }

        private static void Append(XmlElement parent, XmlDocument document, string name, string value) =>
            parent.AppendChild(document.CreateElement(name)).InnerText = value;

        // Handles and keys are synthetic: the queue never contacts MEGA for a task whose
        // link was never verified, and these only need to be stable and unique.
        private static string SyntheticHandle(int package, int file)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                "handle:" + package.ToString(CultureInfo.InvariantCulture) + ":" +
                file.ToString(CultureInfo.InvariantCulture)));
            return Criptografia.base64urlencode(bytes.Take(6).ToArray());
        }

        private static string SyntheticKey(int package, int file)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                "key:" + package.ToString(CultureInfo.InvariantCulture) + ":" +
                file.ToString(CultureInfo.InvariantCulture)));
            return Criptografia.base64urlencode(bytes.Concat(bytes).Take(32).ToArray());
        }
    }
}
