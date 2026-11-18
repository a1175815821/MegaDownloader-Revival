using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class StreamingForm : System.Windows.Forms.Form
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(StreamingForm));
            GroupBox1 = new System.Windows.Forms.GroupBox();
            Label1 = new System.Windows.Forms.Label();
            txtUrlMEGA = new System.Windows.Forms.TextBox();
            GroupBox2 = new System.Windows.Forms.GroupBox();
            Label2 = new System.Windows.Forms.Label();
            txtUrlStreaming = new System.Windows.Forms.TextBox();
            btnCerrar = new System.Windows.Forms.Button();
            btnCerrar.Click += new EventHandler(btnCerrar_Click);
            btnLanzarVLC = new System.Windows.Forms.Button();
            btnLanzarVLC.Click += new EventHandler(btnLanzarVLC_Click);
            GroupBox3 = new System.Windows.Forms.GroupBox();
            lblInfo = new System.Windows.Forms.Label();
            GroupBox1.SuspendLayout();
            GroupBox2.SuspendLayout();
            GroupBox3.SuspendLayout();
            SuspendLayout();
            // 
            // GroupBox1
            // 
            GroupBox1.Controls.Add(Label1);
            GroupBox1.Controls.Add(txtUrlMEGA);
            GroupBox1.Location = new System.Drawing.Point(12, 12);
            GroupBox1.Name = "GroupBox1";
            GroupBox1.Size = new System.Drawing.Size(609, 53);
            GroupBox1.TabIndex = 0;
            GroupBox1.TabStop = false;
            GroupBox1.Text = "MEGA";
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(6, 22);
            Label1.MinimumSize = new System.Drawing.Size(120, 0);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(120, 13);
            Label1.TabIndex = 1;
            Label1.Text = "URL de MEGA:";
            Label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtUrlMEGA
            // 
            txtUrlMEGA.Location = new System.Drawing.Point(127, 19);
            txtUrlMEGA.Name = "txtUrlMEGA";
            txtUrlMEGA.Size = new System.Drawing.Size(469, 20);
            txtUrlMEGA.TabIndex = 0;
            // 
            // GroupBox2
            // 
            GroupBox2.Controls.Add(Label2);
            GroupBox2.Controls.Add(txtUrlStreaming);
            GroupBox2.Location = new System.Drawing.Point(13, 71);
            GroupBox2.Name = "GroupBox2";
            GroupBox2.Size = new System.Drawing.Size(608, 80);
            GroupBox2.TabIndex = 1;
            GroupBox2.TabStop = false;
            GroupBox2.Text = "Streaming";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(5, 22);
            Label2.MinimumSize = new System.Drawing.Size(120, 0);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(120, 13);
            Label2.TabIndex = 2;
            Label2.Text = "URL streaming:";
            Label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtUrlStreaming
            // 
            txtUrlStreaming.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            txtUrlStreaming.Location = new System.Drawing.Point(126, 19);
            txtUrlStreaming.Multiline = true;
            txtUrlStreaming.Name = "txtUrlStreaming";
            txtUrlStreaming.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtUrlStreaming.Size = new System.Drawing.Size(467, 55);
            txtUrlStreaming.TabIndex = 1;
            // 
            // btnCerrar
            // 
            btnCerrar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnCerrar.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCerrar.Location = new System.Drawing.Point(546, 314);
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new System.Drawing.Size(75, 23);
            btnCerrar.TabIndex = 4;
            btnCerrar.Text = "Cerrar";
            btnCerrar.UseVisualStyleBackColor = true;
            // 
            // btnLanzarVLC
            // 
            btnLanzarVLC.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnLanzarVLC.DialogResult = System.Windows.Forms.DialogResult.None;
            btnLanzarVLC.Location = new System.Drawing.Point(12, 314);
            btnLanzarVLC.Name = "btnLanzarVLC";
            btnLanzarVLC.Size = new System.Drawing.Size(101, 23);
            btnLanzarVLC.TabIndex = 5;
            btnLanzarVLC.Text = "Lanzar VLC";
            btnLanzarVLC.UseVisualStyleBackColor = true;
            // 
            // GroupBox3
            // 
            GroupBox3.Controls.Add(lblInfo);
            GroupBox3.Location = new System.Drawing.Point(13, 156);
            GroupBox3.Name = "GroupBox3";
            GroupBox3.Size = new System.Drawing.Size(608, 152);
            GroupBox3.TabIndex = 6;
            GroupBox3.TabStop = false;
            GroupBox3.Text = "Información";
            // 
            // lblInfo
            // 
            lblInfo.AutoSize = true;
            lblInfo.Location = new System.Drawing.Point(6, 16);
            lblInfo.MaximumSize = new System.Drawing.Size(585, 0);
            lblInfo.MinimumSize = new System.Drawing.Size(585, 0);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new System.Drawing.Size(585, 117);
            lblInfo.TabIndex = 0;
            lblInfo.Text = resources.GetString("lblInfo.Text");
            // 
            // StreamingForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btnCerrar;
            ClientSize = new System.Drawing.Size(629, 349);
            Controls.Add(GroupBox3);
            Controls.Add(btnLanzarVLC);
            Controls.Add(btnCerrar);
            Controls.Add(GroupBox2);
            Controls.Add(GroupBox1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            HelpButton = true;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "StreamingForm";
            Text = "StreamingForm";
            GroupBox1.ResumeLayout(false);
            GroupBox1.PerformLayout();
            GroupBox2.ResumeLayout(false);
            GroupBox2.PerformLayout();
            GroupBox3.ResumeLayout(false);
            GroupBox3.PerformLayout();
            Load += new EventHandler(StreamingForm_Load);
            FormClosed += new System.Windows.Forms.FormClosedEventHandler(Cerrando);
            HelpButtonClicked += new System.ComponentModel.CancelEventHandler(HelpButtonPressed);
            ResumeLayout(false);

        }
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.TextBox txtUrlMEGA;
        internal System.Windows.Forms.Button btnCerrar;
        internal System.Windows.Forms.Button btnLanzarVLC;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox txtUrlStreaming;
        internal System.Windows.Forms.GroupBox GroupBox3;
        internal System.Windows.Forms.Label lblInfo;
    }
}