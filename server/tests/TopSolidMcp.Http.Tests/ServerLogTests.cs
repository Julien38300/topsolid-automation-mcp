using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// ServerLog / CrashReport decision logic (v1.8.1): file log rotation, formatting,
    /// scrubbing and the crash report builder. The v1.8.0 tray promised "recent log
    /// lines" in bug reports but Program.cs never redirected stderr: server.log did
    /// not exist, RecentLogLines() returned nothing and every tray bug report shipped
    /// without its log. These tests pin the behaviour the feedback flow relies on.
    /// </summary>
    [TestFixture]
    public class ServerLogTests
    {
        // ---- Formatting ----

        [Test]
        public void FormatLine_PrefixesUtcTimestampAndCrlf()
        {
            string line = ServerLog.FormatLine("[MCP-INFO] ready");
            // "2026-10-06 18:07:31.123 [MCP-INFO] ready\r\n"
            Assert.IsTrue(line.EndsWith("[MCP-INFO] ready\r\n"));
            Assert.AreEqual(23, line.IndexOf(" [MCP-INFO]")); // timestamp field is 23 chars + separator space
            Assert.AreEqual(' ', line[10]); // date / time separator inside the stamp
        }

        [Test]
        public void FormatLine_NullLine_BecomesTimestampOnly()
        {
            string line = ServerLog.FormatLine(null);
            Assert.IsTrue(line.EndsWith("\r\n"));
            Assert.IsFalse(line.Contains("null"));
        }

        // ---- Scrubbing (the log feeds GitHub issues: the key must never land in it) ----

        [Test]
        public void ScrubLine_ReplacesApiKeyEverywhere()
        {
            string key = "TOPSOLID_secret_KEY_0123456789abcdef";
            string line = "X-API-Key: " + key + " and again " + key;
            Assert.AreEqual("X-API-Key: *** and again ***", ServerLog.ScrubLine(line, key));
        }

        [Test]
        public void ScrubLine_EmptyKeyOrLine_NoChange()
        {
            Assert.AreEqual("hello", ServerLog.ScrubLine("hello", ""));
            Assert.AreEqual("", ServerLog.ScrubLine("", "key"));
            Assert.IsNull(ServerLog.ScrubLine(null, "key"));
        }

        // ---- Rotation ----

        [Test]
        public void ShouldRotate_TrueOnlyPastMaxBytes()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tsm-log-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "server.log");
                File.WriteAllText(p, new string('a', 1000));
                long size;
                Assert.IsFalse(ServerLog.ShouldRotate(p, out size));
                Assert.AreEqual(1000, size);

                File.WriteAllText(p, new string('a', (int)ServerLog.MaxBytes + 1));
                Assert.IsTrue(ServerLog.ShouldRotate(p, out size));

                string missing = Path.Combine(dir, "nope.log");
                Assert.IsFalse(ServerLog.ShouldRotate(missing, out size));
                Assert.AreEqual(0, size);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void Rotate_RenamesToOld_AndDeletesPreviousOld()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tsm-log-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "server.log");
                string old = p + ".old";
                File.WriteAllText(p, "new content");
                File.WriteAllText(old, "previous rotation");
                ServerLog.Rotate(p);
                Assert.IsFalse(File.Exists(p), "server.log must be moved away by Rotate");
                Assert.IsTrue(File.Exists(old), "server.log.old must exist after Rotate");
                Assert.AreEqual("new content", File.ReadAllText(old), "the .old must hold the PREVIOUS main log");
            }
            finally { Directory.Delete(dir, true); }
        }

        // ---- ReadLastLines ----

        [Test]
        public void ReadLastLines_ReturnsTailOnly()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tsm-log-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "server.log");
                File.WriteAllLines(p, new[] { "l1", "l2", "l3", "l4", "l5" });
                IList<string> tail = ServerLog.ReadLastLines(p, 3);
                CollectionAssert.AreEqual(new[] { "l3", "l4", "l5" }, tail);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void ReadLastLines_MissingFile_EmptyList()
        {
            IList<string> lines = ServerLog.ReadLastLines(
                Path.Combine(Path.GetTempPath(), "tsm-nolog-" + Path.GetRandomFileName() + ".log"), 5);
            Assert.IsNotNull(lines);
            Assert.AreEqual(0, lines.Count);
        }

        // ---- Crash report builder ----

        [Test]
        public void BuildCrashReport_IncludesExceptionChainAndScrubbedLog()
        {
            var inner = new InvalidOperationException("inner detail");
            var ex = new Exception("outer boom", inner);
            var log = new List<string> { "[MCP-INFO] ready", "X-API-Key: TOPSOLID_secret_KEY_0123456789abcdef" };
            string report = CrashReport.BuildCrashReport(ex, "1.8.1", log, "TOPSOLID_secret_KEY_0123456789abcdef");

            StringAssert.Contains("outer boom", report);
            StringAssert.Contains("InvalidOperationException", report);
            StringAssert.Contains("inner detail", report);
            StringAssert.Contains("Version: 1.8.1", report);
            StringAssert.Contains("[MCP-INFO] ready", report);
            StringAssert.DoesNotContain("TOPSOLID_secret_KEY", report);
            StringAssert.Contains("***", report);
        }

        [Test]
        public void GetCrashPath_NextToServerLog_WithTimestamp()
        {
            string p = CrashReport.GetCrashPath(new DateTime(2026, 10, 6, 16, 5, 42));
            StringAssert.Contains("crash-20261006-160542.txt", p);
            StringAssert.Contains("TopSolidMcp", p);
        }
    }
}