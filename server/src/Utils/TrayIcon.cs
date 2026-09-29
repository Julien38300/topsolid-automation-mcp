using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
    /// <para>
    /// The icon carries a TeamViewer-style status badge: green when TopSolid is
    /// connected, orange while connecting/reconnecting, red when disconnected. Menu
    /// labels follow the OS UI language (French when the OS runs in French, English
    /// otherwise), and a Settings submenu exposes the connection state and the
    /// installation folder.
    /// </para>
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private const string GitHubUrl = "https://github.com/Julien38300/topsolid-automation-mcp";
        private const string DocsUrl = "https://julien38300.github.io/topsolid-automation-mcp/";

        private enum TrayState { Connecting, Connected, Disconnected }

        private NotifyIcon _notifyIcon;
        private Thread _thread;
        private readonly Action _onShutdownRequested;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _readOnlyItem;
        private ToolStripMenuItem _reconnectItem;
        private ToolStripMenuItem _updateAvailableItem;   // "Télécharger la mise à jour…" when an update exists
        private Action _onReconnectRequested;
        private int _port;
        private bool _readOnly;

        // Current state: written with Interlocked from any thread, read on the tray thread.
        private int _state = (int)TrayState.Connecting;

        // Base icon loaded once; the badge is composited over it for each state change.
        private Icon _baseIcon;
        private IntPtr _iconHandle = IntPtr.Zero;

        /// <summary>
        /// Synchronization context owned by the tray STA thread. Created inside
        /// <see cref="RunTray"/> so that <see cref="Dispose"/> can post the shutdown
        /// onto the thread that actually runs the message loop.
        /// </summary>
        private volatile SynchronizationContext _uiContext;

        private volatile bool _disposed;

        private static string L(string fr, string en)
        {
            try
            {
                return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? fr : en;
            }
            catch
            {
                return en;
            }
        }

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
            PostState((TrayState)Volatile.Read(ref _state));
        }

        /// <summary>
        /// Reflects the read-only startup mode in the Settings submenu. Called once at startup.
        /// </summary>
        public void SetReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            string text = readOnly
                ? L("Mode lecture seule : oui", "Read-only mode: yes")
                : L("Mode lecture seule : non", "Read-only mode: no");
            var context = _uiContext;
            var item = _readOnlyItem;
            if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
            {
                context.Post(_ => { if (item != null) item.Text = text; }, null);
                return;
            }
            if (item != null) item.Text = text;
        }

        /// <summary>
        /// Updates the TopSolid connection status shown in the tray menu and the badge
        /// color of the icon. Thread-safe.
        /// </summary>
        public void SetConnected(bool connected)
        {
            PostState(connected ? TrayState.Connected : TrayState.Disconnected);
        }

        /// <summary>
        /// Switches the icon to the orange "connecting" state (startup or manual reconnect).
        /// </summary>
        public void SetConnecting()
        {
            PostState(TrayState.Connecting);
        }

        private void PostState(TrayState state)
        {
            Interlocked.Exchange(ref _state, (int)state);

            var context = _uiContext;
            if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
            {
                // Menu items and the icon belong to the tray thread — never touch them
                // from here. Post() is asynchronous, so a caller holding a lock cannot
                // deadlock.
                context.Post(_ => ApplyState((TrayState)Volatile.Read(ref _state)), null);
                return;
            }

            ApplyState(state);
        }

        /// <summary>
        /// Applies the current state: status text, badge color and tooltip.
        /// Must run on the tray thread.
        /// </summary>
        private void ApplyState(TrayState state)
        {
            try
            {
                string text = BuildStatusText(state);
                if (_statusItem != null) _statusItem.Text = text;

                if (_notifyIcon != null)
                {
                    _notifyIcon.Icon = ComposeIcon(state);
                    string tooltip = string.Format(
                        L("TopSolid MCP v{0} — TopSolid : {1}", "TopSolid MCP v{0} — TopSolid: {1}"),
                        GetVersion(),
                        state == TrayState.Connected
                            ? L("connecté", "connected")
                            : state == TrayState.Disconnected
                                ? L("déconnecté", "disconnected")
                                : L("connexion…", "connecting…"));
                    if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);
                    _notifyIcon.Text = tooltip;
                }
            }
            catch { /* tray already disposed */ }
        }

        private string BuildStatusText(TrayState state)
        {
            string portSuffix = _port > 0 ? " (port " + _port + ")" : "";
            string label;
            switch (state)
            {
                case TrayState.Connected: label = L("connecté", "connected"); break;
                case TrayState.Disconnected: label = L("déconnecté", "disconnected"); break;
                default: label = L("connexion en cours…", "connecting…"); break;
            }
            return "● TopSolid : " + label + portSuffix;
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

            _baseIcon = LoadIcon();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = $"TopSolid MCP v{version}";
            _notifyIcon.Icon = ComposeIcon((TrayState)Volatile.Read(ref _state));
            _notifyIcon.Visible = true;

            var menu = new ContextMenuStrip();

            // Version (disabled, info only)
            var versionItem = new ToolStripMenuItem($"TopSolid MCP v{version}");
            versionItem.Enabled = false;
            versionItem.Font = new Font(versionItem.Font, FontStyle.Bold);
            menu.Items.Add(versionItem);

            // Connection status — show the port if already known
            var state = (TrayState)Volatile.Read(ref _state);
            _statusItem = new ToolStripMenuItem(BuildStatusText(state));
            _statusItem.Enabled = false;
            menu.Items.Add(_statusItem);

            // Reconnect button
            _reconnectItem = new ToolStripMenuItem(L("Se reconnecter à TopSolid", "Reconnect to TopSolid"));
            _reconnectItem.Click += OnReconnectClick;
            menu.Items.Add(_reconnectItem);

            menu.Items.Add(new ToolStripSeparator());

            // ── Settings zone ──
            var settingsMenu = new ToolStripMenuItem(L("Paramètres", "Settings"));

            // Port: click to change. Persisted in settings.json, applied on next start.
            var portItem = new ToolStripMenuItem(_port > 0
                ? string.Format(L("Port TopSolid : {0} (cliquer pour changer)", "TopSolid port: {0} (click to change)"), _port)
                : L("Port TopSolid : par défaut (cliquer pour changer)", "TopSolid port: default (click to change)"));
            portItem.Click += (s, e) =>
            {
                int currentPort = _port > 0 ? _port : 8090;
                string input = PromptInput(
                    L("Port TopSolid", "TopSolid port"),
                    L("Port TCP de TopSolid (8090 par défaut) :\nAppliqué au prochain démarrage du serveur.",
                      "TopSolid TCP port (8090 default):\nApplied at next server start."),
                    currentPort.ToString(CultureInfo.InvariantCulture));
                if (string.IsNullOrWhiteSpace(input)) return;
                int newPort;
                if (!int.TryParse(input.Trim(), out newPort) || newPort < 1 || newPort > 65535)
                {
                    ShowBalloon(L("Paramètres", "Settings"),
                        L("Port invalide (1-65535 attendu), valeur conservée.", "Invalid port (1-65535 expected), value kept."),
                        ToolTipIcon.Warning, 3000);
                    return;
                }
                int stored = newPort == 8090 ? 0 : newPort;   // 8090 = default, do not store
                TraySettings.Save(stored, _readOnly);
                _port = newPort;
                portItem.Text = string.Format(
                    L("Port TopSolid : {0} (cliquer pour changer)", "TopSolid port: {0} (click to change)"), newPort);
                ShowBalloon(L("Paramètres", "Settings"),
                    string.Format(L("Port {0} enregistré. Il sera appliqué au prochain démarrage du serveur.", "Port {0} saved. It will be applied next time the server starts."), newPort),
                    ToolTipIcon.Info, 3000);
            };
            settingsMenu.DropDownItems.Add(portItem);

            // Read-only: click to toggle. Persisted, applied on next start.
            _readOnlyItem = new ToolStripMenuItem(_readOnly
                ? L("Mode lecture seule : oui (cliquer pour désactiver)", "Read-only mode: yes (click to turn off)")
                : L("Mode lecture seule : non (cliquer pour activer)", "Read-only mode: no (click to turn on)"));
            _readOnlyItem.Click += (s, ev) =>
            {
                _readOnly = !_readOnly;
                TraySettings.Save(_port > 0 ? _port : 0, _readOnly);
                _readOnlyItem.Text = _readOnly
                    ? L("Mode lecture seule : oui (cliquer pour désactiver)", "Read-only mode: yes (click to turn off)")
                    : L("Mode lecture seule : non (cliquer pour activer)", "Read-only mode: no (click to turn on)");
                ShowBalloon(L("Paramètres", "Settings"),
                    _readOnly
                        ? L("Lecture seule activée. Appliqué au prochain démarrage du serveur.", "Read-only enabled. Applied next time the server starts.")
                        : L("Lecture seule désactivée. Appliqué au prochain démarrage du serveur.", "Read-only disabled. Applied next time the server starts."),
                    ToolTipIcon.Info, 3000);
            };
            settingsMenu.DropDownItems.Add(_readOnlyItem);

            settingsMenu.DropDownItems.Add(new ToolStripSeparator());

            var folderItem = new ToolStripMenuItem(L("Ouvrir le dossier d'installation", "Open installation folder"));
            folderItem.Click += (s, e) => OpenUrl(AppDomain.CurrentDomain.BaseDirectory);
            settingsMenu.DropDownItems.Add(folderItem);

            menu.Items.Add(settingsMenu);

            menu.Items.Add(new ToolStripSeparator());

            // Update
            var updateItem = new ToolStripMenuItem(L("Vérifier les mises à jour…", "Check for updates…"));
            updateItem.Click += OnUpdateClick;
            menu.Items.Add(updateItem);

            // GitHub
            var githubItem = new ToolStripMenuItem("GitHub");
            githubItem.Click += (s, e) => OpenUrl(GitHubUrl);
            menu.Items.Add(githubItem);

            // Documentation
            var docsItem = new ToolStripMenuItem(L("Documentation", "Documentation"));
            docsItem.Click += (s, e) => OpenUrl(DocsUrl);
            menu.Items.Add(docsItem);

            menu.Items.Add(new ToolStripSeparator());

            // Quit
            var quitItem = new ToolStripMenuItem(L("Arrêter le serveur", "Stop server"));
            quitItem.Click += OnQuitClick;
            menu.Items.Add(quitItem);

            _notifyIcon.ContextMenuStrip = menu;

            // Show startup balloon
            ShowBalloon(
                "TopSolid MCP",
                L($"Serveur MCP v{version} démarré. Connexion à TopSolid…", $"MCP server v{version} started. Connecting to TopSolid…"),
                ToolTipIcon.Info, 3000);

            // Automatic update check at startup: GitHub API in background; when a newer
            // release exists, a balloon notifies the user and a "Download update…"
            // item appears in the menu (visible until installed).
            ThreadPool.QueueUserWorkItem(_ => CheckForUpdate(false));
        }

        private void OnReconnectClick(object sender, EventArgs e)
        {
            if (_onReconnectRequested == null)
            {
                ShowBalloon(L("Reconnexion", "Reconnection"),
                    L("Le serveur n'est pas encore initialisé.", "The server is not initialized yet."),
                    ToolTipIcon.Warning, 2000);
                return;
            }

            // Orange state + balloon; the connector fires ConnectionChanged when done,
            // which flips the badge to green or red.
            PostState(TrayState.Connecting);
            ShowBalloon("TopSolid MCP",
                L("Tentative de reconnexion…", "Attempting to reconnect…"),
                ToolTipIcon.Info, 2000);

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
            // Feedback directly in the menu: balloons can be disabled per-app in Windows
            // notification settings, which made the button look dead ("ne fait rien").
            var item = sender as ToolStripMenuItem;
            string originalText = item != null ? item.Text : null;
            if (item != null)
            {
                item.Enabled = false;
                item.Text = L("Recherche de mise à jour…", "Checking for updates…");
            }
            ShowBalloon(L("Recherche de mise à jour…", "Checking for updates…"),
                L("Interrogation de GitHub…", "Querying GitHub…"),
                ToolTipIcon.Info, 2000);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { CheckForUpdate(true); }
                finally
                {
                    var context = _uiContext;
                    if (item != null && context != null)
                        context.Post(s =>
                        {
                            try
                            {
                                var it = (ToolStripMenuItem)s;
                                it.Enabled = true;
                                if (originalText != null) it.Text = originalText;
                            }
                            catch { /* menu gone */ }
                        }, item);
                }
            });
        }

        /// <summary>
        /// Queries the GitHub API for the latest release and compares it to the running
        /// version. Manual check (userClicked): balloon reports both outcomes. Startup
        /// check: balloon + menu item only when a newer version exists — silence means
        /// up to date. Runs on a thread-pool thread; UI mutations are marshalled.
        /// </summary>
        private void CheckForUpdate(bool userClicked)
        {
            string latest;
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    client.Headers["User-Agent"] = "TopSolidMcpServer-Updater";
                    client.Headers["Accept"] = "application/vnd.github+json";
                    // 10s hard timeout: WebClient's default (100s) made a stalled DNS or
                    // network lookup freeze the check silently for minutes.
                    client.Proxy = null; // bypass IE proxy auto-detection, another silent stall
                    var json = DownloadStringWithTimeout(client,
                        "https://api.github.com/repos/Julien38300/topsolid-automation-mcp/releases/latest",
                        TimeSpan.FromSeconds(10));
                    var release = Newtonsoft.Json.Linq.JObject.Parse(json);
                    latest = release.Value<string>("tag_name");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TrayIcon] Update check failed: " + ex.Message);
                if (userClicked)
                {
                    // MessageBox, not a balloon: balloons are suppressed when the user
                    // has disabled notifications for this app, which made the result
                    // invisible ("the button does nothing"). A modal box always shows.
                    ShowResultBox(L("Mise à jour", "Update"),
                        L("Impossible de contacter GitHub (connexion ?).", "Could not reach GitHub (network?).") +
                        Environment.NewLine + Environment.NewLine +
                        string.Format(L("Détail : {0}", "Detail: {0}"), ex.Message),
                        MessageBoxIcon.Warning);
                }
                return;
            }

            string current = GetVersion();
            if (CompareVersions(latest, current) <= 0)
            {
                if (userClicked)
                    ShowResultBox(L("Mise à jour", "Update"),
                        string.Format(L("Vous êtes déjà à jour (v{0}).", "You are already up to date (v{0})."), current),
                        MessageBoxIcon.Information);
                return;
            }

            // Newer version available: offer to download it.
            ShowBalloonSafe("TopSolid MCP",
                string.Format(L("Mise à jour v{0} disponible ! Clic droit sur l'icône pour l'installer.", "Update v{0} available! Right-click the icon to install it."), latest),
                ToolTipIcon.Info, 5000);
            if (userClicked)
                ShowResultBox("TopSolid MCP",
                    string.Format(L("Mise à jour v{0} disponible ! Un bouton « Télécharger la mise à jour » a été ajouté au menu.", "Update v{0} available! A 'Download update' button was added to the menu."), latest),
                    MessageBoxIcon.Information);

            var context = _uiContext;
            if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
                context.Post(_ => ShowUpdateAvailableItem(latest), null);
            else
                ShowUpdateAvailableItem(latest);
        }

        /// <summary>Thread-safe balloon (posts to the tray thread when needed).</summary>
        private void ShowBalloonSafe(string title, string text, ToolTipIcon icon, int timeoutMs)
        {
            var context = _uiContext;
            if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
                context.Post(_ => ShowBalloon(title, text, icon, timeoutMs), null);
            else
                ShowBalloon(title, text, icon, timeoutMs);
        }

        /// <summary>
        /// Thread-safe modal result box. Always visible — unlike balloons, a MessageBox
        /// cannot be suppressed by Windows notification settings, so a manual update
        /// check can never look like the button did nothing.
        /// </summary>
        private void ShowResultBox(string title, string text, MessageBoxIcon icon)
        {
            var context = _uiContext;
            if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
                context.Post(_ => MessageBox.Show(text, title, MessageBoxButtons.OK, icon), null);
            else
                MessageBox.Show(text, title, MessageBoxButtons.OK, icon);
        }

        /// <summary>Shows (or refreshes) the "Download update…" menu item. Tray thread only.</summary>
        private void ShowUpdateAvailableItem(string latest)
        {
            try
            {
                string text = string.Format(L("Télécharger la mise à jour v{0}…", "Download update v{0}…"), latest);
                if (_updateAvailableItem == null)
                {
                    _updateAvailableItem = new ToolStripMenuItem(text);
                    // One click = remove the item + launch update.ps1 directly.
                    _updateAvailableItem.Click += (s, ev) =>
                    {
                        var item = (ToolStripMenuItem)s;
                        var parent = item.GetCurrentParent();
                        if (parent != null) parent.Items.Remove(item);
                        _updateAvailableItem = null;
                        LaunchUpdater();
                    };
                    // Insert right after the "Check for updates…" item's separator zone:
                    // find the menu and insert before "GitHub" so the update block stays together.
                    var notifyIcon = _notifyIcon;
                    var menu = notifyIcon != null ? notifyIcon.ContextMenuStrip : null;
                    if (menu != null)
                    {
                        int idx = 0;
                        foreach (ToolStripItem it in menu.Items)
                        {
                            var tsmi = it as ToolStripMenuItem;
                            if (tsmi != null && tsmi.Text == "GitHub") break;
                            idx++;
                        }
                        menu.Items.Insert(idx, _updateAvailableItem);
                    }
                    else
                    {
                        _updateAvailableItem = null;
                    }
                }
                else
                {
                    _updateAvailableItem.Text = text;
                }
            }
            catch { /* menu gone */ }
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
                    ShowBalloon(L("Mise à jour", "Update"),
                        L("update.ps1 est introuvable à côté de l'exécutable.", "update.ps1 was not found next to the executable."),
                        ToolTipIcon.Warning, 3000);
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
                ShowBalloon(L("Erreur", "Error"),
                    string.Format(L("Impossible de lancer la mise à jour : {0}", "Could not start the update: {0}"), ex.Message),
                    ToolTipIcon.Error, 3000);
            }
        }

        /// <summary>
        /// WebClient has no usable per-call timeout (the 100s default made a stalled
        /// DNS lookup freeze the check silently). Run the download on the thread pool
        /// and abort after the given timeout.
        /// </summary>
        private static string DownloadStringWithTimeout(System.Net.WebClient client, string url, TimeSpan timeout)
        {
            var task = System.Threading.Tasks.Task.Factory.StartNew(
                () => client.DownloadString(url));
            if (!task.Wait(timeout))
                throw new TimeoutException("GitHub request timed out after " + (int)timeout.TotalSeconds + "s");
            return task.Result;
        }

        /// <summary>
        /// Semver comparison: 1.7.2-beta is older than 1.7.2; build metadata ignored.
        /// Non-numeric segments are tolerated and compared as 0.
        /// </summary>
        private static int CompareVersions(string a, string b)
        {
            a = (a ?? "0.0.0").TrimStart('v', 'V');
            b = (b ?? "0.0.0").TrimStart('v', 'V');
            a = a.Split('+')[0];
            b = b.Split('+')[0];

            string preA = null, preB = null;
            int dash = a.IndexOf('-');
            if (dash >= 0) { preA = a.Substring(dash + 1); a = a.Substring(0, dash); }
            dash = b.IndexOf('-');
            if (dash >= 0) { preB = b.Substring(dash + 1); b = b.Substring(0, dash); }

            var sa = a.Split('.');
            var sb = b.Split('.');
            for (int i = 0; i < 3; i++)
            {
                int na = i < sa.Length && int.TryParse(sa[i], out int xa) ? xa : 0;
                int nb = i < sb.Length && int.TryParse(sb[i], out int xb) ? xb : 0;
                if (na < nb) return -1;
                if (na > nb) return 1;
            }
            // Release outranks its pre-releases
            if (preA == preB) return 0;
            if (preA == null) return 1;
            if (preB == null) return -1;
            return string.CompareOrdinal(preA, preB);
        }

        private void OnQuitClick(object sender, EventArgs e)
        {
            ShowBalloon("TopSolid MCP",
                L("Arrêt du serveur…", "Stopping the server…"),
                ToolTipIcon.Info, 1000);

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

        /// <summary>
        /// Small modal input dialog (no designer form needed). Returns the typed text,
        /// or null when the user cancelled.
        /// </summary>
        private static string PromptInput(string title, string label, string initialValue)
        {
            var form = new Form
            {
                Width = 420,
                Height = 170,
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label
            {
                Left = 12,
                Top = 12,
                Width = 380,
                Height = 44,
                Text = label
            };
            var box = new TextBox
            {
                Left = 12,
                Top = 62,
                Width = 380,
                Text = initialValue ?? ""
            };
            var ok = new Button { Text = "OK", Left = 220, Width = 80, Top = 100, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Annuler", Left = 310, Width = 80, Top = 100, DialogResult = DialogResult.Cancel };
            form.Controls.Add(lbl);
            form.Controls.Add(box);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            return form.ShowDialog() == DialogResult.OK ? box.Text : null;
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

        /// <summary>
        /// Draws the base icon with a TeamViewer-style status badge: a colored dot in
        /// the lower-right corner — green connected, orange connecting, red disconnected.
        /// Must run on the tray thread. The previous composed handle is destroyed so a
        /// session-long reconnect loop cannot leak icons.
        /// </summary>
        private Icon ComposeIcon(TrayState state)
        {
            if (_baseIcon == null) return null;

            try
            {
                using (var bmp = new Bitmap(32, 32))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawIcon(_baseIcon, new Rectangle(0, 0, 32, 32));

                        Color dot;
                        switch (state)
                        {
                            case TrayState.Connected: dot = Color.FromArgb(46, 204, 64); break;
                            case TrayState.Disconnected: dot = Color.FromArgb(232, 65, 66); break;
                            default: dot = Color.FromArgb(255, 165, 0); break;
                        }

                        using (var brush = new SolidBrush(dot))
                        {
                            g.FillEllipse(brush, 19, 19, 11, 11);
                        }
                        using (var pen = new Pen(Color.White, 2f))
                        {
                            g.DrawEllipse(pen, 18, 18, 13, 13);
                        }
                    }

                    IntPtr handle = bmp.GetHicon();
                    var composed = Icon.FromHandle(handle);

                    DestroyComposedHandle();
                    _iconHandle = handle;
                    return composed;
                }
            }
            catch
            {
                return _baseIcon;
            }
        }

        private void DestroyComposedHandle()
        {
            if (_iconHandle != IntPtr.Zero)
            {
                try { DestroyIcon(_iconHandle); } catch { }
                _iconHandle = IntPtr.Zero;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

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
                    notifyIcon.Icon = null;
                    notifyIcon.Dispose();
                }
                DestroyComposedHandle();
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