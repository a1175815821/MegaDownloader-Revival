using System;
using System.Runtime.CompilerServices;

namespace MegaDownloader
{
    // 
    // Created by SharpDevelop.
    // User: asolino
    // Date: 25/03/2013
    // Time: 9:18
    // 
    // To change this template use Tools | Options | Coding | Edit Standard Headers.
    // 
    public partial class ELCAccountControl
    {
        /// <summary>
	/// 	''' Designer variable used to keep track of non-visual components.
	/// 	''' </summary>
        private System.ComponentModel.IContainer components;

        /// <summary>
	/// 	''' Disposes resources used by the control.
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(ELCAccountControl));
            GroupBox10 = new System.Windows.Forms.GroupBox();
            lblInfoELC = new System.Windows.Forms.Label();
            GroupBox9 = new System.Windows.Forms.GroupBox();
            _lklELCUrl = new System.Windows.Forms.LinkLabel();
            _lklELCUrl.MouseHover += new EventHandler(lklELCUrl_MouseHover);
            _lklELCUrl.MouseLeave += new EventHandler(lklELCUrl_MouseLeave);
            _lklELCUrl.Click += new EventHandler(lklELCUrl_Click);
            txtELCAccountURL = new System.Windows.Forms.TextBox();
            lblELCUrl = new System.Windows.Forms.Label();
            _lklELCMainAccount = new System.Windows.Forms.LinkLabel();
            _lklELCMainAccount.MouseHover += new EventHandler(lklELCMainAccount_MouseHover);
            _lklELCMainAccount.MouseLeave += new EventHandler(lklELCMainAccount_MouseLeave);
            _lklELCMainAccount.Click += new EventHandler(lklELCMainAccount_Click);
            chkELCAccountMain = new System.Windows.Forms.CheckBox();
            _btnELCAccountAddNew = new System.Windows.Forms.Button();
            _btnELCAccountAddNew.Click += new EventHandler(btnELCAccountAddNew_Click);
            _btnELCAccountModify = new System.Windows.Forms.Button();
            _btnELCAccountModify.Click += new EventHandler(btnELCAccountModify_Click);
            _btnELCAccountDelete = new System.Windows.Forms.Button();
            _btnELCAccountDelete.Click += new EventHandler(btnELCAccountDelete_Click);
            _lklELCAliasAccount = new System.Windows.Forms.LinkLabel();
            _lklELCAliasAccount.MouseHover += new EventHandler(lklELCAliasAccount_MouseHover);
            _lklELCAliasAccount.MouseLeave += new EventHandler(lklELCAliasAccount_MouseLeave);
            _lklELCAliasAccount.Click += new EventHandler(lklELCAliasAccount_Click);
            Label3 = new System.Windows.Forms.Label();
            txtELCAccountAlias = new System.Windows.Forms.TextBox();
            _lklELCShowPassword = new System.Windows.Forms.LinkLabel();
            _lklELCShowPassword.MouseHover += new EventHandler(lklELCShowPassword_MouseHover);
            _lklELCShowPassword.MouseLeave += new EventHandler(lklELCShowPassword_MouseLeave);
            _lklELCShowPassword.Click += new EventHandler(lklELCShowPassword_Click);
            _chkELCAccountShowPassword = new System.Windows.Forms.CheckBox();
            _chkELCAccountShowPassword.CheckedChanged += new EventHandler(chkELCAccountShowPassword_CheckedChanged);
            txtELCAccountKey = new System.Windows.Forms.TextBox();
            txtELCAccountUser = new System.Windows.Forms.TextBox();
            lblELCApiKey = new System.Windows.Forms.Label();
            Label7 = new System.Windows.Forms.Label();
            GroupBox8 = new System.Windows.Forms.GroupBox();
            _dgELCUsers = new System.Windows.Forms.DataGridView();
            _dgELCUsers.Paint += new System.Windows.Forms.PaintEventHandler(dgELCUsers_Paint);
            _dgELCUsers.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(dgSELUsers_CellClick);
            GroupBox10.SuspendLayout();
            GroupBox9.SuspendLayout();
            GroupBox8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_dgELCUsers).BeginInit();
            SuspendLayout();
            // 
            // GroupBox10
            // 
            GroupBox10.Controls.Add(lblInfoELC);
            GroupBox10.Location = new System.Drawing.Point(3, 3);
            GroupBox10.Name = "GroupBox10";
            GroupBox10.Size = new System.Drawing.Size(607, 99);
            GroupBox10.TabIndex = 5;
            GroupBox10.TabStop = false;
            GroupBox10.Text = "Información";
            // 
            // lblInfoELC
            // 
            lblInfoELC.AutoSize = true;
            lblInfoELC.Location = new System.Drawing.Point(7, 20);
            lblInfoELC.MaximumSize = new System.Drawing.Size(590, 0);
            lblInfoELC.Name = "lblInfoELC";
            lblInfoELC.Size = new System.Drawing.Size(573, 65);
            lblInfoELC.TabIndex = 0;
            lblInfoELC.Text = resources.GetString("lblInfoELC.Text");
            // 
            // GroupBox9
            // 
            GroupBox9.Controls.Add(_lklELCUrl);
            GroupBox9.Controls.Add(txtELCAccountURL);
            GroupBox9.Controls.Add(lblELCUrl);
            GroupBox9.Controls.Add(_lklELCMainAccount);
            GroupBox9.Controls.Add(chkELCAccountMain);
            GroupBox9.Controls.Add(_btnELCAccountAddNew);
            GroupBox9.Controls.Add(_btnELCAccountModify);
            GroupBox9.Controls.Add(_btnELCAccountDelete);
            GroupBox9.Controls.Add(_lklELCAliasAccount);
            GroupBox9.Controls.Add(Label3);
            GroupBox9.Controls.Add(txtELCAccountAlias);
            GroupBox9.Controls.Add(_lklELCShowPassword);
            GroupBox9.Controls.Add(_chkELCAccountShowPassword);
            GroupBox9.Controls.Add(txtELCAccountKey);
            GroupBox9.Controls.Add(txtELCAccountUser);
            GroupBox9.Controls.Add(lblELCApiKey);
            GroupBox9.Controls.Add(Label7);
            GroupBox9.Location = new System.Drawing.Point(261, 108);
            GroupBox9.Name = "GroupBox9";
            GroupBox9.Size = new System.Drawing.Size(349, 264);
            GroupBox9.TabIndex = 4;
            GroupBox9.TabStop = false;
            GroupBox9.Text = "Datos de cuenta ELC";
            // 
            // lklELCUrl
            // 
            _lklELCUrl.AutoSize = true;
            _lklELCUrl.Location = new System.Drawing.Point(103, 48);
            _lklELCUrl.Name = "_lklELCUrl";
            _lklELCUrl.Size = new System.Drawing.Size(19, 13);
            _lklELCUrl.TabIndex = 27;
            _lklELCUrl.TabStop = true;
            _lklELCUrl.Text = "[?]";
            // 
            // txtELCAccountURL
            // 
            txtELCAccountURL.Location = new System.Drawing.Point(128, 45);
            txtELCAccountURL.Name = "txtELCAccountURL";
            txtELCAccountURL.Size = new System.Drawing.Size(215, 20);
            txtELCAccountURL.TabIndex = 26;
            // 
            // lblELCUrl
            // 
            lblELCUrl.AutoSize = true;
            lblELCUrl.Location = new System.Drawing.Point(39, 48);
            lblELCUrl.MinimumSize = new System.Drawing.Size(65, 0);
            lblELCUrl.Name = "lblELCUrl";
            lblELCUrl.Size = new System.Drawing.Size(65, 13);
            lblELCUrl.TabIndex = 25;
            lblELCUrl.Text = "URL:";
            lblELCUrl.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lklELCMainAccount
            // 
            _lklELCMainAccount.AutoSize = true;
            _lklELCMainAccount.Location = new System.Drawing.Point(148, 123);
            _lklELCMainAccount.Name = "_lklELCMainAccount";
            _lklELCMainAccount.Size = new System.Drawing.Size(19, 13);
            _lklELCMainAccount.TabIndex = 24;
            _lklELCMainAccount.TabStop = true;
            _lklELCMainAccount.Text = "[?]";
            // 
            // chkELCAccountMain
            // 
            chkELCAccountMain.AutoSize = true;
            chkELCAccountMain.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            chkELCAccountMain.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            chkELCAccountMain.Location = new System.Drawing.Point(22, 122);
            chkELCAccountMain.MinimumSize = new System.Drawing.Size(120, 0);
            chkELCAccountMain.Name = "chkELCAccountMain";
            chkELCAccountMain.Size = new System.Drawing.Size(120, 17);
            chkELCAccountMain.TabIndex = 23;
            chkELCAccountMain.Text = "Cuenta principal";
            chkELCAccountMain.TextAlign = System.Drawing.ContentAlignment.TopRight;
            chkELCAccountMain.UseVisualStyleBackColor = false;
            // 
            // btnELCAccountAddNew
            // 
            _btnELCAccountAddNew.Location = new System.Drawing.Point(8, 235);
            _btnELCAccountAddNew.Name = "_btnELCAccountAddNew";
            _btnELCAccountAddNew.Size = new System.Drawing.Size(96, 23);
            _btnELCAccountAddNew.TabIndex = 20;
            _btnELCAccountAddNew.Text = "Add new";
            _btnELCAccountAddNew.UseVisualStyleBackColor = true;
            // 
            // btnELCAccountModify
            // 
            _btnELCAccountModify.Location = new System.Drawing.Point(128, 235);
            _btnELCAccountModify.Name = "_btnELCAccountModify";
            _btnELCAccountModify.Size = new System.Drawing.Size(96, 23);
            _btnELCAccountModify.TabIndex = 21;
            _btnELCAccountModify.Text = "Modify";
            _btnELCAccountModify.UseVisualStyleBackColor = true;
            // 
            // btnELCAccountDelete
            // 
            _btnELCAccountDelete.Location = new System.Drawing.Point(247, 235);
            _btnELCAccountDelete.Name = "_btnELCAccountDelete";
            _btnELCAccountDelete.Size = new System.Drawing.Size(96, 23);
            _btnELCAccountDelete.TabIndex = 22;
            _btnELCAccountDelete.Text = "Delete";
            _btnELCAccountDelete.UseVisualStyleBackColor = true;
            // 
            // lklELCAliasAccount
            // 
            _lklELCAliasAccount.AutoSize = true;
            _lklELCAliasAccount.Location = new System.Drawing.Point(103, 22);
            _lklELCAliasAccount.Name = "_lklELCAliasAccount";
            _lklELCAliasAccount.Size = new System.Drawing.Size(19, 13);
            _lklELCAliasAccount.TabIndex = 12;
            _lklELCAliasAccount.TabStop = true;
            _lklELCAliasAccount.Text = "[?]";
            // 
            // Label3
            // 
            Label3.AutoSize = true;
            Label3.Location = new System.Drawing.Point(39, 22);
            Label3.MinimumSize = new System.Drawing.Size(65, 0);
            Label3.Name = "Label3";
            Label3.Size = new System.Drawing.Size(65, 13);
            Label3.TabIndex = 11;
            Label3.Text = "Alias:";
            Label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtELCAccountAlias
            // 
            txtELCAccountAlias.Location = new System.Drawing.Point(128, 19);
            txtELCAccountAlias.Name = "txtELCAccountAlias";
            txtELCAccountAlias.Size = new System.Drawing.Size(215, 20);
            txtELCAccountAlias.TabIndex = 13;
            // 
            // lklELCShowPassword
            // 
            _lklELCShowPassword.AutoSize = true;
            _lklELCShowPassword.Location = new System.Drawing.Point(320, 123);
            _lklELCShowPassword.Name = "_lklELCShowPassword";
            _lklELCShowPassword.Size = new System.Drawing.Size(19, 13);
            _lklELCShowPassword.TabIndex = 19;
            _lklELCShowPassword.TabStop = true;
            _lklELCShowPassword.Text = "[?]";
            // 
            // chkELCAccountShowPassword
            // 
            _chkELCAccountShowPassword.AutoSize = true;
            _chkELCAccountShowPassword.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            _chkELCAccountShowPassword.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            _chkELCAccountShowPassword.Location = new System.Drawing.Point(198, 123);
            _chkELCAccountShowPassword.MinimumSize = new System.Drawing.Size(120, 0);
            _chkELCAccountShowPassword.Name = "_chkELCAccountShowPassword";
            _chkELCAccountShowPassword.Size = new System.Drawing.Size(120, 17);
            _chkELCAccountShowPassword.TabIndex = 18;
            _chkELCAccountShowPassword.Text = "Mostrar contraseña";
            _chkELCAccountShowPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            _chkELCAccountShowPassword.UseVisualStyleBackColor = false;
            // 
            // txtELCAccountKey
            // 
            txtELCAccountKey.Location = new System.Drawing.Point(128, 97);
            txtELCAccountKey.MaxLength = 128;
            txtELCAccountKey.Name = "txtELCAccountKey";
            txtELCAccountKey.Size = new System.Drawing.Size(215, 20);
            txtELCAccountKey.TabIndex = 17;
            txtELCAccountKey.UseSystemPasswordChar = true;
            // 
            // txtELCAccountUser
            // 
            txtELCAccountUser.Location = new System.Drawing.Point(128, 71);
            txtELCAccountUser.Name = "txtELCAccountUser";
            txtELCAccountUser.Size = new System.Drawing.Size(215, 20);
            txtELCAccountUser.TabIndex = 15;
            // 
            // lblELCApiKey
            // 
            lblELCApiKey.AutoSize = true;
            lblELCApiKey.Location = new System.Drawing.Point(47, 100);
            lblELCApiKey.MinimumSize = new System.Drawing.Size(75, 0);
            lblELCApiKey.Name = "lblELCApiKey";
            lblELCApiKey.Size = new System.Drawing.Size(75, 13);
            lblELCApiKey.TabIndex = 16;
            lblELCApiKey.Text = "API-Key:";
            lblELCApiKey.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label7
            // 
            Label7.AutoSize = true;
            Label7.Location = new System.Drawing.Point(57, 74);
            Label7.MinimumSize = new System.Drawing.Size(65, 0);
            Label7.Name = "Label7";
            Label7.Size = new System.Drawing.Size(65, 13);
            Label7.TabIndex = 14;
            Label7.Text = "Usuario:";
            Label7.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // GroupBox8
            // 
            GroupBox8.Controls.Add(_dgELCUsers);
            GroupBox8.Location = new System.Drawing.Point(3, 108);
            GroupBox8.Name = "GroupBox8";
            GroupBox8.Size = new System.Drawing.Size(252, 264);
            GroupBox8.TabIndex = 3;
            GroupBox8.TabStop = false;
            GroupBox8.Text = "Cuentas ELC";
            // 
            // dgELCUsers
            // 
            _dgELCUsers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _dgELCUsers.Location = new System.Drawing.Point(7, 19);
            _dgELCUsers.Name = "_dgELCUsers";
            _dgELCUsers.Size = new System.Drawing.Size(239, 239);
            _dgELCUsers.TabIndex = 1;
            // 
            // ELCAccountControl
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(GroupBox10);
            Controls.Add(GroupBox9);
            Controls.Add(GroupBox8);
            Name = "ELCAccountControl";
            Size = new System.Drawing.Size(615, 375);
            GroupBox10.ResumeLayout(false);
            GroupBox10.PerformLayout();
            GroupBox9.ResumeLayout(false);
            GroupBox9.PerformLayout();
            GroupBox8.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_dgELCUsers).EndInit();
            ResumeLayout(false);

        }
        private System.Windows.Forms.DataGridView _dgELCUsers;

        internal virtual System.Windows.Forms.DataGridView dgELCUsers
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _dgELCUsers;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_dgELCUsers != null)
                {
                    _dgELCUsers.Paint -= dgELCUsers_Paint;
                    _dgELCUsers.CellClick -= dgSELUsers_CellClick;
                }

                _dgELCUsers = value;
                if (_dgELCUsers != null)
                {
                    _dgELCUsers.Paint += dgELCUsers_Paint;
                    _dgELCUsers.CellClick += dgSELUsers_CellClick;
                }
            }
        }
        internal System.Windows.Forms.GroupBox GroupBox8;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.Label lblELCApiKey;
        internal System.Windows.Forms.TextBox txtELCAccountUser;
        internal System.Windows.Forms.TextBox txtELCAccountKey;
        private System.Windows.Forms.CheckBox _chkELCAccountShowPassword;

        internal virtual System.Windows.Forms.CheckBox chkELCAccountShowPassword
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _chkELCAccountShowPassword;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_chkELCAccountShowPassword != null)
                {
                    _chkELCAccountShowPassword.CheckedChanged -= chkELCAccountShowPassword_CheckedChanged;
                }

                _chkELCAccountShowPassword = value;
                if (_chkELCAccountShowPassword != null)
                {
                    _chkELCAccountShowPassword.CheckedChanged += chkELCAccountShowPassword_CheckedChanged;
                }
            }
        }
        private System.Windows.Forms.LinkLabel _lklELCShowPassword;

        internal virtual System.Windows.Forms.LinkLabel lklELCShowPassword
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _lklELCShowPassword;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_lklELCShowPassword != null)
                {
                    _lklELCShowPassword.MouseHover -= lklELCShowPassword_MouseHover;
                    _lklELCShowPassword.MouseLeave -= lklELCShowPassword_MouseLeave;
                    _lklELCShowPassword.Click -= lklELCShowPassword_Click;
                }

                _lklELCShowPassword = value;
                if (_lklELCShowPassword != null)
                {
                    _lklELCShowPassword.MouseHover += lklELCShowPassword_MouseHover;
                    _lklELCShowPassword.MouseLeave += lklELCShowPassword_MouseLeave;
                    _lklELCShowPassword.Click += lklELCShowPassword_Click;
                }
            }
        }
        internal System.Windows.Forms.TextBox txtELCAccountAlias;
        internal System.Windows.Forms.Label Label3;
        private System.Windows.Forms.LinkLabel _lklELCAliasAccount;

        internal virtual System.Windows.Forms.LinkLabel lklELCAliasAccount
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _lklELCAliasAccount;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_lklELCAliasAccount != null)
                {
                    _lklELCAliasAccount.MouseHover -= lklELCAliasAccount_MouseHover;
                    _lklELCAliasAccount.MouseLeave -= lklELCAliasAccount_MouseLeave;
                    _lklELCAliasAccount.Click -= lklELCAliasAccount_Click;
                }

                _lklELCAliasAccount = value;
                if (_lklELCAliasAccount != null)
                {
                    _lklELCAliasAccount.MouseHover += lklELCAliasAccount_MouseHover;
                    _lklELCAliasAccount.MouseLeave += lklELCAliasAccount_MouseLeave;
                    _lklELCAliasAccount.Click += lklELCAliasAccount_Click;
                }
            }
        }
        private System.Windows.Forms.Button _btnELCAccountDelete;

        internal virtual System.Windows.Forms.Button btnELCAccountDelete
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnELCAccountDelete;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnELCAccountDelete != null)
                {
                    _btnELCAccountDelete.Click -= btnELCAccountDelete_Click;
                }

                _btnELCAccountDelete = value;
                if (_btnELCAccountDelete != null)
                {
                    _btnELCAccountDelete.Click += btnELCAccountDelete_Click;
                }
            }
        }
        private System.Windows.Forms.Button _btnELCAccountModify;

        internal virtual System.Windows.Forms.Button btnELCAccountModify
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnELCAccountModify;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnELCAccountModify != null)
                {
                    _btnELCAccountModify.Click -= btnELCAccountModify_Click;
                }

                _btnELCAccountModify = value;
                if (_btnELCAccountModify != null)
                {
                    _btnELCAccountModify.Click += btnELCAccountModify_Click;
                }
            }
        }
        private System.Windows.Forms.Button _btnELCAccountAddNew;

        internal virtual System.Windows.Forms.Button btnELCAccountAddNew
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnELCAccountAddNew;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnELCAccountAddNew != null)
                {
                    _btnELCAccountAddNew.Click -= btnELCAccountAddNew_Click;
                }

                _btnELCAccountAddNew = value;
                if (_btnELCAccountAddNew != null)
                {
                    _btnELCAccountAddNew.Click += btnELCAccountAddNew_Click;
                }
            }
        }
        internal System.Windows.Forms.CheckBox chkELCAccountMain;
        private System.Windows.Forms.LinkLabel _lklELCMainAccount;

        internal virtual System.Windows.Forms.LinkLabel lklELCMainAccount
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _lklELCMainAccount;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_lklELCMainAccount != null)
                {
                    _lklELCMainAccount.MouseHover -= lklELCMainAccount_MouseHover;
                    _lklELCMainAccount.MouseLeave -= lklELCMainAccount_MouseLeave;
                    _lklELCMainAccount.Click -= lklELCMainAccount_Click;
                }

                _lklELCMainAccount = value;
                if (_lklELCMainAccount != null)
                {
                    _lklELCMainAccount.MouseHover += lklELCMainAccount_MouseHover;
                    _lklELCMainAccount.MouseLeave += lklELCMainAccount_MouseLeave;
                    _lklELCMainAccount.Click += lklELCMainAccount_Click;
                }
            }
        }
        internal System.Windows.Forms.Label lblELCUrl;
        internal System.Windows.Forms.TextBox txtELCAccountURL;
        private System.Windows.Forms.LinkLabel _lklELCUrl;

        internal virtual System.Windows.Forms.LinkLabel lklELCUrl
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _lklELCUrl;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_lklELCUrl != null)
                {
                    _lklELCUrl.MouseHover -= lklELCUrl_MouseHover;
                    _lklELCUrl.MouseLeave -= lklELCUrl_MouseLeave;
                    _lklELCUrl.Click -= lklELCUrl_Click;
                }

                _lklELCUrl = value;
                if (_lklELCUrl != null)
                {
                    _lklELCUrl.MouseHover += lklELCUrl_MouseHover;
                    _lklELCUrl.MouseLeave += lklELCUrl_MouseLeave;
                    _lklELCUrl.Click += lklELCUrl_Click;
                }
            }
        }
        internal System.Windows.Forms.GroupBox GroupBox9;
        internal System.Windows.Forms.Label lblInfoELC;
        internal System.Windows.Forms.GroupBox GroupBox10;
    }
}