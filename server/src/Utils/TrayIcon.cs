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
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private const string GitHubUrl = "https://github.com/Julien38300/noemid-topsolid-automation";
        private const string DocsUrl = "https://julien38300.github.io/noemid-topsolid-automation/";

        private NotifyIcon _notifyIcon;
        private Thread _thread;
        private readonly Action _onShutdownRequested;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _reconnectItem;
        private Action _onReconnectRequested;

        public TrayIcon(Action onShutdownRequested)
        {
            _onShutdownRequested = onShutdownRequested;
        }

        /// <summary>
        /// Sets the callback invoked when user clicks "Reconnecter" in the tray menu.
        /// </summary>
        public void SetReconnectAction(Action onReconnect)
        {
            _onReconnectRequested = onReconnect;
        }

        /// <summary>
        /// Updates the connection info shown in the tray (port, status).
        /// </summary>
        public void SetConnectionInfo(int port)
        {
            if (_statusItem == null) return;
            try
            {
                var parent = _statusItem.GetCurrentParent();
                if (parent != null && parent.InvokeRequired)
                    parent.BeginInvoke(new Action(() => _statusItem.Text = "TopSolid : deconnecte (port " + port + ")"));
                else
                    _statusItem.Text = "TopSolid : deconnecte (port " + port + ")";
            }
            catch { }
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
        /// </summary>
        public void SetConnected(bool connected)
        {
            if (_statusItem == null) return;
            try
            {
                if (_statusItem.GetCurrentParent() != null && _statusItem.GetCurrentParent().InvokeRequired)
                {
                    _statusItem.GetCurrentParent().BeginInvoke(new Action(() =>
                    {
                        _statusItem.Text = connected ? "TopSolid : connecte" : "TopSolid : deconnecte";
                    }));
                }
                else
                {
                    _statusItem.Text = connected ? "TopSolid : connecte" : "TopSolid : deconnecte";
                }
            }
            catch { /* tray already disposed */ }
        }

        private void RunTray()
        {
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

            // Connection status
            _statusItem = new ToolStripMenuItem("TopSolid : en attente...");
            _statusItem.Enabled = false;
            menu.Items.Add(_statusItem);

            // Reconnect button
            _reconnectItem = new ToolStripMenuItem("Reconnecter a TopSolid");
            _reconnectItem.Click += OnReconnectClick;
            menu.Items.Add(_reconnectItem);

            menu.Items.Add(new ToolStripSeparator());

            // Update
            var updateItem = new ToolStripMenuItem("Mettre a jour...");
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
            var quitItem = new ToolStripMenuItem("Arreter le serveur");
            quitItem.Click += OnQuitClick;
            menu.Items.Add(quitItem);

            _notifyIcon.ContextMenuStrip = menu;

            // Show startup balloon
            _notifyIcon.BalloonTipTitle = "TopSolid MCP";
            _notifyIcon.BalloonTipText = $"Serveur MCP v{version} demarre. En ecoute sur stdin.";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(3000);

            // Run the Windows Forms message loop (blocks this thread)
            Application.Run();
        }

        private void OnReconnectClick(object sender, EventArgs e)
        {
            if (_onReconnectRequested == null)
            {
                _notifyIcon.BalloonTipTitle = "Reconnexion";
                _notifyIcon.BalloonTipText = "Le serveur n'est pas encore initialise.";
                _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                _notifyIcon.ShowBalloonTip(2000);
                return;
            }

            _statusItem.Text = "TopSolid : reconnexion...";
            _notifyIcon.BalloonTipTitle = "TopSolid MCP";
            _notifyIcon.BalloonTipText = "Tentative de reconnexion...";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(2000);

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
                    _notifyIcon.BalloonTipTitle = "Mise a jour";
                    _notifyIcon.BalloonTipText = "Script update.ps1 introuvable a cote de l'exe.";
                    _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                    _notifyIcon.ShowBalloonTip(3000);
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
                _notifyIcon.BalloonTipTitle = "Erreur";
                _notifyIcon.BalloonTipText = $"Impossible de lancer la mise a jour : {ex.Message}";
                _notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                _notifyIcon.ShowBalloonTip(3000);
            }
        }

        private void OnQuitClick(object sender, EventArgs e)
        {
            _notifyIcon.BalloonTipTitle = "TopSolid MCP";
            _notifyIcon.BalloonTipText = "Arret du serveur...";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(1000);

            // Give the balloon time to show, then shutdown
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                _onShutdownRequested?.Invoke();
            };
            timer.Start();
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

        public void Dispose()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
                Application.ExitThread();
            }
            catch { /* shutting down */ }
        }
    }
}
