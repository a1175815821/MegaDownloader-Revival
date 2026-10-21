Public Class PropiedadesDescarga

    Private _Descarga As IDescarga
    Public WriteOnly Property Descarga As IDescarga
        Set(value As IDescarga)
            _Descarga = value
        End Set
    End Property

    ' P2-6: 载入时的限速状态快照(单位 KB,与 Fichero.LimiteVelocidad 一致)。
    ' 包模式下"复选框未勾选"有两种含义:用户从没碰过(包内各文件限速不一致,
    ' 载入时就不勾),或者用户主动取消了勾选。之前只按 Checked 判断,后者被
    ' 当成前者 → 取消勾选保存后限速纹丝不动,重开属性窗口复选框又变回勾选。
    ' 改用"用户是否动过"来判断:只有相对快照发生了变化才下发/清除限速。
    Private _limiteInicialActivo As Boolean = False
    Private _limiteInicialKB As Integer = 0



    Private Sub PropiedadesDescarga_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        ThemeManager.ApplyTheme(Me)
        Translate()

        txtNombre.Text = _Descarga.DescargaNombre
        ' P2-6: 载入时的限速快照(见字段说明)。限速以 KB 记,与 Fichero.LimiteVelocidad 同单位。
        Dim limiteInicialKB As Integer = 0
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
                txtLimiteVelocidad.Text = (f.LimiteVelocidad / 1024).ToString("0.####")
                limiteInicialKB = f.LimiteVelocidad
            End If
        Else
            GroupBox1.Text = Language.GetText("Package properties")
            Dim p As Paquete = CType(_Descarga, Paquete)
            txtRuta.Text = p.RutaLocal
            txtMD5.Text = Language.GetText("Not applied")
            txtUrl.Text = Language.GetText("Not applied")
            ' ⑨:包模式初始化各文件公共限速(全一致才显示),否则保持未勾选;
            ' 保存侧按"用户是否动过"判断(见字段说明),只改路径/密码不会静默清零文件限速。
            Try
                Dim commonLimit As Integer = -1
                For Each ff As Fichero In p.ListaFicheros
                    If commonLimit = -1 Then
                        commonLimit = ff.LimiteVelocidad
                    ElseIf commonLimit <> ff.LimiteVelocidad Then
                        commonLimit = -2
                        Exit For
                    End If
                Next
                If commonLimit > 0 Then
                    chkLimitarVelocidad.Checked = True
                    txtLimiteVelocidad.Text = (commonLimit / 1024).ToString("0.####")
                    limiteInicialKB = commonLimit
                Else
                    chkLimitarVelocidad.Checked = False
                    txtLimiteVelocidad.Text = String.Empty
                End If
            Catch
                chkLimitarVelocidad.Checked = False
                txtLimiteVelocidad.Text = String.Empty
            End Try
            txtLimiteVelocidad.Enabled = chkLimitarVelocidad.Checked
        End If
        ' P2-6: 快照落地。保存时只有相对这份快照发生了变化才动限速,
        ' 这样"用户从没碰过"与"用户主动取消勾选"才能区分开。
        _limiteInicialActivo = chkLimitarVelocidad.Checked
        _limiteInicialKB = limiteInicialKB
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
        If t Is Nothing Then t = New ToolTip ' 复用同一实例,避免每次悬停泄漏一个 ToolTip
        t.SetToolTip(LinkLabel1, If(TypeOf (_Descarga) Is Fichero, MsgRutaFic, MsgRutaPaq))
    End Sub


    Private Sub LinkLabel1_MouseLeave(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseLeave
        If t IsNot Nothing Then t.Hide(LinkLabel1)
    End Sub

    Private Sub LinkLabel1_Click(sender As Object, e As System.EventArgs) Handles LinkLabel1.Click
        MessageBox.Show(If(TypeOf (_Descarga) Is Fichero, MsgRutaFic, MsgRutaPaq), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub



    Private Sub LinkLabel2_MouseHover(sender As Object, e As System.EventArgs) Handles LinkLabel2.MouseHover
        If t Is Nothing Then t = New ToolTip ' 复用同一实例,避免每次悬停泄漏一个 ToolTip
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

        ' P1-2 同 Configuration:MB 小数须按 Double 解析,否则 0.5 存不进。
        Dim LimiteVelocidadMB As Double = 0
        Double.TryParse(txtLimiteVelocidad.Text, LimiteVelocidadMB)
        Dim LimiteVelocidad As Integer = 0
        If Not chkLimitarVelocidad.Checked Then
            LimiteVelocidad = 0
        ElseIf LimiteVelocidadMB <= 0 Or String.IsNullOrEmpty(txtLimiteVelocidad.Text) Then
            MessageBox.Show(Language.GetText("Invalid speed limit"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        Else
            Dim asLong As Long = CLng(Math.Round(LimiteVelocidadMB * 1024.0))
            If asLong <= 0 Then
                MessageBox.Show(Language.GetText("Invalid speed limit"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Exit Sub
            End If
            If asLong > Integer.MaxValue Then asLong = Integer.MaxValue
            LimiteVelocidad = CInt(asLong)
        End If

        ' P2-6: 判断"用户是否动过限速",而不是只看复选框勾没勾。
        ' 包模式下"未勾选"有两种含义:用户从没碰过(包内各文件限速不一致,载入时
        ' 就不勾),或者用户主动取消了勾选。之前只按 Checked 判断,后者被当成前者 →
        ' 取消勾选保存后限速纹丝不动,重开属性窗口复选框又变回勾选。
        Dim limiteCambiado As Boolean = (chkLimitarVelocidad.Checked <> _limiteInicialActivo) OrElse _
                                       (chkLimitarVelocidad.Checked AndAlso LimiteVelocidad <> _limiteInicialKB)


        If TypeOf (_Descarga) Is Fichero Then
            Dim f As Fichero = CType(_Descarga, Fichero)
            If txtRuta.Text <> f.RutaLocal And Not f.DescargaComenzada Then
                ' N-P2-4: the package branch below validates the path (CreateDirectory
                ' with a friendly "Invalid directory" error); the single-file branch
                ' accepted anything — including an emptied box — and the download only
                ' failed later with a cryptic IO exception. Align both branches.
                If String.IsNullOrWhiteSpace(txtRuta.Text) Then
                    MessageBox.Show(Language.GetText("Invalid directory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Exit Sub
                End If
                Try
                    If Not System.IO.Directory.Exists(txtRuta.Text) Then
                        System.IO.Directory.CreateDirectory(txtRuta.Text)
                    End If
                Catch ex As Exception
                    Log.WriteError("PropiedadesDescarga: invalid single-file path: " & Log.SafeException(ex))
                    MessageBox.Show(Language.GetText("Invalid directory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Exit Sub
                End Try
                f.RutaLocal = txtRuta.Text
            End If
            f.SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
            ' P2-6: 同样按"用户是否动过"判断,避免没碰限速时被回写
            ' (回写会因 MB→KB 的四舍五入产生微小漂移)。取消勾选 = 清除限速。
            If limiteCambiado Then
                f.LimiteVelocidad = LimiteVelocidad
                ' ③:单文件限速同为 KB,控制器要字节。
                ThrottledStreamController.GetController.SetMaxSpeed(f.FileID, CLng(LimiteVelocidad) * 1024L)
            End If
        Else
            Dim p As Paquete = CType(_Descarga, Paquete)

            Dim newRoot As String = txtRuta.Text
            Dim rootChanged As Boolean = (newRoot <> p.RutaLocal)
            If rootChanged Then
                ' 无效路径直接拒收,不再把垃圾写进队列(文案沿用 AddLinks 的 Invalid directory)
                Try
                    System.IO.Directory.CreateDirectory(newRoot)
                Catch ex As Exception
                    MessageBox.Show(Language.GetText("Invalid directory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Exit Sub
                End Try
            End If
            For Each f As Fichero In p.ListaFicheros
                ' 仅包根真变了才搬移未开下文件,且按 RutaRelativa 保留相对结构;
                ' 此前逐文件比 txtRuta <> f.RutaLocal,包内但凡有子目录、哪怕只改限速点确定也会被拍平到包根。
                If rootChanged And Not f.DescargaComenzada Then
                    Try
                        f.RutaLocal = PathGuard.GetSafePathUnderRoot(newRoot, If(f.RutaRelativa, String.Empty), allowRoot:=True)
                    Catch ex As Exception
                        Log.WriteError("PropiedadesDescarga: invalid relocated path, keeping old one for " & f.FileID & ": " & Log.SafeException(ex))
                    End Try
                End If
                ' P2-5:逐文件下发必须覆盖"只改密码"的情况。此前条件只比勾选状态,
                ' 于是用户只改解压密码(勾选不变)时子文件拿不到新密码 —— 包级字段更新了
                ' (:259),文件级仍是旧密码,解压时用的是文件自己的密码(Fichero.vb:686
                ' 传 Me.DescargaExtraccionPassword)→ 解压必失败。
                ' 判定条件与包级更新条件(:259)保持一致:勾选状态变了 或 密码变了 就下发。
                If p.DescargaExtraccionAutomatica <> chkUnZip.Checked Or _
                   p.DescargaExtraccionPassword <> txtPassword.Text Then
                    f.SetDescargaExtraccionAutomatica(txtPassword.Text) = chkUnZip.Checked
                End If
                ' P2-6: 只有用户动过限速才批量下发。勾选 = 统一设置;
                ' 取消勾选 = 统一清除。此前"未勾选=不覆写"让取消勾选完全无效
                ' (限速纹丝不动,重开窗口复选框又变回勾选),与单文件分支
                ' "未勾选=清零"的语义正好相反。
                If limiteCambiado Then
                    If chkLimitarVelocidad.Checked Then
                        f.LimiteVelocidad = LimiteVelocidad
                        ThrottledStreamController.GetController.SetMaxSpeed(f.FileID, CLng(LimiteVelocidad) * 1024L)
                    Else
                        f.LimiteVelocidad = 0
                        ThrottledStreamController.GetController.SetMaxSpeed(f.FileID, 0)
                    End If
                End If
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