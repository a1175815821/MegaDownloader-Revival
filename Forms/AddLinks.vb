Imports System.Text.RegularExpressions

Public Class AddLinks
	
	Public Main As Main
	Public Config As Configuracion
	Public HiddenLinks As String = String.Empty

	' P0-6 UI:链接计数 debounce(300ms,避免大文本每次击键跑正则)+输入框水印
	Private WithEvents _linkCountTimer As Timer
	Private _cueFallbackLabel As Label = Nothing

	Private Const EM_SETCUEBANNER As Integer = &H1501
	<System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
	Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As String) As IntPtr
	End Function
	
	Private Sub AddLinks_Load(sender As Object, e As System.EventArgs) Handles Me.Load
		ThemeManager.ApplyTheme(Me)
		Translate()
		
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
		
		' Centramos la pantalla
		' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
		Dim scr = Screen.FromPoint(Me.Location)
		Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))
		
    End Sub

    Public Function AgregarEnlaces(texto As String, limpiarContenidoAnterior As Boolean, ByVal ExtraerURLs As Boolean, EsconderLinks As Boolean) As Boolean

        If Not limpiarContenidoAnterior Then
            texto = Me.txtLinks.Text & vbNewLine & texto
        End If

        Dim URLs As Generic.List(Of String) = URLExtractor.ExtraerURLs(texto)
        If URLs IsNot Nothing AndAlso URLs.Count > 0 Then

            If ExtraerURLs Then
                Dim URLstr As String = ""
                For Each url As String In URLs
                    If URLstr.Length > 0 Then
                        URLstr &= vbNewLine
                    End If
                    URLstr &= url
                Next

                texto = URLstr

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
        Me.btnAgregar.Text = Language.GetText("Add links")
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
        ' http://stackoverflow.com/questions/278237/keep-window-on-top-and-steal-focus-in-winforms
        Me.TopMost = True
        Me.TopMost = False
        Me.TopMost = True ' Para que funcione tenemos que quitar Topmost y volverlo a activar (??)
        Me.Activate()
    End Sub

    Private Sub ToggleOpciones_Click(sender As System.Object, e As System.EventArgs)
        OpcionesPaquete.Visible = Not OpcionesPaquete.Visible
    End Sub

	''' <summary>P0-6:输入变化只重启 debounce 计时,真正的正则解析在 Tick 里跑一次。</summary>
	Private Sub txtLinks_TextChanged(sender As Object, e As System.EventArgs) Handles txtLinks.TextChanged
		' RC:清空与 HiddenLinks 同步,不等 300ms debounce,否则窗口期内点添加仍用残留建包。
		If String.IsNullOrWhiteSpace(txtLinks.Text) Then HiddenLinks = String.Empty
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
		Try
			If _linkCountTimer IsNot Nothing Then _linkCountTimer.Dispose()
		Catch
		End Try
	End Sub

	Private Sub UpdateLinkCount()
		Try
			' RC:文本框清空时同步清 HiddenLinks,否则计数残留且点添加仍建包。
			If String.IsNullOrWhiteSpace(txtLinks.Text) Then
				HiddenLinks = String.Empty
			End If
			Dim n As Integer = 0
			If Not String.IsNullOrEmpty(txtLinks.Text) Then
				Dim urls As Generic.List(Of String) = ExtraerURLs()
				If urls IsNot Nothing Then n = urls.Count
				' 隐身链路:N 行占位符对应 1 个 elc,计数至少反映可见行数,避免“5 行显示 1 个”困惑。
				If Not String.IsNullOrEmpty(HiddenLinks) Then
					Try
						Dim placeholder As Integer = 0
						For Each ln As String In txtLinks.Text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
							If ln.Trim() = Fichero.HIDDEN_LINK_DESC Then placeholder += 1
						Next
						If placeholder > n Then n = placeholder
					Catch
					End Try
				End If
			Else
				n = 0
			End If
			If n > 0 Then
				lblLinkCount.Text = Language.GetText("AddLinks_LinkCount").Replace("%N%", n.ToString())
			Else
				lblLinkCount.Text = ""
			End If
			If _cueFallbackLabel IsNot Nothing AndAlso Not _cueFallbackLabel.IsDisposed Then
				_cueFallbackLabel.Visible = (txtLinks.TextLength = 0)
			End If
		Catch ex As Exception
			Log.WriteError("UpdateLinkCount failed: " & ex.ToString)
		End Try
	End Sub

	''' <summary>
	''' P0-6:RichTextBox 水印。EM_SETCUEBANNER 在部分 RichEdit 版本上返回 0,
	''' 此时 fallback 为覆盖式灰 Label(Disabled 穿透点击),随 Shown 只建一次。
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
					_cueFallbackLabel.Enabled = False
					_cueFallbackLabel.BackColor = txtLinks.BackColor
					' RC:深色下水印此前默认黑字不可见,固定灰字。
					_cueFallbackLabel.ForeColor = Drawing.SystemColors.GrayText
					_cueFallbackLabel.Location = New Point(txtLinks.Left + 4, txtLinks.Top + 3)
					_cueFallbackLabel.Text = cue
					Me.Controls.Add(_cueFallbackLabel)
					_cueFallbackLabel.BringToFront()
				End If
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
        Try
            ' RC:与 TextChanged 同步语义——空文本=无操作,残留 HiddenLinks 不得建包。
            If String.IsNullOrWhiteSpace(txtLinks.Text) Then HiddenLinks = String.Empty
            Dim URLs As Generic.List(Of String) = ExtraerURLs()
            If URLs.Count = 0 Then
                Throw New ApplicationException(Language.GetText("Links not valid"))
            ElseIf Not System.IO.Directory.Exists(txtRuta.Text) Then

                Try
                    System.IO.Directory.CreateDirectory(txtRuta.Text)
                    If Not System.IO.Directory.Exists(txtRuta.Text) Then Throw New ApplicationException("Invalid dir")
                Catch ex As Exception
                    Throw New ApplicationException(Language.GetText("Invalid directory"))
                End Try

            Else

                btnAgregar.Text = Language.GetText("Loading...")
                btnAgregar.Enabled = False


                ' Guardamos la última configuración usada
                UltimaConfiguracionUsada.ExisteUltimaConfiguracion = True
                UltimaConfiguracionUsada.CrearDirectorioPaquete = chkCrearDirectorio.Checked
                UltimaConfiguracionUsada.ExtraerAutomaticamente = chkUnZip.Checked
                UltimaConfiguracionUsada.RutaDescarga = txtRuta.Text
                UltimaConfiguracionUsada.IniciarDescarga = chkStartDownload.Checked

                ' v2.5 beta: 文件夹解析可能很慢(API + 逐节点解密),放线程池并显示实时计数,避免 UI 假死。
                ResolveUrlsAsync(URLs, AddressOf OnResolveForAdd)
                Return

            End If
        Catch ex As Exception
            Log.WriteError("Error while adding the link: " & ex.ToString)
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            btnAgregar.Enabled = True
            btnAgregar.Text = Language.GetText("Add links")
        End Try

    End Sub

    ''' <summary>v2.5 beta: 文件夹读取进度窗(常亮计数 + 取消=丢弃结果)。</summary>
    Private Class FolderResolveProgressForm
        Inherits Form

        Public Cancelled As Boolean = False
        Public ReadOnly CancelSource As New System.Threading.CancellationTokenSource()
        Private ReadOnly lbl As New Label()
        Private ReadOnly bar As New ProgressBar()

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
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            Try
                If disposing Then
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
            If n <= 0 Then
                lbl.Text = Language.GetText("Folder_Reading")
            Else
                lbl.Text = Language.GetText("Folder_Reading_Count").Replace("%N%", n.ToString())
            End If
        End Sub
    End Class

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
                    btnAgregar.Enabled = True
                    btnAgregar.Text = Language.GetText("Add links")
                    Return
                End If
                If t.IsFaulted Then
                    Dim inner As Exception = If(t.Exception IsNot Nothing AndAlso t.Exception.InnerException IsNot Nothing, t.Exception.InnerException, DirectCast(t.Exception, Exception))
                    onDone(Nothing, inner, Nothing)
                ElseIf t.IsCanceled Then
                    btnAgregar.Enabled = True
                    btnAgregar.Text = Language.GetText("Add links")
                Else
                    onDone(t.Result, Nothing, Nothing)
                End If
            End Sub, ui)
    End Sub

    Private Sub OnResolveForAdd(URLs2 As Generic.List(Of URLProcessor.FileURL), err As Exception, unused As FolderResolveProgressForm)
        Try
            If err IsNot Nothing Then
                If TypeOf err Is MegaQuotaExceededException Then
                    Log.WriteWarning("Quota hit while reading folder: " & Log.SafeException(err))
                    MessageBox.Show(Language.GetText("Quota_Error"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Else
                    Log.WriteError("Error while adding the link: " & err.ToString)
                    MessageBox.Show(err.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
                btnAgregar.Enabled = True
                btnAgregar.Text = Language.GetText("Add links")
                Return
            End If
            If URLs2 Is Nothing OrElse URLs2.Count = 0 Then
                Throw New ApplicationException(Language.GetText("Links not valid"))
            End If
            FinishAddPackage(URLs2)
        Catch ex As Exception
            Log.WriteError("Error while adding the link: " & ex.ToString)
            MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            btnAgregar.Enabled = True
            btnAgregar.Text = Language.GetText("Add links")
        End Try
    End Sub

    Private Sub FinishAddPackage(URLs2 As Generic.List(Of URLProcessor.FileURL))
        ' Creamos el paquete
        Dim oPaquete As New Paquete
        With oPaquete
            .Nombre = txtNombre.Text
            .RutaLocal = txtRuta.Text
            .CrearSubdirectorio = chkCrearDirectorio.Checked
            .PendienteNombrePaquete = String.IsNullOrEmpty(txtNombre.Text)

            ' Creamos el directorio
            If .CrearSubdirectorio And Not String.IsNullOrEmpty(txtNombre.Text) Then
                Dim packageSegment As String = PathGuard.SanitizeFileName(txtNombre.Text, "package")
                .RutaLocal = PathGuard.GetSafePathUnderRoot(.RutaLocal, packageSegment, allowRoot:=False)
                System.IO.Directory.CreateDirectory(.RutaLocal)
            End If

            Log.WriteWarning("Adding package in " & .RutaLocal)

            .SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
            For Each URL In URLs2

                Dim ruta As String = PathGuard.GetSafePathUnderRoot(oPaquete.RutaLocal, If(URL.Path, String.Empty), allowRoot:=True)
                System.IO.Directory.CreateDirectory(ruta)

                Dim URLFile As String = URL.URL
                Dim Visible As Boolean = True
                If Not String.IsNullOrEmpty(URLFile) AndAlso URLFile.Contains(Fichero.HIDDEN_LINK) Then
                    Visible = False
                    URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "")
                End If

                Dim oFichero As New Fichero(URLFile)
                With oFichero
                    .LinkVisible = Visible
                    .RutaLocal = ruta
                    .RutaRelativa = URL.Path
                    .NombreFichero = If(Visible, URLFile, Fichero.HIDDEN_LINK_DESC)
                    .FileID = Fichero.ExtraerFileID(URLFile)
                    .FileKey = Fichero.ExtraerFileKey(URLFile)
                    .SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
                    Log.WriteWarning("Adding file to the new package: " & .FileID)
                End With
                .AgregarFichero(oFichero)

            Next
        End With

        Main.AgregarPaquete(oPaquete, False)
        If chkStartDownload.Checked Then Main.StartDownload()
        Me.Close()
    End Sub


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


        ResolveUrlsAsync(URLs, AddressOf OnResolveForWatch)
    End Sub

    Private Sub OnResolveForWatch(URLs2 As Generic.List(Of URLProcessor.FileURL), err As Exception, unused As FolderResolveProgressForm)
        Try
            If err IsNot Nothing Then
                If TypeOf err Is MegaQuotaExceededException Then
                    MessageBox.Show(Language.GetText("Quota_Error"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
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
        lblPassword.Enabled = chkUnZip.Checked
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