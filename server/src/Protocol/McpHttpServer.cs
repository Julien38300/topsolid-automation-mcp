using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Native MCP server over streamable HTTP (POST /mcp with JSON-RPC bodies).
    /// Replaces the old node mcp-proxy: one process, one port, X-API-Key auth.
    /// Reuses McpRouter so stdio and HTTP serve exactly the same tools.
    /// </summary>
    public class McpHttpServer
    {
        private readonly McpRouter _router;
        private readonly Func<string> _getApiKey;   // live key (hot-reloadable from the tray)
        private readonly int _port;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        // Sessions issued at initialize; required on subsequent requests.
        private readonly Dictionary<string, DateTime> _sessions = new Dictionary<string, DateTime>();
        private readonly object _sessionLock = new object();
        private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(12);

        public McpHttpServer(McpRouter router, int port, Func<string> getApiKey)
        {
            _router = router;
            _port = port;
            _getApiKey = getApiKey ?? (() => null);
        }

        public bool IsRunning => _running;

        /// <summary>Local endpoint the listener binds (http://+:PORT/mcp requires admin).</summary>
        private string Url => "http://+:" + _port + "/mcp/";

        /// <summary>
        /// Starts the HTTP listener on a background thread. Binds "+" when possible
        /// (Tailscale/LAN access), falls back to localhost when the URL ACL denies it.
        /// </summary>
        public void Start()
        {
            var prefixes = new[] { "http://+:" + _port + "/mcp/", "http://localhost:" + _port + "/mcp/" };
            foreach (var prefix in prefixes)
            {
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add(prefix);
                    listener.Start();
                    _listener = listener;
                    break;
                }
                catch (HttpListenerException ex)
                {
                    Console.Error.WriteLine("[MCP-HTTP] Cannot bind " + prefix + ": " + ex.Message);
                }
            }

            if (_listener == null)
                throw new InvalidOperationException("No HTTP prefix could be bound (port " + _port + ").");

            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "McpHttpServer" };
            _thread.Start();
            Console.Error.WriteLine("[MCP-HTTP] Listening on " + _listener.Prefixes.FirstOrDefault());
        }

        /// <summary>Stops the listener and the accept thread.</summary>
        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch (Exception) { if (_running) Thread.Sleep(200); continue; }

                ThreadPool.QueueUserWorkItem(_ => { try { Handle(ctx); } catch (Exception ex) { Console.Error.WriteLine("[MCP-HTTP] Handler error: " + ex.Message); } });
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            try
            {
                // CORS for local web clients (harmless: auth still required).
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-API-Key, Mcp-Session-Id, Mcp-Protocol-Version";
                    ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, DELETE, OPTIONS";
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                // Health endpoint, no auth: lets monitoring probe without a key.
                if (ctx.Request.Url.AbsolutePath.TrimEnd('/') == "/status")
                {
                    WriteJson(ctx, 200, JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["service"] = "TopSolid-MCP-Server",
                        ["version"] = ReadVersion(),
                        ["http"] = "up",
                        ["uptime_seconds"] = (int)(DateTime.UtcNow - _started).TotalSeconds
                    }));
                    return;
                }

                // GET on /mcp (SSE stream): not supported; answer 405 per spec.
                if (ctx.Request.HttpMethod == "GET")
                {
                    ctx.Response.StatusCode = 405;
                    ctx.Response.Close();
                    return;
                }

                // DELETE on /mcp: end a session.
                if (ctx.Request.HttpMethod == "DELETE")
                {
                    string sid = ctx.Request.Headers[McpHttpAuthLogic.SessionIdHeader];
                    if (!string.IsNullOrEmpty(sid))
                    {
                        lock (_sessionLock) _sessions.Remove(sid);
                    }
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                // Auth on everything else.
                string expected = _getApiKey();
                string presented = ctx.Request.Headers[McpHttpAuthLogic.ApiKeyHeader];
                if (!McpHttpAuthLogic.IsAuthorized(expected, presented))
                {
                    WriteJson(ctx, 401, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32001,\"message\":\"Unauthorized: invalid or missing X-API-Key\"}}");
                    return;
                }

                if (ctx.Request.HttpMethod != "POST")
                {
                    ctx.Response.StatusCode = 405;
                    ctx.Response.Close();
                    return;
                }

                string body;
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                ProcessBody(ctx, body);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-HTTP] Request error: " + ex.Message);
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }

        private void ProcessBody(HttpListenerContext ctx, string body)
        {
            JsonRpcRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(body);
            }
            catch (Exception)
            {
                WriteJson(ctx, 400, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error\"}}");
                return;
            }

            if (request == null)
            {
                WriteJson(ctx, 400, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"}}");
                return;
            }

            // Notifications: 202 Accepted, no body (streamable-HTTP spec).
            if (request.Id == null ||
                (request.Method != null && request.Method.StartsWith("notifications/", StringComparison.Ordinal)))
            {
                if (request.Method == "notifications/initialized")
                {
                    Console.Error.WriteLine("[MCP-HTTP] Session initialized (session " + (ctx.Request.Headers[McpHttpAuthLogic.SessionIdHeader] ?? "new") + ").");
                }
                ctx.Response.StatusCode = 202;
                ctx.Response.Close();
                return;
            }

            // initialize: create a session and echo its id.
            if (request.Method == "initialize")
            {
                string sid = McpHttpAuthLogic.NewSessionId();
                lock (_sessionLock) _sessions[sid] = DateTime.UtcNow;
                var response = _router.Route(request);
                ctx.Response.Headers[McpHttpAuthLogic.SessionIdHeader] = sid;
                WriteJson(ctx, 200, JsonConvert.SerializeObject(response, Formatting.None));
                return;
            }

            // Everything else requires a valid session id.
            string sessionId = ctx.Request.Headers[McpHttpAuthLogic.SessionIdHeader];
            lock (_sessionLock)
            {
                if (!McpHttpAuthLogic.IsValidSessionId(sessionId) || !_sessions.ContainsKey(sessionId))
                {
                    WriteJson(ctx, 404, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32002,\"message\":\"Session not found: initialize first\"}}");
                    return;
                }
                _sessions[sessionId] = DateTime.UtcNow; // touch
            }

            var resp = _router.Route(request);
            WriteJson(ctx, 200, JsonConvert.SerializeObject(resp, Formatting.None));
        }

        private DateTime _started = DateTime.UtcNow;

        private static string ReadVersion()
        {
            try
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "0.0.0" : v.Major + "." + v.Minor + "." + v.Build;
            }
            catch { return "0.0.0"; }
        }

        private static void WriteJson(HttpListenerContext ctx, int status, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            try { ctx.Response.OutputStream.Write(bytes, 0, bytes.Length); } catch { }
            try { ctx.Response.Close(); } catch { }
        }
    }

    internal static class PrefixExt
    {
        public static string FirstOrDefault(this HttpListenerPrefixCollection c)
        {
            foreach (var p in c) return p;
            return null;
        }
    }
}