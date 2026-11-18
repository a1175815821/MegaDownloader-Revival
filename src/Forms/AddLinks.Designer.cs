using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class AddLinks : System.Windows.Forms.Form
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
            FlowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            OpcionesPaquete = new SectionGroupBox();
            LinkLabel2 = new System.Windows.Forms.LinkLabel();
            LinkLabel2.MouseHover += new EventHandler(LinkLabel2_MouseHover);
            LinkLabel2.MouseLeave += new EventHandler(LinkLabel2_MouseLeave);
            LinkLabel2.Click += new EventHandler(LinkLabel2_Click);
            chkStartDownload = new System.Windows.Forms.CheckBox();
            lblPassword = new System.Windows.Forms.Label();
            chkUnZip = new System.Windows.Forms.CheckBox();
            chkUnZip.CheckedChanged += new EventHandler(chkUnZip_CheckedChanged);
            txtPassword = new System.Windows.Forms.TextBox();
            chkCrearDirectorio = new System.Windows.Forms.CheckBox();
            btnExaminar = new CommandButton();
            btnExaminar.Click += new EventHandler(btnExaminar_Click);
            txtRuta = new System.Windows.Forms.TextBox();
            txtNombre = new System.Windows.Forms.TextBox();
            Label2 = new System.Windows.Forms.Label();
            Label1 = new System.Windows.Forms.Label();
            LinkLabel1 = new System.Windows.Forms.LinkLabel();
            LinkLabel1.MouseHover += new EventHandler(LinkLabel1_MouseHover);
            LinkLabel1.MouseLeave += new EventHandler(LinkLabel1_MouseLeave);
            LinkLabel1.Click += new EventHandler(LinkLabel1_Click);
            btnAgregar = new CommandButton();
            btnAgregar.Click += new EventHandler(btnAgregar_Click);
            btnWatchOnline = new CommandButton();
            btnWatchOnline.Click += new EventHandler(btnWatchOnline_Click);
            txtLinks = new UiStyles.PlainTextRichTextBox();
            txtLinks.TextChanged += new EventHandler(txtLinks_TextChanged);
            linkStegano = new System.Windows.Forms.LinkLabel();
            linkStegano.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkStegano_LinkClicked);
            lblLinkCount = new System.Windows.Forms.Label();
            preflightProgressBar = new System.Windows.Forms.ProgressBar();
            OpcionesPaquete.SuspendLayout();
            SuspendLayout();
            // 
            // FlowLayoutPanel1
            // 
            FlowLayoutPanel1.AutoSize = true;
            FlowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            FlowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            FlowLayoutPanel1.Location = new System.Drawing.Point(12, 186);
            FlowLayoutPanel1.Name = "FlowLayoutPanel1";
            FlowLayoutPanel1.Size = new System.Drawing.Size(0, 0);
            FlowLayoutPanel1.TabIndex = 1;
            // 
            // OpcionesPaquete
            // 
            OpcionesPaquete.Controls.Add(LinkLabel2);
            OpcionesPaquete.Controls.Add(chkStartDownload);
            OpcionesPaquete.Controls.Add(lblPassword);
            OpcionesPaquete.Controls.Add(chkUnZip);
            OpcionesPaquete.Controls.Add(txtPassword);
            OpcionesPaquete.Controls.Add(chkCrearDirectorio);
            OpcionesPaquete.Controls.Add(btnExaminar);
            OpcionesPaquete.Controls.Add(txtRuta);
            OpcionesPaquete.Controls.Add(txtNombre);
            OpcionesPaquete.Controls.Add(Label2);
            OpcionesPaquete.Controls.Add(Label1);
            OpcionesPaquete.Location = new System.Drawing.Point(12, 264);
            OpcionesPaquete.Name = "OpcionesPaquete";
            OpcionesPaquete.Size = new System.Drawing.Size(649, 111);
            OpcionesPaquete.TabIndex = 4;
            OpcionesPaquete.TabStop = false;
            OpcionesPaquete.Text = "Opciones";
            // 
            // LinkLabel2
            // 
            LinkLabel2.AutoSize = true;
            LinkLabel2.Location = new System.Drawing.Point(506, 86);
            LinkLabel2.MinimumSize = new System.Drawing.Size(20, 0);
            LinkLabel2.Name = "LinkLabel2";
            LinkLabel2.Size = new System.Drawing.Size(20, 13);
            LinkLabel2.TabIndex = 32;
            LinkLabel2.TabStop = true;
            LinkLabel2.Text = "[?]";
            LinkLabel2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkStartDownload
            // 
            chkStartDownload.AutoSize = true;
            chkStartDownload.Location = new System.Drawing.Point(59, 85);
            chkStartDownload.MinimumSize = new System.Drawing.Size(97, 0);
            chkStartDownload.Name = "chkStartDownload";
            chkStartDownload.Size = new System.Drawing.Size(101, 17);
            chkStartDownload.TabIndex = 7;
            chkStartDownload.Text = "Iniciar descarga";
            chkStartDownload.UseVisualStyleBackColor = true;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Location = new System.Drawing.Point(435, 86);
            lblPassword.MinimumSize = new System.Drawing.Size(75, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new System.Drawing.Size(75, 13);
            lblPassword.TabIndex = 31;
            lblPassword.Text = "Password";
            lblPassword.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkUnZip
            // 
            chkUnZip.AutoSize = true;
            chkUnZip.Location = new System.Drawing.Point(304, 85);
            chkUnZip.Name = "chkUnZip";
            chkUnZip.Size = new System.Drawing.Size(131, 17);
            chkUnZip.TabIndex = 2;
            chkUnZip.Text = "Extracción automática";
            chkUnZip.UseVisualStyleBackColor = true;
            // 
            // txtPassword
            // 
            txtPassword.Location = new System.Drawing.Point(532, 83);
            txtPassword.MaxLength = 128;
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new System.Drawing.Size(102, 20);
            txtPassword.TabIndex = 30;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // chkCrearDirectorio
            // 
            chkCrearDirectorio.AutoSize = true;
            chkCrearDirectorio.Location = new System.Drawing.Point(178, 85);
            chkCrearDirectorio.MinimumSize = new System.Drawing.Size(97, 0);
            chkCrearDirectorio.Name = "chkCrearDirectorio";
            chkCrearDirectorio.Size = new System.Drawing.Size(97, 17);
            chkCrearDirectorio.TabIndex = 3;
            chkCrearDirectorio.Text = "Crear directorio";
            chkCrearDirectorio.UseVisualStyleBackColor = true;
            // 
            // btnExaminar
            // 
            btnExaminar.Location = new System.Drawing.Point(559, 51);
            btnExaminar.Name = "btnExaminar";
            btnExaminar.Size = new System.Drawing.Size(75, 23);
            btnExaminar.TabIndex = 6;
            btnExaminar.Text = "Examinar";
            btnExaminar.UseVisualStyleBackColor = true;
            // 
            // txtRuta
            // 
            txtRuta.Location = new System.Drawing.Point(59, 53);
            txtRuta.Name = "txtRuta";
            txtRuta.Size = new System.Drawing.Size(494, 20);
            txtRuta.TabIndex = 5;
            // 
            // txtNombre
            // 
            txtNombre.Location = new System.Drawing.Point(59, 24);
            txtNombre.Name = "txtNombre";
            txtNombre.Size = new System.Drawing.Size(575, 20);
            txtNombre.TabIndex = 1;
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(6, 27);
            Label2.MinimumSize = new System.Drawing.Size(47, 0);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(47, 13);
            Label2.TabIndex = 0;
            Label2.Text = "Nombre:";
            Label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(6, 56);
            Label1.MinimumSize = new System.Drawing.Size(47, 0);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(47, 13);
            Label1.TabIndex = 4;
            Label1.Text = "Ruta:";
            Label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // LinkLabel1
            // 
            LinkLabel1.AutoSize = true;
            LinkLabel1.Location = new System.Drawing.Point(310, 191);
            LinkLabel1.Name = "LinkLabel1";
            LinkLabel1.Size = new System.Drawing.Size(19, 13);
            LinkLabel1.TabIndex = 11;
            LinkLabel1.TabStop = true;
            LinkLabel1.Text = "[?]";
            LinkLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnAgregar
            // 
            btnAgregar.Location = new System.Drawing.Point(12, 186);
            btnAgregar.Name = "btnAgregar";
            btnAgregar.Size = new System.Drawing.Size(133, 23);
            btnAgregar.TabIndex = 1;
            btnAgregar.Text = "Agregar links";
            btnAgregar.UseVisualStyleBackColor = true;
            // 
            // btnWatchOnline
            // 
            btnWatchOnline.Location = new System.Drawing.Point(171, 186);
            btnWatchOnline.Name = "btnWatchOnline";
            btnWatchOnline.Size = new System.Drawing.Size(133, 23);
            btnWatchOnline.TabIndex = 5;
            btnWatchOnline.Text = "Ver Online";
            btnWatchOnline.UseVisualStyleBackColor = true;
            // 
            // txtLinks
            // 
            txtLinks.Location = new System.Drawing.Point(12, 12);
            txtLinks.Name = "txtLinks";
            txtLinks.Size = new System.Drawing.Size(649, 168);
            txtLinks.TabIndex = 12;
            txtLinks.Text = "";
            // 
            // linkStegano
            // 
            linkStegano.AutoSize = true;
            linkStegano.Location = new System.Drawing.Point(411, 191);
            linkStegano.MinimumSize = new System.Drawing.Size(250, 0);
            linkStegano.Name = "linkStegano";
            linkStegano.RightToLeft = System.Windows.Forms.RightToLeft.No;
            linkStegano.Size = new System.Drawing.Size(250, 13);
            linkStegano.TabIndex = 13;
            linkStegano.TabStop = true;
            linkStegano.Text = "Recuperar enlaces de una imagen";
            linkStegano.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblLinkCount
            // 
            lblLinkCount.AutoSize = false;
            lblLinkCount.Location = new System.Drawing.Point(12, 213);
            lblLinkCount.Name = "lblLinkCount";
            lblLinkCount.Size = new System.Drawing.Size(649, 32);
            lblLinkCount.TabIndex = 14;
            lblLinkCount.Text = "";
            lblLinkCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // preflightProgressBar
            // 
            preflightProgressBar.Location = new System.Drawing.Point(12, 248);
            preflightProgressBar.Size = new System.Drawing.Size(649, 8);
            preflightProgressBar.Name = "preflightProgressBar";
            preflightProgressBar.Visible = false;
            // 
            // AddLinks
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7.0f, 15.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(960, 600);
            Controls.Add(preflightProgressBar);
            Controls.Add(linkStegano);
            Controls.Add(lblLinkCount);
            Controls.Add(txtLinks);
            Controls.Add(LinkLabel1);
            Controls.Add(btnWatchOnline);
            Controls.Add(btnAgregar);
            Controls.Add(OpcionesPaquete);
            Controls.Add(FlowLayoutPanel1);
            Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AddLinks";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            Text = "AddLinks";
            OpcionesPaquete.ResumeLayout(false);
            OpcionesPaquete.PerformLayout();
            Disposed += new EventHandler(AddLinks_Disposed);
            Load += new EventHandler(AddLinks_Load);
            Shown += new EventHandler(AddLinks_Shown);
            FormClosed += new System.Windows.Forms.FormClosedEventHandler(AddLinks_FormClosed);
            FormClosing += new System.Windows.Forms.FormClosingEventHandler(AddLinks_FormClosing);
            ResumeLayout(false);
            PerformLayout();

        }
        internal System.Windows.Forms.FlowLayoutPanel FlowLayoutPanel1;
        internal System.Windows.Forms.GroupBox OpcionesPaquete;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox txtNombre;
        internal System.Windows.Forms.TextBox txtRuta;
        internal System.Windows.Forms.CheckBox chkCrearDirectorio;
        internal System.Windows.Forms.Button btnExaminar;
        internal System.Windows.Forms.Button btnAgregar;
        internal System.Windows.Forms.CheckBox chkUnZip;
        internal System.Windows.Forms.Button btnWatchOnline;
        internal System.Windows.Forms.LinkLabel LinkLabel1;
        internal System.Windows.Forms.CheckBox chkStartDownload;
        internal System.Windows.Forms.LinkLabel LinkLabel2;
        internal System.Windows.Forms.Label lblPassword;
        internal System.Windows.Forms.TextBox txtPassword;
        internal System.Windows.Forms.RichTextBox txtLinks;
        internal System.Windows.Forms.LinkLabel linkStegano;
        internal System.Windows.Forms.Label lblLinkCount;
        internal System.Windows.Forms.ProgressBar preflightProgressBar;
    }
}