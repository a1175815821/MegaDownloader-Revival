using System;
using System.Linq;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{
    public partial class ELCAccountControl : System.Windows.Forms.UserControl
    {

        public const string PASSWORDDEFECTO = "*****";
        private const string PREFIX_MAIN_ACCOUNT = "[*] ";

        public Configuracion Config;
        private ELCAccountHelper ELCAccountH;
        private ToolTip t;
        private Font gridHeaderFont;



        public ELCAccountControl()
        {
            // The Me.InitializeComponent call is required for Windows Forms designer support.
            InitializeComponent();
            Disposed += (s, e) =>
                {
                    Cerrar();
                    if (gridHeaderFont is not null)
                        gridHeaderFont.Dispose();
                    if (t is not null)
                        t.Dispose();
                };

        }

        public void Cerrar()
        {
            if (ELCAccountH is not null)
            {
                ELCAccountH.Dispose();
                ELCAccountH = null;
            }
        }

        public void SaveToConfig(ref Configuracion Config)
        {
            if (ELCAccountH is not null)
                ELCAccountH.SaveToConfig(ref Config);
        }



        public void CargarDatos()
        {
            // SEL Accounts
            if (Config is not null)
            {
                if (ELCAccountH is not null)
                {
                    try
                    {
                        ELCAccountH.Dispose();
                    }
                    catch
                    {
                    }
                    ELCAccountH = null;
                }
                ELCAccountH = new ELCAccountHelper(ref Config);
                dgELCUsers.BackgroundColor = ThemeManager.GetColor("AltBack");
                {
                    var withBlock = dgELCUsers.ColumnHeadersDefaultCellStyle;
                    withBlock.BackColor = ThemeManager.GetColor("ControlBack");
                    withBlock.ForeColor = ThemeManager.GetColor("Fore");
                    if (gridHeaderFont is null)
                        gridHeaderFont = new Font(dgELCUsers.Font, FontStyle.Bold);
                    withBlock.Font = gridHeaderFont;
                }
                {
                    var withBlock1 = dgELCUsers.DefaultCellStyle;
                    withBlock1.BackColor = ThemeManager.GetColor("Back");
                    withBlock1.ForeColor = ThemeManager.GetColor("Fore");
                    withBlock1.SelectionBackColor = ThemeManager.GetColor("Selection");
                    withBlock1.SelectionForeColor = ThemeManager.GetColor("SelectionFore");
                }
                {
                    var withBlock2 = dgELCUsers.AlternatingRowsDefaultCellStyle;
                    withBlock2.BackColor = ThemeManager.GetColor("AltBack");
                    withBlock2.ForeColor = ThemeManager.GetColor("Fore");
                    withBlock2.SelectionBackColor = ThemeManager.GetColor("Selection");
                    withBlock2.SelectionForeColor = ThemeManager.GetColor("SelectionFore");
                }
                dgELCUsers.GridColor = ThemeManager.GetColor("Border");
                dgELCUsers.EnableHeadersVisualStyles = false;
                FillELCAccountDataGridView();
                dgELCUsers.ClearSelection();

                txtELCAccountKey.Enabled = false;
                txtELCAccountUser.Enabled = false;
                txtELCAccountURL.Enabled = false;
                txtELCAccountAlias.Enabled = false;
                chkELCAccountShowPassword.Enabled = false;
                chkELCAccountMain.Enabled = false;
                btnELCAccountAddNew.Enabled = true;
                btnELCAccountDelete.Enabled = false;
                btnELCAccountModify.Enabled = false;
                ELCURLBeingEdited = "";
                ELCAliasBeingEdited = "";
                DataGridViewTextBoxColumn dbColumn2 = (DataGridViewTextBoxColumn)dgELCUsers.Columns[0];
                dbColumn2.Name = "E-mail";
                dbColumn2.ReadOnly = true;
                dbColumn2.Resizable = DataGridViewTriState.False;
                dgELCUsers.AutoSizeColumnsMode = (System.Windows.Forms.DataGridViewAutoSizeColumnsMode)Conversions.ToInteger(DataGridViewAutoSizeColumnsMode.Fill);
                dgELCUsers.RowHeadersVisible = false;
                dgELCUsers.ColumnHeadersVisible = false;

                Translate();
            }

        }

        private void Translate()
        {

            chkELCAccountShowPassword.Text = Language.GetText("Show password");

            Label7.Text = Language.GetText("User") + ":";
            btnELCAccountDelete.Text = Language.GetText("Delete");
            btnELCAccountModify.Text = Language.GetText("Modify");
            chkELCAccountMain.Text = Language.GetText("Main account");
            Label3.Text = Language.GetText("Alias") + ":";
            btnELCAccountAddNew.Text = Language.GetText("Add new");
            lblELCUrl.Text = Language.GetText("URL") + ":";
            GroupBox10.Text = Language.GetText("Information");
            lblELCUrl.Text = Language.GetText("URL") + ":";
            GroupBox8.Text = Language.GetText("ELC Accounts");
            GroupBox9.Text = Language.GetText("ELC Account Info");
            lblInfoELC.Text = Language.GetText("ELC Desc Info");
            lblELCApiKey.Text = Language.GetText("API-Key") + ":";

        }

        private void chkELCAccountShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            txtELCAccountKey.UseSystemPasswordChar = !chkELCAccountShowPassword.Checked;
        }


        #region DataGridView SEL Accounts



        private string ELCURLBeingEdited = "";
        private string ELCAliasBeingEdited = "";
        private string ELCAccountAction = "";
        private void FillELCAccountDataGridView()
        {
            dgELCUsers.DataSource = ELCAccountH.GetAccounts().OrderBy(c =>
                {
                    string t;
                    if (c.DefaultAccount)
                    {
                        t = "A" + c.Alias;
                    }
                    else
                    {
                        t = "Z" + c.Alias;
                    }
                    return t;
                }).Select((c) => new { Email = (c.DefaultAccount ? PREFIX_MAIN_ACCOUNT : "") + c.Alias }).ToList();



        }


        private void dgELCUsers_Paint(object sender, PaintEventArgs e)
        {
            DataGridView sndr = (DataGridView)sender;

            if (Config is not null && sndr is not null && sndr.Rows.Count == 0)
            {
                // <-- if there are no rows in the DataGridView when it paints, then it will create your message

                using (var emptyBrush = new SolidBrush(ThemeManager.GetColor("Fore")))
                {
                    using (var f = new Font(dgELCUsers.Font.FontFamily, dgELCUsers.Font.Size, FontStyle.Italic))
                    {
                        e.Graphics.DrawString(Language.GetText("Account list empty"), f, emptyBrush, new PointF(55, 10));
                    }
                }
            }
        }

        private void dgSELUsers_CellClick(object sender, System.Windows.Forms.DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            // N-P3-1: in NEW mode (user pressed "Add new" and started typing) a mis-click
            // on the grid used to silently overwrite the five input boxes with the clicked
            // account's data and flip the button back to "Add new" — the half-entered
            // account was gone with no confirmation. Confirm when there is unsaved input;
            // with an empty form, leave NEW mode without a dialog.
            if (ELCAccountAction == "NEW")
            {
                if (!string.IsNullOrEmpty(txtELCAccountURL.Text) || !string.IsNullOrEmpty(txtELCAccountUser.Text) || !string.IsNullOrEmpty(txtELCAccountAlias.Text) || !string.IsNullOrEmpty(txtELCAccountKey.Text))
                {
                    if (MessageBox.Show(Language.GetText("ELC discard new account confirm"), Language.GetText("Note"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != System.Windows.Forms.DialogResult.Yes)
                    {
                        return;
                    }
                }
            }
            string aliasName = Conversions.ToString(dgELCUsers.Rows[e.RowIndex].Cells[0].Value);
            // Private Sub dgSELUsers_CellClick(ByVal sender As Object, ByVal e As System.EventArgs) Handles dgELCUsers.SelectionChanged
            // 
            // If dgELCUsers.SelectedCells Is Nothing OrElse dgELCUsers.SelectedCells.Count = 0 Then Exit Sub
            // Dim aliasName As String = CStr(dgELCUsers.SelectedCells(0).value)

            if (aliasName.StartsWith(PREFIX_MAIN_ACCOUNT))
                aliasName = aliasName.Substring(PREFIX_MAIN_ACCOUNT.Length);
            var account = ELCAccountH.GetAccountDetailsByAlias(aliasName);
            if (account is not null)
            {
                txtELCAccountUser.Text = Criptografia.ToInsecureString(account.User);
                txtELCAccountAlias.Text = account.Alias;
                txtELCAccountURL.Text = account.URL;
                txtELCAccountKey.Text = PASSWORDDEFECTO;
                ELCURLBeingEdited = txtELCAccountURL.Text;
                ELCAliasBeingEdited = txtELCAccountAlias.Text;
                chkELCAccountMain.Checked = account.DefaultAccount;
                btnELCAccountAddNew.Enabled = true;
                btnELCAccountDelete.Enabled = true;
                btnELCAccountModify.Enabled = true;
                ELCAccountAction = "EDIT";
                btnELCAccountAddNew.Text = Language.GetText("Add new");
                txtELCAccountKey.Enabled = true;
                txtELCAccountUser.Enabled = true;
                txtELCAccountURL.Enabled = true;
                txtELCAccountAlias.Enabled = true;
                chkELCAccountShowPassword.Enabled = true;
                chkELCAccountShowPassword.Checked = false;
                chkELCAccountMain.Enabled = true;
            }
            else
            {
                MessageBox.Show(Language.GetText("Account not found"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                ELCURLBeingEdited = "";
                ELCAliasBeingEdited = "";
                btnELCAccountAddNew.Enabled = true;
                btnELCAccountDelete.Enabled = false;
                btnELCAccountModify.Enabled = false;
            }
        }

        private void btnELCAccountAddNew_Click(object sender, EventArgs e)
        {
            switch (ELCAccountAction ?? "")
            {
                case "NEW":
                    {

                        if (string.IsNullOrEmpty(txtELCAccountURL.Text))
                        {
                            MessageBox.Show(Language.GetText("URL is mandatory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }


                        var account = new ELCAccountHelper.Account();
                        account.DefaultAccount = chkELCAccountMain.Checked;
                        account.User = Criptografia.ToSecureString(txtELCAccountUser.Text);
                        account.Key = Criptografia.ToSecureString(txtELCAccountKey.Text);
                        account.Alias = txtELCAccountAlias.Text;
                        account.URL = txtELCAccountURL.Text;
                        if (string.IsNullOrEmpty(account.Alias))
                        {
                            account.Alias = txtELCAccountURL.Text;
                        }
                        if (ELCAccountH.AddNewAccount(account))
                        {
                            FillELCAccountDataGridView();
                            ELCAccountAction = "EDIT";
                            btnELCAccountAddNew.Text = Language.GetText("Add new");
                            ELCURLBeingEdited = txtELCAccountURL.Text;
                            ELCAliasBeingEdited = txtELCAccountAlias.Text;
                            btnELCAccountDelete.Enabled = true;
                            btnELCAccountModify.Enabled = true;
                            chkELCAccountShowPassword.Checked = false;
                            txtELCAccountKey.Text = PASSWORDDEFECTO;
                            dgELCUsers.ClearSelection();
                            MessageBox.Show(Language.GetText("Account created correctly"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(Language.GetText("Account already exists"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        break;
                    }

                default:

                    ELCAccountAction = "NEW";
                    btnELCAccountAddNew.Text = Language.GetText("Save");
                    btnELCAccountModify.Enabled = false;
                    btnELCAccountDelete.Enabled = false;
                    ELCURLBeingEdited = "";
                    ELCAliasBeingEdited = "";
                    txtELCAccountUser.Text = "";
                    txtELCAccountAlias.Text = "";
                    txtELCAccountKey.Text = "";
                    txtELCAccountURL.Text = "";
                    txtELCAccountKey.Enabled = true;
                    txtELCAccountUser.Enabled = true;
                    txtELCAccountURL.Enabled = true;
                    txtELCAccountAlias.Enabled = true;
                    chkELCAccountShowPassword.Enabled = true;
                    chkELCAccountMain.Checked = false;
                    chkELCAccountMain.Enabled = true;
                    dgELCUsers.ClearSelection();
                    break;
            }
        }


        private void btnELCAccountModify_Click(object sender, EventArgs e)
        {

            if (ELCAccountAction == "EDIT" & !string.IsNullOrEmpty(ELCURLBeingEdited) & !string.IsNullOrEmpty(ELCAliasBeingEdited))
            {

                if (string.IsNullOrEmpty(txtELCAccountURL.Text))
                {
                    MessageBox.Show(Language.GetText("URL is mandatory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var account = new ELCAccountHelper.Account();
                account.DefaultAccount = chkELCAccountMain.Checked;
                account.User = Criptografia.ToSecureString(txtELCAccountUser.Text);
                account.Alias = txtELCAccountAlias.Text;
                account.URL = txtELCAccountURL.Text;
                if (string.IsNullOrEmpty(account.Alias))
                {
                    account.Alias = txtELCAccountURL.Text;
                }
                if ((txtELCAccountKey.Text ?? "") == PASSWORDDEFECTO) // Password not changed
                {
                    var accAux = ELCAccountH.GetAccountDetailsByURL(ELCURLBeingEdited);
                    if (accAux is not null)
                    {
                        account.Key = accAux.Key.Copy();
                    }
                }
                else
                {
                    account.Key = Criptografia.ToSecureString(txtELCAccountKey.Text);
                }

                if (ELCAccountH.ModifyAccountDetails(ELCURLBeingEdited, account))
                {
                    FillELCAccountDataGridView();
                    ELCAccountAction = "EDIT";
                    btnELCAccountAddNew.Text = Language.GetText("Add new");
                    ELCURLBeingEdited = txtELCAccountURL.Text;
                    ELCAliasBeingEdited = txtELCAccountAlias.Text;
                    chkELCAccountShowPassword.Checked = false;
                    txtELCAccountKey.Text = PASSWORDDEFECTO;
                    dgELCUsers.ClearSelection();
                    MessageBox.Show(Language.GetText("Account modified correctly"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(Language.GetText("Account already exists"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }

        private void btnELCAccountDelete_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ELCURLBeingEdited) & !string.IsNullOrEmpty(ELCAliasBeingEdited))
            {
                if (MessageBox.Show(Language.GetText("Do you want to delete this account?"), Language.GetText("Confirmation"), MessageBoxButtons.YesNo) == DialogResult.Yes)
                {

                    if (ELCAccountH.DeleteAccount(ELCURLBeingEdited))
                    {
                        FillELCAccountDataGridView();
                        dgELCUsers.ClearSelection();
                        txtELCAccountKey.Text = "";
                        txtELCAccountUser.Text = "";
                        txtELCAccountAlias.Text = "";
                        txtELCAccountURL.Text = "";
                        txtELCAccountKey.Enabled = false;
                        txtELCAccountURL.Enabled = false;
                        txtELCAccountAlias.Enabled = false;
                        txtELCAccountUser.Enabled = false;
                        chkELCAccountShowPassword.Enabled = false;
                        chkELCAccountMain.Enabled = false;
                        btnELCAccountAddNew.Enabled = true;
                        btnELCAccountDelete.Enabled = false;
                        btnELCAccountModify.Enabled = false;
                        ELCURLBeingEdited = "";
                        dgELCUsers.ClearSelection();
                        MessageBox.Show(Language.GetText("Account deleted correctly"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(Language.GetText("Account could not be deleted"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    } // 
                }

            }
        }




        private void lklELCUrl_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(lklELCUrl, MsgUrlELCAccount());
        }

        private void lklELCUrl_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(lklELCUrl);
        }

        private void lklELCUrl_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgUrlELCAccount(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string MsgUrlELCAccount()
        {
            return Language.GetText("URL ELC account explanation");
        }


        private void lklELCAliasAccount_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(lklELCAliasAccount, MsgAliasELCAccount());
        }

        private void lklELCAliasAccount_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(lklELCAliasAccount);
        }

        private void lklELCAliasAccount_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgAliasELCAccount(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string MsgAliasELCAccount()
        {
            return Language.GetText("Alias ELC account explanation");
        }


        private void lklELCMainAccount_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(lklELCMainAccount, MsgDefaultELCAccount());
        }

        private void lklELCMainAccount_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(lklELCMainAccount);
        }

        private void lklELCMainAccount_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgDefaultELCAccount(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string MsgDefaultELCAccount()
        {
            return Language.GetText("Default ELC account explanation");
        }


        private void lklELCShowPassword_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(lklELCShowPassword, MsgELCShowPassword());
        }

        private void lklELCShowPassword_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(lklELCShowPassword);
        }

        private void lklELCShowPassword_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgELCShowPassword(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string MsgELCShowPassword()
        {
            return Language.GetText("Show password explanation");
        }

        /// <summary>
	/// 	''' N-P3-3: the lazy help ToolTip ("If t Is Nothing Then t = New ToolTip") is not
	/// 	''' in any components container and was never disposed, so its HWND outlived the
	/// 	''' control until GC (and forever while ThemeManager's registry rooted it — fixed
	/// 	''' there as well). Release it deterministically when the control goes away.
	/// 	''' </summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (t is not null)
            {
                t.Dispose();
                t = null;
            }
            base.OnHandleDestroyed(e);
        }

        #endregion

    }
}