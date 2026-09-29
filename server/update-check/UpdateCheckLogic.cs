using System;
using System.Net;

namespace TopSolidMcp.UpdateCheck
{
    /// <summary>
    /// Pure update-check logic extracted from the tray (TrayIcon.cs) so it can be
    /// unit-tested on CI machines that do not have TopSolid installed. The server
    /// references this assembly; the tray only forwards UI feedback.
    /// <para>
    /// Both helpers guard against the "dead update button" regression (v1.7.2 era):
    /// a stalled network call or a wrong version comparison made the tray look
    /// inert with no feedback.
    /// </para>
    /// </summary>
    public static class UpdateCheckLogic
    {
        /// <summary>
        /// Download a string with a hard timeout. <see cref="WebClient"/> has no usable
        /// per-call timeout (its 100s default made a stalled DNS lookup freeze the
        /// update check silently), so the download runs on the thread pool and the
        /// call aborts after <paramref name="timeout"/>.
        /// <para>The real exception is unwrapped from the AggregateException so the
        /// user-facing message is the actual cause ("DNS name not resolved"), not
        /// "One or more errors occurred".</para>
        /// </summary>
        public static string DownloadStringWithTimeout(WebClient client, string url, TimeSpan timeout)
        {
            var task = System.Threading.Tasks.Task.Factory.StartNew(
                () => client.DownloadString(url));
            try
            {
                if (!task.Wait(timeout))
                    throw new TimeoutException("GitHub request timed out after " + (int)timeout.TotalSeconds + "s");
            }
            catch (System.AggregateException agg)
            {
                throw agg.GetBaseException();
            }
            return task.Result;
        }

        /// <summary>
        /// Semver-ish comparison: 1.7.2-beta is older than 1.7.2; build metadata
        /// (anything after '+') is ignored; a 'v' prefix is ignored; null is
        /// tolerated as "0.0.0"; non-numeric segments compare as 0. Unlike the
        /// original tray implementation, ANY number of segments is compared
        /// (1.7.4.2 &gt; 1.7.4.1).
        /// </summary>
        public static int CompareVersions(string a, string b)
        {
            a = (a ?? "0.0.0").TrimStart('v', 'V');
            b = (b ?? "0.0.0").TrimStart('v', 'V');
            a = a.Split('+')[0];
            b = b.Split('+')[0];

            string preA = null, preB = null;
            int dash = a.IndexOf('-');
            if (dash >= 0) { preA = a.Substring(dash + 1); a = a.Substring(0, dash); }
            dash = b.IndexOf('-');
            if (dash >= 0) { preB = b.Substring(dash + 1); b = b.Substring(0, dash); }

            var sa = a.Split('.');
            var sb = b.Split('.');
            int len = Math.Max(sa.Length, sb.Length);
            for (int i = 0; i < len; i++)
            {
                int na = i < sa.Length && int.TryParse(sa[i], out int xa) ? xa : 0;
                int nb = i < sb.Length && int.TryParse(sb[i], out int xb) ? xb : 0;
                if (na < nb) return -1;
                if (na > nb) return 1;
            }
            // Release outranks its pre-releases
            if (preA == preB) return 0;
            if (preA == null) return 1;
            if (preB == null) return -1;
            return string.CompareOrdinal(preA, preB);
        }
    }
}