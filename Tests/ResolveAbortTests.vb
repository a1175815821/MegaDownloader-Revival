''' <summary>
''' 导入取消穿透回归用例：服务端接受连接但永远不回包（模拟黑洞代理/卡死的服务端），
''' 取消后阻塞的 SendJSON 必须迅速以 OperationCanceledException 返回，
''' 而不是等 60s 超时。纯本机回环，无外部网络依赖。
''' 注意：Conexion.SendJSON 是 Friend，用反射调用（不为测试放宽封装）。
''' </summary>
Public Module ResolveAbortTests

    Public Sub Run()
        Dim listener As New Net.HttpListener()
        Dim port As Integer = 18099
        listener.Prefixes.Add("http://127.0.0.1:" & port & "/hang/")
        Try
            listener.Start()
        Catch ex As Exception
            Runner.Skip("SendJSON_Abort", "HttpListener unavailable: " + ex.GetType().Name)
            Return
        End Try
        Try
            ' 黑洞服务：接受连接但 30 秒内不回包
            System.Threading.Tasks.Task.Run(Sub()
                                                Try
                                                    Dim ctx As Net.HttpListenerContext = listener.GetContext()
                                                    System.Threading.Thread.Sleep(30000)
                                                    Try
                                                        ctx.Response.StatusCode = 200
                                                        ctx.Response.Close()
                                                    Catch
                                                    End Try
                                                Catch
                                                End Try
                                            End Sub)
            Dim asm As System.Reflection.Assembly = GetType(Global.MegaDownloader.URLExtractor).Assembly
            Dim t As Type = asm.GetType("MegaDownloader.Conexion")
            Runner.Check(t IsNot Nothing, "SendJSON_Abort_TypeFound")
            If t Is Nothing Then Return
            Dim m As System.Reflection.MethodInfo = t.GetMethod("SendJSON", Reflection.BindingFlags.Static Or Reflection.BindingFlags.NonPublic)
            Runner.Check(m IsNot Nothing, "SendJSON_Abort_MethodFound")
            If m Is Nothing Then Return

            Dim cts As New System.Threading.CancellationTokenSource()
            Dim gotCancel As Boolean = False
            Dim gotOther As String = Nothing
            Dim worker As System.Threading.Tasks.Task = System.Threading.Tasks.Task.Run(Sub()
                                                                                            Try
                                                                                                Dim pars() As Object = New Object() {"http://127.0.0.1:" & port & "/hang/", "{}", "", True, cts.Token}
                                                                                                m.Invoke(Nothing, pars)
                                                                                            Catch ex As System.Reflection.TargetInvocationException
                                                                                                If TypeOf ex.InnerException Is OperationCanceledException Then
                                                                                                    gotCancel = True
                                                                                                ElseIf ex.InnerException IsNot Nothing Then
                                                                                                    gotOther = ex.InnerException.GetType().Name
                                                                                                End If
                                                                                            Catch ex As Exception
                                                                                                gotOther = ex.GetType().Name
                                                                                            End Try
                                                                                        End Sub)
            System.Threading.Thread.Sleep(800)
            Dim sw As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
            cts.Cancel()
            Dim finished As Boolean = worker.Wait(15000)
            sw.Stop()
            Runner.Check(finished, "SendJSON_Abort_Returns")
            Runner.Check(gotCancel, "SendJSON_Abort_IsCancellation" & If(gotOther Is Nothing, "", " (got " & gotOther & ")"))
            Runner.Check(sw.Elapsed.TotalSeconds < 15, "SendJSON_Abort_Fast")
        Finally
            Try
                listener.Stop()
            Catch
            End Try
        End Try
    End Sub

End Module
