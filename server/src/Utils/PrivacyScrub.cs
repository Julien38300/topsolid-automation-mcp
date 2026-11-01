using System;
using System.Text.RegularExpressions;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Privacy scrubbing for everything that leaves the machine: the GitHub issue
    /// bodies built by the tray's "Report a bug / Suggest a feature" flow
    /// (GitHubFeedbackLogic.BuildBody) and the crash reports written next to
    /// server.log (CrashReport.BuildCrashReport, later attached to an issue).
    ///
    /// The API key is redacted by GitHubFeedbackLogic.ScrubKey and
    /// ServerLog.ScrubLine; this class closes the other half (issue #15): the
    /// Windows user profile directory (C:\Users\%ACCOUNT%) must never reach a
    /// public repository, since it discloses the reporter's account name. Every
    /// occurrence is replaced by %USERPROFILE%, case-insensitively and with both
    /// "\" and "/" separators.
    ///
    /// Pure functions only: no I/O, source-linked into TopSolidMcp.Http.Tests.
    /// </summary>
    public static class PrivacyScrub
    {
        /// <summary>Placeholder substituted for the user profile directory.</summary>
        public const string ProfilePlaceholder = "%USERPROFILE%";

        /// <summary>
        /// Resolves the profile directory to redact: the explicit value when one
        /// is given (unit tests inject deterministic paths), otherwise the
        /// profile of the user running the process.
        /// </summary>
        public static string ResolveProfile(string userProfilePath)
        {
            if (!string.IsNullOrEmpty(userProfilePath)) return userProfilePath;
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        /// <summary>
        /// Replaces every occurrence of the profile directory by %USERPROFILE%:
        /// case-insensitive, backslash and forward-slash separators (and any mix
        /// of both), so absolute paths under the profile never disclose the
        /// Windows account name. Unusable inputs (null/empty text or profile,
        /// bare drive roots like "C:") leave the text unchanged.
        /// </summary>
        public static string ScrubUserProfile(string text, string profilePath)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(profilePath)) return text;
            string path = profilePath.TrimEnd('\\', '/');
            // Guard: without a separator the pattern would rewrite unrelated
            // paths (profile "C:" would turn "C:\Program Files" into
            // "%USERPROFILE%\Program Files").
            if (path.Length < 3 || (path.IndexOf('\\') < 0 && path.IndexOf('/') < 0)) return text;
            // Regex.Escape maps every "\" to "\\" — fold each escaped backslash
            // into a [\\/] class so C:\Users\username and C:/Users/username both match.
            string pattern = Regex.Escape(path).Replace("\\\\", "[\\\\/]");
            return Regex.Replace(text, pattern, ProfilePlaceholder,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}