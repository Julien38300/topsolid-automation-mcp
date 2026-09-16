using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// System tray icon for TopSolid MCP Server.
    /// Runs on a dedicated STA thread so the main thread can block on stdin.
    /// <para>
    /// Every UI mutation is marshalled to that STA thread: the message loop belongs to
    /// it, so calling <see cref="Application.ExitThread"/> from the main thread would be
    /// a no-op and leave the icon in the notification area.
    /// </para>
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private const string GitHubUrl = "https://github.com/Julien38300/topsolid-automation-mcp";
        private const string DocsUrl = "https://julien38300.github.io/topsolid-automation-mcp/";

        private NotifyIcon _notifyIcon;
        private Thread _thread;
        private readonly Action _onShutdownRequested;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _reconnectItem;
        private Action _onReconnectRequested;
        private int _port;
        private bool? _lastConnectedState;

        /// <summary>
        /// Synchronization context owned by the tray STA thread. Created inside
        /// <see cref="RunTray"/> so that <see cref="Dispose"/> can post the shutdown
        /// onto the thread that actually runs the message loop.
        /// </summary>
        private volatile SynchronizationContext _uiContext;

        private volatile bool _disposed;

        public TrayIcon(Action onShutdownRequested)
        {
            _onShutdownRequested = onShutdownRequested;
        }

        /// <summary>
        /// Sets the callback invoked when the user clicks "Reconnect to TopSolid" in the tray menu.
        /// </summary>
        public void SetReconnectAction(Action onReconnect)
        {
            _onReconnectRequested = onReconnect;
        }

        /// <summary>
        /// Sets the TCP port used for the TopSolid connection. Called once at startup.
        /// </summary>
        public void SetPort(int port)
        {
            _port = port;
            UpdateStatusText();
        }

        /// <summary>
        /// Updates the status text in the tray menu. Thread-safe.
        /// </summary>
        private void UpdateStatusText()
        {
            if (_statusItem == null) return;

            string portSuffix = _port > 0 ? " (port " + _port + ")" : "";
            string text;
            if (_lastConnectedState == null)
                text = "TopSolid: waiting..." + portSuffix;
            else if (_lastConnectedState == true)
                text = "TopSolid: connected" + portSuffix;
            else
                text = "TopSolid: disconnected" + portSuffix;

            try
            {
                var context = _uiContext;
                if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
                {
                    // Menu items belong to the tray thread — never touch them from here.
                    // Post() is asynchronous, so a caller holding a lock cannot deadlock.
                    context.Post(_ => SetStatusItemText(text), null);
                    return;
                }

                SetStatusItemText(text);
            }
            catch { /* tray already disposed */ }
        }

        /// <summary>
        /// Applies the status text. Must run on the tray thread.
        /// </summary>
        private void SetStatusItemText(string text)
        {
            try
            {
                var item = _statusItem;
                if (item != null) item.Text = text;
            }
            catch { /* tray already disposed */ }
        }

        /// <summary>
        /// Starts the tray icon on a background STA thread.
        /// </summary>
        public void Start()
        {
            _thread = new Thread(RunTray);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Name = "TrayIcon";
            _thread.Start();
        }

        /// <summary>
        /// Updates the TopSolid connection status shown in the tray menu.
        /// Includes the port number for clarity.
        /// </summary>
        public void SetConnected(bool connected)
        {
            _lastConnectedState = connected;
            UpdateStatusText();
        }

        /// <summary>
        /// Body of the tray thread. Any failure here (headless host, session 0, no window
        /// station) is logged and swallowed: an unhandled exception on this thread would
        /// take the whole server down.
        /// </summary>
        private void RunTray()
        {
            try
            {
                BuildTray();

                // Run the Windows Forms message loop (blocks this thread)
                Application.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] Tray icon unavailable, continuing without it: " + ex.Message);
            }
            finally
            {
                // The loop ended (or never started): release the icon on this thread.
                DisposeNotifyIcon();
            }
        }

        private void BuildTray()
        {
            // Captured before the message loop starts: the marshaling window it creates
            // belongs to this thread, so posts are delivered to this message loop.
            _uiContext = new WindowsFormsSynchronizationContext();

            var version = GetVersion();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = $"TopSolid MCP v{version}";
            _notifyIcon.Icon = LoadIcon();
            _notifyIcon.Visible = true;

            var menu = new ContextMenuStrip();

            // Version (disabled, info only)
            var versionItem = new ToolStripMenuItem($"TopSolid MCP v{version}");
            versionItem.Enabled = false;
            versionItem.Font = new Font(versionItem.Font, FontStyle.Bold);
            menu.Items.Add(versionItem);

            // Connection status — show the port if already known
            string initStatus = _port > 0
                ? "TopSolid: waiting... (port " + _port + ")"
                : "TopSolid: waiting...";
            _statusItem = new ToolStripMenuItem(initStatus);
            _statusItem.Enabled = false;
            menu.Items.Add(_statusItem);

            // Apply any state that was set before the menu was created
            UpdateStatusText();

            // Reconnect button
            _reconnectItem = new ToolStripMenuItem("Reconnect to TopSolid");
            _reconnectItem.Click += OnReconnectClick;
            menu.Items.Add(_reconnectItem);

            menu.Items.Add(new ToolStripSeparator());

            // Update
            var updateItem = new ToolStripMenuItem("Check for updates...");
            updateItem.Click += OnUpdateClick;
            menu.Items.Add(updateItem);

            // GitHub
            var githubItem = new ToolStripMenuItem("GitHub");
            githubItem.Click += (s, e) => OpenUrl(GitHubUrl);
            menu.Items.Add(githubItem);

            // Documentation
            var docsItem = new ToolStripMenuItem("Documentation");
            docsItem.Click += (s, e) => OpenUrl(DocsUrl);
            menu.Items.Add(docsItem);

            menu.Items.Add(new ToolStripSeparator());

            // Quit
            var quitItem = new ToolStripMenuItem("Stop server");
            quitItem.Click += OnQuitClick;
            menu.Items.Add(quitItem);

            _notifyIcon.ContextMenuStrip = menu;

            // Show startup balloon
            ShowBalloon("TopSolid MCP", $"MCP server v{version} started. Listening on stdin.", ToolTipIcon.Info, 3000);
        }

        private void OnReconnectClick(object sender, EventArgs e)
        {
            if (_onReconnectRequested == null)
            {
                ShowBalloon("Reconnection", "The server is not initialized yet.", ToolTipIcon.Warning, 2000);
                return;
            }

            if (_statusItem != null) _statusItem.Text = "TopSolid: reconnecting...";
            ShowBalloon("TopSolid MCP", "Attempting to reconnect...", ToolTipIcon.Info, 2000);

            // Run reconnect on a background thread (avoid blocking the UI)
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _onReconnectRequested.Invoke();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[TrayIcon] Reconnect error: " + ex.Message);
                }
            });
        }

        private void OnUpdateClick(object sender, EventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updateScript = Path.Combine(baseDir, "update.ps1");

                if (!File.Exists(updateScript))
                {
                    ShowBalloon("Update", "update.ps1 was not found next to the executable.", ToolTipIcon.Warning, 3000);
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{updateScript}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowBalloon("Error", $"Could not start the update: {ex.Message}", ToolTipIcon.Error, 3000);
            }
        }

        private void OnQuitClick(object sender, EventArgs e)
        {
            ShowBalloon("TopSolid MCP", "Stopping the server...", ToolTipIcon.Info, 1000);

            // Give the balloon time to show, then shut down
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                timer.Dispose();
                var shutdown = _onShutdownRequested;
                if (shutdown != null) shutdown();
            };
            timer.Start();
        }

        /// <summary>
        /// Shows a balloon tip, ignoring the call when the icon is already gone.
        /// </summary>
        private void ShowBalloon(string title, string text, ToolTipIcon icon, int timeoutMs)
        {
            var notifyIcon = _notifyIcon;
            if (notifyIcon == null) return;

            try
            {
                notifyIcon.BalloonTipTitle = title;
                notifyIcon.BalloonTipText = text;
                notifyIcon.BalloonTipIcon = icon;
                notifyIcon.ShowBalloonTip(timeoutMs);
            }
            catch { /* tray already disposed */ }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { /* ignore */ }
        }

        private static Icon LoadIcon()
        {
            try
            {
                // Try embedded resource first
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = Path.Combine(baseDir, "topsolid-mcp.ico");
                if (File.Exists(icoPath))
                    return new Icon(icoPath);
            }
            catch { /* fall through */ }

            // Fallback: use default application icon
            return SystemIcons.Application;
        }

        public static string GetVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                return $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }
            catch
            {
                return "0.0.0";
            }
        }

        /// <summary>
        /// Removes the icon from the notification area. Must run on the tray thread.
        /// </summary>
        private void DisposeNotifyIcon()
        {
            try
            {
                var notifyIcon = _notifyIcon;
                if (notifyIcon != null)
                {
                    _notifyIcon = null;
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
            }
            catch { /* shutting down */ }
        }

        /// <summary>
        /// Stops the tray: the icon is removed and the message loop is ended on the STA
        /// thread that owns it. Safe to call from any thread, and more than once.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var context = _uiContext;
            var thread = _thread;

            // Never started, or called from the tray thread itself: act inline.
            if (context == null || thread == null || ReferenceEquals(Thread.CurrentThread, thread))
            {
                DisposeNotifyIcon();
                try { if (context != null) Application.ExitThread(); } catch { }
                return;
            }

            try
            {
                context.Post(_ =>
                {
                    DisposeNotifyIcon();
                    try { Application.ExitThread(); } catch { }
                }, null);

                // Give the STA thread a moment to unwind so the icon really disappears.
                thread.Join(2000);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TrayIcon] Shutdown error: " + ex.Message);
                DisposeNotifyIcon();
            }
        }
    }
}
