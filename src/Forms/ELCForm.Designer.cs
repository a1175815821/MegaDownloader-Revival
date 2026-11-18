using System;
using System.Runtime.CompilerServices;

namespace MegaDownloader
{
    // 
    // Created by SharpDevelop.
    // User: asolino
    // Date: 25/03/2013
    // Time: 10:51
    // 
    // To change this template use Tools | Options | Coding | Edit Standard Headers.
    // 
    public partial class ELCForm : System.Windows.Forms.Form
    {

        /// <summary>
	/// 	''' Designer variable used to keep track of non-visual components.
	/// 	''' </summary>
        private System.ComponentModel.IContainer components;

        /// <summary>
	/// 	''' Disposes resources used by the form.
	/// 	''' </summary>
	/// 	''' <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components is not null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
	/// 	''' This method is required for Windows Forms designer support.
	/// 	''' Do not change the method contents inside the source code editor. The Forms designer might
	/// 	''' not be able to load this method if it was changed manually.
	/// 	''' </summary>
        private void InitializeComponent()
        {
            gbMEGAUrl = new System.Windows.Forms.GroupBox();
            _chkMultipleELCs = new System.Windows.Forms.CheckBox();
            label3 = new System.Windows.Forms.Label();
            _btnCrearELC = new System.Windows.Forms.Button();
            _btnCrearELC.Click += new EventHandler(btnCrearELC_Click);
            comboELCAccounts = new System.Windows.Forms.ComboBox();
            _txtMegaURLs = new System.Windows.Forms.TextBox();
            _txtMegaURLs.KeyDown += new System.Windows.Forms.KeyEventHandler(txtMegaURLs_KeyDown);
            lblExplanation = new System.Windows.Forms.Label();
            gbELCLink = new System.Windows.Forms.GroupBox();
            lblExplanation2 = new System.Windows.Forms.Label();
            _btnSaveFile = new System.Windows.Forms.Button();
            _btnSaveFile.Click += new EventHandler(btnSaveFile_Click);
            _btnExaminar = new System.Windows.Forms.Button();
            _btnExaminar.Click += new EventHandler(btnExaminar_Click);
            lblSaveAsELC = new System.Windows.Forms.Label();
            txtExaminar = new System.Windows.Forms.TextBox();
            _txtELCUrl = new System.Windows.Forms.TextBox();
            _txtELCUrl.KeyDown += new System.Windows.Forms.KeyEventHandler(txtELCUrl_KeyDown);
            _btnCerrar = new System.Windows.Forms.Button();
            _btnCerrar.Click += new EventHandler(btnCerrar_Click);
            gbMEGAUrl.SuspendLayout();
            gbELCLink.SuspendLayout();
            SuspendLayout();
            // 
            // gbMEGAUrl
            // 
            gbMEGAUrl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbMEGAUrl.Controls.Add(_chkMultipleELCs);
            gbMEGAUrl.Controls.Add(label3);
            gbMEGAUrl.Controls.Add(_btnCrearELC);
            gbMEGAUrl.Controls.Add(comboELCAccounts);
            gbMEGAUrl.Controls.Add(_txtMegaURLs);
            gbMEGAUrl.Controls.Add(lblExplanation);
            gbMEGAUrl.Location = new System.Drawing.Point(12, 12);
            gbMEGAUrl.Name = "gbMEGAUrl";
            gbMEGAUrl.Size = new System.Drawing.Size(658, 181);
            gbMEGAUrl.TabIndex = 0;
            gbMEGAUrl.TabStop = false;
            gbMEGAUrl.Text = "groupBox1";
            // 
            // chkMultipleELCs
            // 
            _chkMultipleELCs.AutoSize = true;
            _chkMultipleELCs.Location = new System.Drawing.Point(322, 152);
            _chkMultipleELCs.Name = "_chkMultipleELCs";
            _chkMultipleELCs.Size = new System.Drawing.Size(85, 17);
            _chkMultipleELCs.TabIndex = 5;
            _chkMultipleELCs.Text = "Multiple ELC";
            _chkMultipleELCs.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(6, 146);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(100, 23);
            label3.TabIndex = 4;
            label3.Text = "Cuenta ELC:";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnCrearELC
            // 
            _btnCrearELC.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            _btnCrearELC.Location = new System.Drawing.Point(499, 148);
            _btnCrearELC.Name = "_btnCrearELC";
            _btnCrearELC.Size = new System.Drawing.Size(144, 23);
            _btnCrearELC.TabIndex = 3;
            _btnCrearELC.Text = "Generar ELC";
            _btnCrearELC.UseVisualStyleBackColor = true;
            // 
            // comboELCAccounts
            // 
            comboELCAccounts.FormattingEnabled = true;
            comboELCAccounts.Location = new System.Drawing.Point(112, 148);
            comboELCAccounts.Name = "comboELCAccounts";
            comboELCAccounts.Size = new System.Drawing.Size(195, 21);
            comboELCAccounts.TabIndex = 2;
            // 
            // txtMegaURLs
            // 
            _txtMegaURLs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            _txtMegaURLs.Location = new System.Drawing.Point(14, 42);
            _txtMegaURLs.Multiline = true;
            _txtMegaURLs.Name = "_txtMegaURLs";
            _txtMegaURLs.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            _txtMegaURLs.Size = new System.Drawing.Size(629, 100);
            _txtMegaURLs.TabIndex = 1;
            // 
            // lblExplanation
            // 
            lblExplanation.Location = new System.Drawing.Point(6, 16);
            lblExplanation.Name = "lblExplanation";
            lblExplanation.Size = new System.Drawing.Size(495, 23);
            lblExplanation.TabIndex = 0;
            lblExplanation.Text = "label1";
            lblExplanation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // gbELCLink
            // 
            gbELCLink.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            gbELCLink.Controls.Add(lblExplanation2);
            gbELCLink.Controls.Add(_btnSaveFile);
            gbELCLink.Controls.Add(_btnExaminar);
            gbELCLink.Controls.Add(lblSaveAsELC);
            gbELCLink.Controls.Add(txtExaminar);
            gbELCLink.Controls.Add(_txtELCUrl);
            gbELCLink.Location = new System.Drawing.Point(12, 198);
            gbELCLink.Name = "gbELCLink";
            gbELCLink.Size = new System.Drawing.Size(658, 180);
            gbELCLink.TabIndex = 1;
            gbELCLink.TabStop = false;
            gbELCLink.Text = "groupBox2";
            // 
            // lblExplanation2
            // 
            lblExplanation2.Location = new System.Drawing.Point(6, 16);
            lblExplanation2.Name = "lblExplanation2";
            lblExplanation2.Size = new System.Drawing.Size(495, 23);
            lblExplanation2.TabIndex = 4;
            lblExplanation2.Text = "label2";
            lblExplanation2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnSaveFile
            // 
            _btnSaveFile.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            _btnSaveFile.Location = new System.Drawing.Point(499, 147);
            _btnSaveFile.Name = "_btnSaveFile";
            _btnSaveFile.Size = new System.Drawing.Size(144, 23);
            _btnSaveFile.TabIndex = 4;
            _btnSaveFile.Text = "Guardar fichero";
            _btnSaveFile.UseVisualStyleBackColor = true;
            // 
            // btnExaminar
            // 
            _btnExaminar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            _btnExaminar.Location = new System.Drawing.Point(372, 147);
            _btnExaminar.Name = "_btnExaminar";
            _btnExaminar.Size = new System.Drawing.Size(75, 23);
            _btnExaminar.TabIndex = 3;
            _btnExaminar.Text = "Examinar";
            _btnExaminar.UseVisualStyleBackColor = true;
            // 
            // lblSaveAsELC
            // 
            lblSaveAsELC.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            lblSaveAsELC.Location = new System.Drawing.Point(6, 147);
            lblSaveAsELC.Name = "lblSaveAsELC";
            lblSaveAsELC.Size = new System.Drawing.Size(100, 23);
            lblSaveAsELC.TabIndex = 4;
            lblSaveAsELC.Text = "Guardar ELC:";
            lblSaveAsELC.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtExaminar
            // 
            txtExaminar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtExaminar.Location = new System.Drawing.Point(112, 149);
            txtExaminar.Name = "txtExaminar";
            txtExaminar.Size = new System.Drawing.Size(254, 20);
            txtExaminar.TabIndex = 3;
            // 
            // txtELCUrl
            // 
            _txtELCUrl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            _txtELCUrl.Location = new System.Drawing.Point(14, 42);
            _txtELCUrl.Multiline = true;
            _txtELCUrl.Name = "_txtELCUrl";
            _txtELCUrl.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            _txtELCUrl.Size = new System.Drawing.Size(629, 99);
            _txtELCUrl.TabIndex = 2;
            // 
            // btnCerrar
            // 
            _btnCerrar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            _btnCerrar.Location = new System.Drawing.Point(595, 386);
            _btnCerrar.Name = "_btnCerrar";
            _btnCerrar.Size = new System.Drawing.Size(75, 23);
            _btnCerrar.TabIndex = 2;
            _btnCerrar.Text = "Cerrar";
            _btnCerrar.UseVisualStyleBackColor = true;
            // 
            // ELCForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(682, 421);
            Controls.Add(_btnCerrar);
            Controls.Add(gbELCLink);
            Controls.Add(gbMEGAUrl);
            Icon = My.Resources.Resources.icono;
            MinimumSize = new System.Drawing.Size(500, 400);
            Name = "ELCForm";
            Text = "ELCForm";
            gbMEGAUrl.ResumeLayout(false);
            gbMEGAUrl.PerformLayout();
            gbELCLink.ResumeLayout(false);
            gbELCLink.PerformLayout();
            ResumeLayout(false);

        }
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblExplanation2;
        private System.Windows.Forms.Button _btnSaveFile;

