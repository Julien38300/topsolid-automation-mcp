using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using TopSolidMcp.UpdateCheck;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// System tray icon for the TopSolid MCP server.
    /// Runs its own Windows message loop on a dedicated STA background thread.
    /// Provides visual status (connected / disconnected / read-only), update checks,
    /// direct TopSolid instance selection (max 2 menu levels), and a dedicated
    /// MCP Server settings dialog.
    /// </summary>
    public partial class TrayIcon : IDisposable
    {
        private const string DocsUrl = "https://julien38300.github.io/topsolid-automation-mcp/";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/Julien38300/topsolid-automation-mcp/releases/latest";

        private readonly Action _onQuit;
        private Action _onReconnect;
        private Thread _thread;
        private NotifyIcon _notifyIcon;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _mcpStatusItem;
        private ToolStripMenuItem _reconnectItem;
        private ToolStripMenuItem _updateItem;
        private ToolStripMenuItem _checkUpdateItem;
        private int _port = 8090;
        private bool _readOnly;
        private bool _connected;
        private bool _disposed;
        private string _updateUrl;
        private string _latestVersion;
        private string _currentVersion;
        private int _checkInProgress;

        public TrayIcon(Action onQuit)
        {
            _onQuit = onQuit;
            _currentVersion = GetCurrentVersion();
        }

        public static string GetVersion() => GetCurrentVersion();

        private static string GetCurrentVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoAttr != null && !string.IsNullOrEmpty(infoAttr.InformationalVersion))
                {
                    string v = infoAttr.InformationalVersion;
                    int plus = v.IndexOf('+');
                    return plus > 0 ? v.Substring(0, plus) : v;
                }
                var ver = asm.GetName().Version;
                if (ver != null)
                    return $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }
            catch { }
            return "0.0.0";
        }

        /// <summary>
        /// Starts the tray icon on a dedicated STA thread.
        /// Safe to call from any thread; returns immediately.
        /// </summary>
        public void Start()
        {
            _thread = new Thread(RunMessageLoop);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Name = "TopSolidMCP-TrayIcon";
            _thread.Start();
        }

        /// <summary>
        /// Updates the displayed TopSolid connection status (tooltip, menu text, icon badge).
        /// Thread-safe.
        /// </summary>
        public void SetConnected(bool connected)
        {
            _connected = connected;
            UpdateStatusDisplay();
        }

        /// <summary>
        /// Sets the TopSolid WCF port number displayed in the tray menu and tooltip.
        /// Thread-safe.
        /// </summary>
        public void SetPort(int port)
        {
            _port = port;
            UpdateStatusDisplay();
        }

        /// <summary>
        /// Sets whether the server is running in read-only mode.
        /// Thread-safe.
        /// </summary>
        public void SetReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            UpdateStatusDisplay();
        }

        /// <summary>
        /// Sets the callback invoked when the user clicks "Se reconnecter à TopSolid".
        /// </summary>
        public void SetReconnectAction(Action onReconnect)
        {
            _onReconnect = onReconnect;
        }

        private void UpdateStatusDisplay()
        {
            try
            {
                if (_notifyIcon == null) return;

                string stateText = _connected ? "connecté" : "déconnecté";
                string modeSuffix = _readOnly ? " [lecture seule]" : "";
                int effectiveHttpPort = _httpPort > 0 ? _httpPort : 8080;

                string tip = $"TopSolid MCP v{_currentVersion} — {stateText} (port {_port}){modeSuffix}";
                if (!string.IsNullOrEmpty(_latestVersion))
                    tip += $"\nMise à jour v{_latestVersion} disponible";
                if (tip.Length > 63) tip = tip.Substring(0, 63);
                _notifyIcon.Text = tip;

                var menu = _notifyIcon.ContextMenuStrip;
                if (menu != null && menu.IsHandleCreated)
                {
                    menu.BeginInvoke((Action)(() =>
                    {
                        if (_statusItem != null)
                        {
                            string dot = _connected ? "\u25CF" : "\u25CB";
                            _statusItem.Text = $"{dot}  TopSolid : {stateText} (port {_port}){modeSuffix}";
                        }
                        if (_mcpStatusItem != null)
                        {
                            _mcpStatusItem.Text = $"\u25CF  Serveur MCP : http://localhost:{effectiveHttpPort}/mcp";
                        }
                        RefreshTrayIcon();
                    }));
                }
                else
                {
                    if (_statusItem != null)
                    {
                        string dot = _connected ? "\u25CF" : "\u25CB";
                        _statusItem.Text = $"{dot}  TopSolid : {stateText} (port {_port}){modeSuffix}";
                    }
                    if (_mcpStatusItem != null)
                    {
                        _mcpStatusItem.Text = $"\u25CF  Serveur MCP : http://localhost:{effectiveHttpPort}/mcp";
                    }
                    RefreshTrayIcon();
                }
            }
            catch { }
        }

        private void RefreshTrayIcon()
        {
            try
            {
                if (_notifyIcon == null) return;
                var oldIcon = _notifyIcon.Icon;
                _notifyIcon.Icon = CreateBadgedIcon(_connected, _readOnly);
                if (oldIcon != null)
                {
                    try { oldIcon.Dispose(); } catch { }
                }
            }
            catch { }
        }

        private void RunMessageLoop()
        {
            try
            {
                Application.EnableVisualStyles();
                InitV18State();

                var menuFont = new Font("Segoe UI", 9.25f, FontStyle.Regular);
                var headerFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

                var menu = new ContextMenuStrip
                {
                    Font = menuFont,
                    Renderer = new ModernTrayMenuRenderer(),
                    ShowImageMargin = false,
                    BackColor = Color.White,
                    Padding = new Padding(4, 6, 4, 6)
                };

                // ── Section 1 : En-tête & état en direct ──
                var headerItem = new ToolStripMenuItem($"TopSolid MCP v{_currentVersion}")
                {
                    Enabled = false,
                    Font = headerFont
                };
                StyleMenuItem(headerItem);
                menu.Items.Add(headerItem);

                string initialDot = _connected ? "\u25CF" : "\u25CB";
                string initialState = _connected ? "connecté" : "déconnecté";
                string initialMode = _readOnly ? " [lecture seule]" : "";
                _statusItem = new ToolStripMenuItem($"{initialDot}  TopSolid : {initialState} (port {_port}){initialMode}")
                {
                    Enabled = false
                };
                StyleMenuItem(_statusItem);
                menu.Items.Add(_statusItem);

                int initialHttpPort = _httpPort > 0 ? _httpPort : 8080;
                _mcpStatusItem = new ToolStripMenuItem($"\u25CF  Serveur MCP : http://localhost:{initialHttpPort}/mcp")
                {
                    Enabled = false
                };
                StyleMenuItem(_mcpStatusItem);
                menu.Items.Add(_mcpStatusItem);

                menu.Items.Add(new ToolStripSeparator());

                // ── Section 2 : Instance CAO TopSolid (Accès direct niveau 1 -> sous-menu niveau 2 max) ──
                BuildInstancePickerMenu(menu);

                _reconnectItem = new ToolStripMenuItem("Se reconnecter à TopSolid");
                StyleMenuItem(_reconnectItem);
                _reconnectItem.Click += (s, e) =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { _onReconnect?.Invoke(); } catch { }
                    });
                };
                menu.Items.Add(_reconnectItem);

                menu.Items.Add(new ToolStripSeparator());

                // ── Section 3 : Serveur MCP (Fenêtre dédiée unifiée + raccourci clé API) ──
                BuildMcpServerMenuItems(menu);

                menu.Items.Add(new ToolStripSeparator());

                // ── Section 4 : Aide, Maintenance & Mises à jour ──
                var helpMenu = new ToolStripMenuItem("Aide && Maintenance");
                StyleMenuItem(helpMenu);
                helpMenu.DropDown.Font = menuFont;
                helpMenu.DropDown.BackColor = Color.White;
                helpMenu.DropDown.Padding = new Padding(4, 4, 4, 4);
                if (helpMenu.DropDown is ToolStripDropDownMenu helpDropDown)
                    helpDropDown.ShowImageMargin = false;

                var docsItem = new ToolStripMenuItem("Documentation en ligne", null, (s, e) => OpenUrl(DocsUrl));
                StyleMenuItem(docsItem);
                helpMenu.DropDownItems.Add(docsItem);

                BuildHelpSubmenuItems(helpMenu);
                menu.Items.Add(helpMenu);

                _checkUpdateItem = new ToolStripMenuItem("Vérifier les mises à jour…");
                StyleMenuItem(_checkUpdateItem);
                _checkUpdateItem.Click += (s, e) => CheckForUpdatesAsync(manual: true);
                menu.Items.Add(_checkUpdateItem);

                _updateItem = new ToolStripMenuItem("Télécharger la mise à jour…")
                {
                    Visible = false,
                    Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(37, 99, 235)
                };
                StyleMenuItem(_updateItem);
                _updateItem.Click += (s, e) =>
                {
                    _updateItem.Visible = false;
                    LaunchUpdater();
                };
                menu.Items.Add(_updateItem);

                menu.Items.Add(new ToolStripSeparator());

                // ── Section 5 : Quitter ──
                var quitItem = new ToolStripMenuItem("Arrêter le serveur");
                StyleMenuItem(quitItem);
                quitItem.Click += (s, e) =>
                {
                    try { _notifyIcon.Visible = false; } catch { }
                    Application.ExitThread();
                    _onQuit?.Invoke();
                };
                menu.Items.Add(quitItem);

                string stateInit = _connected ? "connecté" : "déconnecté";
                string modeInit = _readOnly ? " [lecture seule]" : "";
                _notifyIcon = new NotifyIcon
                {
                    Icon = CreateBadgedIcon(_connected, _readOnly),
                    Text = $"TopSolid MCP v{_currentVersion} — {stateInit} (port {_port}){modeInit}",
                    ContextMenuStrip = menu,
                    Visible = true
                };

                // Double-clicking the tray icon opens the unified MCP Server Settings window.
                _notifyIcon.DoubleClick += (s, e) => ShowMcpServerSettingsDialog();

                _notifyIcon.BalloonTipClicked += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(_latestVersion))
                        LaunchUpdater();
                };

                CheckForUpdatesAsync(manual: false);
                PromptInitialApiKeySetupIfNeeded();

                Application.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-WARN] Tray icon failed: {ex.Message}");
            }
        }

        internal static void StyleMenuItem(ToolStripMenuItem item)
        {
            if (item == null) return;
            item.Padding = new Padding(6, 4, 6, 4);
            item.ForeColor = Color.FromArgb(15, 23, 42);
        }

        /// <summary>
        /// Modern flat Windows 11 style renderer for the system tray ContextMenuStrip.
        /// Eliminates legacy WinForms gradients and 3D bevels.
        /// </summary>
        private sealed class ModernTrayMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernTrayMenuRenderer() : base(new ModernColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (!e.Item.Enabled)
                {
                    bool isHeader = e.Item.Font != null && e.Item.Font.Bold;
                    e.TextColor = isHeader
                        ? Color.FromArgb(15, 23, 42)
                        : Color.FromArgb(71, 85, 105);
                }
                else if (e.Item.Selected)
                {
                    e.TextColor = Color.FromArgb(29, 78, 216);
                }
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2);
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
                }
            }

            private sealed class ModernColorTable : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => Color.White;
                public override Color MenuBorder => Color.FromArgb(203, 213, 225);
                public override Color MenuItemSelected => Color.FromArgb(239, 246, 255);
                public override Color MenuItemBorder => Color.FromArgb(191, 219, 254);
                public override Color MenuItemSelectedGradientBegin => Color.FromArgb(239, 246, 255);
                public override Color MenuItemSelectedGradientEnd => Color.FromArgb(239, 246, 255);
                public override Color MenuItemPressedGradientBegin => Color.FromArgb(219, 234, 254);
                public override Color MenuItemPressedGradientEnd => Color.FromArgb(219, 234, 254);
                public override Color ImageMarginGradientBegin => Color.White;
                public override Color ImageMarginGradientMiddle => Color.White;
                public override Color ImageMarginGradientEnd => Color.White;
                public override Color SeparatorDark => Color.FromArgb(226, 232, 240);
                public override Color SeparatorLight => Color.Transparent;
            }
        }

        /// <summary>
        /// Queries GitHub Releases API (releases/latest) in a background thread.
        /// Never blocks or throws.
        /// </summary>
        private void CheckForUpdatesAsync(bool manual)
        {
            if (Interlocked.CompareExchange(ref _checkInProgress, 1, 0) != 0)
            {
                if (manual)
                {
                    ShowBalloon(
                        "Vérification en cours",
                        "Une vérification des mises à jour est déjà en cours…",
                        ToolTipIcon.Info);
                }
                return;
            }

            SetCheckMenuText("Recherche de mise à jour…", enabled: false);
            if (manual)
            {
                ShowBalloon("Recherche de mise à jour…", "Interrogation de GitHub…", ToolTipIcon.Info);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string remoteVersion;
                    using (var client = new WebClient())
                    {
                        client.Headers["User-Agent"] = "TopSolidMcpServer-Updater";
                        client.Headers["Accept"] = "application/vnd.github+json";
                        client.Proxy = null;
                        client.Encoding = System.Text.Encoding.UTF8;
                        string json = DownloadStringWithTimeout(client, LatestReleaseApiUrl, TimeSpan.FromSeconds(10));
                        var release = JObject.Parse(json);
                        remoteVersion = release.Value<string>("tag_name")?.Trim();
                    }

                    if (string.IsNullOrEmpty(remoteVersion))
                    {
                        if (manual)
                        {
                            ShowModalBox(
                                "Mise à jour",
                                "Impossible de lire les informations de version distante sur GitHub.",
                                MessageBoxIcon.Warning);
                        }
                        return;
                    }

                    if (CompareVersions(_currentVersion, remoteVersion) < 0)
                    {
                        string cleanVer = remoteVersion.TrimStart('v', 'V');
                        _latestVersion = cleanVer;
                        _updateUrl = GitHubFeedbackLogic.BuildReleaseUrl(cleanVer);
                        ShowUpdateAvailable(cleanVer, manual);
                    }
                    else if (manual)
                    {
                        ShowModalBox(
                            "Mise à jour",
                            $"Vous êtes déjà à jour (v{_currentVersion}).",
                            MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[TrayIcon] Update check failed: {ex.Message}");
                    if (manual)
                    {
                        ShowModalBox(
                            "Mise à jour",
                            "Impossible de contacter GitHub (connexion ?).\r\n\r\nDétail : " + ex.Message,
                            MessageBoxIcon.Warning);
                    }
                }
                finally
                {
                    SetCheckMenuText("Vérifier les mises à jour…", enabled: true);
                    Interlocked.Exchange(ref _checkInProgress, 0);
                }
            });
        }

        private void ShowModalBox(string title, string text, MessageBoxIcon icon)
        {
            try
            {
                var menu = _notifyIcon?.ContextMenuStrip;
                if (menu != null && menu.IsHandleCreated)
                    menu.BeginInvoke((Action)(() => MessageBox.Show(text, title, MessageBoxButtons.OK, icon)));
                else
                    MessageBox.Show(text, title, MessageBoxButtons.OK, icon);
            }
            catch { }
        }

        /// <summary>Launches update.ps1 next to the executable in a visible console.</summary>
        private void LaunchUpdater()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updateScript = Path.Combine(baseDir, "update.ps1");

                if (!File.Exists(updateScript))
                {
                    if (!string.IsNullOrEmpty(_updateUrl))
                    {
                        OpenUrl(_updateUrl);
                        return;
                    }
                    ShowBalloon("Mise à jour", "update.ps1 est introuvable à côté de l'exécutable.", ToolTipIcon.Warning);
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{updateScript}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);

                try { OpenUrl(GitHubFeedbackLogic.BuildReleaseUrl(null)); } catch { }
            }
            catch (Exception ex)
            {
                ShowBalloon("Erreur", $"Impossible de lancer la mise à jour : {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void SetCheckMenuText(string text, bool enabled)
        {
            try
            {
                var menu = _notifyIcon?.ContextMenuStrip;
                if (menu != null && menu.IsHandleCreated)
                {
                    menu.BeginInvoke((Action)(() =>
                    {
                        if (_checkUpdateItem != null)
                        {
                            _checkUpdateItem.Text = text;
                            _checkUpdateItem.Enabled = enabled;
                        }
                    }));
                }
            }
            catch { }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (_notifyIcon == null) return;
                var menu = _notifyIcon.ContextMenuStrip;
                Action show = () =>
                {
                    try
                    {
                        _notifyIcon.BalloonTipTitle = title;
                        _notifyIcon.BalloonTipText = text;
                        _notifyIcon.BalloonTipIcon = icon;
                        _notifyIcon.ShowBalloonTip(5000);
                    }
                    catch { }
                };

                if (menu != null && menu.IsHandleCreated)
                    menu.BeginInvoke(show);
                else
                    show();
            }
            catch { }
        }

        private void ShowUpdateAvailable(string remoteVersion, bool manual)
        {
            try
            {
                if (_notifyIcon == null) return;

                UpdateStatusDisplay();

                var menu = _notifyIcon.ContextMenuStrip;
                Action apply = () =>
                {
                    if (_updateItem != null)
                    {
                        _updateItem.Text = $"Télécharger la mise à jour v{remoteVersion}…";
                        _updateItem.Visible = true;
                    }
                    try
                    {
                        _notifyIcon.BalloonTipTitle = $"TopSolid MCP v{remoteVersion} disponible";
                        _notifyIcon.BalloonTipText = $"Mise à jour v{remoteVersion} disponible ! Clic droit sur l'icône pour l'installer.";
                        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                        _notifyIcon.ShowBalloonTip(5000);
                    }
                    catch { }
                    if (manual)
                    {
                        MessageBox.Show(
                            $"Mise à jour v{remoteVersion} disponible ! Un bouton « Télécharger la mise à jour » a été ajouté au menu.",
                            "TopSolid MCP",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                };

                if (menu != null && menu.IsHandleCreated)
                    menu.BeginInvoke(apply);
                else
                    apply();
            }
            catch { }
        }

        internal static int CompareVersions(string current, string remote)
        {
            return UpdateCheckLogic.CompareVersions(current, remote);
        }

        internal static string DownloadStringWithTimeout(WebClient client, string url, TimeSpan timeout)
        {
            return UpdateCheckLogic.DownloadStringWithTimeout(client, url, timeout);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-WARN] Could not open URL {url}: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon CreateBadgedIcon(bool connected, bool readOnly)
        {
            const int size = 32;
            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                bool drewBase = false;
                try
                {
                    string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "topsolid-mcp.ico");
                    if (File.Exists(icoPath))
                    {
                        using (var baseIcon = new Icon(icoPath, size, size))
                        {
                            g.DrawIcon(baseIcon, new Rectangle(0, 0, size, size));
                            drewBase = true;
                        }
                    }
                    else
                    {
                        string exePath = Assembly.GetExecutingAssembly().Location;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            using (var exeIcon = Icon.ExtractAssociatedIcon(exePath))
                            {
                                if (exeIcon != null)
                                {
                                    g.DrawIcon(exeIcon, new Rectangle(0, 0, size, size));
                                    drewBase = true;
                                }
                            }
                        }
                    }
                }
                catch { }

                if (!drewBase)
                {
                    using (var bgBrush = new SolidBrush(Color.FromArgb(200, 16, 46)))
                        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
                    using (var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var fgBrush = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString("TS", font, fgBrush, new RectangleF(0, 0, size, size), sf);
                    }
                }

                Color badgeColor = !connected
                    ? Color.FromArgb(220, 38, 38)
                    : (readOnly ? Color.FromArgb(245, 158, 11) : Color.FromArgb(34, 197, 94));

                const int badgeSize = 11;
                int bx = size - badgeSize - 1;
                int by = size - badgeSize - 1;

                using (var ringBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                    g.FillEllipse(ringBrush, bx - 2, by - 2, badgeSize + 4, badgeSize + 4);
                using (var dotBrush = new SolidBrush(badgeColor))
                    g.FillEllipse(dotBrush, bx, by, badgeSize, badgeSize);

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (var temp = Icon.FromHandle(hIcon))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopAutoReconnect();

            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
            catch { }
        }
    }
}
