using System;
using System.ComponentModel;
using System.Diagnostics;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Runs schtasks for "Start with Windows" (logic and arguments in <see cref="AutoStartLogic"/>).
    /// A task created by the installer from an elevated prompt can only be changed elevated: when the
    /// plain call is refused, the same call runs again through the Windows elevation prompt (UAC).
    /// </summary>
    public static class AutoStart
    {
        public static AutoStartState GetState()
        {
            string output;
            int code = Run(AutoStartLogic.QueryArguments, out output);
            return code == int.MinValue ? AutoStartState.Unknown : AutoStartLogic.ParseQuery(code, output);
        }

        /// <summary>Brings the logon task to the wanted state. Null on success, else a message for the user.</summary>
        public static string Set(bool wanted, string exePath)
        {
            AutoStartAction action = AutoStartLogic.PlanFor(wanted, GetState());
            if (action == AutoStartAction.Nothing) return null;

            string args = action == AutoStartAction.Create
                ? AutoStartLogic.CreateArguments(exePath)
                : AutoStartLogic.ChangeArguments(action == AutoStartAction.Enable);

            string output;
            if (Run(args, out output) != 0)
            {
                string error = RunElevated(args);
                if (error != null) return error;
            }

            AutoStartState after = GetState();
            bool ok = wanted ? after == AutoStartState.Enabled : after != AutoStartState.Enabled;
            return ok ? null : (string.IsNullOrWhiteSpace(output) ? "schtasks " + action : output.Trim());
        }

        private static int Run(string args, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return int.MinValue; }
                    output = stdout.Length > 0 ? stdout : stderr;
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return int.MinValue;
            }
        }

        /// <summary>Null when the elevated call ran; a message when it was refused or failed to start.</summary>
        private static string RunElevated(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(60000);
                }
                return null;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return L10n.T("settings.autoStart.cancelled");
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
