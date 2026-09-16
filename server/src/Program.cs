using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using TopSolidApiGraph.Core;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Tools;
using TopSolidMcpServer.Utils;
using System.Windows.Forms;

namespace TopSolidMcpServer
{
    class Program
    {
        private const string MutexName = "Global\\TopSolidMcpServer_Singleton";

        static void Main(string[] args)
        {
            // --version flag
            if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
            {
                Console.WriteLine(TrayIcon.GetVersion());
                return;
            }

            // --compile [file] flag for dry-run validation via CLI
            if (args.Length > 1 && args[0] == "--compile")
            {
                string filePath = args[1];
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("Error: File not found: " + filePath);
                    Environment.Exit(1);
                }
                string code = File.ReadAllText(filePath);
                string result = ScriptExecutor.CompileOnly(code);
                Console.WriteLine(result);
                Environment.Exit(result.StartsWith("OK") ? 0 : 1);
            }

            bool createdNew;
            Mutex mutex;
            try
            {
                // Create (or open) the Global\ mutex with an explicit DACL granting Everyone:
                // without it, the SYSTEM-created mutex (bridge child in session 0) cannot even
                // be OPENED from the user session, which crashed with UnauthorizedAccessException.
                var security = new System.Security.AccessControl.MutexSecurity();
                security.AddAccessRule(new System.Security.AccessControl.MutexAccessRule(
                    new System.Security.Principal.SecurityIdentifier(
                        System.Security.Principal.WellKnownSidType.WorldSid, null),
                    System.Security.AccessControl.MutexRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                bool created;
                mutex = new Mutex(true, MutexName, out created, security);
                createdNew = created;
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex exists but was created by an older build without the Everyone DACL
                // (e.g. the current bridge child as SYSTEM). Try SYNCHRONIZE-only open; if even
                // that is denied, another instance IS running — report cleanly, never crash.
                createdNew = false;
                mutex = null;
                try
                {
                    var existing = Mutex.OpenExisting(MutexName, System.Security.AccessControl.MutexRights.Synchronize);
                    bool acquired = false;
                    try { acquired = existing.WaitOne(TimeSpan.FromSeconds(5)); }
                    catch (AbandonedMutexException) { acquired = true; } // Previous instance crashed — we take over
                    if (!acquired)
                    {
                        existing.Dispose();
                        ReportAlreadyRunning();
                        return;
                    }
                    // We took over an abandoned mutex — keep using it for the lifetime of the process.
                    using (existing)
                    {
                        RunServer(args);
                    }
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    // The existing mutex denies every open we can attempt (legacy SYSTEM-owned
                    // DACL). Treat as "already running": the bridge enforces the singleton anyway.
                    ReportAlreadyRunning();
                    return;
                }
                catch (System.Threading.WaitHandleCannotBeOpenedException)
                {
                    // Gone between the two calls — retry the Everyone-DACL creation.
                    bool created;
                    mutex = new Mutex(true, MutexName, out created);
                    createdNew = created;
                }
            }
            using (mutex)
            {
                if (!createdNew)
                {
                    // Another instance is already running — wait briefly then exit
                    bool acquired = false;
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                    catch (AbandonedMutexException) { acquired = true; } // Previous instance crashed — we take over
                    if (!acquired)
                    {
                        ReportAlreadyRunning();
                        return;
                    }
                }

                RunServer(args);

            } // mutex released automatically
        }

        /// <summary>
        /// Emits the "already running" notice (stderr + JSON-RPC error on stdout so MCP
        /// clients don't retry blindly), then terminates the process.
        /// </summary>
        private static void ReportAlreadyRunning()
        {
            Console.Error.WriteLine("[Program] Another TopSolidMcpServer instance is already running. Exiting.");

            // Return a valid JSON-RPC error so OpenClaw doesn't retry
            var errorResponse = new
            {
                jsonrpc = "2.0",
                id = (object)null,
                error = new
                {
                    code = -32000,
                    message = "TopSolidMcpServer is already running in another process."
                }
            };

            using (var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true })
            {
                writer.WriteLine(JsonConvert.SerializeObject(errorResponse));
            }
        }

