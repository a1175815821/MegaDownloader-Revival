using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class PropiedadesDescarga : System.Windows.Forms.Form
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
                // N-P3-3: the lazy help ToolTip (PropiedadesDescarga.vb "If t Is Nothing Then
                // t = New ToolTip") is not in the components container and was never
                // disposed — its HWND survived until GC. Deterministic release here.
                if (disposing && t is not null)
                {
                    t.Dispose();
                    t = null;
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
            chkUnZip = new System.Windows.Forms.CheckBox();
            chkUnZip.CheckedChanged += new EventHandler(chkUnZip_CheckedChanged);
            btnExaminar = new CommandButton();
            btnExaminar.Click += new EventHandler(btnExaminar_Click);
            txtRuta = new System.Windows.Forms.TextBox();
            Label1 = new System.Windows.Forms.Label();
            GroupBox1 = new System.Windows.Forms.GroupBox();
            txtUrl = new System.Windows.Forms.TextBox();
            Label5 = new System.Windows.Forms.Label();
            txtMD5 = new System.Windows.Forms.TextBox();
            txtTamano = new System.Windows.Forms.TextBox();
            txtNombre = new System.Windows.Forms.TextBox();
            Label4 = new System.Windows.Forms.Label();
            Label3 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            GroupBox2 = new System.Windows.Forms.GroupBox();
            LinkLabel2 = new System.Windows.Forms.LinkLabel();
            LinkLabel2.MouseHover += new EventHandler(LinkLabel2_MouseHover);
            LinkLabel2.MouseLeave += new EventHandler(LinkLabel2_MouseLeave);
            LinkLabel2.Click += new EventHandler(LinkLabel2_Click);
            lblPassword = new System.Windows.Forms.Label();
            txtPassword = new System.Windows.Forms.TextBox();
            Label15 = new System.Windows.Forms.Label();
            txtLimiteVelocidad = new System.Windows.Forms.TextBox();
            chkLimitarVelocidad = new System.Windows.Forms.CheckBox();
            chkLimitarVelocidad.CheckedChanged += new EventHandler(chkLimitarVelocidad_CheckedChanged);
            LinkLabel1 = new System.Windows.Forms.LinkLabel();
            LinkLabel1.MouseHover += new EventHandler(LinkLabel1_MouseHover);
            LinkLabel1.MouseLeave += new EventHandler(LinkLabel1_MouseLeave);
            LinkLabel1.Click += new EventHandler(LinkLabel1_Click);
            btnGuardar = new CommandButton();
            btnGuardar.Click += new EventHandler(btnGuardar_Click);
            btnCancelar = new CommandButton();
            btnCancelar.Click += new EventHandler(btnCancelar_Click);
            GroupBox1.SuspendLayout();
            GroupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // chkUnZip
            // 
            chkUnZip.AutoSize = true;
            chkUnZip.Location = new System.Drawing.Point(13, 91);
            chkUnZip.Name = "chkUnZip";
            chkUnZip.Size = new System.Drawing.Size(131, 17);
            chkUnZip.TabIndex = 5;
            chkUnZip.Text = "Extracción automática";
            chkUnZip.UseVisualStyleBackColor = true;
            // 
            // btnExaminar
            // 
            btnExaminar.Location = new System.Drawing.Point(335, 19);
            btnExaminar.Name = "btnExaminar";
            btnExaminar.Size = new System.Drawing.Size(84, 23);
            btnExaminar.TabIndex = 1;
            btnExaminar.Text = "Examinar";
            btnExaminar.UseVisualStyleBackColor = true;
            // 
            // txtRuta
            // 
            txtRuta.Location = new System.Drawing.Point(49, 22);
            txtRuta.Name = "txtRuta";
            txtRuta.Size = new System.Drawing.Size(280, 20);
            txtRuta.TabIndex = 0;
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(10, 25);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(33, 13);
            Label1.TabIndex = 7;
            Label1.Text = "Ruta:";
            // 
            // GroupBox1
            // 
            GroupBox1.Controls.Add(txtUrl);
            GroupBox1.Controls.Add(Label5);
            GroupBox1.Controls.Add(txtMD5);
            GroupBox1.Controls.Add(txtTamano);
            GroupBox1.Controls.Add(txtNombre);
            GroupBox1.Controls.Add(Label4);
            GroupBox1.Controls.Add(Label3);
            GroupBox1.Controls.Add(Label2);
            GroupBox1.Location = new System.Drawing.Point(12, 11);
            GroupBox1.Name = "GroupBox1";
            GroupBox1.Size = new System.Drawing.Size(425, 138);
            GroupBox1.TabIndex = 11;
            GroupBox1.TabStop = false;
            GroupBox1.Text = "Datos de la descarga";
            // 
            // txtUrl
            // 
            txtUrl.Location = new System.Drawing.Point(59, 50);
            txtUrl.Name = "txtUrl";
            txtUrl.ReadOnly = true;
            txtUrl.Size = new System.Drawing.Size(351, 20);
            txtUrl.TabIndex = 15;
            // 
            // Label5
            // 
            Label5.AutoSize = true;
            Label5.Location = new System.Drawing.Point(20, 53);
            Label5.Name = "Label5";
            Label5.Size = new System.Drawing.Size(32, 13);
            Label5.TabIndex = 14;
            Label5.Text = "URL:";
            // 
            // txtMD5
            // 
            txtMD5.Location = new System.Drawing.Point(59, 74);
            txtMD5.Name = "txtMD5";
            txtMD5.ReadOnly = true;
            txtMD5.Size = new System.Drawing.Size(351, 20);
            txtMD5.TabIndex = 13;
            // 
            // txtTamano
            // 
            txtTamano.Location = new System.Drawing.Point(59, 102);
            txtTamano.Name = "txtTamano";
            txtTamano.ReadOnly = true;
            txtTamano.Size = new System.Drawing.Size(351, 20);
            txtTamano.TabIndex = 12;
            // 
            // txtNombre
            // 
            txtNombre.Location = new System.Drawing.Point(59, 24);
            txtNombre.Name = "txtNombre";
            txtNombre.ReadOnly = true;
            txtNombre.Size = new System.Drawing.Size(351, 20);
            txtNombre.TabIndex = 11;
            // 
            // Label4
            // 
            Label4.AutoSize = true;
            Label4.Location = new System.Drawing.Point(20, 77);
            Label4.Name = "Label4";
            Label4.Size = new System.Drawing.Size(33, 13);
            Label4.TabIndex = 2;
            Label4.Text = "MD5:";
            // 
            // Label3
            // 
            Label3.AutoSize = true;
            Label3.Location = new System.Drawing.Point(4, 105);
            Label3.MinimumSize = new System.Drawing.Size(49, 0);
            Label3.Name = "Label3";
            Label3.Size = new System.Drawing.Size(49, 13);
            Label3.TabIndex = 1;
            Label3.Text = "Tamaño:";
            Label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(6, 27);
            Label2.MinimumSize = new System.Drawing.Size(49, 0);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(49, 13);
            Label2.TabIndex = 0;
            Label2.Text = "Nombre:";
            Label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // GroupBox2
            // 
            GroupBox2.Controls.Add(LinkLabel2);
            GroupBox2.Controls.Add(lblPassword);
            GroupBox2.Controls.Add(txtPassword);
            GroupBox2.Controls.Add(Label15);
            GroupBox2.Controls.Add(txtLimiteVelocidad);
            GroupBox2.Controls.Add(chkLimitarVelocidad);
            GroupBox2.Controls.Add(LinkLabel1);
            GroupBox2.Controls.Add(txtRuta);
            GroupBox2.Controls.Add(Label1);
            GroupBox2.Controls.Add(chkUnZip);
            GroupBox2.Controls.Add(btnExaminar);
            GroupBox2.Location = new System.Drawing.Point(12, 155);
            GroupBox2.Name = "GroupBox2";
            GroupBox2.Size = new System.Drawing.Size(425, 114);
            GroupBox2.TabIndex = 12;
            GroupBox2.TabStop = false;
            GroupBox2.Text = "Opciones";
            // 
            // LinkLabel2
            // 
            LinkLabel2.AutoSize = true;
            LinkLabel2.Location = new System.Drawing.Point(245, 92);
            LinkLabel2.MinimumSize = new System.Drawing.Size(20, 0);
            LinkLabel2.Name = "LinkLabel2";
            LinkLabel2.Size = new System.Drawing.Size(20, 13);
            LinkLabel2.TabIndex = 29;
            LinkLabel2.TabStop = true;
            LinkLabel2.Text = "[?]";
            LinkLabel2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Location = new System.Drawing.Point(173, 92);
            lblPassword.MinimumSize = new System.Drawing.Size(75, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new System.Drawing.Size(75, 13);
            lblPassword.TabIndex = 28;
            lblPassword.Text = "Password";
            lblPassword.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtPassword
            // 
            txtPassword.Location = new System.Drawing.Point(266, 88);
            txtPassword.MaxLength = 128;
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new System.Drawing.Size(153, 20);
            txtPassword.TabIndex = 27;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // Label15
            // 
            Label15.AutoSize = true;
            Label15.Location = new System.Drawing.Point(205, 62);
            Label15.Name = "Label15";
            Label15.Size = new System.Drawing.Size(31, 13);
            Label15.TabIndex = 26;
            Label15.Text = "MB/s";
            // 
            // txtLimiteVelocidad
            // 
            txtLimiteVelocidad.Location = new System.Drawing.Point(144, 58);
            txtLimiteVelocidad.MaxLength = 10;
            txtLimiteVelocidad.Name = "txtLimiteVelocidad";
            txtLimiteVelocidad.Size = new System.Drawing.Size(55, 20);
            txtLimiteVelocidad.TabIndex = 3;
            // 
            // chkLimitarVelocidad
            // 
            chkLimitarVelocidad.AutoSize = true;
            chkLimitarVelocidad.Location = new System.Drawing.Point(13, 61);
            chkLimitarVelocidad.MinimumSize = new System.Drawing.Size(120, 0);
            chkLimitarVelocidad.Name = "chkLimitarVelocidad";
            chkLimitarVelocidad.Size = new System.Drawing.Size(125, 17);
            chkLimitarVelocidad.TabIndex = 2;
            chkLimitarVelocidad.Text = "Limitar la velocidad a";
            chkLimitarVelocidad.UseVisualStyleBackColor = true;
            // 
            // LinkLabel1
            // 
            LinkLabel1.AutoSize = true;
            LinkLabel1.Location = new System.Drawing.Point(289, 62);
            LinkLabel1.MinimumSize = new System.Drawing.Size(120, 0);
            LinkLabel1.Name = "LinkLabel1";
            LinkLabel1.Size = new System.Drawing.Size(121, 13);
            LinkLabel1.TabIndex = 4;
            LinkLabel1.TabStop = true;
            LinkLabel1.Text = "Nota sobre las opciones";
            LinkLabel1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // btnGuardar
            // 
            btnGuardar.Location = new System.Drawing.Point(12, 275);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new System.Drawing.Size(75, 23);
            btnGuardar.TabIndex = 0;
            btnGuardar.Text = "Guardar";
            btnGuardar.UseVisualStyleBackColor = true;
            // 
            // btnCancelar
            // 
            btnCancelar.Location = new System.Drawing.Point(362, 275);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new System.Drawing.Size(75, 23);
            btnCancelar.TabIndex = 1;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = true;
            // 
            // PropiedadesDescarga
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(446, 310);
            Controls.Add(btnCancelar);
            Controls.Add(btnGuardar);
            Controls.Add(GroupBox2);
            Controls.Add(GroupBox1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = My.Resources.Resources.icono;
            Name = "PropiedadesDescarga";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Propiedades";
            GroupBox1.ResumeLayout(false);
            GroupBox1.PerformLayout();
            GroupBox2.ResumeLayout(false);
            GroupBox2.PerformLayout();
            Load += new EventHandler(PropiedadesDescarga_Load);
            ResumeLayout(false);

        }
        internal System.Windows.Forms.CheckBox chkUnZip;
        internal System.Windows.Forms.Button btnExaminar;
        internal System.Windows.Forms.TextBox txtRuta;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.TextBox txtMD5;
        internal System.Windows.Forms.TextBox txtTamano;
        internal System.Windows.Forms.TextBox txtNombre;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.LinkLabel LinkLabel1;
        internal System.Windows.Forms.Button btnGuardar;
        internal System.Windows.Forms.Button btnCancelar;
        internal System.Windows.Forms.TextBox txtUrl;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.TextBox txtLimiteVelocidad;
        internal System.Windows.Forms.CheckBox chkLimitarVelocidad;
        internal System.Windows.Forms.Label Label15;
        internal System.Windows.Forms.LinkLabel LinkLabel2;
        internal System.Windows.Forms.Label lblPassword;
        internal System.Windows.Forms.TextBox txtPassword;
    }
}