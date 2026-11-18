using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class EncodeLinksForm : System.Windows.Forms.Form
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
            Button1 = new System.Windows.Forms.Button();
            Button1.Click += new EventHandler(Button1_Click);
            txtEncodedLinks = new System.Windows.Forms.RichTextBox();
            GroupBox1 = new System.Windows.Forms.GroupBox();
            lblExplanation = new System.Windows.Forms.Label();
            txtOriginalLinks = new System.Windows.Forms.RichTextBox();
            GroupBox2 = new System.Windows.Forms.GroupBox();
            GroupBox1.SuspendLayout();
            GroupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // Button1
            // 
            Button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            Button1.Location = new System.Drawing.Point(616, 328);
            Button1.Name = "Button1";
            Button1.Size = new System.Drawing.Size(75, 23);
            Button1.TabIndex = 0;
            Button1.Text = "Cerrar";
            Button1.UseVisualStyleBackColor = true;
            // 
            // txtEncodedLinks
            // 
            txtEncodedLinks.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            txtEncodedLinks.Location = new System.Drawing.Point(6, 19);
            txtEncodedLinks.Name = "txtEncodedLinks";
            txtEncodedLinks.ReadOnly = true;
            txtEncodedLinks.Size = new System.Drawing.Size(667, 107);
            txtEncodedLinks.TabIndex = 1;
            txtEncodedLinks.Text = "";
            // 
            // GroupBox1
            // 
            GroupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            GroupBox1.Controls.Add(lblExplanation);
            GroupBox1.Controls.Add(txtOriginalLinks);
            GroupBox1.Location = new System.Drawing.Point(12, 12);
            GroupBox1.Name = "GroupBox1";
            GroupBox1.Size = new System.Drawing.Size(679, 172);
            GroupBox1.TabIndex = 2;
            GroupBox1.TabStop = false;
            GroupBox1.Text = "GroupBox1";
            // 
            // lblExplanation
            // 
            lblExplanation.AutoSize = true;
            lblExplanation.Location = new System.Drawing.Point(6, 30);
            lblExplanation.MinimumSize = new System.Drawing.Size(500, 0);
            lblExplanation.Name = "lblExplanation";
            lblExplanation.Size = new System.Drawing.Size(500, 13);
            lblExplanation.TabIndex = 3;
            lblExplanation.Text = "Label1";
            // 
            // txtOriginalLinks
            // 
            txtOriginalLinks.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            txtOriginalLinks.Location = new System.Drawing.Point(6, 59);
            txtOriginalLinks.Name = "txtOriginalLinks";
            txtOriginalLinks.Size = new System.Drawing.Size(667, 107);
            txtOriginalLinks.TabIndex = 2;
            txtOriginalLinks.Text = "";
            // 
            // GroupBox2
            // 
            GroupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            GroupBox2.Controls.Add(txtEncodedLinks);
            GroupBox2.Location = new System.Drawing.Point(12, 190);
            GroupBox2.Name = "GroupBox2";
            GroupBox2.Size = new System.Drawing.Size(679, 132);
            GroupBox2.TabIndex = 3;
            GroupBox2.TabStop = false;
            GroupBox2.Text = "GroupBox2";
            // 
            // EncodeLinksForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(703, 363);
            Controls.Add(GroupBox2);
            Controls.Add(GroupBox1);
            Controls.Add(Button1);
            Icon = My.Resources.Resources.icono;
            MinimumSize = new System.Drawing.Size(650, 340);
            Name = "EncodeLinksForm";
            Text = "Información";
            GroupBox1.ResumeLayout(false);
            GroupBox1.PerformLayout();
            GroupBox2.ResumeLayout(false);
            Load += new EventHandler(EncodeLinksForm_Load);
            FormClosed += new System.Windows.Forms.FormClosedEventHandler(Cerrando);
            ResumeLayout(false);

        }
        internal System.Windows.Forms.Button Button1;
        internal System.Windows.Forms.RichTextBox txtEncodedLinks;
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.RichTextBox txtOriginalLinks;
        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.Label lblExplanation;
    }
}