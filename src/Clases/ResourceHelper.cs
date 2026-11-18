
namespace MegaDownloader
{
    public class ResourceHelper
    {


        public static string GetResourceName(string EndsWith)
        {
            if (string.IsNullOrEmpty(EndsWith))
                return "";
            foreach (string resname in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (resname.ToUpper().EndsWith(EndsWith.ToUpper()))
                {
                    return resname;
                }
            }
            return "";
        }

    }
}