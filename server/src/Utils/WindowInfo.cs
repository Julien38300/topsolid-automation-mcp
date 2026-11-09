using System;
using System.Collections.Generic;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// A top-level window of a TopSolid process, as seen from Win32.
    /// </summary>
    public sealed class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public int ProcessId { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }

        /// <summary>Owner window (GW_OWNER), or IntPtr.Zero for an unowned window.</summary>
        public IntPtr Owner { get; set; }

        /// <summary>True when the owner window accepts input; a modal dialog disables its owner.</summary>
        public bool OwnerEnabled { get; set; }

        public List<string> Texts { get; } = new List<string>();
        public List<string> Buttons { get; } = new List<string>();
    }
}
