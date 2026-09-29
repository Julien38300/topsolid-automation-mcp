using System;
using System.Net;
using System.Threading;
using NUnit.Framework;
using TopSolidMcp.UpdateCheck;

namespace TopSolidMcpServer.Tests
{
    [TestFixture]
    public class TrayIconUpdateLogicTests
    {
        // NOTE: delegation from TrayIcon → UpdateCheckLogic is covered on CI
        // (TopSolidMcp.UpdateCheck.Tests) where Smart App Control doesn't run.
        // Local runs pin only the pure logic, since SAC on dev machines blocks
        // freshly built TopSolidMcpServer.exe by hash (FileLoadException 0x800711C7).
        private static int CompareVersions(string a, string b)
        {
            return UpdateCheckLogic.CompareVersions(a, b);
        }
        // ---------------------------------------------------------------
        // CompareVersions — regression guard for the update-check flow.
        // A wrong comparison either proposes a downgrade or hides an
        // available update (the exact bug fixed in v1.7.4's area).
        // ---------------------------------------------------------------
        [Test]
        public void CompareVersions_CurrentIsOlder_ReturnsNegative()
        {
            int r = UpdateCheckLogic.CompareVersions("1.7.3", "1.7.4");
            Assert.Less(r, 0, "1.7.3 vs 1.7.4 must compare as older (update available)");
        }

        [Test]
        public void CompareVersions_EqualVersions_ReturnsZero()
        {
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("1.7.4", "1.7.4"));
        }

