using NUnit.Framework;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// HttpKeepalivePolicy: shutdown semantics after the stdio loop ends (stdin EOF).
    ///
    /// Context (v1.8.0 regression found by runtime probe on the release staging exe):
    /// the native HTTP endpoint starts before the stdio loop, but the process only
    /// lives as long as stdin delivers lines. A detached start (scheduled task,
    /// no stdio client) got an instant stdin EOF and silently killed the HTTP
    /// server that had just started.
    ///
    /// Contract:
    /// - http-standalone + HTTP endpoint live → keep running (the HTTP server IS
    ///   the transport; stdin EOF just means "started detached").
    /// - http-standalone + HTTP endpoint NOT live (port taken, bind failure) →
    ///   exit: a process serving nothing must not hold the singleton mutex.
    /// - NO standalone flag + HTTP live → exit anyway: a stdio client spawn that
    ///   loses stdin must release the singleton mutex for the next client.
    /// </summary>
    [TestFixture]
    public class HttpKeepalivePolicyTests
    {
        [Test]
        public void Standalone_WithLiveHttp_KeepsRunning()
        {
            Assert.IsTrue(TopSolidMcpServer.Protocol.HttpKeepalivePolicy.KeepRunningAfterStdinEof(true, true));
        }

        [Test]
        public void Standalone_WithHttpBindFailure_Exits()
        {
            Assert.IsFalse(TopSolidMcpServer.Protocol.HttpKeepalivePolicy.KeepRunningAfterStdinEof(true, false));
        }

        [Test]
        public void StdioClientSpawn_WithLiveHttp_Exits()
        {
            // Regression guard: a stdio MCP client (Claude Desktop…) that closes
            // stdin must make the process exit so the next spawn can take the mutex,
            // even though the process also owns a live HTTP endpoint.
            Assert.IsFalse(TopSolidMcpServer.Protocol.HttpKeepalivePolicy.KeepRunningAfterStdinEof(false, true));
        }

        [Test]
        public void StdioClientSpawn_WithHttpDown_Exits()
        {
            // Pre-v1.8.0 behavior must be preserved exactly.
            Assert.IsFalse(TopSolidMcpServer.Protocol.HttpKeepalivePolicy.KeepRunningAfterStdinEof(false, false));
        }
    }
}