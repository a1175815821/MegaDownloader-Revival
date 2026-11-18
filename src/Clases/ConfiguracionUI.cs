using System;
using System.Xml;

namespace MegaDownloader
{

    public class ConfiguracionUI
    {

        public enum ThemeModeType
        {
            Auto,    // 跟随系统
            Light,   // 浅色
            Dark    // 深色
        }

        public int AnchoVentanaPrincipal; // 0 = por defecto
        public int AltoVentanaPrincipal; // 0 = por defecto
        public byte[] EstadoLista;
        public string RutaSkin;

        public ThemeModeType Tema = ThemeModeType.Auto;


        public void ConfiguracionDefectoVacia()
        {
            AnchoVentanaPrincipal = 0;
            AltoVentanaPrincipal = 0;
            Tema = ThemeModeType.Auto;
        }


        public void CargarXML(XmlDocument XML)
        {
            AnchoVentanaPrincipal = 0;
            string argPath = "ConfigUI/AnchoVentanaPrincipal";
            int.TryParse(LeerNodo(ref XML, ref argPath, "0"), out AnchoVentanaPrincipal);
            AltoVentanaPrincipal = 0;
            string argPath1 = "ConfigUI/AltoVentanaPrincipal";
            int.TryParse(LeerNodo(ref XML, ref argPath1, "0"), out AltoVentanaPrincipal);

            string argPath2 = "ConfigUI/RutaSkin";
            RutaSkin = LeerNodo(ref XML, ref argPath2, "");
            string argPath3 = "ConfigUI/ConfigListaDescargas";
            string strEstadoLista = LeerNodo(ref XML, ref argPath3, "");

            try
            {
                EstadoLista = Convert.FromBase64String(strEstadoLista);
            }
            catch (Exception ex)
            {
                EstadoLista = null;
            }

            // 主题模式:旧配置文件无此节点时默认 Auto
            Tema = ThemeModeType.Auto;
            string argPath4 = "ConfigUI/Tema";
            switch (LeerNodo(ref XML, ref argPath4, "Auto") ?? "")
            {
                case "Light":
                    Tema = ThemeModeType.Light;
                    break;
                case "Dark":
                    Tema = ThemeModeType.Dark;
                    break;
                default:
                    Tema = ThemeModeType.Auto;
                    break;
            }
        }

        private string LeerNodo(ref XmlDocument DocumentoXML, ref string Path, string ValorDefecto)
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

        public void GuardarXML(ref XmlDocument XML)
        {

            string strEstadoLista = "";
            if (EstadoLista is not null)
            {
                strEstadoLista = Convert.ToBase64String(EstadoLista);
            }

            var NodoUI = XML.DocumentElement.AppendChild(XML.CreateElement("ConfigUI"));

            NodoUI.AppendChild(XML.CreateElement("AnchoVentanaPrincipal")).InnerText = AnchoVentanaPrincipal.ToString();
            NodoUI.AppendChild(XML.CreateElement("AltoVentanaPrincipal")).InnerText = AltoVentanaPrincipal.ToString();
            NodoUI.AppendChild(XML.CreateElement("ConfigListaDescargas")).InnerText = strEstadoLista;
            NodoUI.AppendChild(XML.CreateElement("RutaSkin")).InnerText = RutaSkin;
            NodoUI.AppendChild(XML.CreateElement("Tema")).InnerText = Tema.ToString();

        }
    }
}