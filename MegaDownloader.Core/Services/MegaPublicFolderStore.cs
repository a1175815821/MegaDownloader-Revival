using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MegaDownloader
{
    /// <summary>
    /// Stores public-folder subscriptions in the application data directory. The link
    /// holds the folder key, so the file is protected with the platform secret protector
    /// the same way the account store is.
    /// </summary>
    public sealed class MegaPublicFolderStore
    {
        private readonly object _gate = new object();
        private readonly string _path;
        private List<MegaPublicFolderSubscription> _items = new List<MegaPublicFolderSubscription>();

        public MegaPublicFolderStore(string folder)
        {
            _path = Path.Combine(folder, "public-folders.dat");
            lock (_gate)
                Load();
        }

        public static int LocalError => MegaAccountService.LocalError;

        public List<MegaPublicFolderSubscription> Snapshot()
        {
            lock (_gate)
                return _items.Select(item => item.Copy()).ToList();
        }

        public MegaPublicFolderSubscription Find(string folderId)
        {
            lock (_gate)
                return _items.FirstOrDefault(item =>
                    string.Equals(item.FolderId, folderId, StringComparison.Ordinal))?.Copy();
        }

        public int Upsert(MegaPublicFolderSubscription settings)
        {
            lock (_gate)
            {
                List<MegaPublicFolderSubscription> before = _items;
                var updated = new List<MegaPublicFolderSubscription>(_items);
                int index = updated.FindIndex(item =>
                    string.Equals(item.FolderId, settings.FolderId, StringComparison.Ordinal));
                if (index >= 0)
                    updated[index] = settings.Copy();
                else
                    updated.Add(settings.Copy());
                _items = updated;
                int result = Save();
                if (result != 0)
                    _items = before;
                return result;
            }
        }

        public int Remove(string folderId)
        {
            lock (_gate)
            {
                List<MegaPublicFolderSubscription> before = _items;
                _items = _items
                    .Where(item => !string.Equals(item.FolderId, folderId, StringComparison.Ordinal))
                    .ToList();
                int result = Save();
                if (result != 0)
                    _items = before;
                return result;
            }
        }

        /// <summary>Stores a finished check, but only while the saved settings still
        /// match the ones the check started with.</summary>
        public int SaveCheck(MegaPublicFolderSubscription subscription)
        {
            lock (_gate)
            {
                MegaPublicFolderSubscription saved = _items.FirstOrDefault(item =>
                    string.Equals(item.FolderId, subscription.FolderId, StringComparison.Ordinal));
                if (saved is null || !SameSettings(saved, subscription))
                    return MegaAccountService.Busy;
                return Upsert(subscription);
            }
        }

        public static bool SameSettings(MegaPublicFolderSubscription left, MegaPublicFolderSubscription right) =>
            string.Equals(left.Destination, right.Destination, StringComparison.OrdinalIgnoreCase) &&
            left.CheckMinutes.Equals(right.CheckMinutes) && left.AutoQueue == right.AutoQueue &&
            left.Extensions == right.Extensions && left.Paused == right.Paused &&
            left.ScheduleFromUtc == right.ScheduleFromUtc;

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return;
                string json = Encoding.UTF8.GetString(Unprotect(File.ReadAllBytes(_path)));
                List<MegaPublicFolderSubscription> loaded =
                    JsonConvert.DeserializeObject<List<MegaPublicFolderSubscription>>(json);
                _items = loaded?.Where(item => item is not null && !string.IsNullOrEmpty(item.FolderId)).ToList()
                    ?? new List<MegaPublicFolderSubscription>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
                FormatException or System.Security.Cryptography.CryptographicException)
            {
                Log.WriteWarning("Could not read the public folder subscriptions: " + Log.SafeException(ex));
                _items = new List<MegaPublicFolderSubscription>();
            }
        }

        private int Save()
        {
            string tempPath = _path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
                File.WriteAllBytes(tempPath, Protect(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_items))));
                File.Move(tempPath, _path, overwrite: true);
                return 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.Cryptography.CryptographicException)
            {
                Log.WriteWarning("Could not save the public folder subscriptions: " + Log.SafeException(ex));
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                }
                return LocalError;
            }
        }

        // The account store keeps DPAPI-with-no-entropy blobs on Windows for the WinForms
        // import path; public folders are new, so they use the platform protector on every OS.
        private static byte[] Protect(byte[] plaintext) =>
            Encoding.UTF8.GetBytes(Criptografia.SecretProtector.Protect(Encoding.UTF8.GetString(plaintext)));

        private static byte[] Unprotect(byte[] stored)
        {
            string text = Encoding.UTF8.GetString(stored);
            if (!Criptografia.SecretProtector.TryUnprotect(text, out string plaintext, out _))
                throw new FormatException("The public folder subscription file could not be opened.");
            return Encoding.UTF8.GetBytes(plaintext);
        }
    }
}
