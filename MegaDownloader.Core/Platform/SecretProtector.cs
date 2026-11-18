using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace MegaDownloader.Platform
{
    /// <summary>
    /// Reversible protection for secrets written to disk (MEGA credentials, proxy and
    /// streaming passwords, queue links, extraction passwords, streaming-library links).
    /// </summary>
    /// <remarks>
    /// Windows keeps the original DPAPI scheme, including its historical entropy chain, so
    /// existing configuration files keep working. macOS and Linux install
    /// <see cref="AesFileSecretProtector"/> instead; DPAPI does not exist there and every
    /// save used to fail with a platform-not-supported exception.
    /// </remarks>
    public interface ISecretProtector
    {
        /// <summary>Encrypts a value for storage. Empty input returns an empty string.</summary>
        string Protect(string plaintext);

        /// <summary>
        /// Decrypts a stored value. Returns false when the blob cannot be opened on this
        /// machine or account, which callers surface as a "please re-enter" prompt.
        /// </summary>
        /// <param name="ciphertext">Stored value.</param>
        /// <param name="plaintext">Decrypted value when this returns true.</param>
        /// <param name="writtenWithCurrentKey">
        /// True when the blob was produced by the current key. Callers reuse such a
        /// ciphertext verbatim on the next save; a value written by an older release is
        /// re-encrypted instead, so it migrates forward.
        /// </param>
        bool TryUnprotect(string ciphertext, out string plaintext, out bool writtenWithCurrentKey);
    }

    /// <summary>
    /// Windows DPAPI implementation with the legacy entropy chain. The chain lets a value
    /// written by any earlier release decrypt after an upgrade.
    /// </summary>
    public sealed class WindowsDpapiSecretProtector : ISecretProtector
    {
        private const string AppIdentity = "MegaDownloader";

        // Entropy used by v1.9 .. v2.3.0, before it was derived from the assembly identity.
        private const string PreDerivationEntropyLiteral = "G*SNAfhHW5A¿Amck+XMLCM6M#$xEK;9q";

        // Releases that derived entropy from AssemblyName.FullName (v2.4.0 .. v2.5.3).
        // Do NOT add later releases: they write with the stable entropy.
        private static readonly string[] VersionDerivedReleases =
        {
            "2.4.0.0", "2.4.1.0", "2.4.2.0", "2.4.3.0", "2.4.4.0", "2.4.5.0",
            "2.4.6.0", "2.4.7.0", "2.5.0.0", "2.5.1.0", "2.5.2.0", "2.5.3.0"
        };

        private static readonly byte[] CurrentEntropy = DeriveEntropy(AppIdentity);
        private static readonly byte[][] EntropyChain = BuildDecryptionEntropyChain();

        /// <summary>
        /// The decryption candidates, current entropy first. Exposed so the regression
        /// harness can verify that every historical release's blobs still open.
        /// </summary>
        public static IReadOnlyList<byte[]> DecryptionEntropies => EntropyChain;

        public string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;
            byte[] encrypted = ProtectedData.Protect(
                Encoding.Unicode.GetBytes(plaintext), CurrentEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public bool TryUnprotect(string ciphertext, out string plaintext, out bool writtenWithCurrentKey)
        {
            plaintext = null;
            writtenWithCurrentKey = false;
            if (string.IsNullOrEmpty(ciphertext))
            {
                plaintext = string.Empty;
                return true;
            }

            byte[] blob;
            try
            {
                blob = Convert.FromBase64String(ciphertext);
            }
            catch (FormatException)
            {
                // Not base64: a plaintext value left by a version that predates encryption.
                return false;
            }

            for (int index = 0; index < EntropyChain.Length; index++)
            {
                byte[] decrypted = null;
                try
                {
                    decrypted = ProtectedData.Unprotect(blob, EntropyChain[index], DataProtectionScope.CurrentUser);
                    plaintext = Encoding.Unicode.GetString(decrypted);
                    // Only the first entry is the current entropy; older ones mean the value
                    // still needs to be re-encrypted.
                    writtenWithCurrentKey = index == 0;
                    return true;
                }
                catch (CryptographicException)
                {
                    // Not this entropy; keep walking the chain.
                }
                finally
                {
                    if (decrypted is not null)
                        Array.Clear(decrypted, 0, decrypted.Length);
                }
            }

            return false;
        }

        private static byte[] DeriveEntropy(string identity) =>
            SHA256.HashData(Encoding.Unicode.GetBytes(identity));

        private static byte[][] BuildDecryptionEntropyChain()
        {
            var chain = new List<byte[]> { CurrentEntropy };
            foreach (string release in VersionDerivedReleases)
            {
                // AssemblyName.FullName layout for a culture-neutral, non-strong-named assembly.
                chain.Add(DeriveEntropy(AppIdentity + ", Version=" + release + ", Culture=neutral, PublicKeyToken=null"));
            }
            chain.Add(Encoding.Unicode.GetBytes(PreDerivationEntropyLiteral));
            return chain.ToArray();
        }
    }

    /// <summary>
    /// Portable protector for macOS and Linux: AES-256-GCM with a random 96-bit nonce per
    /// value, keyed from a user-only key file in the application data directory.
    /// </summary>
    /// <remarks>
    /// This is file-permission security, not a hardware-backed keychain: anyone who can read
    /// the user's profile can read the key. That matches the protection DPAPI gives a
    /// same-user attacker on Windows. A keychain-backed implementation can replace this
    /// without touching call sites.
    /// </remarks>
    public sealed class AesFileSecretProtector : ISecretProtector
    {
        private const string Prefix = "AESGCM:";
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly object _gate = new object();
        private readonly string _keyPath;
        private byte[] _key;

        public AesFileSecretProtector(string appDataDirectory)
        {
            Directory.CreateDirectory(appDataDirectory);
            _keyPath = Path.Combine(appDataDirectory, "secret.key");
        }

        public string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            byte[] key = GetKey();
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] plain = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[TagSize];

            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, plain, cipher, tag);

            Array.Clear(plain, 0, plain.Length);

            byte[] blob = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, blob, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, blob, NonceSize + TagSize, cipher.Length);
            return Prefix + Convert.ToBase64String(blob);
        }

        public bool TryUnprotect(string ciphertext, out string plaintext, out bool writtenWithCurrentKey)
        {
            plaintext = null;
            // This protector has a single key, so any value it can open is current.
            writtenWithCurrentKey = true;
            if (string.IsNullOrEmpty(ciphertext))
            {
                plaintext = string.Empty;
                return true;
            }

            // A value written by the Windows build (bare base64 DPAPI blob) cannot be opened
            // here; report failure so the caller prompts for re-entry.
            if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
                return false;

            byte[] blob;
            try
            {
                blob = Convert.FromBase64String(ciphertext.Substring(Prefix.Length));
            }
            catch (FormatException)
            {
                return false;
            }

            if (blob.Length < NonceSize + TagSize)
                return false;

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] cipher = new byte[blob.Length - NonceSize - TagSize];
            Buffer.BlockCopy(blob, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(blob, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(blob, NonceSize + TagSize, cipher, 0, cipher.Length);

            byte[] plain = new byte[cipher.Length];
            try
            {
                using var aes = new AesGcm(GetKey(), TagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            catch (CryptographicException)
            {
                Array.Clear(plain, 0, plain.Length);
                return false;
            }

            plaintext = Encoding.UTF8.GetString(plain);
            Array.Clear(plain, 0, plain.Length);
            return true;
        }

        private byte[] GetKey()
        {
            lock (_gate)
            {
                if (_key is not null)
                    return _key;

                if (File.Exists(_keyPath))
                {
                    try
                    {
                        byte[] existing = File.ReadAllBytes(_keyPath);
                        if (existing.Length == 32)
                            return _key = existing;
                    }
                    catch (IOException ex)
                    {
                        Log.WriteWarning("Secret key could not be read: " + Log.SafeException(ex));
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Log.WriteWarning("Secret key could not be read: " + Log.SafeException(ex));
                    }
                }

                byte[] key = RandomNumberGenerator.GetBytes(32);
                WriteKey(key);
                return _key = key;
            }
        }

        private void WriteKey(byte[] key)
        {
            // Write to a temporary file and rename, so a crash never leaves a half key.
            string temp = _keyPath + ".tmp";
            File.WriteAllBytes(temp, key);
            TryRestrictPermissions(temp);
            File.Move(temp, _keyPath, overwrite: true);
            TryRestrictPermissions(_keyPath);
        }

        private static void TryRestrictPermissions(string path)
        {
            if (OperatingSystem.IsWindows())
                return;
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Could not restrict the secret key permissions: " + Log.SafeException(ex));
            }
        }
    }
}
