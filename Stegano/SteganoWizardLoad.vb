Namespace Stegano
    Public Class SteganoWizardLoad


        Public Config As Configuracion
        Public MainForm As Main

        Private Sub Translate()

            Me.Text = Language.GetText("Steganography Wizard")
            Me.gbIntro.Text = Language.GetText("Introduction")
            Me.lbIntro.Text = Language.GetText("Steganography is the art and science ... LOAD")
            Me.lbImage.Text = Language.GetText("Select an existing image as Input image, or enter the image URL")
            Me.lbPassword.Text = Language.GetText("If the image has a password, enter it. If not, just leave it empty")
            Me.btCancel.Text = Language.GetText("Cancel")
            Me.btLoad.Text = Language.GetText("Load")
            Me.gbImage.Text = Language.GetText("Image to load")
            Me.gbPassword.Text = Language.GetText("Other options")
            Me.lbPasswordTxt.Text = Language.GetText("Password") & ":"
            Me.btImageInput.Text = Language.GetText("Browse")
            Me.lbImageInput.Text = Language.GetText("Input image") & ":"
        End Sub


        Private Sub ScreenLoad(sender As Object, e As System.EventArgs) Handles Me.Load

            ' Centramos la pantalla
            ' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
            Dim scr = Screen.FromPoint(Me.Location)
            Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))

            Translate()
            ThemeManager.ApplyTheme(Me)
        End Sub

        Private Sub btLoad_Click(sender As Object, e As EventArgs) Handles btLoad.Click
            btLoad.Enabled = False
            btCancel.Enabled = False

            'Me.Invoke(Sub()
            '              txtLinks.Text = "Working please wait"
            '          End Sub)

            Dim start As Date = Now

            Try

                If String.IsNullOrEmpty(txtImageInput.Text) Then
                    Throw New ApplicationException(Language.GetText("Input image not specified"))
                End If

                If Not System.IO.File.Exists(txtImageInput.Text) AndAlso Not IsValidUri(txtImageInput.Text) Then
                    Throw New ApplicationException(Language.GetText("Input image does not exist"))
                End If


                Dim Text As String = Nothing

                Dim mng As New SteganoManager
                If mng.LoadImages(txtImageInput.Text, txtPassword.Text, Text) Then

                    Me.Hide()
                    If (Text & "").StartsWith(Fichero.HIDDEN_LINK) Then
                        Main.ComprobarYAgregarLinks(Text.Substring(Fichero.HIDDEN_LINK.Length), False, True)
                    Else
                        Main.ComprobarYAgregarLinks(Text, False, False)
                    End If

                    Me.Close()
                    Exit Sub

                Else
                    MessageBox.Show(Language.GetText("No hidden links were found. Please check the image and the password"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                End If


            Catch ex As ApplicationException
                ' R-P2-1: restore the window before showing the error. The wizard is
                ' hidden at :62 and Main.ComprobarYAgregarLinks can throw after that
                ' (e.g. an ELC server failure while hiding the extracted links); the
                ' single-instance guard then blocked reopening the wizard, so the menu
                ' looked dead until a restart.
                RestaurarVentana()
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)


            Catch ex As Exception
                Log.WriteError("Error trying to load stegano: " & ex.ToString)
                RestaurarVentana()
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)

            End Try

            btCancel.Enabled = True
            btLoad.Enabled = True

        End Sub

        ''' <summary>R-P2-1: make the wizard visible again after an error on the hidden
        ''' code path (Me.Hide() at :62).</summary>
        Private Sub RestaurarVentana()
            Try
                If Not Me.Visible Then
                    Me.Show()
                End If
                Me.Activate()
            Catch
            End Try
        End Sub

        Public Function IsValidUri(url As String) As Boolean
            ' R-P3-4: RelativeOrAbsolute 对 "hello world" 这类字符串也返回 True,校验形同虚设;
            ' 必须限定 Absolute 且只允许 http/https/file(本地图片路径)。
            ' 照抄 SteganoWizardSave.vb:96-104(该处已硬化,本处漏改)。
            ' 修复前 :53 的 "Input image does not exist" 弹窗不可达——
            ' 用户填不存在的路径只会得到 "未找到隐藏链接,请检查图片和口令" 的误导提示。
            Dim validatedUri As Uri = Nothing
            If Not Uri.TryCreate(url, UriKind.Absolute, validatedUri) Then Return False
            Return validatedUri.Scheme = Uri.UriSchemeHttp OrElse _
                   validatedUri.Scheme = Uri.UriSchemeHttps OrElse _
                   validatedUri.Scheme = Uri.UriSchemeFile
        End Function

        Private Sub btCancel_Click(sender As Object, e As EventArgs) Handles btCancel.Click
            Me.Close()
        End Sub

        Private Sub btImageInput_Click(sender As Object, e As EventArgs) Handles btImageInput.Click

            Dim Examinar As New OpenFileDialog
            Examinar.CheckFileExists = True
            Examinar.DefaultExt = "jpg"
            Examinar.Filter = Language.GetText("Images") & "|*.jpg"
            Examinar.Multiselect = False

            If Examinar.ShowDialog = Windows.Forms.DialogResult.OK Then
                txtImageInput.Text = Examinar.FileName
            End If

            Examinar.Dispose()
        End Sub

        Private Sub HelpButtonPressed() Handles Me.HelpButtonClicked
            Dim codIdi As String = Language.GetCurrentLanguageCode.ToUpperInvariant
            If codIdi.StartsWith("ES") Then
                System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("STEGANO_LINK_ES"))
            Else
                System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("STEGANO_LINK_EN"))
            End If
        End Sub


        ' R-P4-2: txtImageInput/btImageInput 也接拖拽(与保存向导对称;此前只有窗体空白处生效)
        Private Sub SteganoWizard_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragDrop, txtImageInput.DragDrop, btImageInput.DragDrop
            If e.Data.GetData(DataFormats.FileDrop) IsNot Nothing Then
                ' File drag & drop

                Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
                Dim TodosExisten As Boolean = True
                For Each Fichero As String In ficheros
                    If Not IO.File.Exists(Fichero) OrElse Not Fichero.ToLower.EndsWith(".jpg") Then  ' Solo permitimos jpgs
                        TodosExisten = False
                    End If
                Next
                If TodosExisten Then

                    For Each Fichero As String In ficheros
                        If Fichero.ToLower.EndsWith(".jpg") Then
                            txtImageInput.Text = Fichero
                            Exit Sub
                        End If
                    Next
                End If
            End If
        End Sub

        Private Sub SteganoWizard_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter, txtImageInput.DragEnter, btImageInput.DragEnter
            If e.Data.GetData(DataFormats.FileDrop) IsNot Nothing Then
                ' File drag & drop
                Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
                Dim TodosExisten As Boolean = True
                For Each Fichero As String In ficheros
                    If Not IO.File.Exists(Fichero) OrElse Not Fichero.ToLower.EndsWith(".jpg") Then  ' Solo permitimos jpgs
                        TodosExisten = False
                    End If
                Next
                If TodosExisten Then
                    e.Effect = DragDropEffects.Copy
                End If
            End If
        End Sub

      
    End Class
End Namespace

