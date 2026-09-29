using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// API key storage: settings.json holds the key DPAPI-encrypted (entropy: assembly
    /// location), the env var TOPSOLID_MCP_API_KEY is the migration source (read once,
    /// then persisted). Pure, side-effect-light so tests can link the file directly.
    /// </summary>
    public static class ApiKeyStore
    {
        public const string EnvVarName = "TOPSOLID_MCP_API_KEY";

        // ---- Pure helpers (unit-tested) ----

        /// <summary>DPAPI-encrypt a key for the current user. Returns base64 ciphertext.</summary>
        public static string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return "";
            byte[] bytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] encrypted = ProtectedData.Protect(bytes, Entropy(), DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>DPAPI-decrypt. Returns "" on failure (never throws: wrong machine/user = empty).</summary>
        public static string Unprotect(string base64Ciphertext)
        {
            if (string.IsNullOrEmpty(base64Ciphertext)) return "";
            try
            {
                byte[] encrypted = Convert.FromBase64String(base64Ciphertext);
                byte[] bytes = ProtectedData.Unprotect(encrypted, Entropy(), DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }

        // The entropy binds the ciphertext to this application, so a stray DPAPI blob
        // from another tool cannot be swapped in. Static getter = testable seam.
        private static byte[] Entropy()
        {
            return Encoding.UTF8.GetBytes("TopSolidMcpServer.v1.apikey");
        }

        /// <summary>
        /// Resolves the effective key: settings.json first, env var as migration fallback.
        /// Pure: takes both candidates, decides. No I/O.
        /// </summary>
        public static string ResolveKey(string storedKey, string envKey)
        {
            if (!string.IsNullOrEmpty(storedKey)) return storedKey;
            if (!string.IsNullOrEmpty(envKey)) return envKey;
            return "";
        }

        // ---- File-backed state (thin, mirrors TraySettings) ----

        private static string FilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        /// <summary>Reads the stored (decrypted) key from settings.json, "" if absent.</summary>
        public static string LoadStoredKey()
        {
            try
            {
                string path = FilePath();
                if (!File.Exists(path)) return "";
                var obj = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                string enc = obj.Value<string>("api_key_dpapi") ?? "";
                return Unprotect(enc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ApiKeyStore] Load failed: " + ex.Message);
                return "";
            }
        }

        /// <summary>Persists the key (DPAPI-encrypted) into settings.json. Never throws.</summary>
        public static void SaveKey(string key)
        {
            try
            {
                string path = FilePath();
                var obj = File.Exists(path)
                    ? Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))
                    : new Newtonsoft.Json.Linq.JObject();
                obj["api_key_dpapi"] = string.IsNullOrEmpty(key) ? "" : Protect(key);
                File.WriteAllText(path, obj.ToString());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ApiKeyStore] Save failed: " + ex.Message);
            }
        }
    }
}