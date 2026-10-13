using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// A TopSolid instance declared manually in the tray's "Manage" dialog:
    /// a display name, its automation TCP port, and whether it is the default
    /// target at server startup.
    /// </summary>
    public sealed class DeclaredTopSolidInstance
    {
        public DeclaredTopSolidInstance(string name, int port)
        {
            Name = name;
            Port = port;
        }

        public string Name { get; private set; }
        public int Port { get; private set; }

        /// <summary>True when this instance is the startup default. Only one per list.</summary>
        public bool IsDefault { get; set; }
    }

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

        /// <summary>
        /// v1.8.3: "auto" (default) targets the first detected running TopSolid,
        /// "manual" targets <see cref="Port"/> whatever is running.
        /// </summary>
        public static string InstanceMode = "auto";

        /// <summary>v1.8.3: instances declared in the "Manage TopSolid" dialog.</summary>
        public static List<DeclaredTopSolidInstance> DeclaredInstances =
            new List<DeclaredTopSolidInstance>();

        /// <summary>Test hook: overrides where settings.json is read/written. Null = BaseDirectory.</summary>
        private static string _baseDirectoryOverride;

        internal static void OverrideBaseDirectoryForTests(string dir)
        {
            _baseDirectoryOverride = dir;
        }

        private static string BaseDir()
        {
            return _baseDirectoryOverride ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string FilePath()
        {
            return Path.Combine(BaseDir(), "settings.json");
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

                string mode = obj.Value<string>("instance_mode");
                InstanceMode = (mode == "manual") ? "manual" : "auto";

                DeclaredInstances = new List<DeclaredTopSolidInstance>();
                var arr = obj["declared_instances"] as JArray;
                if (arr != null)
                {
                    foreach (var item in arr)
                    {
                        string name = item.Value<string>("name");
                        int? p = item.Value<int?>("port");
                        if (name != null && p != null && p.Value > 0 && p.Value <= 65535)
                            DeclaredInstances.Add(new DeclaredTopSolidInstance(name, p.Value)
                            {
                                IsDefault = item.Value<bool?>("default") ?? false
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TraySettings] Load failed, using defaults: " + ex.Message);
                Port = 0;
                ReadOnly = false;
                InstanceMode = "auto";
                DeclaredInstances = new List<DeclaredTopSolidInstance>();
            }
        }

        /// <summary>
        /// Writes settings.json with the given port and read-only flag plus the
        /// current instance-picker fields. Failure is logged, never thrown.
        /// </summary>
        public static void Save(int port, bool readOnly)
        {
            try
            {
                var obj = new JObject
                {
                    ["port"] = port,
                    ["read_only"] = readOnly,
                    ["instance_mode"] = InstanceMode,
                    ["declared_instances"] = new JArray(
                        DeclaredInstances.Select(d => new JObject
                        {
                            ["name"] = d.Name,
                            ["port"] = d.Port,
                            ["default"] = d.IsDefault
                        }))
                };
                File.WriteAllText(
                    Path.Combine(BaseDir(), "settings.json"),
                    obj.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TraySettings] Save failed: " + ex.Message);
            }
        }

        /// <summary>
        /// v1.8.3: saves the picker state — chosen port and "auto"/"manual" mode —
        /// keeping every other field. The legacy "port" field stays in sync so
        /// Program.cs keeps working unchanged. Also marks the matching declared
        /// instance (by port) as the default, like the "Manage" dialog does.
        /// </summary>
        public static void SaveInstanceMode(int port, string mode)
        {
            Port = port;
            InstanceMode = (mode == "manual") ? "manual" : "auto";

            foreach (var d in DeclaredInstances)
                d.IsDefault = (d.Port == port && port > 0);

            Save(Port, ReadOnly);
        }

        /// <summary>v1.8.3: saves the "Manage TopSolid" dialog list, keeping every other field.</summary>
        public static void SaveDeclaredInstances(List<DeclaredTopSolidInstance> instances)
        {
            DeclaredInstances = instances ?? new List<DeclaredTopSolidInstance>();
            Save(Port, ReadOnly);
        }
    }
}