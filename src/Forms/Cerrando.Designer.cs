using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class Cerrando : System.Windows.Forms.Form
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(Cerrando));
            lblMensaje = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // lblMensaje
            // 
            lblMensaje.AutoSize = true;
            lblMensaje.Location = new System.Drawing.Point(12, 9);
            lblMensaje.Name = "lblMensaje";
            lblMensaje.Size = new System.Drawing.Size(34, 13);
            lblMensaje.TabIndex = 0;
            lblMensaje.Text = "Texto";
            // 
            // Cerrando
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(180, 35);
            ControlBox = false;
            Controls.Add(lblMensaje);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Cerrando";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Load += new EventHandler(Cerrando_Load);
            ResumeLayout(false);
            PerformLayout();

        }
        internal System.Windows.Forms.Label lblMensaje;

    }
}