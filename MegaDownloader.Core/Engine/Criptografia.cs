using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace MegaDownloader
{

    public class Criptografia
    {

        #region Criptografía interna


        // Secrets at rest are handled by a platform protector. Windows keeps the original
        // DPAPI scheme (including its historical entropy chain, so existing configuration
        // files keep working); macOS and Linux use an AES-GCM key file instead, because
        // DPAPI does not exist there and every save used to fail with a platform exception.
        private static volatile Platform.ISecretProtector _protector;

        /// <summary>Installs the protector used for secrets written to disk.</summary>
        public static void SetSecretProtector(Platform.ISecretProtector protector)
        {
            _protector = protector;
        }

        internal static Platform.ISecretProtector SecretProtector
        {
            get
            {
                var existing = _protector;
                if (existing is not null)
                    return existing;

                // Lazy default: callers that never configure one (tests, the WinForms shell
                // before start-up completes) still get the platform's native scheme.
                var created = OperatingSystem.IsWindows()
                    ? (Platform.ISecretProtector)new Platform.WindowsDpapiSecretProtector()
                    : new Platform.AesFileSecretProtector(DefaultAppDataDirectory());
                _protector = created;
                return created;
            }
        }

        private static string DefaultAppDataDirectory() => ApplicationDataDirectory.GetLegacyRoot();

        public static string EncryptString_DPAPI(SecureString input)
        {
            string plaintext = ToInsecureString(input);
            return SecretProtector.Protect(plaintext);
        }

        public static SecureString DecryptString_DPAPI(string encryptedData)
        {
            string currentCiphertext = null;
            return DecryptStringWithCacheInfo(encryptedData, ref currentCiphertext);
        }

        internal static SecureString DecryptStringWithCacheInfo(string encryptedData, ref string currentCiphertext)
        {
            currentCiphertext = null;
            // Empty is the most common input by far (unset account/password fields), so
            // short-circuit it instead of walking the whole entropy chain.
            if (string.IsNullOrEmpty(encryptedData))
                return new SecureString();

            if (SecretProtector.TryUnprotect(encryptedData, out string plaintext, out bool currentKey))
            {
                // Reuse the stored ciphertext only when it was written with the current key;
                // a value from an older release is re-encrypted on the next save.
                if (currentKey && !string.IsNullOrEmpty(plaintext))
                    currentCiphertext = encryptedData;
                return ToSecureString(plaintext);
            }

            // A genuine blob that cannot be opened (config restored onto another machine or
            // account, or corrupted) must not be silently blanked: callers such as
            // DecryptPasswordField turn this into a re-entry prompt.
            Log.WriteWarning("DecryptString_DPAPI: the stored value could not be decrypted on this machine (cross-machine/user restore or corrupted value); returning empty, caller should prompt for re-entry");
            return new SecureString();
        }

        public static SecureString ToSecureString(string input)
        {
            // The framework converts the whole value without the per-character cost
            // of AppendChar. Keep the same read-only result and empty/null semantics.
            var secure = new System.Net.NetworkCredential(string.Empty, input ?? string.Empty).SecurePassword;
            secure.MakeReadOnly();
            return secure;
        }

        public static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }


        public static string AES_EncryptString(string vstrTextToBeEncrypted, string vstrEncryptionKey, bool useRandomIV = true)

        {

            return AES_EncryptString(vstrTextToBeEncrypted, vstrEncryptionKey, System.Text.Encoding.ASCII, useRandomIV);

        }
        public static string AES_EncryptString(string vstrTextToBeEncrypted, string vstrEncryptionKey, System.Text.Encoding Encoding, bool useRandomIV = true)


        {

            int intRemaining;
            int intLength;
            byte[] bytKey;

            intLength = Strings.Len(vstrEncryptionKey);


            // ********************************************************************
            // ******   Encryption Key must be 256 bits long (32 bytes)      ******
            // ******   If it is longer than 32 bytes it will be truncated.  ******
            // ******   If it is shorter than 32 bytes it will be padded     ******
            // ******   with upper-case Xs.                                  ****** 
            // ********************************************************************

            if (intLength >= 32)
            {
                vstrEncryptionKey = Strings.Left(vstrEncryptionKey, 32);
            }
            else
            {
                intLength = Strings.Len(vstrEncryptionKey);
                intRemaining = 32 - intLength;
                vstrEncryptionKey = vstrEncryptionKey + Strings.StrDup(intRemaining, "X");
            }

            bytKey = System.Text.Encoding.ASCII.GetBytes(vstrEncryptionKey.ToCharArray());

            return AES_EncryptString(vstrTextToBeEncrypted, bytKey, Encoding, useRandomIV);

        }

        // 旧格式密文复用的静态 IV。仅为兼容历史落盘数据保留读取能力；
        // 新写入的数据一律 useRandomIV=True（见下方重载）。
        private static readonly byte[] LegacyStaticIV = new byte[] { 121, 241, 10, 1, 132, 74, 11, 39, 255, 91, 45, 78, 14, 211, 22, 62 };

        public static string AES_EncryptString(string vstrTextToBeEncrypted, byte[] bytKey, System.Text.Encoding Encoding, bool useRandomIV = true)


        {

            byte[] bytValue;
            byte[] bytEncoded = null;

            vstrTextToBeEncrypted = StripNullCharacters(vstrTextToBeEncrypted + ""); // Evitamos nothing

            bytValue = Encoding.GetBytes(vstrTextToBeEncrypted.ToCharArray());

            // CBC 模式下复用 IV 会泄露明文前缀相等性。默认每次加密生成随机 IV，
            // 并以 {版本字节=1}||{IV}||{密文} 的自描述格式存储（总长 mod 16 == 1，
            // 与旧格式密文（mod 16 == 0）无歧义）。useRandomIV=False 仅供需要被
            // 旧版本程序解密的对外格式（如兼容模式编码链接）使用。
            var bytIV = new byte[16];
            if (useRandomIV)
            {
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(bytIV);
                }
            }
            else
            {
                Array.Copy(LegacyStaticIV, bytIV, 16);
            }

            try
            {
                using (var objRijndaelManaged = new RijndaelManaged())
                {
                    using (var objMemoryStream = new MemoryStream())
                    {
                        using (var objCryptoStream = new CryptoStream(objMemoryStream, objRijndaelManaged.CreateEncryptor(bytKey, bytIV), CryptoStreamMode.Write, leaveOpen: true))

                        {
                            objCryptoStream.Write(bytValue, 0, bytValue.Length);
                            objCryptoStream.FlushFinalBlock();
                        }
                        bytEncoded = objMemoryStream.ToArray();
                    }
                }
            }
            catch (CryptographicException ex)
            {
                Log.WriteError("AES_EncryptString failed: " + ex.ToString());
            }
            catch (Exception ex)
            {
                Log.WriteError("AES_EncryptString unexpected error: " + ex.ToString());
            }

            // 失败返回 Nothing（而非空串）：调用方能区分“加密失败”与“合法空值”，
            // 避免把静默空值写进配置/链接。
            if (bytEncoded is null)
                return null;

            if (useRandomIV)
            {
                var result = new byte[bytEncoded.Length + 16 + 1];
                result[0] = 1;
                Array.Copy(bytIV, 0, result, 1, 16);
                Array.Copy(bytEncoded, 0, result, 17, bytEncoded.Length);
                return Convert.ToBase64String(result);
            }

            return Convert.ToBase64String(bytEncoded);

        }

        public static string AES_DecryptString(string vstrStringToBeDecrypted, string vstrDecryptionKey)
        {

            return AES_DecryptString(vstrStringToBeDecrypted, vstrDecryptionKey, System.Text.Encoding.ASCII);

        }

        public static string AES_DecryptString(string vstrStringToBeDecrypted, string vstrDecryptionKey, System.Text.Encoding Encoding)

        {

            int intLength;
            int intRemaining;
            byte[] bytDecryptionKey;

            intLength = Strings.Len(vstrDecryptionKey);

            if (intLength >= 32)
            {
                vstrDecryptionKey = Strings.Left(vstrDecryptionKey, 32);
            }
            else
            {
                intLength = Strings.Len(vstrDecryptionKey);
                intRemaining = 32 - intLength;
                vstrDecryptionKey = vstrDecryptionKey + Strings.StrDup(intRemaining, "X");
            }

            bytDecryptionKey = System.Text.Encoding.ASCII.GetBytes(vstrDecryptionKey.ToCharArray());

            return AES_DecryptString(vstrStringToBeDecrypted, bytDecryptionKey, Encoding);

        }

        public static string AES_DecryptString(string vstrStringToBeDecrypted, byte[] bytDecryptionKey, System.Text.Encoding Encoding)

        {

            byte[] bytDataToBeDecrypted;
            byte[] bytPlain = null;

            // ********************************************************************
            // ******   Encryption Key must be 256 bits long (32 bytes)      ******
            // ******   If it is longer than 32 bytes it will be truncated.  ******
            // ******   If it is shorter than 32 bytes it will be padded     ******
            // ******   with upper-case Xs.                                  ******
            // ********************************************************************


            try
            {
                bytDataToBeDecrypted = Convert.FromBase64String(vstrStringToBeDecrypted);

                // 自动识别密文格式：新格式 {1}||{IV16}||{CBC密文} 总长 mod 16 == 1；
                // 旧格式为纯 CBC 密文，总长 mod 16 == 0。两种长度不重叠，无歧义。
                var bytIV = new byte[16];
                byte[] bytCipher;
                if (bytDataToBeDecrypted.Length > 17 && bytDataToBeDecrypted.Length % 16 == 1 && bytDataToBeDecrypted[0] == 1)

                {
                    Array.Copy(bytDataToBeDecrypted, 1, bytIV, 0, 16);
                    bytCipher = new byte[bytDataToBeDecrypted.Length - 18 + 1];
                    Array.Copy(bytDataToBeDecrypted, 17, bytCipher, 0, bytCipher.Length);
                }
                else
                {
                    Array.Copy(LegacyStaticIV, bytIV, 16);
                    bytCipher = bytDataToBeDecrypted;
                }

                using (var objRijndaelManaged = new RijndaelManaged())
                {
                    using (var objMemoryStream = new MemoryStream(bytCipher))
                    {
                        using (var objCryptoStream = new CryptoStream(objMemoryStream, objRijndaelManaged.CreateDecryptor(bytDecryptionKey, bytIV), CryptoStreamMode.Read, leaveOpen: true))

                        {
                            using (var plainStream = new MemoryStream())
                            {
                                objCryptoStream.CopyTo(plainStream);
                                bytPlain = plainStream.ToArray();
                            }
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                Log.WriteError("AES_DecryptString failed: " + ex.ToString());
            }
            catch (FormatException ex)
            {
                Log.WriteError("AES_DecryptString failed (invalid input): " + ex.ToString());
            }
            catch (Exception ex)
            {
                Log.WriteError("AES_DecryptString unexpected error: " + ex.ToString());
            }

            if (bytPlain is null)
                return string.Empty;
            return StripNullCharacters(Encoding.GetString(bytPlain));

        }


        private static string StripNullCharacters(string vstrStringWithNulls)
        {
            // 旧实现按“原始字符串里的位置”在“已缩短的副本”上删除 null：
            // 每删一个字符副本变短，后续删除位置整体偏移，单个 null 就会把
            // 后续内容全部截断。Replace 一次遍历删除所有 null，无位置偏移。
            if (vstrStringWithNulls is null)
                return string.Empty;
            return vstrStringWithNulls.Replace(Constants.vbNullChar, string.Empty);
        }




        #endregion

        #region Criptografía MEGA

        /// <summary>
    /// 常量时间字节比较，避免逐字符短路比较泄露“匹配到第几位”的时序信号。
    /// </summary>
        internal static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a is null || b is null || a.Length != b.Length)
                return false;
            byte diff = 0;
            for (int i = 0, loopTo = a.Length - 1; i <= loopTo; i++)
                diff = (byte)(diff | a[i] ^ b[i]);
            return diff == 0;
        }

        internal static byte[] Utf8BytesOrNull(string s)
        {
            if (s is null)
                return new byte[] { };
            return System.Text.Encoding.UTF8.GetBytes(s);
        }


        internal static string GetFileKeyFromPreSharedKey(string PreSharedKey)
        {
            // MEGA pre-shared keys are base64url text. Silently truncating non-ASCII
            // characters to their low byte (as GetBytes did) derived a WRONG key with no
            // hint to the user — reject such keys explicitly instead.
            if (PreSharedKey is not null)
            {
                foreach (char c in PreSharedKey)
                {
                    if (c > Strings.ChrW(255))
                    {
                        Log.WriteError("GetFileKeyFromPreSharedKey: the pre-shared key contains a non-ASCII character (U+" + Strings.AscW(c).ToString("X4") + ") and cannot be valid; skipping this key.");
                        return null;
                    }
                }
            }

            string PSK = PreSharedKey.PadRight(24, '#').Substring(0, 24);

            byte[] bitArray = GetBytes(PSK);

            int[] hashKey = new[] { -1815844893, 2108737444, -776061055, 22203222, 1885434739, 2003792484 };





            byte[] hasharray = IntArrayToBytesArray(hashKey);
            if (bitArray.Length == hasharray.Length)
            {
                for (int i = 0, loopTo = bitArray.Length - 1; i <= loopTo; i++)
                {
                    bitArray[i] = (byte)(bitArray[i] ^ i);
                    bitArray[i] = (byte)(bitArray[i] ^ hasharray[i]);
                }
            }

            int[] intKey = ByteArrayToIntArray(bitArray);

            int[] Key = new int[] { intKey[0] ^ intKey[4], intKey[1] ^ intKey[5], intKey[2], intKey[3], intKey[4], intKey[5], 0, 0 };
            return a32_to_base64(Key);
        }

        private static byte[] GetBytes(string str)
        {
            var l = new List<byte>();
            foreach (char c in str)
                l.Add(BitConverter.GetBytes(c)[0]);
            return l.ToArray();
        }

        private static string a32_to_str(int[] a)
        {
            string b = "";
            for (int i = 0, loopTo = a.Length * 4 - 1; i <= loopTo; i++)
            {
                int val = a[i >> 2];
                int val2 = 24 - (i & 3) * 8;
                int CodCharacter = ZFRS(val, val2) & 255;
                // Case 156
                // No hacemos nada, en javascript "String.fromCharCode(156)" no devuelve nada
                switch (CodCharacter)
                {
                    default:
                        b += Conversions.ToString(Strings.ChrW(CodCharacter));
                        break;
                }
            }
            return b;
        }
        private static int ZFRS(int i, int j)
        {
            bool maskIt = i < 0;
            i = i >> j;
            if (maskIt)
            {
                i = i & 0x7FFFFFFF;
            }
            return i;
        }

        internal static SicSeekableBlockCipher GetInstaceCipher(string pKey)
        {
            byte[] b64Dec = B64Decode(pKey);
            int[] intKey = ByteArrayToIntArray(b64Dec);
            // A MEGA file key is 8 words (key + nonce + MetaMAC); the expression below indexes
            // up to intKey(7) unconditionally. ExtraerFileKey only enforces a minimum of 40
            // characters, so a 40..42 character key decodes to 7 words and used to die with a
            // bare IndexOutOfRangeException here.
            if (intKey.Length < 8)
            {
                throw new FormatException("Invalid MEGA file key: expected 8 key words, got " + intKey.Length + " (the link appears to be truncated or corrupted).");
            }
            int[] keyNOnce = new int[] { intKey[0] ^ intKey[4], intKey[1] ^ intKey[5], intKey[2] ^ intKey[6], intKey[3] ^ intKey[7], intKey[4], intKey[5] };
            byte[] key = IntArrayToBytesArray(new int[] { keyNOnce[0], keyNOnce[1], keyNOnce[2], keyNOnce[3] });
            byte[] iv = IntArrayToBytesArray(new int[] { keyNOnce[4], keyNOnce[5], 0, 0 });
            SicSeekableBlockCipher cipher = null;
            cipher = new SicSeekableBlockCipher(new AesEngine());
            var ivAndKey = new ParametersWithIV(new KeyParameter(key), iv);
            cipher.Init(false, ivAndKey);
            return cipher;
        }

        internal static string AES_MEGA_DecryptString(string pEnc, string pKey)
        {
            byte[] b64Dec = B64Decode(pKey);
            int[] intKey = ByteArrayToIntArray(b64Dec);
            byte[] key;
            if (intKey.Length == 4)
            {
                key = IntArrayToBytesArray(new int[] { intKey[0], intKey[1], intKey[2], intKey[3] });
            }
            else if (intKey.Length >= 8)
            {
                key = IntArrayToBytesArray(new int[] { intKey[0] ^ intKey[4], intKey[1] ^ intKey[5], intKey[2] ^ intKey[6], intKey[3] ^ intKey[7] });
            }
            else
            {
                // 5..7 words: the Else branch above would index past the end of the array.
                throw new FormatException("Invalid MEGA key: expected 4 or at least 8 key words, got " + intKey.Length + ".");
            }

            byte[] iv = IntArrayToBytesArray(new int[] { 0, 0, 0, 0 });

            byte[] unPadded = B64Decode(pEnc);
            int len = 16 - (unPadded.Length - 1 & 15) - 1;
            byte[] payLoadBytes = new byte[unPadded.Length + (len - 1) + 1];
            Array.Copy(unPadded, 0, payLoadBytes, 0, unPadded.Length);

            return DecryptStringFromBytesAes(payLoadBytes, key, iv);
        }

        private static string DecryptStringFromBytesAes(byte[] pCipherText, byte[] pKey, byte[] pIV)
        {
            // Check arguments.
            if (pCipherText is null || pCipherText.Length <= 0)
            {
                throw new ArgumentNullException("pCipherText");
            }
            if (pKey is null || pKey.Length <= 0)
            {
                throw new ArgumentNullException("pKey");
            }
            if (pIV is null || pIV.Length <= 0)
            {
                throw new ArgumentNullException("pIV");
            }

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an Aes object
            // with the specified key and IV.
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.None;

                aesAlg.Key = pKey;
                aesAlg.IV = pIV;

                // Create a decrytor to perform the stream transform.
                var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                // Create the streams used for decryption.
                using (var msDecrypt = new MemoryStream(pCipherText))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }

                }
            }

            return plaintext;
        }

        internal static int[] decrypt_key(int[] Data, int[] keyhash)
        {

            using (var aesAlg = new AesManaged())
            {

                aesAlg.KeySize = 128;
                aesAlg.BlockSize = 128;


                aesAlg.Key = IntArrayToBytesArrayREVERSE(keyhash);
                aesAlg.IV = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.Zeros;


                byte[] Buffer = IntArrayToBytesArrayREVERSE(Data);

                var buffer2 = new byte[Buffer.Length];
                // AES 块大小固定 128 位;循环内创建的 ICryptoTransform 必须 Using 释放
                // (此前每次迭代 New 一个 decryptor 从不 Dispose,累计泄漏)
                const int AES_BLOCK = 16;
                for (int i = 0, loopTo = Buffer.Length - 1; i <= loopTo; i += AES_BLOCK)
                {
                    using (var dec = aesAlg.CreateDecryptor())
                    {
                        dec.TransformBlock(Buffer, i, AES_BLOCK, buffer2, i);
                    }
                }
                int[] l = ByteArrayToIntArrayREVERSE(buffer2);
                return l;

            }

        }

        internal static int[] ByteArrayToIntArrayREVERSE(byte[] pBytes)
        {
            var b = new int[((int)Math.Round(Math.Ceiling(pBytes.Count() / 4d)))];
            int x = 0;

            for (int i = 0, loopTo = (int)Math.Round(pBytes.Length / 4d) - 1; i <= loopTo; i++)
            {
                if (4 * (i + 1) <= pBytes.Length)
                {
                    var l = new List<byte>();
                    for (int z = i * 4 + 3, loopTo1 = i * 4; z >= loopTo1; z -= 1)
                        l.Add(pBytes[z]);

                    b[x] = BitConverter.ToInt32(l.ToArray(), 0);
                    x += 1;
                }
            }
            return b;

        }
        private static byte[] IntArrayToBytesArrayREVERSE(IEnumerable<int> pInts)
        {
            var res = new byte[(pInts.Count() * 4)];
            int i = 0;
            foreach (int t in pInts)
            {
                byte[] b = BitConverter.GetBytes(t);
                for (int j = b.Length - 1; j >= 0; j -= 1)
                {
                    res[i] = b[j];
                    i += 1;
                }
            }
            return res;
        }


        internal static int[] base64_to_a32(string pData)
        {
            return str_to_a32(base64urldecode(pData));
        }

        internal static string a32_to_base64(int[] a)
        {
            string str = a32_to_str(a);
            str = EncodeTo64(str);
            str = str.Replace("+", "-").Replace("/", "_").Replace("=", "");
            return str;
        }

        private static string EncodeTo64(string toEncode)
        {
            string returnValue = Convert.ToBase64String(GetBytes(toEncode));
            return returnValue;
        }

        private static string base64urldecode(string pData)
        {
            byte[] bytes = base64urldecodeBytes(pData);
            string RS = "";
            foreach (byte b in bytes)
                RS += Conversions.ToString(Strings.ChrW(b));
            return RS;
        }

        internal static int[] str_to_a32(string b)
        {
            int[] a = new int[(b.Length + 3 >> 2)];

            for (int i = 0, loopTo = b.Length - 1; i <= loopTo; i++)
                a[i >> 2] = a[i >> 2] | Strings.AscW(Convert.ToChar(b.Substring(i, 1))) << 24 - (i & 3) * 8;

            return a;
        }

        private static byte[] B64Decode(string pData)
        {
            // base64url without padding: a length of %4 == 1 cannot be produced by any real
            // encoder. Without this guard "(2 - len*3) And 3" evaluates to 3 and the two-char
            // "==".Substring(3) throws a bare ArgumentOutOfRangeException. URLExtractor applies
            // the same guard to enc?/fenc? links; plain mega.nz keys had none.
            if (pData is null || pData.Length % 4 == 1)
            {
                throw new FormatException("Invalid base64url input (length " + (pData is null ? -1 : pData.Length) + "); the link appears to be truncated or corrupted.");
            }
            pData += "==".Substring(2 - pData.Length * 3 & 3);
            pData = pData.Replace("-", "+").Replace("_", "/").Replace(",", "");
            return Convert.FromBase64String(pData);
        }

        private static int[] ByteArrayToIntArray(byte[] pBytes)
        {
            var b = new List<int>();
            for (int i = 0, loopTo = (int)Math.Round(pBytes.Length / 4d) - 1; i <= loopTo; i++)
            {
                if (4 * (i + 1) <= pBytes.Length)
                    b.Add(BitConverter.ToInt32(pBytes, i * 4));
            }
            return b.ToArray();
        }

        private static byte[] IntArrayToBytesArray(IEnumerable<int> pInts)
        {
            var res = new List<byte>();
            foreach (int t in pInts)
                res.AddRange(BitConverter.GetBytes(t));
            return res.ToArray();
        }



        public static string base64urlencode(byte[] pData)
        {
            string d = Convert.ToBase64String(pData);
            d = d.Replace("+", "-").Replace("/", "_").Replace("=", "");
            return d;
        }

        public static byte[] base64urldecodeBytes(string pData)
        {
            // Same %4 == 1 guard as B64Decode; see the note there.
            if (pData is null || pData.Length % 4 == 1)
            {
                throw new FormatException("Invalid base64url input (length " + (pData is null ? -1 : pData.Length) + ").");
            }
            pData += "==".Substring(2 - pData.Length * 3 & 3);
            pData = pData.Replace("-", "+").Replace("_", "/").Replace(",", "");
            byte[] bytes = Convert.FromBase64String(pData);
            return bytes;
        }

        #endregion

        #region SicSeekableBlockCipher

        // Copied from SICBlockCipher.cs (BouncyCastle C#) with a new "IncrementCounter" property 
        public class SicSeekableBlockCipher : IBlockCipher, IDisposable
        {

            private readonly IBlockCipher cipher;
            private readonly int blockSize;
            private readonly byte[] IV;
            private readonly byte[] counter;
            private readonly byte[] counterOut;

            // P1-3: batch the counter blocks into one system-AES ECB transform (AES-NI).
            // Any failure falls back to the per-block BouncyCastle path.
            private const int WindowsAesBatchBytes = 4096;
            private System.Security.Cryptography.Aes _windowsAes;
            private System.Security.Cryptography.ICryptoTransform _windowsEncryptor;
            private byte[] _counterBatch;
            private byte[] _keystreamBatch;

            // *
            // * Basic constructor.
            // *
            // * @param c the block cipher to be used.
            // 

            public SicSeekableBlockCipher(IBlockCipher cipher)
            {
                this.cipher = cipher;
                blockSize = cipher.GetBlockSize();
                IV = new byte[blockSize];
                counter = new byte[blockSize];
                counterOut = new byte[blockSize];
            }

            // ignored by this CTR mode
            public void Init(bool forEncryption, ICipherParameters parameters)
            {
                if (parameters is ParametersWithIV)
                {
                    ParametersWithIV ivParam = (ParametersWithIV)parameters;
                    byte[] iv__1 = ivParam.GetIV();
                    Array.Copy(iv__1, 0, IV, 0, IV.Length);

                    Reset();
                    cipher.Init(true, ivParam.Parameters);
                    ConfigureWindowsAes(ivParam.Parameters);
                }
                else
                {
                    throw new ArgumentException("SIC mode requires ParametersWithIV", "parameters");
                }
            }

            private void ConfigureWindowsAes(ICipherParameters parameters)
            {
                DisposeWindowsAes();
                if (blockSize != 16 || cipher is not AesEngine)
                    return;
                if (parameters is not KeyParameter keyParam)
                    return;
                byte[] keyBytes = keyParam.GetKey();
                if (keyBytes is null || (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32))
                    return;
                try
                {
                    // Aes.Create() dispatches to the OS AES (AES-NI) on every platform.
                    var aes = System.Security.Cryptography.Aes.Create();
                    _windowsAes = aes; // register first so catch disposes it
                    aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                    aes.Padding = System.Security.Cryptography.PaddingMode.None;
                    aes.Key = keyBytes;
                    _windowsEncryptor = aes.CreateEncryptor();
                    _counterBatch = new byte[WindowsAesBatchBytes];
                    _keystreamBatch = new byte[WindowsAesBatchBytes];
                }
                catch
                {
                    DisposeWindowsAes();
                }
            }

            private void DisposeWindowsAes()
            {
                _windowsEncryptor?.Dispose();
                _windowsEncryptor = null;
                _windowsAes?.Dispose();
                _windowsAes = null;
                _counterBatch = null;
                _keystreamBatch = null;
            }

            public void Dispose() => DisposeWindowsAes();

            public string AlgorithmName
            {
                get
                {
                    return cipher.AlgorithmName + "/SIC";
                }
            }

            // BouncyCastle 2.x 从 IBlockCipher 移除了 IsPartialBlockOkay（恒 True 的能力查询，
            // CTR/Seek 逻辑本就按整块处理），保留为普通成员供内部语义自述，不再挂 Implements。
            public bool IsPartialBlockOkay
            {
                get
                {
                    return true;
                }
            }

            public int GetBlockSize()
            {
                return cipher.GetBlockSize();
            }

            public int ProcessBlock(byte[] input, int inOff, byte[] output, int outOff)
            {
                cipher.ProcessBlock(counter, 0, counterOut, 0);

                //
                // XOR the counterOut with the plaintext producing the cipher text
                //
                for (int i = 0, loopTo = counterOut.Length - 1; i <= loopTo; i++)
                    output[outOff + i] = (byte)(counterOut[i] ^ input[inOff + i]);

                IncrementCounter();

                return counter.Length;
            }

            /// <summary>
            /// Equivalent to blockCount consecutive ProcessBlock calls, but batches counter
            /// blocks into one system-AES ECB transform and XORs them in bulk.
            /// </summary>
            public void ProcessBlocks(byte[] input, int inOff, byte[] output, int outOff, int blockCount)
            {
                if (blockCount <= 0)
                    return;
                if (_windowsEncryptor is null || _counterBatch is null)
                {
                    int bs = blockSize;
                    for (int i = 0; i < blockCount; i++)
                        ProcessBlock(input, inOff + i * bs, output, outOff + i * bs);
                    return;
                }
                int maxBlocks = _counterBatch.Length / blockSize;
                int done = 0;
                while (done < blockCount)
                {
                    int n = Math.Min(maxBlocks, blockCount - done);
                    int byteCount = n * blockSize;
                    for (int b = 0; b < n; b++)
                    {
                        Array.Copy(counter, 0, _counterBatch, b * blockSize, blockSize);
                        AddToCounter(1);
                    }
                    _windowsEncryptor.TransformBlock(_counterBatch, 0, byteCount, _keystreamBatch, 0);
                    int inBase = inOff + done * blockSize;
                    int outBase = outOff + done * blockSize;
                    for (int i = 0; i < byteCount; i++)
                        output[outBase + i] = (byte)(_keystreamBatch[i] ^ input[inBase + i]);
                    done += n;
                }
            }

            // BouncyCastle 2.x added this span overload to IBlockCipher. Keep both paths
            // using the identical CTR/XOR/increment logic; the array overload above is
            // retained for callers that hold byte[] buffers.
            public int ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output)
            {
                cipher.ProcessBlock(counter, 0, counterOut, 0);
                for (int i = 0; i < counterOut.Length; i++)
                    output[i] = (byte)(counterOut[i] ^ input[i]);
                IncrementCounter();
                return counter.Length;
            }

            // BouncyCastle 2.x 删除了 IBlockCipher.Reset（连 AesEngine 的具体实现一并移除，
            // 上游认定无状态清理语义）。此处仅复位自有 IV→counter；底层 AES 无块间状态，
            // 每次 Init 已重建工作密钥，行为与旧版一致。
            public void Reset()
            {
                Array.Copy(IV, 0, counter, 0, counter.Length);
            }

            /// <summary>
        /// Seeks CTR to the block that covers the given absolute file offset (floor division by block size).
        /// </summary>
            public void SeekToFileOffset(long fileOffset)
            {
                if (fileOffset < 0L)
                    throw new ArgumentOutOfRangeException("fileOffset");
                Reset();
                long blockIndex = fileOffset / blockSize;
                IncrementCounter(blockIndex);
            }

            public void IncrementCounter(long NumberOfIncrements = 1L)
            {
                if (NumberOfIncrements <= 0L)
                    return;
                AddToCounter(NumberOfIncrements);
            }

            private void AddToCounter(long value)
            {
                if (value <= 0L)
                    return;
                long carry = value;
                for (int i = counter.Length - 1; i >= 0; i -= 1)
                {
                    if (carry == 0L)
                        break;
                    long sum = counter[i] + (carry & 0xFFL);
                    counter[i] = (byte)(sum & 0xFFL);
                    carry = (carry >> 8) + (sum >> 8);
                }
            }

        }

        #endregion

        #region MEGA MetaMAC

        /// <summary>
    /// Verifies MEGA file MetaMAC (words 6-7 of the 8-word file key) over plaintext on disk.
    /// Public link keys carry only 4 words (16 bytes, the AES key itself) and do NOT
    /// embed a MetaMAC — verification is impossible for them and is skipped (True).
    /// Only 8-word (32-byte) node keys, obtained e.g. from folder API responses,
    /// contain the nonce (words 4-5) and MetaMAC (words 6-7) needed to verify.
    /// The chunk-size schedule is MEGA's ChunkedHash: 128 KiB * i for the first 8
    /// chunks (128, 256, 384, 512, 640, 768, 896 KiB, then 1 MiB), and a fixed 1 MiB
    /// for every chunk after that. This mirrors the MEGA SDK's
    /// ChunkedHash::chunkfloor/chunkceil (SEGSIZE = 131072).
    /// </summary>
        internal static bool VerifyMegaMetaMac(string filePath, string pKey)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
            if (string.IsNullOrEmpty(pKey))
                return false;

            string keyWithoutN = pKey;
            if (keyWithoutN.Contains("=###n="))
            {
                keyWithoutN = keyWithoutN.Substring(0, keyWithoutN.IndexOf("=###n="));
            }

            // Check the shape before decoding: B64Decode throws on a structurally impossible
            // base64url length, but this function's contract is to report "cannot verify"
            // rather than to throw at its callers on the download path.
            if (keyWithoutN.Length % 4 == 1)
            {
                // 第15项:日志已声明 skipping,跳过绝不能当失败(同下方 4 词公链跳过分支的语义),
                // 否则畸形密钥会让每个完成的下载都报完整性错误。
                Log.WriteWarning("VerifyMegaMetaMac: key length " + keyWithoutN.Length + " is not valid base64url; skipping end-to-end verification for " + filePath);
                return true;
            }

            byte[] b64Dec = B64Decode(keyWithoutN);
            int[] intKey = ByteArrayToIntArray(b64Dec);
            if (intKey is null || intKey.Length < 4)
                return false;

            if (intKey.Length < 8)
            {
                // 4-word public link key: no MetaMAC is embedded in the key, so there is
                // nothing to verify against. Skipping must NOT be treated as a failure —
                // treating it as one wrongly errored every completed public-link download.
                // B3:跳过即无端到端校验(仅剩长度匹配,而 .part 预分配使长度恒真),
                // 必须 Warning 留痕,不能让用户误以为"已校验"。Info 级会被正常日志淹没。
                Log.WriteWarning("VerifyMegaMetaMac: key has " + intKey.Length + " words (public link key); no MetaMAC available, skipping end-to-end verification for " + filePath);
                return true;
            }

            byte[] aesKey = IntArrayToBytesArray(new int[] { intKey[0] ^ intKey[4], intKey[1] ^ intKey[5], intKey[2] ^ intKey[6], intKey[3] ^ intKey[7] });
            int[] expectedMac = new int[] { intKey[6], intKey[7] };

            // MEGA chunk-MAC initialisation: the first 8 bytes of each chunk's CBC-MAC
            // IV are the file nonce (key words 4-5) and the second 8 bytes repeat the
            // same nonce (SDK: SymmCipher::ctr_crypt does memcpy(mac, ctr, 8) twice).
            int[] nonceWords = new int[] { intKey[4], intKey[5] };

            return VerifyMegaFileMac(filePath, aesKey, expectedMac, nonceWords);
        }

        /// <summary>
    /// Verifies the MEGA file MetaMAC using the SDK's ChunkedHash schedule: chunk sizes
    /// are 128 KiB * i for the first 8 chunks (i = 1..8), then a fixed 1 MiB. Each chunk
    /// contributes a CBC-MAC whose initial IV is the file nonce repeated twice
    /// ([n0, n1, n0, n1] — SDK: SymmCipher::ctr_crypt initialises mac = [ctriv, ctriv]),
    /// with a zero-padded final partial block, folded into the file MAC starting from
    /// zero with fileMac = AES(fileMac XOR chunkMac).
    /// 
    /// The final file MAC is compared with the expected value exactly once, after the
    /// whole file has been read — the same single full comparison the SDK performs
    /// (generateMetaMac + macsmac). Any mismatch means the bytes on disk genuinely
    /// differ from the uploaded file.
    /// </summary>
        /// <summary>
        /// P2-2/P2-1: a CBC-MAC chain is mathematically CBC encryption with the file
        /// nonce repeated twice as IV. Use the system AES (AES-NI) for a whole MetaMAC
        /// chunk at once and, for large files, compute chunks in parallel while the
        /// file is read sequentially. Falls back to the legacy per-block BouncyCastle
        /// path when a system AES is unavailable; the result is byte-identical.
        /// </summary>
        private static bool VerifyMegaFileMac(string filePath, byte[] aesKey, int[] expectedMac, int[] nonceWords)
        {
            long fileLen = new FileInfo(filePath).Length;
            if (fileLen == 0L)
                return expectedMac[0] == 0 && expectedMac[1] == 0;
            byte[] iv = BuildMacIv(nonceWords);
            if (CreateMacCipher(aesKey, iv) is null)
                return VerifyMegaFileMacLegacy(filePath, aesKey, expectedMac, nonceWords);
            var chunks = BuildMacChunks(fileLen);
            int[] fileMac = chunks.Count >= 8 && Environment.ProcessorCount >= 2
                ? ComputeMacCbcParallel(filePath, aesKey, iv, chunks)
                : ComputeMacCbcSequential(filePath, aesKey, iv, chunks);
            return (fileMac[0] ^ fileMac[1]) == expectedMac[0] && (fileMac[2] ^ fileMac[3]) == expectedMac[1];
        }

        private struct MacChunk
        {
            public long Start;
            public int Size;
        }

        private static byte[] BuildMacIv(int[] nonceWords)
        {
            var iv = new byte[16];
            WriteWordLE(iv, 0, nonceWords[0]);
            WriteWordLE(iv, 4, nonceWords[1]);
            WriteWordLE(iv, 8, nonceWords[0]);
            WriteWordLE(iv, 12, nonceWords[1]);
            return iv;
        }

        /// <summary>MEGA ChunkedHash: 128 KiB * i for the first 8 chunks, then 1 MiB.</summary>
        private static List<MacChunk> BuildMacChunks(long fileLen)
        {
            var chunks = new List<MacChunk>();
            long start = 0L;
            int number = 1;
            while (start < fileLen)
            {
                long size = number <= 8 ? number * 0x20000L : 0x100000L;
                int thisSize = (int)Math.Min(size, fileLen - start);
                chunks.Add(new MacChunk { Start = start, Size = thisSize });
                start += thisSize;
                number += 1;
            }
            return chunks;
        }

        private static System.Security.Cryptography.Aes CreateMacCipher(byte[] aesKey, byte[] iv)
        {
            try
            {
                // AesCng is Windows-only; Aes.Create() uses the OS AES on every platform.
                var aes = System.Security.Cryptography.Aes.Create();
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                aes.Key = aesKey;
                aes.IV = iv;
                return aes;
            }
            catch
            {
                return null;
            }
        }

        private static int ReadAtMost(FileStream fs, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = fs.Read(buffer, total, count - total);
                if (n == 0)
                    break;
                total += n;
            }
            return total;
        }

        /// <summary>CBC-encrypt one chunk (zero-padded tail); its last ciphertext block is the chunk MAC.</summary>
        private static void ComputeChunkCbc(byte[] aesKey, byte[] iv, byte[] input, int inOff, int paddedSize,
            byte[] output, int outOff, byte[] macs, int macOff)
        {
            if (paddedSize <= 0)
            {
                Array.Copy(iv, 0, macs, macOff, 16);
                return;
            }
            using var aes = CreateMacCipher(aesKey, iv);
            using var enc = aes.CreateEncryptor();
            enc.TransformBlock(input, inOff, paddedSize, output, outOff);
            Array.Copy(output, outOff + paddedSize - 16, macs, macOff, 16);
        }

        /// <summary>fileMac = AES(fileMac XOR chunkMac), matching the SDK macsmac fold.</summary>
        private static void FoldChunkMac(AesEngine engine, int[] fileMac, byte[] macs, int macOff,
            byte[] aesInput, byte[] aesOutput)
        {
            int m0 = fileMac[0] ^ BitConverter.ToInt32(macs, macOff);
            int m1 = fileMac[1] ^ BitConverter.ToInt32(macs, macOff + 4);
            int m2 = fileMac[2] ^ BitConverter.ToInt32(macs, macOff + 8);
            int m3 = fileMac[3] ^ BitConverter.ToInt32(macs, macOff + 12);
            WriteWordLE(aesInput, 0, m0);
            WriteWordLE(aesInput, 4, m1);
            WriteWordLE(aesInput, 8, m2);
            WriteWordLE(aesInput, 12, m3);
            engine.ProcessBlock(aesInput, 0, aesOutput, 0);
            fileMac[0] = BitConverter.ToInt32(aesOutput, 0);
            fileMac[1] = BitConverter.ToInt32(aesOutput, 4);
            fileMac[2] = BitConverter.ToInt32(aesOutput, 8);
            fileMac[3] = BitConverter.ToInt32(aesOutput, 12);
        }

        private static int[] ComputeMacCbcSequential(string filePath, byte[] aesKey, byte[] iv, List<MacChunk> chunks)
        {
            const int MaxChunk = 0x100000;
            var fileMac = new int[] { 0, 0, 0, 0 };
            var engine = new AesEngine();
            engine.Init(true, new KeyParameter(aesKey));
            var inBuf = new byte[MaxChunk + 15];
            var outBuf = new byte[MaxChunk + 15];
            var macs = new byte[16];
            var aesInput = new byte[16];
            var aesOutput = new byte[16];
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0x10000))
            {
                foreach (var chunk in chunks)
                {
                    int total = ReadAtMost(fs, inBuf, chunk.Size);
                    int padded = ((total + 15) / 16) * 16;
                    if (padded > total)
                        Array.Clear(inBuf, total, padded - total);
                    ComputeChunkCbc(aesKey, iv, inBuf, 0, padded, outBuf, 0, macs, 0);
                    FoldChunkMac(engine, fileMac, macs, 0, aesInput, aesOutput);
                }
            }
            return fileMac;
        }

        /// <summary>Read up to 8 chunks sequentially, compute their CBC in parallel, fold in order.</summary>
        private static int[] ComputeMacCbcParallel(string filePath, byte[] aesKey, byte[] iv, List<MacChunk> chunks)
        {
            const int GroupChunks = 8;
            const int MaxChunk = 0x100000;
            var fileMac = new int[] { 0, 0, 0, 0 };
            var engine = new AesEngine();
            engine.Init(true, new KeyParameter(aesKey));
            var inBuf = new byte[GroupChunks * MaxChunk + 15];
            var outBuf = new byte[GroupChunks * MaxChunk + 15];
            var offsets = new int[GroupChunks];
            var pads = new int[GroupChunks];
            var macs = new byte[GroupChunks * 16];
            var aesInput = new byte[16];
            var aesOutput = new byte[16];
            var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) };
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0x10000))
            {
                int index = 0;
                while (index < chunks.Count)
                {
                    int count = Math.Min(GroupChunks, chunks.Count - index);
                    int groupBytes = 0;
                    for (int j = 0; j < count; j++)
                    {
                        offsets[j] = groupBytes;
                        groupBytes += chunks[index + j].Size;
                    }
                    int total = ReadAtMost(fs, inBuf, groupBytes);
                    for (int j = 0; j < count; j++)
                    {
                        int size = chunks[index + j].Size;
                        int got = Math.Max(0, Math.Min(size, total - offsets[j]));
                        pads[j] = ((got + 15) / 16) * 16;
                        if (pads[j] > got)
                            Array.Clear(inBuf, offsets[j] + got, pads[j] - got);
                    }
                    if (count == 1)
                        ComputeChunkCbc(aesKey, iv, inBuf, offsets[0], pads[0], outBuf, offsets[0], macs, 0);
                    else
                        System.Threading.Tasks.Parallel.For(0, count, options, j =>
                            ComputeChunkCbc(aesKey, iv, inBuf, offsets[j], pads[j], outBuf, offsets[j], macs, j * 16));
                    for (int j = 0; j < count; j++)
                        FoldChunkMac(engine, fileMac, macs, j * 16, aesInput, aesOutput);
                    index += count;
                }
            }
            return fileMac;
        }

        /// <summary>Original per-block BouncyCastle implementation, kept as a portable fallback.</summary>
        private static bool VerifyMegaFileMacLegacy(string filePath, byte[] aesKey, int[] expectedMac, int[] nonceWords)
        {
            var engine = new AesEngine();
            engine.Init(true, new KeyParameter(aesKey));

            long fileLen = new FileInfo(filePath).Length;

            // An empty file has no chunks, so MEGA's MetaMAC for it is (0, 0).
            if (fileLen == 0L)
                return expectedMac[0] == 0 && expectedMac[1] == 0;

            int[] fileMac = new int[] { 0, 0, 0, 0 };

            // P-02: the old loop issued one FileStream.Read per 16 bytes and allocated
            // two int[4]/byte[16] pairs plus two List(Of T) per block (~4 million
            // allocations for 16 MiB). Measured 775ms vs 42ms for SHA256 of the same
            // file. Batch reads into one reusable 1 MiB buffer and keep the AES
            // input/output buffers alive for the whole scan: zero per-block allocations.
            // CBC-MAC chaining, chunk schedule (128 KiB * i, then 1 MiB), little-endian
            // word order, zero padding of the final partial block and the fold order are
            // all unchanged — verified byte-for-byte by the existing MetaMAC assertions.
            const int READ_BUFFER_SIZE = 0x100000;
            var readBuffer = new byte[1048576];
            var tailBuffer = new byte[16];
            var aesInput = new byte[16];
            var aesOutput = new byte[16];

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0x10000))
            {
                long chunkStart = 0L;
                int chunkNumber = 1; // 1-based chunk index

                while (chunkStart < fileLen)
                {
                    // ChunkedHash: 128 KiB * i for i = 1..8, then a fixed 1 MiB.
                    long chunkSize = chunkNumber <= 8 ? chunkNumber * 0x20000L : 0x100000L;
                    long thisChunk = Math.Min(chunkSize, fileLen - chunkStart);

                    // Initial IV = [n0, n1, n0, n1], exactly as the MEGA SDK does.
                    int m0 = nonceWords[0];
                    int m1 = nonceWords[1];
                    int m2 = nonceWords[0];
                    int m3 = nonceWords[1];

                    long chunkRemaining = thisChunk;
                    while (chunkRemaining > 0L)
                    {
                        int toRead = (int)Math.Min(READ_BUFFER_SIZE, chunkRemaining);
                        int totalRead = 0;
                        while (totalRead < toRead)
                        {
                            int n = fs.Read(readBuffer, totalRead, toRead - totalRead);
                            if (n == 0)
                                break;
                            totalRead += n;
                        }
                        if (totalRead == 0)
                            break;

                        int blocks = (totalRead + 15) / 16;
                        for (int b = 0, loopTo = blocks - 1; b <= loopTo; b++)
                        {
                            int off = b * 16;
                            int w0;
                            int w1;
                            int w2;
                            int w3;
                            if (off + 16 <= totalRead)
                            {
                                w0 = BitConverter.ToInt32(readBuffer, off);
                                w1 = BitConverter.ToInt32(readBuffer, off + 4);
                                w2 = BitConverter.ToInt32(readBuffer, off + 8);
                                w3 = BitConverter.ToInt32(readBuffer, off + 12);
                            }
                            else
                            {
                                // Final partial block: zero padded, as before.
                                Array.Clear(tailBuffer, 0, 16);
                                Array.Copy(readBuffer, off, tailBuffer, 0, totalRead - off);
                                w0 = BitConverter.ToInt32(tailBuffer, 0);
                                w1 = BitConverter.ToInt32(tailBuffer, 4);
                                w2 = BitConverter.ToInt32(tailBuffer, 8);
                                w3 = BitConverter.ToInt32(tailBuffer, 12);
                            }

                            m0 = m0 ^ w0;
                            m1 = m1 ^ w1;
                            m2 = m2 ^ w2;
                            m3 = m3 ^ w3;
                            WriteWordLE(aesInput, 0, m0);
                            WriteWordLE(aesInput, 4, m1);
                            WriteWordLE(aesInput, 8, m2);
                            WriteWordLE(aesInput, 12, m3);
                            engine.ProcessBlock(aesInput, 0, aesOutput, 0);
                            m0 = BitConverter.ToInt32(aesOutput, 0);
                            m1 = BitConverter.ToInt32(aesOutput, 4);
                            m2 = BitConverter.ToInt32(aesOutput, 8);
                            m3 = BitConverter.ToInt32(aesOutput, 12);
                        }

                        chunkRemaining -= toRead;
                        if (totalRead < toRead)
                            break;
                    }

                    // Fold the chunk MAC into the file MAC.
                    fileMac[0] = fileMac[0] ^ m0;
                    fileMac[1] = fileMac[1] ^ m1;
                    fileMac[2] = fileMac[2] ^ m2;
                    fileMac[3] = fileMac[3] ^ m3;
                    fileMac = AesEncryptBlock(engine, fileMac);

                    chunkStart += thisChunk;
                    chunkNumber += 1;
                }
            }

            // SDK macsmac(): m[0] ^= m[1]; m[1] = m[2] ^ m[3].
            return (fileMac[0] ^ fileMac[1]) == expectedMac[0] && (fileMac[2] ^ fileMac[3]) == expectedMac[1];
        }

        /// <summary>Little-endian, matching BitConverter.GetBytes on this platform,
    /// but without allocating a 4-byte array per AES block.</summary>
        private static void WriteWordLE(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8 & 0xFF);
            buffer[offset + 2] = (byte)(value >> 16 & 0xFF);
            buffer[offset + 3] = (byte)(value >> 24 & 0xFF);
        }

        private static int[] AesEncryptBlock(AesEngine engine, int[] words)
        {
            byte[] input = IntArrayToBytesArray(words);
            var output = new byte[16];
            engine.ProcessBlock(input, 0, output, 0);
            return ByteArrayToIntArray(output);
        }

        #endregion

    }
}