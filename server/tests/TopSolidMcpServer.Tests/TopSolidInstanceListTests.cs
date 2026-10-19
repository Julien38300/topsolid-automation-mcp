using System;
using System.Collections.Generic;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// Pure logic behind the tray's TopSolid instance picker (v1.8.3):
    /// merging the process list with the TCP listener table and extracting
    /// the displayed version. The OS-facing part (Process enumeration,
    /// GetExtendedTcpTable) is a thin wrapper around this and is probed
    /// at runtime, not unit-tested.
    /// </summary>
    [TestFixture]
    public class TopSolidInstanceListTests
    {
        // ---- Version extraction from an executable path ----

        [Test]
        public void ExtractVersion_StandardTopSolidFolder_ReturnsVersion()
        {
            string v = TopSolidInstanceList.ExtractVersion(
                @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.exe");
            Assert.AreEqual("7.21", v);
        }

        [Test]
        public void ExtractVersion_ProgramFilesX86_ReturnsVersion()
        {
            string v = TopSolidInstanceList.ExtractVersion(
                @"C:\Program Files (x86)\TOPSOLID\TopSolid 7.15\bin\TopSolid.exe");
            Assert.AreEqual("7.15", v);
        }

        [Test]
        public void ExtractVersion_MisslerLayout_ReturnsNull()
        {
            // Missler layout (C:\Missler\V###) has no readable "7.x" segment:
            // null = the menu shows the instance without a version label.
            string v = TopSolidInstanceList.ExtractVersion(@"C:\Missler\V721\bin\TopSolid.exe");
            Assert.IsNull(v);
        }

        [Test]
        public void ExtractVersion_UnknownPath_ReturnsNull()
        {
            Assert.IsNull(TopSolidInstanceList.ExtractVersion(@"C:\Somewhere\TopSolid.exe"));
        }

        // ---- Merge: processes + listening ports ----

        [Test]
        public void Merge_TwoTopSolidWithPorts_BothListedWithPorts()
        {
            // Real LY458 situation (07/10/2026): 7.21 on 8091 and 7.20 on 8090.
            var procs = new List<ProcessSnapshot>
            {
                new ProcessSnapshot(8428, @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.exe"),
                new ProcessSnapshot(16568, @"C:\Program Files\TOPSOLID\TopSolid 7.20\bin\TopSolid.exe")
            };
            var ports = new List<PortSnapshot>
            {
                new PortSnapshot(8428, 8091),
                new PortSnapshot(16568, 8090)
            };

            List<TopSolidInstance> instances = TopSolidInstanceList.Merge(procs, ports);

            Assert.AreEqual(2, instances.Count, "both running TopSolid must appear");
            var byPid = new Dictionary<int, TopSolidInstance>();
            foreach (var i in instances) byPid[i.Pid] = i;
            Assert.AreEqual(8091, byPid[8428].Port, "7.21 instance must carry its automation port");
            Assert.AreEqual("7.21", byPid[8428].Version);
            Assert.IsTrue(byPid[8428].HasAutomationPort);
            Assert.AreEqual(8090, byPid[16568].Port);
            Assert.AreEqual("7.20", byPid[16568].Version);
        }

        [Test]
        public void Merge_NoAutomationPort_PortIsZeroAndFlagged()
        {
            // A TopSolid running with Automation disabled: visible in the menu
            // but not targetable over TCP.
            var procs = new List<ProcessSnapshot>
            {
                new ProcessSnapshot(100, @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.exe")
            };
            var instances = TopSolidInstanceList.Merge(procs, new List<PortSnapshot>());
            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual(0, instances[0].Port, "no listener = port 0, never a guessed port");
            Assert.IsFalse(instances[0].HasAutomationPort);
        }

        [Test]
        public void Merge_PidWithoutProcess_Ignored()
        {
            // A TCP listener whose owning process is gone (race during scan).
            var ports = new List<PortSnapshot> { new PortSnapshot(999, 8090) };
            var instances = TopSolidInstanceList.Merge(new List<ProcessSnapshot>(), ports);
            Assert.AreEqual(0, instances.Count);
        }

        [Test]
        public void Merge_EmptyInputs_ReturnsEmptyList()
        {
            Assert.AreEqual(0, TopSolidInstanceList.Merge(
                new List<ProcessSnapshot>(), new List<PortSnapshot>()).Count);
        }

        [Test]
        public void Merge_NewestVersionSortedFirst()
        {
            // Menu order: newest version first so the picker reads naturally.
            var procs = new List<ProcessSnapshot>
            {
                new ProcessSnapshot(1, @"C:\Program Files\TOPSOLID\TopSolid 7.19\bin\TopSolid.exe"),
                new ProcessSnapshot(2, @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.exe"),
                new ProcessSnapshot(3, @"C:\Program Files\TOPSOLID\TopSolid 7.20\bin\TopSolid.exe")
            };
            var instances = TopSolidInstanceList.Merge(procs, new List<PortSnapshot>());
            Assert.AreEqual("7.21", instances[0].Version, "7.21 must sort before 7.20 and 7.19");
        }

        [Test]
        public void Merge_MultiplePortsForSamePid_PrefersAutomationPortOverEphemeral()
        {
            // When TopSolid.exe owns both an ephemeral/callback TCP listener (e.g. 49670)
            // and its Automation listener (8090 or 8091), Merge must pick the Automation port
            // regardless of the order in GetExtendedTcpTable.
            var procs = new List<ProcessSnapshot>
            {
                new ProcessSnapshot(10600, @"C:\Program Files\TOPSOLID\TopSolid 7.20\bin\TopSolid.exe")
            };
            var ports = new List<PortSnapshot>
            {
                new PortSnapshot(10600, 49670),
                new PortSnapshot(10600, 8090),
                new PortSnapshot(10600, 51234)
            };

            var instances = TopSolidInstanceList.Merge(procs, ports);
            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual(8090, instances[0].Port, "Automation port 8090 must win over ephemeral ports");
        }

        // ---- Declared-instance matching (persisted vs detected) ----

        [Test]
        public void FindByPort_DeclaredDefaultMatchesDetectedInstance()
        {
            var procs = new List<ProcessSnapshot>
            {
                new ProcessSnapshot(8428, @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.exe")
            };
            var ports = new List<PortSnapshot> { new PortSnapshot(8428, 8091) };
            var instances = TopSolidInstanceList.Merge(procs, ports);

            TopSolidInstance match = TopSolidInstanceList.FindByPort(instances, 8091);
            Assert.IsNotNull(match);
            Assert.AreEqual("7.21", match.Version);

            Assert.IsNull(TopSolidInstanceList.FindByPort(instances, 9999));
        }

        // ---- Issue #13: Network byte-order port decoding & active port fallback ----

        [Test]
        public void DecodeNetworkPort_ConvertsBigEndianToHostPort()
        {
            // 8090 = 0x1F9A -> in network byte order on little-endian x86: 0x9A1F (39455)
            Assert.AreEqual(8090, TopSolidInstanceList.DecodeNetworkPort(0x9A1F));
            // 8091 = 0x1F9B -> 0x9B1F (39711)
            Assert.AreEqual(8091, TopSolidInstanceList.DecodeNetworkPort(0x9B1F));
            // Upper 16 bits garbage must be ignored per Win32 MIB_TCPROW_OWNER_PID docs
            Assert.AreEqual(8090, TopSolidInstanceList.DecodeNetworkPort(unchecked((int)0xDEAD9A1F)));
        }

        [Test]
        public void GetListeningPorts_DetectsRealLocalTcpListener()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int boundPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                int myPid = System.Diagnostics.Process.GetCurrentProcess().Id;

                var ports = TopSolidInstanceList.GetListeningPorts();
                bool found = false;
                foreach (var p in ports)
                {
                    if (p.Pid == myPid && p.Port == boundPort)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, $"Expected GetListeningPorts() to find port {boundPort} for pid {myPid}");
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void ResolveActivePort_StaleConfiguredPort8091_FallsBackToRunning8090_Issue13()
        {
            // Issue #13 exact reproduction: settings.json had port 8091 from an earlier
            // session, but today the user launched TopSolid with port 8090 open.
            var running = new List<TopSolidInstance>
            {
                new TopSolidInstance(1234, "7.20", 8090)
            };

            int resolvedAuto = TopSolidInstanceList.ResolveActivePort(8091, "auto", running);
            Assert.AreEqual(8090, resolvedAuto, "Must auto-switch from unreachable 8091 to running 8090 in auto mode");

            int resolvedManual = TopSolidInstanceList.ResolveActivePort(8091, "manual", running);
            Assert.AreEqual(8090, resolvedManual, "Must also fall back to running 8090 when manual port 8091 is not running");
        }

        [Test]
        public void ResolveActivePort_ConfiguredPort8091IsRunning_Honours8091()
        {
            var running = new List<TopSolidInstance>
            {
                new TopSolidInstance(100, "7.21", 8091),
                new TopSolidInstance(200, "7.20", 8090)
            };

            int resolved = TopSolidInstanceList.ResolveActivePort(8091, "manual", running);
            Assert.AreEqual(8091, resolved);
        }

        [Test]
        public void TraySettings_Save_PreservesExistingApiKeyDpapiField()
        {
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsmcp_test_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                string settingsPath = System.IO.Path.Combine(tempDir, "settings.json");
                System.IO.File.WriteAllText(settingsPath, "{\"api_key_dpapi\":\"secret_blob\",\"port\":8091}");

                TraySettings.OverrideBaseDirectoryForTests(tempDir);
                TraySettings.Load();
                Assert.AreEqual(8091, TraySettings.Port);

                TraySettings.Save(8090, false);
                var saved = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(settingsPath));
                Assert.AreEqual("secret_blob", saved.Value<string>("api_key_dpapi"), "TraySettings.Save must not wipe api_key_dpapi");
                Assert.AreEqual(8090, saved.Value<int>("port"));
            }
            finally
            {
                TraySettings.OverrideBaseDirectoryForTests(null);
                try { System.IO.Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}