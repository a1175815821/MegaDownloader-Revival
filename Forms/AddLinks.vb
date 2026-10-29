Imports System.Text.RegularExpressions

Public Class AddLinks

    Public Sub New()
        ' Build the complete hierarchy before the first automatic font/DPI scaling pass.
        SuspendLayout()
        Try
            InitializeComponent()
            InitAddLinksLayout()
        Finally
            ResumeLayout(True)
        End Try
    End Sub
	
	Public Main As Main
	Public Config As Configuracion
	Public HiddenLinks As String = String.Empty
    Public ReadOnly Property LastImportedPackage As Paquete

	' P0-6 UI:链接计数 debounce(300ms,避免大文本每次击键跑正则)+输入框水印
	Private WithEvents _linkCountTimer As Timer
	Private _cueFallbackLabel As Label = Nothing

	Private Const EM_SETCUEBANNER As Integer = &H1501
	<System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
	Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As String) As IntPtr
	End Function
	
	Private Sub AddLinks_Load(sender As Object, e As System.EventArgs) Handles Me.Load
		Translate()
		ThemeManager.ApplyTheme(Me)
		
		txtRuta.Text = Config.RutaDefecto
		chkCrearDirectorio.Checked = Config.CrearDirectorioPaquete
        chkUnZip.Checked = Config.ExtraerAutomaticamente
        chkStartDownload.Checked = True
		
		'OpcionesPaquete 常显(历史条件已废弃,见 git)。
		OpcionesPaquete.Visible = True
		
		If Config.MantenerUltimaConfiguracion And UltimaConfiguracionUsada.ExisteUltimaConfiguracion Then
			chkCrearDirectorio.Checked = UltimaConfiguracionUsada.CrearDirectorioPaquete
			chkUnZip.Checked = UltimaConfiguracionUsada.ExtraerAutomaticamente
            txtRuta.Text = UltimaConfiguracionUsada.RutaDescarga
            chkStartDownload.Checked = UltimaConfiguracionUsada.IniciarDescarga
        End If

        chkUnZip_CheckedChanged(Nothing, Nothing)

		' P0-6:计数 Timer 挂 components,随窗体释放(本窗体每次打开新建,防泄漏)
		If Me.components IsNot Nothing Then
			_linkCountTimer = New Timer(Me.components)
		Else
			_linkCountTimer = New Timer()
		End If
		_linkCountTimer.Interval = 300
		
		FitAddLinksToWorkingArea(True)
        addLinksLayoutReady = True
        UpdateAddLinksInputHeight()
		
    End Sub

    Public Function AgregarEnlaces(texto As String, limpiarContenidoAnterior As Boolean, ByVal ExtraerURLs As Boolean, EsconderLinks As Boolean, Optional parsed As Generic.List(Of String) = Nothing) As Boolean

        If importBusy OrElse onSelectionPage Then
            deferredInputs.Add(Tuple.Create(texto, ExtraerURLs, EsconderLinks, parsed))
            If preflightView IsNot Nothing Then preflightView.ShowNotice(Language.GetText("AddLinks_Deferred"))
            Return False ' Keep the current selection stable; merge new links on return.
        End If
        If parsed IsNot Nothing AndAlso Not limpiarContenidoAnterior AndAlso Not EsconderLinks Then
            If parsed.Count = 0 Then Return False
            Dim addition As String = If(ExtraerURLs, String.Join(vbNewLine, parsed), texto)
            If txtLinks.TextLength > 0 Then txtLinks.AppendText(vbNewLine)
            txtLinks.AppendText(addition)
            Return True
        End If

        Dim previousText As String = Me.txtLinks.Text
        If Not limpiarContenidoAnterior Then
            texto = Me.txtLinks.Text & vbNewLine & texto
        End If

        Dim URLs As Generic.List(Of String) = If(parsed IsNot Nothing AndAlso Not EsconderLinks, parsed, URLExtractor.ExtraerURLs(texto))
        If URLs IsNot Nothing AndAlso URLs.Count > 0 Then

            If ExtraerURLs Then
                texto = String.Join(vbNewLine, URLs)
                If parsed IsNot Nothing AndAlso Not limpiarContenidoAnterior Then texto = previousText & vbNewLine & texto

            End If

            If EsconderLinks Then

                ' Lo guardamos como ELC para que los links no sean visibles
                Dim lHidden As New Generic.List(Of ServerEncoderLinkHelper.MegaLink)

                URLs = URLExtractor.ExtraerURLs(texto)
                If URLs IsNot Nothing AndAlso URLs.Count > 0 Then
                    For Each url In URLs
                        texto = texto.Replace(url, Fichero.HIDDEN_LINK_DESC)

                        Dim h As New ServerEncoderLinkHelper.MegaLink
                        h.FileID = URLExtractor.ExtraerFileID(url)
                        h.FileKey = URLExtractor.ExtraerFileKey(url)
                        h.MegaFolder = URLExtractor.IsMegaFolder(url)
                        h.SubFolderID = URLExtractor.ExtraerSubFolderID(url)
                        h.SubFileID = URLExtractor.ExtraerSubFileID(url)
                        lHidden.Add(h)

                    Next
                End If

                ' P1-3:多批隐身链接必须换行分隔,否则拼成 mega://elc?AAAAmega://elc?BBBB,
                ' 正则把第二段吃掉导致静默丢失(同文件 URLstr 拼接即用 vbNewLine)。
                If HiddenLinks.Length > 0 Then HiddenLinks &= vbNewLine
                HiddenLinks &= "mega://elc?" & ServerEncoderLinkHelper.ServerEncode("HIDDEN", lHidden, Me.Config)

            End If

            Me.txtLinks.Text = texto

            Return True
        Else
            Return False
        End If
    End Function

    Private Sub Translate()
        Me.Text = Language.GetText("Add links")
        Me.OpcionesPaquete.Text = Language.GetText("Options")
        Me.chkUnZip.Text = Language.GetText("Automatic extraction")
        Me.chkCrearDirectorio.Text = Language.GetText("Create directory")
        Me.btnExaminar.Text = Language.GetText("Browse")
        Me.Label2.Text = Language.GetText("Name") & ":"
        Me.Label1.Text = Language.GetText("Path") & ":"
        Me.btnAgregar.Text = Language.GetText("AddLinks_SelectFiles")
        Me.btnWatchOnline.Text = Language.GetText("Watch Online")
        Me.chkStartDownload.Text = Language.GetText("Start download")
        Me.lblPassword.Text = Language.GetText("Password") & ":"
        Me.linkStegano.Text = Language.GetText("Retrieve links from an image")
        'Me.LinkLabel1.Text = Language.GetText("Watch Online help link")
    End Sub

    Private Sub AddLinks_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        TrySetCueBanner()
        UpdateLinkCount()
        PonerFoco()
        If Not String.IsNullOrEmpty(txtLinks.Text) Then
            btnAgregar.Focus()
        Else
            txtLinks.Focus()
        End If
    End Sub

    Public Sub PonerFoco()
        ' The modal owner keeps this window above Main without making it globally topmost.
        Me.Activate()
    End Sub

    Private Sub ToggleOpciones_Click(sender As System.Object, e As System.EventArgs)
        OpcionesPaquete.Visible = Not OpcionesPaquete.Visible
    End Sub

	''' <summary>P0-6:输入变化只重启 debounce 计时,真正的正则解析在 Tick 里跑一次。</summary>
	Private Sub txtLinks_TextChanged(sender As Object, e As System.EventArgs) Handles txtLinks.TextChanged
		' RC:清空与 HiddenLinks 同步,不等 300ms debounce,否则窗口期内点添加仍用残留建包。
		If String.IsNullOrWhiteSpace(txtLinks.Text) Then HiddenLinks = String.Empty
		PreflightInputChanged()
		If _linkCountTimer IsNot Nothing Then
			_linkCountTimer.Stop()
			_linkCountTimer.Start()
		End If
	End Sub

	Private Sub _linkCountTimer_Tick(sender As Object, e As System.EventArgs) Handles _linkCountTimer.Tick
		_linkCountTimer.Stop()
		UpdateLinkCount()
	End Sub

	Private Sub AddLinks_FormClosed(sender As Object, e As System.Windows.Forms.FormClosedEventArgs) Handles Me.FormClosed
		DisposePreflight()
		Try
			If _linkCountTimer IsNot Nothing Then _linkCountTimer.Dispose()
		Catch
		End Try
	End Sub

	''' <summary>
	''' P0-6:RichTextBox 水印。EM_SETCUEBANNER 在部分 RichEdit 版本上返回 0,
	''' 此时使用带主题色的提示 Label，点击交还输入焦点，随 Shown 只建一次。
	''' </summary>
	Private Sub TrySetCueBanner()
		Try
			Dim cue As String = Language.GetText("AddLinks_CueBanner")
			Dim ok As Boolean = False
			If txtLinks.IsHandleCreated AndAlso Not String.IsNullOrEmpty(cue) Then
				ok = (SendMessage(txtLinks.Handle, EM_SETCUEBANNER, 0, cue) <> IntPtr.Zero)
			End If
			If Not ok Then
				If _cueFallbackLabel Is Nothing OrElse _cueFallbackLabel.IsDisposed Then
					_cueFallbackLabel = New Label()
					_cueFallbackLabel.AutoSize = True
					_cueFallbackLabel.Enabled = True
					_cueFallbackLabel.BackColor = txtLinks.BackColor
					' RC:深色下水印此前默认黑字不可见,固定灰字。
					UiStyles.SetRole(_cueFallbackLabel, UiStyles.Role.Muted)
					_cueFallbackLabel.Location = New Point(4, 3)
					_cueFallbackLabel.Text = cue
					txtLinks.Controls.Add(_cueFallbackLabel)
					AddHandler _cueFallbackLabel.Click, Sub() txtLinks.Focus()
					_cueFallbackLabel.BringToFront()
				End If
				UpdateAddLinksCueBounds()
				_cueFallbackLabel.Visible = (txtLinks.TextLength = 0)
			End If
		Catch ex As Exception
			Log.WriteError("TrySetCueBanner failed: " & ex.ToString)
		End Try
	End Sub


    Private Sub btnExaminar_Click(sender As System.Object, e As System.EventArgs) Handles btnExaminar.Click

        Dim ExaminarDirectorio As New FolderBrowserDialog
        ExaminarDirectorio.Description = Language.GetText("Select directory")

        If ExaminarDirectorio.ShowDialog = Windows.Forms.DialogResult.OK Then
            txtRuta.Text = ExaminarDirectorio.SelectedPath
        End If

        ExaminarDirectorio.Dispose()

    End Sub

    Private Function ExtraerURLs() As Generic.List(Of String)
        Return URLExtractor.ExtraerURLs(txtLinks.Text & vbNewLine & HiddenLinks)
    End Function

    Private Sub btnAgregar_Click(sender As System.Object, e As System.EventArgs) Handles btnAgregar.Click
        ShowSelectionPage()
    End Sub

    ''' <summary>v2.5 beta: 文件夹读取进度窗(常亮计数 + 取消=丢弃结果)。</summary>
    Private Class FolderResolveProgressForm
        Inherits Form

        Public Cancelled As Boolean = False
        Public ReadOnly CancelSource As New System.Threading.CancellationTokenSource()
        Private ReadOnly lbl As New Label()
        Private ReadOnly bar As New ProgressBar()
        ' 卡住可感知：解析主体是同步阻塞 I/O（单次 60s+），计数可能几分钟不动。
        ' 用 1s 滴答刷"已用秒数"，转圈 freeze 时用户能区分"还活着"与"死了"，取消键也敢点。
        ' 后缀纯数字+s，无需进语言系统。
        Private ReadOnly _started As Date = Now
        Private _lastCount As Integer = 0
        Private WithEvents _elapsedTimer As New System.Windows.Forms.Timer()

        <System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
        Private Shared Sub SetWindowTheme(hWnd As IntPtr, appName As String, idList As String)
        End Sub

        Public Sub New()
            Me.Text = Language.GetText("Add links")
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False
            Me.StartPosition = FormStartPosition.CenterParent
            Me.Size = New System.Drawing.Size(380, 130)
            lbl.Left = 12
            lbl.Top = 12
            lbl.Width = 340
            lbl.Height = 40
            bar.Left = 12
            bar.Top = 58
            bar.Width = 250
            bar.Height = 23
            bar.Style = ProgressBarStyle.Marquee
            Dim btn As New Button()
            btn.Text = Language.GetText("Cancel")
            If String.IsNullOrEmpty(btn.Text) Then btn.Text = "Cancel"
            btn.Left = 270
            btn.Top = 56
            btn.Width = 82
            btn.Height = 25
            AddHandler btn.Click, AddressOf OnCancel
            Me.Controls.Add(lbl)
            Me.Controls.Add(bar)
            Me.Controls.Add(btn)
            ThemeManager.ApplyTheme(Me)
            ApplyProgressTheme()
            SetCount(0)
            Try
                _elapsedTimer.Interval = 1000
                _elapsedTimer.Start()
            Catch
            End Try
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            Try
                If disposing Then
                    Try
                        _elapsedTimer.Stop()
                    Catch
                    End Try
                    _elapsedTimer.Dispose()
                    Try
                        CancelSource.Cancel()
                    Catch
                    End Try
                    CancelSource.Dispose()
                End If
            Finally
                MyBase.Dispose(disposing)
            End Try
        End Sub

        ''' <summary>原生 ProgressBar 在视觉样式下忽略 Back/ForeColor(主列表靠 OLV 自绘才解决)。
        ''' 此处去视觉样式走经典绘制,颜色跟主题,Marquee 照常滚动。</summary>
        Private Sub ApplyProgressTheme()
            Try
                If bar.IsHandleCreated Then
                    SetWindowTheme(bar.Handle, "", "")
                End If
                bar.BackColor = ThemeManager.GetColor("ControlBack")
                bar.ForeColor = ThemeManager.GetColor("Selection")
            Catch
            End Try
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ApplyProgressTheme()
        End Sub

        Private Sub OnCancel(sender As Object, e As EventArgs)
            Cancelled = True
            ' 真取消:此前只关窗丢结果,后台把整个文件夹解析跑完,熔断期也在耗 API。
            Try
                CancelSource.Cancel()
            Catch
            End Try
            Me.Close()
        End Sub

        Public Sub SetCount(n As Integer)
            If Me.InvokeRequired Then
                Me.BeginInvoke(New Action(Of Integer)(AddressOf SetCount), n)
                Return
            End If
            _lastCount = n
            RenderLabel()
        End Sub

        Private Sub _elapsedTimer_Tick(sender As Object, e As EventArgs) Handles _elapsedTimer.Tick
            Try
                If Me.IsDisposed OrElse Me.Disposing Then Return
                RenderLabel()
            Catch
            End Try
        End Sub

        Private Sub RenderLabel()
            Dim base As String
            If _lastCount <= 0 Then
                base = Language.GetText("Folder_Reading")
            Else
                base = Language.GetText("Folder_Reading_Count").Replace("%N%", _lastCount.ToString())
            End If
            Dim secs As Long = CLng(Now.Subtract(_started).TotalSeconds)
            If secs < 0 Then secs = 0
            lbl.Text = base & " (" & secs & "s)"
        End Sub
    End Class

    ''' <summary>P0-2:解析取消/完成的统一复位。ResolveUrlsAsync 被 Add 与 Watch 共用,
    ''' 取消分支必须同时复位两个按钮 + _watchResolving,否则 Watch 取消一次就永久锁死。</summary>
    Private Sub ResetResolveButtons()
        _watchResolving = False
        Try
            If btnAgregar IsNot Nothing AndAlso Not btnAgregar.IsDisposed Then
                btnAgregar.Enabled = True
                btnAgregar.Text = Language.GetText("AddLinks_SelectFiles")
            End If
        Catch
        End Try
        Try
            If btnWatchOnline IsNot Nothing AndAlso Not btnWatchOnline.IsDisposed Then
                btnWatchOnline.Enabled = True
            End If
        Catch
        End Try
    End Sub

    Private Sub ResolveUrlsAsync(URLs As Generic.List(Of String), onDone As Action(Of Generic.List(Of URLProcessor.FileURL), Exception, FolderResolveProgressForm))
        Dim dlg As New FolderResolveProgressForm()
        dlg.Show(Me)
        Dim prog As New Progress(Of Integer)(Sub(n) dlg.SetCount(n))
        Dim cfg As Configuracion = Me.Config
        Dim ct As System.Threading.CancellationToken = dlg.CancelSource.Token
        Dim ui As System.Threading.Tasks.TaskScheduler = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext()
        System.Threading.Tasks.Task.Run(Function() URLProcessor.ProcessURLs(URLs, cfg, prog, ct), ct).ContinueWith(
            Sub(t)
                Dim wasCancelled As Boolean = dlg.Cancelled
                Try
                    dlg.Close()
                Catch
                End Try
                dlg.Dispose()
                If Me.IsDisposed Then Return
                If wasCancelled Then
                    ResetResolveButtons()
                    Return
                End If
                If t.IsFaulted Then
                    Dim inner As Exception = If(t.Exception IsNot Nothing AndAlso t.Exception.InnerException IsNot Nothing, t.Exception.InnerException, DirectCast(t.Exception, Exception))
                    onDone(Nothing, inner, Nothing)
                ElseIf t.IsCanceled Then
                    ResetResolveButtons()
                Else
                    onDone(t.Result, Nothing, Nothing)
                End If
            End Sub, ui)
    End Sub

    ''' <summary>N-P2-1: in breakthrough mode nothing is quarantined and the Retry-now
    ''' banner never appears, so the standard Quota_Error text would promise pause/resume/
    ''' retry mechanisms that do not exist in that mode. Shared by OnResolveForAdd and
    ''' OnResolveForWatch, where this block was duplicated verbatim.</summary>
    Private Shared Function GetQuotaErrorMessage() As String
        If Not MegaQuotaManager.IsBreakthroughMode() Then Return Language.GetText("Quota_Error")
        Dim quotaMsg As String = Language.GetText("Quota_Error_Breakthrough")
        If String.IsNullOrEmpty(quotaMsg) OrElse quotaMsg = "Quota_Error_Breakthrough" Then
            quotaMsg = "MEGA transfer quota exceeded while reading the shared folder (HTTP 509 / -17). Breakthrough mode is on: the queue is not paused automatically - change IP or proxy and try again."
        End If
        Return quotaMsg
    End Function


    Private Async Function FinishAddPackageAsync(urls As Generic.List(Of URLProcessor.FileURL)) As System.Threading.Tasks.Task(Of Boolean)
        Dim name As String = txtNombre.Text
        Dim path As String = txtRuta.Text
        Dim create As Boolean = chkCrearDirectorio.Checked
        Dim extract As Boolean = chkUnZip.Checked
        Dim password As String = txtPassword.Text
        Dim token As System.Threading.CancellationToken = importCancellation.Token
        Dim progress As New PreflightProgress.ItemReporter(Sub(count) System.Threading.Volatile.Write(importCompleted, count))
        Dim package As Paquete = Await System.Threading.Tasks.Task.Run(Function() DownloadPackageBuilder.Build(urls, name, path, create, extract, password, token, progress), token)
        importCommitting = True
        RefreshImportProgress()
        ' Give the final stage a paint turn before the synchronous queue publication.
        Await System.Threading.Tasks.Task.Yield()
        If preflightDisposed OrElse token.IsCancellationRequested Then
            For Each file As Fichero In package.ListaFicheros
                file.Dispose()
            Next
            token.ThrowIfCancellationRequested()
            Return False
        End If
        Try
            Main.AgregarPaquete(package, False, True)
        Catch ex As Exception
            If Not Main.IsPackageQueued(package) Then
                For Each file As Fichero In package.ListaFicheros
                    file.Dispose()
                Next
                Throw
            End If
            ' Queue ownership has already transferred. A later refresh/notification
            ' failure must not report this batch as uncommitted or dispose its files.
            Log.WriteError("Imported tasks were queued, but post-add updates failed: " & Log.SafeException(ex))
            Main.SaveImportedQueueInBackground()
        End Try
        _LastImportedPackage = package
        Return True
    End Function


    ' 在线观看解析进行中标记：ResolveUrlsAsync 是异步的，连点/双击会并发跑两次
    ' 解析，同样的链接被解析两次还可能弹两个 VLC。进行中直接忽略新的点击。
    Private _watchResolving As Boolean = False

    Private Sub btnWatchOnline_Click(sender As System.Object, e As System.EventArgs) Handles btnWatchOnline.Click
        If Not Config.ServidorStreamingActivo Then
            MessageBox.Show(Language.GetText("Streaming server not activated"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        If String.IsNullOrEmpty(Config.VLCPath) Then
            MessageBox.Show(Language.GetText("Missing VLC Path"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If
        Dim URLs As Generic.List(Of String) = ExtraerURLs()
        If URLs.Count = 0 Then
            MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        If _watchResolving Then Return
        _watchResolving = True
        btnWatchOnline.Enabled = False
        btnAgregar.Enabled = False

        ResolveUrlsAsync(URLs, AddressOf OnResolveForWatch)
    End Sub

    Private Sub OnResolveForWatch(URLs2 As Generic.List(Of URLProcessor.FileURL), err As Exception, unused As FolderResolveProgressForm)
        Try
            If err IsNot Nothing Then
                If TypeOf err Is MegaQuotaExceededException Then
                    MessageBox.Show(GetQuotaErrorMessage(), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Else
                    Log.WriteError("Error resolving links for streaming: " & err.ToString)
                    MessageBox.Show(err.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
                Return
            End If
            If URLs2 Is Nothing OrElse URLs2.Count = 0 Then
                MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If
            Dim link As String = StreamingHelper.CreateStreamingLink(URLs2(0).URL, Config.ServidorStreamingPuerto, Config)
            If String.IsNullOrEmpty(link) Then
                MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If
            If Not StreamingHelper.WatchOnline(Config.VLCPath, link) Then
                MessageBox.Show(Language.GetText("VLC could not be started"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If
            Me.Close()
        Catch ex As Exception
            Log.WriteError("Error resolving links for streaming: " & ex.ToString)
            MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _watchResolving = False
            If btnWatchOnline IsNot Nothing AndAlso Not btnWatchOnline.IsDisposed Then btnWatchOnline.Enabled = True
            If btnAgregar IsNot Nothing AndAlso Not btnAgregar.IsDisposed Then btnAgregar.Enabled = True
        End Try
    End Sub



    Private t As ToolTip
    Private Function MsgVerOnline() As String
        Return Language.GetText("Watch Online Note")
    End Function
    Private Function MsgPasswordZip() As String
        Return Language.GetText("MsgPasswordZip")
    End Function

    Private Sub LinkLabel1_MouseHover(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseHover
        If t Is Nothing Then t = New ToolTip
        ThemeManager.ApplyThemeToToolTip(t)
        t.SetToolTip(LinkLabel1, MsgVerOnline)
    End Sub

    Private Sub LinkLabel1_MouseLeave(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseLeave
        If t IsNot Nothing Then t.Hide(LinkLabel1)
    End Sub

    Private Sub LinkLabel1_Click(sender As Object, e As System.EventArgs) Handles LinkLabel1.Click
        MessageBox.Show(MsgVerOnline, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub


    Private Sub chkUnZip_CheckedChanged(sender As Object, e As EventArgs) Handles chkUnZip.CheckedChanged
        txtPassword.Enabled = chkUnZip.Checked
        ' Labels are not interactive; use themed muted text instead of native disabled ink.
        UiStyles.SetRole(lblPassword, If(chkUnZip.Checked, UiStyles.Role.BodyText, UiStyles.Role.Muted))
    End Sub

    Private Sub LinkLabel2_MouseHover(sender As Object, e As System.EventArgs) Handles LinkLabel2.MouseHover
        If t Is Nothing Then t = New ToolTip
        ThemeManager.ApplyThemeToToolTip(t)
        t.SetToolTip(LinkLabel2, MsgPasswordZip)
    End Sub

    Private Sub LinkLabel2_MouseLeave(sender As Object, e As System.EventArgs) Handles LinkLabel2.MouseLeave
        If t IsNot Nothing Then t.Hide(LinkLabel2)
    End Sub

    Private Sub LinkLabel2_Click(sender As Object, e As System.EventArgs) Handles LinkLabel2.Click
        MessageBox.Show(MsgPasswordZip, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

 
    Private Sub linkStegano_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles linkStegano.LinkClicked
        OpenSteganoLoadOnExit = True
        Me.Close()
    End Sub

    ' Tell the Main form to open de stegano form
    Public OpenSteganoLoadOnExit As Boolean = False
End Class
