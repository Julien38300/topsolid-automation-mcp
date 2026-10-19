using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TopSolidMcpServer.Utils
{
    /// <summary>A running process as seen by the instance scanner: pid + executable path.</summary>
    public sealed class ProcessSnapshot
    {
        public ProcessSnapshot(int pid, string path)
        {
            Pid = pid;
            Path = path;
        }

        public int Pid { get; private set; }
        public string Path { get; private set; }
    }

    /// <summary>A listening TCP port and the pid that owns it.</summary>
    public sealed class PortSnapshot
    {
        public PortSnapshot(int pid, int port)
        {
            Pid = pid;
            Port = port;
        }

        public int Pid { get; private set; }
        public int Port { get; private set; }
    }

    /// <summary>One TopSolid instance to show in the tray picker: version + automation port.</summary>
    public sealed class TopSolidInstance
    {
        public TopSolidInstance(int pid, string version, int port)
        {
            Pid = pid;
            Version = version;
            Port = port;
        }

        public int Pid { get; private set; }

        /// <summary>Major.minor version extracted from the executable path ("7.21"), or null.</summary>
        public string Version { get; private set; }

        /// <summary>Automation TCP port. 0 = Automation not detected (no listener).</summary>
        public int Port { get; private set; }

        /// <summary>True when a TCP automation listener was found for this instance.</summary>
        public bool HasAutomationPort { get { return Port > 0; } }
    }

    /// <summary>
    /// Builds the list of running TopSolid instances for the tray picker (v1.8.3),
    /// like the FEA Quality app's "version TopSolid" dropdown: every running
    /// TopSolid shows up with its version and automation port (or none).
    /// <para>
    /// Pure merge/version logic (unit-tested) is split from the OS-facing scan
    /// (Process enumeration + GetExtendedTcpTable), which is a thin wrapper.
    /// </para>
    /// </summary>
    public static class TopSolidInstanceList
    {
        private static readonly Regex VersionSegment = new Regex(
            @"TopSolid\s+(7\.\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> ProcessPathCache =
            new System.Collections.Concurrent.ConcurrentDictionary<int, string>();

        // ---- Pure logic ----

        /// <summary>
        /// Extracts the "7.x" version from a TopSolid executable path.
        /// Returns null for Missler layouts (C:\Missler\V###) or unknown paths:
        /// the menu then shows the instance without a version label.
        /// </summary>
        public static string ExtractVersion(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return null;

            Match m = VersionSegment.Match(exePath);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Ranks a listening TCP port so that when a TopSolid.exe process owns multiple
        /// listening sockets (e.g. an ephemeral WCF callback port + the Automation port
        /// 8090/8091), Merge always picks the Automation port instead of an ephemeral port.
        /// Higher score = preferred.
        /// </summary>
        public static int ScoreAutomationPort(int port)
        {
            if (port <= 0) return 0;
            // Standard TopSolid Automation port range (8090 default, 8091..8099 multi-instance)
            if (port >= 8085 && port <= 8099) return 300;
            // Extended TopSolid / custom low range (8000..8999)
            if (port >= 8000 && port <= 8999) return 200;
            // User-registered non-ephemeral ports (< 49152)
            if (port < 49152) return 100;
            // Windows ephemeral/dynamic port range (49152..65535)
            return 10;
        }

        /// <summary>
        /// Merges a process list and a TCP listener list into the picker entries.
        /// A TopSolid without listener keeps Port=0 (never a guessed port); a listener
        /// whose process is gone is ignored (scan race). Result: newest version first.
        /// </summary>
        public static List<TopSolidInstance> Merge(
            IList<ProcessSnapshot> processes,
            IList<PortSnapshot> ports)
        {
            var byPid = new Dictionary<int, TopSolidInstance>();
            var portsByPid = new Dictionary<int, int>();
            if (ports != null)
            {
                foreach (var p in ports)
                {
                    if (p == null || p.Pid <= 0 || p.Port <= 0) continue;
                    int existing;
                    if (!portsByPid.TryGetValue(p.Pid, out existing) ||
                        ScoreAutomationPort(p.Port) > ScoreAutomationPort(existing))
                    {
                        portsByPid[p.Pid] = p.Port;
                    }
                }
            }

            if (processes != null)
            {
                foreach (var proc in processes)
                {
                    if (proc == null || proc.Pid <= 0 || string.IsNullOrEmpty(proc.Path)) continue;
                    if (byPid.ContainsKey(proc.Pid)) continue;

                    int port;
                    portsByPid.TryGetValue(proc.Pid, out port);
                    byPid[proc.Pid] = new TopSolidInstance(
                        proc.Pid, ExtractVersion(proc.Path), port);
                }
            }

            // Newest version first; instances without a version sort last (stable).
            return byPid.Values
                .OrderByDescending(i => i.Version, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Pid)
                .ToList();
        }

        /// <summary>Returns the instance listening on the given port, or null.</summary>
        public static TopSolidInstance FindByPort(IList<TopSolidInstance> instances, int port)
        {
            if (instances == null || port <= 0) return null;
            foreach (var i in instances)
                if (i.Port == port) return i;
            return null;
        }

        // ---- OS-facing scan (thin wrapper, probed at runtime) ----

        /// <summary>
        /// Scans the machine for running TopSolid instances. Never throws:
        /// access-denied on MainModule (session 0, elevated instances) simply
        /// drops the version label. Caches executable paths by PID so UI-thread
        /// menu refreshes complete in under 1 ms without repeated WMI queries.
        /// </summary>
        public static List<TopSolidInstance> Scan()
        {
            var procs = new List<ProcessSnapshot>();
            foreach (var p in Process.GetProcessesByName("TopSolid"))
            {
                string path;
                if (!ProcessPathCache.TryGetValue(p.Id, out path) || string.IsNullOrEmpty(path))
                {
                    try { path = p.MainModule != null ? p.MainModule.FileName : null; }
                    catch { /* elevated or session-0 process: keep it without a path */ }
                    finally
                    {
                        if (path == null)
                        {
                            try { path = TryGetPathFromWmi(p.Id); } catch { }
                        }
                    }
                    if (!string.IsNullOrEmpty(path))
                        ProcessPathCache[p.Id] = path;
                }
                procs.Add(new ProcessSnapshot(p.Id, path));
            }

            return Merge(procs, GetListeningPorts());
        }

        private static string TryGetPathFromWmi(int pid)
        {
            // Fallback for the MainModule access-denied case: the WMI ExecutablePath.
            using (var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + pid))
            {
                foreach (var o in searcher.Get())
                {
                    var v = o["ExecutablePath"];
                    if (v != null) return v.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// Converts the raw <c>dwLocalPort</c> field of <c>MIB_TCPROW_OWNER_PID</c>
        /// (network byte order in the lower 16 bits; upper 16 bits may contain
        /// uninitialized data per Win32 docs) into a host-order TCP port number.
        /// </summary>
        public static int DecodeNetworkPort(int dwLocalPort)
        {
            return ((dwLocalPort & 0xFF) << 8) | ((dwLocalPort >> 8) & 0xFF);
        }

        /// <summary>
        /// Issue #13: Resolves the effective TopSolid TCP port to target given the
        /// configured port (from settings.json / default), the instance mode ("auto"
        /// or "manual"), and the currently running TopSolid instances.
        /// <list type="bullet">
        ///   <item>If a running TopSolid instance is listening on <paramref name="configuredPort"/>
        ///         and either mode is "manual" or <paramref name="configuredPort"/> was explicitly set, keep it.</item>
        ///   <item>Otherwise, if any running TopSolid instance exposes an open Automation port
        ///         (e.g. user launched TopSolid on 8090 while settings.json still had a stale 8091),
        ///         automatically target the first running instance with an open Automation port.</item>
        ///   <item>If no running TopSolid with Automation port is found, keep <paramref name="configuredPort"/>
        ///         (or 8090 when unset).</item>
        /// </list>
        /// </summary>
        public static int ResolveActivePort(
            int configuredPort,
            string instanceMode,
            IList<TopSolidInstance> runningInstances)
        {
            int fallbackPort = configuredPort > 0 ? configuredPort : 8090;
            if (runningInstances == null || runningInstances.Count == 0)
                return fallbackPort;

            // If the configured port is actually alive on a running TopSolid, honour it
            // when in manual mode or when a non-default port was configured.
            if (configuredPort > 0 && (instanceMode == "manual" || configuredPort != 8090))
            {
                var exact = FindByPort(runningInstances, configuredPort);
                if (exact != null && exact.HasAutomationPort)
                    return exact.Port;
            }

            // Otherwise (auto mode, or configuredPort is stale/unreachable like 8091 when
            // only 8090 is running — Issue #13), pick the first running TopSolid that has
            // an open Automation port.
            foreach (var inst in runningInstances)
            {
                if (inst != null && inst.HasAutomationPort)
                    return inst.Port;
            }

            return fallbackPort;
        }

        /// <summary>
        /// Lists listening TCP ports with their owning pid via GetExtendedTcpTable
        /// (what netstat -ano reads; no admin rights needed, no external process).
        /// Scans both IPv4 (AF_INET) and IPv6 (AF_INET6).
        /// </summary>
        public static List<PortSnapshot> GetListeningPorts()
        {
            var table = new List<PortSnapshot>();
            ReadTcp4Listeners(table);
            ReadTcp6Listeners(table);
            return table;
        }

        private static void ReadTcp4Listeners(List<PortSnapshot> table)
        {
            const int AF_INET = 2;
            // Win32 TCP_TABLE_CLASS enum:
            //   0 = TCP_TABLE_BASIC_LISTENER
            //   1 = TCP_TABLE_BASIC_CONNECTIONS
            //   2 = TCP_TABLE_BASIC_ALL
            //   3 = TCP_TABLE_OWNER_PID_LISTENER
            //   4 = TCP_TABLE_OWNER_PID_CONNECTIONS
            //   5 = TCP_TABLE_OWNER_PID_ALL
            const int TCP_TABLE_OWNER_PID_LISTENER = 3;
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);
            if (bufferSize <= 0) return;

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedTcpTable(buffer, ref bufferSize, false, AF_INET,
                        TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
                    return;

                int count = Marshal.ReadInt32(buffer);
                long rowPtr = buffer.ToInt64() + 4;
                int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                for (int i = 0; i < count; i++)
                {
                    var row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(
                        new IntPtr(rowPtr + i * rowSize), typeof(MIB_TCPROW_OWNER_PID));
                    // dwState 2 = MIB_TCP_STATE_LISTEN
                    if (row.dwState == 2)
                    {
                        int port = DecodeNetworkPort(row.dwLocalPort);
                        if (port > 0)
                            table.Add(new PortSnapshot(row.dwOwningPid, port));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void ReadTcp6Listeners(List<PortSnapshot> table)
        {
            const int AF_INET6 = 23;
            const int TCP_TABLE_OWNER_PID_LISTENER = 3;
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET6, TCP_TABLE_OWNER_PID_LISTENER, 0);
            if (bufferSize <= 0) return;

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedTcpTable(buffer, ref bufferSize, false, AF_INET6,
                        TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
                    return;

                int count = Marshal.ReadInt32(buffer);
                long rowPtr = buffer.ToInt64() + 4;
                int rowSize = Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID));
                for (int i = 0; i < count; i++)
                {
                    var row = (MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(
                        new IntPtr(rowPtr + i * rowSize), typeof(MIB_TCP6ROW_OWNER_PID));
                    if (row.dwState == 2)
                    {
                        int port = DecodeNetworkPort(row.dwLocalPort);
                        if (port > 0)
                            table.Add(new PortSnapshot(row.dwOwningPid, port));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public int dwState;
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwRemoteAddr;
            public int dwRemotePort;
            public int dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public int dwLocalScopeId;
            public int dwLocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public int dwRemoteScopeId;
            public int dwRemotePort;
            public int dwState;
            public int dwOwningPid;
        }

        [DllImport("iphlpapi.dll")]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable, ref int pdwSize, bool bOrder,
            int ulAf, int TableClass, int Reserved);
    }
}