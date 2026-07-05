Namespace Stegano
    <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
    Partial Class SteganoWizardSave
        Inherits System.Windows.Forms.Form

        'Form overrides dispose to clean up the component list.
        <System.Diagnostics.DebuggerNonUserCode()> _
        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            Try
                If disposing AndAlso components IsNot Nothing Then
                    components.Dispose()
                End If
            Finally
                MyBase.Dispose(disposing)
            End Try
        End Sub

        'Required by the Windows Form Designer
        Private components As System.ComponentModel.IContainer

        'NOTE: The following procedure is required by the Windows Form Designer
        'It can be modified using the Windows Form Designer.  
        'Do not modify it using the code editor.
        <System.Diagnostics.DebuggerStepThrough()> _
        Private Sub InitializeComponent()
            Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(SteganoWizardSave))
            Me.gbIntro = New System.Windows.Forms.GroupBox()
            Me.helpStegano = New System.Windows.Forms.LinkLabel()
            Me.lbIntro = New System.Windows.Forms.Label()
            Me.gbImage = New System.Windows.Forms.GroupBox()
            Me.btImageOutput = New System.Windows.Forms.Button()
            Me.txtImageOutput = New System.Windows.Forms.TextBox()
            Me.lbImageOutput = New System.Windows.Forms.Label()
            Me.lbImageInput = New System.Windows.Forms.Label()
            Me.btImageInput = New System.Windows.Forms.Button()
            Me.txtImageInput = New System.Windows.Forms.TextBox()
            Me.lbImage = New System.Windows.Forms.Label()
            Me.gbLinks = New System.Windows.Forms.GroupBox()
            Me.helpVisibleLinks = New System.Windows.Forms.LinkLabel()
            Me.chkVisibleLinks = New System.Windows.Forms.CheckBox()
            Me.txtLinks = New System.Windows.Forms.RichTextBox()
            Me.lbLinks = New System.Windows.Forms.Label()
            Me.gbPassword = New System.Windows.Forms.GroupBox()
            Me.txtQuality = New System.Windows.Forms.TextBox()
            Me.lbQuality = New System.Windows.Forms.Label()
            Me.lbPassword = New System.Windows.Forms.Label()
            Me.lbPasswordTxt = New System.Windows.Forms.Label()
            Me.txtPassword = New System.Windows.Forms.TextBox()
            Me.btSave = New System.Windows.Forms.Button()
            Me.btCancel = New System.Windows.Forms.Button()
            Me.gbIntro.SuspendLayout()
            Me.gbImage.SuspendLayout()
            Me.gbLinks.SuspendLayout()
            Me.gbPassword.SuspendLayout()
            Me.SuspendLayout()
            '
            'gbIntro
            '
            Me.gbIntro.Controls.Add(Me.helpStegano)
            Me.gbIntro.Controls.Add(Me.lbIntro)
            Me.gbIntro.Location = New System.Drawing.Point(12, 12)
            Me.gbIntro.Name = "gbIntro"
            Me.gbIntro.Size = New System.Drawing.Size(489, 187)
            Me.gbIntro.TabIndex = 0
            Me.gbIntro.TabStop = False
            Me.gbIntro.Text = "Introduction"
            '
            'helpStegano
            '
            Me.helpStegano.AutoSize = True
            Me.helpStegano.Location = New System.Drawing.Point(6, 157)
            Me.helpStegano.Name = "helpStegano"
            Me.helpStegano.Size = New System.Drawing.Size(151, 13)
            Me.helpStegano.TabIndex = 1
            Me.helpStegano.TabStop = True
            Me.helpStegano.Text = "For more information click here"
            '
            'lbIntro
            '
            Me.lbIntro.AutoSize = True
            Me.lbIntro.Location = New System.Drawing.Point(6, 28)
            Me.lbIntro.MaximumSize = New System.Drawing.Size(480, 0)
            Me.lbIntro.Name = "lbIntro"
            Me.lbIntro.Size = New System.Drawing.Size(470, 117)
            Me.lbIntro.TabIndex = 0
            Me.lbIntro.TabStop = True
            Me.lbIntro.Text = resources.GetString("lbIntro.Text")
            '
            'gbImage
            '
            Me.gbImage.Controls.Add(Me.btImageOutput)
            Me.gbImage.Controls.Add(Me.txtImageOutput)
            Me.gbImage.Controls.Add(Me.lbImageOutput)
            Me.gbImage.Controls.Add(Me.lbImageInput)
            Me.gbImage.Controls.Add(Me.btImageInput)
            Me.gbImage.Controls.Add(Me.txtImageInput)
            Me.gbImage.Controls.Add(Me.lbImage)
            Me.gbImage.Location = New System.Drawing.Point(12, 346)
            Me.gbImage.Name = "gbImage"
            Me.gbImage.Size = New System.Drawing.Size(489, 218)
            Me.gbImage.TabIndex = 1
            Me.gbImage.TabStop = False
            Me.gbImage.Text = "Image to hide"
            '
            'btImageOutput
            '
            Me.btImageOutput.AllowDrop = True
            Me.btImageOutput.Location = New System.Drawing.Point(391, 177)
            Me.btImageOutput.Name = "btImageOutput"
            Me.btImageOutput.Size = New System.Drawing.Size(85, 23)
            Me.btImageOutput.TabIndex = 7
            Me.btImageOutput.Text = "Browse"
            Me.btImageOutput.UseVisualStyleBackColor = True
            '
            'txtImageOutput
            '
            Me.txtImageOutput.AllowDrop = True
            Me.txtImageOutput.Location = New System.Drawing.Point(15, 179)
            Me.txtImageOutput.Name = "txtImageOutput"
            Me.txtImageOutput.Size = New System.Drawing.Size(370, 20)
            Me.txtImageOutput.TabIndex = 6
            '
            'lbImageOutput
            '
            Me.lbImageOutput.AutoSize = True
            Me.lbImageOutput.Location = New System.Drawing.Point(5, 152)
            Me.lbImageOutput.Name = "lbImageOutput"
            Me.lbImageOutput.Size = New System.Drawing.Size(73, 13)
            Me.lbImageOutput.TabIndex = 5
            Me.lbImageOutput.Text = "Output image:"
            '
            'lbImageInput
            '
            Me.lbImageInput.AutoSize = True
            Me.lbImageInput.Location = New System.Drawing.Point(5, 91)
            Me.lbImageInput.Name = "lbImageInput"
            Me.lbImageInput.Size = New System.Drawing.Size(65, 13)
            Me.lbImageInput.TabIndex = 4
            Me.lbImageInput.Text = "Input image:"
            '
            'btImageInput
            '
            Me.btImageInput.Location = New System.Drawing.Point(391, 116)
            Me.btImageInput.Name = "btImageInput"
            Me.btImageInput.Size = New System.Drawing.Size(85, 23)
            Me.btImageInput.TabIndex = 3
            Me.btImageInput.Text = "Browse"
            Me.btImageInput.UseVisualStyleBackColor = True
            '
            'txtImageInput
            '
            Me.txtImageInput.Location = New System.Drawing.Point(15, 118)
            Me.txtImageInput.Name = "txtImageInput"
            Me.txtImageInput.Size = New System.Drawing.Size(370, 20)
            Me.txtImageInput.TabIndex = 2
            '
            'lbImage
            '
            Me.lbImage.AutoSize = True
            Me.lbImage.Location = New System.Drawing.Point(6, 32)
            Me.lbImage.MaximumSize = New System.Drawing.Size(480, 0)
            Me.lbImage.Name = "lbImage"
            Me.lbImage.Size = New System.Drawing.Size(455, 52)
            Me.lbImage.TabIndex = 1
            Me.lbImage.Text = "Select an existing image as ""Input image"", and then select or create a new JPEG i" & _
        "mage as the ""Output image""." & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "The input image can be an existing file or an image" & _
        " URL." & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10)
            '
            'gbLinks
            '
            Me.gbLinks.Controls.Add(Me.helpVisibleLinks)
            Me.gbLinks.Controls.Add(Me.chkVisibleLinks)
            Me.gbLinks.Controls.Add(Me.txtLinks)
            Me.gbLinks.Controls.Add(Me.lbLinks)
            Me.gbLinks.Location = New System.Drawing.Point(12, 205)
            Me.gbLinks.Name = "gbLinks"
            Me.gbLinks.Size = New System.Drawing.Size(489, 135)
            Me.gbLinks.TabIndex = 2
            Me.gbLinks.TabStop = False
            Me.gbLinks.Text = "Links"
            '
            'helpVisibleLinks
            '
            Me.helpVisibleLinks.AutoSize = True
            Me.helpVisibleLinks.Location = New System.Drawing.Point(457, 27)
            Me.helpVisibleLinks.Name = "helpVisibleLinks"
            Me.helpVisibleLinks.Size = New System.Drawing.Size(19, 13)
            Me.helpVisibleLinks.TabIndex = 7
            Me.helpVisibleLinks.TabStop = True
            Me.helpVisibleLinks.Text = "[?]"
            '
            'chkVisibleLinks
            '
            Me.chkVisibleLinks.AutoSize = True
            Me.chkVisibleLinks.Location = New System.Drawing.Point(301, 27)
            Me.chkVisibleLinks.MinimumSize = New System.Drawing.Size(150, 0)
            Me.chkVisibleLinks.Name = "chkVisibleLinks"
            Me.chkVisibleLinks.RightToLeft = System.Windows.Forms.RightToLeft.Yes
            Me.chkVisibleLinks.Size = New System.Drawing.Size(150, 17)
            Me.chkVisibleLinks.TabIndex = 6
            Me.chkVisibleLinks.Text = "Visible links"
            Me.chkVisibleLinks.UseVisualStyleBackColor = True
            '
            'txtLinks
            '
            Me.txtLinks.Location = New System.Drawing.Point(9, 56)
            Me.txtLinks.Name = "txtLinks"
            Me.txtLinks.Size = New System.Drawing.Size(467, 68)
            Me.txtLinks.TabIndex = 5
            Me.txtLinks.Text = ""
            '
            'lbLinks
            '
            Me.lbLinks.AutoSize = True
            Me.lbLinks.Location = New System.Drawing.Point(6, 27)
            Me.lbLinks.Name = "lbLinks"
            Me.lbLinks.Size = New System.Drawing.Size(214, 13)
            Me.lbLinks.TabIndex = 4
            Me.lbLinks.Text = "Enter one or more MEGA links to be hidden." & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10)
            '
            'gbPassword
            '
            Me.gbPassword.Controls.Add(Me.txtQuality)
            Me.gbPassword.Controls.Add(Me.lbQuality)
            Me.gbPassword.Controls.Add(Me.lbPassword)
            Me.gbPassword.Controls.Add(Me.lbPasswordTxt)
            Me.gbPassword.Controls.Add(Me.txtPassword)
            Me.gbPassword.Location = New System.Drawing.Point(12, 570)
            Me.gbPassword.Name = "gbPassword"
            Me.gbPassword.Size = New System.Drawing.Size(489, 117)
            Me.gbPassword.TabIndex = 3
            Me.gbPassword.TabStop = False
            Me.gbPassword.Text = "Other options"
            '
            'txtQuality
            '
            Me.txtQuality.Location = New System.Drawing.Point(346, 74)
            Me.txtQuality.Name = "txtQuality"
            Me.txtQuality.Size = New System.Drawing.Size(25, 20)
            Me.txtQuality.TabIndex = 10
            Me.txtQuality.Text = "85"
            '
            'lbQuality
            '
            Me.lbQuality.AutoSize = True
            Me.lbQuality.Location = New System.Drawing.Point(250, 77)
            Me.lbQuality.MaximumSize = New System.Drawing.Size(90, 0)
            Me.lbQuality.MinimumSize = New System.Drawing.Size(90, 0)
            Me.lbQuality.Name = "lbQuality"
            Me.lbQuality.Size = New System.Drawing.Size(90, 13)
            Me.lbQuality.TabIndex = 9
            Me.lbQuality.Text = "JPEG Quality:"
            Me.lbQuality.TextAlign = System.Drawing.ContentAlignment.TopRight
            '
            'lbPassword
            '
            Me.lbPassword.AutoSize = True
            Me.lbPassword.Location = New System.Drawing.Point(6, 29)
            Me.lbPassword.MaximumSize = New System.Drawing.Size(480, 0)
            Me.lbPassword.Name = "lbPassword"
            Me.lbPassword.Size = New System.Drawing.Size(415, 13)
            Me.lbPassword.TabIndex = 8
            Me.lbPassword.Text = "Optionally, you can specify the JPEG quality and a password. If not, just leave i" & _
        "t empty."
            '
            'lbPasswordTxt
            '
            Me.lbPasswordTxt.AutoSize = True
            Me.lbPasswordTxt.Location = New System.Drawing.Point(7, 77)
            Me.lbPasswordTxt.MaximumSize = New System.Drawing.Size(90, 0)
            Me.lbPasswordTxt.MinimumSize = New System.Drawing.Size(90, 0)
            Me.lbPasswordTxt.Name = "lbPasswordTxt"
            Me.lbPasswordTxt.Size = New System.Drawing.Size(90, 13)
            Me.lbPasswordTxt.TabIndex = 8
            Me.lbPasswordTxt.Text = "Password:"
            Me.lbPasswordTxt.TextAlign = System.Drawing.ContentAlignment.TopRight
            '
            'txtPassword
            '
            Me.txtPassword.Location = New System.Drawing.Point(103, 74)
            Me.txtPassword.MaxLength = 256
            Me.txtPassword.Name = "txtPassword"
            Me.txtPassword.PasswordChar = Global.Microsoft.VisualBasic.ChrW(42)
            Me.txtPassword.Size = New System.Drawing.Size(141, 20)
            Me.txtPassword.TabIndex = 8
            '
            'btSave
            '
            Me.btSave.Location = New System.Drawing.Point(12, 693)
            Me.btSave.Name = "btSave"
            Me.btSave.Size = New System.Drawing.Size(85, 23)
            Me.btSave.TabIndex = 8
            Me.btSave.Text = "Save"
            Me.btSave.UseVisualStyleBackColor = True
            '
            'btCancel
            '
            Me.btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
            Me.btCancel.Location = New System.Drawing.Point(416, 693)
            Me.btCancel.Name = "btCancel"
            Me.btCancel.Size = New System.Drawing.Size(85, 23)
            Me.btCancel.TabIndex = 9
            Me.btCancel.Text = "Cancel"
            Me.btCancel.UseVisualStyleBackColor = True
            '
            'SteganoWizardSave
            '
            Me.AcceptButton = Me.btSave
            Me.AllowDrop = True
            Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
            Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
            Me.CancelButton = Me.btCancel
            Me.ClientSize = New System.Drawing.Size(513, 726)
            Me.Controls.Add(Me.btCancel)
            Me.Controls.Add(Me.btSave)
            Me.Controls.Add(Me.gbPassword)
            Me.Controls.Add(Me.gbLinks)
            Me.Controls.Add(Me.gbImage)
            Me.Controls.Add(Me.gbIntro)
            Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
            Me.HelpButton = True
            Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.MaximizeBox = false
        Me.MinimizeBox = false
        Me.Name = "SteganoWizardSave"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "SteganoWizard"
        Me.gbIntro.ResumeLayout(false)
        Me.gbIntro.PerformLayout
        Me.gbImage.ResumeLayout(false)
        Me.gbImage.PerformLayout
        Me.gbLinks.ResumeLayout(false)
        Me.gbLinks.PerformLayout
        Me.gbPassword.ResumeLayout(false)
        Me.gbPassword.PerformLayout
        Me.ResumeLayout(false)

