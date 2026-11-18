using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    public class Language
    {
        public const string FollowSystemLanguageCode = "auto";
        private const string EnglishLanguageCode = "en-US";
        private static readonly Lazy<Dictionary<string, XmlDocument>> builtinPacks = new Lazy<Dictionary<string, XmlDocument>>(LoadBuiltinPacks);



        private static CultureInfo culture = CultureInfo.InvariantCulture;


        private static XmlDocument LanguageFileDisk = null;
        private static XmlDocument LanguageFileInternal = null;
        private static XmlDocument LanguageFileEnUs = null;

        public static string GetCurrentLanguageCode()
        {
            return culture.Name;
        }

        public static bool IsValidLanguageCode(string CultureCode)
        {
            try
            {
                if (string.IsNullOrEmpty(CultureInfo.GetCultureInfo(CultureCode).Name))
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public static string NormalizeLanguagePreference(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || string.Equals(code.Trim(), FollowSystemLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return FollowSystemLanguageCode;
            }
            try
            {
                string name = CultureInfo.GetCultureInfo(code.Trim()).Name;
                return name.Length == 0 ? FollowSystemLanguageCode : name;
            }
            catch (Exception ex)
            {
                return FollowSystemLanguageCode;
            }
        }

        public static void InitLanguage(string CultureCode)
        {
            InitializeLanguage(CultureCode, CultureInfo.CurrentUICulture, GetLanguageDirectory());
        }

        private static string GetLanguageDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Language");
        }

        private static Dictionary<string, XmlDocument> LoadBuiltinPacks()
        {
            var packs = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
            foreach (string resource in typeof(Language).Assembly.GetManifestResourceNames())
            {
                if (!resource.EndsWith("-Language.xml", StringComparison.OrdinalIgnoreCase))
                    continue;
                var pack = new XmlDocument();
                using (var stream = typeof(Language).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream is null)
                        throw new InvalidDataException("Language resource could not be opened: " + resource);
                    pack.Load(stream);
                }
                string code = pack.DocumentElement.GetAttribute("id");
                if (pack.DocumentElement.Name != "Language" || !IsValidLanguageCode(code))
                {
                    throw new InvalidDataException("Invalid built-in language pack: " + resource);
                }
                packs[CultureInfo.GetCultureInfo(code).Name] = pack;
            }
            if (!packs.ContainsKey(EnglishLanguageCode))
                throw new InvalidDataException("The English language pack is missing.");
            return packs;
        }

        // Stock v2.6.2 menu values also exist in the editable copies created on first run.
        // Migrate an exact match and preserve every other user translation.
        private static readonly Dictionary<string, Dictionary<string, string>> legacyMenuLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase) { { "de-DE", new Dictionary<string, string>() { { "&Retrieve links from an image", "&Links aus einem Bild extrahieren" } } }, { "es-ES", new Dictionary<string, string>() { { "Encode lin&ks", "Codi&ficar enlaces" } } }, { "fr-FR", new Dictionary<string, string>() { { "&Help", "Aide&" }, { "E&xit", "F&ermer" }, { "Steganograph&y", "Stéganographi&e" } } }, { "hu-HU", new Dictionary<string, string>() { { "&Streaming", "&Streaming" }, { "&Retrieve links from an image", "&Linkek kinyerése képből" } } }, { "it-IT", new Dictionary<string, string>() { { "See extraction &queue", "Guarda l'estrazione e la &coda" } } }, { "pt-BR", new Dictionary<string, string>() { { "&Help", "&Ajuda" }, { "Manage Streaming &Library", "Editar &Biblioteca" } } }, { "ro-RO", new Dictionary<string, string>() { { "&Streaming", "&Flux" }, { "Manage Streaming &Library", "Gestionează & Biblioteca" }, { "FA&Q", "Întrebări frecvente&" } } }, { "ru-RU", new Dictionary<string, string>() { { "&Streaming", "&Стриминг" } } } };

        private static Dictionary<string, XmlDocument> LoadUserPacks(string directory)
        {
            var packs = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directory))
                return packs;
            foreach (string @file in Directory.GetFiles(directory, "*.xml").OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var pack = new XmlDocument();
                    pack.Load(@file);
                    if (pack.DocumentElement is null || pack.DocumentElement.Name != "Language")
                        continue;
                    string code = pack.DocumentElement.GetAttribute("id");
                    if (IsValidLanguageCode(code) && !string.Equals(code, FollowSystemLanguageCode, StringComparison.OrdinalIgnoreCase))
                    {
                        string canonical = CultureInfo.GetCultureInfo(code).Name;
                        MigrateLegacyMenuLabels(pack, canonical);
                        packs[canonical] = pack;
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("User language file could not be loaded: " + @file + " (" + Log.SafeException(ex) + ")");
                }
            }
            return packs;
        }

        private static void MigrateLegacyMenuLabels(XmlDocument pack, string code)
        {
            Dictionary<string, string> oldLabels = null;
            XmlDocument defaultPack = null;
            if (!legacyMenuLabels.TryGetValue(code, out oldLabels) || !builtinPacks.Value.TryGetValue(code, out defaultPack))
                return;
            var currentLabels = GetTextMap(defaultPack);
            foreach (XmlElement node in pack.DocumentElement.SelectNodes("Text").OfType<XmlElement>())
            {
                string key = node.GetAttribute("key");
                string oldLabel = null;
                string newLabel = null;
                if (oldLabels.TryGetValue(key, out oldLabel) && (node.InnerText ?? "") == (oldLabel ?? "") && currentLabels.TryGetValue(key, out newLabel))
                {
                    node.InnerText = newLabel;
                }
            }
        }

        private static string GetLanguageFamily(CultureInfo value)
        {
            var parent = value;
            while (!parent.IsNeutralCulture && parent.Parent.Name.Length > 0)
                parent = parent.Parent;
            // Preserve script distinctions, including the older .NET Chinese aliases.
            if (string.Equals(parent.Name, "zh-CHS", StringComparison.OrdinalIgnoreCase))
                return "zh-Hans";
            if (string.Equals(parent.Name, "zh-CHT", StringComparison.OrdinalIgnoreCase))
                return "zh-Hant";
            return parent.Name;
        }

        internal static string ResolveLanguageCode(string preference, CultureInfo systemLanguage, IEnumerable<string> availableCodes)
        {
            string normalized = NormalizeLanguagePreference(preference);
            var preferred = (normalized ?? "") == FollowSystemLanguageCode ? systemLanguage ?? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(normalized);
            if (preferred.Name.Length == 0)
                return EnglishLanguageCode;
            var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string code in availableCodes)
            {
                if (IsValidLanguageCode(code))
                {
                    string canonical = CultureInfo.GetCultureInfo(code).Name;
                    available[canonical] = canonical;
                }
            }
            string exact = null;
            if (available.TryGetValue(preferred.Name, out exact))
                return exact;

            string family = GetLanguageFamily(preferred);
            bool genericNeutral = preferred.IsNeutralCulture && preferred.Name.IndexOf('-') < 0;
            string related = available.Keys.Where(code =>
    {
        var candidate = CultureInfo.GetCultureInfo(code);
        return genericNeutral ? string.Equals(candidate.TwoLetterISOLanguageName, preferred.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase) : string.Equals(GetLanguageFamily(candidate), family, StringComparison.OrdinalIgnoreCase);
    }).OrderBy(code => builtinPacks.Value.ContainsKey(code) ? 0 : 1).ThenBy(code => code, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            return related ?? EnglishLanguageCode;
        }

        // Exercise this same initialization path with a temporary directory in tests.
        private static void InitializeLanguage(string preference, CultureInfo systemLanguage, string languageDirectory)
        {
            var builtin = builtinPacks.Value;
            string builtinDirectory = Path.Combine(languageDirectory, "Builtin");
            Directory.CreateDirectory(builtinDirectory);
            // P-16: the built-in packs only change when the assembly changes. The old
            // loop serialised all 10 XmlDocuments and read back all 10 Builtin files on
            // EVERY startup. Gate it on the module MVID (changes on every rebuild, so a
            // new binary always re-syncs); user-editable packs are still checked each run.
            string stampPath = Path.Combine(builtinDirectory, ".resource-stamp");
            string stamp = typeof(Language).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            bool needBuiltinSync = true;
            try
            {
                needBuiltinSync = !File.Exists(stampPath) || (File.ReadAllText(stampPath) ?? "") != (stamp ?? "");
            }
            catch (Exception ex)
            {
                needBuiltinSync = true;
            }
            foreach (KeyValuePair<string, XmlDocument> entry in builtin)
            {
                string builtinFile = Path.Combine(builtinDirectory, entry.Key + ".xml");
                // 旧内置包要在被覆盖前读出来:用户可编辑副本若与旧内置包完全一致(即从未自定义),
                // 升级时应同步成新内置包,否则老用户磁盘上的旧译文会一直盖住新文案。
                XmlDocument oldBuiltin = null;
                if (needBuiltinSync && File.Exists(builtinFile))
                {
                    try
                    {
                        oldBuiltin = new XmlDocument() { XmlResolver = null };
                        oldBuiltin.Load(builtinFile);
                    }
                    catch (Exception ex)
                    {
                        oldBuiltin = null;
                    }
                }
                if (needBuiltinSync)
                    SaveBuiltinIfChanged(entry.Value, builtinFile);
                string userFile = Path.Combine(languageDirectory, entry.Key + ".xml");
                if (!File.Exists(userFile))
                {
                    entry.Value.Save(userFile);
                }
                else if (needBuiltinSync && oldBuiltin is not null)
                {
                    RefreshUserPackIfUncustomized(userFile, oldBuiltin, entry.Value);
                }
            }
            if (needBuiltinSync)
            {
                try
                {
                    File.WriteAllText(stampPath, stamp);
                }
                catch (Exception ex)
                {
                    Log.WriteDebug("Language resource stamp could not be written: " + Log.SafeException(ex));
                }
            }

            var users = LoadUserPacks(languageDirectory);
            string selected = ResolveLanguageCode(preference, systemLanguage, builtin.Keys.Concat(users.Keys));
            var internalPack = builtin[ResolveLanguageCode(selected, systemLanguage, builtin.Keys)];
            XmlDocument diskPack = null;
            if (!users.TryGetValue(selected, out diskPack))
                diskPack = internalPack;
            LanguageFileEnUs = builtin[EnglishLanguageCode];
            LanguageFileInternal = internalPack;
            LanguageFileDisk = diskPack;
            culture = CultureInfo.GetCultureInfo(selected);
            Log.WriteDebug("Language selected: " + selected + " (preference: " + NormalizeLanguagePreference(preference) + ")");
        }


        private static void SaveBuiltinIfChanged(XmlDocument xml, string filePath)
        {
            using (var serialized = new MemoryStream())
            {
                xml.Save(serialized);
                byte[] expected = serialized.ToArray();
                if (File.Exists(filePath) && File.ReadAllBytes(filePath).SequenceEqual(expected))
                    return;
                File.WriteAllBytes(filePath, expected);
            }
        }

        /// <summary>升级内置语言包时,把"从未自定义"的用户副本同步为新内置包。
    /// 判定依据:用户副本的键值内容与旧内置包完全一致。自定义过的副本保持不动,
    /// 缺失的新键仍由 GetText 回退到内置包补齐。返回是否执行了同步。</summary>
        internal static bool RefreshUserPackIfUncustomized(string userFile, XmlDocument oldBuiltin, XmlDocument newBuiltin)
        {
            try
            {
                if (!File.Exists(userFile))
                    return false;
                var userPack = new XmlDocument() { XmlResolver = null };
                userPack.Load(userFile);
                var userMap = BuildTextMap(userPack);
                var oldMap = BuildTextMap(oldBuiltin);
                if (userMap.Count != oldMap.Count)
                    return false;
                foreach (KeyValuePair<string, string> pair in oldMap)
                {
                    string value = null;
                    if (!userMap.TryGetValue(pair.Key, out value) || !string.Equals(value, pair.Value, StringComparison.Ordinal))
                        return false;
                }
                newBuiltin.Save(userFile);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("User language pack refresh skipped for " + userFile + ": " + Log.SafeException(ex));
                return false;
            }
        }

        /// <summary>P-15: GetText used to build an XPath string and scan the whole
    /// XmlDocument three times per call (12-15 calls per 430ms tick). Cache the
    /// key/value map per XmlDocument instance instead. Keyed by *reference*, so
    /// restoring the original LanguageFile* fields (as the regression harness does)
    /// transparently restores the matching cache — no stale global dictionaries.
    /// User-edited packs get a fresh XmlDocument on reload, hence a fresh map.</summary>
        private sealed class XmlDocumentReferenceComparer : IEqualityComparer<XmlDocument>
        {

            public bool Equals(XmlDocument x, XmlDocument y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(XmlDocument obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<XmlDocument, Dictionary<string, string>> textMaps = new System.Collections.Concurrent.ConcurrentDictionary<XmlDocument, Dictionary<string, string>>(new XmlDocumentReferenceComparer());

        private static Dictionary<string, string> BuildTextMap(XmlDocument pack)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (pack is null || pack.DocumentElement is null)
                return map;
            foreach (XmlNode node in pack.DocumentElement.SelectNodes("Text"))
            {
                var keyAttr = node.Attributes["key"];
                if (keyAttr is null || string.IsNullOrEmpty(keyAttr.Value))
                    continue;
                string key = keyAttr.Value;
                // SelectSingleNode returned the first match; empty text was treated as missing.
                if (!map.ContainsKey(key) && !string.IsNullOrEmpty(node.InnerText))
                {
                    map[key] = node.InnerText;
                }
            }
            return map;
        }

        private static Dictionary<string, string> GetTextMap(XmlDocument pack)
        {
            if (pack is null)
                return null;
            Dictionary<string, string> map = null;
            if (!textMaps.TryGetValue(pack, out map))
            {
                map = BuildTextMap(pack);
                map = textMaps.GetOrAdd(pack, map);
            }
            return map;
        }

        private static string ProcessMsg(string msg)
        {
#if MSD
            if (string.IsNullOrEmpty(msg))
                return string.Empty;
            return msg.Replace("MegaDownloader", "MegaSearch Desktop");
#else
            return msg;
#endif
        }


        public static string GetText(string key)
        {
            if (key is null)
                return string.Empty;
            string text = null;
            // 1) Current language on disk (may be user-customized)
            var map = GetTextMap(LanguageFileDisk);
            if (map is not null && map.TryGetValue(key, out text))
                return ProcessMsg(text);

            // 2) Built-in pack for current language
            map = GetTextMap(LanguageFileInternal);
            if (map is not null && map.TryGetValue(key, out text))
                return ProcessMsg(text);

            // 3) Stable fallback to en-US
            if (!ReferenceEquals(LanguageFileEnUs, LanguageFileInternal))
            {
                map = GetTextMap(LanguageFileEnUs);
                if (map is not null && map.TryGetValue(key, out text))
                    return ProcessMsg(text);
            }

            Log.WriteDebug("Translation not found: " + key);
            return ProcessMsg(key);
        }

        // P-16: GetAvailableLanguages used to re-parse every user language XML on each
        // settings-window open. Cache the result keyed by file path + write time, so new
        // or edited packs are still picked up without any XML parsing on cache hits.
        private static readonly object _availableLanguagesLock = new object();
        private static Dictionary<string, string> _availableLanguagesCache = null;
        private static string _availableLanguagesStamp = null;

        private static string GetUserPackStamp(string directory)
        {
            var sb = new System.Text.StringBuilder();
            if (!Directory.Exists(directory))
                return string.Empty;
            foreach (string @file in Directory.GetFiles(directory, "*.xml").OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    sb.Append(Path.GetFileName(@file)).Append(':').Append(File.GetLastWriteTimeUtc(@file).Ticks).Append(';');
                }
                catch (Exception ex)
                {
                    return null; // force a full refresh when a file cannot be inspected
                }
            }
            return sb.ToString();
        }

        public static Dictionary<string, string> GetAvailableLanguages()
        {
            lock (_availableLanguagesLock)
            {
                string directory = GetLanguageDirectory();
                string stamp = GetUserPackStamp(directory);
                if (stamp is not null && _availableLanguagesCache is not null && string.Equals(stamp, _availableLanguagesStamp, StringComparison.Ordinal))
                {
                    return new Dictionary<string, string>(_availableLanguagesCache, StringComparer.OrdinalIgnoreCase);
                }

                var Lista = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, XmlDocument> entry in builtinPacks.Value.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
                    Lista[entry.Key] = entry.Value.DocumentElement.GetAttribute("name");
                foreach (KeyValuePair<string, XmlDocument> entry in LoadUserPacks(directory))
                {
                    string name = entry.Value.DocumentElement.GetAttribute("name");
                    Lista[entry.Key] = string.IsNullOrWhiteSpace(name) ? entry.Key : name;
                }
                _availableLanguagesCache = new Dictionary<string, string>(Lista, StringComparer.OrdinalIgnoreCase);
                _availableLanguagesStamp = stamp;
                return Lista;
            }
        }





        public static void SaveTranslationReport()
        {
            var str = new System.Text.StringBuilder();


            var htBase = new Dictionary<string, string>();
            string BaseLanguage = "en-US";


            // First we retrieve the base language
            foreach (string resname in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (resname.ToUpper().EndsWith("-Language.xml".ToUpper()))
                {
                    var @file = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname);

                    if (@file is null)
                        continue;

                    var xmlIdioma = new XmlDocument();
                    try
                    {
                        xmlIdioma.Load(@file);
                    }
                    finally
                    {
                        @file.Close();
                    }

                    string CodigoIdioma = xmlIdioma.DocumentElement.Attributes["id"].Value;
                    if ((CodigoIdioma ?? "") == (BaseLanguage ?? ""))
                    {
                        foreach (XmlNode textNode in xmlIdioma.DocumentElement.SelectNodes("Text"))
                        {
                            string KEY = textNode.Attributes["key"].Value;
                            string VALUE = textNode.InnerText;
                            htBase[KEY] = VALUE;
                        }
                    }
                }
            }

            // Then we compare with the rest of languages
            foreach (string resname in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (resname.ToUpper().EndsWith("-Language.xml".ToUpper()))
                {
                    var @file = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname);

                    if (@file is null)
                        continue;

                    var strIdi = new System.Text.StringBuilder();

                    var xmlIdioma = new XmlDocument();
                    try
                    {
                        xmlIdioma.Load(@file);
                    }
                    finally
                    {
                        @file.Close();
                    }

                    var htIdioma = new Dictionary<string, string>();

                    string CodigoIdioma = xmlIdioma.DocumentElement.Attributes["id"].Value;
                    foreach (XmlNode textNode in xmlIdioma.DocumentElement.SelectNodes("Text"))
                    {
                        string KEY = textNode.Attributes["key"].Value;
                        string VALUE = textNode.InnerText;
                        htIdioma[KEY] = VALUE;
                    }

                    foreach (string key in htBase.Keys)
                    {
                        if (!htIdioma.ContainsKey(key))
                        {
                            if (strIdi.Length == 0)
                            {
                                strIdi.AppendLine(Constants.vbNewLine);
                                strIdi.AppendLine("Missing text in " + CodigoIdioma + " that is present in " + BaseLanguage + ": " + Constants.vbNewLine);
                            }
                            strIdi.AppendLine("<Text key=\"" + key.Replace("&", "&amp;") + "\"><![CDATA[" + htBase[key] + "]]></Text>");
                        }
                    }
                    if (strIdi.Length > 0)
                    {
                        str.AppendLine(strIdi.ToString());
                    }
                }
            }


            if (str.Length > 0)
            {
                Log.WriteError(Constants.vbNewLine + "TRANSLATION REPORT" + Constants.vbNewLine + str.ToString());
            }
        }


    }
}