using System;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Origin allow-list for the native HTTP MCP endpoint (v1.8.0).
    /// Pure static logic, no dependency: source-linked into the test suite so the
    /// MCP HTTP transport spec requirement (validate Origin, DNS rebinding defense)
    /// is unit-tested like UpdateCheckLogic and McpHttpAuthLogic.
    /// <para>
    /// Allowed: no Origin (CLI/MCP clients/Tailscale), http(s)://localhost[:port],
    /// http(s)://127.0.0.1[:port], http(s)://[::1][:port], plus any origin listed in
    /// TOPSOLID_MCP_ALLOWED_ORIGINS (comma-separated, e.g.
    /// "http://localhost:3000,https://mydomain"). Everything else — any other
    /// http(s)://hostname — is rejected: a page loaded from an Intranet or LAN name
    /// must not be able to drive this server.
    /// </para>
    /// </summary>
    internal static class McpOriginPolicy
    {
        /// <summary>
        /// Validates an Origin header value. Returns null when the request is allowed,
        /// or a human-readable error message to reject it with (HTTP 403, JSON-RPC -32002).
        /// </summary>
        public static string Validate(string origin)
        {
            if (string.IsNullOrEmpty(origin))
                return null;

            Uri uri;
            try { uri = new Uri(origin); }
            catch (UriFormatException) { return "Origin header validation failed: malformed Origin"; }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return "Origin header validation failed: unexpected scheme";

            string host = uri.Host.ToLowerInvariant();
            string port = uri.IsDefaultPort ? "(default)" : uri.Port.ToString();
            bool isLocalHost = host == "localhost" || host == "127.0.0.1" || host == "[::1]" || host == "::1";

            if (isLocalHost)
                return null;

            string extra = Environment.GetEnvironmentVariable(AllowedOriginsEnvVar);
            if (!string.IsNullOrEmpty(extra))
            {
                foreach (string raw in extra.Split(','))
                {
                    if (raw.Trim().Equals(origin.Trim(), StringComparison.OrdinalIgnoreCase))
                        return null;
                }
            }

            return "Origin header validation failed: '" + uri.Scheme + "://" + host + ":" + port +
                   "' is not allowed (local server, DNS-rebinding defense).";
        }

        /// <summary>Comma-separated extra allowed origins (e.g. a LAN web client).</summary>
        public const string AllowedOriginsEnvVar = "TOPSOLID_MCP_ALLOWED_ORIGINS";
    }
}