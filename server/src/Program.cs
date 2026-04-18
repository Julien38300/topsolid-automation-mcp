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

            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running — wait briefly then exit
                    bool acquired = false;
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                    catch (AbandonedMutexException) { acquired = true; } // Previous instance crashed — we take over
                    if (!acquired)
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

                        return;
                    }
                }

                RunServer(args);

            } // mutex released automatically
        }

        /// <summary>
        /// Contains the full server startup logic (graph loading, tool registration, stdio loop).
        /// Extracted from Main() to keep the mutex guard readable.
        /// </summary>
        private static void RunServer(string[] args)
        {
            // Register the TopSolid assembly resolver
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string topSolidBin = @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\";
                string assemblyName = new System.Reflection.AssemblyName(resolveArgs.Name).Name;
                string path = System.IO.Path.Combine(topSolidBin, assemblyName + ".dll");
                return System.IO.File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
            };

            string version = TrayIcon.GetVersion();
            Console.Error.WriteLine($"[MCP-INFO] TopSolid MCP Server v{version} starting...");

            TrayIcon tray = null;

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
                var connector = new TopSolidConnector(port);
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

                var modifyScriptTool = new ModifyScriptTool(() => connector);
                modifyScriptTool.Register(registry);

                var apiHelpTool = new ApiHelpTool(() => { EnsureGraphLoaded(); return graph; });
                apiHelpTool.Register(registry);

                var recipeTool = new RecipeTool(() => connector);
                recipeTool.Register(registry);

                var getRecipeTool = new GetRecipeTool();
                getRecipeTool.Register(registry);

                var compileTool = new CompileTool();
                compileTool.Register(registry);

                var searchExamplesTool = new SearchExamplesTool();
                searchExamplesTool.Register(registry);

                var whatsNewTool = new WhatsNewTool();
                whatsNewTool.Register(registry);

                var router = new McpRouter(registry);
                var server = new McpStdioServer(router);

                // Start tray icon (background STA thread)
                tray = new TrayIcon(() =>
                {
                    Console.Error.WriteLine("[MCP-INFO] Shutdown requested from tray.");
                    try { Console.In.Close(); } catch { }
                    Environment.Exit(0);
                });
                tray.Start();

                // Wire tray icon to connector (AFTER tray.Start())
                tray.SetPort(port);
                tray.SetConnected(connector.IsConnected);

                connector.ConnectionChanged += (connected) =>
                {
                    tray.SetConnected(connected);
                    Console.Error.WriteLine(connected
                        ? "[MCP-INFO] TopSolid connection established."
                        : "[MCP-INFO] TopSolid connection lost.");
                };

                tray.SetReconnectAction(() =>
                {
                    bool ok = connector.Connect();
                    Console.Error.WriteLine(ok
                        ? "[MCP-INFO] Manual reconnect succeeded."
                        : "[MCP-INFO] Manual reconnect failed — TopSolid not available on port " + port + ".");
                });

                Console.Error.WriteLine("[MCP-INFO] Server ready. Listening on stdin.");
                server.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-FATAL] Server crashed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
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
    }
}
