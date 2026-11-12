using System;
using System.Linq;
using System.Xml.Linq;

namespace TopSolidMcpServer.Utils
{
    public enum AutoStartState { Unknown, Absent, Enabled, Disabled }

    public enum AutoStartAction { Nothing, Create, Enable, Disable }

    /// <summary>
    /// "Start with Windows" in the server settings. It drives the TopSolidMcpServer logon task that
    /// install\Installer_TopSolidMCP.bat creates, so the server never gets a second, competing autostart.
    /// Pure logic (no process, no WinForms) so that it is unit tested; <see cref="AutoStart"/> runs schtasks.
    /// </summary>
    public static class AutoStartLogic
    {
        public const string TaskName = "TopSolidMcpServer";

        public static string QueryArguments
        {
            get { return "/Query /TN \"" + TaskName + "\" /XML"; }
        }

        /// <summary>Same task as the installer: at logon, interactive, highest privileges, no console window.</summary>
        public static string CreateArguments(string exePath)
        {
            return "/Create /F /TN \"" + TaskName + "\" /SC ONLOGON /RL HIGHEST /IT " +
                   "/TR \"conhost.exe --headless \\\"" + exePath + "\\\" --http-standalone\"";
        }

        public static string ChangeArguments(bool enable)
        {
            return "/Change /TN \"" + TaskName + "\" " + (enable ? "/ENABLE" : "/DISABLE");
        }

        /// <summary>
        /// State from `schtasks /Query /XML`. A failed query means no task. The XML element
        /// Task/Settings/Enabled is omitted when true, and does not depend on the Windows language.
        /// </summary>
        public static AutoStartState ParseQuery(int exitCode, string output)
        {
            if (exitCode != 0) return AutoStartState.Absent;
            try
            {
                var doc = XDocument.Parse((output ?? "").Trim());
                var settings = doc.Root == null ? null : doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "Settings");
                if (settings == null) return AutoStartState.Unknown;
                var enabled = settings.Elements().FirstOrDefault(e => e.Name.LocalName == "Enabled");
                if (enabled == null) return AutoStartState.Enabled;
                return string.Equals(enabled.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase)
                    ? AutoStartState.Disabled : AutoStartState.Enabled;
            }
            catch (Exception)
            {
                return AutoStartState.Unknown;
            }
        }

        /// <summary>What to do to reach the wanted state. Turning it off disables the task, never deletes it.</summary>
        public static AutoStartAction PlanFor(bool wanted, AutoStartState state)
        {
            if (wanted)
            {
                if (state == AutoStartState.Absent) return AutoStartAction.Create;
                if (state == AutoStartState.Disabled) return AutoStartAction.Enable;
                return state == AutoStartState.Unknown ? AutoStartAction.Enable : AutoStartAction.Nothing;
            }
            return state == AutoStartState.Enabled || state == AutoStartState.Unknown ? AutoStartAction.Disable : AutoStartAction.Nothing;
        }
    }
}
