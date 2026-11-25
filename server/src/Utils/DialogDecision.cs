namespace TopSolidMcpServer.Utils
{
    /// <summary>What to do with one dialog box seen by the watch.</summary>
    public enum DialogDecision
    {
        /// <summary>A progress window: TopSolid is working, not waiting for an answer.</summary>
        Ignore,
        /// <summary>Close it with its only button and let the script continue.</summary>
        AutoClick,
        /// <summary>Return to the agent: the dialog asks a question or has a non-whitelisted button the user must handle.</summary>
        Report
    }
}
