using System;
using System.Collections.Generic;

namespace MegaDownloader
{
    public class PreSharedKeyManager
    {

        public static List<string> GetFileKeyFromPreSharedKeys(ref Configuracion Config)
        {


            var list2 = new List<string>();
            foreach (System.Security.SecureString key in Config.ListaPreSharedKeys)
            {
                string derived = Criptografia.GetFileKeyFromPreSharedKey(Criptografia.ToInsecureString(key));
                if (derived is not null)
                    list2.Add(derived);
            }

            return list2;
        }

        public static string DecryptFileInfo(string EncryptedFileInfo, string FileKey)
        {
            string FileInfoDec = null;

            if (FileKey.Contains("=###n="))
            {
                FileKey = FileKey.Substring(0, FileKey.IndexOf("=###n="));
            }

            try
            {
                FileInfoDec = "" + Criptografia.AES_MEGA_DecryptString(EncryptedFileInfo, FileKey);
            }
            catch (Exception exc)
            {
                return null;
            }

            if (!FileInfoDec.ToUpperInvariant().StartsWith("MEGA"))
            {
                return null;
            }
            else
            {
                return FileInfoDec;
            }
        }

    }
}