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

        /// <summary>Rebuilds the instance list items. Callable from any thread.</summary>
        private void RefreshInstanceMenu()
        {
            try
            {
                var menu = _instanceMenu;
                if (menu == null) return;

                // Menu items belong to the tray thread: post when called from
                // elsewhere (the retarget/auto callbacks run on the thread pool).
                var context = _uiContext;
                if (context != null && !ReferenceEquals(Thread.CurrentThread, _thread))
                {
                    context.Post(_ => RefreshInstanceMenu(), null);
                    return;
                }

                menu.DropDownItems.Clear();

                // Scan can hit MainModule access-denied and WMI: background it,
                // then post the rebuild back on the tray thread.
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    List<TopSolidInstance> instances;
                    try { instances = TopSolidInstanceList.Scan(); }
                    catch { instances = new List<TopSolidInstance>(); }

                    var ctx = _uiContext;
                    if (ctx == null) return;
                    ctx.Post(__ => PopulateInstanceMenu(instances), null);
                });
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

                if (instances.Count == 0)
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
                        var item = new ToolStripMenuItem(BuildInstanceLabel(inst));
                        item.Enabled = inst.HasAutomationPort;
                        item.ToolTipText = L(
                            "Cible cette instance pour les outils MCP (port " + port + ")",
                            "Target this instance for the MCP tools (port " + port + ")");
                        item.Click += (s, ev) => OnInstanceClick(inst);
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

        private string BuildInstanceLabel(TopSolidInstance inst)
        {
            // "TopSolid 7.21 (port 8091)" — "TopSolid (sans port)" when Automation
            // is off: visible in the list but not clickable, like the FEA app.
            string version = inst.Version ?? "";
            string name = string.IsNullOrEmpty(version)
                ? "TopSolid"
                : "TopSolid " + version;
            return inst.HasAutomationPort
                ? name + " (port " + inst.Port + ")"
                : name + " (" + L("sans port", "no port") + ")";
        }

        private void OnInstanceClick(TopSolidInstance inst)
        {
            var retarget = _retarget;
            if (retarget == null) return;

            ShowBalloon(L("Instance TopSolid", "TopSolid instance"),
                L("Connexion à " + BuildInstanceLabel(inst) + "…", "Connecting to " + BuildInstanceLabel(inst) + "…"),
                ToolTipIcon.Info, 2000);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok;
                try { ok = retarget(inst.Port); }
                catch { ok = false; }

                // Persist the choice: it becomes the startup default (the ★ in the
                // "Manage" dialog), like the FEA app's default instance.
                try { TraySettings.SaveInstanceMode(inst.Port, "manual"); }
                catch { }

                ShowBalloonSafe("TopSolid MCP",
                    ok
                        ? L("Connecté à " + BuildInstanceLabel(inst) + ".", "Connected to " + BuildInstanceLabel(inst) + ".")
                        : L("Impossible de se connecter à " + BuildInstanceLabel(inst) + ".", "Could not connect to " + BuildInstanceLabel(inst) + "."),
                    ok ? ToolTipIcon.Info : ToolTipIcon.Warning, 3000);

                if (!ok) RefreshInstanceMenu();
            });
        }

        private void OnAutoClick()
        {
            // Switch to "auto" mode: save it, then retarget now to the first
            // detected running TopSolid (if any) so the change is immediate.
            try { TraySettings.SaveInstanceMode(0, "auto"); }
            catch { }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                int detected = 0;
                try
                {
                    var list = TopSolidInstanceList.Scan();
                    if (list.Count > 0 && list[0].HasAutomationPort)
                        detected = list[0].Port;
                }
                catch { }

                if (detected > 0)
                {
                    var retarget = _retarget;
                    bool ok = false;
                    if (retarget != null)
                    {
                        try { ok = retarget(detected); }
                        catch { ok = false; }
                    }

                    ShowBalloonSafe("TopSolid MCP",
                        ok
                            ? L("Mode automatique : connecté au TopSolid détecté (port " + detected + ").", "Automatic mode: connected to the detected TopSolid (port " + detected + ").")
                            : L("Mode automatique : détection ok (port " + detected + ") mais connexion échouée.", "Automatic mode: detection ok (port " + detected + ") but connection failed."),
                        ok ? ToolTipIcon.Info : ToolTipIcon.Warning, 3000);
                }
                else
                {
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
                    if (dlg.ShowDialog() == DialogResult.OK)
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
        private readonly ListBox _list;
        private readonly TextBox _nameBox;
        private readonly NumericUpDown _portBox;
        private readonly Button _addButton;
        private readonly Button _removeButton;
        private List<DeclaredTopSolidInstance> _instances;

        internal ManageInstancesDialog(TrayIcon owner)
        {
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
                    _instances.Add(new DeclaredTopSolidInstance("TopSolid " + currentPort, currentPort) { IsDefault = true });
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
                    "Déclarez vos instances TopSolid (nom + port Automation).\nLa coche « par défaut » sera ciblée au démarrage du serveur.",
                    "Declare your TopSolid instances (name + Automation port).\nThe \"default\" one is targeted at server startup."),
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

            // "Par défaut" — marks the selected instance as the startup default.
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
            _instances.Add(new DeclaredTopSolidInstance(name, (int)_portBox.Value));
            TraySettings.SaveDeclaredInstances(_instances);
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

        /// <summary>Marks the selected instance as the startup default (exactly one).</summary>
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

            // The default is also the port the server targets: keep settings.json's
            // "port" field in sync so Program.cs picks it at next startup.
            TraySettings.SaveInstanceMode(_instances[idx].Port, "manual");
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