''' <summary>
''' DescompresorController 回归用例（第三批第 8 项的前置安全网）。
''' 覆盖：ZIP 中文名、Zip Slip 拦截、7z 单卷/分卷/带密码/错密码。
''' RAR（单卷/分卷/加密/固实）无本地生成手段， fixtures 缺失——合入前必须用真实样本手工覆盖。
''' 注意：中文断言只做文件计数（源码无 BOM 时旧版 vbc 会按系统代码页读入，字面量不可靠）。
''' </summary>
Imports System
Imports System.IO

Public Module DescompresorTests

    Private Function FixtureDir() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures")
    End Function

    Private Function NewOutDir(ByVal name As String) As String
        Dim d As String = Path.Combine(Path.GetTempPath(), "MDDecompTest_" & name)
        Try
            If Directory.Exists(d) Then Directory.Delete(d, True)
        Catch
        End Try
        Directory.CreateDirectory(d)
        Return d
    End Function

    Private Function Extract(ByVal fixture As String, ByVal outDir As String, ByVal password As String) As String
        Return Global.MegaDownloader.DescompresorController.ExtractArchiveSync(Path.Combine(FixtureDir(), fixture), outDir, password)
    End Function

    Private Function CountFiles(ByVal dir As String) As Integer
        If Not Directory.Exists(dir) Then Return -1
        Return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length
    End Function

    Public Sub Run()
        ' --- ZIP 中文名：3 个条目全部落盘，ASCII 文件内容一致 ---
        Dim out1 As String = NewOutDir("ZipCn")
        Runner.Check(Extract("cn_names.zip", out1, Nothing) = "", "Decomp_ZipCn_Success")
        Runner.Check(CountFiles(out1) = 3, "Decomp_ZipCn_ThreeFiles")
        Runner.Check(File.ReadAllText(Path.Combine(out1, "readme.txt")).Trim() = "plain file", "Decomp_ZipCn_AsciiContent")

        ' --- Zip Slip：必须失败，且越狱目标不得出现 ---
        Dim out2 As String = NewOutDir("ZipSlip")
        Dim slipErr As String = Extract("zipslip.zip", out2, Nothing)
        Runner.Check(Not String.IsNullOrEmpty(slipErr), "Decomp_ZipSlip_Blocked")
        Dim escapeTarget As String = Path.GetFullPath(Path.Combine(out2, "..", "evil.txt"))
        Runner.Check(Not File.Exists(escapeTarget), "Decomp_ZipSlip_NoEscape")

        ' --- 7z 单卷（内嵌 7zr，无需系统 7-Zip）---
        Dim out3 As String = NewOutDir("SevenSingle")
        Runner.Check(Extract("plain.7z", out3, Nothing) = "", "Decomp_7zSingle_Success")
        Runner.Check(New FileInfo(Path.Combine(out3, "blob.bin")).Length = 204800L, "Decomp_7zSingle_Size")

        ' --- 7z 分卷 .7z.001 ---
        Dim out4 As String = NewOutDir("SevenSplit")
        Runner.Check(Extract("split.7z.001", out4, Nothing) = "", "Decomp_7zSplit_Success")
        Runner.Check(New FileInfo(Path.Combine(out4, "blob.bin")).Length = 204800L, "Decomp_7zSplit_Size")

        ' --- 7z 带密码：对/错 ---
        Dim out5 As String = NewOutDir("SevenPass")
        Runner.Check(Extract("secret.7z", out5, "Test7z_Pass1") = "", "Decomp_7zPassword_Success")
        Runner.Check(New FileInfo(Path.Combine(out5, "blob.bin")).Length = 204800L, "Decomp_7zPassword_Size")
        Dim out6 As String = NewOutDir("SevenWrongPass")
        Runner.Check(Not String.IsNullOrEmpty(Extract("secret.7z", out6, "WrongPassword")), "Decomp_7zWrongPassword_Fails")

        ' --- 不存在的文件：返回错误而非抛异常 ---
        Dim out7 As String = NewOutDir("Missing")
        Runner.Check(Not String.IsNullOrEmpty(Extract("no_such_file.zip", out7, Nothing)), "Decomp_MissingFile_Error")
    End Sub

End Module
