using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Resolves the TopSolid bin directory at runtime, supporting multiple installed
    /// versions and both known install layouts:
    ///   - modern: &lt;ProgramFiles&gt;\TOPSOLID\TopSolid 7.xx\bin
    ///   - historical Missler: &lt;drive&gt;:\Missler\V&lt;nnn&gt;\bin (TopSolid 2026 = V627)
    /// Resolution order:
    ///   1. TOPSOLID_BIN_PATH environment variable (explicit override)
    ///   2. Windows registry (HKLM\SOFTWARE\[WOW6432Node\]Missler Software\*, InstallDir or Path)
    ///   3. Filesystem scan of %ProgramFiles% / %ProgramFiles(x86)% \TOPSOLID\TopSolid 7.*\bin
    ///   4. Scan of fixed drives for \Missler\V*\bin and \TOPSOLID\TopSolid 7.*\bin
    ///   5. Hardcoded fallback (backward-compat; logs a warning and leaves <see cref="Found"/> false)
    /// </summary>
    public static class TopSolidPathResolver
    {
        private const string FallbackPath = @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\";
        private const string KernelDll = "TopSolid.Kernel.Automating.dll";

        private static string _cached;
        private static bool _found;
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
                bool found;
                string path = ResolveInternal(out found);
                _found = found;
                _cached = path;
                return _cached;
            }
        }

        /// <summary>
        /// False when <see cref="Resolve"/> could not locate a real TopSolid install and
        /// returned the hardcoded fallback path. Triggers resolution if needed.
        /// </summary>
        public static bool Found
        {
            get
            {
                Resolve();
                return _found;
            }
        }

        private static string ResolveInternal(out bool found)
        {
            found = true;

            // 1. Explicit env-var override
            string envPath = Environment.GetEnvironmentVariable("TOPSOLID_BIN_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                string normalized = EnsureTrailingSlash(envPath.Trim());
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

            // 3. Program Files scan (paths taken from the environment, never hardcoded)
            string fromScan = ScanProgramFiles();
            if (fromScan != null)
            {
                Console.Error.WriteLine("[MCP-INFO] TopSolid bin path from Program Files scan: " + fromScan);
                return fromScan;
            }

            // 4. Last resort: fixed drives only
            string fromDrives = ScanFixedDrives();
            if (fromDrives != null)
            {
                Console.Error.WriteLine("[MCP-INFO] TopSolid bin path from drive scan: " + fromDrives);
                return fromDrives;
            }

            // 5. Fallback
            found = false;
            Console.Error.WriteLine("[MCP-WARN] TopSolid install not found (registry, Program Files and fixed drives " +
                "were all searched for TopSolid 7.* and Missler V* layouts). " +
                "Falling back to " + FallbackPath + " which does not exist on this machine. " +
                "Set TOPSOLID_BIN_PATH to the folder containing " + KernelDll + " to fix this.");
            return FallbackPath;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Registry
        // ─────────────────────────────────────────────────────────────────────────

        private static string ReadFromRegistry()
        {
            // TopSolid registers under "Missler Software" with either a "TopSolid 7.XX"
            // subkey (modern layout) or a "V<nnn>" subkey (historical Missler layout).
            // The install folder is exposed as "InstallDir" or as "Path", and may point
            // either at the install root or directly at its bin directory.
            string[] hives = {
                @"SOFTWARE\Missler Software",
                @"SOFTWARE\WOW6432Node\Missler Software"
            };

            string bestBin = null;
            int bestScore = int.MinValue;

            foreach (string hive in hives)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(hive))
                    {
                        if (key == null) continue;
                        foreach (string subName in key.GetSubKeyNames())
                        {
                            int score;
                            if (!TryScoreVersionName(subName, out score)) continue;

                            using (RegistryKey sub = key.OpenSubKey(subName))
                            {
                                if (sub == null) continue;

                                string[] valueNames = { "InstallDir", "Path" };
                                foreach (string valueName in valueNames)
                                {
                                    string installDir = sub.GetValue(valueName) as string;
                                    if (string.IsNullOrWhiteSpace(installDir)) continue;

                                    string binDir = ToBinDirectory(installDir);
                                    if (binDir == null) continue;

                                    if (score > bestScore)
                                    {
                                        bestScore = score;
                                        bestBin = binDir;
                                    }
                                    break;
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

        // ─────────────────────────────────────────────────────────────────────────
        // Filesystem scans
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans the TOPSOLID folder under every Program Files directory reported by the
        /// environment (both 64-bit and 32-bit views).
        /// </summary>
        private static string ScanProgramFiles()
        {
            string bestBin = null;
            int bestScore = int.MinValue;

            foreach (string programFiles in GetProgramFilesDirectories())
            {
                ScanVersionedContainer(Path.Combine(programFiles, "TOPSOLID"), ref bestBin, ref bestScore);
            }

            return bestBin;
        }

        /// <summary>
        /// Enumerates the Program Files directories from the environment, de-duplicated.
        /// </summary>
        private static IEnumerable<string> GetProgramFilesDirectories()
        {
            var seen = new List<string>();
            string[] variables = { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" };

            foreach (string variable in variables)
            {
                string value = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(value)) continue;

                string normalized = value.TrimEnd('\\', '/');
                bool duplicate = false;
                foreach (string existing in seen)
                {
                    if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                seen.Add(normalized);
            }

            return seen;
        }

        /// <summary>
        /// Last-resort scan: fixed drives only (never network or removable drives), looking
        /// for the two known layouts at the root of each drive.
        /// </summary>
        private static string ScanFixedDrives()
        {
            string bestBin = null;
            int bestScore = int.MinValue;

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] Drive enumeration failed: " + ex.Message);
                return null;
            }

            foreach (DriveInfo drive in drives)
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed) continue;
                    if (!drive.IsReady) continue;
                }
                catch (Exception)
                {
                    continue;
                }

                string root = drive.RootDirectory.FullName;

                // Historical Missler layout: <drive>:\Missler\V<nnn>\bin
                ScanVersionedContainer(Path.Combine(root, "Missler"), ref bestBin, ref bestScore);

                // Modern layout dropped at a drive root: <drive>:\TOPSOLID\TopSolid 7.*\bin
                ScanVersionedContainer(Path.Combine(root, "TOPSOLID"), ref bestBin, ref bestScore);
            }

            return bestBin;
        }

        /// <summary>
        /// Inspects every direct subfolder of <paramref name="container"/> whose name looks
        /// like a TopSolid version ("TopSolid 7.21" or "V627") and keeps the highest one
        /// that actually contains the kernel Automation assembly.
        /// </summary>
        private static void ScanVersionedContainer(string container, ref string bestBin, ref int bestScore)
        {
            try
            {
                if (!Directory.Exists(container)) return;

                foreach (string dir in Directory.GetDirectories(container))
                {
                    string dirName = Path.GetFileName(dir);

                    int score;
                    if (!TryScoreVersionName(dirName, out score)) continue;

                    string binDir = ToBinDirectory(dir);
                    if (binDir == null) continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestBin = binDir;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-WARN] Scan of " + container + " failed: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Turns an install folder into a usable bin directory: the folder itself when it
        /// already holds the kernel assembly, otherwise its "bin" subfolder.
        /// Returns null when the kernel assembly is nowhere to be found.
        /// </summary>
        private static string ToBinDirectory(string installDir)
        {
            try
            {
                string trimmed = installDir.Trim().TrimEnd('\\', '/');
                if (trimmed.Length == 0) return null;

                if (File.Exists(Path.Combine(trimmed, KernelDll)))
                    return EnsureTrailingSlash(trimmed);

                string binDir = Path.Combine(trimmed, "bin");
                if (File.Exists(Path.Combine(binDir, KernelDll)))
                    return EnsureTrailingSlash(binDir);
            }
            catch (Exception)
            {
                // Malformed path (invalid characters, too long, ...) — just ignore it.
            }

            return null;
        }

        /// <summary>
        /// Scores a version folder or registry key name so installs can be compared.
        /// "TopSolid 7.21" scores 721, the Missler form "V627" scores 627. Higher wins.
        /// </summary>
        private static bool TryScoreVersionName(string name, out int score)
        {
            score = int.MinValue;
            if (string.IsNullOrWhiteSpace(name)) return false;

            string trimmed = name.Trim();

            // Modern: "TopSolid 7.21" (also tolerates "TopSolid 7.21.195")
            var modern = Regex.Match(trimmed, @"^TopSolid\s+(\d+)\.(\d+)", RegexOptions.IgnoreCase);
            if (modern.Success)
            {
                int major, minor;
                if (int.TryParse(modern.Groups[1].Value, out major) &&
                    int.TryParse(modern.Groups[2].Value, out minor))
                {
                    score = major * 100 + minor;
                    return true;
                }
                return false;
            }

            // Historical Missler: "V627"
            var missler = Regex.Match(trimmed, @"^V(\d+)$", RegexOptions.IgnoreCase);
            if (missler.Success)
            {
                int build;
                if (int.TryParse(missler.Groups[1].Value, out build))
                {
                    score = build;
                    return true;
                }
            }

            return false;
        }

        private static string EnsureTrailingSlash(string path)
        {
            return path.TrimEnd('\\', '/') + '\\';
        }
    }
}
