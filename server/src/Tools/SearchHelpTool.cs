using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Full-text search over the TopSolid online help (5809 pages: 2974 EN + 2835 FR)
    /// indexed as a SQLite FTS5 virtual table in data/help.db.
    ///
    /// The index is built offline by scripts/build-help-index.py and shipped with
    /// the server (~20 MB). No TopSolid connection required, no external service.
    /// </summary>
    public class SearchHelpTool
    {
        private static string _dbPath;
        private static bool _dbChecked;

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_search_help",
                Description = "Full-text search the TopSolid online help (5809 pages, EN+FR). " +
                    "Returns ranked excerpts with title, domain and file path. " +
                    "Use this to answer 'how does feature X work' questions, find menu locations, " +
                    "or learn TopSolid workflow before writing automation code.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "FTS5 query. Supports AND/OR/NOT, phrases \"...\", " +
                                "prefix foo*, column filters (title:sketch). Unicode-aware, diacritics folded."
                        },
                        ["lang"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Restrict to 'EN' or 'FR'. Omit for both."
                        },
                        ["domain"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Restrict to one domain: Cad, Cae, Cam, Erp, Kernel, Pdm, WorkManager."
                        },
                        ["max_results"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Max hits to return (default 5, max 20)."
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

                string lang = arguments?["lang"]?.ToString();
                string domain = arguments?["domain"]?.ToString();
                int maxResults = arguments?["max_results"]?.Value<int>() ?? 5;
                if (maxResults < 1) maxResults = 1;
                if (maxResults > 20) maxResults = 20;

                string dbPath = ResolveDbPath();
                if (dbPath == null)
                {
                    return "Error: help.db not found. Run `python scripts/build-help-index.py` " +
                        "to build the full-text search index.";
                }

                var hits = new List<Hit>();
                long total = 0;

                using (var conn = new SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly"))
                {
                    conn.Open();

                    // Total pages indexed (metadata-style count)
                    using (var cntCmd = conn.CreateCommand())
                    {
                        cntCmd.CommandText = "SELECT COUNT(*) FROM help;";
                        total = (long)cntCmd.ExecuteScalar();
                    }

                    var where = new StringBuilder("help MATCH $q");
                    if (!string.IsNullOrEmpty(lang))
                        where.Append(" AND lang = $lang");
                    if (!string.IsNullOrEmpty(domain))
                        where.Append(" AND domain = $domain");

                    using (var cmd = conn.CreateCommand())
                    {
                        // snippet(tbl, col_idx, start, end, ellipsis, max_tokens)
                        // col 4 = content (0 title, 1 lang, 2 domain, 3 path, 4 content)
                        cmd.CommandText =
                            "SELECT title, lang, domain, path, " +
                            "       snippet(help, 4, '[', ']', ' ... ', 18) AS excerpt, " +
                            "       bm25(help) AS score " +
                            "FROM help WHERE " + where +
                            " ORDER BY score LIMIT $lim;";
                        cmd.Parameters.AddWithValue("$q", query);
                        if (!string.IsNullOrEmpty(lang))
                            cmd.Parameters.AddWithValue("$lang", lang);
                        if (!string.IsNullOrEmpty(domain))
                            cmd.Parameters.AddWithValue("$domain", domain);
                        cmd.Parameters.AddWithValue("$lim", maxResults);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                hits.Add(new Hit
                                {
                                    Title = r.GetString(0),
                                    Lang = r.GetString(1),
                                    Domain = r.GetString(2),
                                    Path = r.GetString(3),
                                    Excerpt = r.IsDBNull(4) ? "" : r.GetString(4),
                                    Score = r.IsDBNull(5) ? 0.0 : r.GetDouble(5),
                                });
                            }
                        }
                    }
                }

                if (hits.Count == 0)
                {
                    return "No help pages matched '" + query + "'" +
                        (string.IsNullOrEmpty(lang) ? "" : " (lang=" + lang + ")") +
                        (string.IsNullOrEmpty(domain) ? "" : " (domain=" + domain + ")") +
                        ". Total pages indexed: " + total + ". " +
                        "Try broader terms, remove filters, or use prefix match (e.g. 'sketch*').";
                }

                var sb = new StringBuilder();
                sb.AppendLine("Found " + hits.Count + " help pages (of " + total + " indexed) for '" + query + "':");
                sb.AppendLine();
                foreach (var h in hits)
                {
                    sb.AppendLine("---");
                    sb.AppendLine("Title: " + h.Title);
                    sb.AppendLine("Lang: " + h.Lang + "  Domain: " + h.Domain);
                    sb.AppendLine("Path: help-md/" + h.Path);
                    sb.AppendLine("Excerpt: " + h.Excerpt.Replace("\r", " ").Replace("\n", " "));
                    sb.AppendLine();
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[SearchHelpTool] Error: " + ex.Message);
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Locates help.db in the usual places (output dir, repo data dir, dev fallback).
        /// Result cached after the first successful lookup.
        /// </summary>
        private static string ResolveDbPath()
        {
            if (_dbChecked) return _dbPath;
            _dbChecked = true;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "data", "help.db"),
                Path.Combine(baseDir, "help.db"),
                Path.Combine(baseDir, "..", "..", "..", "data", "help.db"),
                Path.Combine(baseDir, "..", "..", "..", "..", "data", "help.db"),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) { _dbPath = Path.GetFullPath(c); return _dbPath; }
            }
            return null;
        }

        private class Hit
        {
            public string Title;
            public string Lang;
            public string Domain;
            public string Path;
            public string Excerpt;
            public double Score;
        }
    }
}
