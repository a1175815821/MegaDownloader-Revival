using System;
using System.Globalization;
using System.Text;

namespace MegaDownloader
{

    public class Utils
    {

        internal static string FormatSpeedLimit(int kilobytesPerSecond)
        {
            return (kilobytesPerSecond / 1024.0d).ToString("0.####", CultureInfo.CurrentCulture);
        }

        internal static bool TryParseSpeedLimit(string value, ref int kilobytesPerSecond)
        {
            kilobytesPerSecond = 0;
            double megabytesPerSecond;
            // B5: the parameterless Double.TryParse implicitly allows group separators.
            // In de-DE/es-ES "1.5" becomes 15 (a silent 10x speed limit), while in fr-FR
            // the same input fails outright. Parse with an explicit style that never accepts
            // thousands separators, first in the UI culture (keeps the local decimal comma)
            // and then in the invariant culture for unambiguous typed dot decimals.
            var styles = NumberStyles.Float;
            var culture = CultureInfo.CurrentCulture;
            if (!double.TryParse(value, styles, culture, out megabytesPerSecond))
            {
                // Final fix: the invariant fallback must not reinterpret a locale group
                // separator as a decimal point. In de-DE/es-ES "1.500" used to mean
                // 1500 MB/s and would silently become 1.5 MB/s (1000x too small).
                // Reject the classic group shape (separator + exactly three digits) so
                // the user gets a clear error instead; a plain dot decimal ("1.5")
                // and a four-digit fraction ("1.5000") still round-trip correctly.
                string groupSeparator = culture.NumberFormat.NumberGroupSeparator;
                if (!string.IsNullOrEmpty(groupSeparator) && HasGroupedDigits(value, groupSeparator))
                    return false;
                if (!double.TryParse(value, styles, CultureInfo.InvariantCulture, out megabytesPerSecond))
                    return false;
            }
            if (double.IsNaN(megabytesPerSecond) || double.IsInfinity(megabytesPerSecond) || megabytesPerSecond <= 0d || megabytesPerSecond > int.MaxValue / 1024.0d)
                return false;
            double rounded = Math.Round(megabytesPerSecond * 1024.0d);
            if (rounded < 1d)
                return false;
            kilobytesPerSecond = (int)Math.Round(rounded);
            return true;
        }

        /// <summary>True when the text looks like a thousands-grouped number in the
    /// current culture (for example "1.500" in de-DE or "1,500" in en-US).</summary>
        private static bool HasGroupedDigits(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            int index = value.IndexOf(separator, StringComparison.Ordinal);
            while (index >= 0)
            {
                int start = index + separator.Length;
                int digits = 0;
                while (start + digits < value.Length && digits < 4 && char.IsDigit(value[start + digits]))
                    digits += 1;
                if (digits == 3 && (start + 3 == value.Length || !char.IsDigit(value[start + 3])))
                    return true;
                index = value.IndexOf(separator, start, StringComparison.Ordinal);
            }
            return false;
        }

        // Used only in in-memory save comparisons; randomized ciphertext stays on disk.
        internal static string SecretFingerprint(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }

        internal static string RemoveDiacritics(string stIn)
        {

            if (string.IsNullOrEmpty(stIn))
                return stIn;

            string stFormD = stIn.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            for (int ich = 0, loopTo = stFormD.Length - 1; ich <= loopTo; ich++)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(stFormD[ich]);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }


    }
}