using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    public class ELCAccountHelper : IDisposable
    {

        // P1-5:ELCAccounts 列表及其 SecureString 的统一保护锁。Configuracion.GuardarXML /
        // ELCAccountsHash 跑在磁盘 worker(每 5s)上,而 SaveToConfig 在 UI 线程就地
        // Dispose→Clear→Add 重建列表,两者并发时读侧 For Each(在 GuardarXML 的 Try 之外)
        // 抛 "Collection was modified" / ObjectDisposedException 直接炸死磁盘 worker,
        // 本会话内自动存盘全部停摆。所有对 Config.ELCAccounts 的读写都必须持此锁。
        public static readonly object ELCAccountsLock = new object();

        private List<Account> _AccountList;

        public class Account
        {
            public SecureString User;
            public SecureString Key;
            public string Alias;
            public string URL;
            public bool DefaultAccount;
        }

        public ELCAccountHelper(ref Configuracion Config)
        {
            _AccountList = new List<Account>();
            // P1-5:持锁遍历 Config.ELCAccounts(可能正被 SaveToConfig 换引用/释放)
            lock (ELCAccountsLock)
            {
                foreach (Account c in Config.ELCAccounts)
                {
                    var newAccount = new Account();
                    newAccount.User = c.User.Copy();
                    newAccount.Key = c.Key.Copy();
                    newAccount.Alias = c.Alias;
                    newAccount.URL = c.URL;
                    newAccount.DefaultAccount = c.DefaultAccount;
                    _AccountList.Add(newAccount);
                }
            }
        }


        public List<Account> GetAccounts()
        {
            return _AccountList;
        }

        public Account GetDefaultAccount()
        {
            return GetAccounts().FirstOrDefault((c) => c.DefaultAccount);
        }


        public Account GetAccountDetailsByURL(string URL)
        {
            return GetAccounts().FirstOrDefault((c) => (c.URL.ToLower().Trim() ?? "") == (URL.ToLower().Trim() ?? ""));
        }
        public Account GetAccountDetailsByAlias(string Alias)
        {
            return GetAccounts().FirstOrDefault((c) => (c.Alias.ToLower().Trim() ?? "") == (Alias.ToLower().Trim() ?? ""));
        }

        public bool ModifyAccountDetails(string URLPreviousAccount, Account NewAccount)
        {
            var c = GetAccountDetailsByURL(URLPreviousAccount);
            if (c is null)
                return false;

            // Check that the new email is not used by another account
            var c2 = GetAccountDetailsByURL(NewAccount.URL);
            if (c2 is not null && !c2.Equals(c))
                return false;
            // Check that the new alias is not used by another account
            c2 = GetAccountDetailsByAlias(NewAccount.Alias);
            if (c2 is not null && !c2.Equals(c))
                return false;


            c.User.Dispose();
            c.Key.Dispose();
            c.URL = NewAccount.URL;
            c.Alias = NewAccount.Alias;
            c.User = NewAccount.User;
            c.Key = NewAccount.Key;
            c.DefaultAccount = NewAccount.DefaultAccount;
            ReAssignDefault(ref c);
            return true;
        }

        public bool AddNewAccount(Account NewAccount)
        {
            Account c;

            // Check alias and email
            c = GetAccountDetailsByAlias(NewAccount.Alias);
            if (c is not null)
                return false;
            c = GetAccountDetailsByURL(NewAccount.URL);
            if (c is not null)
                return false;

            _AccountList.Add(NewAccount);
            ReAssignDefault(ref NewAccount);
            return true;
        }

        public bool DeleteAccount(string URL)
        {
            bool DeleteAccountRet = default;
            var c = GetAccountDetailsByURL(URL);
            if (c is null)
                return false;
            DeleteAccountRet = _AccountList.Remove(c);
            c.User.Dispose();
            c.Key.Dispose();
            Account argc = null;
            ReAssignDefault(ref argc);
            return DeleteAccountRet;
        }

        private void ReAssignDefault(ref Account c)
        {
            if (c is not null && c.DefaultAccount)
            {
                foreach (Account a in _AccountList)
                {
                    if (!c.Equals(a))
                        a.DefaultAccount = false;
                }
            }
            else
            {
                bool ThereIsDefault = false;
                foreach (Account a in _AccountList)
                {
                    if (a.DefaultAccount)
                        ThereIsDefault = true;
                }
                if (!ThereIsDefault)
                {
                    var l = from a in _AccountList
                            orderby a.Alias
                            select a;
                    if (l.Count() > 0)
                    {
                        l.ElementAtOrDefault(0).DefaultAccount = true;
                    }
                }
            }
        }

        public void SaveToConfig(ref Configuracion Config)
        {
            // P1-5:先在锁外把新列表构造成独立副本,再持锁一次性换引用并释放旧 SecureString。
            // 旧代码就地 Dispose→Clear→Add,与 GuardarXML 的 For Each 竞态(见 ELCAccountsLock
            // 注释);现在任何读方持同一把锁,看到的要么是完整旧列表、要么是完整新列表,
            // 绝不会踩到半改状态或已释放的 SecureString。整体换引用仿
            // Forms/Configuration.vb 对 ListaPreSharedKeys 的安全写法。
            var newList = new List<Account>();
            foreach (Account c in _AccountList)
            {
                var newAccount = new Account();
                newAccount.User = c.User.Copy();
                newAccount.Key = c.Key.Copy();
                newAccount.DefaultAccount = c.DefaultAccount;
                newAccount.URL = c.URL;
                newAccount.Alias = c.Alias;
                newList.Add(newAccount);
            }

            lock (ELCAccountsLock)
            {
                if (Config.ELCAccounts is not null)
                {
                    foreach (Account c in Config.ELCAccounts)
                    {
                        c.User.Dispose();
                        c.Key.Dispose();
                    }
                }
                Config.ELCAccounts = newList;
            }
        }

        /// <summary>
        /// Parses ELC account configuration strings. The original WinForms version asked
        /// for confirmation and showed message boxes here; the cross-platform UI is now
        /// responsible for prompting, so core only parses and imports.
        /// </summary>
        public bool ImportConfig(List<string> ConfigList)
        {
            if (ConfigList is null || ConfigList.Count == 0)
                return false;

            var TempAccount = new List<Account>();
            string Description = "";

            foreach (string Conf in ConfigList)
            {
                string conf2 = Conf.Replace(@"\:", "{TEMP_PUNTOS}");
                // url : nick : apikey (: alias)
                if (conf2.Split(':').Length == 3 | conf2.Split(':').Length == 4)
                {
                    int index = 0;

                    string URL = "";
                    string Nick = "";
                    string ApiKey = "";
                    string Alias = "";
                    foreach (string token in conf2.Split(':'))
                    {
                        string t = System.Web.HttpUtility.UrlDecode(token.Replace("{TEMP_PUNTOS}", ":"));
                        switch (index)
                        {
                            case 0:
                                URL = t;
                                break;
                            case 1:
                                Nick = t;
                                break;
                            case 2:
                                ApiKey = t;
                                break;
                            case 3:
                                Alias = t;
                                break;
                        }
                        index += 1;
                    }
                    var A = new Account();
                    A.URL = URL;
                    A.User = Criptografia.ToSecureString(Nick);
                    A.Key = Criptografia.ToSecureString(ApiKey);
                    A.Alias = Alias;
                    if (string.IsNullOrEmpty(A.Alias))
                        A.Alias = URL;
                    TempAccount.Add(A);
                    if (Description.Length > 0)
                        Description += ", ";
                    Description += URL;
                }
            }

            if (TempAccount.Count == 0)
                return false;

            foreach (Account Acc in TempAccount)
            {
                if (!ModifyAccountDetails(Acc.URL, Acc))
                {
                    AddNewAccount(Acc);
                }
            }

            return true;
        }


        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                foreach (Account c in _AccountList)
                {
                    c.User.Dispose();
                    c.Key.Dispose();
                }
                _AccountList.Clear();
            }
            disposedValue = true;
        }



        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}