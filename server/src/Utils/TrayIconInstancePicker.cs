using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// v1.8.3: TopSolid instance picker for the tray, mirroring the FEA Quality
    /// app's "version TopSolid" dropdown: the Settings submenu lists every running
    /// TopSolid (version + automation port, or no port), one click retargets the
    /// connector immediately; "Manage…" opens the declared-instance dialog where
    /// known instances can be named and a default chosen.
    /// <para>
    /// All UI rules of TrayIcon.cs apply: tray-thread only mutations, L() FR/EN,
    /// settings.json persistence with CLI/env precedence kept by Program.cs.
    /// </para>
    /// </summary>
    public partial class TrayIcon
    {
        private ToolStripMenuItem _instanceMenu;          // "Instance TopSolid" submenu

        /// <summary>
        /// Program wires the retarget so a picker click applies immediately
        /// (disconnect + reconnect on the new port), like the FEA app.
        /// Returns true when connected to the new target.
        /// </summary>
        private Func<int, bool> _retarget;

        /// <summary>Wires the connector retarget used by the instance picker.</summary>
        public void SetRetargetAction(Func<int, bool> retarget)
        {
            _retarget = retarget;
        }

        /// <summary>
        /// v1.8.10: Program wires the native HTTP endpoint hot-restart so the
        /// "Manage" dialog can change the MCP port without a server restart.
        /// Returns true when the endpoint listens on the new port.
        /// </summary>
        private Func<int, bool> _httpRestart;

        /// <summary>v1.8.10: port the native HTTP endpoint currently listens on.</summary>
        private volatile int _httpPort;

        /// <summary>Wires the HTTP endpoint restart used by the "Manage" dialog (v1.8.10).</summary>
        public void SetHttpRestartAction(Func<int, bool> restart)
        {
            _httpRestart = restart;
        }

        /// <summary>Program reports the effective HTTP port after binding (v1.8.10).</summary>
        public void SetHttpPort(int port)
        {
            _httpPort = port;
        }

        /// <summary>Port the native MCP endpoint listens on (default 8080).</summary>
        public int HttpPort
        {
            get { int p = _httpPort; return p > 0 ? p : 8080; }
        }

        /// <summary>
        /// v1.8.10: live HTTP-restart action, read by the "Manage" dialog
        /// (null = stdio-only mode, port saved for the next start).
        /// </summary>
        internal Func<int, bool> HttpRestart
        {
            get { return _httpRestart; }
        }

        /// <summary>Effective read-only mode, shown by the "Manage" dialog (v1.8.10).</summary>
        internal bool ReadOnlyMode
        {
            get { return _readOnly; }
        }

        /// <summary>
        /// v1.8.3: the port the connector currently targets. Read by the
        /// "Manage" dialog to seed a fresh list with the running target.
        /// </summary>
        public int CurrentTargetPort
        {
            get { return _port; }
        }

        /// <summary>Builds the instance picker items, called from BuildTray() on the tray thread.</summary>
        private void BuildInstancePicker(ToolStripMenuItem settingsMenu)
        {
            _instanceMenu = new ToolStripMenuItem(L("Instance TopSolid", "TopSolid instance"));
            settingsMenu.DropDownItems.Insert(0, _instanceMenu);

            // Rescan each time the submenu is about to open: the running set
            // changes while the tray lives for days.
            _instanceMenu.DropDownOpening += (s, e) => RefreshInstanceMenu();

            RefreshInstanceMenu();
        }

        /// <summary>
        /// Rebuilds the instance list items. Callable from any thread.
        /// CRITICAL WinForms rule: when called from DropDownOpening on the tray STA
        /// thread, DropDownItems MUST be populated synchronously before DropDownOpening
        /// returns — clearing DropDownItems and populating asynchronously via Post()
        /// leaves DropDownItems.Count == 0 when OnDropDownShow checks it, which causes
        /// WinForms to cancel opening the submenu every time.
        /// </summary>
        private void RefreshInstanceMenu()
        {
            try
            {
                var menu = _instanceMenu;
                if (menu == null) return;

                var context = _uiContext;
                if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
                {
                    // Called from a worker thread: scan off-thread, then marshal
                    // the menu rebuild to the tray thread.
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        List<TopSolidInstance> bgInstances;
                        try { bgInstances = TopSolidInstanceList.Scan(); }
                        catch { bgInstances = new List<TopSolidInstance>(); }

                        var ctx = _uiContext;
                        if (ctx != null)
                            ctx.Post(__ => PopulateInstanceMenu(bgInstances), null);
                    });
                    return;
                }

                // Already on the tray STA thread (e.g. DropDownOpening): scan and
                // populate synchronously so DropDownItems is non-empty on return.
                List<TopSolidInstance> instances;
                try { instances = TopSolidInstanceList.Scan(); }
                catch { instances = new List<TopSolidInstance>(); }
                PopulateInstanceMenu(instances);
            }
            catch { /* tray gone */ }
        }

        private void PopulateInstanceMenu(List<TopSolidInstance> instances)
        {
            try
            {
                var menu = _instanceMenu;
                if (menu == null) return;
                menu.DropDownItems.Clear();

                var runningPorts = new HashSet<int>();
                var declaredList = TraySettings.DeclaredInstances ?? new List<DeclaredTopSolidInstance>();
                var declaredByPort = new Dictionary<int, DeclaredTopSolidInstance>();
                foreach (var d in declaredList)
                {
                    if (d != null && d.Port > 0 && !declaredByPort.ContainsKey(d.Port))
                        declaredByPort[d.Port] = d;
                }

                if (instances.Count == 0 && declaredList.Count == 0)
                {
                    var none = new ToolStripMenuItem(
                        L("Aucun TopSolid détecté", "No TopSolid detected"));
                    none.Enabled = false;
                    menu.DropDownItems.Add(none);
                }
                else
                {
                    foreach (var inst in instances)
                    {
                        int port = inst.Port;
                        if (port > 0) runningPorts.Add(port);

                        DeclaredTopSolidInstance decl = null;
                        if (port > 0) declaredByPort.TryGetValue(port, out decl);

                        bool isActiveTarget = inst.HasAutomationPort && port == _port;
                        string label = BuildInstanceLabel(inst, decl, isActiveTarget);
                        var item = new ToolStripMenuItem(label);
                        item.Enabled = inst.HasAutomationPort;
                        item.Checked = isActiveTarget;
                        item.ToolTipText = inst.HasAutomationPort
                            ? L("Cible cette instance pour les outils MCP (port " + port + ")",
                                "Target this instance for the MCP tools (port " + port + ")")
                            : L("Aucun port Automation détecté pour ce processus (vérifiez Outils > Options > Général > Automation ou un conflit de port 8090).",
                                "No Automation port detected for this process (check Tools > Options > General > Automation or a port 8090 conflict).");
                        var capturedInst = inst;
                        item.Click += (s, ev) => OnInstanceClick(capturedInst);
                        menu.DropDownItems.Add(item);
                    }

                    // Also show declared instances from "Gérer…" whose port is not currently detected
                    foreach (var decl in declaredList)
                    {
                        if (decl == null || decl.Port <= 0 || runningPorts.Contains(decl.Port))
                            continue;

                        bool isActiveTarget = (decl.Port == _port);
                        string suffix = L("non détecté", "not detected");
                        string activeTag = isActiveTarget ? L(" — cible actuelle", " — current target") : "";
                        string label = (decl.IsDefault ? "★ " : "") + decl.Name +
                            " (port " + decl.Port + " — " + suffix + activeTag + ")";

                        var item = new ToolStripMenuItem(label);
                        item.Checked = isActiveTarget;
                        item.ToolTipText = L(
                            "Instance déclarée sur le port " + decl.Port + " (cliquer pour cibler ce port)",
                            "Declared instance on port " + decl.Port + " (click to target this port)");
                        var capturedDecl = decl;
                        item.Click += (s, ev) => RetargetToPort(
                            capturedDecl.Port,
                            capturedDecl.Name + " (port " + capturedDecl.Port + ")");
                        menu.DropDownItems.Add(item);
                    }
                }

                menu.DropDownItems.Add(new ToolStripSeparator());

                // "Automatic" — the ideal case: detect the first running TopSolid
                // and target it, now and at every startup (instance_mode = "auto").
                var autoItem = new ToolStripMenuItem(
                    L("Automatique (premier TopSolid détecté)", "Automatic (first detected TopSolid)"));
                autoItem.Checked = (TraySettings.InstanceMode == "auto");
                autoItem.ToolTipText = L(
                    "Au démarrage, le serveur détecte le premier TopSolid lancé et cible son port Automation.",
                    "At startup, the server detects the first running TopSolid and targets its Automation port.");
                autoItem.Click += (s, ev) => OnAutoClick();
                menu.DropDownItems.Add(autoItem);

                var manageItem = new ToolStripMenuItem(L("Gérer…", "Manage…"));
                manageItem.Click += (s, ev) => OnManageInstancesClick();
                menu.DropDownItems.Add(manageItem);
            }
            catch { /* menu gone */ }
        }

        private string BuildInstanceLabel(TopSolidInstance inst, DeclaredTopSolidInstance decl = null, bool isActiveTarget = false)
        {
            // "TopSolid 7.21 (port 8091)" — "TopSolid (sans port)" when Automation
            // is off: visible in the list but not clickable, like the FEA app.
            string version = inst.Version ?? "";
            string name = string.IsNullOrEmpty(version)
                ? "TopSolid"
                : "TopSolid " + version;

            if (decl != null && !string.IsNullOrWhiteSpace(decl.Name) &&
                !string.Equals(decl.Name, name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(decl.Name, "TopSolid " + inst.Port, StringComparison.OrdinalIgnoreCase))
            {
                name = name + " — " + decl.Name;
            }

            if (decl != null && decl.IsDefault)
                name = "★ " + name;

            if (!inst.HasAutomationPort)
                return name + " (" + L("sans port Automation", "no Automation port") + ")";

            string activeSuffix = isActiveTarget
                ? " [" + L("actif", "active") + "]"
                : "";
            return name + " (port " + inst.Port + ")" + activeSuffix;
        }

        private void OnInstanceClick(TopSolidInstance inst)
        {
            RetargetToPort(inst.Port, BuildInstanceLabel(inst));
        }

        /// <summary>
        /// Switches the active TopSolid target port in manual mode, persists it to
        /// settings.json, updates the tray badge state, and refreshes the instance menu.
        /// </summary>
        internal void RetargetToPort(int port, string displayLabel)
        {
            if (port <= 0) return;

            _port = port;
            try { TraySettings.SaveInstanceMode(port, "manual"); }
            catch { }

            var retarget = _retarget;
            if (retarget == null)
            {
                PostState((TrayState)Volatile.Read(ref _state));
                RefreshInstanceMenu();
                return;
            }

            PostState(TrayState.Connecting);
            ShowBalloon(L("Instance TopSolid", "TopSolid instance"),
                L("Connexion à " + displayLabel + "…", "Connecting to " + displayLabel + "…"),
                ToolTipIcon.Info, 2000);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = false;
                try { ok = retarget(port); }
                catch { ok = false; }
                finally
                {
                    PostState(ok ? TrayState.Connected : TrayState.Disconnected);
                    ShowBalloonSafe("TopSolid MCP",
                        ok
                            ? L("Connecté à " + displayLabel + ".", "Connected to " + displayLabel + ".")
                            : L("Cible définie sur " + displayLabel + ", mais TopSolid ne répond pas sur le port " + port + ".",
                                "Target set to " + displayLabel + ", but TopSolid is not responding on port " + port + "."),
                        ok ? ToolTipIcon.Info : ToolTipIcon.Warning, 3000);

                    RefreshInstanceMenu();
                }
            });
        }

        private void OnAutoClick()
        {
            // Switch to "auto" mode: save it, then retarget now to the first
            // detected running TopSolid (if any) so the change is immediate.
            try { TraySettings.SaveInstanceMode(0, "auto"); }
            catch { }

            PostState(TrayState.Connecting);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int detected = 0;
                try
                {
                    var list = TopSolidInstanceList.Scan();
                    foreach (var inst in list)
                    {
                        if (inst != null && inst.HasAutomationPort)
                        {
                            detected = inst.Port;
                            break;
                        }
                    }
                }
                catch { }

                if (detected > 0)
                {
                    _port = detected;
                    var retarget = _retarget;
                    bool ok = false;
                    try
                    {
                        if (retarget != null) ok = retarget(detected);
                    }
                    catch { ok = false; }
                    finally
                    {
                        PostState(ok ? TrayState.Connected : TrayState.Disconnected);
                    }

                    ShowBalloonSafe("TopSolid MCP",
                        ok
                            ? L("Mode automatique : connecté au TopSolid détecté (port " + detected + ").", "Automatic mode: connected to the detected TopSolid (port " + detected + ").")
                            : L("Mode automatique : détection ok (port " + detected + ") mais connexion échouée.", "Automatic mode: detection ok (port " + detected + ") but connection failed."),
                        ok ? ToolTipIcon.Info : ToolTipIcon.Warning, 3000);
                }
                else
                {
                    bool currentlyConnected = false;
                    try { if (_isConnected != null) currentlyConnected = _isConnected(); } catch { }
                    PostState(currentlyConnected ? TrayState.Connected : TrayState.Disconnected);

                    ShowBalloonSafe("TopSolid MCP",
                        L("Mode automatique activé. Aucun TopSolid lancé pour l'instant — le serveur ciblera le premier détecté.", "Automatic mode enabled. No TopSolid running yet — the server will target the first detected one."),
                        ToolTipIcon.Info, 3000);
                }

                RefreshInstanceMenu();
            });
        }

        private void OnManageInstancesClick()
        {
            try
            {
                using (var dlg = new ManageInstancesDialog(this))
                {
                    dlg.ShowDialog();
                    RefreshInstanceMenu();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TrayIcon] Manage instances dialog error: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// v1.8.10: "Manage TopSolid" dialog — the single settings surface:
    /// declared instances with icon buttons (add, edit, remove, default star,
    /// reorder up/down) and a server section (MCP HTTP port hot-restarted,
    /// read-only mode, client connection URL). Persisted to settings.json;
    /// the env var keeps winning over the dialog.
    /// </summary>
    internal sealed class ManageInstancesDialog : Form
    {
        // ── Segoe icon glyphs (Segoe Fluent Icons on Win11 / MDL2 on Win10) ──
        private const string GlyphAdd = "\uE710";
        private const string GlyphRemove = "\uE74D";
        private const string GlyphEdit = "\uE70F";
        private const string GlyphStar = "\uE735";
        private const string GlyphUp = "\uE70D";
        private const string GlyphDown = "\uE70E";
        private const string GlyphApply = "\uE72C";
        private const string GlyphCancel = "\uE711";
        private const string GlyphSave = "\uE73E";

        private static string _iconFontFamily;   // null until probed; "" = no icon font

        private readonly TrayIcon _owner;
        private readonly ListBox _list;
        private readonly TextBox _nameBox;
        private readonly NumericUpDown _portBox;
        private readonly Button _addButton;          // "+ Add" -> "Save" in edit mode
        private readonly Button _cancelEditButton;   // visible in edit mode only
        private readonly Button _upButton;
        private readonly Button _downButton;
        private readonly Button _editButton;
        private readonly Button _removeButton;
        private readonly Button _defaultButton;
        private readonly NumericUpDown _httpPortBox;
        private readonly Button _httpApplyButton;
        private readonly Label _urlLabel;
        private readonly CheckBox _readOnlyBox;
        private readonly ToolTip _tips;
        private List<DeclaredTopSolidInstance> _instances;
        private int _editIndex = -1;                // >= 0 while editing that row

        internal ManageInstancesDialog(TrayIcon owner)
        {
            _owner = owner;
            _tips = new ToolTip();
            Text = L("Gérer les instances TopSolid", "Manage TopSolid instances");
            Width = 580;
            Height = 640;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;

            _instances = new List<DeclaredTopSolidInstance>(TraySettings.DeclaredInstances);
            if (_instances.Count == 0)
            {
                // v1.8.3: nothing declared yet — seed the list with the currently
                // targeted port as default, so the dialog is never empty and the
                // user can immediately see what the server targets.
                int currentPort = owner.CurrentTargetPort;
                if (currentPort > 0)
                {
                    _instances.Add(new DeclaredTopSolidInstance("TopSolid " + currentPort, currentPort) { IsDefault = true });
                    TraySettings.SaveDeclaredInstances(_instances);
                }
            }

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12)
            };

            // ── Section: declared instances ──
            var instancesBox = new GroupBox
            {
                Text = L("Instances TopSolid", "TopSolid instances"),
                Width = 540,
                Height = 300
            };
            var instancesFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 18, 8, 4)
            };
            instancesBox.Controls.Add(instancesFlow);

            var listRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            _list = new ListBox { Width = 420, Height = 170 };
            _list.HorizontalScrollbar = true;
            _list.DoubleClick += (s, ev) => OnEditRequested();
            _tips.SetToolTip(_list,
                L("Double-clic sur une instance pour l'éditer.", "Double-click an instance to edit it."));
            listRow.Controls.Add(_list);

            var sideButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Width = 46,
                Padding = new Padding(2)
            };
            _upButton = IconButton(GlyphUp, "↑", L("Monter", "Move up"));
            _upButton.Click += (s, ev) => OnMove(-1);
            _downButton = IconButton(GlyphDown, "↓", L("Descendre", "Move down"));
            _downButton.Click += (s, ev) => OnMove(+1);
            _editButton = IconButton(GlyphEdit, L("Éditer", "Edit"), L("Modifier l'instance sélectionnée (ou double-clic).", "Edit the selected instance (or double-click)."));
            _editButton.Click += (s, ev) => OnEditRequested();
            _removeButton = IconButton(GlyphRemove, L("Suppr.", "Del"), L("Supprimer", "Remove"));
            _removeButton.Click += (s, ev) => OnRemove();
            _defaultButton = IconButton(GlyphStar, L("Défaut", "Def."), L("Définir par défaut (★) — ciblée immédiatement et au démarrage.", "Set as default (★) — targeted immediately and at startup."));
            _defaultButton.Click += (s, ev) => OnSetDefault();
            sideButtons.Controls.Add(_upButton);
            sideButtons.Controls.Add(_downButton);
            sideButtons.Controls.Add(_editButton);
            sideButtons.Controls.Add(_removeButton);
            sideButtons.Controls.Add(_defaultButton);
            listRow.Controls.Add(sideButtons);
            instancesFlow.Controls.Add(listRow);

            var intro = new Label
            {
                Text = L(
                    "Déclarez vos instances (nom + port Automation). L'ordre de la liste est celui du menu.",
                    "Declare your instances (name + Automation port). The list order is the menu order."),
                AutoSize = true
            };
            instancesFlow.Controls.Add(intro);

            var nameRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            nameRow.Controls.Add(new Label
            {
                Text = L("Nom : ", "Name: "),
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            });
            _nameBox = new TextBox { Width = 190 };
            nameRow.Controls.Add(_nameBox);
            nameRow.Controls.Add(new Label
            {
                Text = L("  Port : ", "  Port: "),
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            });
            _portBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 8090,
                Width = 80
            };
            nameRow.Controls.Add(_portBox);
            _addButton = new Button
            {
                Width = 120,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Margin = new Padding(1)
            };
            _addButton.Click += (s, ev) => OnAddOrSave();
            _tips.SetToolTip(_addButton, L("Ajouter une instance déclarée.", "Add a declared instance."));
            nameRow.Controls.Add(_addButton);
            _cancelEditButton = new Button
            {
                Width = 90,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Margin = new Padding(1)
            };
            _cancelEditButton.Visible = false;
            _cancelEditButton.Click += (s, ev) => CancelEdit();
            _tips.SetToolTip(_cancelEditButton, L("Annuler la modification en cours.", "Cancel the ongoing edit."));
            nameRow.Controls.Add(_cancelEditButton);
            instancesFlow.Controls.Add(nameRow);

            layout.Controls.Add(instancesBox);

            // ── Section: server / client connection ──
            var serverBox = new GroupBox
            {
                Text = L("Serveur MCP", "MCP server"),
                Width = 540,
                Height = 180
            };
            var serverFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 18, 8, 4)
            };
            serverBox.Controls.Add(serverFlow);

            var portRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            portRow.Controls.Add(new Label
            {
                Text = L("Port MCP (HTTP) : ", "MCP port (HTTP): "),
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            });
            _httpPortBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = owner.HttpPort,
                Width = 80
            };
            portRow.Controls.Add(_httpPortBox);
            _httpApplyButton = new Button
            {
                Width = 120,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Margin = new Padding(1)
            };
            _httpApplyButton.Click += (s, ev) => OnApplyHttpPort();
            _tips.SetToolTip(_httpApplyButton, L("Redémarre l'endpoint HTTP sur ce port, immédiatement.", "Restarts the HTTP endpoint on this port, immediately."));
            portRow.Controls.Add(_httpApplyButton);
            serverFlow.Controls.Add(portRow);

            _urlLabel = new Label
            {
                Text = L("URL client : ", "Client URL: ") + "http://localhost:" + owner.HttpPort + "/mcp",
                AutoSize = true
            };
            _tips.SetToolTip(_urlLabel,
                L("Clients HTTP : cette URL + la clé API (Paramètres > Clé API).\nClients stdio (Claude Desktop…) : aucun port ni clé requis.",
                  "HTTP clients: this URL + the API key (Settings > API key).\nstdio clients (Claude Desktop…): no port, no key."));
            serverFlow.Controls.Add(_urlLabel);

            _readOnlyBox = new CheckBox
            {
                Text = L("Mode lecture seule (appliqué au prochain démarrage)", "Read-only mode (applied next start)"),
                AutoSize = true
            };
            _readOnlyBox.Checked = owner.ReadOnlyMode;
            _readOnlyBox.CheckedChanged += (s, ev) =>
            {
                try { TraySettings.Save(TraySettings.Port, _readOnlyBox.Checked); }
                catch { }
            };
            serverFlow.Controls.Add(_readOnlyBox);

            layout.Controls.Add(serverBox);

            var okRow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Width = 540 };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = L("Annuler", "Cancel"), DialogResult = DialogResult.Cancel };
            okRow.Controls.Add(cancel);
            okRow.Controls.Add(ok);
            layout.Controls.Add(okRow);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;

            ApplyButtonGlyphs();
            ReloadList();
        }

        /// <summary>Small square button with a Segoe icon glyph, text fallback included.</summary>
        private Button IconButton(string glyph, string fallbackText, string tooltip)
        {
            var b = new Button
            {
                Width = 40,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Margin = new Padding(1)
            };
            string family = IconFontFamily();
            if (family.Length > 0)
            {
                b.Text = glyph;
                b.Font = new Font(family, 11f);
            }
            else
            {
                b.Text = fallbackText;
            }
            if (tooltip != null) _tips.SetToolTip(b, tooltip);
            return b;
        }

        /// <summary>Probes the Segoe icon fonts once; empty string = none installed.</summary>
        private static string IconFontFamily()
        {
            if (_iconFontFamily != null) return _iconFontFamily;
            foreach (var family in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                try
                {
                    using (var probe = new Font(family, 9f))
                    {
                        if (string.Equals(probe.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase))
                        {
                            _iconFontFamily = family;
                            return _iconFontFamily;
                        }
                    }
                }
                catch { }
            }
            _iconFontFamily = "";
            return _iconFontFamily;
        }

        /// <summary>Sets the labelled button glyphs according to the icon font availability.</summary>
        private void ApplyButtonGlyphs()
        {
            string family = IconFontFamily();
            _addButton.Text = family.Length > 0
                ? GlyphAdd + "  " + L("Ajouter", "Add")
                : L("Ajouter", "Add");
            _cancelEditButton.Text = family.Length > 0
                ? GlyphCancel + "  " + L("Annuler", "Cancel")
                : L("Annuler", "Cancel");
            _httpApplyButton.Text = family.Length > 0
                ? GlyphApply + "  " + L("Appliquer", "Apply")
                : L("Appliquer", "Apply");
        }

        /// <summary>Add-mode button shows "+" / edit-mode shows the check "Save".</summary>
        private void SetAddMode(bool addMode)
        {
            string family = IconFontFamily();
            _addButton.Text = addMode
                ? (family.Length > 0 ? GlyphAdd + "  " + L("Ajouter", "Add") : L("Ajouter", "Add"))
                : (family.Length > 0 ? GlyphSave + "  " + L("Enregistrer", "Save") : L("Enregistrer", "Save"));
            _cancelEditButton.Visible = !addMode;
        }

        private void Persist()
        {
            try { TraySettings.SaveDeclaredInstances(_instances); }
            catch { }
        }

        private void ReloadList()
        {
            if (_list == null) return;
            _list.Items.Clear();
            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                _list.Items.Add((i + 1) + ". " + (inst.IsDefault ? "★ " : "") +
                    inst.Name + " — port " + inst.Port);
            }
            if (_editIndex >= 0 && _editIndex < _list.Items.Count)
                _list.SelectedIndex = _editIndex;
        }

        private void OnAdd()
        {
            string name = _nameBox.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this,
                    L("Saisissez un nom.", "Enter a name."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (TraySettingsContains(name))
            {
                MessageBox.Show(this,
                    L("Ce nom existe déjà.", "This name already exists."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var added = new DeclaredTopSolidInstance(name, (int)_portBox.Value);
            if (_instances.Count == 0)
                added.IsDefault = true;
            _instances.Add(added);
            TraySettings.SaveDeclaredInstances(_instances);
            if (added.IsDefault && _owner != null)
                _owner.RetargetToPort(added.Port, added.Name + " (port " + added.Port + ")");
            ReloadList();
            _nameBox.Clear();
        }

        private bool TraySettingsContains(string name)
        {
            foreach (var i in _instances)
                if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private void OnRemove()
        {
            int idx = _list.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show(this,
                    L("Sélectionnez une instance à supprimer.", "Select an instance to remove."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (idx == _editIndex) CancelEdit();
            else if (idx < _editIndex) _editIndex--;

            _instances.RemoveAt(idx);
            Persist();
            ReloadList();
        }

        /// <summary>Marks the selected instance as the startup default (exactly one) and retargets immediately.</summary>
        private void OnSetDefault()
        {
            int idx = _list.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show(this,
                    L("Sélectionnez une instance.", "Select an instance."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            for (int i = 0; i < _instances.Count; i++)
                _instances[i].IsDefault = (i == idx);

            var chosen = _instances[idx];
            Persist();
            TraySettings.SaveInstanceMode(chosen.Port, "manual");
            if (_owner != null)
                _owner.RetargetToPort(chosen.Port, chosen.Name + " (port " + chosen.Port + ")");
            ReloadList();
        }

        // ── Editing (v1.8.10) ──

        private void OnEditRequested()
        {
            int idx = _list.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show(this,
                    L("Sélectionnez une instance à modifier.", "Select an instance to edit."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _editIndex = idx;
            _nameBox.Text = _instances[idx].Name;
            _portBox.Value = _instances[idx].Port;
            SetAddMode(false);
            _nameBox.Focus();
        }

        private void CancelEdit()
        {
            _editIndex = -1;
            _nameBox.Clear();
            SetAddMode(true);
        }

        private void OnAddOrSave()
        {
            if (_editIndex >= 0) OnSaveEdit();
            else OnAdd();
        }

        private void OnSaveEdit()
        {
            int idx = _editIndex;
            string name = _nameBox.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this,
                    L("Saisissez un nom.", "Enter a name."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            for (int i = 0; i < _instances.Count; i++)
            {
                if (i != idx && string.Equals(_instances[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this,
                        L("Ce nom existe déjà.", "This name already exists."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int newPort = (int)_portBox.Value;
            var old = _instances[idx];
            if (old.Name == name && old.Port == newPort)
            {
                CancelEdit();
                return;                              // nothing changed
            }

            // DeclaredTopSolidInstance has private setters: rebuild the row,
            // keeping the default star.
            _instances[idx] = new DeclaredTopSolidInstance(name, newPort) { IsDefault = old.IsDefault };
            Persist();

            // Keep the live target consistent when the default instance moved.
            if (_instances[idx].IsDefault)
                TraySettings.SaveInstanceMode(newPort, "manual");

            _editIndex = -1;
            _nameBox.Clear();
            SetAddMode(true);
            ReloadList();
        }

        // ── Reorder (v1.8.10) ──

        /// <summary>
        /// Moves the selected instance one slot up (-1) or down (+1).
        /// The new order is the JSON order and the menu order.
        /// </summary>
        private void OnMove(int delta)
        {
            int idx = _list.SelectedIndex;
            int target = idx + delta;
            if (idx < 0 || target < 0 || target >= _instances.Count)
                return;

            var tmp = _instances[idx];
            _instances[idx] = _instances[target];
            _instances[target] = tmp;
            Persist();
            ReloadList();
            _list.SelectedIndex = target;
        }

        // ── MCP HTTP port (v1.8.10) ──

        private void OnApplyHttpPort()
        {
            int newPort = (int)_httpPortBox.Value;
            int current = _owner != null ? _owner.HttpPort : 8080;
            if (newPort == current)
                return;                               // nothing to do

            var restart = _owner != null ? _owner.HttpRestart : null;
            if (restart == null)
            {
                // No live endpoint (stdio-only mode): persist for the next start.
                TraySettings.SaveHttpPort(newPort);
                _urlLabel.Text = L("URL client : ", "Client URL: ") + "http://localhost:" + newPort + "/mcp";
                MessageBox.Show(this,
                    L("Port enregistré. Il sera utilisé au prochain démarrage du serveur.", "Port saved. It will be used the next time the server starts."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool ok = false;
            try { ok = restart(newPort); }
            catch { ok = false; }

            if (ok && _owner != null)
            {
                _owner.SetHttpPort(newPort);
                _urlLabel.Text = L("URL client : ", "Client URL: ") + "http://localhost:" + newPort + "/mcp";
                MessageBox.Show(this,
                    L("Endpoint MCP redémarré sur le port " + newPort + ".", "MCP endpoint restarted on port " + newPort + "."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _httpPortBox.Value = current;
                MessageBox.Show(this,
                    L("Impossible d'écouter sur le port " + newPort + " (port occupé ?). L'ancien port est conservé.",
                      "Cannot listen on port " + newPort + " (port busy?). The previous port is kept."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string L(string fr, string en)
        {
            try
            {
                return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? fr : en;
            }
            catch { return en; }
        }
    }
}