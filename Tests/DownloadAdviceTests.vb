''' <summary>
''' DownloadAdvice 回归用例：失败分类与人话文案。纯函数，无网络无 UI。
''' 注意：文案断言只校验"非空且含关键动作词"，不锁死整句（翻译可独立演进）。
''' </summary>
Public Module DownloadAdviceTests

    Public Sub Run()
        ' 配额：关键字命中 / 标记命中
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Classify("MegaQuotaExceededException: EOVERQUOTA", False, False) = Global.MegaDownloader.DownloadAdvice.FailureKind.Quota, "Advice_QuotaKeyword")
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Classify("anything", True, False) = Global.MegaDownloader.DownloadAdvice.FailureKind.Quota, "Advice_QuotaFlag")
        Dim quotaTxt As String = Global.MegaDownloader.DownloadAdvice.Describe("EOVERQUOTA transfer quota exceeded", True, False)
        Runner.Check(Not String.IsNullOrEmpty(quotaTxt), "Advice_QuotaText_NonEmpty")

        ' 永久失败：标记 / 错误码
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Classify("MEGA error -9 ENOENT", False, False) = Global.MegaDownloader.DownloadAdvice.FailureKind.Permanent, "Advice_PermanentCode")
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Classify("whatever", False, True) = Global.MegaDownloader.DownloadAdvice.FailureKind.Permanent, "Advice_PermanentFlag")

        ' 连接错误：chunk 重试耗尽文案
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Classify("Download stopped because there were too many connection errors (11).", False, False) = Global.MegaDownloader.DownloadAdvice.FailureKind.Connection, "Advice_ConnectionKeyword")

        ' 未知：回退原文首行；空输入回退通用提示（永不空）
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Classify("weird new failure", False, False) = Global.MegaDownloader.DownloadAdvice.FailureKind.Unknown, "Advice_UnknownKind")
        Runner.Check(Global.MegaDownloader.DownloadAdvice.Describe("weird new failure" & vbLf & "second line", False, False) = "weird new failure", "Advice_UnknownFallsBackToFirstLine")
        Runner.Check(Not String.IsNullOrEmpty(Global.MegaDownloader.DownloadAdvice.Describe("", False, False)), "Advice_EmptyNeverEmpty")
        Runner.Check(Not String.IsNullOrEmpty(Global.MegaDownloader.DownloadAdvice.Describe(Nothing, False, False)), "Advice_NothingNeverEmpty")
    End Sub

End Module
