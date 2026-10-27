using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;

namespace TopSolidMcpServer.Utils
{
    // ─────────────────────────────────────────────────────────────────────
    // TrayIcon partial — MCP Server Settings dialog, API key, auto-reconnect, feedback
    // ─────────────────────────────────────────────────────────────────────
    public partial class TrayIcon
    {
        private const string RepoUrl = "https://github.com/Julien38300/topsolid-automation-mcp";
        private const string FeedbackProxyUrl = "https://topsolid-mcp-feedback.julienpoirier-pro.workers.dev/issue";

        private volatile string _apiKey = "";
        private bool _autoConnect = true;
        private Action _tryConnect;
        private Func<bool> _isConnectedProbe;
        private System.Threading.Timer _reconnectTimer;
        private int _reconnectAttempts;
        private DateTime _lastReconnectAttemptUtc = DateTime.MinValue;
        private bool _wasEverConnected;
        private bool _disconnectNotified;
        private McpServerSettingsDialog _mcpSettingsDialog;

        public string ApiKey => _apiKey ?? "";

        public bool AutoConnect => _autoConnect;

        public void SetApiKey(string key)
        {
            _apiKey = (key ?? "").Trim();
        }

        public void SetAutoConnectors(Action tryConnect, Func<bool> isConnected)
        {
            _tryConnect = tryConnect;
            _isConnectedProbe = isConnected;
        }

        public void StartAutoReconnect()
        {
            _autoConnect = LoadAutoConnectSetting();
            _reconnectTimer = new System.Threading.Timer(_ => AutoReconnectTick(), null, 5000, 5000);
        }

        private void StopAutoReconnect()
        {
            try { _reconnectTimer?.Dispose(); } catch { }
            _reconnectTimer = null;
        }

