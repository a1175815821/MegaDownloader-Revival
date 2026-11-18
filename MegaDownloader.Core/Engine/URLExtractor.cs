using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    public class URLExtractor
    {

        // Esta clase debe ser igual en MegaDownloader y MegaUploader!!

        // Private Shared ENC_XOR As Byte() = New Byte() {-} ' REMOVED FROM SOURCE CODE, SORRY
        private static byte[] ENC_XOR = new byte[] { 12, 57, 251, 120, 18, 75, 6, 250, 85 }; // REMOVED FROM SOURCE CODE, SORRY

        public const string ENCODE_PASSWORD = "k1o6Al-1kz¿!z05y";
        public const string ENCODE_PASSWORD2 = @"nYrXa@9Q¿1&hCWM\9(731Bp?t42=!k3.";

        public const string ENCODEDPREFIX = "enc?";
        public const string ENCODEDPREFIX2 = "enc2?";
        public const string FOLDERENCODEDPREFIX = "fenc?";
        public const string FOLDERENCODEDPREFIX2 = "fenc2?";

        public const string SERVERENCODEDPREFIX = "elc?";

        // Host-bounded patterns: only mega.nz / mega.co.nz (optional www.), case-insensitive at match time
        // Modern folder links may address a subfolder or a single file inside the folder:
        // https://mega.nz/folder/<rootID>#<key>                     -> whole folder
        // https://mega.nz/folder/<rootID>#<key>/folder/<subID>      -> subfolder only
        // https://mega.nz/folder/<rootID>#<key>/file/<fileID>       -> single file only
        // The SubFolderID/SubFileID groups keep the suffix inside match.Value so link
        // extraction (clipboard / add-links dialog) does not silently drop it.
        // 字符类必须显式写 ASCII:.NET 的 \w 匹配 Unicode 字母,链接后紧跟中文/俄文/假名时
        // (QQ/微信/论坛复制的常态)会被并进 key,解密必败。MEGA 的 ID/key 都是 base64url。
        // 主机左边界必须显式写出:否则 "notmega.nz/folder/" 之类的仿冒域名会从 "mega.nz" 处
        // 起匹配成功,被当成官方文件夹链接送去解密。前一字符是字母/数字/点/连字符时不算主机开头。
        private static readonly string[] patternHTTPURI = new string[] { @"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.co\.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!\s]+)(!(?<FileKey>[A-Za-z0-9_\-#=]+))?", @"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!\s]+)(!(?<FileKey>[A-Za-z0-9_\-#=]+))?", @"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.nz/file/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?", @"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.nz/folder/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?(?:/folder/(?<SubFolderID>[A-Za-z0-9_\-]+))?(?:/file/(?<SubFileID>[A-Za-z0-9_\-]+))?", @"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.co\.nz/file/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?", @"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.co\.nz/folder/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?(?:/folder/(?<SubFolderID>[A-Za-z0-9_\-]+))?(?:/file/(?<SubFileID>[A-Za-z0-9_\-]+))?" };






        private static readonly string[] patternMEGAURI = new string[] { @"(?<TAG>mega)(?<MODE1>:///|://|:)(?<MODE2>#F|F|#N|N|#|)!(?<FileID>[^!\s]+)(!(?<FileKey>[A-Za-z0-9_\-#=]+))?", @"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)", @"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc2(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)", @"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)", @"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc2(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)" };





        private static readonly string[] patternELCUri = new string[] { @"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>elc(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-]+)" };

        // 注意:patternElcConfig 必须在下面的 rx 单例之前声明(Shared 字段按文本顺序初始化)。
        private static readonly string[] patternElcConfig = new string[] { @"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>configelc(\?|/\?))(?<ENCODED_CONFIG>[^\s]+)" };

        // B3-⑧:正则单例(Compiled)。此前每次调用 New Regex,粘贴数百链接时
        // ExtraerURLs×ExtraerFileID/Key×IsMegaFolder 层层回扫(11 pattern 全表扫描),
        // UI 线程冻结数秒~十数秒。Regex 实例读方法线程安全,可全局共享。
        private static Regex[] BuildRegexes(params string[] patterns)
        {
            var list = new List<Regex>(patterns.Length);
            foreach (string p in patterns)
                list.Add(new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            return list.ToArray();
        }

        private static readonly Regex[] rxHTTPURI = BuildRegexes(patternHTTPURI);
        private static readonly Regex[] rxELCUri = BuildRegexes(patternELCUri);
        private static readonly Regex[] rxGetInfoURL = BuildRegexes(patternHTTPURI.Concat(patternMEGAURI).Concat(patternELCUri).ToArray());
        private static readonly Regex rxMegaGeneric = new Regex(@"(?<TAG>mega)(?<MODE>://|:///|:)(?<DATA>[A-Za-z0-9_\-/#!:.?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex[] rxElcConfig = BuildRegexes(patternElcConfig);
        private static readonly Regex rxSubFolderSuffix = new Regex(@"/folder/(?<SubFolderID>[A-Za-z0-9_\-]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex rxSubFileSuffix = new Regex(@"/file/(?<SubFileID>[A-Za-z0-9_\-]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex rxFolderHost = new Regex(@"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.(?:co\.)?nz/(?:#f!|folder/)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex rxMegaPlainAt = new Regex(@"\G" + patternMEGAURI[0], RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex rxFolderSelection = new Regex(@"\G(?:/folder/[A-Za-z0-9_\-]+)?(?:/file/[A-Za-z0-9_\-]+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        /// <summary>
    /// Checks if the URL is a MEGA folder
    /// </summary>
    /// <param name="URI"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        internal static bool IsMegaFolder(string URI)
        {
            bool Result = false;
            if (string.IsNullOrEmpty(URI))
                return false;
            Result = rxFolderHost.IsMatch(URI);

            if (!Result) // Enlaces mega sin codificar
            {
                foreach (Regex regex in rxGetInfoURL)
                {
                    if (regex.IsMatch(URI))
                    {
                        var match = regex.Match(URI);

                        if (!string.IsNullOrEmpty(match.Groups["MODE2"].Value))
                        {
                            // Enlace mega: sin codificar (comparación insensible a mayúsculas: #f/#F)
                            bool exitFor = false;
                            switch (match.Groups["MODE2"].Value.ToUpperInvariant() ?? "")
                            {
                                case "#F":
                                case "F":
                                    Result = true;
                                    exitFor = true;
                                    break;
                            }

                            if (exitFor)
                            {
                                break;
                            }
                        }

                        else if (!string.IsNullOrEmpty(match.Groups["BASIC_ENCODE"].Value) && match.Groups["BASIC_ENCODE"].Value.StartsWith("fenc", StringComparison.OrdinalIgnoreCase))
                        {
                            // Enlace codificado fenc
                            Result = true;
                            break;

                        }
                    }
                }
            }
            return Result;
        }


        /// <summary>
    /// Checks if the URL is an ELC
    /// </summary>
    /// <param name="URI"></param>
    /// <returns></returns>
        internal static bool IsELC(string URI)
        {
            foreach (Regex regex in rxELCUri)
            {
                if (regex.IsMatch(URI))
                    return true;
            }
            return false;
        }


        /// <summary>Repairs a short key from complete base64url fragments. Stop at the key boundary
    /// so ordinary prose after the final fragment remains outside the extracted link.</summary>
        private static string RepairSplitKey(string text, Match m)
        {
            var key = m.Groups["FileKey"];
            string url = m.Value.Trim();
            bool isFolder = rxFolderHost.IsMatch(url) || string.Equals(m.Groups["MODE2"].Value.TrimStart('#'), "F", StringComparison.OrdinalIgnoreCase);
            int expected = isFolder ? 22 : 43;
            if (!key.Success || key.Length >= expected || key.Index + key.Length != m.Index + m.Length)
                return url;

            int index = key.Index + key.Length;
            int limit = index + Math.Min(text.Length - index, 128);
            int remaining = expected - key.Length;
            var tail = new System.Text.StringBuilder(remaining);
            while (remaining > 0)
            {
                if (index >= limit || !char.IsWhiteSpace(text[index]))
                    return url;
                while (index < limit && char.IsWhiteSpace(text[index]))
                    index += 1;
                int start = index;
                while (index < limit && IsKeyCharacter(text[index]))
                    index += 1;
                int length = index - start;
                if (length == 0 || length > remaining || index == limit && index < text.Length && IsKeyCharacter(text[index]))
                    return url;
                tail.Append(text, start, length);
                remaining -= length;
            }

            if (isFolder)
            {
                var suffix = rxFolderSelection.Match(text, index);
                if (suffix.Success)
                    tail.Append(suffix.Value);
            }
            return url + tail.ToString();
        }

        private static bool IsKeyCharacter(char c)
        {
            return c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-';
        }

        public static List<string> ExtraerSoloURLsOficiales(string Texto)
        {
            var links = new HashSet<string>();
            if (!string.IsNullOrEmpty(Texto))
                AddOfficialLinks(Texto, links);
            return links.ToList();
        }

        private static void AddOfficialLinks(string text, HashSet<string> links)
        {
            foreach (Regex regex in rxHTTPURI)
            {
                foreach (Match m in regex.Matches(text))
                {
                    if (m.Groups["FileID"].Length > 0)
                        links.Add(RepairSplitKey(text, m));
                }
            }
        }

        // mega://configelc?http%3A%2F%2Ftest.com%2Felc%3Fa%3D1%26b%3D2:User%20Name:Api%20Key:Account%20Alias
        public static List<string> ExtraerConfiguracionELC(string Texto)
        {
            var conf = new HashSet<string>(); // Evitamos repetidos

            if (string.IsNullOrEmpty(Texto))
                return conf.ToList();

            MatchCollection matches;

            foreach (Regex regx in rxElcConfig)
            {

                matches = regx.Matches(Texto);

                foreach (Match match in matches)
                {
                    string config = match.Groups["ENCODED_CONFIG"].Value;
                    // url : nick : apikey (: alias)
                    if (!string.IsNullOrEmpty(config))
                    {
                        string config2 = config.Replace(@"\:", "{TEMP_PUNTOS}");
                        if (config2.Split(':').Length == 3 | config2.Split(':').Length == 4)
                        {
                            conf.Add(config);
                        }
                    }
                }
            }
            return conf.ToList();
        }

        public static List<string> ExtraerURLs(string Texto)
        {
            var links = new HashSet<string>(); // Evitamos repetidos

            if (string.IsNullOrEmpty(Texto))
                return links.ToList();


            AddOfficialLinks(Texto, links);

            // 2) Detect MEGA uri
            // Examples:
            // mega:!ABC!12345678900
            // mega:#!ABC!12345678900
            // mega:#F!ABC!12345678900
            // mega://#!123!456789ABC
            // mega://#F!123!456789ABC
            // mega://https://mega.co.nz/#!abcdef!ghijklmnopqr
            // mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
            // mega://senc?_xlPqemSILarh5VBKbhSTFyQQQ0
            var matches = rxMegaGeneric.Matches(Texto);
            foreach (Match match in matches)
            {

                string data = match.Groups["DATA"].Value;
                string value = match.Value.Trim();
                var plain = rxMegaPlainAt.Match(Texto, match.Index);
                if (plain.Success && plain.Length == value.Length)
                    value = RepairSplitKey(Texto, plain);

                if (!string.IsNullOrEmpty(data) && data.Length > 5)
                {

                    if (data.ToLower().StartsWith(ENCODEDPREFIX) | data.ToLower().StartsWith(FOLDERENCODEDPREFIX) | data.ToLower().StartsWith(ENCODEDPREFIX2) | data.ToLower().StartsWith(FOLDERENCODEDPREFIX2) | data.ToLower().StartsWith(SERVERENCODEDPREFIX))



                    {
                        links.Add(value);
                        continue;
                    }


                    string fileid = ExtraerFileID(value);
                    if (!string.IsNullOrEmpty(fileid))
                    {
                        links.Add(value);
                        continue;
                    }
                    fileid = ExtraerFileID(data);
                    if (!string.IsNullOrEmpty(fileid))
                    {
                        links.Add(value);
                        continue;
                    }

                }
            }

            return links.ToList();
        }



        public static bool EsUrlAcortador(string URL)
        {
            // goo.gl 短链服务已于 2019 年 3 月停止解析,已无支持的短链服务
            return false;
        }


        public static void CheckFileIDAndFileKey(ref string FileID, ref string FileKey)
        {

            if (!string.IsNullOrEmpty(FileID))
                FileID = FileID.Replace("/?", "?"); // A veces firefox mete un /? en vez de un ? :/

            // Encoded links (enc?/fenc?/enc2?/fenc2?): el FileID trae el prefijo y el payload cifrado
            if (string.IsNullOrEmpty(FileKey) & !string.IsNullOrEmpty(FileID) && FileID.StartsWith(ENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) | FileID.StartsWith(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) | FileID.StartsWith(ENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) | FileID.StartsWith(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase))
            {


                // Encoded string
                // Generate it: GenerateEncodedURILink
                string encodedStr;
                if (FileID.StartsWith(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase))
                {
                    encodedStr = FileID.Substring(FileID.IndexOf(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) + FOLDERENCODEDPREFIX2.Length);
                }
                else if (FileID.StartsWith(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase))
                {
                    encodedStr = FileID.Substring(FileID.IndexOf(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) + FOLDERENCODEDPREFIX.Length);
                }
                else if (FileID.StartsWith(ENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase))
                {
                    encodedStr = FileID.Substring(FileID.IndexOf(ENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) + ENCODEDPREFIX2.Length);
                }
                else
                {
                    encodedStr = FileID.Substring(FileID.IndexOf(ENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) + ENCODEDPREFIX.Length);
                }

                // base64url sin padding: longitud % 4 == 1 es inválida (nunca la genera un encoder real).
                // Antes: "==".Substring(3) lanzaba ArgumentOutOfRangeException. Ahora: error amistoso.
                if (encodedStr.Length % 4 == 1)
                {
                    throw new ApplicationException("Invalid encoded link.");
                }
                encodedStr += "==".Substring(2 - encodedStr.Length * 3 & 3);
                encodedStr = encodedStr.Replace("-", "+").Replace("_", "/").Replace(",", "");

                bool isFolder = FileID.StartsWith(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) | FileID.StartsWith(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase);

                string link = (isFolder ? "F" : "") + Criptografia.AES_DecryptString(encodedStr, ENCODE_PASSWORD);
                if (!link.StartsWith("mega:"))
                {
                    link = "mega://#" + link;
                }

                string F = ExtraerFileID(link);
                string K = ExtraerFileKey(link);
                if (!string.IsNullOrEmpty(F) & !HasNonPrintableCharacters(F))
                {
                    FileID = F;
                    FileKey = K;
                }
                else // Encode V2??
                {
                    link = (isFolder ? "F" : "") + Criptografia.AES_DecryptString(encodedStr, getENC2Bytes(), System.Text.Encoding.ASCII);
                    if (!link.StartsWith("mega:"))
                    {
                        link = "mega://#" + link;
                    }
                    F = ExtraerFileID(link);
                    K = ExtraerFileKey(link);
                    if (!string.IsNullOrEmpty(F))
                    {
                        FileID = F;
                        FileKey = K;
                    }
                }

            }
        }

        private static bool HasNonPrintableCharacters(string F)
        {
            var nonvalidchars = System.IO.Path.GetInvalidFileNameChars().ToList();
            nonvalidchars.Remove('?');
            foreach (var c in F)
            {
                if (nonvalidchars.Contains(c))
                    return true;
            }
            return false;
        }

        private static byte[] getENC2Bytes()
        {
            int intLength;
            int intRemaining;
            byte[] bytDecryptionKey;

            string temp = ENCODE_PASSWORD2;

            intLength = Strings.Len(temp);

            if (intLength >= 32)
            {
                temp = Strings.Left(temp, 32);
            }
            else
            {
                intLength = Strings.Len(temp);
                intRemaining = 32 - intLength;
                temp = temp + Strings.StrDup(intRemaining, "X");
            }

            bytDecryptionKey = System.Text.Encoding.ASCII.GetBytes(temp.ToCharArray());

            for (int i = 0; i <= 159; i++)
                bytDecryptionKey[i % bytDecryptionKey.Length] = (byte)(bytDecryptionKey[i % bytDecryptionKey.Length] ^ ENC_XOR[i % ENC_XOR.Length]);

            return bytDecryptionKey;
        }



        public static string GenerateEncodedURILink(string FileID, string FileKey, bool MegaFolder, bool Compatibility)
        {
            // !ABC!12345678900

            string link = "!" + FileID + "!" + FileKey;
            string str = null;
            if (Compatibility)
            {
                // 兼容模式：保持旧静态 IV 密文格式，确保旧版本程序仍能解密。
                str = Criptografia.AES_EncryptString(link, ENCODE_PASSWORD, useRandomIV: false);
            }
            else
            {
                str = Criptografia.AES_EncryptString(link, getENC2Bytes(), System.Text.Encoding.ASCII);
            }

            // 加密失败返回 Nothing：显式报错而非生成损坏的编码链接。
            if (str is null)
            {
                throw new ApplicationException("Failed to encrypt the encoded link.");
            }

            str = str.Replace("+", "-").Replace("/", "_").Replace("=", "");

            return "mega://" + (MegaFolder ? FOLDERENCODEDPREFIX2 : ENCODEDPREFIX2) + str;
        }


        #region Extract FileID/FileKey

        /// <summary>
    /// Extracts the subfolder handle from a modern folder link
    /// (mega.nz/folder/ID#KEY/folder/SUBID). Empty for whole-folder links.
    /// </summary>
        public static string ExtraerSubFolderID(string URL)
        {
            if (string.IsNullOrEmpty(URL))
                return "";

            Match m = null;
            foreach (Regex regex in rxHTTPURI)
            {
                if (regex.IsMatch(URL))
                {
                    m = regex.Match(URL);
                    break;
                }
            }

            // 不能在无 HTTP 匹配时提前 Return:旧式 token(mega://#F!根!key/folder/子ID,
            // ELC 解码产物)不含 mega.nz 主机,提前返回会让下面的后缀兜底变成死代码,
            // 用户只要一个子文件夹却把整个分享展开。
            string subFolderID = "";
            if (m is not null && m.Success)
                subFolderID = m.Groups["SubFolderID"].Value + "";
            if (string.IsNullOrEmpty(subFolderID))
            {
                // 回退:旧式 token (mega://#F!根!key/folder/子ID,ELC 解码产物)后缀解析
                var suffix = rxSubFolderSuffix.Match(URL);
                if (suffix.Success)
                    subFolderID = suffix.Groups["SubFolderID"].Value;
            }
            return subFolderID.Trim();
        }

        /// <summary>
    /// Extracts the single-file handle from a modern folder link
    /// (mega.nz/folder/ID#KEY/file/FILEID). Empty for whole-folder links.
    /// </summary>
        public static string ExtraerSubFileID(string URL)
        {
            if (string.IsNullOrEmpty(URL))
                return "";

            Match m = null;
            foreach (Regex regex in rxHTTPURI)
            {
                if (regex.IsMatch(URL))
                {
                    m = regex.Match(URL);
                    break;
                }
            }

            // 同 ExtraerSubFolderID:无 HTTP 匹配时也要走后缀兜底,否则单文件范围丢失。
            string subFileID = "";
            if (m is not null && m.Success)
                subFileID = m.Groups["SubFileID"].Value + "";
            if (string.IsNullOrEmpty(subFileID))
            {
                // 回退:旧式 token (mega://#F!根!key/file/文件ID,ELC 解码产物)后缀解析
                var suffix = rxSubFileSuffix.Match(URL);
                if (suffix.Success)
                    subFileID = suffix.Groups["SubFileID"].Value;
            }
            return subFileID.Trim();
        }

        // B3-⑨:论坛/聊天软件会把 fragment 转义(%23=#,%3D== 等,=###n= 恰含 #/=)。
        // 旧代码只洗 %21/%20,转义过的 key 原样进 B64Decode 必败,报永久性"无法解密"。
        // 这里做一次性 URL 解码(非法 % 序列回退原串)+去空白;长度判定必须在清洗后做。
        private static string NormalizeLinkForExtraction(string URL)
        {
            URL = URL.Replace("%21", "!"); // Algunos links estan sin el ! :/
            URL = URL.Replace("%20", ""); // Algunos links tienen espacios en medio :/
            try
            {
                string unescaped = Uri.UnescapeDataString(URL);
                if (!string.IsNullOrEmpty(unescaped))
                    URL = unescaped;
            }
            catch
            {
                // 非法 % 序列(如裸 %/截断 %2)保持原串,后续正则照常处理
            }
            URL = URL.Replace(" ", "").Replace(Constants.vbTab, "").Replace(Constants.vbCr, "").Replace(Constants.vbLf, "");
            return URL;
        }

        public static string ExtraerFileID(string URL)
        {
            if (string.IsNullOrEmpty(URL))
                return "";

            URL = NormalizeLinkForExtraction(URL);

            foreach (Regex regex in rxGetInfoURL)
            {
                if (regex.IsMatch(URL))
                {
                    var match = regex.Match(URL);

                    if (!string.IsNullOrEmpty(match.Groups["ENCODED_FILEID"].Value))
                    {
                        return match.Groups["BASIC_ENCODE"].Value + match.Groups["ENCODED_FILEID"].Value.Trim('/');
                    }
                    else
                    {
                        string fileID = match.Groups["FileID"].Value + "";

                        if (!string.IsNullOrEmpty(match.Groups["MODE2"].Value)) // private file from folder
                        {
                            switch (match.Groups["MODE2"].Value.ToUpperInvariant() ?? "")
                            {
                                case "#N":
                                case "N":
                                    fileID = "N?" + fileID;
                                    break;
                            }
                        }

                        return fileID;
                    }
                }
            }

            return string.Empty;
        }

        public static string ExtraerFileKey(string URL)
        {
            if (string.IsNullOrEmpty(URL))
                return "";

            URL = NormalizeLinkForExtraction(URL);


            foreach (Regex regex in rxGetInfoURL)
            {
                if (regex.IsMatch(URL))
                {
                    var match = regex.Match(URL);

                    string fileKey = match.Groups["FileKey"].Value + "";
                    // B3-⑨:空白已在 NormalizeLinkForExtraction 去除(旧 Contains(" ") 分支是死代码:
                    // FileKey 组字符集 [\w\-#=] 根本吃不进空格),此处直接判长。
                    if (fileKey.Length < 40 & !IsMegaFolder(URL)) // Seguramente esté mal
                    {
                        return string.Empty;
                    }
                    return fileKey;
                }
            }

            return string.Empty;
        }

        #endregion

    }
}