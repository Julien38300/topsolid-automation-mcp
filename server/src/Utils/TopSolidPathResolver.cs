using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Resolves the TopSolid bin directory at runtime, supporting multiple installed versions.
    /// Resolution order:
    ///   1. TOPSOLID_BIN_PATH environment variable (explicit override)
    ///   2. Windows registry (HKLM\SOFTWARE\Missler Software\TopSolid 7.*\InstallDir)
    ///   3. Filesystem scan of C:\Program Files\TOPSOLID\TopSolid 7.*\bin (highest version wins)
    ///   4. Hardcoded fallback (backward-compat; logs a warning)
    /// </summary>
    internal static class TopSolidPathResolver
    {
        private const string FallbackPath = @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\";
        private const string ProgramFilesBase = @"C:\Program Files\TOPSOLID";
        private const string KernelDll = "TopSolid.Kernel.Automating.dll";

        private static string _cached;
        private static readonly object _lock = new object();

        /// <summary>
        /// Returns the resolved TopSolid bin path (trailing backslash included).
        /// Always returns a non-null string; callers should check whether the DLL actually exists.
        /// </summary>
        public static string Resolve()
        {
            if (_cached != null) return _cached;
            lock (_lock)
            {
                if (_cached != null) return _cached;
                _cached = ResolveInternal();
                return _cached;
            }
        }

        private static string ResolveInternal()
        {
            // 1. Explicit env-var override
            string envPath = Environment.GetEnvironmentVariable("TOPSOLID_BIN_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                string normalized = EnsureTrailingSlash(envPath);
                Console.Error.WriteLine("[MCP-INFO] TopSolid bin path from TOPSOLID_BIN_PATH: " + normalized);
                return normalized;
            }

            // 2. Registry — try both 64-bit and WOW6432Node hives
            string fromRegistry = ReadFromRegistry();
            if (fromRegistry != null)
            {
                Console.Error.WriteLine("[MCP-INFO] TopSolid bin path from registry: " + fromRegistry);
                return fromRegistry;
            }

            // 3. Filesystem scan
            string fromScan = ScanProgramFiles();
            if (fromScan != null)
            {
                Console.Error.WriteLine("[MCP-INFO] TopSolid bin path from filesystem scan: " + fromScan);
                return fromScan;
            }

            // 4. Fallback
            Console.Error.WriteLine("[MCP-WARN] TopSolid install not found via registry or filesystem. " +
                "Falling back to " + FallbackPath + ". " +
                "Set TOPSOLID_BIN_PATH to override.");
            return FallbackPath;
        }

        private static string ReadFromRegistry()
        {
            // TopSolid registers under "Missler Software\TopSolid 7.XX" with an InstallDir value.
            // We check both the native 64-bit hive and the WOW6432Node (32-bit) hive.
            string[] hives = {
                @"SOFTWARE\Missler Software",
                @"SOFTWARE\WOW6432Node\Missler Software"
            };

            string bestBin = null;
            Version bestVersion = null;

            foreach (string hive in hives)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(hive))
                    {
                        if (key == null) continue;
                        foreach (string subName in key.GetSubKeyNames())
                        {
                            // Match "TopSolid 7.XX" keys
                            var m = Regex.Match(subName, @"^TopSolid\s+(7\.\d+)$", RegexOptions.IgnoreCase);
                            if (!m.Success) continue;

                            if (!Version.TryParse(m.Groups[1].Value, out Version v)) continue;

                            using (RegistryKey sub = key.OpenSubKey(subName))
                            {
                                if (sub == null) continue;
                                string installDir = sub.GetValue("InstallDir") as string;
                                if (string.IsNullOrWhiteSpace(installDir)) continue;

                                string binDir = Path.Combine(installDir, "bin");
                                string dll = Path.Combine(binDir, KernelDll);
                                if (!File.Exists(dll)) continue;

                                if (bestVersion == null || v > bestVersion)
                                {
                                    bestVersion = v;
                                    bestBin = EnsureTrailingSlash(binDir);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[MCP-WARN] Registry read failed for " + hive + ": " + ex.Message);
                }
            }

            return bestBin;
        }

        private static string ScanProgramFiles()
        {
            if (!Directory.Exists(ProgramFilesBase)) return null;

            string bestBin = null;
            Version bestVersion = null;

            try
            {
                foreach (string dir in Directory.GetDirectories(ProgramFilesBase, "TopSolid 7.*"))
                {
                    string dirName = Path.GetFileName(dir);
                    var m = Regex.Match(dirName, @"^TopSolid\s+(7[\d.]+)$", RegexOptions.IgnoreCase);
                    if (!m.Success) continue;

                    if (!Version.TryParse(m.Groups[1].Value, out Version v)) continue;

                    string binDir = Path.Combine(dir, "bin");
                    string dll = Path.Combine(binDir, KernelDll);
                    if (!File.Exists(dll)) continue;

                    if (bestVersion == null || v > bestVersion)
                    {
                        bestVersion = v;
                        bestBin = EnsureTrailingSlash(binDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] Filesystem scan failed: " + ex.Message);
            }

            return bestBin;
        }

        private static string EnsureTrailingSlash(string path)
        {
            return path.TrimEnd('\\', '/') + '\\';
        }
    }
}
