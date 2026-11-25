using System;
using System.Collections.Generic;
using System.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Pure logic of the dialog watch (field report: "when TopSolid shows an error window, it is not
    /// reported through the API and the process stays blocked until someone closes it"). Win32 lives in
    /// <see cref="DialogDetector"/>; this class decides and is unit-tested.
    /// </summary>
    public static class DialogWatch
    {
        public const string PolicyEnvVar = "TOPSOLID_MCP_DIALOG_POLICY";
        public const string GraceEnvVar = "TOPSOLID_MCP_DIALOG_GRACE_MS";

        /// <summary>A dialog must stay open this long before it is acted on: short-lived windows are left alone.</summary>
        public const int DefaultGraceMs = 1500;

        /// <summary>Auto-clicks allowed in one script, so that a loop of messages ends up reported.</summary>
        public const int MaxAutoClicksPerScript = 5;

        /// <summary>Captions of the buttons that cancel or close without saying yes to anything.</summary>
        private static readonly HashSet<string> CancelCaptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cancel", "Annuler", "Close", "Fermer", "Abbrechen", "Schließen", "Annulla", "Chiudi", "Cancelar", "Cerrar"
        };

        /// <summary>Captions that stop an operation in progress: never pressed automatically.</summary>
        private static readonly HashSet<string> AbortCaptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cancel", "Annuler", "Abbrechen", "Annulla", "Cancelar", "Stop", "Arrêter", "Abort", "Interrompre"
        };

        /// <summary>Benign single-button captions safe to auto-click (case- and '&amp;'-insensitive).</summary>
        private static readonly HashSet<string> AutoClickCaptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OK", "Fermer", "Close", "Continuer", "Continue"
        };

        public static DialogPolicy ParsePolicy(string raw)
        {
            switch ((raw ?? "").Trim().ToLowerInvariant())
            {
                case "off": case "none": case "0": return DialogPolicy.Off;
                case "report": return DialogPolicy.Report;
                default: return DialogPolicy.Acknowledge;
            }
        }

        public static DialogPolicy CurrentPolicy()
        {
            return ParsePolicy(Environment.GetEnvironmentVariable(PolicyEnvVar));
        }

        public static int ParseGraceMs(string raw)
        {
            int ms;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw.Trim(), out ms) && ms >= 0 && ms <= 60000) return ms;
            return DefaultGraceMs;
        }

        public static int CurrentGraceMs()
        {
            return ParseGraceMs(Environment.GetEnvironmentVariable(GraceEnvVar));
        }

        /// <summary>A button caption without its '&amp;' accelerator markers ("&amp;Yes" -> "Yes").</summary>
        public static string NormalizeButton(string caption)
        {
            if (string.IsNullOrEmpty(caption)) return "";
            return caption.Replace("&&", "\u0001").Replace("&", "").Replace('\u0001', '&').Trim();
        }

        /// <summary>The distinct buttons of a dialog, normalized.</summary>
        public static List<string> Buttons(WindowInfo dialog)
        {
            return (dialog == null ? new List<string>() : dialog.Buttons)
                .Select(NormalizeButton).Where(b => b.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The raw caption of the button named <paramref name="wanted"/>, or null.</summary>
        public static string FindButton(WindowInfo dialog, string wanted)
        {
            string target = NormalizeButton(wanted);
            if (dialog == null || target.Length == 0) return null;
            return dialog.Buttons.FirstOrDefault(b => string.Equals(NormalizeButton(b), target, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsCancelLike(string caption)
        {
            return CancelCaptions.Contains(NormalizeButton(caption));
        }

        /// <summary>A dialog with a single button only informs: pressing it answers nothing.</summary>
        public static bool IsAcknowledgeOnly(WindowInfo dialog)
        {
            var buttons = Buttons(dialog);
            return buttons.Count == 1 && AutoClickCaptions.Contains(buttons[0]);
        }

        /// <summary>
        /// Pressing <paramref name="caption"/> needs confirm=true unless it cancels, or it is the only
        /// button: "Yes", "No", "Save", "Retry"... are answers the user must give.
        /// </summary>
        public static bool RequiresConfirm(WindowInfo dialog, string caption)
        {
            return !(IsCancelLike(caption) || IsAcknowledgeOnly(dialog));
        }

        public static DialogDecision Decide(WindowInfo dialog, DialogPolicy policy, int autoClicksSoFar)
        {
            var buttons = Buttons(dialog);
            if (dialog != null && dialog.HasProgressBar && buttons.All(b => IsCancelLike(b) || AbortCaptions.Contains(b)))
                return DialogDecision.Ignore;
            if (policy == DialogPolicy.Acknowledge && buttons.Count == 1 && AutoClickCaptions.Contains(buttons[0]) &&
                autoClicksSoFar < MaxAutoClicksPerScript)
                return DialogDecision.AutoClick;
            return DialogDecision.Report;
        }

        /// <summary>One line for a dialog closed automatically: "Title: text [OK]".</summary>
        public static string Summarize(WindowInfo dialog)
        {
            if (dialog == null) return "";
            string title = (dialog.Title ?? "").Trim();
            var texts = dialog.Texts.Select(t => System.Text.RegularExpressions.Regex.Replace(t ?? "", @"\s+", " ").Trim())
                .Where(t => t.Length > 0 && t != title && !Buttons(dialog).Contains(t)).Distinct().Take(5);
            string text = string.Join(" ", texts);
            if (text.Length > 400) text = text.Substring(0, 400) + "...";
            return "\"" + title + "\": " + (text.Length > 0 ? text : "(no readable text)") +
                " [" + string.Join("] [", Buttons(dialog)) + "]";
        }

        /// <summary>
        /// Chooses the dialog and the button of a topsolid_dismiss_dialog call. Returns null and sets
        /// <paramref name="target"/> and <paramref name="caption"/> when the button may be pressed, else
        /// the message returned to the agent. <paramref name="title"/> (optional) narrows the dialogs
        /// by a part of their title; modal dialogs come first in <paramref name="dialogs"/>.
        /// </summary>
        public static string PlanDismiss(IList<WindowInfo> dialogs, string button, string title, bool confirmed,
            out WindowInfo target, out string caption)
        {
            target = null;
            caption = null;
            if (NormalizeButton(button).Length == 0)
                return "Error: 'button' is required: the caption of the button to press, as listed by topsolid_detect_dialog.";

            var candidates = (dialogs ?? new List<WindowInfo>())
                .Where(d => d != null && (string.IsNullOrWhiteSpace(title) ||
                    (d.Title ?? "").IndexOf(title.Trim(), StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            if (candidates.Count == 0)
                return string.IsNullOrWhiteSpace(title)
                    ? "No dialog box is open in TopSolid."
                    : "No open dialog box has \"" + title.Trim() + "\" in its title.";

            target = candidates.FirstOrDefault(d => FindButton(d, button) != null);
            if (target == null)
            {
                return "Error: no open dialog box has a button [" + NormalizeButton(button) + "]. Buttons found: " +
                    string.Join("; ", candidates.Select(d => "\"" + (d.Title ?? "").Trim() + "\" [" + string.Join("] [", Buttons(d)) + "]")) + ".";
            }

            caption = FindButton(target, button);
            if (!confirmed && RequiresConfirm(target, caption))
            {
                string pick = NormalizeButton(caption);
                target = null;
                caption = null;
                return "Not pressed: [" + pick + "] answers the question of the dialog box, so it is the user's choice. " +
                    "Show the user the dialog's text, ask them which button to press, then call again with confirm=true.";
            }
            return null;
        }

        /// <summary>Header of the error returned when a dialog stops a script.</summary>
        public static string BlockedHeader(int elapsedSeconds)
        {
            return "Error [topsolid_dialog]: TopSolid opened a dialog box after " + elapsedSeconds + " s and is waiting for an answer. " +
                "The script is paused, not failed: it resumes when the dialog is closed. " +
                "Tell the user what the dialog says, then call topsolid_dismiss_dialog with the button to press " +
                "(it returns the script result once the script finishes).";
        }
    }
}
