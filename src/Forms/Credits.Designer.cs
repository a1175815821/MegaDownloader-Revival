using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class Credits : System.Windows.Forms.Form
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
            lblTitle = new System.Windows.Forms.LinkLabel();
            lblTitle.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(lblTitle_LinkClicked);
            lblAutor = new System.Windows.Forms.Label();
            lblEmail = new System.Windows.Forms.Label();
            lblGraciasA = new System.Windows.Forms.Label();
            lblListaColaboradores = new System.Windows.Forms.Label();
            Label1 = new System.Windows.Forms.Label();
            PictureBox1 = new System.Windows.Forms.PictureBox();
            Label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)PictureBox1).BeginInit();
            SuspendLayout();
            // 
            // Button1
            // 
            Button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            Button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Button1.Location = new System.Drawing.Point(314, 267);
            Button1.Name = "Button1";
            Button1.Size = new System.Drawing.Size(75, 23);
            Button1.TabIndex = 0;
            Button1.Text = "Cerrar";
            Button1.UseVisualStyleBackColor = true;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblTitle.Location = new System.Drawing.Point(149, 25);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new System.Drawing.Size(190, 17);
            lblTitle.TabIndex = 1;
            lblTitle.TabStop = true;
            lblTitle.Text = "MegaDownloader BETA v";
            // 
            // lblAutor
            // 
            lblAutor.AutoSize = true;
            lblAutor.Location = new System.Drawing.Point(149, 74);
            lblAutor.Name = "lblAutor";
            lblAutor.Size = new System.Drawing.Size(173, 13);
            lblAutor.TabIndex = 2;
            lblAutor.Text = "Creado y diseñado por Andres_age";
            // 
            // lblEmail
            // 
            lblEmail.AutoSize = true;
            lblEmail.Location = new System.Drawing.Point(194, 96);
            lblEmail.Name = "lblEmail";
            lblEmail.Size = new System.Drawing.Size(118, 13);
            lblEmail.TabIndex = 3;
            lblEmail.Text = "a1175815821@163.com";
            // 
            // lblGraciasA
            // 
            lblGraciasA.AutoSize = true;
            lblGraciasA.Location = new System.Drawing.Point(9, 197);
            lblGraciasA.Name = "lblGraciasA";
            lblGraciasA.Size = new System.Drawing.Size(55, 13);
            lblGraciasA.TabIndex = 4;
            lblGraciasA.Text = "Gracias a:";
            // 
            // lblListaColaboradores
            // 
            lblListaColaboradores.AutoSize = true;
            lblListaColaboradores.Location = new System.Drawing.Point(20, 219);
            lblListaColaboradores.Name = "lblListaColaboradores";
            lblListaColaboradores.Size = new System.Drawing.Size(62, 39);
            lblListaColaboradores.TabIndex = 5;
            lblListaColaboradores.Text = "* ------------" + '\r' + '\n' + "* ----------------" + '\r' + '\n' + "* ----------------" + '\r' + '\n';
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 130);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(350, 13);
            Label1.TabIndex = 6;
            Label1.Text = "Gracias por usar esta aplicación no oficial de descarga de MEGA.CO.NZ";
            // 
            // PictureBox1
            // 
            PictureBox1.Image = My.Resources.Resources.mega;
            PictureBox1.Location = new System.Drawing.Point(15, 25);
            PictureBox1.Name = "PictureBox1";
            PictureBox1.Size = new System.Drawing.Size(128, 84);
            PictureBox1.TabIndex = 7;
            PictureBox1.TabStop = false;
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 159);
            Label2.MaximumSize = new System.Drawing.Size(350, 0);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(325, 26);
            Label2.TabIndex = 8;
            Label2.Text = "MegaDownloader no está relacionado con Mega.co.nz - Todas las marcas y logos son " + "propiedad de sus respectivos dueños.";
            // 
            // Credits
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            CancelButton = Button1;
            ClientSize = new System.Drawing.Size(401, 302);
            Controls.Add(Label2);
            Controls.Add(PictureBox1);
            Controls.Add(Label1);
            Controls.Add(lblListaColaboradores);
            Controls.Add(lblGraciasA);
            Controls.Add(lblEmail);
            Controls.Add(lblAutor);
            Controls.Add(lblTitle);
            Controls.Add(Button1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Credits";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Información";
            ((System.ComponentModel.ISupportInitialize)PictureBox1).EndInit();
            Load += new EventHandler(Credits_Load);
            ResumeLayout(false);
            PerformLayout();

        }
        internal System.Windows.Forms.Button Button1;
        internal System.Windows.Forms.Label lblAutor;
        internal System.Windows.Forms.Label lblEmail;
        internal System.Windows.Forms.Label lblGraciasA;
        internal System.Windows.Forms.Label lblListaColaboradores;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.PictureBox PictureBox1;
        internal System.Windows.Forms.LinkLabel lblTitle;
        internal System.Windows.Forms.Label Label2;
    }
}