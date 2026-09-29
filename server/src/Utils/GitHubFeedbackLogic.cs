using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Pure builders for the tray's "Report a bug / Suggest a feature" flow.
    /// Builds the GitHub issue payload (or the prefilled new-issue URL when no
    /// PAT is configured). No I/O, unit-tested via source link.
    /// </summary>
    public static class GitHubFeedbackLogic
    {
        public const string RepoOwner = "Julien38300";
        public const string RepoName = "topsolid-automation-mcp";

        /// <summary>Categories offered in the tray.</summary>
        public const string CategoryBug = "bug";
        public const string CategoryFeature = "feature";

        /// <summary>
        /// GitHub issue title: "[bug] …" or "[feature] …", truncated to 100 chars.
        /// </summary>
        public static string BuildTitle(string category, string subject)
        {
            string tag = category == CategoryFeature ? "feature" : "bug";
            string s = (subject ?? "").Trim().Replace('\n', ' ');
            if (s.Length > 90) s = s.Substring(0, 90);
            string title = "[" + tag + "] " + s;
            return title.Length > 100 ? title.Substring(0, 100) : title;
        }

        /// <summary>
        /// Issue body: user description + an environment block with version, OS,
        /// connection state and recent log lines. Never includes the API key
        /// (logs are scrubbed) — defense in depth against leaking secrets.
        /// </summary>
        public static string BuildBody(string userText, string serverVersion, string osVersion,
            bool topsolidConnected, int topsolidPort, IList<string> recentLogLines, string keyToScrub)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Description");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(userText) ? "_(pas de description)_" : ScrubKey(userText.Trim(), keyToScrub));
            sb.AppendLine();
            sb.AppendLine("## Environnement (auto)");
            sb.AppendLine();
            sb.AppendLine("- Version serveur : `" + serverVersion + "`");
            sb.AppendLine("- OS : `" + osVersion + "`");
            sb.AppendLine("- TopSolid connecté : " + (topsolidConnected ? "oui" : "non") +
                         " (port " + topsolidPort + ")");
            sb.AppendLine("- Date : " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
            if (recentLogLines != null && recentLogLines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Dernières lignes de log");
                sb.AppendLine();
                sb.AppendLine("```");
                foreach (string line in recentLogLines)
                {
                    string safe = ScrubKey(line, keyToScrub);
                    if (!string.IsNullOrEmpty(safe)) sb.AppendLine(safe);
                }
                sb.AppendLine("```");
            }
            sb.AppendLine();
            sb.AppendLine("_(issue créée depuis le tray TopSolid MCP)_");
            return sb.ToString();
        }

        /// <summary>
        /// Replaces the API key by *** everywhere in a line. Defense in depth:
        /// the key should never be logged, but if it ever leaks into the log
        /// buffer it must not reach GitHub.
        /// </summary>
        public static string ScrubKey(string line, string key)
        {
            if (string.IsNullOrEmpty(line)) return line;
            if (string.IsNullOrEmpty(key)) return line;
            return line.Replace(key, "***");
        }

        /// <summary>
        /// JSON payload for POST /repos/{owner}/{repo}/issues.
        /// </summary>
        public static string BuildIssueJson(string title, string body, string[] labels)
        {
            var obj = new JObject
            {
                ["title"] = title,
                ["body"] = body
            };
            if (labels != null && labels.Length > 0)
            {
                var arr = new JArray();
                foreach (var l in labels) arr.Add(l);
                obj["labels"] = arr;
            }
            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// Prefilled URL for the web "new issue" form (fallback when no PAT).
        /// Uses templates so the browser lands on a structured form.
        /// </summary>
        public static string BuildNewIssueUrl(string category, string title, string body)
        {
            string template = category == CategoryFeature ? "feature_request.md" : "bug_report.md";
            var parms = new Dictionary<string, string>
            {
                ["template"] = template,
                ["title"] = title,
                ["body"] = body
            };
            var sb = new StringBuilder();
            sb.Append("https://github.com/").Append(RepoOwner).Append('/').Append(RepoName)
              .Append("/issues/new?");
            bool first = true;
            foreach (var kv in parms)
            {
                if (!first) sb.Append('&');
                first = false;
                sb.Append(Uri.EscapeDataString(kv.Key)).Append('=')
                  .Append(Uri.EscapeDataString(kv.Value ?? ""));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Release page URL for a given version ("v1.8.0"), or /releases/latest.
        /// </summary>
        public static string BuildReleaseUrl(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "https://github.com/" + RepoOwner + "/" + RepoName + "/releases/latest";
            return "https://github.com/" + RepoOwner + "/" + RepoName + "/releases/tag/v" + version.TrimStart('v');
        }
    }
}