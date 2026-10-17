Imports System.ComponentModel

Public Class StreamingForm

    Public Config As Configuracion
    Public MainForm As Main


    Private WithEvents bckActualizador As BackgroundWorker

    Private Sub StreamingForm_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        ThemeManager.ApplyTheme(Me)
        Translate()

        If Not Config.ServidorStreamingActivo Then
            txtUrlMEGA.Enabled = False
            txtUrlStreaming.Enabled = False
            lblInfo.Text = Language.GetText("Streaming server not activated")
            ' RC:此前写死纯红,深色下对比与主题脱节,改用主题 ErrorFore。
            lblInfo.ForeColor = ThemeManager.GetColor("ErrorFore")
        End If


        bckActualizador = New BackgroundWorker
        bckActualizador.WorkerSupportsCancellation = True
        bckActualizador.RunWorkerAsync()
        
   		' Centramos la pantalla
        ' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
        Dim scr = Screen.FromPoint(Me.Location)
        Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))
        
    End Sub

 
    Private Sub Translate()
        Me.Label1.Text = Language.GetText("MEGA Url") & ":"
        Me.Label2.Text = Language.GetText("Streaming Url") & ":"
        Me.GroupBox2.Text = Language.GetText("Streaming")
        Me.GroupBox3.Text = Language.GetText("Information")
        Me.lblInfo.Text = Language.GetText("Streaming Info")
        Me.btnCerrar.Text = Language.GetText("Close")
        Me.btnLanzarVLC.Text = Language.GetText("Start VLC")
        Me.Text = Language.GetText("Streaming")
    End Sub


    Public Sub bckActualizador_DoWork(sender As Object, e As DoWorkEventArgs) Handles bckActualizador.DoWork
        Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)

        While Not worker.CancellationPending

            System.Threading.Thread.Sleep(150)

            If worker.CancellationPending Then Exit While

            ActualizarDatos()
        End While
    End Sub

    Private Sub Cerrando() Handles Me.FormClosed
        Try
            If bckActualizador IsNot Nothing Then bckActualizador.CancelAsync()
        Catch
        End Try
    End Sub

    Private PreviousURLMega As String = String.Empty
    Private ValidURLMega As Boolean = False
    ' 第17项:解析流水号。联网解析移到线程池后是异步的,URL 快速改动时
    ' 旧结果不得覆盖新结果;每次(重新)解析都递增,结果回 UI 时校验。
    Private _resolveGenLock As New Object()
    Private _resolveGeneration As Integer = 0

    Delegate Sub ActualizarDatosCallback()
    Private Sub ActualizarDatos()
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Exit Sub

        If Me.txtUrlMEGA.InvokeRequired Then
            Try
                ' 第17项:BeginInvoke 代替阻塞 Invoke,150ms 轮询线程不再被 UI 卡住
                Me.BeginInvoke(New ActualizarDatosCallback(AddressOf ActualizarDatos))
            Catch ex As Exception
                ' A veces da error al cerrar la ventana
            End Try
            Exit Sub
        End If

        If Not Config.ServidorStreamingActivo Then
            ValidURLMega = False
            Exit Sub
        End If

        Dim URLMega As String = Me.txtUrlMEGA.Text
        If URLMega <> PreviousURLMega Then
            PreviousURLMega = URLMega
            ValidURLMega = False

            ' 第17项:CreateStreamingLink 会走 MEGA API 解析(联网),此前在 UI 线程
            ' 同步执行导致输入卡死;移到线程池,结果经 BeginInvoke 回 UI。
            Dim gen As Integer
            SyncLock _resolveGenLock
                _resolveGeneration += 1
                gen = _resolveGeneration
            End SyncLock
            Dim urlCopy As String = URLMega
            Threading.ThreadPool.QueueUserWorkItem(
                Sub(state As Object)
                    Dim StreamingLink As String = String.Empty
                    Try
                        StreamingLink = StreamingHelper.CreateStreamingLink(urlCopy, Config.ServidorStreamingPuerto, Config)
                    Catch ex As Exception
                        Log.WriteError("CreateStreamingLink failed: " & ex.ToString)
                    End Try
                    Try
                        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Exit Sub
                        Me.BeginInvoke(New Action(
                            Sub()
                                SyncLock _resolveGenLock
                                    If gen <> _resolveGeneration Then Exit Sub ' 过期结果丢弃
                                End SyncLock
                                If Not String.IsNullOrEmpty(StreamingLink) Then
                                    txtUrlStreaming.Text = StreamingLink
                                    ValidURLMega = True
                                Else
                                    ValidURLMega = False
                                End If
                            End Sub))
                    Catch
                        ' 窗口正在关闭
                    End Try
                End Sub)
        End If
    End Sub

    ''' <summary>
    ''' 第17项:同步解析一次并应用,仅按钮点击兜底用(粘贴后立即点、后台解析
    ''' 尚未返回时不误报)。递增流水号作废在途的后台解析结果,防旧结果覆盖。
    ''' </summary>
    Private Sub ResolveNow()
        Dim URLMega As String = Me.txtUrlMEGA.Text
        If ValidURLMega AndAlso URLMega = PreviousURLMega Then Exit Sub
        PreviousURLMega = URLMega
        ValidURLMega = False
        SyncLock _resolveGenLock
            _resolveGeneration += 1
        End SyncLock
        Dim StreamingLink As String = StreamingHelper.CreateStreamingLink(URLMega, Config.ServidorStreamingPuerto, Config)
        If String.IsNullOrEmpty(StreamingLink) Then Exit Sub
        txtUrlStreaming.Text = StreamingLink
        ValidURLMega = True
    End Sub

    Private Sub btnLanzarVLC_Click(sender As System.Object, e As System.EventArgs) Handles btnLanzarVLC.Click

        ' RC:ValidURLMega 靠后台 150ms 轮询,粘贴后立即点会误判。先算一次再判。
        ' 第17项:解析已移后台异步跑,点击时若链接有变/尚未解析完,同步兜底一次
        ' (仅用户主动点击触发,不随轮询冻结 UI)。
        Try
            ResolveNow()
        Catch
        End Try
        ' RC:无效链接此前静默 Exit,用户以为按钮死了,给提示。
        If Not ValidURLMega Then
            MessageBox.Show(Language.GetText("URL is mandatory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        If Not StreamingHelper.WatchOnline(Config.VLCPath, txtUrlStreaming.Text) Then
            MessageBox.Show(Language.GetText("VLC could not be started"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If

    End Sub

    Private Sub btnCerrar_Click(sender As System.Object, e As System.EventArgs) Handles btnCerrar.Click
        Me.Close()
    End Sub

    Private Sub HelpButtonPressed() Handles Me.HelpButtonClicked
        Main.FAQ_Click(Nothing, Nothing)
    End Sub
End Class