        private System.Windows.Forms.Button btnSaveFile
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnSaveFile;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnSaveFile != null)
                {
                    _btnSaveFile.Click -= btnSaveFile_Click;
                }

                _btnSaveFile = value;
                if (_btnSaveFile != null)
                {
                    _btnSaveFile.Click += btnSaveFile_Click;
                }
            }
        }
        private System.Windows.Forms.TextBox txtExaminar;
        private System.Windows.Forms.Label lblSaveAsELC;
        private System.Windows.Forms.Button _btnExaminar;

        private System.Windows.Forms.Button btnExaminar
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnExaminar;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnExaminar != null)
                {
                    _btnExaminar.Click -= btnExaminar_Click;
                }

                _btnExaminar = value;
                if (_btnExaminar != null)
                {
                    _btnExaminar.Click += btnExaminar_Click;
                }
            }
        }
        private System.Windows.Forms.Button _btnCerrar;

        private System.Windows.Forms.Button btnCerrar
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnCerrar;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnCerrar != null)
                {
                    _btnCerrar.Click -= btnCerrar_Click;
                }

                _btnCerrar = value;
                if (_btnCerrar != null)
                {
                    _btnCerrar.Click += btnCerrar_Click;
                }
            }
        }
        private System.Windows.Forms.ComboBox comboELCAccounts;
        private System.Windows.Forms.Button _btnCrearELC;

        private System.Windows.Forms.Button btnCrearELC
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnCrearELC;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnCrearELC != null)
                {
                    _btnCrearELC.Click -= btnCrearELC_Click;
                }

                _btnCrearELC = value;
                if (_btnCrearELC != null)
                {
                    _btnCrearELC.Click += btnCrearELC_Click;
                }
            }
        }
        private System.Windows.Forms.TextBox _txtELCUrl;

        private System.Windows.Forms.TextBox txtELCUrl
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _txtELCUrl;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_txtELCUrl != null)
                {
                    _txtELCUrl.KeyDown -= txtELCUrl_KeyDown;
                }

                _txtELCUrl = value;
                if (_txtELCUrl != null)
                {
                    _txtELCUrl.KeyDown += txtELCUrl_KeyDown;
                }
            }
        }
        private System.Windows.Forms.GroupBox gbELCLink;
        private System.Windows.Forms.Label lblExplanation;
        private System.Windows.Forms.TextBox _txtMegaURLs;

        private System.Windows.Forms.TextBox txtMegaURLs
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _txtMegaURLs;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_txtMegaURLs != null)
                {
                    _txtMegaURLs.KeyDown -= txtMegaURLs_KeyDown;
                }

                _txtMegaURLs = value;
                if (_txtMegaURLs != null)
                {
                    _txtMegaURLs.KeyDown += txtMegaURLs_KeyDown;
                }
            }
        }
        private System.Windows.Forms.GroupBox gbMEGAUrl;
        private System.Windows.Forms.CheckBox _chkMultipleELCs;

        internal virtual System.Windows.Forms.CheckBox chkMultipleELCs
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _chkMultipleELCs;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                _chkMultipleELCs = value;
            }
        }

    }
}