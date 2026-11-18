using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class SplashScreen : System.Windows.Forms.Form
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
            lblMsg = new System.Windows.Forms.Label();
            PictureBox1 = new System.Windows.Forms.PictureBox();
            lblThx = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)PictureBox1).BeginInit();
            SuspendLayout();
            // 
            // lblMsg
            // 
            lblMsg.AutoSize = true;
            lblMsg.Location = new System.Drawing.Point(42, 134);
            lblMsg.MinimumSize = new System.Drawing.Size(200, 0);
            lblMsg.Name = "lblMsg";
            lblMsg.Size = new System.Drawing.Size(200, 13);
            lblMsg.TabIndex = 0;
            lblMsg.Text = "Cargando, por favor espere...";
            lblMsg.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // PictureBox1
            // 
            PictureBox1.Image = My.Resources.Resources.mega;
            PictureBox1.Location = new System.Drawing.Point(74, 12);
            PictureBox1.Name = "PictureBox1";
            PictureBox1.Size = new System.Drawing.Size(126, 83);
            PictureBox1.TabIndex = 1;
            PictureBox1.TabStop = false;
            // 
            // lblThx
            // 
            lblThx.AutoSize = true;
            lblThx.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblThx.Location = new System.Drawing.Point(14, 107);
            lblThx.MaximumSize = new System.Drawing.Size(250, 0);
            lblThx.MinimumSize = new System.Drawing.Size(250, 0);
            lblThx.Name = "lblThx";
            lblThx.Size = new System.Drawing.Size(250, 13);
            lblThx.TabIndex = 2;
            lblThx.Text = "MegaDownloader VXXX";
            lblThx.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SplashScreen
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            ClientSize = new System.Drawing.Size(276, 162);
            ControlBox = false;
            Controls.Add(lblThx);
            Controls.Add(PictureBox1);
            Controls.Add(lblMsg);
            DoubleBuffered = true;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SplashScreen";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            ((System.ComponentModel.ISupportInitialize)PictureBox1).EndInit();
            Load += new EventHandler(SplashScreen1_Load);
            ResumeLayout(false);
            PerformLayout();

        }
        internal System.Windows.Forms.Label lblMsg;
        internal System.Windows.Forms.PictureBox PictureBox1;
        internal System.Windows.Forms.Label lblThx;


    }
}