using System;
using System.Diagnostics;

namespace MegaDownloader.Stegano
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class SteganoWizardSave : System.Windows.Forms.Form
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(SteganoWizardSave));
            gbIntro = new System.Windows.Forms.GroupBox();
            helpStegano = new System.Windows.Forms.LinkLabel();
            helpStegano.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(helpStegano_LinkClicked);
            lbIntro = new System.Windows.Forms.Label();
            gbImage = new System.Windows.Forms.GroupBox();
            btImageOutput = new System.Windows.Forms.Button();
            btImageOutput.Click += new EventHandler(btImageOutput_Click);
            btImageOutput.DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop2);
            btImageOutput.DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            txtImageOutput = new System.Windows.Forms.TextBox();
            txtImageOutput.DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop2);
            txtImageOutput.DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            lbImageOutput = new System.Windows.Forms.Label();
            lbImageInput = new System.Windows.Forms.Label();
            btImageInput = new System.Windows.Forms.Button();
            btImageInput.Click += new EventHandler(btImageInput_Click);
            btImageInput.DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop);
            btImageInput.DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            txtImageInput = new System.Windows.Forms.TextBox();
            txtImageInput.DragDrop += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragDrop);
            txtImageInput.DragEnter += new System.Windows.Forms.DragEventHandler(SteganoWizard_DragEnter);
            lbImage = new System.Windows.Forms.Label();
            gbLinks = new System.Windows.Forms.GroupBox();
            helpVisibleLinks = new System.Windows.Forms.LinkLabel();
            helpVisibleLinks.MouseHover += new EventHandler(helpVisibleLinks_MouseHover);
            helpVisibleLinks.MouseLeave += new EventHandler(helpVisibleLinks_MouseLeave);
            helpVisibleLinks.Click += new EventHandler(helpVisibleLinks_Click);
            chkVisibleLinks = new System.Windows.Forms.CheckBox();
            txtLinks = new System.Windows.Forms.RichTextBox();
            lbLinks = new System.Windows.Forms.Label();
            gbPassword = new System.Windows.Forms.GroupBox();
            txtQuality = new System.Windows.Forms.TextBox();
            lbQuality = new System.Windows.Forms.Label();
            lbPassword = new System.Windows.Forms.Label();
            lbPasswordTxt = new System.Windows.Forms.Label();
            txtPassword = new System.Windows.Forms.TextBox();
            btSave = new System.Windows.Forms.Button();
            btSave.Click += new EventHandler(btSave_Click);
            btCancel = new System.Windows.Forms.Button();
            btCancel.Click += new EventHandler(btCancel_Click);
            gbIntro.SuspendLayout();
            gbImage.SuspendLayout();
            gbLinks.SuspendLayout();
            gbPassword.SuspendLayout();
            SuspendLayout();
            // 
            // gbIntro
            // 
            gbIntro.Controls.Add(helpStegano);
            gbIntro.Controls.Add(lbIntro);
            gbIntro.Location = new System.Drawing.Point(12, 12);
            gbIntro.Name = "gbIntro";
            gbIntro.Size = new System.Drawing.Size(489, 187);
            gbIntro.TabIndex = 0;
            gbIntro.TabStop = false;
            gbIntro.Text = "Introduction";
            // 
            // helpStegano
            // 
            helpStegano.AutoSize = true;
            helpStegano.Location = new System.Drawing.Point(6, 157);
            helpStegano.Name = "helpStegano";
            helpStegano.Size = new System.Drawing.Size(151, 13);
            helpStegano.TabIndex = 1;
            helpStegano.TabStop = true;
            helpStegano.Text = "For more information click here";
            // 
            // lbIntro
            // 
            lbIntro.AutoSize = true;
            lbIntro.Location = new System.Drawing.Point(6, 28);
            lbIntro.MaximumSize = new System.Drawing.Size(480, 0);
            lbIntro.Name = "lbIntro";
            lbIntro.Size = new System.Drawing.Size(470, 117);
            lbIntro.TabIndex = 0;
            lbIntro.TabStop = true;
            lbIntro.Text = resources.GetString("lbIntro.Text");
            // 
            // gbImage
            // 
            gbImage.Controls.Add(btImageOutput);
            gbImage.Controls.Add(txtImageOutput);
            gbImage.Controls.Add(lbImageOutput);
            gbImage.Controls.Add(lbImageInput);
            gbImage.Controls.Add(btImageInput);
            gbImage.Controls.Add(txtImageInput);
            gbImage.Controls.Add(lbImage);
            gbImage.Location = new System.Drawing.Point(12, 346);
            gbImage.Name = "gbImage";
            gbImage.Size = new System.Drawing.Size(489, 218);
            gbImage.TabIndex = 1;
            gbImage.TabStop = false;
            gbImage.Text = "Image to hide";
            // 
            // btImageOutput
            // 
            btImageOutput.AllowDrop = true;
            btImageOutput.Location = new System.Drawing.Point(391, 177);
            btImageOutput.Name = "btImageOutput";
            btImageOutput.Size = new System.Drawing.Size(85, 23);
            btImageOutput.TabIndex = 7;
            btImageOutput.Text = "Browse";
            btImageOutput.UseVisualStyleBackColor = true;
            // 
            // txtImageOutput
            // 
            txtImageOutput.AllowDrop = true;
            txtImageOutput.Location = new System.Drawing.Point(15, 179);
            txtImageOutput.Name = "txtImageOutput";
            txtImageOutput.Size = new System.Drawing.Size(370, 20);
            txtImageOutput.TabIndex = 6;
            // 
            // lbImageOutput
            // 
            lbImageOutput.AutoSize = true;
            lbImageOutput.Location = new System.Drawing.Point(5, 152);
            lbImageOutput.Name = "lbImageOutput";
            lbImageOutput.Size = new System.Drawing.Size(73, 13);
            lbImageOutput.TabIndex = 5;
            lbImageOutput.Text = "Output image:";
            // 
            // lbImageInput
            // 
            lbImageInput.AutoSize = true;
            lbImageInput.Location = new System.Drawing.Point(5, 91);
            lbImageInput.Name = "lbImageInput";
            lbImageInput.Size = new System.Drawing.Size(65, 13);
            lbImageInput.TabIndex = 4;
            lbImageInput.Text = "Input image:";
            // 
            // btImageInput
            // 
            btImageInput.AllowDrop = true; // R-P4-2: 输入框/浏览钮接拖拽(与输出侧对称)
            btImageInput.Location = new System.Drawing.Point(391, 116);
            btImageInput.Name = "btImageInput";
            btImageInput.Size = new System.Drawing.Size(85, 23);
            btImageInput.TabIndex = 3;
            btImageInput.Text = "Browse";
            btImageInput.UseVisualStyleBackColor = true;
            // 
            // txtImageInput
            // 
            txtImageInput.AllowDrop = true; // R-P4-2: 输入框/浏览钮接拖拽(与输出侧对称)
            txtImageInput.Location = new System.Drawing.Point(15, 118);
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
            lbImage.Size = new System.Drawing.Size(455, 52);
            lbImage.TabIndex = 1;
            lbImage.Text = "Select an existing image as \"Input image\", and then select or create a new JPEG i" + "mage as the \"Output image\"." + '\r' + '\n' + "The input image can be an existing file or an image" + " URL." + '\r' + '\n' + '\r' + '\n';

            // 
            // gbLinks
            // 
            gbLinks.Controls.Add(helpVisibleLinks);
            gbLinks.Controls.Add(chkVisibleLinks);
            gbLinks.Controls.Add(txtLinks);
            gbLinks.Controls.Add(lbLinks);
            gbLinks.Location = new System.Drawing.Point(12, 205);
            gbLinks.Name = "gbLinks";
            gbLinks.Size = new System.Drawing.Size(489, 135);
            gbLinks.TabIndex = 2;
            gbLinks.TabStop = false;
            gbLinks.Text = "Links";
            // 
            // helpVisibleLinks
            // 
            helpVisibleLinks.AutoSize = true;
            helpVisibleLinks.Location = new System.Drawing.Point(457, 27);
            helpVisibleLinks.Name = "helpVisibleLinks";
            helpVisibleLinks.Size = new System.Drawing.Size(19, 13);
            helpVisibleLinks.TabIndex = 7;
            helpVisibleLinks.TabStop = true;
            helpVisibleLinks.Text = "[?]";
            // 
            // chkVisibleLinks
            // 
            chkVisibleLinks.AutoSize = true;
            chkVisibleLinks.Location = new System.Drawing.Point(301, 27);
            chkVisibleLinks.MinimumSize = new System.Drawing.Size(150, 0);
            chkVisibleLinks.Name = "chkVisibleLinks";
            chkVisibleLinks.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            chkVisibleLinks.Size = new System.Drawing.Size(150, 17);
            chkVisibleLinks.TabIndex = 6;
            chkVisibleLinks.Text = "Visible links";
            chkVisibleLinks.UseVisualStyleBackColor = true;
            // 
            // txtLinks
            // 
            txtLinks.Location = new System.Drawing.Point(9, 56);
            txtLinks.Name = "txtLinks";
            txtLinks.Size = new System.Drawing.Size(467, 68);
            txtLinks.TabIndex = 5;
            txtLinks.Text = "";
            // 
            // lbLinks
            // 
            lbLinks.AutoSize = true;
            lbLinks.Location = new System.Drawing.Point(6, 27);
            lbLinks.Name = "lbLinks";
            lbLinks.Size = new System.Drawing.Size(214, 13);
            lbLinks.TabIndex = 4;
            lbLinks.Text = "Enter one or more MEGA links to be hidden." + '\r' + '\n';
            // 
            // gbPassword
            // 
            gbPassword.Controls.Add(txtQuality);
            gbPassword.Controls.Add(lbQuality);
            gbPassword.Controls.Add(lbPassword);
            gbPassword.Controls.Add(lbPasswordTxt);
            gbPassword.Controls.Add(txtPassword);
            gbPassword.Location = new System.Drawing.Point(12, 570);
            gbPassword.Name = "gbPassword";
            gbPassword.Size = new System.Drawing.Size(489, 117);
            gbPassword.TabIndex = 3;
            gbPassword.TabStop = false;
            gbPassword.Text = "Other options";
            // 
            // txtQuality
            // 
            txtQuality.Location = new System.Drawing.Point(346, 74);
            txtQuality.Name = "txtQuality";
            txtQuality.Size = new System.Drawing.Size(25, 20);
            txtQuality.TabIndex = 10;
            txtQuality.Text = "85";
            // 
            // lbQuality
            // 
            lbQuality.AutoSize = true;
            lbQuality.Location = new System.Drawing.Point(250, 77);
            lbQuality.MaximumSize = new System.Drawing.Size(90, 0);
            lbQuality.MinimumSize = new System.Drawing.Size(90, 0);
            lbQuality.Name = "lbQuality";
            lbQuality.Size = new System.Drawing.Size(90, 13);
            lbQuality.TabIndex = 9;
            lbQuality.Text = "JPEG Quality:";
            lbQuality.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lbPassword
            // 
            lbPassword.AutoSize = true;
            lbPassword.Location = new System.Drawing.Point(6, 29);
            lbPassword.MaximumSize = new System.Drawing.Size(480, 0);
            lbPassword.Name = "lbPassword";
            lbPassword.Size = new System.Drawing.Size(415, 13);
            lbPassword.TabIndex = 8;
            lbPassword.Text = "Optionally, you can specify the JPEG quality and a password. If not, just leave i" + "t empty.";
            // 
            // lbPasswordTxt
            // 
            lbPasswordTxt.AutoSize = true;
            lbPasswordTxt.Location = new System.Drawing.Point(7, 77);
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
            txtPassword.Location = new System.Drawing.Point(103, 74);
            txtPassword.MaxLength = 256;
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.Size = new System.Drawing.Size(141, 20);
            txtPassword.TabIndex = 8;
            // 
            // btSave
            // 
            btSave.Location = new System.Drawing.Point(12, 693);
            btSave.Name = "btSave";
            btSave.Size = new System.Drawing.Size(85, 23);
            btSave.TabIndex = 8;
            btSave.Text = "Save";
            btSave.UseVisualStyleBackColor = true;
            // 
            // btCancel
            // 
            btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btCancel.Location = new System.Drawing.Point(416, 693);
            btCancel.Name = "btCancel";
            btCancel.Size = new System.Drawing.Size(85, 23);
            btCancel.TabIndex = 9;
            btCancel.Text = "Cancel";
            btCancel.UseVisualStyleBackColor = true;
            // 
            // SteganoWizardSave
            // 
            AcceptButton = btSave;
            AllowDrop = true;
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btCancel;
            ClientSize = new System.Drawing.Size(513, 726);
            Controls.Add(btCancel);
            Controls.Add(btSave);
            Controls.Add(gbPassword);
            Controls.Add(gbLinks);
            Controls.Add(gbImage);
            Controls.Add(gbIntro);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            HelpButton = true;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SteganoWizardSave";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "SteganoWizard";
            gbIntro.ResumeLayout(false);
            gbIntro.PerformLayout();
            gbImage.ResumeLayout(false);
            gbImage.PerformLayout();
            gbLinks.ResumeLayout(false);
            gbLinks.PerformLayout();
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
        internal System.Windows.Forms.Button btImageOutput;
        internal System.Windows.Forms.TextBox txtImageOutput;
        internal System.Windows.Forms.Label lbImageOutput;
        internal System.Windows.Forms.Label lbImageInput;
        internal System.Windows.Forms.Button btImageInput;
        internal System.Windows.Forms.TextBox txtImageInput;
        internal System.Windows.Forms.GroupBox gbLinks;
        internal System.Windows.Forms.Label lbLinks;
        internal System.Windows.Forms.GroupBox gbPassword;
        internal System.Windows.Forms.Label lbPassword;
        internal System.Windows.Forms.Label lbPasswordTxt;
        internal System.Windows.Forms.TextBox txtPassword;
        internal System.Windows.Forms.Button btSave;
        internal System.Windows.Forms.Button btCancel;
        internal System.Windows.Forms.TextBox txtQuality;
        internal System.Windows.Forms.Label lbQuality;
        internal System.Windows.Forms.RichTextBox txtLinks;
        internal System.Windows.Forms.CheckBox chkVisibleLinks;
        internal System.Windows.Forms.LinkLabel helpVisibleLinks;
        internal System.Windows.Forms.LinkLabel helpStegano;
    }

}