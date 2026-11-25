using System;
using System.Collections.Generic;
using System.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Follows the modal dialogs seen at each poll and says which one has stayed open longer than the
    /// grace period. Windows open before the script started are not the script's doing and are skipped.
    /// </summary>
    public sealed class DialogTracker
    {
        private readonly int _graceMs;
        private readonly HashSet<IntPtr> _skipped;
        private readonly Dictionary<IntPtr, long> _firstSeen = new Dictionary<IntPtr, long>();

        public DialogTracker(IEnumerable<IntPtr> openBefore, int graceMs)
        {
            _graceMs = Math.Max(0, graceMs);
            _skipped = new HashSet<IntPtr>(openBefore ?? Enumerable.Empty<IntPtr>());
        }

        /// <summary>The handles open for at least the grace period and not handled yet.</summary>
        public List<IntPtr> Update(IEnumerable<IntPtr> openNow, long nowMs)
        {
            var open = new HashSet<IntPtr>(openNow ?? Enumerable.Empty<IntPtr>());
            foreach (var gone in _firstSeen.Keys.Where(h => !open.Contains(h)).ToList())
                _firstSeen.Remove(gone);

            var due = new List<IntPtr>();
            foreach (var h in open)
            {
                if (_skipped.Contains(h)) continue;
                long first;
                if (!_firstSeen.TryGetValue(h, out first)) { _firstSeen[h] = nowMs; first = nowMs; }
                if (nowMs - first >= _graceMs) due.Add(h);
            }
            return due;
        }

        /// <summary>Never act on this window again (handled, or a progress window).</summary>
        public void Skip(IntPtr handle)
        {
            _skipped.Add(handle);
            _firstSeen.Remove(handle);
        }
    }
}
