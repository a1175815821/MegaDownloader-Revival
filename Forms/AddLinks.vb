Imports System.Text.RegularExpressions

Public Class AddLinks
	
    Public Main As Main
    Public Config As Configuracion
    Public HiddenLinks As String = String.Empty
	
	Private Sub AddLinks_Load(sender As Object, e As System.EventArgs) Handles Me.Load
		Translate()
		'OpcionesPaquete.Visible = False
		
		txtRuta.Text = Config.RutaDefecto
		chkCrearDirectorio.Checked = Config.CrearDirectorioPaquete
        chkUnZip.Checked = Config.ExtraerAutomaticamente
        chkStartDownload.Checked = True
		
		'If Config.CrearDirectorioPaquete Then
		OpcionesPaquete.Visible = True
		'End If
		
		If Config.MantenerUltimaConfiguracion And UltimaConfiguracionUsada.ExisteUltimaConfiguracion Then
			chkCrearDirectorio.Checked = UltimaConfiguracionUsada.CrearDirectorioPaquete
			chkUnZip.Checked = UltimaConfiguracionUsada.ExtraerAutomaticamente
            txtRuta.Text = UltimaConfiguracionUsada.RutaDescarga
            chkStartDownload.Checked = UltimaConfiguracionUsada.IniciarDescarga
        End If

        chkUnZip_CheckedChanged(Nothing, Nothing)
		
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

                'ElseIf chkCrearDirectorio.Checked And String.IsNullOrEmpty(txtNombre.Text) Then
                '	Throw New ApplicationException(Language.GetText("Invalid package name"))
            Else

                btnAgregar.Text = Language.GetText("Loading...")
                btnAgregar.Enabled = False


                ' Guardamos la última configuración usada
                UltimaConfiguracionUsada.ExisteUltimaConfiguracion = True
                UltimaConfiguracionUsada.CrearDirectorioPaquete = chkCrearDirectorio.Checked
                UltimaConfiguracionUsada.ExtraerAutomaticamente = chkUnZip.Checked
                UltimaConfiguracionUsada.RutaDescarga = txtRuta.Text
                UltimaConfiguracionUsada.IniciarDescarga = chkStartDownload.Checked

                Dim URLs2 = URLProcessor.ProcessURLs(URLs, Me.Config)


                If URLs2.Count = 0 Then
                    Throw New ApplicationException(Language.GetText("Links not valid"))
                End If

                ' Creamos el paquete
                Dim oPaquete As New Paquete
                With oPaquete
                    .Nombre = txtNombre.Text
                    .RutaLocal = txtRuta.Text
                    .CrearSubdirectorio = chkCrearDirectorio.Checked
                    .PendienteNombrePaquete = String.IsNullOrEmpty(txtNombre.Text)

                    ' Creamos el directorio
                    If .CrearSubdirectorio And Not String.IsNullOrEmpty(txtNombre.Text) Then
                        .RutaLocal = System.IO.Path.Combine(.RutaLocal, txtNombre.Text)
                        System.IO.Directory.CreateDirectory(.RutaLocal)
                    End If

                    Log.WriteWarning("Adding package in " & .RutaLocal)

                    .SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
                    For Each URL In URLs2

                        Dim ruta As String = System.IO.Path.Combine(oPaquete.RutaLocal, URL.Path)
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


            End If
        Catch ex As Exception
            Log.WriteError("Error while adding the link: " & ex.ToString)
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            btnAgregar.Enabled = True
            btnAgregar.Text = Language.GetText("Add links")
        End Try

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


        Dim URLs2 = URLProcessor.ProcessURLs(URLs, Me.Config)

        ' Contenedores de links
        'Dim URLs2 As New Generic.List(Of String)
        'For Each URL As String In URLs
        '    If LinkProtectors.IsLinkProtector(URL) Then
        '        URLs2.AddRange(LinkProtectors.ExtraerURLs(URL))
        '    Else
        '        URLs2.Add(URL)
        '    End If
        'Next

        If URLs2.Count = 0 Then
            MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If


        Dim link As String = StreamingHelper.CreateStreamingLink(URLs2(0).URL, Config.ServidorStreamingPuerto, Config)
        If String.IsNullOrEmpty(link) Then
            MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        StreamingHelper.WatchOnline(Config.VLCPath, link)
        Me.Close()
    End Sub



    Private t As ToolTip
    Private Function MsgVerOnline() As String
        Return Language.GetText("Watch Online Note")
    End Function
    Private Function MsgPasswordZip() As String
        Return Language.GetText("MsgPasswordZip")
    End Function

    Private Sub LinkLabel1_MouseHover(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseHover
        t = New ToolTip
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
        t = New ToolTip
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