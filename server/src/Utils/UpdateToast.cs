using System;
using System.Drawing;
using System.Windows.Forms;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// "New release" popup in the bottom-right corner of the screen. Unlike a balloon tip, which
    /// Windows hides after a few seconds or swallows in focus mode, it stays until the user picks:
    /// download and install, read the release notes, or later. It never takes the keyboard focus.
    /// </summary>
    internal sealed class UpdateToast : Form
    {
        private const int Margin16 = 16;

        public UpdateToast(string currentVersion, string newVersion, Action onInstall, Action onNotes)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            ClientSize = new Size(380, 148);
            Padding = new Padding(1);

            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(203, 213, 225), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                using (var accent = new SolidBrush(Color.FromArgb(37, 99, 235)))
                    e.Graphics.FillRectangle(accent, 0, 0, 4, ClientSize.Height);
            };

            var app = new Label
            {
                Text = "TopSolid MCP",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(20, 12),
                AutoSize = true
            };
            Controls.Add(app);

            var close = new Label
            {
                Text = "✕",
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(ClientSize.Width - 28, 8),
                Size = new Size(20, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            close.Click += (s, e) => Close();
            Controls.Add(close);

            var title = new Label
            {
                Text = L10n.T("tray.toast.title", newVersion),
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(20, 32),
                AutoSize = true
            };
            Controls.Add(title);

            var body = new Label
            {
                Text = L10n.T("tray.toast.body", currentVersion),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(20, 60),
                Size = new Size(ClientSize.Width - 40, 36)
            };
            Controls.Add(body);

            var install = TrayIcon.CreatePrimaryButton(L10n.T("tray.toast.install"), 20, 102, 168, 32);
            install.Click += (s, e) => { Close(); Safe(onInstall); };
            Controls.Add(install);

            var notes = TrayIcon.CreateSecondaryButton(L10n.T("tray.toast.notes"), 196, 102, 96, 32);
            notes.Click += (s, e) => Safe(onNotes);
            Controls.Add(notes);

            var later = TrayIcon.CreateSecondaryButton(L10n.T("tray.toast.later"), 298, 102, 66, 32);
            later.Click += (s, e) => Close();
            Controls.Add(later);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x80;
                const int WS_EX_TOPMOST = 0x8;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - Margin16, area.Bottom - Height - Margin16);
        }

        private static void Safe(Action action)
        {
            try { action?.Invoke(); } catch { }
        }
    }
}
