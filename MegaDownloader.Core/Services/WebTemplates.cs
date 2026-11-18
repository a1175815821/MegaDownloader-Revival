using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MegaDownloader
{
    /// <summary>
    /// Pages served by the built-in web servers. The originals lived in the WinForms
    /// project's <c>HttpModule\Template</c> folder, which was never committed, so both
    /// servers failed at start-up with "Error template not found". They are embedded in
    /// MegaDownloader.Core now and read from there.
    /// </summary>
    internal static class WebTemplates
    {
        private const string ResourcePrefix = "MegaDownloader.Web.";
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly object CacheLock = new object();

        internal static string Read(string fileName)
        {
            lock (CacheLock)
            {
                if (Cache.TryGetValue(fileName, out string cached))
                    return cached;
            }

            string text = null;
            var assembly = typeof(WebTemplates).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName))
            {
                if (stream is not null)
                {
                    using var reader = new StreamReader(stream);
                    text = reader.ReadToEnd();
                }
            }

            lock (CacheLock)
                Cache[fileName] = text;
            return text;
        }

        internal static string ReadRequired(string fileName)
        {
            string text = Read(fileName);
            if (string.IsNullOrEmpty(text))
                throw new ApplicationException("Web template not found: " + fileName);
            return text;
        }

        /// <summary>The remote controller's XML template (HtmlTemplate, FavIcon, CSS, bodies).</summary>
        internal static XmlDocument LoadRemoteControlTemplate(string overridePath)
        {
            var document = new XmlDocument();
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                document.Load(overridePath);
                return document;
            }

            string xml = ReadRequired("XmlTemplateData.xml");
            document.LoadXml(xml);
            return document;
        }
    }
}
