Namespace Stegano
    <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
    Partial Class SteganoWizardLoad
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
            Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(SteganoWizardLoad))
            Me.gbIntro = New System.Windows.Forms.GroupBox()
            Me.lbIntro = New System.Windows.Forms.Label()
            Me.gbImage = New System.Windows.Forms.GroupBox()
            Me.lbImageInput = New System.Windows.Forms.Label()
            Me.btImageInput = New System.Windows.Forms.Button()
            Me.txtImageInput = New System.Windows.Forms.TextBox()
            Me.lbImage = New System.Windows.Forms.Label()
            Me.gbPassword = New System.Windows.Forms.GroupBox()
            Me.lbPassword = New System.Windows.Forms.Label()
            Me.lbPasswordTxt = New System.Windows.Forms.Label()
            Me.txtPassword = New System.Windows.Forms.TextBox()
            Me.btLoad = New System.Windows.Forms.Button()
            Me.btCancel = New System.Windows.Forms.Button()
            Me.gbIntro.SuspendLayout()
            Me.gbImage.SuspendLayout()
            Me.gbPassword.SuspendLayout()
            Me.SuspendLayout()
            '
            'gbIntro
            '
            Me.gbIntro.Controls.Add(Me.lbIntro)
            Me.gbIntro.Location = New System.Drawing.Point(12, 12)
            Me.gbIntro.Name = "gbIntro"
            Me.gbIntro.Size = New System.Drawing.Size(489, 199)
            Me.gbIntro.TabIndex = 0
            Me.gbIntro.TabStop = False
            Me.gbIntro.Text = "Introduction"
            '
            'lbIntro
            '
            Me.lbIntro.AutoSize = True
            Me.lbIntro.Location = New System.Drawing.Point(6, 28)
            Me.lbIntro.MaximumSize = New System.Drawing.Size(480, 0)
            Me.lbIntro.Name = "lbIntro"
            Me.lbIntro.Size = New System.Drawing.Size(470, 143)
            Me.lbIntro.TabIndex = 0
            Me.lbIntro.Text = resources.GetString("lbIntro.Text")
            '
            'gbImage
            '
            Me.gbImage.Controls.Add(Me.lbImageInput)
            Me.gbImage.Controls.Add(Me.btImageInput)
            Me.gbImage.Controls.Add(Me.txtImageInput)
            Me.gbImage.Controls.Add(Me.lbImage)
            Me.gbImage.Location = New System.Drawing.Point(12, 217)
            Me.gbImage.Name = "gbImage"
            Me.gbImage.Size = New System.Drawing.Size(489, 126)
            Me.gbImage.TabIndex = 1
            Me.gbImage.TabStop = False
            Me.gbImage.Text = "Image to load"
            '
            'lbImageInput
            '
            Me.lbImageInput.AutoSize = True
            Me.lbImageInput.Location = New System.Drawing.Point(6, 64)
            Me.lbImageInput.Name = "lbImageInput"
            Me.lbImageInput.Size = New System.Drawing.Size(65, 13)
            Me.lbImageInput.TabIndex = 4
            Me.lbImageInput.Text = "Input image:"
            '
            'btImageInput
            '
            Me.btImageInput.Location = New System.Drawing.Point(392, 89)
            Me.btImageInput.Name = "btImageInput"
            Me.btImageInput.Size = New System.Drawing.Size(85, 23)
            Me.btImageInput.TabIndex = 3
            Me.btImageInput.Text = "Browse"
            Me.btImageInput.UseVisualStyleBackColor = True
            '
            'txtImageInput
            '
            Me.txtImageInput.Location = New System.Drawing.Point(16, 91)
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
            Me.lbImage.Size = New System.Drawing.Size(322, 13)
            Me.lbImage.TabIndex = 1
            Me.lbImage.Text = "Select an existing image as ""Input image"", or enter the image URL."
            '
            'gbPassword
            '
            Me.gbPassword.Controls.Add(Me.lbPassword)
            Me.gbPassword.Controls.Add(Me.lbPasswordTxt)
            Me.gbPassword.Controls.Add(Me.txtPassword)
            Me.gbPassword.Location = New System.Drawing.Point(12, 349)
            Me.gbPassword.Name = "gbPassword"
            Me.gbPassword.Size = New System.Drawing.Size(489, 104)
            Me.gbPassword.TabIndex = 3
            Me.gbPassword.TabStop = False
            Me.gbPassword.Text = "Other options"
            '
            'lbPassword
            '
            Me.lbPassword.AutoSize = True
            Me.lbPassword.Location = New System.Drawing.Point(6, 29)
            Me.lbPassword.MaximumSize = New System.Drawing.Size(480, 0)
            Me.lbPassword.Name = "lbPassword"
            Me.lbPassword.Size = New System.Drawing.Size(300, 13)
            Me.lbPassword.TabIndex = 8
            Me.lbPassword.Text = "If the image has a password, enter it. If not, just leave it empty."
            '
            'lbPasswordTxt
            '
            Me.lbPasswordTxt.AutoSize = True
            Me.lbPasswordTxt.Location = New System.Drawing.Point(91, 64)
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
            Me.txtPassword.Location = New System.Drawing.Point(187, 61)
            Me.txtPassword.MaxLength = 256
            Me.txtPassword.Name = "txtPassword"
            Me.txtPassword.PasswordChar = Global.Microsoft.VisualBasic.ChrW(42)
            Me.txtPassword.Size = New System.Drawing.Size(141, 20)
            Me.txtPassword.TabIndex = 8
            '
            'btLoad
            '
            Me.btLoad.Location = New System.Drawing.Point(12, 459)
            Me.btLoad.Name = "btLoad"
            Me.btLoad.Size = New System.Drawing.Size(85, 25)
            Me.btLoad.TabIndex = 8
            Me.btLoad.Text = "Load"
            Me.btLoad.UseVisualStyleBackColor = True
            '
            'btCancel
            '
            Me.btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
            Me.btCancel.Location = New System.Drawing.Point(416, 459)
            Me.btCancel.Name = "btCancel"
            Me.btCancel.Size = New System.Drawing.Size(85, 25)
            Me.btCancel.TabIndex = 9
            Me.btCancel.Text = "Cancel"
            Me.btCancel.UseVisualStyleBackColor = True
            '
            'SteganoWizardLoad
            '
            Me.AcceptButton = Me.btLoad
            Me.AllowDrop = True
            Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
            Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
            Me.CancelButton = Me.btCancel
            Me.ClientSize = New System.Drawing.Size(512, 493)
            Me.Controls.Add(Me.btCancel)
            Me.Controls.Add(Me.btLoad)
            Me.Controls.Add(Me.gbPassword)
            Me.Controls.Add(Me.gbImage)
            Me.Controls.Add(Me.gbIntro)
            Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
            Me.HelpButton = True
            Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.Name = "SteganoWizardLoad"
            Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
            Me.Text = "SteganoWizard"
            Me.gbIntro.ResumeLayout(False)
            Me.gbIntro.PerformLayout()
            Me.gbImage.ResumeLayout(False)
            Me.gbImage.PerformLayout()
            Me.gbPassword.ResumeLayout(False)
            Me.gbPassword.PerformLayout()
            Me.ResumeLayout(False)

        End Sub
        Friend WithEvents gbIntro As System.Windows.Forms.GroupBox
        Friend WithEvents lbIntro As System.Windows.Forms.Label
        Friend WithEvents gbImage As System.Windows.Forms.GroupBox
        Friend WithEvents lbImage As System.Windows.Forms.Label
        Friend WithEvents lbImageInput As System.Windows.Forms.Label
        Friend WithEvents btImageInput As System.Windows.Forms.Button
        Friend WithEvents txtImageInput As System.Windows.Forms.TextBox
        Friend WithEvents gbPassword As System.Windows.Forms.GroupBox
        Friend WithEvents lbPassword As System.Windows.Forms.Label
        Friend WithEvents lbPasswordTxt As System.Windows.Forms.Label
        Friend WithEvents txtPassword As System.Windows.Forms.TextBox
        Friend WithEvents btLoad As System.Windows.Forms.Button
        Friend WithEvents btCancel As System.Windows.Forms.Button
    End Class

End Namespace
