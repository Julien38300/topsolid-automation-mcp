using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Searches the catalog of TopSolid UI commands extracted from the public
    /// help documentation. Lets an LLM discover the right command name before
    /// calling `IApplication.InvokeCommand(fullName)` via the `invoke_command`
    /// recipe — or decide to use an Automation API path instead.
    ///
    /// Full EN catalog: 2428 commands (Cad, Kernel, Pdm, Cae, ...). FullName
    /// mapping verified live 2026-04-20:
    ///   help-md/EN/Kernel/UI/D3/Points/MidpointCommand.md
    ///   -> TopSolid.Kernel.UI.D3.Points.MidpointCommand
    /// The catalog pre-computes `fullName` for each entry — ready for
    /// direct use with invoke_command.
    ///
    /// No TopSolid connection required. Catalog is loaded once from
    /// data/commands-catalog.json and cached in memory.
    /// </summary>
    public class SearchCommandsTool
    {
        private static List<CommandEntry> _cache;
        private static readonly object _lock = new object();

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_search_commands",
                Description = "Search the catalog of TopSolid UI commands (ribbon/menu actions) " +
                    "by keyword. Returns the human title, menu location, summary, and the " +
                    "**fullName** ready to pass to `IApplication.InvokeCommand(fullName)` " +
                    "via the `invoke_command` recipe. Covers 2428 EN commands across all " +
                    "modules (Cad, Kernel, Pdm, Cae, Mold, Tooling...) — useful when the " +
                    "Automation API has no direct method for what you want (many TopSolid " +
                    "features are only reachable via their ribbon command). FullName " +
                    "mapping verified live on 2026-04-20.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Keyword(s) to match against command name / title / menu / summary. " +
                                "Case-insensitive substring match. Example: 'bom' matches BomTable, BomIndex, etc."
                        },
                        ["max_results"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Max hits to return (default 8, max 30)."
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            try
            {
                string query = arguments?["query"]?.ToString();
                if (string.IsNullOrWhiteSpace(query))
                    return "Error: 'query' argument is required.";

                int maxResults = arguments?["max_results"]?.Value<int>() ?? 8;
                if (maxResults < 1) maxResults = 1;
                if (maxResults > 30) maxResults = 30;

                var catalog = LoadCatalog();
                if (catalog == null || catalog.Count == 0)
                    return "Error: commands-catalog.json not found or empty. " +
                        "Rebuild via `python scripts/build-commands-catalog.py`.";

                string q = query.ToLowerInvariant();
                var scored = new List<(int score, CommandEntry entry)>();
                foreach (var c in catalog)
                {
                    int s = Score(c, q);
                    if (s > 0) scored.Add((s, c));
                }
                scored.Sort((a, b) => b.score.CompareTo(a.score));

                if (scored.Count == 0)
                    return "No commands matched '" + query + "'. " +
                        "Catalog: " + catalog.Count + " commands. " +
                        "Current POC covers Drafting only; other domains (Design, Kernel, Mold...) are not yet indexed.";

                var sb = new StringBuilder();
                sb.AppendLine("Found " + Math.Min(scored.Count, maxResults) +
                    " match(es) (of " + scored.Count + " total, " + catalog.Count + " indexed):");
                sb.AppendLine();
                foreach (var (s, e) in scored.Take(maxResults))
                {
                    sb.AppendLine("---");
                    sb.AppendLine("Title    : " + e.Title);
                    sb.AppendLine("FullName : " + e.FullName);
                    sb.AppendLine("Menu     : " + e.Menu);
                    if (!string.IsNullOrEmpty(e.Summary))
                        sb.AppendLine("Brief    : " + e.Summary);
                    if (e.Related != null && e.Related.Count > 0)
                        sb.AppendLine("See also : " + string.Join(", ", e.Related.Take(6)));
                    sb.AppendLine("HelpDoc  : help-md/" + e.Path);
                }
                sb.AppendLine();
                sb.AppendLine("To invoke any of these, pass the FullName to the `invoke_command` recipe:");
                sb.AppendLine("  topsolid_run_recipe(recipe=\"invoke_command\", value=\"" +
                    (scored.Count > 0 ? scored[0].entry.FullName : "TopSolid...") + "\")");
                sb.AppendLine("Modal commands (those that wait for user selection) stay 'active' " +
                    "until the user validates or escapes — check IApplication.ActiveCommandFullName " +
                    "to observe. Non-modal commands execute immediately.");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[SearchCommandsTool] " + ex.Message);
                return "Error: " + ex.Message;
            }
        }

        private static int Score(CommandEntry e, string q)
        {
            int score = 0;
            if (e.Name != null && e.Name.ToLowerInvariant().Contains(q)) score += 10;
            if (e.Title != null && e.Title.ToLowerInvariant().Contains(q)) score += 8;
            if (e.Menu != null && e.Menu.ToLowerInvariant().Contains(q)) score += 4;
            if (e.Summary != null && e.Summary.ToLowerInvariant().Contains(q)) score += 2;
            // Prefer shorter names (more "canonical" command)
            if (score > 0 && e.Name != null) score += Math.Max(0, 40 - e.Name.Length) / 10;
            // Slight preference for top-level modules users hit most
            if (score > 0 && e.FullName != null)
            {
                if (e.FullName.StartsWith("TopSolid.Cad.", StringComparison.Ordinal)) score += 1;
                else if (e.FullName.StartsWith("TopSolid.Kernel.", StringComparison.Ordinal)) score += 1;
            }
            return score;
        }

        private static List<CommandEntry> LoadCatalog()
        {
            if (_cache != null) return _cache;
            lock (_lock)
            {
                if (_cache != null) return _cache;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates =
                {
                    Path.Combine(baseDir, "data", "commands-catalog.json"),
                    Path.Combine(baseDir, "commands-catalog.json"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "data", "commands-catalog.json"),
                };
                foreach (var p in candidates)
                {
                    if (!File.Exists(p)) continue;
                    try
                    {
                        var text = File.ReadAllText(p);
                        var root = JObject.Parse(text);
                        var list = new List<CommandEntry>();
                        var arr = root["entries"] as JArray;
                        if (arr == null) continue;
                        foreach (var item in arr)
                        {
                            var rel = item["related"] as JArray;
                            list.Add(new CommandEntry
                            {
                                Name = (string)item["name"],
                                FullName = (string)item["fullName"],
                                Path = (string)item["path"],
                                Title = (string)item["title"],
                                Summary = (string)item["summary"],
                                Menu = (string)item["menu"],
                                Related = rel != null ? rel.Select(x => (string)x).ToList() : new List<string>(),
                            });
                        }
                        _cache = list;
                        Console.Error.WriteLine("[SearchCommandsTool] Loaded " + list.Count + " commands from " + p);
                        return _cache;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[SearchCommandsTool] Failed to load " + p + ": " + ex.Message);
                    }
                }
                _cache = new List<CommandEntry>();
                return _cache;
            }
        }

        private class CommandEntry
        {
            public string Name;
            public string FullName;
            public string Path;
            public string Title;
            public string Summary;
            public string Menu;
            public List<string> Related;
        }
    }
}
