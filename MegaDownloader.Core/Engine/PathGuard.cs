using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    /// <summary>
/// Centralized validation for every path derived from remote or persisted data.
/// </summary>
    public sealed class PathGuard
    {
        private const int MaxSegmentLength = 240;
        private const int MaxWindowsSegmentLength = 255;

        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        private PathGuard()
        {
        }

        public static string RequireSafePathSegment(string value, string context = "path segment")
        {
            return RequireSafePathSegment(value, context, MaxSegmentLength);
        }

        private static string RequireSafePathSegment(string value, string context, int maxSegmentLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Invalid " + context + ": the name is empty.");
            }
            if (value.Length > maxSegmentLength)
            {
                throw new ArgumentException("Invalid " + context + ": the name is too long.");
            }
            if (Path.IsPathRooted(value) || value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException("Invalid " + context + ": path separators are not allowed.");
            }
            if (value == "." || value == ".." || (value.TrimEnd(' ', '.') ?? "") != (value ?? ""))
            {
                throw new ArgumentException("Invalid " + context + ": relative markers, trailing dots, and trailing spaces are not allowed.");
            }
            if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid " + context + ": the name contains invalid characters.");
            }

            string baseName = value;
            int dotIndex = baseName.IndexOf('.');
            if (dotIndex >= 0)
                baseName = baseName.Substring(0, dotIndex);
            if (ReservedNames.Contains(baseName))
            {
                throw new ArgumentException("Invalid " + context + ": the name is reserved by Windows.");
            }
            return value;
        }

        /// <summary>
    /// Turns a remote/display name into a single safe path segment (never throws for empty; uses fallback).
    /// </summary>
        public static string SanitizeFileName(string value, string fallback = "file")
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string cleaned = value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                cleaned = cleaned.Replace(c, '_');
            cleaned = cleaned.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
            cleaned = cleaned.TrimEnd(' ', '.');
            if (cleaned.Length > MaxSegmentLength)
            {
                cleaned = cleaned.Substring(0, MaxSegmentLength).TrimEnd(' ', '.');
            }
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned == "." || cleaned == "..")
            {
                cleaned = fallback;
            }

            string baseName = cleaned;
            int dotIndex = baseName.IndexOf('.');
            if (dotIndex >= 0)
                baseName = baseName.Substring(0, dotIndex);
            if (ReservedNames.Contains(baseName))
            {
                cleaned = "_" + cleaned;
            }

            return cleaned;
        }

        public static string CombineSafeRelativePath(string baseRelativePath, string childSegment)
        {
            RequireSafePathSegment(childSegment, "remote folder name");
            if (string.IsNullOrEmpty(baseRelativePath))
                return childSegment;
            ValidateRelativePath(baseRelativePath);
            return Path.Combine(baseRelativePath, childSegment);
        }

        public static string GetSafePathUnderRoot(string rootPath, string relativePath, bool allowRoot = true)
        {
            return GetSafePathUnderRoot(rootPath, relativePath, allowRoot, MaxSegmentLength);
        }

        private static string GetSafePathUnderRoot(string rootPath, string relativePath, bool allowRoot, int maxSegmentLength)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("The destination root is empty.");
            string canonicalRoot = NormalizeFullPath(rootPath);
            if (string.IsNullOrEmpty(relativePath))
            {
                if (allowRoot)
                    return canonicalRoot;
                throw new ArgumentException("A child path is required.");
            }
            if (Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
            {
                throw new ArgumentException("Rooted and drive-qualified paths are not allowed.");
            }

            ValidateRelativePath(relativePath, maxSegmentLength);
            string normalizedRelative = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            string combined = Path.Combine(canonicalRoot, normalizedRelative);
            string candidate = Path.GetFullPath(combined);
            EnsurePathUnderRoot(canonicalRoot, candidate, allowRoot);
            return candidate;
        }

        public static string GetSafeFilePathUnderRoot(string rootPath, string fileName)
        {
            string safeName = SanitizeFileName(fileName);
            return GetSafePathUnderRoot(rootPath, safeName, allowRoot: false);
        }

        public static void EnsurePathUnderRoot(string rootPath, string candidatePath, bool allowRoot = false)
        {
            string canonicalRoot = NormalizeFullPath(rootPath);
            string canonicalCandidate = NormalizeFullPath(candidatePath);
            string prefix = canonicalRoot;
            if (!prefix.EndsWith(Conversions.ToString(Path.DirectorySeparatorChar)))
                prefix += Conversions.ToString(Path.DirectorySeparatorChar);

            if (string.Equals(canonicalRoot, canonicalCandidate, StringComparison.OrdinalIgnoreCase))
            {
                if (allowRoot)
                    return;
                throw new UnauthorizedAccessException("The operation cannot target the destination root itself.");
            }
            if (!canonicalCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("The resolved path is outside the allowed destination root.");
            }
        }

        private static string NormalizeFullPath(string value)
        {
            string full = Path.GetFullPath(value);
            string root = Path.GetPathRoot(full);
            string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Keep the separator on volume roots: "C:" means the drive's current directory.
            if (string.Equals(trimmed, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }
            return trimmed;
        }

        /// <summary>Resolves a validated Windows archive name (up to 255 UTF-16 code units per segment).
    /// The staging name can be longer than the final application filename.</summary>
        internal static string GetArchiveStagingPath(string root, string entryKey)
        {
            return GetSafePathUnderRoot(root, NormalizeArchivePath(entryKey), allowRoot: false, maxSegmentLength: MaxWindowsSegmentLength);
        }

        /// <summary>Resolves an archive entry to its final destination, shortening long segments.</summary>
        public static string GetSafeArchiveEntryPath(string extractionRoot, string entryKey)
        {
            string relative = NormalizeArchivePath(entryKey);
            ValidateRelativePath(relative, MaxWindowsSegmentLength);
            return GetSafePathUnderRoot(extractionRoot, ShortenArchiveSegments(relative), allowRoot: false);
        }

        // Call only after canonicalizing and validating the archive path.
        internal static string ArchiveIoPath(string fullPath)
        {
            // \\?\ is a Windows device path; on macOS/Linux it would be a literal
            // directory name and every archive write would land in the wrong place.
            if (!OperatingSystem.IsWindows() || fullPath.Length < 248 || fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
                return fullPath;
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
                return @"\\?\UNC\" + fullPath.Substring(2);
            return @"\\?\" + fullPath;
        }

        private static string NormalizeArchivePath(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
                throw new ArgumentException("Archive entry path is empty.");
            return entryKey.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        }

        private static string ShortenArchiveSegments(string relative)
        {
            string[] segments = relative.Split(Path.DirectorySeparatorChar);
            for (int i = 0, loopTo = segments.Length - 1; i <= loopTo; i++)
            {
                string name = segments[i];
                if (name.Length <= MaxSegmentLength)
                    continue;
                string @extension = Path.GetExtension(name);
                if (@extension.Length > 32)
                    @extension = "";
                string suffix;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(name));
                    suffix = "~" + BitConverter.ToString(digest, 0, 6).Replace("-", "") + @extension;
                }
                int keep = MaxSegmentLength - suffix.Length;
                if (char.IsHighSurrogate(name[keep - 1]))
                    keep -= 1;
                string prefix = name.Substring(0, keep).TrimEnd(' ', '.');
                segments[i] = (prefix.Length == 0 ? "_" : prefix) + suffix;
            }
            return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
        }

        private static void ValidateRelativePath(string relativePath, int maxSegmentLength = MaxSegmentLength)
        {
            string normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(normalized))
                return;
            string[] segments = normalized.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.None);
            foreach (string segment in segments)
                RequireSafePathSegment(segment, "path segment", maxSegmentLength);
        }
    }
}