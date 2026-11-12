using System;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// TopSolid kernel-version helpers for recipes whose body calls Automation methods
    /// that only exist from a given version (GH #25). The Automation API encodes a
    /// version as ((Major * 100 + Minor) * 1000 + Build) * 1000 + Revision
    /// (7.21.304.0 -> 721304000, 7.20.400.161 -> 720400161), and TopSolidHost.Version
    /// returns the version of the CONNECTED instance (0 when unknown).
    /// </summary>
    internal static class KernelVersion
    {
        /// <summary>
        /// Decodes the Automation encoding back to a human-readable "M.m.b.r" string.
        /// Zero or negative values (not connected) are returned as-is.
        /// </summary>
        internal static string Format(int version)
        {
            if (version <= 0) return version.ToString();
            int revision = version % 1000;
            version /= 1000;
            int build = version % 1000;
            version /= 1000;
            int minor = version % 100;
            int major = version / 100;
            return major + "." + minor + "." + build + "." + revision;
        }

        /// <summary>
        /// Version gate for recipes whose body calls version-specific Automation methods.
        /// Returns the refusal message when the connected instance is older than the
        /// recipe needs, or null when the recipe may run. The gate lives server-side on
        /// purpose: the recipe body is compiled at execution time against the locally
        /// installed interop DLLs, so a body calling newer members does not even
        /// compile against older interop DLLs - and reflection is a blocked API in scripts,
        /// so the body cannot detect the gap itself.
        /// </summary>
        internal static string BuildRecipeRefusal(string recipeName, int minKernelVersion, int runningVersion)
        {
            if (minKernelVersion <= 0 || runningVersion >= minKernelVersion) return null;
            return "Not available on this TopSolid: recipe '" + recipeName + "' requires TopSolid " +
                Format(minKernelVersion) + " or newer (the Automation methods it calls do not " +
                "exist in older versions). Connected TopSolid version: " +
                (runningVersion > 0 ? Format(runningVersion) : "unknown") + ".";
        }
    }
}
