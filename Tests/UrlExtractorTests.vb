''' <summary>
''' URLExtractor 回归用例。注意 EsUrlAcortador 恒返 False（goo.gl 已死），
''' ExtraerURLs 的短链分支是死代码——以下用例全离线，无网络依赖。
''' </summary>
Public Module UrlExtractorTests

    Private Const OldLink As String = "https://mega.co.nz/#!abcdef!ghijklmnopqr"
    Private Const NewLink As String = "https://mega.nz/file/abcDEFgh#AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    Private Const NewKey As String = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"

    Public Sub Run()
        ' 空输入 / 垃圾输入
        Runner.Check(Global.MegaDownloader.URLExtractor.ExtraerURLs(Nothing).Count = 0, "ExtraerURLs_Nothing_IsEmpty")
        Runner.Check(Global.MegaDownloader.URLExtractor.ExtraerURLs("   ").Count = 0, "ExtraerURLs_Whitespace_IsEmpty")
        Runner.Check(Global.MegaDownloader.URLExtractor.ExtraerURLs("hello world, no links here").Count = 0, "ExtraerURLs_Junk_IsEmpty")

        ' 旧格式（注释中的 canonical 示例）
        Dim oldFound As List(Of String) = Global.MegaDownloader.URLExtractor.ExtraerURLs("descarga esto " & OldLink & " por favor")
        Runner.Check(oldFound.Count = 1 AndAlso oldFound(0) = OldLink, "ExtraerURLs_OldFormat_Found")
        Runner.Check(Global.MegaDownloader.URLExtractor.ExtraerFileID(OldLink) = "abcdef", "ExtraerFileID_OldFormat")

        ' 新格式（v1.9+ /file/，43 字真实长度 key；文档占位 key 仅 28 字会被判短，需用足长）
        Dim newFound As List(Of String) = Global.MegaDownloader.URLExtractor.ExtraerURLs("link: " & NewLink)
        Runner.Check(newFound.Count = 1 AndAlso newFound(0) = NewLink, "ExtraerURLs_NewFormat_Found")
        Runner.Check(Global.MegaDownloader.URLExtractor.ExtraerFileID(NewLink) = "abcDEFgh", "ExtraerFileID_NewFormat")
        Runner.Check(Global.MegaDownloader.URLExtractor.ExtraerFileKey(NewLink) = NewKey, "ExtraerFileKey_NewFormat")

        ' 去重
        Dim dupes As List(Of String) = Global.MegaDownloader.URLExtractor.ExtraerURLs(OldLink & " " & OldLink)
        Runner.Check(dupes.Count = 1, "ExtraerURLs_Dedupes")

        ' 混合文本双链
        Dim both As List(Of String) = Global.MegaDownloader.URLExtractor.ExtraerURLs(OldLink & " y " & NewLink)
        Runner.Check(both.Count = 2, "ExtraerURLs_Mixed_BothFound")
    End Sub

End Module
