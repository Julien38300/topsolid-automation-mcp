using System;
using System.Collections.Generic;
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
    /// v1.8.3: "Manage TopSolid" modal dialog — the declared-instance list with
    /// name + port, a default selection, add/remove. Persisted to settings.json.
    /// </summary>
    internal sealed class ManageInstancesDialog : Form
    {
        private readonly TrayIcon _owner;
        private readonly ListBox _list;
        private readonly TextBox _nameBox;
        private readonly NumericUpDown _portBox;
        private readonly Button _addButton;
        private readonly Button _removeButton;
        private List<DeclaredTopSolidInstance> _instances;

        internal ManageInstancesDialog(TrayIcon owner)
        {
            _owner = owner;
            Text = L("Gérer les TopSolid", "Manage TopSolid");
            Width = 480;
            Height = 400;
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

            var intro = new Label
            {
                Text = L(
                    "Déclarez vos instances TopSolid (nom + port Automation).\nLa coche « par défaut » sera ciblée immédiatement et au démarrage.",
                    "Declare your TopSolid instances (name + Automation port).\nThe \"default\" one is targeted immediately and at startup."),
                AutoSize = true
            };
            layout.Controls.Add(intro);

            _list = new ListBox { Width = 430, Height = 160 };
            _list.HorizontalScrollbar = true;
            layout.Controls.Add(_list);

            var nameRow = new FlowLayoutPanel { AutoSize = true };
            nameRow.Controls.Add(new Label
            {
                Text = L("Nom : ", "Name: "),
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            });
            _nameBox = new TextBox { Width = 200 };
            nameRow.Controls.Add(_nameBox);
            nameRow.Controls.Add(new Label
            {
                Text = L("  Port : ", "  Port: "),
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            });
            _portBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 8090,
                Width = 80
            };
            nameRow.Controls.Add(_portBox);
            _addButton = new Button { Text = L("Ajouter", "Add"), AutoSize = true };
            _addButton.Click += (s, ev) => OnAdd();
            nameRow.Controls.Add(_addButton);
            layout.Controls.Add(nameRow);

            var buttonRow = new FlowLayoutPanel { AutoSize = true };
            _removeButton = new Button { Text = L("Supprimer", "Remove"), AutoSize = true };
            _removeButton.Click += (s, ev) => OnRemove();
            buttonRow.Controls.Add(_removeButton);

            // "Par défaut" — marks the selected instance as the startup default and retargets immediately.
            var defaultButton = new Button { Text = L("Définir par défaut", "Set as default"), AutoSize = true };
            defaultButton.Click += (s, ev) => OnSetDefault();
            buttonRow.Controls.Add(defaultButton);
            layout.Controls.Add(buttonRow);

            var okRow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Width = 430 };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = L("Annuler", "Cancel"), DialogResult = DialogResult.Cancel };
            okRow.Controls.Add(cancel);
            okRow.Controls.Add(ok);
            layout.Controls.Add(okRow);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;

            ReloadList();
        }

        private void ReloadList()
        {
            if (_list == null) return;
            _list.Items.Clear();
            foreach (var inst in _instances)
                _list.Items.Add((inst.IsDefault ? "★ " : "") + inst.Name + " — port " + inst.Port);
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
            _instances.RemoveAt(idx);
            TraySettings.SaveDeclaredInstances(_instances);
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
            TraySettings.SaveDeclaredInstances(_instances);
            TraySettings.SaveInstanceMode(chosen.Port, "manual");
            if (_owner != null)
                _owner.RetargetToPort(chosen.Port, chosen.Name + " (port " + chosen.Port + ")");
            ReloadList();
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