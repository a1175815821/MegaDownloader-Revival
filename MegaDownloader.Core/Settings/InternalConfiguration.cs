using System;
using System.Xml;

namespace MegaDownloader
{

    public class InternalConfiguration
    {

        private static XmlDocument XML_CONFIG;

        public static string ObtenerValueFromInternalConfig(string Key)
        {
            try
            {
                var XmlConfig = XML_CONFIG;

                if (XmlConfig is null)
                {

                    XmlConfig = GetXmlConfig();
                    XML_CONFIG = XmlConfig;

                }

                var Nodo = XmlConfig.DocumentElement.SelectSingleNode(Key);
                if (Nodo is null)
                    return "";
                return Nodo.InnerText;
            }

            catch (Exception ex)
            {
                throw;
            }
        }

        private static XmlDocument GetXmlConfig()
        {
            var assembly = typeof(InternalConfiguration).Assembly;
            string ResName = "";
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (resource.EndsWith(".InternalConfig.xml", StringComparison.OrdinalIgnoreCase))
                {
                    ResName = resource;
                    break;
                }
            }
            if (string.IsNullOrEmpty(ResName))
            {
                throw new ApplicationException("InternalConfig could not be loaded");
            }
            var @file = assembly.GetManifestResourceStream(ResName);

            string xml = string.Empty;
            using (var reader = new System.IO.StreamReader(@file))
            {
                xml = reader.ReadToEnd();
            }

            if (!xml.StartsWith("<"))
            {
                xml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(xml));
            }

            var XmlConfig = new XmlDocument();
            XmlConfig.LoadXml(xml);
            return XmlConfig;
        }



        public static string ObtenerNombreApp()
        {
#if MSD
            return ObtenerValueFromInternalConfig("TITULO_MAIN_MSD");
#else
            return ObtenerValueFromInternalConfig("TITULO_MAIN");
#endif
        }
    }
}