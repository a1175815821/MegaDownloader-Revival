using System;
using System.Diagnostics;

namespace MegaDownloader.Stegano
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class SteganoWizardLoad : System.Windows.Forms.Form
    {

        // Form overrides dispose to clean up the component list.
        [DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components is not null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(SteganoWizardLoad));
            gbIntro = new System.Windows.Forms.GroupBox();
            lbIntro = new System.Windows.Forms.Label();
            gbImage = new System.Windows.Forms.GroupBox();
            lbImageInput = new System.Windows.Forms.Label();
            btImageInput = new System.Windows.Forms.Button();
            btImageInput.Click += new EventHandler(btImageInput_Click);
            btImageInput.DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop);
            btImageInput.DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            txtImageInput = new System.Windows.Forms.TextBox();
            txtImageInput.DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop);
            txtImageInput.DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            lbImage = new System.Windows.Forms.Label();
            gbPassword = new System.Windows.Forms.GroupBox();
            lbPassword = new System.Windows.Forms.Label();
            lbPasswordTxt = new System.Windows.Forms.Label();
            txtPassword = new System.Windows.Forms.TextBox();
            btLoad = new System.Windows.Forms.Button();
            btLoad.Click += new EventHandler(btLoad_Click);
            btCancel = new System.Windows.Forms.Button();
            btCancel.Click += new EventHandler(btCancel_Click);
            gbIntro.SuspendLayout();
            gbImage.SuspendLayout();
            gbPassword.SuspendLayout();
            SuspendLayout();
            // 
            // gbIntro
            // 
            gbIntro.Controls.Add(lbIntro);
            gbIntro.Location = new System.Drawing.Point(12, 12);
            gbIntro.Name = "gbIntro";
            gbIntro.Size = new System.Drawing.Size(489, 199);
            gbIntro.TabIndex = 0;
            gbIntro.TabStop = false;
            gbIntro.Text = "Introduction";
            // 
            // lbIntro
            // 
            lbIntro.AutoSize = true;
            lbIntro.Location = new System.Drawing.Point(6, 28);
            lbIntro.MaximumSize = new System.Drawing.Size(480, 0);
            lbIntro.Name = "lbIntro";
            lbIntro.Size = new System.Drawing.Size(470, 143);
            lbIntro.TabIndex = 0;
            lbIntro.Text = resources.GetString("lbIntro.Text");
            // 
            // gbImage
            // 
            gbImage.Controls.Add(lbImageInput);
            gbImage.Controls.Add(btImageInput);
            gbImage.Controls.Add(txtImageInput);
            gbImage.Controls.Add(lbImage);
            gbImage.Location = new System.Drawing.Point(12, 217);
            gbImage.Name = "gbImage";
            gbImage.Size = new System.Drawing.Size(489, 126);
            gbImage.TabIndex = 1;
            gbImage.TabStop = false;
            gbImage.Text = "Image to load";
            // 
            // lbImageInput
            // 
            lbImageInput.AutoSize = true;
            lbImageInput.Location = new System.Drawing.Point(6, 64);
            lbImageInput.Name = "lbImageInput";
            lbImageInput.Size = new System.Drawing.Size(65, 13);
            lbImageInput.TabIndex = 4;
            lbImageInput.Text = "Input image:";
            // 
            // btImageInput
            // 
            btImageInput.AllowDrop = true; // R-P4-2: 输入框/浏览钮接拖拽
            btImageInput.Location = new System.Drawing.Point(392, 89);
            btImageInput.Name = "btImageInput";
            btImageInput.Size = new System.Drawing.Size(85, 23);
            btImageInput.TabIndex = 3;
            btImageInput.Text = "Browse";
            btImageInput.UseVisualStyleBackColor = true;
            // 
            // txtImageInput
            // 
            txtImageInput.AllowDrop = true; // R-P4-2: 输入框/浏览钮接拖拽
            txtImageInput.Location = new System.Drawing.Point(16, 91);
            txtImageInput.Name = "txtImageInput";
            txtImageInput.Size = new System.Drawing.Size(370, 20);
            txtImageInput.TabIndex = 2;
            // 
            // lbImage
            // 
            lbImage.AutoSize = true;
            lbImage.Location = new System.Drawing.Point(6, 32);
            lbImage.MaximumSize = new System.Drawing.Size(480, 0);
            lbImage.Name = "lbImage";
            lbImage.Size = new System.Drawing.Size(322, 13);
            lbImage.TabIndex = 1;
            lbImage.Text = "Select an existing image as \"Input image\", or enter the image URL.";
            // 
            // gbPassword
            // 
            gbPassword.Controls.Add(lbPassword);
            gbPassword.Controls.Add(lbPasswordTxt);
            gbPassword.Controls.Add(txtPassword);
            gbPassword.Location = new System.Drawing.Point(12, 349);
            gbPassword.Name = "gbPassword";
            gbPassword.Size = new System.Drawing.Size(489, 104);
            gbPassword.TabIndex = 3;
            gbPassword.TabStop = false;
            gbPassword.Text = "Other options";
            // 
            // lbPassword
            // 
            lbPassword.AutoSize = true;
            lbPassword.Location = new System.Drawing.Point(6, 29);
            lbPassword.MaximumSize = new System.Drawing.Size(480, 0);
            lbPassword.Name = "lbPassword";
            lbPassword.Size = new System.Drawing.Size(300, 13);
            lbPassword.TabIndex = 8;
            lbPassword.Text = "If the image has a password, enter it. If not, just leave it empty.";
            // 
            // lbPasswordTxt
            // 
            lbPasswordTxt.AutoSize = true;
            lbPasswordTxt.Location = new System.Drawing.Point(91, 64);
            lbPasswordTxt.MaximumSize = new System.Drawing.Size(90, 0);
            lbPasswordTxt.MinimumSize = new System.Drawing.Size(90, 0);
            lbPasswordTxt.Name = "lbPasswordTxt";
            lbPasswordTxt.Size = new System.Drawing.Size(90, 13);
            lbPasswordTxt.TabIndex = 8;
            lbPasswordTxt.Text = "Password:";
            lbPasswordTxt.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtPassword
            // 
            txtPassword.Location = new System.Drawing.Point(187, 61);
            txtPassword.MaxLength = 256;
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.Size = new System.Drawing.Size(141, 20);
            txtPassword.TabIndex = 8;
            // 
            // btLoad
            // 
            btLoad.Location = new System.Drawing.Point(12, 459);
            btLoad.Name = "btLoad";
            btLoad.Size = new System.Drawing.Size(85, 25);
            btLoad.TabIndex = 8;
            btLoad.Text = "Load";
            btLoad.UseVisualStyleBackColor = true;
            // 
            // btCancel
            // 
            btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btCancel.Location = new System.Drawing.Point(416, 459);
            btCancel.Name = "btCancel";
            btCancel.Size = new System.Drawing.Size(85, 25);
            btCancel.TabIndex = 9;
            btCancel.Text = "Cancel";
            btCancel.UseVisualStyleBackColor = true;
            // 
            // SteganoWizardLoad
            // 
            AcceptButton = btLoad;
            AllowDrop = true;
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btCancel;
            ClientSize = new System.Drawing.Size(512, 493);
            Controls.Add(btCancel);
            Controls.Add(btLoad);
            Controls.Add(gbPassword);
            Controls.Add(gbImage);
            Controls.Add(gbIntro);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            HelpButton = true;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SteganoWizardLoad";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "SteganoWizard";
            gbIntro.ResumeLayout(false);
            gbIntro.PerformLayout();
            gbImage.ResumeLayout(false);
            gbImage.PerformLayout();
            gbPassword.ResumeLayout(false);
            gbPassword.PerformLayout();
            Load += new EventHandler(ScreenLoad);
            HelpButtonClicked += new System.ComponentModel.CancelEventHandler(HelpButtonPressed);
            DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop);
            DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            ResumeLayout(false);

        }
        internal System.Windows.Forms.GroupBox gbIntro;
        internal System.Windows.Forms.Label lbIntro;
        internal System.Windows.Forms.GroupBox gbImage;
        internal System.Windows.Forms.Label lbImage;
        internal System.Windows.Forms.Label lbImageInput;
        internal System.Windows.Forms.Button btImageInput;
        internal System.Windows.Forms.TextBox txtImageInput;
        internal System.Windows.Forms.GroupBox gbPassword;
        internal System.Windows.Forms.Label lbPassword;
        internal System.Windows.Forms.Label lbPasswordTxt;
        internal System.Windows.Forms.TextBox txtPassword;
        internal System.Windows.Forms.Button btLoad;
        internal System.Windows.Forms.Button btCancel;
    }

}