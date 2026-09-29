using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Pure logic for the native HTTP MCP endpoint: API key validation, masking,
    /// key generation, session-id handling, and HTTP status decisions.
    /// No I/O here so the unit tests can link this file directly
    /// (see server/tests/TopSolidMcp.Http.Tests — same pattern as UpdateCheckLogic).
    /// </summary>
    public static class McpHttpAuthLogic
    {
        /// <summary>
        /// API keys are 32 hex chars (128 bits of entropy) prefixed "tsmcp_".
        /// The prefix makes them recognizable in logs and clipboard.
        /// </summary>
        public const string KeyPrefix = "tsmcp_";
        public const int KeyLength = 32;

        /// <summary>Header the native HTTP server checks. Same as the old bridge.</summary>
        public const string ApiKeyHeader = "X-API-Key";

        /// <summary>Header echoed on initialize, per streamable-HTTP spec.</summary>
        public const string SessionIdHeader = "Mcp-Session-Id";

        /// <summary>
        /// Validates the shape of an API key (prefix + 32 hex chars).
        /// </summary>
        public static bool IsValidKeyShape(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return false;
            if (key.Length != KeyPrefix.Length + KeyLength) return false;
            for (int i = KeyPrefix.Length; i < key.Length; i++)
            {
                char c = key[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>
        /// Generates a new key: "tsmcp_" + 32 lowercase hex chars.
        /// </summary>
        public static string GenerateKey()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var sb = new StringBuilder(KeyPrefix.Length + KeyLength);
            sb.Append(KeyPrefix);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Masked form for display: "••••••ab12" (bullets + last 4).
        /// Full value never shown in menus, balloons or logs.
        /// </summary>
        public static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 4) return new string('•', key.Length);
            return "••••••" + key.Substring(key.Length - 4);
        }

        /// <summary>
        /// Constant-time comparison so a timing attack cannot reveal the key
        /// one character at a time. Length differs → still constant time.
        /// </summary>
        public static bool KeysEqual(string a, string b)
        {
            if (a == null) a = "";
            if (b == null) b = "";
            // Diff length leaks the length, which is public (fixed shape).
            bool same = a.Length == b.Length;
            int n = Math.Max(a.Length, b.Length);
            int acc = 0;
            for (int i = 0; i < n; i++)
            {
                char ca = i < a.Length ? a[i] : (char)0;
                char cb = i < b.Length ? b[i] : (char)0;
                acc |= ca ^ cb;
            }
            return same && acc == 0;
        }

        /// <summary>
        /// Whether the request is authorized given the expected key and the presented one.
        /// An empty expected key means auth disabled (local mode) → everything passes.
        /// </summary>
        public static bool IsAuthorized(string expectedKey, string presentedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return true;
            if (string.IsNullOrEmpty(presentedKey)) return false;
            return KeysEqual(expectedKey, presentedKey);
        }

        /// <summary>
        /// Generates a session id for the streamable-HTTP handshake (UUID v4).
        /// </summary>
        public static string NewSessionId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Whether a session id looks like one we issued (uuid-ish, non-empty).
        /// </summary>
        public static bool IsValidSessionId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
            return Guid.TryParse(sessionId, out _);
        }
    }
}