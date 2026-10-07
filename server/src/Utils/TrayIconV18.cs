using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Tray features added in v1.8.0: API key management, GitHub feedback,
    /// "What's new" release page, automatic reconnection. Split from TrayIcon.cs
    /// (partial class) to keep each file reviewable; all UI rules of TrayIcon.cs
    /// apply here too (tray-thread marshalling via _uiContext, L() for FR/EN).
    /// </summary>
    public partial class TrayIcon
    {
        // ---- API key (v1.8.0) ----
        private ToolStripMenuItem _apiKeyItem;
        private volatile string _apiKey;             // effective key, hot-reloadable
        private Func<string> _apiKeyProvider;       // how the HTTP server reads the key live

        // ---- Auto-reconnect (v1.8.0) ----
        private System.Windows.Forms.Timer _reconnectTimer;
        private bool _autoConnect = true;
        private ToolStripMenuItem _autoConnectItem;
        private DateTime _lastAutoAttemptUtc = DateTime.MinValue;
        private int _autoAttempt;
        private volatile bool _lastConnectedState;   // for transition balloons
        private volatile bool _stateAnnounced;       // skip the first transition balloon

        /// <summary>
        /// Gives the tray the live API key (read from ApiKeyStore at startup,
        /// regenerated from the menu afterwards). The HTTP server reads the key
        /// through the same provider so a regeneration applies immediately.
        /// </summary>
        public void SetApiKey(string key)
        {
            _apiKey = key ?? "";
        }

        /// <summary>Current effective key (may be empty = auth disabled).</summary>
        public string ApiKey => _apiKey ?? "";

        /// <summary>Whether auto-reconnect is on (persisted in settings.json by Program).</summary>
        public bool AutoConnect => _autoConnect;

        /// <summary>
        /// Program wires this so the tray's auto-reconnect can drive the connector.
        /// </summary>
        public void SetAutoConnectors(Action tryConnect, Func<bool> isConnected)
        {
            _tryConnect = tryConnect;
            _isConnected = isConnected;
        }

        private Action _tryConnect;
        private Func<bool> _isConnected;

        /// <summary>Builds the v1.8.0 menu entries; called from BuildTray() on the tray thread.</summary>
        private void BuildV18MenuItems(ContextMenuStrip menu, ToolStripMenuItem settingsMenu)
        {
            // ── API key, inside Settings ──
            _apiKeyItem = new ToolStripMenuItem(BuildApiKeyLabel());
            _apiKeyItem.Click += OnApiKeyClick;
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(_apiKeyItem);

            var keyGenItem = new ToolStripMenuItem(L("Regénérer la clé API…", "Regenerate API key…"));
            keyGenItem.Click += OnApiKeyRegenerateClick;
            settingsMenu.DropDownItems.Add(keyGenItem);

            var keyCopyItem = new ToolStripMenuItem(L("Copier la clé API", "Copy API key"));
            keyCopyItem.Click += OnApiKeyCopyClick;
            settingsMenu.DropDownItems.Add(keyCopyItem);

            // ── Auto-connect toggle, inside Settings ──
            _autoConnectItem = new ToolStripMenuItem(_autoConnect
                ? L("Connexion automatique : oui (cliquer pour désactiver)", "Auto-connect: on (click to turn off)")
                : L("Connexion automatique : non (cliquer pour activer)", "Auto-connect: off (click to turn on)"));
            _autoConnectItem.Click += (s, ev) =>
            {
                _autoConnect = !_autoConnect;
                TraySettings.Save(_port > 0 ? _port : 0, _readOnly);
                _autoConnectItem.Text = _autoConnect
                    ? L("Connexion automatique : oui (cliquer pour désactiver)", "Auto-connect: on (click to turn off)")
                    : L("Connexion automatique : non (cliquer pour activer)", "Auto-connect: off (click to turn on)");
                if (_autoConnect) StartAutoReconnect();
            };
            settingsMenu.DropDownItems.Add(_autoConnectItem);

            // ── What's new / feedback / report bug, next to GitHub ──
            int insertIdx = 0;
            foreach (ToolStripItem it in menu.Items)
            {
                var tsmi = it as ToolStripMenuItem;
                if (tsmi != null && tsmi.Text == "GitHub") break;
                insertIdx++;
            }

            var whatsNewItem = new ToolStripMenuItem(L("Nouveautés de la version actuelle", "What's new in this version"));
            whatsNewItem.Click += (s, ev) => OpenUrl(GitHubFeedbackLogic.BuildReleaseUrl(GetVersion()));
            menu.Items.Insert(insertIdx, whatsNewItem);

            var reportBug = new ToolStripMenuItem(L("Signaler un bug…", "Report a bug…"));
            reportBug.Click += (s, ev) => OnFeedbackClick(GitHubFeedbackLogic.CategoryBug);
            menu.Items.Insert(insertIdx + 1, reportBug);

            var suggestItem = new ToolStripMenuItem(L("Proposer une amélioration…", "Suggest a feature…"));
            suggestItem.Click += (s, ev) => OnFeedbackClick(GitHubFeedbackLogic.CategoryFeature);
            menu.Items.Insert(insertIdx + 2, suggestItem);
        }

        private string BuildApiKeyLabel()
        {
            string key = _apiKey ?? "";
            if (string.IsNullOrEmpty(key))
                return L("Clé API : aucune (accès libre)", "API key: none (open access)");
            return string.Format(L("Clé API : {0}", "API key: {0}"), Protocol.McpHttpAuthLogic.MaskKey(key));
        }

        // ---- API key handlers ----

        private void OnApiKeyClick(object sender, EventArgs e)
        {
            string input = PromptInput(
                L("Clé API", "API key"),
                L("Clé API du serveur HTTP (laisser VIDE pour désactiver l'auth) :", "HTTP server API key (leave EMPTY to disable auth):"),
                _apiKey ?? "");
            if (input == null) return; // cancelled
            input = input.Trim();
            if (input.Length > 0 && !Protocol.McpHttpAuthLogic.IsValidKeyShape(input))
            {
                ShowResultBox(L("Clé API", "API key"),
                    L("Format invalide : attendu tsmcp_ + 32 caractères hexadécimaux.\nUtilisez « Regénérer » pour en produire une valide.",
                      "Invalid format: expected tsmcp_ + 32 hex chars.\nUse 'Regenerate' to produce a valid one."),
                    MessageBoxIcon.Warning);
                return;
            }
            ApplyNewKey(input);
        }

        private void OnApiKeyRegenerateClick(object sender, EventArgs e)
        {
            var choice = MessageBox.Show(
                L("L'ANCIENNE clé sera révoquée immédiatement. Continuer ?", "The OLD key will be revoked immediately. Continue?"),
                L("Regénérer la clé API", "Regenerate API key"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes) return;

            string newKey = Protocol.McpHttpAuthLogic.GenerateKey();
            ApplyNewKey(newKey);
            TryClipboardSetText(newKey);
            ShowResultBox(L("Clé API", "API key"),
                L("Nouvelle clé générée et copiée dans le presse-papiers.\nElle est active immédiatement (les anciennes sessions sont rejetées).",
                  "New key generated and copied to the clipboard.\nIt is effective immediately (old sessions are rejected)."),
                MessageBoxIcon.Information);
        }

        private void OnApiKeyCopyClick(object sender, EventArgs e)
        {
            string key = _apiKey ?? "";
            if (string.IsNullOrEmpty(key))
            {
                ShowResultBox(L("Clé API", "API key"),
                    L("Aucune clé configurée. Utilisez « Regénérer » d'abord.", "No key configured. Use 'Regenerate' first."),
                    MessageBoxIcon.Warning);
                return;
            }
            TryClipboardSetText(key);
            ShowBalloon(L("Clé API", "API key"),
                L("Clé copiée dans le presse-papiers.", "Key copied to the clipboard."),
                ToolTipIcon.Info, 2500);
        }

        /// <summary>Stores + applies the key; the HTTP server picks it up live.</summary>
        private void ApplyNewKey(string key)
        {
            _apiKey = key ?? "";
            ApiKeyStore.SaveKey(_apiKey);
            if (_apiKeyItem != null) _apiKeyItem.Text = BuildApiKeyLabel();
            ShowBalloon(L("Clé API", "API key"),
                _apiKey.Length == 0
                    ? L("Authentification désactivée.", "Authentication disabled.")
                    : L("Clé enregistrée — active immédiatement.", "Key saved — effective immediately."),
                ToolTipIcon.Info, 2500);
        }

        private static void TryClipboardSetText(string text)
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { Console.Error.WriteLine("[TrayIcon] Clipboard error: " + ex.Message); }
        }

        // ---- GitHub feedback handlers ----

        private void OnFeedbackClick(string category)
        {
            string subject = PromptInput(
                category == GitHubFeedbackLogic.CategoryBug ? L("Signaler un bug", "Report a bug") : L("Proposer une amélioration", "Suggest a feature"),
                L("Décrivez en une ligne (le détail sera demandé après) :", "Describe in one line (details asked after):"),
                "");
            if (string.IsNullOrWhiteSpace(subject)) return;

            string details = PromptInput(
                L("Détails", "Details"),
                L("Détails / étapes pour reproduire (optionnel) :", "Details / reproduction steps (optional):"),
                "");
            if (details == null) details = "";

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { SubmitFeedback(category, subject, details); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[TrayIcon] Feedback error: " + ex.Message);
                }
            });
        }

        /// <summary>Builds the issue and posts it (PAT) or opens the prefilled form.
        /// v1.8.1: when a crash report file exists (last 30 days), its content is
        /// appended so the issue carries the stack trace without the user doing
        /// anything — crashes reported after the fact used to ship with zero
        /// technical context.</summary>
        private void SubmitFeedback(string category, string subject, string details)
        {
            string title = GitHubFeedbackLogic.BuildTitle(category, subject);
            string body = GitHubFeedbackLogic.BuildBody(details, GetVersion(),
                Environment.OSVersion.VersionString,
                _isConnected != null && _isConnected(),
                _port > 0 ? _port : 8090,
                RecentLogLines(), _apiKey);

            // v1.8.1: attach the newest crash report (scrubbed) to bug reports.
            if (category == GitHubFeedbackLogic.CategoryBug)
            {
                try
                {
                    string crashPath = LogTail.GetNewestCrashReport();
                    if (crashPath != null)
                    {
                        string crash = File.ReadAllText(crashPath);
                        body += "\n## Rapport de crash (auto-attaché)\n\n```\n"
                              + GitHubFeedbackLogic.ScrubKey(crash, _apiKey)
                              + "\n```\n";
                    }
                }
                catch { }
            }

            string token = GitHubPatStore.LoadToken();
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var client = new System.Net.WebClient();
                    client.Headers["User-Agent"] = "TopSolidMcpServer-Tray";
                    client.Headers["Authorization"] = "token " + token;
                    client.Headers["Accept"] = "application/vnd.github+json";
                    var payload = GitHubFeedbackLogic.BuildIssueJson(title, body,
                        new[] { category == GitHubFeedbackLogic.CategoryBug ? "tray-report" : "tray-enhancement" });
                    string resp = client.UploadString(
                        "https://api.github.com/repos/" + GitHubFeedbackLogic.RepoOwner + "/" + GitHubFeedbackLogic.RepoName + "/issues",
                        payload);
                    string url = (JObject.Parse(resp)["html_url"] ?? "").ToString();
                    ShowBalloonSafe(L("Remontée envoyée", "Feedback sent"),
                        L("Issue GitHub créée : " + url, "GitHub issue created: " + url),
                        ToolTipIcon.Info, 6000);
                    return;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[TrayIcon] Issue create failed, falling back to browser: " + ex.Message);
                }
            }

            // Fallback: prefilled new-issue form in the browser.
            string formUrl = GitHubFeedbackLogic.BuildNewIssueUrl(category, title, body);
            OpenUrl(formUrl);
        }

        /// <summary>Last ~30 stderr log lines for the issue body. Thread-safe ring buffer read.</summary>
        private static IList<string> RecentLogLines()
        {
            try
            {
                string logPath = LogTail.GetLogPath();
                if (!File.Exists(logPath)) return new string[0];
                return LogTail.ReadLastLines(logPath, 30);
            }
            catch { return new string[0]; }
        }

        // ---- Auto-reconnect (v1.8.0) ----

        /// <summary>Starts the reconnect timer; call from BuildTray / Program when the connector exists.</summary>
        public void StartAutoReconnect()
        {
            if (_reconnectTimer == null)
            {
                _reconnectTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                _reconnectTimer.Tick += OnReconnectTimerTick;
            }
            _reconnectTimer.Start();
        }

        private void OnReconnectTimerTick(object sender, EventArgs e)
        {
            // Timer runs on the tray thread — do the actual connect on a worker thread.
            var tryConnect = _tryConnect;
            var isConnected = _isConnected;
            if (tryConnect == null || isConnected == null) return;

            bool connected = false;
            try { connected = isConnected(); } catch { }

            if (connected)
            {
                _autoAttempt = 0;
                // Announce only transitions (down → up).
                if (!_lastConnectedState && _stateAnnounced)
                {
                    ShowBalloon("TopSolid MCP",
                        L("Connexion TopSolid rétablie.", "TopSolid connection restored."),
                        ToolTipIcon.Info, 3000);
                }
                _lastConnectedState = true;
                _stateAnnounced = true;
                return;
            }

            if (_lastConnectedState && _stateAnnounced)
            {
                ShowBalloon("TopSolid MCP",
                    L("Connexion TopSolid perdue — nouvelle tentative automatique…", "TopSolid connection lost — auto-retrying…"),
                    ToolTipIcon.Warning, 3000);
            }
            _lastConnectedState = false;
            _stateAnnounced = true;

            if (!ShouldAttemptNow()) return;

            _lastAutoAttemptUtc = DateTime.UtcNow;
            _autoAttempt++;
            PostState(TrayState.Connecting);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { tryConnect(); }
                catch (Exception ex) { Console.Error.WriteLine("[TrayIcon] Auto-reconnect error: " + ex.Message); }
            });
        }

        private bool ShouldAttemptNow()
        {
            return ReconnectBackoff.ShouldRetry(_autoConnect, _lastConnectedState,
                _lastAutoAttemptUtc, DateTime.UtcNow, _autoAttempt + 1);
        }

        /// <summary>Called by Program when the connector state changes, so the tray mirrors it.</summary>
        public void NoteConnectionState(bool connected)
        {
            _lastConnectedState = connected;
            _stateAnnounced = true;
        }
    }

    /// <summary>
    /// Minimal stderr tail used by the feedback feature. Program.cs redirects
    /// stderr to a file in v1.8.1 (ServerLog.Setup in Utils/ServerLog.cs); this
    /// reads it back. Kept as a thin wrapper for compatibility with v1.8.0 call
    /// sites; the canonical implementation lives in ServerLog.
    /// </summary>
    internal static class LogTail
    {
        public static string GetLogPath()
        {
            return ServerLog.GetPath();
        }

        public static IList<string> ReadLastLines(string path, int count)
        {
            return ServerLog.ReadLastLines(path, count);
        }

        /// <summary>
        /// Newest crash-*.txt in the log dir (last 30 days), or null. Attached to
        /// the next bug report so a crash reported after the fact carries its
        /// stack trace + log context.
        /// </summary>
        public static string GetNewestCrashReport()
        {
            try
            {
                string dir = Path.GetDirectoryName(ServerLog.GetPath());
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
                string newest = null;
                DateTime newestTime = DateTime.MinValue;
                foreach (string f in Directory.GetFiles(dir, "crash-*.txt"))
                {
                    DateTime t = File.GetLastWriteTimeUtc(f);
                    if (t > newestTime && t > DateTime.UtcNow.AddDays(-30))
                    {
                        newestTime = t;
                        newest = f;
                    }
                }
                return newest;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// GitHub PAT (optional, scope public_repo) used by the tray feedback feature.
    /// Stored DPAPI-encrypted next to the API key in settings.json.
    /// </summary>
    internal static class GitHubPatStore
    {
        public static string LoadToken()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (!File.Exists(path)) return "";
                var obj = JObject.Parse(File.ReadAllText(path));
                string enc = obj.Value<string>("github_pat_dpapi") ?? "";
                if (string.IsNullOrEmpty(enc)) return "";
                return ApiKeyStore.Unprotect(enc);
            }
            catch { return ""; }
        }

        public static void SaveToken(string token)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                var obj = File.Exists(path)
                    ? JObject.Parse(File.ReadAllText(path))
                    : new JObject();
                obj["github_pat_dpapi"] = string.IsNullOrEmpty(token) ? "" : ApiKeyStore.Protect(token);
                File.WriteAllText(path, obj.ToString());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[GitHubPatStore] Save failed: " + ex.Message);
            }
        }
    }
}