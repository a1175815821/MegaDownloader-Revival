
namespace MegaDownloader
{
    public class MD5Utils
    {
        // specify the path to a file and this routine will calculate your hash
        public static string MD5CalcFile(string filepath)
        {

            // open file (as read-only)
            // Using reader As New System.IO.FileStream(filepath, IO.FileMode.Open, IO.FileAccess.Read)
            using (var reader = new System.IO.BufferedStream(System.IO.File.OpenRead(filepath), 1200000)) // http://stackoverflow.com/questions/1177607/what-is-the-fastest-way-to-create-a-checksum-for-large-files-in-c-sharp/1177744#1177744
            {
                using (var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                {

                    // hash contents of this stream
                    byte[] hash = md5.ComputeHash(reader);

                    // return formatted hash
                    return ByteArrayToString(hash);

                }
            }

        }

        // utility function to convert a byte array into a hex string
        private static string ByteArrayToString(byte[] arrInput)
        {

            var sb = new System.Text.StringBuilder(arrInput.Length * 2);

            for (int i = 0, loopTo = arrInput.Length - 1; i <= loopTo; i++)
                sb.Append(arrInput[i].ToString("X2"));

            return sb.ToString().ToLower();

        }
    }
}