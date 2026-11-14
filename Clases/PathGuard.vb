Imports System.Collections.Generic
Imports System.IO

''' <summary>
''' Centralized validation for every path derived from remote or persisted data.
''' </summary>
Public NotInheritable Class PathGuard
    Private Const MaxSegmentLength As Integer = 240

    Private Shared ReadOnly ReservedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    }

    Private Sub New()
    End Sub

    Public Shared Function RequireSafePathSegment(ByVal value As String, Optional ByVal context As String = "path segment") As String
        If String.IsNullOrWhiteSpace(value) Then
            Throw New ArgumentException("Invalid " & context & ": the name is empty.")
        End If
        If value.Length > MaxSegmentLength Then
            Throw New ArgumentException("Invalid " & context & ": the name is too long.")
        End If
        If Path.IsPathRooted(value) OrElse value.IndexOf(Path.DirectorySeparatorChar) >= 0 OrElse value.IndexOf(Path.AltDirectorySeparatorChar) >= 0 Then
            Throw New ArgumentException("Invalid " & context & ": path separators are not allowed.")
        End If
        If value = "." OrElse value = ".." OrElse value.TrimEnd(" "c, "."c) <> value Then
            Throw New ArgumentException("Invalid " & context & ": relative markers, trailing dots, and trailing spaces are not allowed.")
        End If
        If value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then
            Throw New ArgumentException("Invalid " & context & ": the name contains invalid characters.")
        End If

        Dim baseName As String = value
        Dim dotIndex As Integer = baseName.IndexOf("."c)
        If dotIndex >= 0 Then baseName = baseName.Substring(0, dotIndex)
        If ReservedNames.Contains(baseName) Then
            Throw New ArgumentException("Invalid " & context & ": the name is reserved by Windows.")
        End If
        Return value
    End Function

    ''' <summary>
    ''' Turns a remote/display name into a single safe path segment (never throws for empty; uses fallback).
    ''' </summary>
    Public Shared Function SanitizeFileName(ByVal value As String, Optional ByVal fallback As String = "file") As String
        If String.IsNullOrWhiteSpace(value) Then Return fallback

        Dim cleaned As String = value.Trim()
        For Each c As Char In Path.GetInvalidFileNameChars()
            cleaned = cleaned.Replace(c, "_"c)
        Next
        cleaned = cleaned.Replace(Path.DirectorySeparatorChar, "_"c).Replace(Path.AltDirectorySeparatorChar, "_"c)
        cleaned = cleaned.TrimEnd(" "c, "."c)
        If cleaned.Length > MaxSegmentLength Then
            cleaned = cleaned.Substring(0, MaxSegmentLength).TrimEnd(" "c, "."c)
        End If
        If String.IsNullOrWhiteSpace(cleaned) OrElse cleaned = "." OrElse cleaned = ".." Then
            cleaned = fallback
        End If

        Dim baseName As String = cleaned
        Dim dotIndex As Integer = baseName.IndexOf("."c)
        If dotIndex >= 0 Then baseName = baseName.Substring(0, dotIndex)
        If ReservedNames.Contains(baseName) Then
            cleaned = "_" & cleaned
        End If

        Return cleaned
    End Function

    Public Shared Function CombineSafeRelativePath(ByVal baseRelativePath As String, ByVal childSegment As String) As String
        RequireSafePathSegment(childSegment, "remote folder name")
        If String.IsNullOrEmpty(baseRelativePath) Then Return childSegment
        ValidateRelativePath(baseRelativePath)
        Return Path.Combine(baseRelativePath, childSegment)
    End Function

    Public Shared Function GetSafePathUnderRoot(ByVal rootPath As String, ByVal relativePath As String, Optional ByVal allowRoot As Boolean = True) As String
        If String.IsNullOrWhiteSpace(rootPath) Then Throw New ArgumentException("The destination root is empty.")
        Dim canonicalRoot As String = NormalizeFullPath(rootPath)
        If String.IsNullOrEmpty(relativePath) Then
            If allowRoot Then Return canonicalRoot
            Throw New ArgumentException("A child path is required.")
        End If
        If Path.IsPathRooted(relativePath) OrElse relativePath.IndexOf(":"c) >= 0 Then
            Throw New ArgumentException("Rooted and drive-qualified paths are not allowed.")
        End If

        ValidateRelativePath(relativePath)
        Dim normalizedRelative As String = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
        Dim candidate As String = Path.GetFullPath(Path.Combine(canonicalRoot, normalizedRelative))
        EnsurePathUnderRoot(canonicalRoot, candidate, allowRoot)
        Return candidate
    End Function

    Public Shared Function GetSafeFilePathUnderRoot(ByVal rootPath As String, ByVal fileName As String) As String
        Dim safeName As String = SanitizeFileName(fileName)
        Return GetSafePathUnderRoot(rootPath, safeName, allowRoot:=False)
    End Function

    Public Shared Sub EnsurePathUnderRoot(ByVal rootPath As String, ByVal candidatePath As String, Optional ByVal allowRoot As Boolean = False)
        Dim canonicalRoot As String = NormalizeFullPath(rootPath)
        Dim canonicalCandidate As String = NormalizeFullPath(candidatePath)
        Dim prefix As String = canonicalRoot
        If Not prefix.EndsWith(Path.DirectorySeparatorChar) Then prefix &= Path.DirectorySeparatorChar

        If String.Equals(canonicalRoot, canonicalCandidate, StringComparison.OrdinalIgnoreCase) Then
            If allowRoot Then Return
            Throw New UnauthorizedAccessException("The operation cannot target the destination root itself.")
        End If
        If Not canonicalCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Throw New UnauthorizedAccessException("The resolved path is outside the allowed destination root.")
        End If
    End Sub

    Private Shared Function NormalizeFullPath(ByVal value As String) As String
        Dim full As String = Path.GetFullPath(value)
        Dim root As String = Path.GetPathRoot(full)
        Dim trimmed As String = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        ' Keep the separator on volume roots: "C:" means the drive's current directory.
        If String.Equals(trimmed, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) Then
            Return root
        End If
        Return trimmed
    End Function

    ''' <summary>
    ''' Resolves an archive entry key under an extraction root. Rejects Zip Slip / absolute paths.
    ''' </summary>
    Public Shared Function GetSafeArchiveEntryPath(ByVal extractionRoot As String, ByVal entryKey As String) As String
        If String.IsNullOrWhiteSpace(entryKey) Then
            Throw New ArgumentException("Archive entry path is empty.")
        End If
        Dim relative As String = entryKey.Replace("/"c, Path.DirectorySeparatorChar).Replace("\"c, Path.DirectorySeparatorChar)
        While relative.StartsWith(Path.DirectorySeparatorChar)
            relative = relative.Substring(1)
        End While
        If String.IsNullOrWhiteSpace(relative) Then
            Throw New ArgumentException("Archive entry path is empty.")
        End If
        relative = ShortenArchiveSegments(relative)
        Return GetSafePathUnderRoot(extractionRoot, relative, allowRoot:=False)
    End Function

    ''' <summary>归档条目名来自第三方压缩包,可能合法地超过本类的 240 字符上限
    ''' (Windows 单段上限 255,实测 7zr 能正常创建/解压 254 字符名)。此前直接抛异常
    ''' 会让整个压缩包被拒。超过上限时保留可读前缀并追加由完整原名派生的 8 位哈希;
    ''' 两个仅末尾不同的长名不会映射成同一路径。穿越/保留名/非法字符校验一律不放宽。</summary>
    Private Shared Function ShortenArchiveSegments(ByVal relative As String) As String
        Dim segments() As String = relative.Split(Path.DirectorySeparatorChar)
        Dim changed As Boolean = False
        For i As Integer = 0 To segments.Length - 1
            Dim s As String = segments(i)
            If s.Length <= MaxSegmentLength Then Continue For
            Dim suffix As String
            Using sha As New System.Security.Cryptography.SHA256Managed()
                Dim digest As Byte() = sha.ComputeHash(New System.Text.UTF8Encoding(False).GetBytes(s))
                suffix = "~" & BitConverter.ToString(digest, 0, 6).Replace("-", "") & Path.GetExtension(s)
            End Using
            Dim keep As Integer = Math.Max(1, MaxSegmentLength - suffix.Length)
            Dim cut As String = s.Substring(0, Math.Min(keep, s.Length)).TrimEnd(" "c, "."c)
            If String.IsNullOrEmpty(cut) OrElse cut = "." OrElse cut = ".." Then cut = "_"
            segments(i) = cut & suffix
            changed = True
        Next
        If Not changed Then Return relative
        Return String.Join(Path.DirectorySeparatorChar.ToString(), segments)
    End Function

    ''' <summary>
    ''' Validates every non-directory archive entry; throws if any entry escapes the root.
    ''' </summary>
    Public Shared Function GetRelativePath(ByVal root As String, ByVal fullPath As String) As String
        Dim fullRoot As String = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim full As String = Path.GetFullPath(fullPath)
        If Not full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) Then Throw New UnauthorizedAccessException("Path escapes the extraction root.")
        Return full.Substring(fullRoot.Length)
    End Function

    Public Shared Sub ValidateArchiveEntries(ByVal extractionRoot As String, ByVal entryKeys As IEnumerable(Of String))
        If entryKeys Is Nothing Then Return
        For Each key As String In entryKeys
            If String.IsNullOrWhiteSpace(key) Then Continue For
            GetSafeArchiveEntryPath(extractionRoot, key)
        Next
    End Sub

    Private Shared Sub ValidateRelativePath(ByVal relativePath As String)
        Dim normalized As String = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
        If String.IsNullOrEmpty(normalized) Then Return
        Dim segments() As String = normalized.Split(New Char() {Path.DirectorySeparatorChar}, StringSplitOptions.None)
        For Each segment As String In segments
            RequireSafePathSegment(segment)
        Next
    End Sub
End Class
