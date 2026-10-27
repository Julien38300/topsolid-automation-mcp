using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace TopSolidMcpServer.Utils
{
    // ─────────────────────────────────────────────────────────────────────
    // TrayIcon partial — Direct Level-1 "Instance TopSolid" submenu
    // and dedicated "Gérer les instances TopSolid" dialog (zero MCP clutter).
    // ─────────────────────────────────────────────────────────────────────
    public partial class TrayIcon
    {
        private Func<int, bool> _retargetAction;
        private Func<int, bool> _httpRestartAction;
        private int _httpPort = 8080;
        private ToolStripMenuItem _instancePickerMenuItem;
        private ManageInstancesDialog _manageDialog;

        /// <summary>
        /// Sets the callback invoked when the user picks a TopSolid instance in the
        /// tray menu: disconnects and reconnects the WCF channel on the given port,
        /// returning <c>true</c> if the connection succeeded.
        /// </summary>
        public void SetRetargetAction(Func<int, bool> onRetarget)
        {
            _retargetAction = onRetarget;
        }

        /// <summary>
        /// Sets the current HTTP port displayed in the tray status and MCP Server Settings dialog.
        /// </summary>
        public void SetHttpPort(int httpPort)
        {
            if (httpPort > 0 && httpPort <= 65535)
                _httpPort = httpPort;
            UpdateStatusDisplay();
        }

        /// <summary>
        /// Sets the callback invoked when the user clicks "Appliquer" on the MCP
        /// HTTP port in the MCP Server Settings dialog: stops the running HTTP listener
        /// and restarts it on the new port, returning <c>true</c> on success.
        /// </summary>
        public void SetHttpRestartAction(Func<int, bool> onRestartHttp)
        {
            _httpRestartAction = onRestartHttp;
        }

        /// <summary>
        /// Adds the "Instance TopSolid" submenu directly at Level 1 of the root
        /// ContextMenuStrip (maximum 2 menu levels total).
        /// </summary>
        private void BuildInstancePickerMenu(ContextMenuStrip rootMenu)
        {
            TraySettings.Load();

            _instancePickerMenuItem = new ToolStripMenuItem("Instance TopSolid");
            StyleMenuItem(_instancePickerMenuItem);
            _instancePickerMenuItem.DropDown.Font = rootMenu.Font;
            _instancePickerMenuItem.DropDown.BackColor = Color.White;
            _instancePickerMenuItem.DropDown.Padding = new Padding(4, 4, 4, 4);
            if (_instancePickerMenuItem.DropDown is ToolStripDropDownMenu dropDown)
                dropDown.ShowImageMargin = false;

            // Seed with a placeholder so the submenu arrow is always drawn;
            // the real list is rebuilt every time the user opens the submenu.
            _instancePickerMenuItem.DropDownItems.Add(new ToolStripMenuItem("(chargement…)") { Enabled = false });
            _instancePickerMenuItem.DropDownOpening += (s, e) => PopulateInstanceSubmenu();

            rootMenu.Items.Add(_instancePickerMenuItem);
        }

        private void PopulateInstanceSubmenu()
        {
            try
            {
                if (_instancePickerMenuItem == null) return;
                var items = _instancePickerMenuItem.DropDownItems;
                items.Clear();

                TraySettings.Load();
                string mode = TraySettings.InstanceMode;
                var declared = TraySettings.DeclaredInstances ?? new List<DeclaredTopSolidInstance>();
                List<TopSolidInstance> running = TopSolidInstanceList.Scan();

                // ── Section 1: Running TopSolid instances (live scan) ──
                if (running.Count == 0)
                {
                    var noneItem = new ToolStripMenuItem("(aucun TopSolid en cours d'exécution)")
                    {
                        Enabled = false
                    };
                    StyleMenuItem(noneItem);
                    items.Add(noneItem);
                }
                else
                {
                    foreach (var inst in running)
                    {
                        var captured = inst;
                        bool isCurrent = _connected && captured.HasAutomationPort && captured.Port == _port;
                        string prefix = isCurrent ? "\u2713  " : "     ";
                        string verText = !string.IsNullOrEmpty(captured.Version) ? " " + captured.Version : "";
                        string portText = captured.HasAutomationPort
                            ? $" (port {captured.Port})"
                            : " (Automation inactif)";
                        var item = new ToolStripMenuItem($"{prefix}TopSolid{verText}{portText}")
                        {
                            Enabled = captured.HasAutomationPort
                        };
                        StyleMenuItem(item);
                        if (isCurrent)
                            item.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);

                        if (captured.HasAutomationPort)
                        {
                            item.Click += (s, e) => SelectInstancePort(captured.Port, manual: true);
                        }
                        items.Add(item);
                    }
                }

                // ── Section 2: User-declared instances (settings.json) ──
                if (declared.Count > 0)
                {
                    items.Add(new ToolStripSeparator());
                    foreach (var decl in declared)
                    {
                        var captured = decl;
                        var match = TopSolidInstanceList.FindByPort(running, captured.Port);
                        bool isCurrent = _connected && captured.Port == _port;
                        string stateTag = match != null ? " [actif]" : " [non démarré]";
                        string defaultStar = captured.IsDefault ? " \u2605" : "";
                        string prefix = isCurrent ? "\u2713  " : "     ";
                        string label = $"{prefix}{captured.Name} (port {captured.Port}){defaultStar}{stateTag}";

                        var item = new ToolStripMenuItem(label);
                        StyleMenuItem(item);
                        if (isCurrent)
                            item.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);

                        item.Click += (s, e) => SelectInstancePort(captured.Port, manual: true);
                        items.Add(item);
                    }
                }

                // ── Section 3: Automatic mode + Manage TopSolid Instances dialog ──
                items.Add(new ToolStripSeparator());

                bool isAuto = !string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase);
                string autoPrefix = isAuto ? "\u2713  " : "     ";
                var autoItem = new ToolStripMenuItem(autoPrefix + "Automatique (premier TopSolid détecté)");
                StyleMenuItem(autoItem);
                if (isAuto)
                    autoItem.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
                autoItem.Click += (s, e) => SelectAutomaticMode(running);
                items.Add(autoItem);

                items.Add(new ToolStripSeparator());

                var manageItem = new ToolStripMenuItem("Gérer les instances TopSolid…");
                StyleMenuItem(manageItem);
                manageItem.Click += (s, e) => ShowManageInstancesDialog();
                items.Add(manageItem);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] PopulateInstanceSubmenu failed: " + ex.Message);
            }
        }

        private void SelectInstancePort(int targetPort, bool manual)
        {
            if (targetPort <= 0) return;

            TraySettings.SaveInstanceMode(targetPort, manual ? "manual" : "auto");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    bool ok = _retargetAction != null && _retargetAction(targetPort);
                    if (ok)
                    {
                        ShowBalloon(
                            "Instance TopSolid sélectionnée",
                            $"Connecté à TopSolid sur le port {targetPort}.",
                            ToolTipIcon.Info);
                    }
                    else
                    {
                        ShowBalloon(
                            "TopSolid non disponible",
                            $"Port {targetPort} enregistré, mais aucun TopSolid ne répond sur ce port.",
                            ToolTipIcon.Warning);
                    }
                }
                catch { }
            });
        }

        private void SelectAutomaticMode(List<TopSolidInstance> runningSnapshot)
        {
            int targetPort = 0;
            if (runningSnapshot != null)
            {
                foreach (var r in runningSnapshot)
                {
                    if (r.HasAutomationPort) { targetPort = r.Port; break; }
                }
            }
            if (targetPort == 0)
            {
                TraySettings.Load();
                foreach (var d in TraySettings.DeclaredInstances)
                {
                    if (d.IsDefault && d.Port > 0) { targetPort = d.Port; break; }
                }
            }
            if (targetPort == 0) targetPort = _port > 0 ? _port : 8090;

            TraySettings.SaveInstanceMode(0, "auto");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    bool ok = _retargetAction != null && _retargetAction(targetPort);
                    ShowBalloon(
                        "Mode automatique activé",
                        ok
                            ? $"Connecté à TopSolid (port {targetPort})."
                            : $"Aucun TopSolid actif sur le port {targetPort} — reconnexion automatique en attente.",
                        ok ? ToolTipIcon.Info : ToolTipIcon.Warning);
                }
                catch { }
            });
        }

        internal void ShowManageInstancesDialog()
        {
            try
            {
                if (_manageDialog != null && !_manageDialog.IsDisposed)
                {
                    _manageDialog.BringToFront();
                    _manageDialog.Activate();
                    return;
                }

                TraySettings.Load();
                _manageDialog = new ManageInstancesDialog(
                    TraySettings.DeclaredInstances,
                    updatedList =>
                    {
                        TraySettings.SaveDeclaredInstances(updatedList);
                        foreach (var d in updatedList)
                        {
                            if (d.IsDefault && d.Port > 0)
                            {
                                SelectInstancePort(d.Port, manual: true);
                                break;
                            }
                        }
                    });
                _manageDialog.Show();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] ManageInstancesDialog failed: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ManageInstancesDialog — 100% Dedicated to TopSolid CAD Instances
        // ─────────────────────────────────────────────────────────────────

        internal sealed class ManageInstancesDialog : Form
        {
            private readonly List<DeclaredTopSolidInstance> _items;
            private readonly Action<List<DeclaredTopSolidInstance>> _onSave;

            private readonly ListBox _listBox;
            private readonly TextBox _nameBox;
            private readonly NumericUpDown _portBox;
            private readonly Button _addOrSaveButton;
            private readonly Button _cancelEditButton;
            private readonly Label _editorHeaderLabel;
            private readonly Button _upButton;
            private readonly Button _downButton;
            private readonly Button _editButton;
            private readonly Button _removeButton;
            private readonly Button _defaultButton;

            private int _editingIndex = -1;

            public ManageInstancesDialog(
                List<DeclaredTopSolidInstance> initial,
                Action<List<DeclaredTopSolidInstance>> onSave)
            {
                _items = new List<DeclaredTopSolidInstance>();
                if (initial != null)
                {
                    foreach (var d in initial)
                        _items.Add(new DeclaredTopSolidInstance(d.Name, d.Port) { IsDefault = d.IsDefault });
                }
                _onSave = onSave;

                Text = "Gérer les instances TopSolid";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(560, 486);
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
                    Text = "Instances TopSolid déclarées",
                    Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Location = new Point(22, 12),
                    AutoSize = true
                };
                var subtitleLabel = new Label
                {
                    Text = "Définissez l'ordre d'affichage de vos sessions CAO et le port Automation par défaut",
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(22, 37),
                    AutoSize = true
                };
                banner.Controls.Add(titleLabel);
                banner.Controls.Add(subtitleLabel);
                Controls.Add(banner);

                // ── Card 1 : Liste ordonnée des instances ──
                var listCard = CreateCard(20, 82, 520, 226, "LISTE ORDONNÉE DES INSTANCES");

                var listBorder = CreateInputContainer(16, 36, 362, 174, Color.White);
                _listBox = new ListBox
                {
                    BorderStyle = BorderStyle.None,
                    Location = new Point(2, 2),
                    Size = new Size(358, 170),
                    DrawMode = DrawMode.OwnerDrawFixed,
                    ItemHeight = 34,
                    IntegralHeight = false,
                    BackColor = Color.White,
                    Font = new Font("Segoe UI", 9.25f, FontStyle.Regular)
                };
                _listBox.DrawItem += OnDrawInstanceItem;
                _listBox.Paint += OnListBoxPaintEmptyState;
                _listBox.SelectedIndexChanged += (s, e) => UpdateActionButtonsState();
                _listBox.DoubleClick += OnBeginEdit;
                listBorder.Controls.Add(_listBox);
                listCard.Controls.Add(listBorder);

                // Right-side clean vertical button stack (with readable labels + symbols)
                _upButton = CreateSecondaryButton("\u25B2  Monter", 390, 36, 114, 31);
                _upButton.Click += OnMoveUp;
                listCard.Controls.Add(_upButton);

                _downButton = CreateSecondaryButton("\u25BC  Descendre", 390, 72, 114, 31);
                _downButton.Click += OnMoveDown;
                listCard.Controls.Add(_downButton);

                _defaultButton = CreateSecondaryButton("\u2605  Par défaut", 390, 108, 114, 31);
                _defaultButton.Click += OnSetDefault;
                listCard.Controls.Add(_defaultButton);

                _editButton = CreateSecondaryButton("\u270E  Modifier", 390, 144, 114, 31);
                _editButton.Click += OnBeginEdit;
                listCard.Controls.Add(_editButton);

                _removeButton = CreateDangerButton("\u2715  Supprimer", 390, 179, 114, 31);
                _removeButton.Click += OnRemove;
                listCard.Controls.Add(_removeButton);

                Controls.Add(listCard);

                // ── Card 2 : Ajouter / Modifier une instance ──
                var editCard = CreateCard(20, 322, 520, 106, null);

                _editorHeaderLabel = new Label
                {
                    Text = "AJOUTER UNE INSTANCE TOPSOLID",
                    Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(16, 12),
                    AutoSize = true
                };
                editCard.Controls.Add(_editorHeaderLabel);

                var nameLbl = new Label
                {
                    Text = "Nom de l'instance",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    Location = new Point(16, 36),
                    AutoSize = true
                };
                editCard.Controls.Add(nameLbl);

                var nameContainer = CreateInputContainer(16, 56, 204, 32, Color.White);
                _nameBox = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Location = new Point(8, 6),
                    Width = 188
                };
                nameContainer.Controls.Add(_nameBox);
                editCard.Controls.Add(nameContainer);

                var portLbl = new Label
                {
                    Text = "Port Automation",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    Location = new Point(230, 36),
                    AutoSize = true
                };
                editCard.Controls.Add(portLbl);

                var portContainer = CreateInputContainer(230, 56, 94, 32, Color.White);
                _portBox = new NumericUpDown
                {
                    BorderStyle = BorderStyle.None,
                    Minimum = 1,
                    Maximum = 65535,
                    Value = 8090,
                    Font = new Font("Consolas", 10f, FontStyle.Regular),
                    Location = new Point(8, 6),
                    Width = 80
                };
                portContainer.Controls.Add(_portBox);
                editCard.Controls.Add(portContainer);

                _addOrSaveButton = CreatePrimaryButton("Ajouter", 334, 56, 96, 32);
                _addOrSaveButton.Click += OnAddOrSave;
                editCard.Controls.Add(_addOrSaveButton);

                _cancelEditButton = CreateSecondaryButton("Annuler", 436, 56, 68, 32);
                _cancelEditButton.Visible = false;
                _cancelEditButton.Click += (s, e) => ExitEditMode();
                editCard.Controls.Add(_cancelEditButton);

                Controls.Add(editCard);

                // ── Footer ──
                var hintLabel = new Label
                {
                    Text = "Les changements sont enregistrés automatiquement.",
                    Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(20, 446),
                    AutoSize = true
                };
                Controls.Add(hintLabel);

                var closeButton = CreateSecondaryButton("Fermer", 440, 440, 100, 32);
                closeButton.Click += (s, e) => Close();
                Controls.Add(closeButton);
                CancelButton = closeButton;

                RefreshList(_items.Count > 0 ? 0 : -1);
            }

            private void OnListBoxPaintEmptyState(object sender, PaintEventArgs e)
            {
                if (_items.Count > 0) return;
                using (var brush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                using (var font = new Font("Segoe UI", 9f, FontStyle.Regular))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString(
                        "Aucune instance déclarée (détection automatique active)",
                        font,
                        brush,
                        new RectangleF(0, 0, _listBox.Width, _listBox.Height),
                        sf);
                }
            }

            private void OnDrawInstanceItem(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= _items.Count) return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = e.Bounds;
                bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                var item = _items[e.Index];

                Color bgColor = selected ? Color.FromArgb(239, 246, 255) : Color.White;
                using (var bgBrush = new SolidBrush(bgColor))
                    g.FillRectangle(bgBrush, bounds);

                if (selected)
                {
                    using (var accentBrush = new SolidBrush(Color.FromArgb(37, 99, 235)))
                        g.FillRectangle(accentBrush, bounds.Left, bounds.Top, 3, bounds.Height);
                }

                using (var sepPen = new Pen(Color.FromArgb(241, 245, 249), 1))
                    g.DrawLine(sepPen, bounds.Left + 8, bounds.Bottom - 1, bounds.Right - 8, bounds.Bottom - 1);

                // Index number
                using (var idxFont = new Font("Segoe UI", 8.75f, FontStyle.Regular))
                using (var idxBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                {
                    g.DrawString($"{e.Index + 1}.", idxFont, idxBrush, bounds.Left + 10, bounds.Top + 8);
                }

                // Instance Name
                using (var nameFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
                using (var nameBrush = new SolidBrush(selected ? Color.FromArgb(29, 78, 216) : Color.FromArgb(15, 23, 42)))
                {
                    g.DrawString(item.Name, nameFont, nameBrush, bounds.Left + 32, bounds.Top + 7);
                    SizeF nameSize = g.MeasureString(item.Name, nameFont);

                    // Port badge
                    using (var portFont = new Font("Consolas", 9f, FontStyle.Regular))
                    using (var portBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
                    {
                        float portX = Math.Min(bounds.Left + 38 + nameSize.Width, bounds.Right - 170);
                        g.DrawString($"port {item.Port}", portFont, portBrush, portX, bounds.Top + 9);
                    }
                }

                // Default badge pill on the right
                if (item.IsDefault)
                {
                    var badgeRect = new Rectangle(bounds.Right - 96, bounds.Top + 7, 86, 20);
                    using (var badgeFill = new SolidBrush(Color.FromArgb(219, 234, 254)))
                        g.FillRectangle(badgeFill, badgeRect);
                    using (var badgeBorder = new Pen(Color.FromArgb(147, 197, 253), 1))
                        g.DrawRectangle(badgeBorder, badgeRect);
                    using (var badgeFont = new Font("Segoe UI Semibold", 7.75f, FontStyle.Bold))
                    using (var badgeText = new SolidBrush(Color.FromArgb(29, 78, 216)))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString("\u2605 PAR DÉFAUT", badgeFont, badgeText, badgeRect, sf);
                    }
                }
            }

            private void RefreshList(int selectIndex)
            {
                _listBox.BeginUpdate();
                _listBox.Items.Clear();
                for (int i = 0; i < _items.Count; i++)
                {
                    var d = _items[i];
                    _listBox.Items.Add($"{i + 1}. {d.Name} (port {d.Port})");
                }
                if (selectIndex >= 0 && selectIndex < _items.Count)
                    _listBox.SelectedIndex = selectIndex;
                _listBox.EndUpdate();
                _listBox.Invalidate();
                UpdateActionButtonsState();
            }

            private void UpdateActionButtonsState()
            {
                int idx = _listBox.SelectedIndex;
                bool hasSel = idx >= 0 && idx < _items.Count;
                _upButton.Enabled = hasSel && idx > 0;
                _downButton.Enabled = hasSel && idx < _items.Count - 1;
                _editButton.Enabled = hasSel;
                _removeButton.Enabled = hasSel;
                _defaultButton.Enabled = hasSel;
            }

            private void Persist()
            {
                try { _onSave?.Invoke(_items); } catch { }
            }

            private void OnAddOrSave(object sender, EventArgs e)
            {
                string name = (_nameBox.Text ?? "").Trim();
                int port = (int)_portBox.Value;
                if (string.IsNullOrEmpty(name))
                    name = $"TopSolid ({port})";

                if (_editingIndex >= 0 && _editingIndex < _items.Count)
                {
                    bool wasDefault = _items[_editingIndex].IsDefault;
                    _items[_editingIndex] = new DeclaredTopSolidInstance(name, port) { IsDefault = wasDefault };
                    int editedIdx = _editingIndex;
                    ExitEditMode();
                    RefreshList(editedIdx);
                    Persist();
                    return;
                }

                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Port == port)
                    {
                        bool wasDefault = _items[i].IsDefault;
                        _items[i] = new DeclaredTopSolidInstance(name, port) { IsDefault = wasDefault };
                        _nameBox.Text = "";
                        RefreshList(i);
                        Persist();
                        return;
                    }
                }

                bool makeDefault = _items.Count == 0;
                _items.Add(new DeclaredTopSolidInstance(name, port) { IsDefault = makeDefault });
                _nameBox.Text = "";
                _portBox.Value = Math.Min(65535, port + 1);
                RefreshList(_items.Count - 1);
                Persist();
            }

            private void OnBeginEdit(object sender, EventArgs e)
            {
                int idx = _listBox.SelectedIndex;
                if (idx < 0 || idx >= _items.Count) return;

                _editingIndex = idx;
                _nameBox.Text = _items[idx].Name;
                int p = _items[idx].Port;
                if (p < _portBox.Minimum) p = (int)_portBox.Minimum;
                if (p > _portBox.Maximum) p = (int)_portBox.Maximum;
                _portBox.Value = p;

                _editorHeaderLabel.Text = $"MODIFIER L'INSTANCE #{idx + 1}";
                _editorHeaderLabel.ForeColor = Color.FromArgb(37, 99, 235);
                _addOrSaveButton.Text = "Enregistrer";
                _cancelEditButton.Visible = true;
                _nameBox.Focus();
                _nameBox.SelectAll();
            }

            private void ExitEditMode()
            {
                _editingIndex = -1;
                _nameBox.Text = "";
                _editorHeaderLabel.Text = "AJOUTER UNE INSTANCE TOPSOLID";
                _editorHeaderLabel.ForeColor = Color.FromArgb(100, 116, 139);
                _addOrSaveButton.Text = "Ajouter";
                _cancelEditButton.Visible = false;
            }

            private void OnRemove(object sender, EventArgs e)
            {
                int idx = _listBox.SelectedIndex;
                if (idx < 0 || idx >= _items.Count) return;
                ExitEditMode();
                _items.RemoveAt(idx);
                RefreshList(Math.Min(idx, _items.Count - 1));
                Persist();
            }

            private void OnMoveUp(object sender, EventArgs e)
            {
                int idx = _listBox.SelectedIndex;
                if (idx <= 0 || idx >= _items.Count) return;
                ExitEditMode();
                var tmp = _items[idx - 1];
                _items[idx - 1] = _items[idx];
                _items[idx] = tmp;
                RefreshList(idx - 1);
                Persist();
            }

            private void OnMoveDown(object sender, EventArgs e)
            {
                int idx = _listBox.SelectedIndex;
                if (idx < 0 || idx >= _items.Count - 1) return;
                ExitEditMode();
                var tmp = _items[idx + 1];
                _items[idx + 1] = _items[idx];
                _items[idx] = tmp;
                RefreshList(idx + 1);
                Persist();
            }

            private void OnSetDefault(object sender, EventArgs e)
            {
                int idx = _listBox.SelectedIndex;
                if (idx < 0 || idx >= _items.Count) return;
                bool willBeDefault = !_items[idx].IsDefault;
                for (int i = 0; i < _items.Count; i++)
                    _items[i].IsDefault = (i == idx) && willBeDefault;
                RefreshList(idx);
                Persist();
            }
        }
    }
}
