using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Persists user-adjustable tray settings (TopSolid port, read-only mode) in a
    /// settings.json next to the executable. CLI arguments and environment variables
    /// still win: settings.json only fills what they leave unset, so an MCP client
    /// configured with --port keeps working after the user tweaks the tray value.
    /// </summary>
    public static class TraySettings
    {
        /// <summary>TopSolid port chosen in the tray; 0 = not set (use default).</summary>
        public static int Port;

        /// <summary>Read-only mode chosen in the tray; false = not set.</summary>
        public static bool ReadOnly;

        private static string FilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        /// <summary>Loads settings.json. Missing or corrupt file = silent defaults.</summary>
        public static void Load()
        {
            try
            {
                string path = FilePath();
                if (!File.Exists(path)) return;
                var obj = JObject.Parse(File.ReadAllText(path));
                Port = obj.Value<int?>("port") ?? 0;
                ReadOnly = obj.Value<bool?>("read_only") ?? false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TraySettings] Load failed, using defaults: " + ex.Message);
                Port = 0;
                ReadOnly = false;
            }
        }

        /// <summary>Writes settings.json. Failure is logged, never thrown.</summary>
        public static void Save(int port, bool readOnly)
        {
            try
            {
                var obj = new JObject
                {
                    ["port"] = port,
                    ["read_only"] = readOnly
                };
                File.WriteAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"),
                    obj.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TraySettings] Save failed: " + ex.Message);
            }
        }
    }
}