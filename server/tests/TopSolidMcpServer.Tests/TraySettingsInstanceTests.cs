using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// settings.json persistence for the v1.8.3 instance picker: the "port"
    /// field stays readable (backward compat with v1.8.2 settings written by
    /// the old "Port TopSolid" item) while the richer structure adds
    /// declared instances and the auto/selected mode.
    /// </summary>
    [TestFixture]
    public class TraySettingsInstanceTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "tsmcp-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            TraySettings.OverrideBaseDirectoryForTests(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch { }
            TraySettings.OverrideBaseDirectoryForTests(null);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsPortAndMode()
        {
            TraySettings.SaveInstanceMode(8091, "auto");
            TraySettings.Load();
            Assert.AreEqual(8091, TraySettings.Port);
            Assert.AreEqual("auto", TraySettings.InstanceMode);
        }

        [Test]
        public void SaveHttpPort_ThenLoad_RoundTrips()
        {
            // v1.8.10: the "Manage" dialog MCP HTTP port persists in settings.json
            // and comes back on Load — the Program.cs resolution (env > settings > 8080)
            // depends on it.
            TraySettings.SaveHttpPort(8123);
            Assert.AreEqual(8123, TraySettings.HttpPort);
            TraySettings.HttpPort = 0;
            TraySettings.Load();
            Assert.AreEqual(8123, TraySettings.HttpPort, "http_port must survive a save/load cycle");
        }

        [Test]
        public void SaveHttpPort_ClampsOutOfRangeValues()
        {
            TraySettings.SaveHttpPort(-5);
            Assert.AreEqual(0, TraySettings.HttpPort, "negative port clamps to 0 (default)");
            TraySettings.SaveHttpPort(70000);
            Assert.AreEqual(65535, TraySettings.HttpPort, "oversized port clamps to 65535");
        }

        [Test]
        public void SaveDeclaredInstances_PreservesListOrder()
        {
            // v1.8.10: the "Manage" dialog reorder must survive persistence — the
            // JSON array order IS the menu order.
            var list = new List<DeclaredTopSolidInstance>
            {
                new DeclaredTopSolidInstance("Zulu", 8099),
                new DeclaredTopSolidInstance("Alpha", 8091),
                new DeclaredTopSolidInstance("Mike", 8095) { IsDefault = true }
            };
            TraySettings.SaveDeclaredInstances(list);
            TraySettings.DeclaredInstances.Clear();
            TraySettings.Load();
            Assert.AreEqual(3, TraySettings.DeclaredInstances.Count);
            Assert.AreEqual("Zulu", TraySettings.DeclaredInstances[0].Name);
            Assert.AreEqual("Alpha", TraySettings.DeclaredInstances[1].Name);
            Assert.AreEqual("Mike", TraySettings.DeclaredInstances[2].Name);
            Assert.IsTrue(TraySettings.DeclaredInstances[2].IsDefault);
        }

        [Test]
        public void Save_ReadOnlyToggledFromManageDialog_Persists()
        {
            // v1.8.10: read-only moved from the tray menu to the "Manage" dialog
            // checkbox — TraySettings.Save(port, readOnly) must keep persisting it.
            TraySettings.Save(8090, true);
            TraySettings.ReadOnly = false;
            TraySettings.Load();
            Assert.IsTrue(TraySettings.ReadOnly, "read_only must survive a save/load cycle");
        }

        [Test]
        public void Load_OldSettingsFile_PortFieldStillRead()
        {
            // A v1.8.2-era settings.json: only "port" + "read_only".
            File.WriteAllText(Path.Combine(_dir, "settings.json"),
                "{\n  \"port\": 8095,\n  \"read_only\": false\n}");
            TraySettings.Load();
            Assert.AreEqual(8095, TraySettings.Port, "legacy port field must keep working");
            Assert.AreEqual("auto", TraySettings.InstanceMode, "missing instance_mode = auto (default)");
        }

        [Test]
        public void Save_MissingDirectory_NeverThrows()
        {
            Assert.DoesNotThrow(() => TraySettings.SaveInstanceMode(8090, "manual"));
        }

        [Test]
        public void Save_CorruptFileAfterSave_LoadFallsBackToDefaults()
        {
            TraySettings.SaveInstanceMode(1234, "manual");
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ this is not json");
            Assert.DoesNotThrow(() => TraySettings.Load());
            Assert.AreEqual(0, TraySettings.Port);
            Assert.AreEqual("auto", TraySettings.InstanceMode);
        }

        [Test]
        public void DeclaredInstances_RoundTrip()
        {
            TraySettings.SaveDeclaredInstances(new List<DeclaredTopSolidInstance>
            {
                new DeclaredTopSolidInstance("TopSolid 7.21 atelier", 8091),
                new DeclaredTopSolidInstance("TopSolid 7.20 test", 8090)
            });
            TraySettings.Load();
            Assert.AreEqual(2, TraySettings.DeclaredInstances.Count);
            Assert.AreEqual("TopSolid 7.21 atelier", TraySettings.DeclaredInstances[0].Name);
            Assert.AreEqual(8091, TraySettings.DeclaredInstances[0].Port);
            Assert.AreEqual("TopSolid 7.20 test", TraySettings.DeclaredInstances[1].Name);
        }

        [Test]
        public void DeclaredInstances_EmptyList_PersistsAsEmptyArray()
        {
            TraySettings.SaveDeclaredInstances(new List<DeclaredTopSolidInstance>());
            TraySettings.Load();
            Assert.AreEqual(0, TraySettings.DeclaredInstances.Count);
        }

        [Test]
        public void SaveInstanceMode_WritesLegacyPortFieldToo()
        {
            // Program.cs reads TraySettings.Port at startup to build the
            // connector; the new mode must keep that field consistent.
            TraySettings.SaveInstanceMode(8091, "manual");
            string json = File.ReadAllText(Path.Combine(_dir, "settings.json"));
            StringAssert.Contains("\"port\": 8091", json, "the port field must remain present for Program.cs");
        }

        [Test]
        public void SaveInstanceMode_MarksMatchingDeclaredInstanceAsDefault()
        {
            // Picker click / "Set as default" flow: choosing port 8090 while two
            // declared instances exist must flag exactly the matching one.
            TraySettings.SaveDeclaredInstances(new List<DeclaredTopSolidInstance>
            {
                new DeclaredTopSolidInstance("TopSolid 7.21 atelier", 8091),
                new DeclaredTopSolidInstance("TopSolid 7.20 test", 8090)
            });
            TraySettings.SaveInstanceMode(8090, "manual");
            TraySettings.Load();

            Assert.IsFalse(TraySettings.DeclaredInstances[0].IsDefault, "instance on 8091 must NOT be default");
            Assert.IsTrue(TraySettings.DeclaredInstances[1].IsDefault, "instance on 8090 must be the default");
        }

        [Test]
        public void DeclaredInstances_DefaultFlag_RoundTrips()
        {
            TraySettings.SaveDeclaredInstances(new List<DeclaredTopSolidInstance>
            {
                new DeclaredTopSolidInstance("TopSolid 7.21 atelier", 8091) { IsDefault = true },
                new DeclaredTopSolidInstance("TopSolid 7.20 test", 8090)
            });
            TraySettings.Load();
            Assert.AreEqual(2, TraySettings.DeclaredInstances.Count);
            Assert.IsTrue(TraySettings.DeclaredInstances[0].IsDefault, "the default flag must survive save/load");
            Assert.IsFalse(TraySettings.DeclaredInstances[1].IsDefault);
        }

        [Test]
        public void DeclaredInstances_UnknownJsonFields_Ignored()
        {
            // Forward compat: a future settings.json adds a field we do not know.
            File.WriteAllText(Path.Combine(_dir, "settings.json"),
                "{\n  \"port\": 8095,\n  \"declared_instances\": [\n" +
                "    {\"name\": \"A\", \"port\": 8091, \"default\": true, \"color\": \"blue\"}\n" +
                "  ]\n}");
            TraySettings.Load();
            Assert.AreEqual(1, TraySettings.DeclaredInstances.Count);
            Assert.AreEqual("A", TraySettings.DeclaredInstances[0].Name);
            Assert.IsTrue(TraySettings.DeclaredInstances[0].IsDefault);
        }
    }
}