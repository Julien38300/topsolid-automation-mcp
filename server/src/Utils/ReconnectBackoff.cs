using System;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Pure reconnect backoff computation for the automatic TopSolid connection.
    /// Unit-tested via source link (see server/tests/TopSolidMcpServer.Tests).
    /// </summary>
    public static class ReconnectBackoff
    {
        /// <summary>Delays in seconds: 15, 30, 60, then 60 forever (cap).</summary>
        private static readonly int[] Steps = { 15, 30, 60 };

        /// <summary>
        /// Delay before attempt N (1-based): steps 15s/30s/60s, capped at 60s.
        /// Invalid attempt numbers return the cap (fail safe = keep trying).
        /// </summary>
        public static TimeSpan DelayFor(int attempt)
        {
            if (attempt < 1) return TimeSpan.FromSeconds(60);
            int idx = Math.Min(attempt - 1, Steps.Length - 1);
            return TimeSpan.FromSeconds(Steps[idx]);
        }

        /// <summary>
        /// True when the connector should auto-retry at the given moment:
        /// autoconnect enabled, currently disconnected, and the due time reached.
        /// </summary>
        public static bool ShouldRetry(bool autoConnectEnabled, bool isConnected, DateTime lastAttemptUtc, DateTime nowUtc, int attempt)
        {
            if (!autoConnectEnabled) return false;
            if (isConnected) return false;
            return nowUtc >= lastAttemptUtc + DelayFor(attempt);
        }
    }
}