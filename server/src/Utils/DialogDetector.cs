using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Finds the dialog boxes open in TopSolid and reads their text, through Win32 and UI
    /// Automation only: it never calls the TopSolid Automation API, so it still answers while
    /// a modal dialog blocks TopSolid and every bridge call times out (field report 2026-11-08).
    /// </summary>
    public static class DialogDetector
    {
        private const uint GW_OWNER = 4;
        private const uint WM_GETTEXT = 0x000D;
        private const uint WM_GETTEXTLENGTH = 0x000E;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint TextTimeoutMs = 300;
        private const int UiAutomationTimeoutMs = 4000;
        private const int MaxChildControls = 200;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageTimeoutW")]
        private static extern IntPtr SendMessageTimeoutText(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam,
            uint flags, uint timeout, out IntPtr result);

        /// <summary>
        /// Describes the dialog boxes open in TopSolid.
        /// </summary>
        /// <param name="processId">TopSolid process to inspect; 0 inspects every TopSolid process.</param>
        public static string Describe(int processId = 0)
        {
            int[] pids = processId > 0
                ? new[] { processId }
                : Process.GetProcessesByName("TopSolid").Select(p => p.Id).ToArray();

            var windows = Snapshot(pids);
            var dialogs = DialogReport.SelectDialogs(windows);
            foreach (var dialog in dialogs)
                ReadTexts(dialog);

            return DialogReport.Format(dialogs, windows, pids.Length);
        }

        /// <summary>
        /// Short text for a timeout message: the dialogs found, or null when there is none.
        /// </summary>
        public static string DescribeIfAny()
        {
            try
            {
                int[] pids = Process.GetProcessesByName("TopSolid").Select(p => p.Id).ToArray();
                var windows = Snapshot(pids);
                var dialogs = DialogReport.SelectDialogs(windows);
                if (dialogs.Count == 0) return null;
                foreach (var dialog in dialogs)
                    ReadTexts(dialog);
                return DialogReport.Format(dialogs, windows, pids.Length);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] Dialog detection failed: " + ex.Message);
                return null;
            }
        }

        private static List<WindowInfo> Snapshot(ICollection<int> pids)
        {
            var result = new List<WindowInfo>();
            if (pids.Count == 0) return result;

            var wanted = new HashSet<int>(pids);
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (!wanted.Contains((int)pid)) return true;

                IntPtr owner = GetWindow(hWnd, GW_OWNER);
                result.Add(new WindowInfo
                {
                    Handle = hWnd,
                    ProcessId = (int)pid,
                    Title = GetText(hWnd),
                    ClassName = GetClass(hWnd),
                    IsVisible = IsWindowVisible(hWnd),
                    IsEnabled = IsWindowEnabled(hWnd),
                    Owner = owner,
                    OwnerEnabled = owner == IntPtr.Zero || IsWindowEnabled(owner)
                });
                return true;
            }, IntPtr.Zero);

            return result;
        }

        /// <summary>
        /// Fills the texts and buttons of a dialog: Win32 child controls first (MessageBox,
        /// WinForms), then UI Automation for WPF windows, which have no child HWND.
        /// </summary>
        private static void ReadTexts(WindowInfo dialog)
        {
            int seen = 0;
            EnumChildWindows(dialog.Handle, delegate(IntPtr child, IntPtr lParam)
            {
                if (++seen > MaxChildControls) return false;
                if (!IsWindowVisible(child)) return true;

                string cls = GetClass(child);
                string text = GetText(child);
                if (string.IsNullOrWhiteSpace(text)) return true;

                if (cls.IndexOf("BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
                    dialog.Buttons.Add(text);
                else if (cls.IndexOf("STATIC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         cls.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         cls.IndexOf("LABEL", StringComparison.OrdinalIgnoreCase) >= 0)
                    dialog.Texts.Add(text);
                return true;
            }, IntPtr.Zero);

            if (dialog.Texts.Count == 0 && dialog.Buttons.Count == 0)
                ReadWithUiAutomation(dialog);
        }

        /// <summary>
        /// UI Automation can hang on a frozen window: it runs on its own thread and is
        /// abandoned after <see cref="UiAutomationTimeoutMs"/>.
        /// </summary>
        private static void ReadWithUiAutomation(WindowInfo dialog)
        {
            var texts = new List<string>();
            var buttons = new List<string>();
            var worker = new Thread(delegate()
            {
                try
                {
                    var root = AutomationElement.FromHandle(dialog.Handle);
                    var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    int count = 0;
                    foreach (AutomationElement element in all)
                    {
                        if (++count > MaxChildControls) break;
                        var info = element.Current;
                        if (string.IsNullOrWhiteSpace(info.Name)) continue;
                        if (info.ControlType == ControlType.Button)
                        {
                            // Minimize / maximize / close live under the title bar: not answers.
                            var parent = TreeWalker.ControlViewWalker.GetParent(element);
                            if (parent == null || parent.Current.ControlType != ControlType.TitleBar)
                                lock (buttons) buttons.Add(info.Name);
                        }
                        else if (info.ControlType == ControlType.Text || info.ControlType == ControlType.Document ||
                                 info.ControlType == ControlType.Edit)
                            lock (texts) texts.Add(info.Name);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[MCP-WARN] UI Automation could not read dialog \"" + dialog.Title + "\": " + ex.Message);
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
            worker.Join(UiAutomationTimeoutMs);

            lock (texts) dialog.Texts.AddRange(texts);
            lock (buttons) dialog.Buttons.AddRange(buttons);
        }

        /// <summary>
        /// WM_GETTEXT with a timeout: a plain GetWindowText on a control of a hung process blocks.
        /// </summary>
        private static string GetText(IntPtr hWnd)
        {
            IntPtr length;
            if (SendMessageTimeout(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, TextTimeoutMs, out length) == IntPtr.Zero)
                return "";

            int size = Math.Min(length.ToInt32(), 4096);
            if (size <= 0) return "";

            var sb = new StringBuilder(size + 1);
            IntPtr copied;
            if (SendMessageTimeoutText(hWnd, WM_GETTEXT, new IntPtr(size + 1), sb, SMTO_ABORTIFHUNG, TextTimeoutMs, out copied) == IntPtr.Zero)
                return "";
            return sb.ToString();
        }

        private static string GetClass(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
