using System.Text;

// System.Web is not part of .NET Core/5+. The original code only needs UrlEncode/UrlDecode;
// these shims keep the migrated source compiling without touching its behaviour.
namespace System.Web
{
    // Internal so the shim never collides with the real System.Web.HttpUtility in
    // consumers that still reference it (the WinForms app).
    internal static class HttpUtility
    {
        public static string UrlEncode(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return Uri.EscapeDataString(value);
        }

        public static string UrlDecode(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return Uri.UnescapeDataString(value.Replace("+", " "));
        }
    }
}
