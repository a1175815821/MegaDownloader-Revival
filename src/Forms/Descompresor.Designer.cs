using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class Descompresor : System.Windows.Forms.Form
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
            gbEstado = new System.Windows.Forms.GroupBox();
            lblTamano = new System.Windows.Forms.Label();
            Label6 = new System.Windows.Forms.Label();
            lblActual = new System.Windows.Forms.Label();
            Label4 = new System.Windows.Forms.Label();
            lblFichero = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            GroupBox2 = new System.Windows.Forms.GroupBox();
            txtCola = new System.Windows.Forms.TextBox();
            Label1 = new System.Windows.Forms.Label();
            gbEstado.SuspendLayout();
            GroupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // gbEstado
            // 
            gbEstado.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbEstado.Controls.Add(lblTamano);
            gbEstado.Controls.Add(Label6);
            gbEstado.Controls.Add(lblActual);
            gbEstado.Controls.Add(Label4);
            gbEstado.Controls.Add(lblFichero);
            gbEstado.Controls.Add(Label2);
            gbEstado.Location = new System.Drawing.Point(12, 48);
            gbEstado.Name = "gbEstado";
            gbEstado.Size = new System.Drawing.Size(504, 81);
            gbEstado.TabIndex = 0;
            gbEstado.TabStop = false;
            gbEstado.Text = "Estado";
            // 
            // lblTamano
            // 
            lblTamano.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            lblTamano.AutoSize = true;
            lblTamano.Location = new System.Drawing.Point(390, 50);
            lblTamano.MinimumSize = new System.Drawing.Size(100, 0);
            lblTamano.Name = "lblTamano";
            lblTamano.Size = new System.Drawing.Size(100, 13);
            lblTamano.TabIndex = 5;
            lblTamano.Text = "  -";
            // 
            // Label6
            // 
            Label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            Label6.AutoSize = true;
            Label6.Location = new System.Drawing.Point(343, 50);
            Label6.Name = "Label6";
            Label6.Size = new System.Drawing.Size(49, 13);
            Label6.TabIndex = 4;
            Label6.Text = "Tamaño:";
            // 
            // lblActual
            // 
            lblActual.AutoSize = true;
            lblActual.Location = new System.Drawing.Point(57, 50);
            lblActual.Name = "lblActual";
            lblActual.Size = new System.Drawing.Size(10, 13);
            lblActual.TabIndex = 3;
            lblActual.Text = "-";
            // 
            // Label4
            // 
            Label4.AutoSize = true;
            Label4.Location = new System.Drawing.Point(6, 50);
            Label4.Name = "Label4";
            Label4.Size = new System.Drawing.Size(40, 13);
            Label4.TabIndex = 2;
            Label4.Text = "Actual:";
            // 
            // lblFichero
            // 
            lblFichero.AutoSize = true;
            lblFichero.Location = new System.Drawing.Point(57, 25);
            lblFichero.Name = "lblFichero";
            lblFichero.Size = new System.Drawing.Size(10, 13);
            lblFichero.TabIndex = 1;
            lblFichero.Text = "-";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(6, 25);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(45, 13);
            Label2.TabIndex = 0;
            Label2.Text = "Fichero:";
            // 
            // GroupBox2
            // 
            GroupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            GroupBox2.Controls.Add(txtCola);
            GroupBox2.Location = new System.Drawing.Point(12, 135);
            GroupBox2.Name = "GroupBox2";
            GroupBox2.Size = new System.Drawing.Size(504, 88);
            GroupBox2.TabIndex = 1;
            GroupBox2.TabStop = false;
            GroupBox2.Text = "Cola de descompresión";
            // 
            // txtCola
            // 
            txtCola.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            txtCola.Enabled = false;
            txtCola.Location = new System.Drawing.Point(6, 19);
            txtCola.Multiline = true;
            txtCola.Name = "txtCola";
            txtCola.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtCola.Size = new System.Drawing.Size(492, 63);
            txtCola.TabIndex = 0;
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 9);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(388, 26);
            Label1.TabIndex = 2;
            Label1.Text = "Esta pantalla le permite ver el estado de los ficheros pendientes de descomprimir" + " " + '\r' + '\n' + "(si ha activado la opción de extraer automáticamente).";
            // 
            // Descompresor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(528, 235);
            Controls.Add(Label1);
            Controls.Add(GroupBox2);
            Controls.Add(gbEstado);
            Icon = My.Resources.Resources.icono;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(400, 250);
            Name = "Descompresor";
            Text = "Descompresor de ficheros";
            gbEstado.ResumeLayout(false);
            gbEstado.PerformLayout();
            GroupBox2.ResumeLayout(false);
            GroupBox2.PerformLayout();
            Load += new EventHandler(Descompresor_Load);
            Shown += new EventHandler(Descompresor_Shown);
            FormClosed += new System.Windows.Forms.FormClosedEventHandler(Cerrando);
            ResumeLayout(false);
            PerformLayout();

        }
        internal System.Windows.Forms.GroupBox gbEstado;
        internal System.Windows.Forms.Label lblTamano;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Label lblActual;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.Label lblFichero;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.TextBox txtCola;
        internal System.Windows.Forms.Label Label1;
    }
}