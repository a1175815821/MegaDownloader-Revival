using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public class ServerEncoderLinkHelper
    {

        // Esta clase debe ser igual en MegaDownloader y MegaUploader!!
        private const string ENCODE_PASSWORD = "tal.vApsg@i0smae";

        public class MegaLink
        {
            public string FileID;
            public string FileKey;
            public bool MegaFolder;
            // v2.4.4 子文件夹链接范围: mega.nz/folder/根#key/folder/子ID 或 /file/文件ID。
            // 旧版解码器忽略 token 尾部的 /folder/... 后缀(退化为整文件夹=历史行为),新版解码器据此恢复子范围。
            public string SubFolderID;
            public string SubFileID;
        }

        /// <summary>issue #6: friendly message for an ELC URL shortener that could
	/// 	''' not be resolved (dead link or unreachable network).</summary>
        private static string GetShortLinkUnresolvedMessage()
        {
            string msg = Language.GetText("ShortLink_Unresolved");
            if (string.IsNullOrEmpty(msg) || msg == "ShortLink_Unresolved")
            {
                msg = "Could not resolve the shortened link. It may be dead, or the network is unreachable.";
            }
            return msg;
        }



        public static string ServerEncode(string URL, List<MegaLink> LinkList, ref Configuracion Config)
        {

            var Helper = new ELCAccountHelper(ref Config);
            try
            {

                // mega://#!ABC!12345678900
                var strBuilderLinks = new System.Text.StringBuilder();
                if (LinkList is not null)
                {
                    foreach (MegaLink Link in LinkList)
                    {
                        if (strBuilderLinks.Length > 0)
                            strBuilderLinks.Append("|");
                        strBuilderLinks.Append("#" + (Link.MegaFolder ? "F" : "") + "!" + Link.FileID + "!" + Link.FileKey);
                        if (Link.MegaFolder)
                        {
                            if (!string.IsNullOrEmpty(Link.SubFileID))
                            {
                                strBuilderLinks.Append("/file/" + Link.SubFileID);
                            }
                            else if (!string.IsNullOrEmpty(Link.SubFolderID))
                            {
                                strBuilderLinks.Append("/folder/" + Link.SubFolderID);
                            }
                        }
                    }
                }

                string linksToEncode = strBuilderLinks.ToString();

                // 1) Generate random password
                byte[] b = new byte[24];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(b);
                }
                string Password = Convert.ToBase64String(b);

                // 2) Encode data
                byte[] LinksBinary = Cipher(false, System.Text.Encoding.UTF8.GetBytes(linksToEncode), b);
                byte[] URLBinary = System.Text.Encoding.UTF8.GetBytes(URL);


                // 3) Contact the server to encode the password
                if (URL == "REVERSE" | URL == "HIDDEN") // For testing or hidden links
                {
                    Password = new string(Password.Reverse().ToArray());
                }
                else
                {

                    var Account = Helper.GetAccountDetailsByURL(URL);
                    if (Account is null)
                    {
                        Log.WriteDebug("ELC Account not found for [" + URL + "]");
                        throw new ApplicationException(Language.GetText("ELC Account not found. You need a valid ELC Account configured for this ELC."));
                    }

                    var @param = new System.Collections.Specialized.NameValueCollection();
                    @param.Add("OPERATION_TYPE", "E");
                    @param.Add("DATA", Password);
                    @param.Add("USER", Criptografia.ToInsecureString(Account.User));
                    @param.Add("APIKEY", Criptografia.ToInsecureString(Account.Key));

                    string URL2 = URL;
                    if (URLExtractor.EsUrlAcortador(URL2))
                    {
                        URL2 = Conexion.ObtenerUrlDesdeAcortador(URL2);
                        if (string.IsNullOrEmpty(URL2))
                        {
                            // issue #6: the resolver swallows its own exception and returns
                            // empty; without this guard SendPOST("") would surface a raw
                            // UriFormatException ("Invalid URI") to the user.
                            Log.WriteError("ServerEncode: could not resolve shortened URL [" + URL + "]");
                            throw new ApplicationException(GetShortLinkUnresolvedMessage());
                        }
                    }
                    var Res = Conexion.SendPOST(URL2, @param);

                    if (Res.Excepcion is not null)
                    {
                        // issue #6: transport failures (no internet, DNS, firewall/VPN/proxy)
                        // get an actionable message; API-level errors keep the short hint.
                        if (NetworkErrors.IsTransportError(Res.Excepcion))
                        {
                            Log.WriteError("ServerEncode: transport failure contacting [" + URL + "] - " + Log.SafeException(Res.Excepcion));
                            throw new ApplicationException(NetworkErrors.DescribeTransportError(Res.Excepcion), Res.Excepcion);
                        }
                        throw new ApplicationException("Error contacting the server to encode string [" + URL + "]", Res.Excepcion);
                    }

                    Dictionary<string, object> ht;
                    // Example: {"e": "Error description", "d": "asdfjaoijeoiwajoiajsdf"}
                    ht = (Dictionary<string, object>)Newtonsoft.Json.JsonConvert.DeserializeObject(Res.Mensaje, typeof(Dictionary<string, object>));


                    if (ht.ContainsKey("e") && ht["e"] is not null && !string.IsNullOrEmpty(Conversions.ToString(ht["e"])))
                    {
                        throw new ApplicationException("Server returned error - " + Conversions.ToString(ht["e"]));
                    }
                    if (!ht.ContainsKey("d"))
                    {
                        return string.Empty;
                    }

                    Password = Conversions.ToString(ht["d"]);
                }



                // 3) Create stream
                byte[] PasswordBinary = System.Text.Encoding.UTF8.GetBytes(Password);
                int Length1 = LinksBinary.Length;
                short Length2 = (short)URLBinary.Length;
                short Length3 = (short)PasswordBinary.Length;

                var ByteList = new List<byte>();

                ByteList.AddRange(BitConverter.GetBytes(Length1));
                ByteList.AddRange(LinksBinary);
                ByteList.AddRange(BitConverter.GetBytes(Length2));
                ByteList.AddRange(URLBinary);
                ByteList.AddRange(BitConverter.GetBytes(Length3));
                ByteList.AddRange(PasswordBinary);

                // 4) Compress if necessary
                var l = CompressBytes(ByteList.ToArray()).ToList();
                if (l.Count < ByteList.Count)
                {
                    ByteList = l;
                    ByteList.Insert(0, 112); // Compressed - Hemos elegido 112 porque al pasarlo a base64 hace que la primera letra sea una "c"
                }
                else
                {
                    ByteList.Insert(0, 185);
                } // Uncompressed -  Hemos elegido 185 porque al pasarlo a base64 hace que la primera letra sea una "u"
                return Convert.ToBase64String(ByteList.ToArray()).Replace("+", "-").Replace("/", "_").Replace("=", "");
            }

            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                Helper.Dispose();
            }
        }

        public static List<string> ServerDecode(string link, ref Configuracion Config, ref Exception Exc, System.Threading.CancellationToken ct = default)
        {
            var Helper = new ELCAccountHelper(ref Config);
            string linkBackup = link;
            var LinkList = new List<string>();
            try
            {


                // 1) Decode string and uncompress
                if (string.IsNullOrEmpty(link))
                    return LinkList;
                // P3-3: prefix stripping must be case-insensitive like the detection side
                // (URLExtractor.vb regexes use RegexOptions.IgnoreCase and :239 compares
                // data.ToLower). "ELC?..." / "MEGA://CONFIGELC?..." links were accepted by
                // detection but their prefix was never stripped here, so the base64 step
                // failed and the link could not be decoded — while the lowercase form
                // worked. OrdinalIgnoreCase keeps the untrusted-substring semantics of the
                // old code (strip from the prefix onwards) — only the comparison changes.
                int pfxIdx = link.IndexOf(URLExtractor.SERVERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase);
                if (pfxIdx >= 0)
                {
                    link = link.Substring(pfxIdx + URLExtractor.SERVERENCODEDPREFIX.Length);
                }
                string prefixSlash = URLExtractor.SERVERENCODEDPREFIX.Replace("?", "/?");
                pfxIdx = link.IndexOf(prefixSlash, StringComparison.OrdinalIgnoreCase);
                if (pfxIdx >= 0)
                {
                    link = link.Substring(pfxIdx + prefixSlash.Length);
                }


                if (link.Length % 4 == 1)
                {
                    throw new ApplicationException(Language.GetText("Invalid input data"));
                }
                link += "==".Substring(2 - link.Length * 3 & 3);
                link = link.Replace("-", "+").Replace("_", "/").Replace(",", "");

                var ByteStream = Convert.FromBase64String(link).ToList();

                bool Compressed = false;
                switch (ByteStream[0])
                {
                    case 112: // Hemos elegido 112 porque al pasarlo a base64 hace que la primera letra sea una "c"
                        ByteStream = ByteStream.GetRange(1, ByteStream.Count - 1);
                        Compressed = true;
                        break;
                    case 185: // Hemos elegido 185 porque al pasarlo a base64 hace que la primera letra sea una "u"
                        ByteStream = ByteStream.GetRange(1, ByteStream.Count - 1);
                        break;
                    default:
                        return LinkList;
                }

                if (Compressed)
                {
                    ByteStream = UnCompressBytes(ByteStream.ToArray()).ToList();
                }


                // 2) Read BinaryStream
                int Length1 = BitConverter.ToInt32(ByteStream.ToArray(), 0);
                var LinksBinary = ByteStream.GetRange(4, Length1);
                short Length2 = BitConverter.ToInt16(ByteStream.ToArray(), 4 + Length1);
                var URLBinary = ByteStream.GetRange(4 + Length1 + 2, Length2);
                short Length3 = BitConverter.ToInt16(ByteStream.ToArray(), 4 + Length1 + 2 + Length2);
                var PasswordBinary = ByteStream.GetRange(4 + Length1 + 2 + Length2 + 2, Length3);

                string Password = System.Text.Encoding.UTF8.GetString(PasswordBinary.ToArray());
                string URL = System.Text.Encoding.UTF8.GetString(URLBinary.ToArray());

                // 3) Contact the server to decode the password
                if (URL == "REVERSE" | URL == "HIDDEN") // For testing or hidden links
                {
                    Password = new string(Password.Reverse().ToArray());
                }
                else
                {

                    var Account = Helper.GetAccountDetailsByURL(URL);
                    if (Account is null)
                    {
                        Log.WriteDebug("ELC Account not found for [" + URL + "]");
                        throw new ApplicationException(Language.GetText("ELC Account not found. You need a valid ELC Account configured for this ELC."));
                    }


                    var @param = new System.Collections.Specialized.NameValueCollection();
                    @param.Add("OPERATION_TYPE", "D");
                    @param.Add("DATA", Password);
                    @param.Add("USER", Criptografia.ToInsecureString(Account.User));
                    @param.Add("APIKEY", Criptografia.ToInsecureString(Account.Key));

                    string URL2 = URL;
                    if (URLExtractor.EsUrlAcortador(URL2))
                    {
                        URL2 = Conexion.ObtenerUrlDesdeAcortador(URL2);
                        if (string.IsNullOrEmpty(URL2))
                        {
                            // issue #6: same guard as ServerEncode — an unresolved short
                            // link must not degrade into a raw UriFormatException.
                            Log.WriteError("ServerDecode: could not resolve shortened URL [" + URL + "]");
                            throw new ApplicationException(GetShortLinkUnresolvedMessage());
                        }
                    }
                    var Res = Conexion.SendPOST(URL2, @param, ct: ct);

                    if (Res.Excepcion is not null)
                    {
                        // issue #6: transport failures (no internet, DNS, firewall/VPN/proxy)
                        // get an actionable message; API-level errors keep the short hint.
                        if (NetworkErrors.IsTransportError(Res.Excepcion))
                        {
                            Log.WriteError("ServerDecode: transport failure contacting [" + URL + "] - " + Log.SafeException(Res.Excepcion));
                            throw new ApplicationException(NetworkErrors.DescribeTransportError(Res.Excepcion), Res.Excepcion);
                        }
                        throw new ApplicationException("Error contacting the server to decode string [" + URL + "]", Res.Excepcion);
                    }

                    Dictionary<string, object> ht;
                    // Example: {"e": "Error description", "d": "asdfjaoijeoiwajoiajsdf"}
                    ht = (Dictionary<string, object>)Newtonsoft.Json.JsonConvert.DeserializeObject(Res.Mensaje, typeof(Dictionary<string, object>));


                    if (ht.ContainsKey("e") && ht["e"] is not null && !string.IsNullOrEmpty(Conversions.ToString(ht["e"])))
                    {
                        throw new ApplicationException("Server returned error - " + Conversions.ToString(ht["e"]));
                    }
                    if (!ht.ContainsKey("d"))
                    {
                        // N-P2-5: a response without "d" used to return an empty list with no
                        // exception, so the ELC link vanished silently (no log, no dialog) while
                        // the rest of the batch succeeded. Fail loudly instead — the outer Catch
                        // below turns this into Exc, which URLProcessor treats as a per-URL
                        // failure (logged, and surfaced when the whole batch fails).
                        throw new ApplicationException(Language.GetText("The ELC link could not be decoded: the server did not return any data. The link may be corrupted or the account may be invalid."));
                    }

                    Password = Conversions.ToString(ht["d"]);
                }


                // 4) Decode the links with the password
                LinksBinary = Trim0s(Cipher(true, LinksBinary.ToArray(), Convert.FromBase64String(Password)).ToList());
                string Data = System.Text.Encoding.UTF8.GetString(LinksBinary.ToArray());

                foreach (string token in Data.Split('|'))
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        LinkList.Add("mega://" + token.Trim());
                    }
                }

                // N-P2-5: an empty decode result is abnormal (a valid ELC always carries at
                // least one link). It used to be returned silently, dropping the link without
                // any log or dialog when other links in the same batch succeeded.
                if (LinkList.Count == 0)
                {
                    throw new ApplicationException(Language.GetText("The ELC link could not be decoded: no links were found inside. The link may be corrupted."));
                }

                return LinkList;
            }

            catch (Exception ex)
            {
                Log.WriteError("Error trying to decrypt the server-encoded string " + linkBackup + " - Error: " + ex.ToString());
                Exc = ex;
                return LinkList;
            }
            finally
            {
                Helper.Dispose();
            }

        }

        private static List<byte> Trim0s(List<byte> BinaryList)
        {
            int index = BinaryList.FindIndex(x => x == 0);
            if (index > 0)
            {
                BinaryList.RemoveRange(index, BinaryList.Count - index);
            }
            return BinaryList;
        }


        private static byte[] Cipher(bool Decrypt, byte[] Data, byte[] Key)
        {

            using (var aesAlg = new AesManaged())
            {

                aesAlg.KeySize = 128;
                aesAlg.BlockSize = 128;

                aesAlg.Key = Key.Take(16).ToArray();

                aesAlg.IV = new byte[] { Key[16], Key[17], Key[18], Key[19], Key[20], Key[21], Key[22], Key[23], 0, 0, 0, 0, 0, 0, 0, 0 };

                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.Zeros;


                ICryptoTransform decryptor;
                if (Decrypt)
                {
                    decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (var ms = new System.IO.MemoryStream(Data))
                    {
                        using (var objCryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            var Response = new byte[Data.Length];
                            objCryptoStream.Read(Response, 0, Response.Length);
                            try
                            {
                                objCryptoStream.FlushFinalBlock();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteError("Cipher: FlushFinalBlock failed during AES decryption (PaddingMode.Zeros, data may be unaligned): " + ex.Message);
                            }

                            return Response;
                        }

                    }
                }
                else
                {
                    decryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                    using (var ms = new System.IO.MemoryStream())
                    {
                        using (var objCryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                        {
                            objCryptoStream.Write(Data, 0, Data.Length);
                            objCryptoStream.FlushFinalBlock();
                            return ms.ToArray();
                        }
                    }
                }

            }

        }

        private static byte[] CompressBytes(byte[] bytes)
        {

            using (var msi = new System.IO.MemoryStream(bytes))
            {
                using (var mso = new System.IO.MemoryStream())
                {
                    using (var gs = new System.IO.Compression.GZipStream(mso, System.IO.Compression.CompressionMode.Compress))
                    {
                        CopyTo(msi, gs);
                    }

                    return mso.ToArray();
                }
            }
        }

        private static byte[] UnCompressBytes(byte[] bytes)
        {

            using (var msi = new System.IO.MemoryStream(bytes))
            {
                using (var mso = new System.IO.MemoryStream())
                {
                    using (var gs = new System.IO.Compression.GZipStream(msi, System.IO.Compression.CompressionMode.Decompress))
                    {
                        CopyTo(gs, mso);
                    }

                    return mso.ToArray();
                }
            }
        }

        private static void CopyTo(System.IO.Stream src, System.IO.Stream dest)
        {
            byte[] bytes = new byte[1024];

            int cnt;
            do
            {
                cnt = src.Read(bytes, 0, bytes.Length);
                if (cnt != 0)
                {
                    dest.Write(bytes, 0, cnt);
                }
            }
            while (cnt > 0);

        }
    }
}