using System;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Decides whether the process keeps running once the stdio loop ended
    /// (stdin EOF). Pure decision, unit-tested; Program.cs applies it.
    ///
    /// The native HTTP endpoint (v1.8.0) starts BEFORE the stdio loop. When the
    /// process is started detached (scheduled task, no stdio client), stdin
    /// reaches EOF immediately; with the http-standalone mode the HTTP endpoint
    /// must keep the process alive instead. A stdio client spawn (Claude
    /// Desktop…) intentionally does NOT get that behavior: its EOF means "the
    /// client is gone", and the process must exit so the next spawn can take
    /// the singleton mutex.
    /// </summary>
    internal static class HttpKeepalivePolicy
    {
        /// <summary>
        /// True when the process must keep running after stdin EOF.
        /// </summary>
        /// <param name="httpStandalone">--http-standalone flag / TOPSOLID_MCP_HTTP_STANDALONE set.</param>
        /// <param name="httpLive">Native HTTP endpoint successfully bound and running.</param>
        public static bool KeepRunningAfterStdinEof(bool httpStandalone, bool httpLive)
        {
            return httpStandalone && httpLive;
        }
    }
}