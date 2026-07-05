Public Class PropiedadesDescarga

    Private _Descarga As IDescarga
    Public WriteOnly Property Descarga As IDescarga
        Set(value As IDescarga)
            _Descarga = value
        End Set
    End Property



    Private Sub PropiedadesDescarga_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        Translate()

        txtNombre.Text = _Descarga.DescargaNombre
        If TypeOf (_Descarga) Is Fichero Then
            GroupBox1.Text = Language.GetText("File properties")
            Dim f As Fichero = CType(_Descarga, Fichero)
            txtMD5.Text = f.MD5
            txtRuta.Text = f.RutaLocal
            txtUrl.Text = If(f.LinkVisible, f.URL, Fichero.HIDDEN_LINK_DESC)
            If String.IsNullOrEmpty(txtMD5.Text) Then
                txtMD5.Text = Language.GetText("Not available")
            End If
            If f.DescargaComenzada Then
                txtRuta.Enabled = False
                btnExaminar.Enabled = False
            End If

            chkLimitarVelocidad.Checked = (f.LimiteVelocidad > 0)
            txtLimiteVelocidad.Enabled = chkLimitarVelocidad.Checked
            If chkLimitarVelocidad.Checked Then
                txtLimiteVelocidad.Text = (f.LimiteVelocidad / 1024).ToString
            End If
        Else
            GroupBox1.Text = Language.GetText("Package properties")
            Dim p As Paquete = CType(_Descarga, Paquete)
            txtRuta.Text = p.RutaLocal
            txtMD5.Text = Language.GetText("Not applied")
            txtUrl.Text = Language.GetText("Not applied")
            txtLimiteVelocidad.Enabled = False
        End If
        txtTamano.Text = Language.GetText("Downloaded") & ": " & PintarTamano(_Descarga.DescargaPorcentaje * _Descarga.DescargaTamanoBytes / 100) & " - " & Language.GetText("Total") & ": " & PintarTamano(_Descarga.DescargaTamanoBytes)
        chkUnZip.Checked = _Descarga.DescargaExtraccionAutomatica
        txtPassword.Text = _Descarga.DescargaExtraccionPassword
        chkUnZip_CheckedChanged(Nothing, Nothing)

    End Sub

    Private Sub Translate()
        Me.chkUnZip.Text = Language.GetText("Automatic extraction")
        Me.btnExaminar.Text = Language.GetText("Browse")
        Me.Label1.Text = Language.GetText("Path") & ":"
        Me.GroupBox1.Text = Language.GetText("Download properties")
        Me.Label5.Text = Language.GetText("URL") & ":"
        Me.Label4.Text = Language.GetText("MD5") & ":"
        Me.Label3.Text = Language.GetText("Size") & ":"
        Me.Label2.Text = Language.GetText("Name") & ":"
        Me.GroupBox2.Text = Language.GetText("Options")
        Me.chkLimitarVelocidad.Text = Language.GetText("Limit speed to")
        Me.LinkLabel1.Text = Language.GetText("Note about the options")
        Me.btnGuardar.Text = Language.GetText("Save")
        Me.btnCancelar.Text = Language.GetText("Cancel")
        Me.Text = Language.GetText("Properties")
        Me.lblPassword.Text = Language.GetText("Password") & ":"
    End Sub

    Private Function PintarTamano(numBytes As Decimal) As String
        Dim Dato As String = "B"
        If numBytes > 1024 Then
            Dato = "KB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "MB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "GB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "TB"
            numBytes = numBytes / 1024
        End If
        Return numBytes.ToString("F2") & " " & Dato

    End Function

    Private t As ToolTip
    Private Function MsgRutaFic() As String
        Return Language.GetText("Can change file path if not downloaded")
    End Function

    Private Function MsgRutaPaq() As String
        Return Language.GetText("Message change package path")
    End Function

    Private Function MsgPasswordZip() As String
        Return Language.GetText("MsgPasswordZip")
    End Function

    Private Sub LinkLabel1_MouseHover(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseHover
        t = New ToolTip
        t.SetToolTip(LinkLabel1, If(TypeOf (_Descarga) Is Fichero, MsgRutaFic, MsgRutaPaq))
    End Sub


    Private Sub LinkLabel1_MouseLeave(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseLeave
        If t IsNot Nothing Then t.Hide(LinkLabel1)
    End Sub

    Private Sub LinkLabel1_Click(sender As Object, e As System.EventArgs) Handles LinkLabel1.Click
        MessageBox.Show(If(TypeOf (_Descarga) Is Fichero, MsgRutaFic, MsgRutaPaq), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
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


    Private Sub btnExaminar_Click(sender As System.Object, e As System.EventArgs) Handles btnExaminar.Click

        Dim ExaminarDirectorio As New FolderBrowserDialog
        ExaminarDirectorio.Description = Language.GetText("Select directory")

        If ExaminarDirectorio.ShowDialog = Windows.Forms.DialogResult.OK Then
            txtRuta.Text = ExaminarDirectorio.SelectedPath
        End If

        ExaminarDirectorio.Dispose()
    End Sub

    Private Sub btnCancelar_Click(sender As System.Object, e As System.EventArgs) Handles btnCancelar.Click
        Me.Close()
    End Sub

    Private Sub btnGuardar_Click(sender As System.Object, e As System.EventArgs) Handles btnGuardar.Click

        Dim LimiteVelocidad As Integer = 0
        Integer.TryParse(txtLimiteVelocidad.Text, LimiteVelocidad)
        If Not chkLimitarVelocidad.Checked Then
            LimiteVelocidad = 0
        ElseIf LimiteVelocidad <= 0 Or String.IsNullOrEmpty(txtLimiteVelocidad.Text) Then
            MessageBox.Show(Language.GetText("Invalid speed limit"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If
        LimiteVelocidad *= 1024


        If TypeOf (_Descarga) Is Fichero Then
            Dim f As Fichero = CType(_Descarga, Fichero)
            If txtRuta.Text <> f.RutaLocal And Not f.DescargaComenzada Then
                f.RutaLocal = txtRuta.Text
            End If
            f.SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
            f.LimiteVelocidad = LimiteVelocidad
            ThrottledStreamController.GetController.SetMaxSpeed(f.FileID, LimiteVelocidad)
        Else
            Dim p As Paquete = CType(_Descarga, Paquete)

            For Each f As Fichero In p.ListaFicheros
                If txtRuta.Text <> f.RutaLocal And Not f.DescargaComenzada Then
                    f.RutaLocal = txtRuta.Text
                End If
                If p.DescargaExtraccionAutomatica <> chkUnZip.Checked Then
                    f.SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
                End If
                f.LimiteVelocidad = LimiteVelocidad
                ThrottledStreamController.GetController.SetMaxSpeed(f.FileID, LimiteVelocidad)
            Next
            If txtRuta.Text <> p.RutaLocal Then
                p.RutaLocal = txtRuta.Text
            End If
            If p.DescargaExtraccionAutomatica <> chkUnZip.Checked Or p.DescargaExtraccionPassword <> txtPassword.Text Then
                p.SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
            End If
        End If
        Me.Close()
    End Sub

    Private Sub chkLimitarVelocidad_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkLimitarVelocidad.CheckedChanged
        txtLimiteVelocidad.Enabled = chkLimitarVelocidad.Checked
    End Sub

    Private Sub chkUnZip_CheckedChanged(sender As Object, e As EventArgs) Handles chkUnZip.CheckedChanged
        txtPassword.Enabled = chkUnZip.Checked
        lblPassword.Enabled = chkUnZip.Checked
    End Sub
End Class