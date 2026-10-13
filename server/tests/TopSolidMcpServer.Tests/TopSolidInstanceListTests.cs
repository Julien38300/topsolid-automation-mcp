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
    }
}