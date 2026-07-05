Public Class PantallaMsg

    Public TextoError As String
    Public MostrarCodificarEnlaces As Boolean = False
    Private Sub VerError_Load(sender As Object, e As System.EventArgs) Handles Me.Load
    	
        txtDatos.Multiline = True
        txtDatos.Text = (TextoError & "").Replace(vbNewLine, Environment.NewLine)

        chkCodificarEnlaces.Visible = MostrarCodificarEnlaces
        lklGenerarELC.Visible = MostrarCodificarEnlaces

        Me.Text = Language.GetText("Information")
        Me.Button1.Text = Language.GetText("Close")
        Me.chkCodificarEnlaces.Text = Language.GetText("Encode Url")
        Me.lklGenerarELC.Text = Language.GetText("Generate ELC")

        Me.lklGenerarELC.Location = New System.Drawing.Point(Me.chkCodificarEnlaces.Location.X + Me.chkCodificarEnlaces.Width, Me.lklGenerarELC.Location.Y)

        ' Centramos la pantalla
        ' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
        Dim scr = Screen.FromPoint(Me.Location)
        Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))

    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
        Me.Close()
    End Sub


    Private Sub lklGenerarELC_Click(sender As System.Object, e As System.EventArgs) Handles lklGenerarELC.Click
        Dim ventanaELC As New ELCForm
        ventanaELC.MegaURLs = TextoError
        ventanaELC.MainForm = CType(Me.Owner, Main)
        ventanaELC.Show()
        Me.Close()
    End Sub


    Private Sub chkCodificarEnlaces_CheckedChanged(sender As Object, e As System.EventArgs) Handles chkCodificarEnlaces.CheckedChanged
        If chkCodificarEnlaces.Checked Then

            Dim l As Generic.List(Of String) = URLExtractor.ExtraerSoloURLsOficiales(TextoError)
            Dim Texto As String = TextoError
            For Each Url As String In l
                Dim FileID As String = URLExtractor.ExtraerFileID(Url)
                Dim FileKey As String = URLExtractor.ExtraerFileKey(Url)
                If Not String.IsNullOrEmpty(FileID) Then
                    Dim NewUrl As String = URLExtractor.GenerateEncodedURILink(FileID, FileKey, URLExtractor.IsMegaFolder(Url), False)
                    Texto = Texto.Replace(Url, NewUrl)
                End If
            Next

            txtDatos.Text = (Texto & "").Replace(vbNewLine, Environment.NewLine)

        Else
            txtDatos.Text = (TextoError & "").Replace(vbNewLine, Environment.NewLine)
        End If
    End Sub


End Class