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
                    // Several listeners on one pid (IPv4+IPv6): keep the first, they
                    // are the same automation endpoint.
                    if (!portsByPid.ContainsKey(p.Pid)) portsByPid[p.Pid] = p.Port;
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
        /// drops the version label.
        /// </summary>
        public static List<TopSolidInstance> Scan()
        {
            var procs = new List<ProcessSnapshot>();
            foreach (var p in Process.GetProcessesByName("TopSolid"))
            {
                string path = null;
                try { path = p.MainModule != null ? p.MainModule.FileName : null; }
                catch { /* elevated or session-0 process: keep it without a path */ }
                finally
                {
                    if (path == null)
                    {
                        try { path = TryGetPathFromWmi(p.Id); } catch { }
                    }
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
        /// Lists listening TCP ports with their owning pid via GetExtendedTcpTable
        /// (what netstat -ano reads; no admin rights needed, no external process).
        /// </summary>
        private static List<PortSnapshot> GetListeningPorts()
        {
            const int AF_INET = 2;
            const int TCP_TABLE_OWNER_PID_LISTENER = 4;
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedTcpTable(buffer, ref bufferSize, false, AF_INET,
                        TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
                    return new List<PortSnapshot>();

                int count = Marshal.ReadInt32(buffer);
                var table = new List<PortSnapshot>(count);
                long rowPtr = buffer.ToInt64() + 4;
                int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                for (int i = 0; i < count; i++)
                {
                    var row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(
                        new IntPtr(rowPtr + i * rowSize), typeof(MIB_TCPROW_OWNER_PID));
                    // dwState 2 = LISTEN
                    if (row.dwState == 2)
                        table.Add(new PortSnapshot(row.dwOwningPid, row.dwLocalPort));
                }
                return table;
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

        [DllImport("iphlpapi.dll")]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable, ref int pdwSize, bool bOrder,
            int ulAf, int TableClass, int Reserved);
    }
}