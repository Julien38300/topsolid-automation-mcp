using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using TopSolidApiGraph.Core;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Tools;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer
{
    class Program
    {
        private const string MutexName = "Global\\TopSolidMcpServer_Singleton";

        static void Main(string[] args)
        {
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
                string topSolidBin = @"C:\Program Files\TOPSOLID\TopSolid 7.20\bin\";
                string assemblyName = new System.Reflection.AssemblyName(resolveArgs.Name).Name;
                string path = System.IO.Path.Combine(topSolidBin, assemblyName + ".dll");
                return System.IO.File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
            };

            Console.Error.WriteLine("[MCP-INFO] TopSolid MCP Server starting...");

            try
            {
                TypeGraph graph = null;
                TopSolidConnector connector = null;
                TypeNameResolver resolver = null;
                bool initialized = false;

                Action EnsureInitialized = () =>
                {
                    if (initialized) return;
                    initialized = true;

                    Console.Error.WriteLine("[MCP-INFO] First tool call — performing slow initialization...");

                    // Path to graph data — search in multiple locations
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string graphPath = Path.Combine(baseDir, "data", "graph.json");

                    if (!File.Exists(graphPath))
                    {
                        // Fallback: graph.json next to the .exe (release layout)
                        string rootPath = Path.Combine(baseDir, "graph.json");
                        if (File.Exists(rootPath)) graphPath = rootPath;
                    }
                    if (!File.Exists(graphPath))
                    {
                        // Fallback: dev layout (src/bin/Debug/net48 -> data/)
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

                    // Initialize the TopSolid connector (Automation API)
                    connector = new TopSolidConnector();
                    connector.Connect();

                    resolver = new TypeNameResolver(graph);

                    Console.Error.WriteLine("[MCP-INFO] Initialization complete.");
                };

                var registry = new McpToolRegistry();

                // Register tools
                var findPathTool = new FindPathTool(() => { EnsureInitialized(); return graph; }, () => { EnsureInitialized(); return resolver; });
                findPathTool.Register(registry);

                var explorePathsTool = new ExplorePathsTool(() => { EnsureInitialized(); return graph; }, () => { EnsureInitialized(); return resolver; });
                explorePathsTool.Register(registry);

                var getStateTool = new GetStateTool(() => { EnsureInitialized(); return connector; });
                getStateTool.Register(registry);

                var executeScriptTool = new ExecuteScriptTool(() => { EnsureInitialized(); return connector; });
                executeScriptTool.Register(registry);

                var modifyScriptTool = new ModifyScriptTool(() => { EnsureInitialized(); return connector; });
                modifyScriptTool.Register(registry);

                var apiHelpTool = new ApiHelpTool(() => { EnsureInitialized(); return graph; });
                apiHelpTool.Register(registry);

                var recipeTool = new RecipeTool(() => { EnsureInitialized(); return connector; });
                recipeTool.Register(registry);

                var router = new McpRouter(registry);
                var server = new McpStdioServer(router);

                Console.Error.WriteLine("[MCP-INFO] Server ready. Listening on stdin.");
                server.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-FATAL] Server crashed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
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
