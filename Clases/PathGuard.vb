Imports System.Collections.Generic
Imports System.IO

''' <summary>
''' Centralized validation for every path derived from remote or persisted data.
''' </summary>
Public NotInheritable Class PathGuard
    Private Const MaxSegmentLength As Integer = 240
    Private Const MaxWindowsSegmentLength As Integer = 255

    Private Shared ReadOnly ReservedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    }

    Private Sub New()
    End Sub

    Public Shared Function RequireSafePathSegment(ByVal value As String, Optional ByVal context As String = "path segment") As String
        Return RequireSafePathSegment(value, context, MaxSegmentLength)
    End Function

    Private Shared Function RequireSafePathSegment(value As String, context As String, maxSegmentLength As Integer) As String
        If String.IsNullOrWhiteSpace(value) Then
            Throw New ArgumentException("Invalid " & context & ": the name is empty.")
        End If
        If value.Length > maxSegmentLength Then
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
        Return GetSafePathUnderRoot(rootPath, relativePath, allowRoot, MaxSegmentLength)
    End Function

    Private Shared Function GetSafePathUnderRoot(rootPath As String, relativePath As String, allowRoot As Boolean, maxSegmentLength As Integer) As String
        If String.IsNullOrWhiteSpace(rootPath) Then Throw New ArgumentException("The destination root is empty.")
        Dim canonicalRoot As String = NormalizeFullPath(rootPath)
        If String.IsNullOrEmpty(relativePath) Then
            If allowRoot Then Return canonicalRoot
            Throw New ArgumentException("A child path is required.")
        End If
        If Path.IsPathRooted(relativePath) OrElse relativePath.IndexOf(":"c) >= 0 Then
            Throw New ArgumentException("Rooted and drive-qualified paths are not allowed.")
        End If

        ValidateRelativePath(relativePath, maxSegmentLength)
        Dim normalizedRelative As String = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
        Dim combined As String = Path.Combine(canonicalRoot, normalizedRelative)
        Dim candidate As String = Path.GetFullPath(combined)
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

    ''' <summary>Resolves a validated Windows archive name (up to 255 UTF-16 code units per segment).
    ''' The staging name can be longer than the final application filename.</summary>
    Friend Shared Function GetArchiveStagingPath(root As String, entryKey As String) As String
        Return GetSafePathUnderRoot(root, NormalizeArchivePath(entryKey), allowRoot:=False, maxSegmentLength:=MaxWindowsSegmentLength)
    End Function

    ''' <summary>Resolves an archive entry to its final destination, shortening long segments.</summary>
    Public Shared Function GetSafeArchiveEntryPath(ByVal extractionRoot As String, ByVal entryKey As String) As String
        Dim relative As String = NormalizeArchivePath(entryKey)
        ValidateRelativePath(relative, MaxWindowsSegmentLength)
        Return GetSafePathUnderRoot(extractionRoot, ShortenArchiveSegments(relative), allowRoot:=False)
    End Function

    ' Call only after canonicalizing and validating the archive path.
    Friend Shared Function ArchiveIoPath(fullPath As String) As String
        If fullPath.Length < 248 OrElse fullPath.StartsWith("\\?\", StringComparison.Ordinal) Then Return fullPath
        If fullPath.StartsWith("\\", StringComparison.Ordinal) Then Return "\\?\UNC\" & fullPath.Substring(2)
        Return "\\?\" & fullPath
    End Function

    Private Shared Function NormalizeArchivePath(entryKey As String) As String
        If String.IsNullOrWhiteSpace(entryKey) Then Throw New ArgumentException("Archive entry path is empty.")
        Return entryKey.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
    End Function

    Private Shared Function ShortenArchiveSegments(relative As String) As String
        Dim segments As String() = relative.Split(Path.DirectorySeparatorChar)
        For i As Integer = 0 To segments.Length - 1
            Dim name As String = segments(i)
            If name.Length <= MaxSegmentLength Then Continue For
            Dim extension As String = Path.GetExtension(name)
            If extension.Length > 32 Then extension = ""
            Dim suffix As String
            Using sha As System.Security.Cryptography.SHA256 = System.Security.Cryptography.SHA256.Create()
                Dim digest As Byte() = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(name))
                suffix = "~" & BitConverter.ToString(digest, 0, 6).Replace("-", "") & extension
            End Using
            Dim keep As Integer = MaxSegmentLength - suffix.Length
            If Char.IsHighSurrogate(name(keep - 1)) Then keep -= 1
            Dim prefix As String = name.Substring(0, keep).TrimEnd(" "c, "."c)
            segments(i) = If(prefix.Length = 0, "_", prefix) & suffix
        Next
        Return String.Join(Path.DirectorySeparatorChar.ToString(), segments)
    End Function

    Private Shared Sub ValidateRelativePath(ByVal relativePath As String, Optional maxSegmentLength As Integer = MaxSegmentLength)
        Dim normalized As String = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
        If String.IsNullOrEmpty(normalized) Then Return
        Dim segments() As String = normalized.Split(New Char() {Path.DirectorySeparatorChar}, StringSplitOptions.None)
        For Each segment As String In segments
            RequireSafePathSegment(segment, "path segment", maxSegmentLength)
        Next
    End Sub
End Class
