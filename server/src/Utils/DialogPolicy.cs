namespace TopSolidMcpServer.Utils
{
    /// <summary>What the server does when TopSolid opens a modal dialog box while a script runs.</summary>
    public enum DialogPolicy
    {
        /// <summary>No watch: the call waits until the timeout, as before 1.8.24.</summary>
        Off,
        /// <summary>The call returns at once with the dialog's text and buttons; nothing is clicked.</summary>
        Report,
        /// <summary>
        /// As Report, but a dialog with a single benign button (OK, Fermer, Close, Continuer, Continue)
        /// is closed with that button, its text is kept for the result, and the script carries on. The default.
        /// </summary>
        Acknowledge
    }
}
