using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MegaDownloader
{
    /// <summary>
    /// Signed-in MEGA accounts and every SDK operation they own. A public file can use
    /// a selected account instead of the anonymous engine; a private node is always
    /// transferred by the account that queued it and resumes through a checkpoint.
    /// </summary>
    public sealed class MegaAccountService
    {
        public const string AppKeyVariable = "MEGADOWNLOADER_MEGA_APP_KEY";
        public const int SessionExpired = -15;
        public const int OverQuota = -17;
        public const int MfaRequired = -26;
        public const int BackendMissing = -1000;
        public const int Busy = -1002;
        public const int LocalError = -1003;

        public static readonly MegaAccountService Current = new MegaAccountService(
            Path.Combine(ApplicationDataDirectory.GetLegacyRoot(), "MEGA Account"));

        public event EventHandler Changed;

        public struct Status
        {
            public string Email;
            public string AliasName;
            public bool SignedIn;
            public bool Connecting;
            public bool NeedsLogin;
            public int LastError;
            public bool Remember;
            public long? Remaining;
            public bool QuotaReached;
            public DateTime? QuotaRetryUtc;
            public bool QuotaDelayUnknown;
            public int? Plan;
            public long? StorageUsed;
            public long? StorageTotal;
            public long? TransferUsed;
            public long? TransferTotal;

            public string DisplayName => string.IsNullOrEmpty(AliasName) ? Email : AliasName;
        }

        private sealed class SavedLogin
        {
            public string Email { get; set; }
            public string AliasName { get; set; }
            public string Session { get; set; }
            public MegaDrivePreferences DrivePreferences { get; set; }
            public Dictionary<ulong, MegaDriveSubscription> DriveSubscriptions { get; set; }
            public DateTime? QuotaRetryUtc { get; set; }
        }

        private sealed class AccountStore
        {
            public string DefaultEmail { get; set; }
            public List<SavedLogin> Accounts { get; set; }
        }

        private sealed class AccountEntry
        {
            public Status Value;
            public string Session = string.Empty;
            public bool Saved;
            public MegaAccountHandle Client;
            public Conexion.ProxySettings AppliedProxy;
            public readonly object ProxyGate = new object();
            public readonly ThrottledStreamController.AccountDownloadBudget DownloadBudget = new();
            public int Operations;
            public DateTime ZeroBalanceExpiresUtc;
            public readonly HashSet<ulong> StreamingNodes = new HashSet<ulong>();
            public MegaDrivePreferences DrivePreferences = new MegaDrivePreferences();
            public Dictionary<ulong, MegaDriveSubscription> DriveSubscriptions = new Dictionary<ulong, MegaDriveSubscription>();
        }

        private readonly object _gate = new object();
        private readonly string _accountFolder;
        private readonly string _storePath;
        private readonly List<AccountEntry> _accounts = new List<AccountEntry>();
        private string _selectedDefault = string.Empty;
        private bool _restoring;
        private bool _preserveUnreadableStore;

        public MegaDriveHistory DriveHistory { get; }

        public MegaAccountService(string folder)
        {
            _accountFolder = folder;
            _storePath = Path.Combine(folder, "accounts.dat");
            DriveHistory = new MegaDriveHistory(Path.Combine(folder, "Drive history"));
        }

        public string DefaultEmail
        {
            get
            {
                lock (_gate)
                    return _selectedDefault;
            }
        }

        public bool IsRestoring
        {
            get
            {
                lock (_gate)
                    return _restoring;
            }
        }

        public Status GetStatus()
        {
            lock (_gate)
                return SnapshotStatus(FindAccount(_selectedDefault));
        }

        public Status GetStatus(string email)
        {
            lock (_gate)
                return SnapshotStatus(FindAccount(email));
        }

        public List<Status> GetAccounts()
        {
            lock (_gate)
                return _accounts.ConvertAll(SnapshotStatus);
        }

        private Status SnapshotStatus(AccountEntry entry)
        {
            if (entry is null)
                return new Status { Email = string.Empty, Connecting = _restoring };
            Status status = entry.Value;
            DateTime utcNow = DateTime.UtcNow;
            if (status.QuotaRetryUtc.HasValue && status.QuotaRetryUtc.Value <= utcNow)
            {
                status.QuotaRetryUtc = null;
                status.Remaining = null;
            }
            else if (status.Remaining.GetValueOrDefault(1L) == 0 && entry.ZeroBalanceExpiresUtc <= utcNow)
            {
                status.Remaining = null;
            }
            status.QuotaReached = status.QuotaRetryUtc.HasValue || status.Remaining.GetValueOrDefault(1L) == 0;
            return status;
        }

        private AccountEntry FindAccount(string email) =>
            _accounts.Find(entry => string.Equals(entry.Value.Email, email, StringComparison.OrdinalIgnoreCase));

        public string NextTransferAccount(string email, HashSet<string> attempted, bool allowUnknownDelay = true)
        {
            lock (_gate)
            {
                int start = _accounts.IndexOf(FindAccount(email));
                for (int offset = 1; offset <= _accounts.Count; offset++)
                {
                    Status status = SnapshotStatus(_accounts[(start + offset) % _accounts.Count]);
                    if (status.SignedIn && !status.Connecting && !status.QuotaReached &&
                        (allowUnknownDelay || !status.QuotaDelayUnknown) && !attempted.Contains(status.Email))
                    {
                        return status.Email;
                    }
                }
                return string.Empty;
            }
        }

        public static int Availability()
        {
            if (!Environment.Is64BitProcess)
                return BackendMissing;
            return MegaAccountNative.EnsureNative() ? 0 : BackendMissing;
        }

        public async Task<int> RestoreAsync()
        {
            lock (_gate)
            {
                if (_restoring || _accounts.Count > 0)
                    return Busy;
                _restoring = true;
            }
            Changed?.Invoke(this, EventArgs.Empty);
            List<AccountEntry> pending;
            try
            {
                pending = await Task.Run(LoadSavedAccounts).ConfigureAwait(false);
                lock (_gate)
                    _preserveUnreadableStore = false;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                lock (_gate)
                    _preserveUnreadableStore = true;
                return LocalError;
            }
            finally
            {
                lock (_gate)
                    _restoring = false;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            int[] results = await Task.WhenAll(pending.Select(RestoreAccountAsync)).ConfigureAwait(false);
            return results.FirstOrDefault(result => result != 0);
        }

        private List<AccountEntry> LoadSavedAccounts()
        {
            string legacyPath = Path.Combine(_accountFolder, "login.dat");
            bool legacyUsed = false;
            AccountStore store;
            var pending = new List<AccountEntry>();
            try
            {
                store = ReadProtected<AccountStore>(_storePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                SavedLogin saved;
                try
                {
                    saved = ReadProtected<SavedLogin>(legacyPath);
                }
                catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
                {
                    return pending;
                }
                legacyUsed = true;
                store = new AccountStore
                {
                    DefaultEmail = saved?.Email,
                    Accounts = new List<SavedLogin> { saved }
                };
            }

            if (store is null || store.Accounts is null ||
                store.Accounts.Any(saved => saved is null || string.IsNullOrWhiteSpace(saved.Email)))
            {
                throw new InvalidDataException("Invalid saved MEGA account list.");
            }

            bool sessionAvailable = Availability() == 0;
            lock (_gate)
            {
                foreach (SavedLogin saved in store.Accounts)
                {
                    var entry = new AccountEntry
                    {
                        Saved = true,
                        Session = saved.Session ?? string.Empty,
                        DrivePreferences = saved.DrivePreferences ?? new MegaDrivePreferences(),
                        DriveSubscriptions = saved.DriveSubscriptions ?? new Dictionary<ulong, MegaDriveSubscription>(),
                        Value = new Status
                        {
                            Email = saved.Email,
                            AliasName = saved.AliasName ?? string.Empty,
                            QuotaRetryUtc = saved.QuotaRetryUtc
                        }
                    };
                    entry.Value.Remember = entry.Session.Length > 0;
                    entry.Value.Connecting = sessionAvailable && entry.Value.Remember;
                    if (entry.Value.Remember && !sessionAvailable)
                        entry.Value.LastError = BackendMissing;
                    _accounts.Add(entry);
                    if (entry.Value.Connecting)
                        pending.Add(entry);
                }
                _selectedDefault = store.DefaultEmail ?? string.Empty;
                if (legacyUsed && SaveStore() == 0)
                {
                    try
                    {
                        File.Delete(legacyPath);
                    }
                    catch (Exception ex) when (IsStorageError(ex))
                    {
                        Log.WriteWarning("Could not delete the migrated MEGA account store: " + Log.SafeException(ex));
                    }
                }
            }
            return pending;
        }

        private async Task<int> RestoreAccountAsync(AccountEntry entry)
        {
            int result = await RunConnectionAsync(entry, () => Connect(entry, "", "", entry.Session, true),
                restoringSession: true).ConfigureAwait(false);
            if (result == 0)
                await RefreshQuotaAsync(entry.Value.Email).ConfigureAwait(false);
            return result;
        }

        public Task<int> LoginAsync(string email, string password, string pin, bool remember, string aliasName = null)
        {
            int available = Availability();
            if (available != 0)
                return Task.FromResult(available);
            email = email.Trim();
            AccountEntry entry;
            lock (_gate)
            {
                if (_restoring)
                    return Task.FromResult(Busy);
                entry = FindAccount(email);
                if (entry is null)
                {
                    entry = new AccountEntry { Value = new Status { Email = email, AliasName = string.Empty } };
                    _accounts.Add(entry);
                }
                else if (entry.Value.Connecting || entry.Operations > 0)
                {
                    return Task.FromResult(Busy);
                }
                entry.Value.Connecting = true;
            }
            return RunConnectionAsync(entry, () => Connect(entry, password, pin, "", remember, aliasName));
        }

        public Task<int> LogoutAsync(string email = null) => DisconnectAsync(email ?? DefaultEmail, remove: false);

        public Task<int> RemoveAsync(string email) => DisconnectAsync(email, remove: true);

        private Task<int> DisconnectAsync(string email, bool remove)
        {
            AccountEntry entry;
            lock (_gate)
            {
                if (_restoring)
                    return Task.FromResult(Busy);
                entry = FindAccount(email);
                if (entry is null)
                    return Task.FromResult(SessionExpired);
                if (entry.Value.Connecting || entry.Operations > 0)
                    return Task.FromResult(Busy);
                entry.Value.Connecting = true;
            }

            return RunConnectionAsync(entry, () =>
            {
                lock (_gate)
                {
                    string savedSession = entry.Session;
                    string previousDefault = _selectedDefault;
                    entry.Session = string.Empty;
                    entry.Saved = !remove;
                    if (remove && string.Equals(_selectedDefault, email, StringComparison.OrdinalIgnoreCase))
                    {
                        AccountEntry nextDefault = _accounts.Find(other => other.Saved && other.Value.SignedIn);
                        nextDefault ??= _accounts.Find(other => other.Saved);
                        _selectedDefault = nextDefault?.Value.Email ?? string.Empty;
                    }
                    int stored = SaveStore();
                    if (stored != 0)
                    {
                        entry.Session = savedSession;
                        entry.Saved = true;
                        _selectedDefault = previousDefault;
                        return stored;
                    }
                    entry.Value = new Status
                    {
                        Email = entry.Value.Email,
                        AliasName = entry.Value.AliasName,
                        Connecting = true,
                        QuotaRetryUtc = entry.Value.QuotaRetryUtc
                    };
                }

                int result = 0;
                int cleanup = 0;
                try
                {
                    if (entry.Client is not null)
                    {
                        try
                        {
                            result = MegaAccountNative.md_sign_out(entry.Client);
                        }
                        finally
                        {
                            entry.Client.Dispose();
                            entry.Client = null;
                        }
                    }
                }
                finally
                {
                    if (remove)
                        cleanup = RemoveAccountData(entry.Value.Email);
                }
                return result == 0 || result == SessionExpired ? cleanup : result;
            });
        }

        private int RemoveAccountData(string email)
        {
            int result = 0;
            try
            {
                Directory.Delete(AccountCachePath(email), recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                Log.WriteWarning("Could not remove the MEGA SDK cache: " + Log.SafeException(ex));
                result = LocalError;
            }
            try
            {
                DriveHistory.RemoveAccount(email);
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                Log.WriteWarning("Could not remove MEGA cloud drive history: " + Log.SafeException(ex));
                result = LocalError;
            }
            return result;
        }

        public void RecordDriveDelivery(string email, ulong node, string cloudName, string output)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is not null && entry.Saved)
                    DriveHistory.Record(email, node, cloudName, output);
            }
        }

        /// <summary>Records a file delivered by an anonymous public-folder subscription.</summary>
        public void RecordPublicFolderDelivery(string folderId, ulong node, string cloudName, string output) =>
            DriveHistory.RecordPublicFolderDelivery(folderId, node, cloudName, output);

        public int SetAlias(string email, string aliasName)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null)
                    return SessionExpired;
                if (entry.Value.Connecting)
                    return Busy;
                string previous = entry.Value.AliasName;
                entry.Value.AliasName = (aliasName ?? string.Empty).Trim();
                int result = SaveStore();
                if (result != 0)
                {
                    entry.Value.AliasName = previous;
                    return result;
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        public int SetDefault(string email)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null)
                    return SessionExpired;
                if (entry.Value.Connecting)
                    return Busy;
                string previous = _selectedDefault;
                _selectedDefault = entry.Value.Email;
                int result = SaveStore();
                if (result != 0)
                {
                    _selectedDefault = previous;
                    return result;
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        private Task<int> RunConnectionAsync(AccountEntry entry, Func<int> operation, bool restoringSession = false)
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.Run(() =>
            {
                int result = LocalError;
                try
                {
                    result = operation();
                }
                catch (Exception ex) when (IsStorageError(ex))
                {
                    result = LocalError;
                }
                catch (Exception ex) when (IsLibraryError(ex))
                {
                    result = BackendMissing;
                }
                finally
                {
                    lock (_gate)
                    {
                        if (restoringSession)
                        {
                            entry.Value.LastError = result;
                            entry.Value.NeedsLogin = result == SessionExpired || result == -9 || result == -11;
                        }
                        entry.Value.Connecting = false;
                        if (!entry.Saved)
                            _accounts.Remove(entry);
                    }
                    Changed?.Invoke(this, EventArgs.Empty);
                }
                return result;
            });
        }

        private string AccountCachePath(string email)
        {
            using (SHA256 hash = SHA256.Create())
            {
                string cacheName = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(email.ToUpperInvariant())))
                    .Replace("-", string.Empty).ToLowerInvariant();
                return Path.Combine(_accountFolder, cacheName);
            }
        }

        private int Connect(AccountEntry entry, string password, string pin, string savedSession, bool remember,
            string aliasName = null)
        {
            string email = entry.Value.Email;
            string cache = AccountCachePath(email);
            Directory.CreateDirectory(cache);
            Conexion.ProxySettings proxy = Conexion.GetMegaAccountProxy();
            if (proxy is not null && proxy.Address is null)
                return LocalError;
            string appKey = (Environment.GetEnvironmentVariable(AppKeyVariable) ?? string.Empty).Trim();
            MegaAccountHandle opened = MegaAccountNative.md_open(appKey, cache,
                proxy?.Address?.AbsoluteUri ?? string.Empty, proxy?.Username ?? string.Empty,
                proxy?.Password ?? string.Empty);
            MegaAccountHandle replaced = null;
            try
            {
                if (opened is null || opened.IsInvalid)
                    return LocalError;
                int result = MegaAccountNative.md_sign_in(opened, email, password, pin, savedSession);
                if (result != 0)
                    return result;
                string session = savedSession;
                if (remember && savedSession.Length == 0)
                {
                    IntPtr value = MegaAccountNative.md_session(opened);
                    try
                    {
                        session = Marshal.PtrToStringUTF8(value) ?? string.Empty;
                        if (session.Length == 0)
                            return LocalError;
                    }
                    finally
                    {
                        MegaAccountNative.md_free_string(value);
                    }
                }

                lock (_gate)
                {
                    Status previous = entry.Value;
                    string previousSession = entry.Session;
                    string previousDefault = _selectedDefault;
                    bool previouslySaved = entry.Saved;
                    entry.Value = new Status
                    {
                        Email = email,
                        AliasName = (aliasName ?? previous.AliasName ?? string.Empty).Trim(),
                        SignedIn = true,
                        Connecting = true,
                        Remember = remember,
                        QuotaRetryUtc = previous.QuotaRetryUtc
                    };
                    entry.Session = session;
                    entry.Saved = true;
                    if (_selectedDefault.Length == 0)
                        _selectedDefault = email;
                    if (savedSession.Length == 0)
                    {
                        result = SaveStore();
                        if (result != 0)
                        {
                            entry.Value = previous;
                            entry.Session = previousSession;
                            entry.Saved = previouslySaved;
                            _selectedDefault = previousDefault;
                            return result;
                        }
                    }
                    replaced = entry.Client;
                    entry.Client = opened;
                    entry.AppliedProxy = proxy;
                    opened = null;
                }
                return 0;
            }
            finally
            {
                replaced?.Dispose();
                opened?.Dispose();
            }
        }

        private static T ReadProtected<T>(string filePath)
        {
            byte[] bytes = UnprotectAccountStore(File.ReadAllBytes(filePath));
            try
            {
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes));
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        private static byte[] UnprotectAccountStore(byte[] stored)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // The WinForms 2.6.5 store used DPAPI with no entropy.
                    return System.Security.Cryptography.ProtectedData.Unprotect(
                        stored, null, DataProtectionScope.CurrentUser);
                }
                catch (CryptographicException)
                {
                    // Not a legacy DPAPI blob; fall through to the platform protector.
                }
            }

            string ciphertext = Encoding.UTF8.GetString(stored).Trim();
            if (Criptografia.SecretProtector.TryUnprotect(ciphertext, out string plaintext, out _) &&
                plaintext is not null)
            {
                return Encoding.UTF8.GetBytes(plaintext);
            }
            throw new CryptographicException("The MEGA account store could not be opened.");
        }

        private static byte[] ProtectAccountStore(byte[] plaintext)
        {
            if (OperatingSystem.IsWindows())
            {
                return System.Security.Cryptography.ProtectedData.Protect(
                    plaintext, null, DataProtectionScope.CurrentUser);
            }
            return Encoding.UTF8.GetBytes(Criptografia.SecretProtector.Protect(Encoding.UTF8.GetString(plaintext)));
        }

        // Called under _gate so a saved default and its account records are published together.
        private int SaveStore()
        {
            var store = new AccountStore { DefaultEmail = _selectedDefault, Accounts = new List<SavedLogin>() };
            foreach (AccountEntry entry in _accounts)
            {
                if (!entry.Saved)
                    continue;
                store.Accounts.Add(new SavedLogin
                {
                    Email = entry.Value.Email,
                    AliasName = entry.Value.AliasName,
                    Session = entry.Session,
                    DrivePreferences = entry.DrivePreferences,
                    DriveSubscriptions = entry.DriveSubscriptions,
                    QuotaRetryUtc = SnapshotStatus(entry).QuotaRetryUtc
                });
            }

            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(store));
            string tempPath = _storePath + ".tmp";
            try
            {
                Directory.CreateDirectory(_accountFolder);
                File.WriteAllBytes(tempPath, ProtectAccountStore(bytes));
                if (File.Exists(_storePath))
                {
                    if (_preserveUnreadableStore)
                    {
                        string backup = _storePath + "." + Guid.NewGuid().ToString("N") + ".bak";
                        File.Copy(_storePath, backup, overwrite: true);
                    }
                    File.Move(tempPath, _storePath, overwrite: true);
                }
                else
                {
                    File.Move(tempPath, _storePath);
                }
                _preserveUnreadableStore = false;
                return 0;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                return LocalError;
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        private static bool IsStorageError(Exception ex) =>
            ex is IOException or InvalidDataException or UnauthorizedAccessException or
                CryptographicException or JsonException;

        private static bool IsLibraryError(Exception ex) =>
            ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

        public int GetAccessError(string email)
        {
            if (string.IsNullOrEmpty(email))
                return 0;
            return AccessError(GetStatus(email));
        }

        private static int AccessError(Status status)
        {
            if (status.Connecting)
                return Busy;
            return status.SignedIn ? 0 : SessionExpired;
        }

        public void UpdateProxies()
        {
            foreach (Status status in GetAccounts())
            {
                if (!status.SignedIn || status.Connecting)
                    continue;
                int result = Acquire(status.Email, out AccountEntry entry);
                if (result == 0)
                    Release(entry, result);
                else if (result != Busy && result != SessionExpired)
                    Log.WriteWarning("Could not update the MEGA account proxy: " + result);
            }
        }

        private int Acquire(string email, out AccountEntry entry)
        {
            lock (_gate)
            {
                entry = FindAccount(email);
                int access = AccessError(SnapshotStatus(entry));
                if (access != 0)
                    return access;
                entry.Operations++;
            }

            int result = LocalError;
            try
            {
                lock (entry.ProxyGate)
                {
                    Conexion.ProxySettings proxy = Conexion.GetMegaAccountProxy();
                    if (ReferenceEquals(proxy, entry.AppliedProxy))
                    {
                        result = 0;
                    }
                    else if (proxy is null || proxy.Address is not null)
                    {
                        result = MegaAccountNative.md_set_proxy(entry.Client,
                            proxy?.Address?.AbsoluteUri ?? string.Empty, proxy?.Username ?? string.Empty,
                            proxy?.Password ?? string.Empty);
                        if (result == 0)
                            entry.AppliedProxy = proxy;
                    }
                }
                return result;
            }
            catch (Exception ex) when (IsLibraryError(ex))
            {
                return BackendMissing;
            }
            finally
            {
                if (result != 0)
                    Release(entry, result);
            }
        }

        private void Release(AccountEntry entry, int result, bool detailsChanged = false, long quotaWaitSeconds = 0)
        {
            lock (_gate)
            {
                entry.Operations--;
                DateTime utcNow = DateTime.UtcNow;
                DateTime? previous = entry.Value.QuotaRetryUtc;
                if (previous.HasValue && previous.Value <= utcNow)
                {
                    entry.Value.QuotaRetryUtc = null;
                    entry.Value.Remaining = null;
                    previous = null;
                    detailsChanged = true;
                }
                if (result == SessionExpired && entry.Value.SignedIn)
                {
                    entry.Value.SignedIn = false;
                    entry.Value.NeedsLogin = true;
                    entry.Value.LastError = result;
                    detailsChanged = true;
                }
                else if (result == OverQuota)
                {
                    if (quotaWaitSeconds > 0)
                    {
                        double seconds = Math.Min((double)quotaWaitSeconds,
                            (DateTime.MaxValue - utcNow.AddDays(1)).TotalSeconds);
                        DateTime deadline = utcNow.AddSeconds(seconds);
                        if (!previous.HasValue || deadline > previous.Value.AddSeconds(1))
                        {
                            entry.Value.QuotaRetryUtc = deadline;
                            entry.Value.QuotaDelayUnknown = false;
                            detailsChanged = true;
                            if (SaveStore() != 0)
                                Log.WriteWarning("Could not save MEGA transfer quota deadline.");
                        }
                    }
                    else if (!previous.HasValue && !entry.Value.QuotaDelayUnknown)
                    {
                        // With no SDK deadline, use the queue's retry interval for failed tasks.
                        entry.Value.QuotaDelayUnknown = true;
                        detailsChanged = true;
                    }
                }
            }
            if (detailsChanged)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        public Task<int> RefreshQuotaAsync(string email = null)
        {
            int access = Acquire(email ?? DefaultEmail, out AccountEntry entry);
            if (access != 0)
                return Task.FromResult(access);
            return Task.Run(() =>
            {
                int result = LocalError;
                try
                {
                    result = MegaAccountNative.md_account_details(entry.Client, out int plan, out long storageUsed,
                        out long storageTotal, out long transferUsed, out long transferTotal);
                    if (result == 0)
                    {
                        lock (_gate)
                        {
                            entry.Value.Plan = plan >= 0 ? plan : null;
                            entry.Value.StorageUsed = storageUsed >= 0 ? storageUsed : null;
                            entry.Value.StorageTotal = storageTotal > 0 ? storageTotal : null;
                            entry.Value.TransferUsed = transferUsed >= 0 ? transferUsed : null;
                            entry.Value.TransferTotal = transferTotal > 0 ? transferTotal : null;
                            entry.Value.Remaining = transferTotal > 0 && transferUsed >= 0
                                ? Math.Max(0L, transferTotal - transferUsed)
                                : null;
                            DateTime utcNow = DateTime.UtcNow;
                            if (entry.Value.Remaining.GetValueOrDefault(1L) == 0)
                                entry.ZeroBalanceExpiresUtc = utcNow.AddMinutes(5);
                            if (entry.Value.QuotaRetryUtc.GetValueOrDefault() <= utcNow)
                                entry.Value.QuotaRetryUtc = null;
                            entry.Value.QuotaDelayUnknown = false;
                        }
                    }
                    return result;
                }
                catch (Exception ex) when (IsLibraryError(ex))
                {
                    return BackendMissing;
                }
                finally
                {
                    Release(entry, result, detailsChanged: result == 0);
                }
            });
        }

        public MegaDrivePreferences GetDrivePreferences(string email)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                return entry is null ? new MegaDrivePreferences() : entry.DrivePreferences.Copy();
            }
        }

        public int SaveDrivePreferences(string email, MegaDrivePreferences preferences)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null || !entry.Saved)
                    return SessionExpired;
                MegaDrivePreferences previous = entry.DrivePreferences;
                if (previous.LastFolder == preferences.LastFolder &&
                    previous.LastSharedFolder == preferences.LastSharedFolder &&
                    previous.Favorites.SequenceEqual(preferences.Favorites) &&
                    previous.FolderDestinations.Count == preferences.FolderDestinations.Count &&
                    previous.FolderDestinations.All(item =>
                        preferences.FolderDestinations.TryGetValue(item.Key, out string path) && path == item.Value) &&
                    previous.BoundFolderPaths.Count == preferences.BoundFolderPaths.Count &&
                    previous.BoundFolderPaths.All(binding =>
                        preferences.BoundFolderPaths.TryGetValue(binding.Key, out Dictionary<ulong, string> paths) &&
                        paths.Count == binding.Value.Count &&
                        binding.Value.All(item => paths.ContainsKey(item.Key) && paths[item.Key] == item.Value)))
                {
                    return 0;
                }

                Dictionary<ulong, MegaDriveSubscription> subscriptions = entry.DriveSubscriptions;
                entry.DrivePreferences = preferences.Copy();
                entry.DriveSubscriptions = subscriptions
                    .Where(item => preferences.FolderDestinations.ContainsKey(item.Key))
                    .ToDictionary(item => item.Key, item => item.Value);
                int result = SaveStore();
                if (result != 0)
                {
                    entry.DrivePreferences = previous;
                    entry.DriveSubscriptions = subscriptions;
                }
                return result;
            }
        }

        public List<MegaDriveSubscription> GetDriveSubscriptions(string email)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null)
                    return new List<MegaDriveSubscription>();
                return entry.DrivePreferences.FolderDestinations
                    .Select(binding => SubscriptionFor(entry, binding.Key, binding.Value)).ToList();
            }
        }

        public List<MegaDriveSubscription> GetDueDriveSubscriptions(string email, DateTime now)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null)
                    return new List<MegaDriveSubscription>();
                return entry.DriveSubscriptions.Values.Where(item =>
                {
                    DateTime? due = item.NextCheckLocal();
                    return due.HasValue && due.Value <= now;
                }).Select(item => item.Copy()).ToList();
            }
        }

        private static MegaDriveSubscription SubscriptionFor(AccountEntry entry, ulong folder, string destination)
        {
            if (entry.DriveSubscriptions.TryGetValue(folder, out MegaDriveSubscription saved))
                return saved.Copy();
            var result = new MegaDriveSubscription { Folder = folder, Destination = destination };
            if (entry.DrivePreferences.BoundFolderPaths.TryGetValue(folder, out Dictionary<ulong, string> paths))
                result.FolderPaths = new Dictionary<ulong, string>(paths);
            return result;
        }

        public int SaveDriveSubscriptionSettings(string email, MegaDriveSubscription settings)
        {
            string destination;
            try
            {
                destination = PathGuard.GetSafePathUnderRoot(settings.Destination, string.Empty);
            }
            catch (ArgumentException)
            {
                return LocalError;
            }

            int result;
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null || !entry.Saved)
                    return SessionExpired;
                MegaDrivePreferences previous = entry.DrivePreferences;
                previous.FolderDestinations.TryGetValue(settings.Folder, out string oldDestination);
                MegaDriveSubscription subscription = SubscriptionFor(entry, settings.Folder, oldDestination);
                bool destinationChanged = !string.Equals(oldDestination, destination, StringComparison.OrdinalIgnoreCase);
                string extensions = MegaDriveSubscription.NormalizeExtensions(settings.Extensions);
                if (destinationChanged || extensions != subscription.Extensions)
                    subscription.Files.Clear();
                if (destinationChanged)
                    subscription.FolderPaths.Clear();
                if (!subscription.CheckMinutes.Equals(settings.CheckMinutes))
                    subscription.ScheduleFromUtc = DateTime.UtcNow;
                subscription.Destination = destination;
                subscription.CheckMinutes = settings.CheckMinutes;
                subscription.AutoQueue = settings.AutoQueue;
                subscription.Paused = settings.Paused;
                subscription.Extensions = extensions;
                subscription.Name = settings.Name ?? subscription.Name;
                subscription.Incoming = settings.Incoming ?? subscription.Incoming;

                MegaDrivePreferences updated = previous.Copy();
                updated.FolderDestinations[settings.Folder] = destination;
                if (destinationChanged)
                    updated.BoundFolderPaths.Remove(settings.Folder);
                entry.DrivePreferences = updated;
                result = SaveSubscription(entry, subscription);
                if (result != 0)
                    entry.DrivePreferences = previous;
            }
            if (result == 0)
                Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public int SaveDriveSubscriptionCheck(string email, MegaDriveSubscription subscription)
        {
            lock (_gate)
            {
                AccountEntry entry = FindAccount(email);
                if (entry is null || !entry.Saved)
                    return SessionExpired;
                if (!entry.DrivePreferences.FolderDestinations.TryGetValue(subscription.Folder, out string destination) ||
                    !SubscriptionFor(entry, subscription.Folder, destination).MatchesSettings(subscription))
                {
                    return Busy;
                }
                return SaveSubscription(entry, subscription);
            }
        }

        private int SaveSubscription(AccountEntry entry, MegaDriveSubscription subscription)
        {
            Dictionary<ulong, MegaDriveSubscription> before = entry.DriveSubscriptions;
            entry.DriveSubscriptions = new Dictionary<ulong, MegaDriveSubscription>(before)
            {
                [subscription.Folder] = subscription.Copy()
            };
            int result = SaveStore();
            if (result != 0)
                entry.DriveSubscriptions = before;
            return result;
        }

        public int ReadDrive(string email, CancellationToken ct, out MegaDriveSnapshot snapshot,
            bool incomingShares = false)
        {
            snapshot = null;
            int access = Acquire(email, out AccountEntry entry);
            if (access != 0)
                return access;
            int result = LocalError;
            try
            {
                var nodes = new List<MegaDriveNode>();
                MegaAccountNative.DriveNode visit = (id, parent, name, folder, bytes, modified) =>
                {
                    if (ct.IsCancellationRequested)
                        return 0;
                    nodes.Add(new MegaDriveNode(id, parent, name, folder != 0, bytes, modified));
                    return 1;
                };
                result = incomingShares
                    ? MegaAccountNative.md_list_shares(entry.Client, visit, out ulong root)
                    : MegaAccountNative.md_list_drive(entry.Client, visit, out root);
                if (result == 0)
                    snapshot = new MegaDriveSnapshot(root, nodes);
                return result;
            }
            catch (Exception ex) when (IsLibraryError(ex))
            {
                return BackendMissing;
            }
            finally
            {
                Release(entry, result);
            }
        }

        public int Inspect(string email, string link, out string name, out long size, ulong? node = null)
        {
            name = null;
            size = 0;
            int access = Acquire(email, out AccountEntry entry);
            if (access != 0)
                return access;
            int result = LocalError;
            try
            {
                var buffer = new byte[4096];
                result = node.HasValue
                    ? MegaAccountNative.md_inspect_node(entry.Client, node.Value, buffer, buffer.Length, out size)
                    : MegaAccountNative.md_inspect(entry.Client, link, buffer, buffer.Length, out size);
                if (result == 0)
                {
                    int length = Array.IndexOf(buffer, (byte)0);
                    name = Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
                }
                return result;
            }
            catch (Exception ex) when (IsLibraryError(ex))
            {
                return BackendMissing;
            }
            finally
            {
                Release(entry, result);
            }
        }

        public int Download(string email, string link, string destination,
            Action<long, long, long> progress, CancellationToken ct)
        {
            int access = Acquire(email, out AccountEntry entry);
            if (access != 0)
                return access;
            int result = LocalError;
            long quotaWaitSeconds = 0;
            ThrottledStreamController controller = ThrottledStreamController.GetController();
            ThrottledStreamController.AccountDownloadBudget budget = entry.DownloadBudget;
            bool registered = false;
            try
            {
                if (GetStatus(email).QuotaReached)
                {
                    result = OverQuota;
                    return OverQuota;
                }
                MegaAccountNative.Progress report = (received, total, speed) => progress(received, total, speed);
                MegaAccountNative.Cancelled cancelled = () => ct.IsCancellationRequested ? 1 : 0;
                controller.BeginAccountDownload(budget);
                registered = true;
                result = MegaAccountNative.md_download_with_limit(entry.Client, link, destination, report,
                    cancelled, out quotaWaitSeconds, () => budget.BytesPerSecond);
                return result;
            }
            catch (Exception ex) when (IsLibraryError(ex))
            {
                return BackendMissing;
            }
            finally
            {
                if (registered)
                    controller.EndAccountDownload(budget);
                Release(entry, result, quotaWaitSeconds: quotaWaitSeconds);
            }
        }

        public int InspectPrivateDownload(string email, ulong node, out string name, out long size, out string key)
        {
            name = null;
            size = 0;
            key = null;
            int access = Acquire(email, out AccountEntry entry);
            if (access != 0)
                return access;
            int result = LocalError;
            try
            {
                var nameBuffer = new byte[4096];
                // The bridge requires room for a 43-character key plus the terminator.
                var keyBuffer = new byte[44];
                result = MegaAccountNative.md_inspect_node_for_resume(entry.Client, node, nameBuffer,
                    nameBuffer.Length, out size, keyBuffer, keyBuffer.Length);
                if (result == 0)
                {
                    int nameLength = Array.IndexOf(nameBuffer, (byte)0);
                    name = Encoding.UTF8.GetString(nameBuffer, 0, nameLength < 0 ? nameBuffer.Length : nameLength);
                    int keyLength = Array.IndexOf(keyBuffer, (byte)0);
                    key = Encoding.ASCII.GetString(keyBuffer, 0, keyLength < 0 ? keyBuffer.Length : keyLength);
                    // VerifyMegaMetaMac also accepts public keys that do not contain a MAC.
                    // A private transfer must always supply a full 32-byte file key.
                    if (key.Length != 43 || !Regex.IsMatch(key, "\\A[A-Za-z0-9_-]{43}\\z") ||
                        Criptografia.base64urldecodeBytes(key).Length != 32)
                    {
                        result = -14;
                    }
                }
                return result;
            }
            catch (Exception ex) when (IsLibraryError(ex))
            {
                return BackendMissing;
            }
            finally
            {
                Release(entry, result);
            }
        }

        internal int StreamPrivateDownload(string email, ulong node, string key, long size, long offset,
            MegaAccountNative.StreamData write, CancellationToken ct)
        {
            int access = Acquire(email, out AccountEntry entry);
            if (access != 0)
                return access;
            int result = LocalError;
            bool started = false;
            long quotaWaitSeconds = 0;
            ThrottledStreamController controller = ThrottledStreamController.GetController();
            ThrottledStreamController.AccountDownloadBudget budget = entry.DownloadBudget;
            bool registered = false;
            try
            {
                // SDK streaming cancellation is by node and range, not by listener. Do not
                // let a second task for the same node be stopped by the first task's pause.
                lock (entry.StreamingNodes)
                {
                    while (entry.StreamingNodes.Contains(node))
                    {
                        ct.ThrowIfCancellationRequested();
                        Monitor.Wait(entry.StreamingNodes, 100);
                    }
                    ct.ThrowIfCancellationRequested();
                    started = entry.StreamingNodes.Add(node);
                }
                if (offset < size && GetStatus(email).QuotaReached)
                {
                    result = OverQuota;
                    return OverQuota;
                }
                MegaAccountNative.Cancelled cancelled = () => ct.IsCancellationRequested ? 1 : 0;
                if (offset < size)
                {
                    controller.BeginAccountDownload(budget);
                    registered = true;
                }
                result = MegaAccountNative.md_stream_node_with_limit(entry.Client, node, key, size, offset,
                    write, cancelled, out quotaWaitSeconds, () => budget.BytesPerSecond);
                return result;
            }
            catch (Exception ex) when (IsLibraryError(ex))
            {
                return BackendMissing;
            }
            finally
            {
                if (registered)
                    controller.EndAccountDownload(budget);
                if (started)
                {
                    lock (entry.StreamingNodes)
                    {
                        entry.StreamingNodes.Remove(node);
                        Monitor.PulseAll(entry.StreamingNodes);
                    }
                }
                Release(entry, result, quotaWaitSeconds: quotaWaitSeconds);
            }
        }

        public static string ErrorText(int code, bool signingIn = false, bool connecting = false)
        {
            switch (code)
            {
                case MfaRequired:
                    return Language.GetText("MegaAccount_MfaRequired");
                case SessionExpired:
                    return Language.GetText("MegaAccount_SessionExpired");
                case OverQuota:
                    return Language.GetText("MegaAccount_NoAvailableQuota");
                case BackendMissing:
                    return Language.GetText("MegaAccount_BackendMissing");
                case Busy:
                    return Language.GetText(connecting ? "MegaAccount_Connecting" : "MegaAccount_Busy");
                case LocalError:
                    return Language.GetText("MegaAccount_LocalError");
                case -9:
                case -11:
                    return Language.GetText(signingIn ? "MegaAccount_AuthenticationFailed" : "MegaAccount_FileUnavailable");
                case -14:
                case -2:
                    return Language.GetText("MegaAccount_FileUnavailable");
                default:
                    return Language.GetText("MegaAccount_RequestFailed")
                        .Replace("%N%", code.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
