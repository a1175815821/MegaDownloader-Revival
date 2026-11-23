using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MegaDownloader
{
    /// <summary>
    /// Link containers the shell accepts from drag-and-drop or the command line. An
    /// <c>.elc</c> file holds one server-encoded link; <c>.dlc</c> lost its decoder when
    /// dcrypt.it shut down, so it is reported instead of silently ignored.
    /// </summary>
    public static class LinkContainer
    {
        public static bool IsContainerPath(string? path) =>
            path is not null &&
            (path.EndsWith(".elc", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".dlc", StringComparison.OrdinalIgnoreCase));

        /// <summary>Reads one container and returns the MEGA link it stands for.</summary>
        public static async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The file does not exist.", path);

            if (path.EndsWith(".dlc", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    "DLC files are no longer supported because the dcrypt.it service shut down. Use an .elc file or paste the links.");

            string content = (await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false)).Trim();
            if (content.Length == 0)
                throw new InvalidDataException("The .elc file is empty.");

            return "mega://" + URLExtractor.SERVERENCODEDPREFIX + content;
        }
    }
}