End Sub
        Friend WithEvents gbIntro As System.Windows.Forms.GroupBox
        Friend WithEvents lbIntro As System.Windows.Forms.Label
        Friend WithEvents gbImage As System.Windows.Forms.GroupBox
        Friend WithEvents lbImage As System.Windows.Forms.Label
        Friend WithEvents btImageOutput As System.Windows.Forms.Button
        Friend WithEvents txtImageOutput As System.Windows.Forms.TextBox
        Friend WithEvents lbImageOutput As System.Windows.Forms.Label
        Friend WithEvents lbImageInput As System.Windows.Forms.Label
        Friend WithEvents btImageInput As System.Windows.Forms.Button
        Friend WithEvents txtImageInput As System.Windows.Forms.TextBox
        Friend WithEvents gbLinks As System.Windows.Forms.GroupBox
        Friend WithEvents lbLinks As System.Windows.Forms.Label
        Friend WithEvents gbPassword As System.Windows.Forms.GroupBox
        Friend WithEvents lbPassword As System.Windows.Forms.Label
        Friend WithEvents lbPasswordTxt As System.Windows.Forms.Label
        Friend WithEvents txtPassword As System.Windows.Forms.TextBox
        Friend WithEvents btSave As System.Windows.Forms.Button
        Friend WithEvents btCancel As System.Windows.Forms.Button
        Friend WithEvents txtQuality As System.Windows.Forms.TextBox
        Friend WithEvents lbQuality As System.Windows.Forms.Label
        Friend WithEvents txtLinks As System.Windows.Forms.RichTextBox
        Friend WithEvents chkVisibleLinks As System.Windows.Forms.CheckBox
        Friend WithEvents helpVisibleLinks As System.Windows.Forms.LinkLabel
        Friend WithEvents helpStegano As System.Windows.Forms.LinkLabel
    End Class

End Namespace
