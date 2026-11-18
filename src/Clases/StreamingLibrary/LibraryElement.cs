using System;
using System.Security;
using System.Xml;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public class LibraryElement
    {


        public const string HIDDEN_LINK = "{HIDDEN}";
        public const string HIDDEN_LINK_DESC = "** LINK NOT VISIBLE **";

        public string ID;
        public string Name;
        public string Description;
        public string Comments;
        public string Poster;
        public DateTime LastModification;
        public SecureString Link;
        public bool LinkVisible;

        // F3: Original DPAPI ciphertext preserved when LoadXML cannot decrypt it
        // (e.g. library restored cross-machine, then any add/delete triggers SaveXML).
        // Without this, SaveXML would re-encrypt the empty Link and permanently erase
        // the blob. Cleared when Link is non-empty or decode succeeds.
        private string _undecryptableLinkBlob = null;

        // Movie info
        public string IMDB;
        public string Allocine;
        public string Filmaffinity;


        public LibraryElement()
        {
            LastModification = DateTime.Now;
        }

        public string ToJSON(string CurrentURL, ref Configuracion Config)
        {
            string linkStr = HIDDEN_LINK_DESC;
            if (LinkVisible)
            {
                linkStr = Criptografia.ToInsecureString(Link);
            }
            string vlc = StreamingHelper.CreateStreamingLinkFromLibrary(ID, CurrentURL, ref Config);
            var str = new System.Text.StringBuilder();
            str.Append("{");
            str.Append("\"ID\":").Append(JsonString(ID)).Append(',');
            str.Append("\"Name\":").Append(JsonString(Name)).Append(',');
            str.Append("\"Desc\":").Append(JsonString(Description)).Append(',');
            str.Append("\"Com\":").Append(JsonString(Comments)).Append(',');
            str.Append("\"Poster\":").Append(JsonString(Poster)).Append(',');
            str.Append("\"IMDB\":").Append(JsonString(IMDB)).Append(',');
            str.Append("\"Filmaffinity\":").Append(JsonString(Filmaffinity)).Append(',');
            str.Append("\"Allocine\":").Append(JsonString(Allocine)).Append(',');
            str.Append("\"Date\":").Append(JsonString(LastModification.ToString("yyyy-MM-dd HH:mm"))).Append(',');
            str.Append("\"Link\":").Append(JsonString(linkStr)).Append(',');
            str.Append("\"VlcLink\":").Append(JsonString(vlc));
            str.Append("}");
            return str.ToString();
        }

        internal static string JsonString(string value)
        {
            if (value is null)
                value = "";
            var sb = new System.Text.StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"':
                        sb.Append(@"\""");
                        break;
                    case '\\':
                        sb.Append(@"\\");
                        break;
                    case '/':
                        sb.Append(@"\/");
                        break;
                    case '\b':
                        sb.Append(@"\b");
                        break;
                    case '\f':
                        sb.Append(@"\f");
                        break;
                    case '\n':
                        sb.Append(@"\n");
                        break;
                    case '\r':
                        sb.Append(@"\r");
                        break;
                    case '\t':
                        sb.Append(@"\t");
                        break;
                    default:
                        if (Strings.AscW(ch) < 32)
                        {
                            sb.Append(@"\u").Append(Strings.AscW(ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }

                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }


        public void LoadXML(XmlNode XML, bool Import)
        {
            string argPath = "ID";
            ID = LeerNodo(ref XML, ref argPath, "");
            string argPath1 = "Name";
            Name = LeerNodo(ref XML, ref argPath1, "");
            string argPath2 = "Desc";
            Description = LeerNodo(ref XML, ref argPath2, "");
            string argPath3 = "Com";
            Comments = LeerNodo(ref XML, ref argPath3, "");
            string argPath4 = "Post";
            Poster = LeerNodo(ref XML, ref argPath4, "");
            string argPath5 = "IMDB";
            IMDB = LeerNodo(ref XML, ref argPath5, "");
            string argPath6 = "Filmaffinity";
            Filmaffinity = LeerNodo(ref XML, ref argPath6, "");
            string argPath7 = "Allocine";
            Allocine = LeerNodo(ref XML, ref argPath7, "");
            string argPath8 = "Link";
            string str = LeerNodo(ref XML, ref argPath8, "");
            if (!string.IsNullOrEmpty(str))
            {
                if (Import)
                {
                    str = Criptografia.AES_DecryptString(str, ExportPassword);
                }
                else
                {
                    string rawBlob = str;
                    Link = Criptografia.DecryptString_DPAPI(str);
                    str = Criptografia.ToInsecureString(Link);
                    // F3: DPAPI failure yields empty but raw was non-empty.
                    // Only preserve when the blob looks like genuine base64 ciphertext
                    // (DecryptString_DPAPI also returns empty for non-base64 plaintext
                    // leftovers, where falling back to old behavior is fine).
                    if (string.IsNullOrEmpty(str) && !string.IsNullOrEmpty(rawBlob))
                    {
                        try
                        {
                            byte[] probe = Convert.FromBase64String(rawBlob);
                            if (probe is not null && probe.Length > 0)
                            {
                                _undecryptableLinkBlob = rawBlob;
                            }
                        }
                        catch
                        {
                            // Not base64: plaintext leftover, no preservation needed.
                        }
                    }
                    else
                    {
                        _undecryptableLinkBlob = null;
                    }
                }

                LinkVisible = !str.StartsWith(HIDDEN_LINK);
                str = str.Replace(HIDDEN_LINK, "");
                Link = Criptografia.ToSecureString(str);
            }
            // Empty plaintext that started with HIDDEN_LINK legitimately decodes
            // to empty link with LinkVisible=False, while DPAPI failure leaves
            // LinkVisible=True (empty str). SaveXML uses that flag (with empty
            // Link and a preserved blob) to decide to keep the ciphertext.
            else
            {
                Link = new SecureString();
            }

            LastModification = DateTime.MinValue;
            string argPath9 = "Date";
            str = LeerNodo(ref XML, ref argPath9, "");
            if (Information.IsDate(str))
            {
                LastModification = Conversions.ToDate(str);
            }
        }

        private string LeerNodo(ref XmlNode NodoXML, ref string Path, string ValorDefecto)
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

        public void SaveXML(ref XmlNode XML, bool Export)
        {

            var ElementNode = XML.AppendChild(XML.OwnerDocument.CreateElement("Element"));
            if (!Export)
            {
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("ID")).InnerText = ID;
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Date")).InnerText = LastModification.ToString("s");
            }

            if (!string.IsNullOrEmpty(Name))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Name")).InnerText = Name;
            if (!string.IsNullOrEmpty(Description))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Desc")).InnerText = Description;
            if (!string.IsNullOrEmpty(Comments))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Com")).InnerText = Comments;
            if (!string.IsNullOrEmpty(Poster))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Post")).InnerText = Poster;
            if (!string.IsNullOrEmpty(IMDB))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("IMDB")).InnerText = IMDB;
            if (!string.IsNullOrEmpty(Filmaffinity))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Filmaffinity")).InnerText = Filmaffinity;
            if (!string.IsNullOrEmpty(Allocine))
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Allocine")).InnerText = Allocine;

            string link2 = (LinkVisible ? "" : HIDDEN_LINK) + Criptografia.ToInsecureString(Link);
            if (Export)
            {
                string encryptedLink = Criptografia.AES_EncryptString(link2, ExportPassword);
                if (encryptedLink is not null)
                {
                    ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Link")).InnerText = encryptedLink;
                }
            }
            // F3: preserve original ciphertext when DPAPI decode failed on load
            // (Link empty, blob preserved, element still visible). Prevents the
            // empty re-encryption from erasing the stored blob on any save.
            else if (!string.IsNullOrEmpty(_undecryptableLinkBlob) && string.IsNullOrEmpty(Criptografia.ToInsecureString(Link)) && LinkVisible)
            {
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Link")).InnerText = _undecryptableLinkBlob;
            }
            else
            {
                var link3 = Criptografia.ToSecureString(link2);
                ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Link")).InnerText = Criptografia.EncryptString_DPAPI(link3);
                // If Link became non-empty (user edited it), stale blob is obsolete.
                if (!string.IsNullOrEmpty(_undecryptableLinkBlob) && !string.IsNullOrEmpty(Criptografia.ToInsecureString(Link)))
                {
                    _undecryptableLinkBlob = null;
                }
            }


        }

        private const string ExportPassword = "ae7}Kazdje/twiev";

    }
}