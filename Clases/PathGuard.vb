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
        Dim canonicalRoot As String = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
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
        Dim canonicalRoot As String = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Dim canonicalCandidate As String = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Dim prefix As String = canonicalRoot & Path.DirectorySeparatorChar

        If String.Equals(canonicalRoot, canonicalCandidate, StringComparison.OrdinalIgnoreCase) Then
            If allowRoot Then Return
            Throw New UnauthorizedAccessException("The operation cannot target the destination root itself.")
        End If
        If Not canonicalCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Throw New UnauthorizedAccessException("The resolved path is outside the allowed destination root.")
        End If
    End Sub

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
        Return GetSafePathUnderRoot(extractionRoot, relative, allowRoot:=False)
    End Function

    ''' <summary>
    ''' Validates every non-directory archive entry; throws if any entry escapes the root.
    ''' </summary>
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
