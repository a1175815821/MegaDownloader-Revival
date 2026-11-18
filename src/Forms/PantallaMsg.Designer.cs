using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class PantallaMsg : System.Windows.Forms.Form
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
            txtDatos = new System.Windows.Forms.RichTextBox();
            chkCodificarEnlaces = new System.Windows.Forms.CheckBox();
            chkCodificarEnlaces.CheckedChanged += new EventHandler(chkCodificarEnlaces_CheckedChanged);
            lklGenerarELC = new System.Windows.Forms.LinkLabel();
            lklGenerarELC.Click += new EventHandler(lklGenerarELC_Click);
            SuspendLayout();
            // 
            // Button1
            // 
            Button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            Button1.Location = new System.Drawing.Point(638, 234);
            Button1.Name = "Button1";
            Button1.Size = new System.Drawing.Size(75, 23);
            Button1.TabIndex = 0;
            Button1.Text = "Cerrar";
            Button1.UseVisualStyleBackColor = true;
            // 
            // txtDatos
            // 
            txtDatos.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            txtDatos.Location = new System.Drawing.Point(12, 12);
            txtDatos.Name = "txtDatos";
            txtDatos.ReadOnly = true;
            txtDatos.Size = new System.Drawing.Size(701, 216);
            txtDatos.TabIndex = 1;
            txtDatos.Text = "";
            // 
            // chkCodificarEnlaces
            // 
            chkCodificarEnlaces.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            chkCodificarEnlaces.AutoSize = true;
            chkCodificarEnlaces.Location = new System.Drawing.Point(13, 239);
            chkCodificarEnlaces.Name = "chkCodificarEnlaces";
            chkCodificarEnlaces.Size = new System.Drawing.Size(107, 17);
            chkCodificarEnlaces.TabIndex = 2;
            chkCodificarEnlaces.Text = "Codificar enlaces";
            chkCodificarEnlaces.UseVisualStyleBackColor = true;
            // 
            // lklGenerarELC
            // 
            lklGenerarELC.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            lklGenerarELC.Location = new System.Drawing.Point(143, 240);
            lklGenerarELC.Name = "lklGenerarELC";
            lklGenerarELC.Size = new System.Drawing.Size(100, 23);
            lklGenerarELC.TabIndex = 3;
            lklGenerarELC.TabStop = true;
            lklGenerarELC.Text = "Generar ELC";
            // 
            // PantallaMsg
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(725, 269);
            Controls.Add(lklGenerarELC);
            Controls.Add(chkCodificarEnlaces);
            Controls.Add(txtDatos);
            Controls.Add(Button1);
            Icon = My.Resources.Resources.icono;
            Name = "PantallaMsg";
            Text = "Información";
            Load += new EventHandler(VerError_Load);
            ResumeLayout(false);
            PerformLayout();

        }
        internal System.Windows.Forms.LinkLabel lklGenerarELC;
        internal System.Windows.Forms.Button Button1;
        internal System.Windows.Forms.RichTextBox txtDatos;
        internal System.Windows.Forms.CheckBox chkCodificarEnlaces;
    }
}