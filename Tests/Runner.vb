''' <summary>
''' 零 NuGet 回归测试运行器（第二批安全网）。
''' 约束：无测试框架、无网络、无 UI、无文件落盘（纯内存断言 + 路径字符串计算）。
''' 用法：CI 在 Release 构建后执行 Tests\bin\Release\MegaDownloader.Tests.exe，
''' 退出码 0=全绿，非 0=失败数。第二批安全网。
''' </summary>
Public Module Runner

    Private _passed As Integer
    Private _failed As Integer
    Private _skipped As Integer
    Private _failures As New List(Of String)

    Public Sub Main()
        Console.WriteLine("MegaDownloader regression tests")
        Try
            Global.MegaDownloader.Log.SetLogLevel = Global.MegaDownloader.Log.LevelLogType.Minimal
        Catch
        End Try
        RunSuite("URLExtractor", AddressOf UrlExtractorTests.Run)
        RunSuite("PathGuard", AddressOf PathGuardTests.Run)
        RunSuite("Criptografia", AddressOf CriptografiaTests.Run)
        RunSuite("Descompresor", AddressOf DescompresorTests.Run)
        RunSuite("DownloadAdvice", AddressOf DownloadAdviceTests.Run)
        RunSuite("ResolveAbort", AddressOf ResolveAbortTests.Run)
        Console.WriteLine("----")
        Console.WriteLine("Passed: " & _passed & ", Failed: " & _failed & ", Skipped: " & _skipped)
        For Each f As String In _failures
            Console.WriteLine("FAIL: " & f)
        Next
        Environment.ExitCode = If(_failed = 0, 0, 1)
    End Sub

    Private Sub RunSuite(ByVal name As String, ByVal run As Action)
        Try
            run()
        Catch ex As Exception
            Fail(name & " suite crashed: " & ex.GetType().Name & ": " & ex.Message)
        End Try
    End Sub

    Public Sub Check(ByVal condition As Boolean, ByVal testName As String)
        If condition Then
            _passed += 1
            Console.WriteLine("  ok - " & testName)
        Else
            Fail(testName)
        End If
    End Sub

    Public Sub Fail(ByVal testName As String)
        _failed += 1
        _failures.Add(testName)
        Console.WriteLine("  FAIL - " & testName)
    End Sub

    Public Sub Skip(ByVal testName As String, ByVal reason As String)
        _skipped += 1
        Console.WriteLine("  SKIP - " & testName & " (" & reason & ")")
    End Sub

    ''' <summary>断言指定异常类型被抛出（精确类型或派生类均可）。</summary>
    Public Sub CheckThrows(Of T As Exception)(ByVal action As Action, ByVal testName As String)
        Try
            action()
        Catch ex As Exception
            If TypeOf ex Is T Then
                Check(True, testName)
                Return
            End If
            Fail(testName & " [wrong exception: " & ex.GetType().Name & "]")
            Return
        End Try
        Fail(testName & " [no exception thrown]")
    End Sub

End Module
