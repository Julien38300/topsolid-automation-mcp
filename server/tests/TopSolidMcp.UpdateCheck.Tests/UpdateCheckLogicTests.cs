using System;
using System.Net;
using System.Threading;
using NUnit.Framework;
using TopSolidMcp.UpdateCheck;

namespace TopSolidMcp.UpdateCheck.Tests
{
    /// <summary>
    /// Unit tests for the pure update-check logic (no TopSolid assemblies needed,
    /// so these run on CI). They pin the contract guarding the "dead update
    /// button" regression (v1.7.2 era): a stalled network call or a wrong
    /// version comparison made the tray look inert with no feedback.
    /// </summary>
    [TestFixture]
    public class UpdateCheckLogicTests
    {
        // ---------------------------------------------------------------
        // CompareVersions — a wrong comparison either proposes a downgrade
        // or hides an available update.
        // ---------------------------------------------------------------
        [Test]
        public void CompareVersions_CurrentIsOlder_ReturnsNegative()
        {
            int r = UpdateCheckLogic.CompareVersions("1.7.3", "1.7.4");
            Assert.Less(r, 0, "1.7.3 must be detected as older than 1.7.4");
        }

        [Test]
        public void CompareVersions_CurrentIsNewer_ReturnsPositive()
        {
            int r = UpdateCheckLogic.CompareVersions("1.7.4", "1.7.3");
            Assert.Greater(r, 0, "1.7.4 must be detected as newer than 1.7.3");
        }

        [Test]
        public void CompareVersions_Equal_ReturnsZero()
        {
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("1.7.4", "1.7.4"));
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("v1.7.4", "1.7.4"),
                "A 'v' prefix must be ignored");
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions("1.7.4+build.123", "1.7.4"),
                "Build metadata must be ignored");
        }

        [Test]
        public void CompareVersions_PreRelease_OlderThanRelease()
        {
            int r = UpdateCheckLogic.CompareVersions("1.7.4-beta", "1.7.4");
            Assert.Less(r, 0, "A pre-release must never be considered up-to-date");
        }

        [Test]
        public void CompareVersions_Release_NewerThanPreRelease()
        {
            int r = UpdateCheckLogic.CompareVersions("1.7.4", "1.7.4-beta");
            Assert.Greater(r, 0);
        }

        [Test]
        public void CompareVersions_Null_TreatedAsZero()
        {
            int r = UpdateCheckLogic.CompareVersions(null, "0.0.1");
            Assert.Less(r, 0, "null must be tolerated (treated as 0.0.0), not crash");
            Assert.Greater(UpdateCheckLogic.CompareVersions("0.1.0", null), 0);
            Assert.AreEqual(0, UpdateCheckLogic.CompareVersions(null, null));
        }

        [Test]
        public void CompareVersions_DifferentLengths()
        {
            // The original tray implementation compared only 3 segments, so
            // 1.7.4.2 vs 1.7.4.1 compared as equal. Fixed: any segment count.
            Assert.Less(UpdateCheckLogic.CompareVersions("1.7.4.1", "1.7.4.2"), 0);
            Assert.Greater(UpdateCheckLogic.CompareVersions("1.7.4.2", "1.7.4.1"), 0);
            Assert.Greater(UpdateCheckLogic.CompareVersions("1.7.5", "1.7.4.9"), 0);
        }

        // ---------------------------------------------------------------
        // DownloadStringWithTimeout — the update check must never hang
        // (the pre-v1.7.4 code had no timeout at all).
        // ---------------------------------------------------------------
        [Test]
        public void DownloadStringWithTimeout_ReturnsContent()
        {
            using (var server = new HttpListenerFixture("hello-release-json"))
            {
                using (var client = new WebClient())
                {
                    string result = UpdateCheckLogic.DownloadStringWithTimeout(
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
                        UpdateCheckLogic.DownloadStringWithTimeout(
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
                    UpdateCheckLogic.DownloadStringWithTimeout(
                        client, "http://nonexistent.invalid/latest-release.json",
                        TimeSpan.FromSeconds(10));
                });
                sw.Stop();
                Assert.Less(sw.ElapsedMilliseconds, 10000,
                    "DNS failure must surface fast, not eat the whole timeout");
            }
        }

        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        /// <summary>Local HTTP server that immediately serves the given body.</summary>
        private sealed class HttpListenerFixture : IDisposable
        {
            private readonly HttpListener _listener;
            private readonly Thread _thread;

            public string Url { get; }

            public HttpListenerFixture(string body)
            {
                var port = GetFreePort();
                Url = "http://localhost:" + port + "/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _thread = new Thread(() =>
                {
                    try
                    {
                        while (_listener.IsListening)
                        {
                            var ctx = _listener.GetContext();
                            var buf = System.Text.Encoding.UTF8.GetBytes(body);
                            ctx.Response.ContentLength64 = buf.Length;
                            ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            ctx.Response.OutputStream.Close();
                        }
                    }
                    catch (HttpListenerException) { /* stopped */ }
                    catch (ObjectDisposedException) { /* stopped */ }
                });
                _thread.IsBackground = true;
                _thread.Start();
            }

            public void Dispose()
            {
                try { _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }
            }
        }

        /// <summary>TCP server that accepts connections but never replies — pins the timeout contract.</summary>
        private sealed class HangingServerFixture : IDisposable
        {
            private readonly System.Net.Sockets.TcpListener _tcp;

            public string Url { get; }

            public HangingServerFixture()
            {
                var port = GetFreePort();
                Url = "http://localhost:" + port + "/";
                _tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
                _tcp.Start();
                var acceptThread = new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            var client = _tcp.AcceptTcpClient(); // accept, never respond
                        }
                    }
                    catch (System.Net.Sockets.SocketException) { /* stopped */ }
                });
                acceptThread.IsBackground = true;
                acceptThread.Start();
            }

            public void Dispose()
            {
                try { _tcp.Stop(); } catch { }
            }
        }

        private static int GetFreePort()
        {
            var s = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            s.Start();
            int port = ((IPEndPoint)s.LocalEndpoint).Port;
            s.Stop();
            return port;
        }
    }
}