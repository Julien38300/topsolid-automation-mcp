using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Pure logic of the dialog detector: which windows are blocking dialogs, and how to report
    /// them. Win32 enumeration lives in <see cref="DialogDetector"/>.
    /// </summary>
    public static class DialogReport
    {
        /// <summary>Standard Win32 dialog class (MessageBox, common dialogs).</summary>
        public const string Win32DialogClass = "#32770";

        private const int MaxLines = 20;
        private const int MaxLineChars = 300;

        private static readonly string[] IgnoredClasses =
        {
            "tooltips_class32", "#32768", "IME", "MSCTFIME UI", "SysShadow", "Ghost"
        };

        /// <summary>
        /// Keeps the windows that block TopSolid: a visible, enabled window whose owner is
        /// disabled (that is how Windows implements a modal dialog), or an unowned window while
        /// the main window of the same process is disabled, plus any standard Win32 dialog.
        /// Floating tool panes (owned but not modal) are left out. Modal windows come first.
        /// </summary>
        public static List<WindowInfo> SelectDialogs(IEnumerable<WindowInfo> windows)
        {
            var visible = (windows ?? Enumerable.Empty<WindowInfo>())
                .Where(w => w != null && w.IsVisible && !IsIgnored(w.ClassName))
                .ToList();

            var pidsWithDisabledMain = new HashSet<int>(visible
                .Where(w => w.Owner == IntPtr.Zero && !w.IsEnabled)
                .Select(w => w.ProcessId));

            return visible
                .Where(w => IsModal(w, pidsWithDisabledMain) || w.ClassName == Win32DialogClass)
                .OrderByDescending(w => IsModal(w, pidsWithDisabledMain))
                .ToList();
        }

        /// <summary>True when the window blocks the rest of its process.</summary>
        public static bool IsModal(WindowInfo w, ICollection<int> pidsWithDisabledMain)
        {
            if (w == null || !w.IsEnabled) return false;
            if (w.Owner != IntPtr.Zero) return !w.OwnerEnabled;
            return pidsWithDisabledMain != null && pidsWithDisabledMain.Contains(w.ProcessId);
        }

        /// <summary>True when one of the scanned processes has its main window disabled.</summary>
        public static bool AnyMainWindowDisabled(IEnumerable<WindowInfo> windows)
        {
            return (windows ?? Enumerable.Empty<WindowInfo>())
                .Any(w => w != null && w.IsVisible && w.Owner == IntPtr.Zero && !w.IsEnabled && !IsIgnored(w.ClassName));
        }

        /// <summary>
        /// Builds the text returned to the caller.
        /// </summary>
        /// <param name="dialogs">Result of <see cref="SelectDialogs"/>, texts filled in.</param>
        /// <param name="allWindows">Every window scanned, to explain an empty result.</param>
        /// <param name="processCount">Number of TopSolid processes scanned.</param>
        public static string Format(IList<WindowInfo> dialogs, IList<WindowInfo> allWindows, int processCount)
        {
            if (processCount == 0)
                return "No TopSolid process is running on this machine.";

            int visibleCount = (allWindows ?? new List<WindowInfo>()).Count(w => w != null && w.IsVisible);
            if (visibleCount == 0)
                return "TopSolid is running (" + processCount + " process(es)) but none of its windows is visible from the " +
                    "MCP server. The server probably runs in another Windows session (service or scheduled task): " +
                    "ask the user to look at the TopSolid screen.";

            if (dialogs == null || dialogs.Count == 0)
            {
                if (AnyMainWindowDisabled(allWindows))
                    return "The TopSolid main window is disabled (a modal window is open) but the dialog itself could not be " +
                        "found or read. Ask the user to look at the TopSolid screen and close it.";
                return "No dialog box is open in TopSolid (" + processCount + " process(es), " + visibleCount + " visible window(s) scanned).";
            }

            var pidsWithDisabledMain = new HashSet<int>((allWindows ?? new List<WindowInfo>())
                .Where(w => w != null && w.IsVisible && w.Owner == IntPtr.Zero && !w.IsEnabled)
                .Select(w => w.ProcessId));

            var sb = new StringBuilder();
            sb.AppendLine(dialogs.Count + " dialog box(es) open in TopSolid:");
            foreach (var d in dialogs)
            {
                bool modal = IsModal(d, pidsWithDisabledMain);
                sb.AppendLine();
                sb.AppendLine((modal ? "[MODAL - TopSolid is blocked until it is closed] " : "[dialog] ") +
                    "\"" + Clean(d.Title) + "\" (process " + d.ProcessId + ")");

                // Win32 button captions carry '&' accelerator markers ("&Yes").
                var buttons = d.Buttons
                    .Select(b => Clean((b ?? "").Replace("&&", "\u0001").Replace("&", "").Replace('\u0001', '&')))
                    .Where(b => b.Length > 0).Distinct().ToList();

                // A WPF button caption is also exposed as a text element: list it once, as a button.
                var lines = d.Texts
                    .Select(Clean)
                    .Where(t => t.Length > 0 && !string.Equals(t, Clean(d.Title), StringComparison.Ordinal) && !buttons.Contains(t))
                    .Distinct()
                    .Take(MaxLines)
                    .ToList();
                foreach (string line in lines)
                    sb.AppendLine("  " + line);
                if (lines.Count == 0)
                    sb.AppendLine("  (no readable text)");
                if (buttons.Count > 0)
                    sb.AppendLine("  Buttons: " + string.Join(" ", buttons.Select(b => "[" + b + "]")));
            }

            sb.AppendLine();
            sb.Append("TopSolid calls will wait or time out while a modal dialog is open. " +
                "Report the text above to the user and ask them to close it in TopSolid before retrying.");
            return sb.ToString();
        }

        private static bool IsIgnored(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            return IgnoredClasses.Any(c => string.Equals(c, className, StringComparison.OrdinalIgnoreCase));
        }

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string oneLine = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            return oneLine.Length <= MaxLineChars ? oneLine : oneLine.Substring(0, MaxLineChars) + "...";
        }
    }
}