        /// <summary>
        /// Contains the full server startup logic (graph loading, tool registration, stdio loop).
        /// Extracted from Main() to keep the mutex guard readable.
        /// </summary>
        private static void RunServer(string[] args)
        {
            // Register the TopSolid assembly resolver.
            // The resolved bin directory is cached in a captured local: the handler runs on
            // every failed assembly resolution, including ones unrelated to TopSolid.
            string topSolidBin = null;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                try
                {
                    if (topSolidBin == null)
                        topSolidBin = TopSolidMcpServer.Utils.TopSolidPathResolver.Resolve();

                    string assemblyName = new AssemblyName(resolveArgs.Name).Name;
                    string path = Path.Combine(topSolidBin, assemblyName + ".dll");
                    if (!File.Exists(path)) return null;

                    return Assembly.LoadFrom(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[MCP-WARN] Assembly resolve failed for " + resolveArgs.Name + ": " + ex.Message);
                    return null;
                }
            };

            string version = TrayIcon.GetVersion();
            Console.Error.WriteLine($"[MCP-INFO] TopSolid MCP Server v{version} starting...");

            bool noTray = HasFlag(args, "--no-tray") || IsEnvFlagSet("TOPSOLID_MCP_NO_TRAY");
            bool readOnly = HasFlag(args, "--read-only") || IsEnvFlagSet("TOPSOLID_MCP_READ_ONLY");

            if (readOnly)
                Console.Error.WriteLine("[MCP-INFO] Read-only mode enabled: modify_script is not registered and recipes run in read-only mode.");
            if (noTray)
                Console.Error.WriteLine("[MCP-INFO] Tray icon disabled (--no-tray / TOPSOLID_MCP_NO_TRAY).");

            TrayIcon tray = null;
            TopSolidConnector connector = null;

            try
            {
                TypeGraph graph = null;
                TypeNameResolver resolver = null;
                bool graphInitialized = false;

                // ── Connector: created IMMEDIATELY (not lazy) ──
                // so the tray icon and reconnect button work from the start.
                int port = 8090;
                string portArg = GetArg(args, "--port");
                if (portArg != null && int.TryParse(portArg, out int parsedPort))
                {
                    port = parsedPort;
                }
                connector = new TopSolidConnector(port);
                Console.Error.WriteLine($"[MCP-INFO] Connector ready (port {port}). Attempting initial connection...");
                connector.Connect(); // non-blocking, just tries once

                // ── Graph: lazy-loaded on first tool call (heavy) ──
                Action EnsureGraphLoaded = () =>
                {
                    if (graphInitialized) return;
                    graphInitialized = true;

                    Console.Error.WriteLine("[MCP-INFO] First tool call — loading graph...");

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string graphPath = Path.Combine(baseDir, "data", "graph.json");

                    if (!File.Exists(graphPath))
                    {
                        string rootPath = Path.Combine(baseDir, "graph.json");
                        if (File.Exists(rootPath)) graphPath = rootPath;
                    }
                    if (!File.Exists(graphPath))
                    {
                        string devPath = Path.Combine(baseDir, "..", "..", "..", "data", "graph.json");
                        if (File.Exists(devPath)) graphPath = devPath;
                    }

                    if (File.Exists(graphPath))
                    {
                        Console.Error.WriteLine($"[MCP-INFO] Loading graph from {graphPath}...");
                        var graphJson = File.ReadAllText(graphPath);
                        graph = JsonConvert.DeserializeObject<TypeGraph>(graphJson);
                        graph.RebuildAdjacencyList();
                        graph.BuildKeywordIndex();
                        Console.Error.WriteLine("[MCP-INFO] Graph loaded and indexed successfully.");
                    }
                    else
                    {
                        throw new FileNotFoundException($"Graph data not found at expected locations (e.g. {graphPath})");
                    }

                    resolver = new TypeNameResolver(graph);
                    Console.Error.WriteLine("[MCP-INFO] Graph initialization complete.");
                };

                var registry = new McpToolRegistry();

                // Register tools — graph is lazy, connector is immediate
                var findPathTool = new FindPathTool(() => { EnsureGraphLoaded(); return graph; }, () => { EnsureGraphLoaded(); return resolver; });
                findPathTool.Register(registry);

                var explorePathsTool = new ExplorePathsTool(() => { EnsureGraphLoaded(); return graph; }, () => { EnsureGraphLoaded(); return resolver; });
                explorePathsTool.Register(registry);

                var getStateTool = new GetStateTool(() => connector);
                getStateTool.Register(registry);

                var executeScriptTool = new ExecuteScriptTool(() => connector);
                executeScriptTool.Register(registry);

                if (!readOnly)
                {
                    var modifyScriptTool = new ModifyScriptTool(() => connector);
                    modifyScriptTool.Register(registry);
                }

                var apiHelpTool = new ApiHelpTool(() => { EnsureGraphLoaded(); return graph; });
                apiHelpTool.Register(registry);

                var recipeTool = new RecipeTool(() => connector, readOnly);
                recipeTool.Register(registry);

                var listRecipesTool = new ListRecipesTool();
                listRecipesTool.Register(registry);

                var getRecipeTool = new GetRecipeTool();
                getRecipeTool.Register(registry);

                var compileTool = new CompileTool();
                compileTool.Register(registry);

                var searchExamplesTool = new SearchExamplesTool();
                searchExamplesTool.Register(registry);

                var searchHelpTool = new SearchHelpTool();
                searchHelpTool.Register(registry);

                var searchCommandsTool = new SearchCommandsTool();
                searchCommandsTool.Register(registry);

                var router = new McpRouter(registry);
                var server = new McpStdioServer(router);

                // Start tray icon (background STA thread).
                // A headless or session-0 environment must never take the server down.
                if (!noTray)
                {
                    try
                    {
                        tray = new TrayIcon(() =>
                        {
                            Console.Error.WriteLine("[MCP-INFO] Shutdown requested from tray.");
                            try { connector.Disconnect(); } catch { }
                            // Remove the icon from the notification area before killing the
                            // process, otherwise a ghost icon stays until the user hovers it.
                            try { if (tray != null) tray.Dispose(); } catch { }
                            try { Console.In.Close(); } catch { }
                            Environment.Exit(0);
                        });
                        tray.Start();

                        // Wire tray icon to connector (AFTER tray.Start())
                        var startedTray = tray;
                        startedTray.SetPort(port);
                        startedTray.SetConnected(connector.IsConnected);

                        connector.ConnectionChanged += (connected) =>
                        {
                            startedTray.SetConnected(connected);
                            Console.Error.WriteLine(connected
                                ? "[MCP-INFO] TopSolid connection established."
                                : "[MCP-INFO] TopSolid connection lost.");
                        };

                        startedTray.SetReconnectAction(() =>
                        {
                            bool ok = connector.Connect();
                            Console.Error.WriteLine(ok
                                ? "[MCP-INFO] Manual reconnect succeeded."
                                : "[MCP-INFO] Manual reconnect failed - TopSolid not available on port " + port + ".");
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[MCP-WARN] Tray icon unavailable, continuing without it: " + ex.Message);
                        try { if (tray != null) tray.Dispose(); } catch { }
                        tray = null;
                    }
                }

                if (tray == null)
                {
                    connector.ConnectionChanged += (connected) =>
                    {
                        Console.Error.WriteLine(connected
                            ? "[MCP-INFO] TopSolid connection established."
                            : "[MCP-INFO] TopSolid connection lost.");
                    };
                }

                Console.Error.WriteLine("[MCP-INFO] Server ready. Listening on stdin.");
                server.Start();

                // stdin closed — the client is gone, release the TopSolid connection.
                Console.Error.WriteLine("[MCP-INFO] stdin closed. Shutting down.");
                connector.Disconnect();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-FATAL] Server crashed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);

                // Environment.Exit skips the finally block: release everything here.
                try { if (connector != null) connector.Disconnect(); } catch { }
                try { if (tray != null) tray.Dispose(); } catch { }
                Environment.Exit(1);
            }
            finally
            {
                tray?.Dispose();
            }
        }

        /// <summary>
        /// Extracts a named argument value from the CLI args array (e.g. --bridge http://...).
        /// </summary>
        private static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        /// <summary>
        /// True when the CLI args contain the given switch (e.g. --no-tray).
        /// </summary>
        private static bool HasFlag(string[] args, string name)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null && args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when the environment variable is set to an affirmative value (1, true, yes, on).
        /// </summary>
        private static bool IsEnvFlagSet(string variable)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value)) return false;

            value = value.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