        private static bool LoadAutoConnectSetting()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (!File.Exists(path)) return true;
                var obj = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                var tok = obj["auto_connect"];
                return tok == null || tok.Value<bool>();
            }
            catch
            {
                return true;
            }
        }

        private static void SaveAutoConnectSetting(bool enabled)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                var obj = File.Exists(path)
                    ? JObject.Parse(File.ReadAllText(path, Encoding.UTF8))
                    : new JObject();
                obj["auto_connect"] = enabled;
                File.WriteAllText(path, obj.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-WARN] Could not save auto_connect: {ex.Message}");
            }
        }

        private void AutoReconnectTick()
        {
            if (_disposed) return;
            try
            {
                bool live = _isConnectedProbe != null ? _isConnectedProbe() : _connected;
                if (live != _connected)
                {
                    SetConnected(live);
                    NoteConnectionState(live);
                }

                if (!ReconnectBackoff.ShouldRetry(_autoConnect, live,
                        _lastReconnectAttemptUtc, DateTime.UtcNow, _reconnectAttempts + 1))
                    return;

                _reconnectAttempts++;
                _lastReconnectAttemptUtc = DateTime.UtcNow;

                _tryConnect?.Invoke();
                bool nowConnected = _isConnectedProbe != null ? _isConnectedProbe() : _connected;
                if (nowConnected)
                {
                    _reconnectAttempts = 0;
                    NoteConnectionState(true);
                }
            }
            catch { }
        }

        public void NoteConnectionState(bool connected)
        {
            if (connected)
            {
                bool reconnected = _wasEverConnected && _disconnectNotified;
                _wasEverConnected = true;
                _disconnectNotified = false;
                _reconnectAttempts = 0;
                if (reconnected)
                    ShowBalloon("TopSolid reconnecté", $"Connexion rétablie sur le port {_port}.", ToolTipIcon.Info);
            }
            else if (_wasEverConnected && !_disconnectNotified)
            {
                _disconnectNotified = true;
                string suffix = _autoConnect ? " Reconnexion automatique activée." : "";
                ShowBalloon("TopSolid déconnecté", "La session TopSolid a été fermée." + suffix, ToolTipIcon.Warning);
            }
        }

        private void InitV18State()
        {
            _autoConnect = LoadAutoConnectSetting();
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = ApiKeyStore.ResolveKey(
                    ApiKeyStore.LoadStoredKey(),
                    Environment.GetEnvironmentVariable(ApiKeyStore.EnvVarName));
            }
        }

        /// <summary>
        /// Builds the root-level MCP Server section items (max 1 level here:
        /// opens the unified MCP Server Settings window or copies the API key in 1 click).
        /// </summary>
        private void BuildMcpServerMenuItems(ContextMenuStrip menu)
        {
            var mcpSettingsItem = new ToolStripMenuItem("Paramètres du serveur MCP…", null, (s, e) => ShowMcpServerSettingsDialog());
            StyleMenuItem(mcpSettingsItem);
            menu.Items.Add(mcpSettingsItem);

            var copyKeyItem = new ToolStripMenuItem("Copier la clé API", null, (s, e) => OnCopyApiKey());
            StyleMenuItem(copyKeyItem);
            menu.Items.Add(copyKeyItem);
        }

        /// <summary>
        /// Populates the 2nd-level "Aide &amp; Maintenance" submenu.
        /// </summary>
        private void BuildHelpSubmenuItems(ToolStripMenuItem helpMenu)
        {
            var whatsNewItem = new ToolStripMenuItem("Nouveautés de cette version", null,
                (s, e) => OpenUrl(GitHubFeedbackLogic.BuildReleaseUrl(_currentVersion)));
            StyleMenuItem(whatsNewItem);
            helpMenu.DropDownItems.Add(whatsNewItem);

            var openDirItem = new ToolStripMenuItem("Ouvrir le dossier d'installation", null, (s, e) => OpenInstallFolder());
            StyleMenuItem(openDirItem);
            helpMenu.DropDownItems.Add(openDirItem);

            helpMenu.DropDownItems.Add(new ToolStripSeparator());

            var bugItem = new ToolStripMenuItem("Signaler un bug…", null, (s, e) => ShowFeedbackDialog(GitHubFeedbackLogic.CategoryBug));
            StyleMenuItem(bugItem);
            helpMenu.DropDownItems.Add(bugItem);

            var featItem = new ToolStripMenuItem("Proposer une amélioration…", null, (s, e) => ShowFeedbackDialog(GitHubFeedbackLogic.CategoryFeature));
            StyleMenuItem(featItem);
            helpMenu.DropDownItems.Add(featItem);

            var ghItem = new ToolStripMenuItem("Page GitHub du projet", null, (s, e) => OpenUrl(RepoUrl));
            StyleMenuItem(ghItem);
            helpMenu.DropDownItems.Add(ghItem);
        }

        internal void ShowMcpServerSettingsDialog()
        {
            try
            {
                if (_mcpSettingsDialog != null && !_mcpSettingsDialog.IsDisposed)
                {
                    _mcpSettingsDialog.BringToFront();
                    _mcpSettingsDialog.Activate();
                    return;
                }

                TraySettings.Load();
                int currentHttpPort = _httpPort > 0
                    ? _httpPort
                    : (TraySettings.HttpPort > 0 ? TraySettings.HttpPort : 8080);

                _mcpSettingsDialog = new McpServerSettingsDialog(
                    initialHttpPort: currentHttpPort,
                    initialApiKey: _apiKey,
                    initialAutoConnect: _autoConnect,
                    initialReadOnly: TraySettings.ReadOnly,
                    onApplyHttpPort: newHttpPort =>
                    {
                        TraySettings.SaveHttpPort(newHttpPort);
                        bool ok = _httpRestartAction == null || _httpRestartAction(newHttpPort);
                        if (ok)
                        {
                            _httpPort = newHttpPort;
                            UpdateStatusDisplay();
                        }
                        return ok;
                    },
                    onSaveApiKey: newKey =>
                    {
                        _apiKey = (newKey ?? "").Trim();
                        ApiKeyStore.SaveKey(_apiKey);
                    },
                    onToggleAutoConnect: enabled =>
                    {
                        _autoConnect = enabled;
                        SaveAutoConnectSetting(enabled);
                        if (enabled) _reconnectAttempts = 0;
                    },
                    onToggleReadOnly: ro =>
                    {
                        TraySettings.Save(TraySettings.Port, ro);
                    },
                    onOpenInstallFolder: OpenInstallFolder);

                _mcpSettingsDialog.Show();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] McpServerSettingsDialog failed: " + ex.Message);
            }
        }

        private void PromptInitialApiKeySetupIfNeeded()
        {
            if (!string.IsNullOrEmpty(_apiKey)) return;

            var menu = _notifyIcon?.ContextMenuStrip;
            Action prompt = () =>
            {
                var r = MessageBox.Show(
                    "Aucune clé API n'est configurée pour le serveur MCP HTTP.\n\n" +
                    "• Oui : générer automatiquement une clé sécurisée (recommandé)\n" +
                    "• Non : ouvrir les paramètres du serveur MCP pour saisir une clé existante\n" +
                    "• Annuler : configurer plus tard",
                    "TopSolid MCP — Configuration de la clé API",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Information);

                if (r == DialogResult.Yes)
                {
                    GenerateAndStoreNewKey(showPopup: true);
                }
                else if (r == DialogResult.No)
                {
                    ShowMcpServerSettingsDialog();
                }
            };
            if (menu != null && menu.IsHandleCreated) menu.BeginInvoke(prompt);
            else prompt();
        }

        private void GenerateAndStoreNewKey(bool showPopup)
        {
            string key = McpHttpAuthLogic.GenerateKey();
            _apiKey = key;
            ApiKeyStore.SaveKey(key);
            TrySetClipboard(key);
            if (showPopup)
            {
                MessageBox.Show(
                    "Nouvelle clé API générée et copiée dans le presse-papiers :\n\n" + key +
                    "\n\nCollez-la dans l'en-tête X-API-Key de votre client MCP.",
                    "TopSolid MCP — Clé API",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                ShowBalloon("Nouvelle clé API", "Générée et copiée dans le presse-papiers.", ToolTipIcon.Info);
            }
        }

        private void OnCopyApiKey()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                ShowMcpServerSettingsDialog();
                return;
            }
            if (TrySetClipboard(_apiKey))
                ShowBalloon("Clé API copiée", "La clé API a été copiée dans le presse-papiers.", ToolTipIcon.Info);
        }

        internal static bool TrySetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text ?? "");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void OpenInstallFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-WARN] Could not open folder: {ex.Message}");
            }
        }

        private static string TrySubmitFeedbackViaProxy(string kind, string title, string body, string version)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(FeedbackProxyUrl);
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.UserAgent = "TopSolidMcpServer/" + (version ?? "0.0.0");
                req.Timeout = 8000;
                req.ReadWriteTimeout = 8000;

                var payload = new JObject
                {
                    ["kind"] = kind,
                    ["title"] = title,
                    ["body"] = body,
                    ["version"] = version ?? ""
                };
                byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None));
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream())
                    s.Write(bytes, 0, bytes.Length);

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var obj = JObject.Parse(r.ReadToEnd());
                    return obj.Value<string>("html_url");
                }
            }
            catch
            {
                return null;
            }
        }

        private void ShowFeedbackDialog(string kind)
        {
            using (var dlg = new FeedbackDialog(kind))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string title = GitHubFeedbackLogic.BuildTitle(kind, dlg.SubjectText);
                var logs = dlg.IncludeLogs
                    ? ServerLog.ReadLastLines(ServerLog.GetPath(), 50)
                    : new System.Collections.Generic.List<string>();
                string body = GitHubFeedbackLogic.BuildBody(
                    dlg.DescriptionText, _currentVersion, Environment.OSVersion.VersionString,
                    _connected, _port, logs, _apiKey);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    string issueUrl = TrySubmitFeedbackViaProxy(kind, title, body, _currentVersion);
                    if (!string.IsNullOrEmpty(issueUrl))
                    {
                        ShowBalloon("Merci pour votre retour !", "Ticket GitHub créé — cliquez pour l'ouvrir.", ToolTipIcon.Info);
                        OpenUrl(issueUrl);
                    }
                    else
                    {
                        OpenUrl(GitHubFeedbackLogic.BuildNewIssueUrl(kind, title, body));
                    }
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // McpServerSettingsDialog — Unified, modern MCP Server Settings UI
        // ─────────────────────────────────────────────────────────────────

        internal sealed class McpServerSettingsDialog : Form
        {
            private readonly Func<int, bool> _onApplyHttpPort;
            private readonly Action<string> _onSaveApiKey;
            private readonly Action<bool> _onToggleAutoConnect;
            private readonly Action<bool> _onToggleReadOnly;

            private readonly TextBox _urlBox;
            private readonly NumericUpDown _httpPortBox;
            private readonly Label _httpStatusLabel;

            private readonly TextBox _apiKeyBox;
            private readonly Button _toggleRevealButton;
            private readonly Button _editSaveKeyButton;
            private readonly Label _keyStatusLabel;

            private string _currentApiKey;
            private bool _keyRevealed;
            private bool _keyEditing;

            public McpServerSettingsDialog(
                int initialHttpPort,
                string initialApiKey,
                bool initialAutoConnect,
                bool initialReadOnly,
                Func<int, bool> onApplyHttpPort,
                Action<string> onSaveApiKey,
                Action<bool> onToggleAutoConnect,
                Action<bool> onToggleReadOnly,
                Action onOpenInstallFolder)
            {
                _onApplyHttpPort = onApplyHttpPort;
                _onSaveApiKey = onSaveApiKey;
                _onToggleAutoConnect = onToggleAutoConnect;
                _onToggleReadOnly = onToggleReadOnly;
                _currentApiKey = (initialApiKey ?? "").Trim();

                int safePort = (initialHttpPort >= 1024 && initialHttpPort <= 65535) ? initialHttpPort : 8080;

                Text = "Paramètres du serveur MCP";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(560, 520);
                BackColor = Color.FromArgb(248, 250, 252);
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

                // ── Top Banner ──
                var banner = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 66,
                    BackColor = Color.White
                };
                banner.Paint += (s, e) =>
                {
                    using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                        e.Graphics.DrawLine(pen, 0, banner.Height - 1, banner.Width, banner.Height - 1);
                };

                var titleLabel = new Label
                {
                    Text = "Paramètres du serveur MCP",
                    Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Location = new Point(22, 12),
                    AutoSize = true
                };
                var subtitleLabel = new Label
                {
                    Text = "Point d'accès HTTP, clé d'authentification API et comportement du serveur",
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(22, 37),
                    AutoSize = true
                };
                banner.Controls.Add(titleLabel);
                banner.Controls.Add(subtitleLabel);
                Controls.Add(banner);

                // ── Card 1 : Point d'accès HTTP (Streamable HTTP) ──
                var httpCard = CreateCard(20, 82, 520, 138, "POINT D'ACCÈS HTTP (STREAMABLE HTTP)");

                var urlLabel = new Label
                {
                    Text = "URL cliente MCP :",
                    ForeColor = Color.FromArgb(51, 65, 85),
                    Location = new Point(16, 42),
                    AutoSize = true
                };
                httpCard.Controls.Add(urlLabel);

                var urlContainer = CreateInputContainer(132, 36, 254, 32, Color.FromArgb(248, 250, 252));
                _urlBox = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    ReadOnly = true,
                    TabStop = false,
                    BackColor = Color.FromArgb(248, 250, 252),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Font = new Font("Consolas", 9.75f, FontStyle.Regular),
                    Text = $"http://localhost:{safePort}/mcp",
                    Location = new Point(8, 7),
                    Width = 238
                };
                urlContainer.Controls.Add(_urlBox);
                httpCard.Controls.Add(urlContainer);

                var copyUrlBtn = CreateSecondaryButton("Copier l'URL", 394, 36, 110, 32);
                copyUrlBtn.Click += (s, e) =>
                {
                    if (TrySetClipboard(_urlBox.Text))
                    {
                        _httpStatusLabel.ForeColor = Color.FromArgb(21, 128, 61);
                        _httpStatusLabel.Text = "\u2713 URL copiée";
                    }
                };
                httpCard.Controls.Add(copyUrlBtn);

                var portLabel = new Label
                {
                    Text = "Port d'écoute TCP :",
                    ForeColor = Color.FromArgb(51, 65, 85),
                    Location = new Point(16, 86),
                    AutoSize = true
                };
                httpCard.Controls.Add(portLabel);

                var portContainer = CreateInputContainer(132, 80, 96, 32, Color.White);
                _httpPortBox = new NumericUpDown
                {
                    BorderStyle = BorderStyle.None,
                    Minimum = 1024,
                    Maximum = 65535,
                    Value = safePort,
                    Font = new Font("Consolas", 10f, FontStyle.Regular),
                    Location = new Point(8, 6),
                    Width = 82
                };
                portContainer.Controls.Add(_httpPortBox);
                httpCard.Controls.Add(portContainer);

                var applyPortBtn = CreatePrimaryButton("Appliquer", 236, 80, 100, 32);
                applyPortBtn.Click += OnApplyHttpPortClick;
                httpCard.Controls.Add(applyPortBtn);

                _httpStatusLabel = new Label
                {
                    Text = $"Actif sur le port {safePort}",
                    Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(344, 87),
                    AutoSize = true
                };
                httpCard.Controls.Add(_httpStatusLabel);
                Controls.Add(httpCard);

                // ── Card 2 : Authentification (Clé API X-API-Key) ──
                var authCard = CreateCard(20, 234, 520, 138, "AUTHENTIFICATION (EN-TÊTE X-API-KEY)");

                var keyContainer = CreateInputContainer(16, 36, 294, 32, Color.FromArgb(248, 250, 252));
                _apiKeyBox = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    ReadOnly = true,
                    TabStop = false,
                    BackColor = Color.FromArgb(248, 250, 252),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Font = new Font("Consolas", 9.5f, FontStyle.Regular),
                    Location = new Point(8, 7),
                    Width = 278
                };
                keyContainer.Controls.Add(_apiKeyBox);
                authCard.Controls.Add(keyContainer);

                _toggleRevealButton = CreateSecondaryButton("Afficher", 318, 36, 88, 32);
                _toggleRevealButton.Click += (s, e) =>
                {
                    if (_keyEditing) return;
                    _keyRevealed = !_keyRevealed;
                    RefreshApiKeyDisplay();
                };
                authCard.Controls.Add(_toggleRevealButton);

                var copyKeyBtn = CreatePrimaryButton("Copier", 414, 36, 90, 32);
                copyKeyBtn.Click += (s, e) =>
                {
                    string toCopy = _keyEditing ? (_apiKeyBox.Text ?? "").Trim() : _currentApiKey;
                    if (string.IsNullOrEmpty(toCopy))
                    {
                        _keyStatusLabel.ForeColor = Color.FromArgb(185, 28, 28);
                        _keyStatusLabel.Text = "Aucune clé API définie.";
                        return;
                    }
                    if (TrySetClipboard(toCopy))
                    {
                        _keyStatusLabel.ForeColor = Color.FromArgb(21, 128, 61);
                        _keyStatusLabel.Text = "\u2713 Clé API copiée dans le presse-papiers";
                    }
                };
                authCard.Controls.Add(copyKeyBtn);

                var regenKeyBtn = CreateSecondaryButton("Régénérer une clé…", 16, 80, 148, 32);
                regenKeyBtn.Click += OnRegenerateKeyClick;
                authCard.Controls.Add(regenKeyBtn);

                _editSaveKeyButton = CreateSecondaryButton("Saisir manuellement…", 172, 80, 152, 32);
                _editSaveKeyButton.Click += (s, e) => OnEditOrSaveKeyClick(keyContainer);
                authCard.Controls.Add(_editSaveKeyButton);

                _keyStatusLabel = new Label
                {
                    Text = "Chiffrée via Windows DPAPI",
                    Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(332, 87),
                    AutoSize = true
                };
                authCard.Controls.Add(_keyStatusLabel);
                Controls.Add(authCard);

                // ── Card 3 : Comportement & Sécurité ──
                var behaviorCard = CreateCard(20, 386, 520, 76, "COMPORTEMENT && SÉCURITÉ");

                var autoConnectCheck = new CheckBox
                {
                    Text = "Connexion automatique à TopSolid",
                    Checked = initialAutoConnect,
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Location = new Point(16, 38),
                    AutoSize = true,
                    Cursor = Cursors.Hand
                };
                autoConnectCheck.CheckedChanged += (s, e) =>
                {
                    try { _onToggleAutoConnect?.Invoke(autoConnectCheck.Checked); } catch { }
                };
                behaviorCard.Controls.Add(autoConnectCheck);

                var readOnlyCheck = new CheckBox
                {
                    Text = "Mode lecture seule (au redémarrage)",
                    Checked = initialReadOnly,
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Location = new Point(268, 38),
                    AutoSize = true,
                    Cursor = Cursors.Hand
                };
                readOnlyCheck.CheckedChanged += (s, e) =>
                {
                    try { _onToggleReadOnly?.Invoke(readOnlyCheck.Checked); } catch { }
                };
                behaviorCard.Controls.Add(readOnlyCheck);
                Controls.Add(behaviorCard);

                // ── Footer ──
                var openFolderBtn = CreateSecondaryButton("Ouvrir le dossier d'installation", 20, 474, 210, 32);
                openFolderBtn.Click += (s, e) =>
                {
                    try { onOpenInstallFolder?.Invoke(); } catch { }
                };
                Controls.Add(openFolderBtn);

                var closeBtn = CreateSecondaryButton("Fermer", 440, 474, 100, 32);
                closeBtn.Click += (s, e) => Close();
                Controls.Add(closeBtn);
                CancelButton = closeBtn;

                RefreshApiKeyDisplay();
            }

            private void RefreshApiKeyDisplay()
            {
                if (_keyEditing) return;

                if (string.IsNullOrEmpty(_currentApiKey))
                {
                    _apiKeyBox.Text = "(aucune clé configurée — accès libre local)";
                    _apiKeyBox.ForeColor = Color.FromArgb(100, 116, 139);
                    _toggleRevealButton.Enabled = false;
                    _toggleRevealButton.Text = "Afficher";
                }
                else
                {
                    _apiKeyBox.ForeColor = Color.FromArgb(15, 23, 42);
                    _toggleRevealButton.Enabled = true;
                    if (_keyRevealed)
                    {
                        _apiKeyBox.Text = _currentApiKey;
                        _toggleRevealButton.Text = "Masquer";
                    }
                    else
                    {
                        string suffix = _currentApiKey.Length > 4
                            ? _currentApiKey.Substring(_currentApiKey.Length - 4)
                            : _currentApiKey;
                        _apiKeyBox.Text = "tsmcp_\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" + suffix;
                        _toggleRevealButton.Text = "Afficher";
                    }
                }
            }

            private void OnApplyHttpPortClick(object sender, EventArgs e)
            {
                int newPort = (int)_httpPortBox.Value;
                bool ok = false;
                try
                {
                    ok = _onApplyHttpPort == null || _onApplyHttpPort(newPort);
                }
                catch { ok = false; }

                if (ok)
                {
                    _urlBox.Text = $"http://localhost:{newPort}/mcp";
                    _httpStatusLabel.ForeColor = Color.FromArgb(21, 128, 61);
                    _httpStatusLabel.Text = $"\u2713 Port {newPort} actif";
                }
                else
                {
                    _httpStatusLabel.ForeColor = Color.FromArgb(185, 28, 28);
                    _httpStatusLabel.Text = $"Port {newPort} indisponible";
                }
            }

            private void OnRegenerateKeyClick(object sender, EventArgs e)
            {
                var confirm = MessageBox.Show(
                    "Générer une nouvelle clé API ?\n\n" +
                    "L'ancienne clé sera immédiatement invalidée et la nouvelle clé sera copiée dans le presse-papiers.",
                    "Régénérer la clé API",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes) return;

                string newKey = McpHttpAuthLogic.GenerateKey();
                _currentApiKey = newKey;
                _keyEditing = false;
                _keyRevealed = true;
                _apiKeyBox.ReadOnly = true;
                _editSaveKeyButton.Text = "Saisir manuellement…";
                try { _onSaveApiKey?.Invoke(newKey); } catch { }
                TrySetClipboard(newKey);
                RefreshApiKeyDisplay();

                _keyStatusLabel.ForeColor = Color.FromArgb(21, 128, 61);
                _keyStatusLabel.Text = "\u2713 Nouvelle clé générée et copiée";
            }

            private void OnEditOrSaveKeyClick(Panel keyContainer)
            {
                if (!_keyEditing)
                {
                    _keyEditing = true;
                    _apiKeyBox.ReadOnly = false;
                    _apiKeyBox.BackColor = Color.White;
                    keyContainer.BackColor = Color.White;
                    _apiKeyBox.ForeColor = Color.FromArgb(15, 23, 42);
                    _apiKeyBox.Text = _currentApiKey;
                    _apiKeyBox.Focus();
                    _apiKeyBox.SelectAll();
                    _toggleRevealButton.Enabled = false;
                    _editSaveKeyButton.Text = "Enregistrer la clé";
                    _keyStatusLabel.ForeColor = Color.FromArgb(37, 99, 235);
                    _keyStatusLabel.Text = "Modifiez puis cliquez sur Enregistrer";
                }
                else
                {
                    string entered = (_apiKeyBox.Text ?? "").Trim();
                    _currentApiKey = entered;
                    _keyEditing = false;
                    _keyRevealed = false;
                    _apiKeyBox.ReadOnly = true;
                    _apiKeyBox.BackColor = Color.FromArgb(248, 250, 252);
                    keyContainer.BackColor = Color.FromArgb(248, 250, 252);
                    _editSaveKeyButton.Text = "Saisir manuellement…";
                    try { _onSaveApiKey?.Invoke(_currentApiKey); } catch { }
                    RefreshApiKeyDisplay();

                    _keyStatusLabel.ForeColor = Color.FromArgb(21, 128, 61);
                    _keyStatusLabel.Text = string.IsNullOrEmpty(_currentApiKey)
                        ? "Authentification désactivée"
                        : "\u2713 Clé API enregistrée";
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Shared UI Builder Helpers (Modern Slate / Blue Windows 11 Theme)
        // ─────────────────────────────────────────────────────────────────

        internal static Panel CreateCard(int x, int y, int width, int height, string sectionTitle)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.White
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            if (!string.IsNullOrEmpty(sectionTitle))
            {
                var header = new Label
                {
                    Text = sectionTitle,
                    Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(16, 12),
                    AutoSize = true
                };
                card.Controls.Add(header);
            }
            return card;
        }

        internal static Panel CreateInputContainer(int x, int y, int width, int height, Color backColor)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = backColor
            };
            panel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(203, 213, 225), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            return panel;
        }

        internal static Button CreatePrimaryButton(string text, int x, int y, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            return btn;
        }

        internal static Button CreateSecondaryButton(string text, int x, int y, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);
            btn.EnabledChanged += (s, e) =>
            {
                btn.BackColor = btn.Enabled ? Color.White : Color.FromArgb(248, 250, 252);
                btn.ForeColor = btn.Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(148, 163, 184);
            };
            return btn;
        }

        internal static Button CreateDangerButton(string text, int x, int y, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 241, 242),
                ForeColor = Color.FromArgb(190, 18, 60),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(254, 205, 211);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 228, 230);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(254, 205, 211);
            btn.EnabledChanged += (s, e) =>
            {
                btn.BackColor = btn.Enabled ? Color.FromArgb(255, 241, 242) : Color.FromArgb(248, 250, 252);
                btn.ForeColor = btn.Enabled ? Color.FromArgb(190, 18, 60) : Color.FromArgb(148, 163, 184);
                btn.FlatAppearance.BorderColor = btn.Enabled ? Color.FromArgb(254, 205, 211) : Color.FromArgb(203, 213, 225);
            };
            return btn;
        }

        // ─────────────────────────────────────────────────────────────────
        // FeedbackDialog — Modernized bug / feature report dialog
        // ─────────────────────────────────────────────────────────────────

        private sealed class FeedbackDialog : Form
        {
            private readonly TextBox _subject;
            private readonly TextBox _description;
            private readonly CheckBox _includeLogs;

            public string SubjectText => _subject.Text;
            public string DescriptionText => _description.Text;
            public bool IncludeLogs => _includeLogs.Checked;

            public FeedbackDialog(string kind)
            {
                bool isBug = !string.Equals(kind, GitHubFeedbackLogic.CategoryFeature, StringComparison.OrdinalIgnoreCase);
                Text = isBug ? "Signaler un bug — TopSolid MCP" : "Proposer une amélioration — TopSolid MCP";
                Width = 520;
                Height = 400;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterScreen;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = Color.FromArgb(248, 250, 252);
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

                var lbl1 = new Label
                {
                    Text = "Sujet :",
                    Left = 20,
                    Top = 18,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Font = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold)
                };
                _subject = new TextBox { Left = 20, Top = 42, Width = 464, Font = new Font("Segoe UI", 9.5f) };

                var lbl2 = new Label
                {
                    Text = isBug ? "Étapes pour reproduire / comportement observé :" : "Description de l'amélioration souhaitée :",
                    Left = 20,
                    Top = 78,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Font = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold)
                };
                _description = new TextBox
                {
                    Left = 20,
                    Top = 102,
                    Width = 464,
                    Height = 165,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Segoe UI", 9.5f)
                };

                _includeLogs = new CheckBox
                {
                    Left = 20,
                    Top = 280,
                    Width = 464,
                    Checked = isBug,
                    ForeColor = Color.FromArgb(51, 65, 85),
                    Text = "Joindre la version, l'OS et les 50 dernières lignes de log (clé API masquée)"
                };

                var ok = CreatePrimaryButton("Envoyer", 384, 314, 100, 32);
                ok.DialogResult = DialogResult.OK;
                var cancel = CreateSecondaryButton("Annuler", 276, 314, 100, 32);
                cancel.DialogResult = DialogResult.Cancel;

                Controls.AddRange(new Control[] { lbl1, _subject, lbl2, _description, _includeLogs, ok, cancel });
                AcceptButton = ok;
                CancelButton = cancel;
            }
        }
    }
}
