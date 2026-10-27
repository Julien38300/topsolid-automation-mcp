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

        // v1.8.10: SSE legacy transport (MCP 2024-11-05) — one open stream per
        // session, the response channel for POSTs made with ?sessionId=.
        private readonly Dictionary<string, SseSession> _sseSessions = new Dictionary<string, SseSession>();

        /// <summary>An open server-push SSE stream, owned by the thread that served the GET.</summary>
        private sealed class SseSession
        {
            public readonly System.Collections.Concurrent.BlockingCollection<string> Queue =
                new System.Collections.Concurrent.BlockingCollection<string>();
            public volatile bool Open;
        }

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
        /// Starts the HTTP listener on a background thread. Binds the ROOT of the
        /// port ("http://+:PORT/") so both /mcp and /status are reachable — a /mcp/
        /// -only prefix makes HttpListener answer 400 to every other path, which
        /// stranded the /status health endpoint (found by the v1.8.10 release
        /// probe). Falls back to localhost when the URL ACL denies "+".
        /// </summary>
        public void Start()
        {
            var prefixes = new[] { "http://+:" + _port + "/", "http://localhost:" + _port + "/" };
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

            // v1.8.10: unblock the SSE stream threads so they exit with the listener
            // (each one parks in Queue.TryTake and would otherwise outlive Stop()).
            lock (_sessionLock)
            {
                foreach (var sse in _sseSessions.Values)
                    sse.Open = false;
                _sseSessions.Clear();
            }

            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        /// <summary>
        /// Validates the Origin header per the MCP HTTP transport spec (DNS rebinding).
        /// The logic lives in <see cref="McpOriginPolicy"/> (pure, unit-tested); kept
        /// here for the handler's readability.
        /// </summary>
        internal static string ValidateOrigin(string origin)
        {
            return McpOriginPolicy.Validate(origin);
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
                // v1.8.10: one trace line per request — the only way to see what a
                // client (Claude, OpenWebUI, a small local model gateway…) actually
                // sends when its "connection test" fails.
                Console.Error.WriteLine("[MCP-HTTP] " + ctx.Request.HttpMethod + " " + ctx.Request.Url.PathAndQuery
                    + " from " + (ctx.Request.RemoteEndPoint != null ? ctx.Request.RemoteEndPoint.Address.ToString() : "?")
                    + " UA=" + (ctx.Request.Headers["User-Agent"] ?? "-")
                    + " key=" + (ctx.Request.Headers[McpHttpAuthLogic.ApiKeyHeader] != null ? "yes" : "no")
                    + " accept=" + (ctx.Request.Headers["Accept"] ?? "-"));
                // MCP spec (HTTP transport): a local server MUST validate the Origin
                // header — browsers always send it on cross-site POSTs, so a request
                // carrying a non-local Origin is rejected (DNS rebinding defense).
                // No Origin at all (curl, MCP clients, Tailscale peers) is allowed.
                string requestOrigin = ctx.Request.Headers["Origin"];
                string originError = ValidateOrigin(requestOrigin);
                if (originError != null)
                {
                    WriteJson(ctx, 403, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32002,\"message\":\"" + originError + "\"}}");
                    return;
                }

                // CORS for allowed local web clients only (harmless: auth still required).
                if (requestOrigin != null)
                    ctx.Response.Headers["Access-Control-Allow-Origin"] = requestOrigin;
                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-API-Key, Mcp-Session-Id, Mcp-Protocol-Version";
                    ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, DELETE, OPTIONS";
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                // Health endpoint, no auth: lets monitoring probe without a key.
                if (ctx.Request.HttpMethod == "GET" && ctx.Request.Url.AbsolutePath.TrimEnd('/') == "/status")
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

                // Root prefix listens to everything on the port: only /mcp is
                // the MCP endpoint, anything else is a plain 404.
                if (ctx.Request.Url.AbsolutePath.TrimEnd('/') != "/mcp")
                {
                    WriteJson(ctx, 404, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32000,\"message\":\"Not found: the MCP endpoint is /mcp (health check: GET /status)\"}}");
                    return;
                }

                // v1.8.10: GET on /mcp opens the SSE legacy stream (MCP 2024-11-05):
                // the server sends the messages endpoint ("event: endpoint"), then
                // keeps the stream open; replies to POSTs made with ?sessionId=
                // are pushed here. Same X-API-Key auth as POST — no open stream.
                if (ctx.Request.HttpMethod == "GET")
                {
                    string expectedGet = _getApiKey();
                    string presentedGet = ctx.Request.Headers[McpHttpAuthLogic.ApiKeyHeader];
                    if (!McpHttpAuthLogic.IsAuthorized(expectedGet, presentedGet))
                    {
                        WriteJson(ctx, 401, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32001,\"message\":\"Unauthorized: invalid or missing X-API-Key\"}}");
                        return;
                    }
                    OpenSseStream(ctx);
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

                // v1.8.10: SSE legacy — a POST carrying ?sessionId=<sid> targets the
                // endpoint received in the stream's "endpoint" event. Replies go
                // into the SSE stream; the POST itself is answered 202 Accepted.
                SseSession sse = null;
                string querySid = ctx.Request.QueryString["sessionId"];
                if (!string.IsNullOrEmpty(querySid))
                {
                    lock (_sessionLock)
                    {
                        DateTime touched;
                        if (_sessions.TryGetValue(querySid, out touched) && _sseSessions.ContainsKey(querySid))
                        {
                            sse = _sseSessions[querySid];
                            _sessions[querySid] = DateTime.UtcNow; // touch
                        }
                    }
                    if (sse == null || !sse.Open)
                    {
                        WriteJson(ctx, 404, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32002,\"message\":\"SSE session not found: open the stream first (GET /mcp)\"}}");
                        return;
                    }
                }

                ProcessBody(ctx, body, sse);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-HTTP] Request error: " + ex.Message);
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }

        private void ProcessBody(HttpListenerContext ctx, string body, SseSession sse)
        {
            JsonRpcRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(body);
            }
            catch (Exception)
            {
                Console.Error.WriteLine("[MCP-HTTP] 400 parse error, body (truncated): " + (body == null ? "" : (body.Length > 200 ? body.Substring(0, 200) : body)));
                WriteJson(ctx, 400, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error — this endpoint expects a JSON-RPC body. Streamable HTTP: POST /mcp (initialize first, then Mcp-Session-Id header). SSE legacy: GET /mcp stream + POST /mcp?sessionId=... Test the server with GET /status.\"}}");
                return;
            }

            if (request == null)
            {
                Console.Error.WriteLine("[MCP-HTTP] 400 invalid request, body (truncated): " + (body == null ? "" : (body.Length > 200 ? body.Substring(0, 200) : body)));
                WriteJson(ctx, 400, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32600,\"message\":\"Invalid Request — this endpoint expects a JSON-RPC body. Streamable HTTP: POST /mcp (initialize first, then Mcp-Session-Id header). SSE legacy: GET /mcp stream + POST /mcp?sessionId=... Test the server with GET /status.\"}}");
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

            // v1.8.10: SSE legacy — route and push the reply into the open stream;
            // the POST itself is answered 202 Accepted (2024-11-05 transport).
            if (sse != null)
            {
                var sseResp = _router.Route(request);
                string sseJson = JsonConvert.SerializeObject(sseResp, Formatting.None);
                try
                {
                    sse.Queue.Add("data: " + sseJson + "\n\n");
                    ctx.Response.StatusCode = 202;
                }
                catch (Exception pushEx)
                {
                    Console.Error.WriteLine("[MCP-HTTP] SSE push failed: " + pushEx.Message);
                    ctx.Response.StatusCode = 503;
                }
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

        /// <summary>
        /// v1.8.10: opens the SSE legacy stream (MCP 2024-11-05): sends the messages
        /// endpoint, then serves replies pushed by POSTs carrying ?sessionId=.
        /// Runs on the request thread until the client closes the stream.
        /// </summary>
        private void OpenSseStream(HttpListenerContext ctx)
        {
            string sid = McpHttpAuthLogic.NewSessionId();
            var sse = new SseSession();
            lock (_sessionLock)
            {
                _sessions[sid] = DateTime.UtcNow;
                _sseSessions[sid] = sse;
            }

            try
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.Headers["Cache-Control"] = "no-cache";
                var outStream = ctx.Response.OutputStream;
                byte[] hello = Encoding.UTF8.GetBytes("event: endpoint\ndata: /mcp?sessionId=" + sid + "\n\n");
                outStream.Write(hello, 0, hello.Length);
                outStream.Flush();
                sse.Open = true;
                Console.Error.WriteLine("[MCP-HTTP] SSE stream opened (session " + sid + ").");

                var lastPing = DateTime.UtcNow;
                while (sse.Open && _running)
                {
                    string item;
                    if (sse.Queue.TryTake(out item, TimeSpan.FromSeconds(5)))
                    {
                        byte[] payload = Encoding.UTF8.GetBytes(item);
                        outStream.Write(payload, 0, payload.Length);
                        outStream.Flush();
                        lastPing = DateTime.UtcNow;
                    }
                    else if ((DateTime.UtcNow - lastPing).TotalSeconds > 20)
                    {
                        byte[] ping = Encoding.UTF8.GetBytes(": ping\n\n");
                        outStream.Write(ping, 0, ping.Length);
                        outStream.Flush();
                        lastPing = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                // Client closed the stream / server stopping: normal SSE teardown.
                Console.Error.WriteLine("[MCP-HTTP] SSE stream closed (session " + sid + "): " + ex.Message);
            }
            finally
            {
                sse.Open = false;
                lock (_sessionLock)
                {
                    _sessions.Remove(sid);
                    _sseSessions.Remove(sid);
                }
                try { ctx.Response.Close(); } catch { }
            }
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