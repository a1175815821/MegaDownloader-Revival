Imports System
Imports System.IO

''' <summary>
''' PathGuard 回归用例：Zip Slip 拦截是解压升级（SharpCompress 单独立项）的前置安全网。
''' 注意：RequireSafePathSegment 对 ".." 段抛 ArgumentException（相对标记），
''' EnsurePathUnderRoot 的 UnauthorizedAccessException 只在直接调用且越界时出现。
''' 路径用例均为 Windows 语义（net48 只跑 Windows）。
''' </summary>
Public Module PathGuardTests

    Public Sub Run()
        ' SanitizeFileName：永不抛，坏名回 fallback
        Runner.Check(Global.MegaDownloader.PathGuard.SanitizeFileName(Nothing) = "file", "Sanitize_Nothing_Fallback")
        Runner.Check(Global.MegaDownloader.PathGuard.SanitizeFileName("   ") = "file", "Sanitize_Whitespace_Fallback")
        Runner.Check(Global.MegaDownloader.PathGuard.SanitizeFileName("..") = "file", "Sanitize_DotDot_Fallback")
        Runner.Check(Global.MegaDownloader.PathGuard.SanitizeFileName("CON") = "_CON", "Sanitize_Reserved_Prefixed")
        Runner.Check(Global.MegaDownloader.PathGuard.SanitizeFileName("a/b\c:d") = "a_b_c_d", "Sanitize_Separators_Replaced")
        Runner.Check(Global.MegaDownloader.PathGuard.SanitizeFileName("informe final (1).pdf") = "informe final (1).pdf", "Sanitize_Clean_Passthrough")

        Dim root As String = IO.Path.Combine(IO.Path.GetTempPath(), "MDTests_Root")

        ' Zip Slip：含 ".." 段在 ValidateRelativePath 即被 ArgumentException 拦下
        Runner.CheckThrows(Of ArgumentException)(Sub() Global.MegaDownloader.PathGuard.GetSafeArchiveEntryPath(root, "../../evil.txt"), "ZipSlip_ParentEscape_Blocked")
        Runner.CheckThrows(Of ArgumentException)(Sub() Global.MegaDownloader.PathGuard.GetSafeArchiveEntryPath(root, "sub/../../evil.txt"), "ZipSlip_NestedEscape_Blocked")
        Runner.CheckThrows(Of ArgumentException)(Sub() Global.MegaDownloader.PathGuard.GetSafePathUnderRoot(root, "C:\Windows", True), "AbsolutePath_Rejected")

        ' 正常条目落在 root 下
        Dim sep As String = IO.Path.DirectorySeparatorChar.ToString()
        Dim canonicalRoot As String = IO.Path.GetFullPath(root).TrimEnd(IO.Path.DirectorySeparatorChar, IO.Path.AltDirectorySeparatorChar)
        Dim expected As String = canonicalRoot & sep & "sub" & sep & "file.txt"
        Dim actual As String = Global.MegaDownloader.PathGuard.GetSafeArchiveEntryPath(root, "sub/file.txt")
        Runner.Check(String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase), "ArchiveEntry_Normal_ResolvedUnderRoot")

        ' EnsurePathUnderRoot 根目录本身：allowRoot 决定抛与不抛
        Runner.CheckThrows(Of UnauthorizedAccessException)(Sub() Global.MegaDownloader.PathGuard.EnsurePathUnderRoot(root, root, False), "EnsureRoot_DisallowRoot_Throws")
        Try
            Global.MegaDownloader.PathGuard.EnsurePathUnderRoot(root, root, True)
            Runner.Check(True, "EnsureRoot_AllowRoot_NoThrow")
        Catch ex As Exception
            Runner.Fail("EnsureRoot_AllowRoot_NoThrow [threw " & ex.GetType().Name & "]")
        End Try
    End Sub

End Module
