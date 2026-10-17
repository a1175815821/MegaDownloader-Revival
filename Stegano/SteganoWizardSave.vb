Namespace Stegano
    Public Class SteganoWizardSave


        Public Config As Configuracion
        Public MainForm As Main

        Private Sub Translate()

            Me.Text = Language.GetText("Steganography Wizard")
            Me.gbIntro.Text = Language.GetText("Introduction")
            Me.gbImage.Text = Language.GetText("Image to load")
            Me.gbPassword.Text = Language.GetText("Other options")
            Me.btCancel.Text = Language.GetText("Cancel")
            Me.btSave.Text = Language.GetText("Save")
            Me.lbIntro.Text = Language.GetText("Steganography is the art and science ... SAVE")
            Me.lbQuality.Text = Language.GetText("JPEG Quality") & ":"
            Me.lbPasswordTxt.Text = Language.GetText("Password") & ":"
            Me.btImageInput.Text = Language.GetText("Browse")
            Me.btImageOutput.Text = Language.GetText("Browse")
            Me.lbLinks.Text = Language.GetText("Enter one or more MEGA links to be hidden")
            Me.lbImage.Text = Language.GetText("Select an existing image as Input image ...")
            Me.lbImageInput.Text = Language.GetText("Input image") & ":"
            Me.lbImageOutput.Text = Language.GetText("Output image") & ":"
            Me.lbPassword.Text = Language.GetText("Optionally, you can specify the JPEG quality and a password. If not, just leave it empty")
            Me.chkVisibleLinks.Text = Language.GetText("Visible links")
            Me.helpStegano.Text = Language.GetText("For more information about stegano click here")

        End Sub

        Private Sub ScreenLoad(sender As Object, e As System.EventArgs) Handles Me.Load

            ' Centramos la pantalla
            ' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
            Dim scr = Screen.FromPoint(Me.Location)
            Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))

            Translate()
            ThemeManager.ApplyTheme(Me)
        End Sub


        Private Sub btSave_Click(sender As Object, e As EventArgs) Handles btSave.Click
            btSave.Enabled = False
            btCancel.Enabled = False

            Dim start As Date = Now

            Try
                ' Validate all inputs
                Dim URLs As Generic.List(Of String) = URLExtractor.ExtraerURLs(txtLinks.Text)
                If URLs.Count = 0 Then
                    Throw New ApplicationException(Language.GetText("Links not valid"))
                End If

                If String.IsNullOrEmpty(txtImageInput.Text) Then
                    Throw New ApplicationException(Language.GetText("Input image not specified"))
                ElseIf String.IsNullOrEmpty(txtImageOutput.Text) Then
                    Throw New ApplicationException(Language.GetText("Output image not specified"))
                End If

                ' P1-3: 旧校验 IsValidUri 对 Windows 路径恒真(Uri 把 "C:\..." 解析成
                ' scheme=file),不存在的路径被放行,随后 CreateImage 直接炸原始
                ' InvalidCastException。改用 SteganoManager.IsValidImageSource。
                If Not SteganoManager.IsValidImageSource(txtImageInput.Text) Then
                    Throw New ApplicationException(Language.GetText("Input image does not exist"))
                End If

                ' R-P3-6: IsNumeric + CInt threw a raw OverflowException (surfaced as an
                ' English "Value was either too large or too small for an Integer."
                ' from the generic Catch) for out-of-Int32 inputs like "99999999999".
                ' TryParse is overflow-safe, trims stray spaces, and parses once
                ' instead of three times.
                Dim qualityValue As Integer = 0
                If Not Integer.TryParse(txtQuality.Text.Trim(), qualityValue) OrElse qualityValue < 10 OrElse qualityValue > 100 Then
                    Throw New ApplicationException(Language.GetText("Invalid JPEG quality"))
                End If

                Dim mng As New SteganoManager
                'mng.CreateImage(String.Join(vbNewLine, URLs.ToArray), txtImageInput.Text, txtImageOutput.Text, CInt(txtQuality.Text), txtPassword.Text)
                mng.CreateImage(If(Not chkVisibleLinks.Checked, Fichero.HIDDEN_LINK, "") & txtLinks.Text, _
                                txtImageInput.Text, _
                                txtImageOutput.Text, _
                                qualityValue, _
                                txtPassword.Text)

                MessageBox.Show(String.Format(Language.GetText("The image was created successfully in {0} ms"), Now.Subtract(start).TotalMilliseconds.ToString("F2")), Language.GetText("Save"), MessageBoxButtons.OK, MessageBoxIcon.Information)

            Catch ex As ApplicationException
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)


            Catch ex As Exception
                Log.WriteError("Error trying to generate stegano: " & ex.ToString)
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)

            End Try

            btCancel.Enabled = True
            btSave.Enabled = True
            btSave.Text = Language.GetText("Save")

        End Sub

        Private Sub btCancel_Click(sender As Object, e As EventArgs) Handles btCancel.Click
            Me.Close()
        End Sub

        Private Sub btImageInput_Click(sender As Object, e As EventArgs) Handles btImageInput.Click

            Dim Examinar As New OpenFileDialog
            Examinar.CheckFileExists = True
            Examinar.DefaultExt = "jpg"
            Examinar.Filter = Language.GetText("Images") & "|*.jpg;*.png;*.bmp;*.gif"
            Examinar.Multiselect = False

            If Examinar.ShowDialog = Windows.Forms.DialogResult.OK Then
                txtImageInput.Text = Examinar.FileName
            End If

            Examinar.Dispose()
        End Sub

        Private Sub btImageOutput_Click(sender As Object, e As EventArgs) Handles btImageOutput.Click
            Dim Examinar As New SaveFileDialog
            Examinar.CheckFileExists = False
            Examinar.DefaultExt = "jpg"
            Examinar.Filter = Language.GetText("Images") & " (*.jpg)|*.jpg"

            If Examinar.ShowDialog = Windows.Forms.DialogResult.OK Then
                ' R-P3-5: the dialog still lets the user type any extension ("out.png"),
                ' and a non-.jpg output cannot be read back (the load wizard filters
                ' *.jpg). Force .jpg deterministically instead of silently producing
                ' an unreadable image.
                Dim outPath As String = Examinar.FileName
                If Not outPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) Then
                    outPath = IO.Path.ChangeExtension(outPath, ".jpg")
                End If
                txtImageOutput.Text = outPath
            End If

            Examinar.Dispose()
        End Sub


        Private t As ToolTip
        Private Function MsgVisibleLinksHelp() As String
            Return Language.GetText("Visible links HELP")
        End Function
      

        Private Sub helpVisibleLinks_MouseHover(sender As Object, e As System.EventArgs) Handles helpVisibleLinks.MouseHover
            If t Is Nothing Then t = New ToolTip
            t.SetToolTip(helpVisibleLinks, MsgVisibleLinksHelp)
        End Sub

        Private Sub helpVisibleLinks_MouseLeave(sender As Object, e As System.EventArgs) Handles helpVisibleLinks.MouseLeave
            If t IsNot Nothing Then t.Hide(helpVisibleLinks)
        End Sub

        Private Sub helpVisibleLinks_Click(sender As Object, e As System.EventArgs) Handles helpVisibleLinks.Click
            MessageBox.Show(MsgVisibleLinksHelp, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub


        Private Sub helpStegano_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles helpStegano.LinkClicked
            Dim codIdi As String = Language.GetCurrentLanguageCode.ToUpperInvariant
            If codIdi.StartsWith("ES") Then
                System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("STEGANO_LINK_ES"))
            Else
                System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("STEGANO_LINK_EN"))
            End If
        End Sub

        Private Sub HelpButtonPressed() Handles Me.HelpButtonClicked
            helpStegano_LinkClicked(Nothing, Nothing)
        End Sub

        ' R-P4-2: txtImageInput/btImageInput 也接拖拽(此前 AllowDrop=False,拖到输入框上
        ' 光标变禁入、窗体的 DragDrop 不会触发,只有拖到窗体空白处才生效)
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

        Private Sub SteganoWizard_DragDrop2(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles txtImageOutput.DragDrop, btImageOutput.DragDrop
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
                            txtImageOutput.Text = Fichero
                            Exit Sub
                        End If
                    Next
                End If
            End If
        End Sub

        Private Sub SteganoWizard_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter, txtImageOutput.DragEnter, btImageOutput.DragEnter, txtImageInput.DragEnter, btImageInput.DragEnter
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

