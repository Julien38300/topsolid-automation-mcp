using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Manages the connection to a running TopSolid instance through the Automation API.
    /// <para>
    /// Only <c>TopSolid.Kernel.Automating</c> is mandatory. The Design and Drafting
    /// modules are optional: they are probed on disk and loaded through dedicated
    /// non-inlined helpers so that a missing or unlicensed module degrades gracefully
    /// (related tools are disabled) instead of terminating the server. This is required
    /// because the CLR resolves the assemblies referenced by a method body when that
    /// method is JIT-compiled, i.e. before any try/catch inside it can run.
    /// </para>
    /// </summary>
    public class TopSolidConnector
    {
        private const string KernelDll = "TopSolid.Kernel.Automating.dll";
        private const string DesignDll = "TopSolid.Cad.Design.Automating.dll";
        private const string DraftingDll = "TopSolid.Cad.Drafting.Automating.dll";

        /// <summary>Guards every connection state transition (the tray thread and the stdio thread both call in).</summary>
        private readonly object _lock = new object();

        private volatile bool _isConnected;
        private volatile bool _hasDesignModule;
        private volatile bool _hasDraftingModule;
        private volatile string _version = "unknown";

        private readonly int _port;
        private DateTime _lastConnectAttempt = DateTime.MinValue;
        private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Fired when connection status changes. Used by TrayIcon to update UI.
        /// </summary>
        public event Action<bool> ConnectionChanged;

        /// <summary>
        /// Creates a connector targeting a specific TopSolid instance via TCP port.
        /// </summary>
        /// <param name="port">TCP port (default 8090). Set in TopSolid: Tools &gt; Options &gt; General &gt; Automation.</param>
        public TopSolidConnector(int port = 8090)
        {
            _port = port;
        }

        /// <summary>
        /// Last known connection state. Side-effect free: this is a plain field read,
        /// it never calls the TopSolid API. Use <see cref="RefreshConnectionState"/> or
        /// <see cref="EnsureConnected"/> to actually probe the connection.
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        /// <summary>
        /// True when the optional TopSolid Design Automation module was loaded successfully.
        /// Tools relying on <c>TopSolidDesignHost</c> must be skipped when this is false.
        /// </summary>
        public bool HasDesignModule
        {
            get { return _hasDesignModule; }
        }

        /// <summary>
        /// True when the optional TopSolid Drafting Automation module was loaded successfully.
        /// Tools relying on <c>TopSolidDraftingHost</c> must be skipped when this is false.
        /// </summary>
        public bool HasDraftingModule
        {
            get { return _hasDraftingModule; }
        }

        /// <summary>
        /// TCP port this connector targets.
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        /// Opens the connection to a running TopSolid instance.
        /// Uses DefineConnection to target the specific TCP port, which allows several
        /// TopSolid instances to coexist on the same machine.
        /// Optional modules are connected on a best-effort basis and never fail the call.
        /// </summary>
        /// <returns>True when the connection is established, otherwise false.</returns>
        public bool Connect()
        {
            bool wasConnected;
            bool nowConnected;

            lock (_lock)
            {
                _lastConnectAttempt = DateTime.UtcNow;
                wasConnected = _isConnected;

                string binPath = TopSolidPathResolver.Resolve();
                string version = null;
                bool ok = false;

                try
                {
                    ok = ConnectKernel(_port, out version);
                }
                catch (FileNotFoundException ex)
                {
                    Console.Error.WriteLine("[MCP-ERROR] Core module " + KernelDll + " could not be loaded from " +
                        binPath + " (" + ex.Message + "). TopSolid tools are unavailable. " +
                        "Set TOPSOLID_BIN_PATH to the TopSolid bin directory.");
                }
                catch (TypeLoadException ex)
                {
                    Console.Error.WriteLine("[MCP-ERROR] Core module " + KernelDll + " is incompatible (" + ex.Message + ").");
                }
                catch (BadImageFormatException ex)
                {
                    Console.Error.WriteLine("[MCP-ERROR] Core module " + KernelDll + " has a wrong architecture (" + ex.Message + ").");
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.Error.WriteLine("[MCP-ERROR] Core module " + KernelDll + " failed to load its types (" + ex.Message + ").");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[TopSolidConnector] Connection error: " + ex.Message);
                }

                _isConnected = ok;
                _version = ok && version != null ? version : "unknown";

                if (ok)
                {
                    // Optional modules: never fatal.
                    _hasDesignModule = TryLoadModule("Design", DesignDll, ConnectDesignModule);
                    _hasDraftingModule = TryLoadModule("Drafting", DraftingDll, ConnectDraftingModule);

                    Console.Error.WriteLine("[TopSolidConnector] Connected to TopSolid v" + _version +
                        " on port " + _port + " (Design: " + (_hasDesignModule ? "yes" : "no") +
                        ", Drafting: " + (_hasDraftingModule ? "yes" : "no") + ").");
                }
                else
                {
                    _hasDesignModule = false;
                    _hasDraftingModule = false;
                    Console.Error.WriteLine("[TopSolidConnector] TopSolid not available on port " + _port + ".");
                }

                nowConnected = _isConnected;
            }

            // Notify listeners outside the lock so handlers never run while holding it.
            if (wasConnected != nowConnected)
                RaiseConnectionChanged(nowConnected);

            return nowConnected;
        }

        /// <summary>
        /// Ensures a live connection to TopSolid, auto-reconnecting if needed.
        /// Call this before every operation that requires TopSolid.
        /// Respects a cooldown to avoid hammering the connection.
        /// </summary>
        /// <returns>True if connected, false if TopSolid is unreachable.</returns>
        public bool EnsureConnected()
        {
            // The lock is re-entrant: Connect() takes it again on this same thread.
            lock (_lock)
            {
                if (_isConnected && CheckConnection())
                    return true;

                // Cooldown: don't retry too fast
                if (DateTime.UtcNow - _lastConnectAttempt < ReconnectCooldown)
                    return false;

                Console.Error.WriteLine("[TopSolidConnector] Connection lost - attempting reconnect...");
                return Connect();
            }
        }

        /// <summary>
        /// Probes the TopSolid API and updates the cached connection state accordingly.
        /// This is the explicit, side-effecting counterpart of <see cref="IsConnected"/>.
        /// </summary>
        /// <returns>True when the connection answered.</returns>
        public bool RefreshConnectionState()
        {
            lock (_lock)
            {
                return CheckConnection();
            }
        }

        /// <summary>
        /// Closes the connection to TopSolid. Optional modules are disconnected first,
        /// each one in isolation so a missing module cannot break the shutdown path.
        /// </summary>
        public void Disconnect()
        {
            bool wasConnected;

            lock (_lock)
            {
                wasConnected = _isConnected;
                if (!wasConnected) return;

                if (_hasDraftingModule)
                    TryLoadModule("Drafting", DraftingDll, DisconnectDraftingModule);
                if (_hasDesignModule)
                    TryLoadModule("Design", DesignDll, DisconnectDesignModule);

                try
                {
                    DisconnectKernel();
                    Console.Error.WriteLine("[TopSolidConnector] Disconnected.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[TopSolidConnector] Disconnection error: " + ex.Message);
                }

                _isConnected = false;
                _hasDesignModule = false;
                _hasDraftingModule = false;
            }

            RaiseConnectionChanged(false);
        }

        /// <summary>
        /// Returns a human-readable snapshot of the TopSolid session: version, resolved
        /// bin directory, detected optional modules, edited document and project.
        /// </summary>
        /// <returns>A description of the current state, or an explanatory message.</returns>
        public string GetState()
        {
            if (!EnsureConnected())
            {
                return "Not connected to TopSolid. Please check that TopSolid is running with Automation enabled (port " + _port + ").\n" +
                       "The tools find_path and explore_paths are still available.";
            }

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("TopSolid version : " + GetKernelVersion());
                sb.AppendLine("Automation port : " + _port);
                sb.AppendLine("Bin path : " + TopSolidPathResolver.Resolve() +
                    (TopSolidPathResolver.Found ? "" : " (not found - hardcoded fallback)"));
                sb.AppendLine("Modules : Design = " + (_hasDesignModule ? "available" : "not available") +
                    ", Drafting = " + (_hasDraftingModule ? "available" : "not available"));

                var docId = TopSolidHost.Documents.EditedDocument;
                if (docId.IsEmpty)
                {
                    sb.AppendLine("State : connected (no document currently edited)");
                    return sb.ToString();
                }

                string docName = TopSolidHost.Documents.GetName(docId);
                string docExtension = Path.GetExtension(docName);
                string projectName = "unknown";

                var pdmObj = TopSolidHost.Documents.GetPdmObject(docId);
                if (!pdmObj.IsEmpty)
                {
                    var projectId = TopSolidHost.Pdm.GetProject(pdmObj);
                    if (!projectId.IsEmpty)
                    {
                        projectName = TopSolidHost.Pdm.GetName(projectId);
                    }
                }

                sb.AppendLine("Edited document : " + Path.GetFileNameWithoutExtension(docName));
                sb.AppendLine("Type : " + docExtension);
                sb.AppendLine("Project : " + projectName);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TopSolidConnector] Error getting state: " + ex.Message);
                return "Failed to read the TopSolid state: " + ex.Message;
            }
        }

        /// <summary>
        /// Checks whether the connection still answers, with a lightweight API call.
        /// Updates the cached state and raises <see cref="ConnectionChanged"/> when it drops.
        /// Callers must hold <see cref="_lock"/>.
        /// </summary>
        private bool CheckConnection()
        {
            bool alive;

            try
            {
                alive = PingKernel();
            }
            catch (Exception)
            {
                // Any API failure means the link is down, whatever the exception type.
                alive = false;
            }

            if (!alive)
            {
                bool wasConnected = _isConnected;
                _isConnected = false;
                if (wasConnected)
                {
                    Console.Error.WriteLine("[TopSolidConnector] Connection lost to TopSolid.");
                    RaiseConnectionChanged(false);
                }
            }

            return alive;
        }

        private void RaiseConnectionChanged(bool connected)
        {
            var handler = ConnectionChanged;
            if (handler == null) return;
            try { handler(connected); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TopSolidConnector] ConnectionChanged handler error: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Optional module loading
        //
        // Each optional-module call lives in its own [MethodImpl(NoInlining)] helper.
        // The assemblies a method body references are resolved when that method is
        // JIT-compiled — which happens at the call site below, inside the try/catch.
        // Inlining them into Connect()/Disconnect() would move the resolution (and the
        // FileNotFoundException) before the try/catch could ever run. See issue #11.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Probes <paramref name="dllName"/> on disk then invokes <paramref name="call"/>,
        /// swallowing every assembly-loading failure.
        /// </summary>
        /// <returns>True when the module call succeeded.</returns>
        private static bool TryLoadModule(string moduleName, string dllName, Action call)
        {
            if (!ModuleDllExists(dllName))
            {
                Console.Error.WriteLine("[MCP-WARN] Module " + moduleName + " not available - related tools disabled.");
                return false;
            }

            try
            {
                call();
                return true;
            }
            catch (FileNotFoundException ex)
            {
                LogModuleUnavailable(moduleName, ex.Message);
            }
            catch (TypeLoadException ex)
            {
                LogModuleUnavailable(moduleName, ex.Message);
            }
            catch (BadImageFormatException ex)
            {
                LogModuleUnavailable(moduleName, ex.Message);
            }
            catch (ReflectionTypeLoadException ex)
            {
                LogModuleUnavailable(moduleName, ex.Message);
            }
            catch (Exception ex)
            {
                LogModuleUnavailable(moduleName, ex.Message);
            }

            return false;
        }

        private static void LogModuleUnavailable(string moduleName, string detail)
        {
            Console.Error.WriteLine("[MCP-WARN] Module " + moduleName + " not available - related tools disabled. " + detail);
        }

        /// <summary>
        /// True when the module assembly sits either in the resolved TopSolid bin
        /// directory or next to the server executable.
        /// </summary>
        private static bool ModuleDllExists(string dllName)
        {
            try
            {
                string binPath = TopSolidPathResolver.Resolve();
                if (File.Exists(Path.Combine(binPath, dllName))) return true;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return File.Exists(Path.Combine(baseDir, dllName));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] Could not probe " + dllName + ": " + ex.Message);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ConnectKernel(int port, out string version)
        {
            version = null;

            // Target the specific TopSolid instance by TCP port. Without this,
            // Connect() picks the first available named pipe, which fails when
            // several TopSolid versions are running.
            TopSolidHost.DefineConnection("localhost", port, null, 0);

            // Connect() returns false even when the connection succeeds (TopSolid 7.20
            // bug). The return value is ignored and verified with a real API call.
            TopSolidHost.Connect(false, 10000);

            var v = TopSolidHost.Version;
            version = v.ToString();
            return v > 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisconnectKernel()
        {
            TopSolidHost.Disconnect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PingKernel()
        {
            var app = TopSolidHost.Application;
            return app != null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetKernelVersion()
        {
            return TopSolidHost.Version.ToString();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ConnectDesignModule()
        {
            TopSolidDesignHost.Connect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisconnectDesignModule()
        {
            TopSolidDesignHost.Disconnect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ConnectDraftingModule()
        {
            TopSolidDraftingHost.Connect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisconnectDraftingModule()
        {
            TopSolidDraftingHost.Disconnect();
        }
    }
}
