using System;
using System.Linq;
using System.Xml;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;

namespace MegaDownloader
{

    public class Updater
    {

        private enum Version
        {
            Binary, // Just the EXE
            Installer, // Installer
            MSD // Mega Search Desktop - installer
        }

        public static void ComprobarVersionMegadownloader(ref string UrlNuevaVersion, ref string Version)
        {
            // ByRef 由调用方按小时复用:失败时不清空会让上一次结果继续触发提醒/按钮。
            UrlNuevaVersion = "";
            Version = "";
            if (string.IsNullOrEmpty(Conexion.GetUpdateCheckURL()))
                return;
            string URL = Conexion.GetUpdateCheckURL();
            var Resultado = Conexion.LeerURL(URL);
            if (Resultado.Excepcion is null)
            {
                var XML = new XmlDocument();
                try
                {
                    // B3:远端 XML 禁外部实体解析。version.xml 来自网络,默认 XmlResolver
                    // 会跟随 DOCTYPE 外部实体(file/http),仓库沦陷/MITM 时即 XXE。
                    XML.XmlResolver = null;
                    XML.LoadXml(Resultado.Mensaje);
                    if (XML.DocumentElement is null)
                        return;
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error loading the version check XML: " + ex.ToString());
                    return;
                }
                System.Version UltimaVersion = null;
                System.Version VersionActual = null;

                string argPath = "Version";
                string ultimaVersionStr = NormalizeVersionString(LeerNodo(ref XML, ref argPath, ""));
                string actualVersionStr = NormalizeVersionString(InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_UPDATE"));

                if (System.Version.TryParse(ultimaVersionStr, out UltimaVersion) && System.Version.TryParse(actualVersionStr, out VersionActual))
                {
                    if (UltimaVersion > VersionActual)
                    {


                        string NodeToCheck = "Link";
                        switch (GetVersion())
                        {
                            case Updater.Version.MSD:
                                NodeToCheck = "LinkMSD";
                                break;
                            case Updater.Version.Installer:
                                NodeToCheck = "LinkInstaller";
                                break;
                            default:
                                NodeToCheck = "Link";
                                break;
                        }

                        UrlNuevaVersion = LeerNodo(ref XML, ref NodeToCheck, "");
                        if (string.IsNullOrEmpty(UrlNuevaVersion))
                        {
                            string argPath1 = "Link";
                            UrlNuevaVersion = LeerNodo(ref XML, ref argPath1, "");
                        }

                        string argPath2 = "Version";
                        Version = LeerNodo(ref XML, ref argPath2, "");
                        Log.WriteInfo("There is a new version of MegaDownloader: " + Version + " - " + UrlNuevaVersion);
                    }
                }
            }
        }

        private static Version GetVersion()
        {
#if MSD
            return Version.MSD;
#else
            try
            {
                using (var rKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\MegaDownloader", false))
                {
                    if (rKey is not null && rKey.GetValue("Installer") is not null && Conversions.ToString(rKey.GetValue("Installer")) == "1")
                    {
                        return Version.Installer;
                    }
                    else
                    {
                        return Version.Binary;
                    }
                }
            }
            catch (System.Security.SecurityException ex)
            {
                Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry. Installation check not possible, assume binaries.");
                return Version.Binary;
            }
            catch (Exception ex)
            {
                Log.WriteError("Error accessing the registry for checking installation. Error: " + ex.ToString());
                return Version.Binary;
            }
#endif
        }

        private static string LeerNodo(ref XmlDocument DocumentoXML, ref string Path, string ValorDefecto)
        {
            var nodo = DocumentoXML.DocumentElement.SelectSingleNode(Path);
            if (nodo is null)
            {
                return ValorDefecto;
            }
            else
            {
                return nodo.InnerText;
            }
        }

        /// <summary>
    /// Normalizes "2.0" and "2.0.0.0" to the same four-part version string for comparisons.
    /// </summary>
        public static string NormalizeVersionString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0.0.0.0";
            var parts = value.Trim().Split('.').ToList();
            while (parts.Count < 4)
                parts.Add("0");
            if (parts.Count > 4)
                parts = parts.Take(4).ToList();
            for (int i = 0, loopTo = parts.Count - 1; i <= loopTo; i++)
            {
                int n;
                if (!int.TryParse(parts[i], out n) || n < 0)
                    parts[i] = "0";
            }
            return string.Join(".", parts);
        }
    }
}