        [Test]
        public void CompareVersions_IgnoresVPrefix()
        {
            // GitHub tags are commonly "v1.7.4" while the assembly version is "1.7.4"
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("1.7.4", "v1.7.4"));
            Assert.Less(UpdateCheckLogic.CompareVersions("v1.7.4", "v1.7.5"), 0);
        }

        [Test]
        public void CompareVersions_NullTolerated()
        {
            // A missing remote version must never crash the tray (regression of v1.7.2-era dead button)
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions(null, "0.0.0"));
            Assert.Less(UpdateCheckLogic.CompareVersions(null, "1.0.0"), 0);
        }

        [Test]
        public void CompareVersions_PrereleaseOlderThanRelease()
        {
            // 1.7.2-beta < 1.7.2 (semver-ish behaviour documented on the method)
            Assert.Less(UpdateCheckLogic.CompareVersions("1.7.2-beta", "1.7.2"), 0);
            Assert.Greater(UpdateCheckLogic.CompareVersions("1.7.3", "1.7.2-beta"), 0);
        }

        [Test]
        public void CompareVersions_NumericComparisonNotLexical()
        {
            // Lexical string comparison would say "1.10.0" < "1.9.0" — must not happen
            Assert.Greater(UpdateCheckLogic.CompareVersions("1.10.0", "1.9.0"), 0);
            Assert.Less(UpdateCheckLogic.CompareVersions("1.9.0", "1.10.0"), 0);
        }

        [Test]
        public void CompareVersions_MajorMinorOnly_Padded()
        {
            // "1.7" vs "1.7.4": shorter version padded with zeros
            Assert.Less(UpdateCheckLogic.CompareVersions("1.7", "1.7.4"), 0);
            Assert.Greater(UpdateCheckLogic.CompareVersions("1.7.4", "1.7"), 0);
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("1.7", "1.7.0"));
        }

        [Test]
        public void CompareVersions_DifferentLengths()
        {
            Assert.Less(UpdateCheckLogic.CompareVersions("1.7.4.1", "1.7.4.2"), 0);
            Assert.Greater(UpdateCheckLogic.CompareVersions("1.7.4.2", "1.7.4.1"), 0);
        }

        [Test]
        public void CompareVersions_IgnoresBuildMetadata()
        {
            // semver build metadata is ignored: 1.7.4+build123 == 1.7.4
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("1.7.4+build.123", "1.7.4"));
        }

        // ---------------------------------------------------------------
        // DownloadStringWithTimeout — the network call used by the update
        // check. The v1.7.2 "dead button" bug was exactly this call stalling
        // with no timeout. These tests pin the contract: it must return
        // within the timeout even when the download hangs.
        // ---------------------------------------------------------------
        private static string DownloadWithTimeout(System.Net.WebClient c, string url, TimeSpan t)
        {
            // Both surfaces must behave identically; pin the delegation.
            return UpdateCheckLogic.DownloadStringWithTimeout(c, url, t);
        }

        [Test]
        public void DownloadStringWithTimeout_ReturnsContent()
        {
            // A local HTTP listener that responds normally
            using (var server = new HttpListenerFixture("hello-release-json"))
            {
                using (var client = new WebClient())
                {
                    string result = DownloadWithTimeout(
                        client, server.Url, TimeSpan.FromSeconds(10));
                    Assert.AreEqual("hello-release-json", result);
                }
            }
        }

        [Test]
        public void DownloadStringWithTimeout_HangingServer_ThrowsTimeout()
        {
            // Server accepts the connection but never responds: the call must
            // abort after the timeout instead of freezing the tray silently
            using (var server = new HangingServerFixture())
            {
                using (var client = new WebClient())
                {
                    Assert.Throws<TimeoutException>(() =>
                    {
                        DownloadWithTimeout(
                            client, server.Url, TimeSpan.FromSeconds(2));
                    }, "A stalled server must produce a TimeoutException, not an infinite hang");
                }
            }
        }

        [Test]
        public void DownloadStringWithTimeout_DnsFailure_ThrowsQuickly()
        {
            // Root cause seen on LY458: unresolvable host. Must throw (WebException),
            // quickly — never hang, never silently swallow.
            using (var client = new WebClient())
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Assert.Throws<WebException>(() =>
                {
                    DownloadWithTimeout(
                        client, "http://nonexistent.invalid/latest-release.json",
                        TimeSpan.FromSeconds(10));
                });
                sw.Stop();
                Assert.Less(sw.ElapsedMilliseconds, 10000,
                    "DNS failure must surface fast, not eat the whole timeout");
            }
        }

        // ---------------------------------------------------------------
        // Test fixtures
        // ---------------------------------------------------------------

        /// <summary>Minimal local HTTP server on a random port.</summary>
        private sealed class HttpListenerFixture : IDisposable
        {
            private readonly HttpListener _listener;
            private readonly Thread _serverThread;
            public string Url { get; }

            public HttpListenerFixture(string response)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:0/");
                // HttpListener needs an explicit port; find a free one
                int port = GetFreePort();
                Url = "http://localhost:" + port + "/";
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _serverThread = new Thread(() =>
                {
                    try
                    {
                        while (_listener.IsListening)
                        {
                            var ctx = _listener.GetContext();
                            byte[] buf = System.Text.Encoding.UTF8.GetBytes(response);
                            ctx.Response.ContentLength64 = buf.Length;
                            ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            ctx.Response.OutputStream.Close();
                        }
                    }
                    catch (Exception) { /* listener closed */ }
                }) { IsBackground = true };
                _serverThread.Start();
            }

            private static int GetFreePort()
            {
                var s = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                s.Start();
                int port = ((System.Net.IPEndPoint)s.LocalEndpoint).Port;
                s.Stop();
                return port;
            }

            public void Dispose()
            {
                try { if (_listener != null) _listener.Stop(); } catch { }
            }
        }

        /// <summary>TCP server that accepts connections but never writes a response.</summary>
        private sealed class HangingServerFixture : IDisposable
        {
            private readonly System.Net.Sockets.TcpListener _listener;
            public string Url { get; }

            public HangingServerFixture()
            {
                _listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                int port = ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
                Url = "http://localhost:" + port + "/latest-release.json";
                var t = new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            var client = _listener.AcceptTcpClient(); // accept, never respond
                        }
                    }
                    catch (Exception) { /* closed */ }
                }) { IsBackground = true };
                t.Start();
            }

            public void Dispose()
            {
                try { _listener.Stop(); } catch { }
            }
        }
    }
}