using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Presses a button of a dialog box open in TopSolid, then waits for the script it had stopped.
    /// Like topsolid_detect_dialog, it goes through Win32 and UI Automation, never the Automation API,
    /// so it works while TopSolid is blocked. A button that answers a question (Yes, No, Save...) needs
    /// confirm=true: the agent asks the user first. A cancel button, or the only button of a message,
    /// does not.
    /// </summary>
    public class DismissDialogTool
    {
        private const int DefaultWaitSeconds = 60;
        private const int MaxWaitSeconds = 600;

        private readonly Func<TopSolidConnector> _connectorProvider;

        public DismissDialogTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_dismiss_dialog",
                Description = "Press a button of a dialog box open in TopSolid (the one reported by topsolid_detect_dialog " +
                    "or by an Error [topsolid_dialog] result), then wait for the script it had paused and return that script's result. " +
                    "Works even when TopSolid is blocked. Cancel/Close, or the only button of a message, are pressed directly; " +
                    "any other answer (Yes, No, Save, Retry...) is the user's choice: ask them, then pass confirm=true.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["button"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Caption of the button to press, as reported (e.g. \"OK\", \"Annuler\", \"Oui\")."
                        },
                        ["title"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional: part of the dialog's title, when several dialogs are open."
                        },
                        ["confirm"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "true once the user has chosen this answer. Needed for every button except Cancel/Close or a single OK."
                        },
                        ["wait_seconds"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "How long to wait for the paused script after the click (default 60, max 600)."
                        }
                    },
                    ["required"] = new JArray("button")
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            try
            {
                string button = arguments == null ? null : (string)arguments["button"];
                string title = arguments == null ? null : (string)arguments["title"];
                bool confirmed = DestructiveGuard.Disabled || DestructiveGuard.IsConfirmed(arguments);
                int wait = DefaultWaitSeconds;
                if (arguments != null && arguments["wait_seconds"] != null)
                {
                    int parsed;
                    if (int.TryParse(arguments["wait_seconds"].ToString(), out parsed))
                        wait = Math.Max(1, Math.Min(MaxWaitSeconds, parsed));
                }

                var dialogs = DialogDetector.ReadDialogs(DetectDialogTool.ResolveProcessId(_connectorProvider));
                WindowInfo target;
                string caption;
                string refusal = DialogWatch.PlanDismiss(dialogs, button, title, confirmed, out target, out caption);
                if (refusal != null)
                {
                    if (target == null && dialogs.Count > 0 && !refusal.StartsWith("No ", StringComparison.Ordinal))
                        refusal += "\n\n" + DialogDetector.Format(dialogs);
                    else if (dialogs.Count == 0 && ScriptExecutor.HasRunningScript)
                        refusal += " A script is still running in the background; its result comes with the next call.";
                    return refusal;
                }

                string summary = DialogWatch.Summarize(target);
                string clickError = DialogDetector.Click(target, caption);
                if (clickError != null)
                    return "Error: " + clickError + ". Ask the user to answer the dialog in TopSolid.\n\n" + DialogDetector.Format(new[] { target });

                Console.Error.WriteLine("[MCP-INFO] Dialog box answered [" + DialogWatch.NormalizeButton(caption) + "]: " + summary);
                string pressed = "Pressed [" + DialogWatch.NormalizeButton(caption) + "] in " + summary + ".";

                string script = ScriptExecutor.ResumePendingScript(wait);
                if (script == null)
                    return pressed + " No script was waiting on it.";
                return pressed + " Result of the paused script:\n\n" + script;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[DismissDialogTool] Unexpected error: " + ex.Message);
                return "Error: the dialog box could not be answered (" + ex.Message + ").";
            }
        }
    }
}
