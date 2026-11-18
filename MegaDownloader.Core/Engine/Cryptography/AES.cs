using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MegaDownloader.Cryptography
{
    public class AES
    {

        private const int SALT_SIZE = 4;

        private const int SALT_ITERATIONS = 25997;  // Chosen because it is prime :P

        private static byte[] SALT_BYTES = new byte[] { 236, 148, 113, 216, 75, 18, 129, 6, 250, 85, 24, 243 };




        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        {
            byte[] encryptedBytes = null;



            using (var ms = new MemoryStream())
            {
                using (var AES = new RijndaelManaged())
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, SALT_BYTES, SALT_ITERATIONS);
                    AES.Key = key.GetBytes((int)Math.Round(AES.KeySize / 8d));
                    AES.IV = GetRandomBytes((int)Math.Round(AES.BlockSize / 8d));

                    AES.Mode = CipherMode.CBC;

                    using (var cs = new CryptoStream(ms, AES.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                        cs.Close();
                    }
                    encryptedBytes = AES.IV.Concat(ms.ToArray()).ToArray();
                }
            }

            return encryptedBytes;
        }

        public byte[] AES_Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;


            using (var ms = new MemoryStream())
            {
                using (var AES = new RijndaelManaged())
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, SALT_BYTES, SALT_ITERATIONS);
                    AES.Key = key.GetBytes((int)Math.Round(AES.KeySize / 8d));
                    AES.IV = bytesToBeDecrypted.Take((int)Math.Round(AES.BlockSize / 8d)).ToArray();

                    AES.Mode = CipherMode.CBC;

                    byte[] bytes = bytesToBeDecrypted.Skip((int)Math.Round(AES.BlockSize / 8d)).ToArray();

                    using (var cs = new CryptoStream(ms, AES.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytes, 0, bytes.Length);
                        cs.Close();
                    }
                    decryptedBytes = ms.ToArray();
                }
            }

            return decryptedBytes;
        }

        public string Encrypt(string text, string pwd)
        {
            byte[] originalBytes = Encoding.UTF8.GetBytes(text);
            byte[] encryptedBytes = null;
            byte[] passwordBytes = Encoding.UTF8.GetBytes(pwd);

            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);
            byte[] saltBytes = GetRandomBytes(SALT_SIZE);

            byte[] bytesToBeEncrypted = saltBytes.Concat(originalBytes).ToArray();

            encryptedBytes = AES_Encrypt(bytesToBeEncrypted, passwordBytes);

            return Convert.ToBase64String(encryptedBytes);
        }

        public string Decrypt(string decryptedText, string pwd)
        {
            byte[] bytesToBeDecrypted = Convert.FromBase64String(decryptedText);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(pwd);

            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

            byte[] decryptedBytes = AES_Decrypt(bytesToBeDecrypted, passwordBytes);

            return Encoding.UTF8.GetString(decryptedBytes.Skip(SALT_SIZE).ToArray());
        }

        public byte[] GetRandomBytes(int numberOfBytes)
        {

            byte[] ba = new byte[numberOfBytes];
            RandomNumberGenerator.Create().GetBytes(ba);
            return ba;
        }
    }
}