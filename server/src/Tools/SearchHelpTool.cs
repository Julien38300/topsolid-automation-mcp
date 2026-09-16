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
        /// <summary>Maximum size of the returned text, to keep a single call from flooding the caller's context.</summary>
        private const int MaxOutputChars = 8000;

        /// <summary>Columns of the FTS5 table: a "word:" prefix is only FTS syntax when the word is one of these.</summary>
        private static readonly string[] FtsColumns = { "help", "title", "lang", "domain", "path", "content" };

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
            return Truncate(ExecuteCore(arguments));
        }

        private string ExecuteCore(JObject arguments)
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

                // A natural-language question ("tolerie: depliage") is NOT valid FTS5 syntax:
                // quote every token unless the caller clearly wrote an FTS5 expression.
                string ftsQuery = LooksLikeFtsSyntax(query) ? query : QuoteTokens(query);
                if (string.IsNullOrEmpty(ftsQuery))
                {
                    return "Error: 'query' contains no searchable word.";
                }

                long total = 0;
                List<Hit> hits;
                try
                {
                    hits = RunSearch(dbPath, ftsQuery, lang, domain, maxResults, out total);
                }
                catch (SqliteException)
                {
                    // FTS5 still rejected the expression: retry once with the whole
                    // user query as a single quoted phrase, which is always valid.
                    hits = RunSearch(dbPath, QuoteWhole(query), lang, domain, maxResults, out total);
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
            catch (SqliteException ex)
            {
                Console.Error.WriteLine("[SearchHelpTool] SQLite error: " + ex.Message);
                return "Error: the search query could not be run against the help index (" +
                    ex.Message + "). Try plain keywords, or quote them: \"sheet metal\".";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[SearchHelpTool] Error: " + ex.Message);
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Runs one FTS5 query against help.db and returns the hits.
        /// </summary>
        private static List<Hit> RunSearch(string dbPath, string ftsQuery, string lang, string domain,
            int maxResults, out long total)
        {
            var hits = new List<Hit>();
            total = 0;

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
                        cmd.Parameters.AddWithValue("$q", ftsQuery);
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

            return hits;
        }

        /// <summary>
        /// True when the caller deliberately used FTS5 syntax (AND/OR/NOT, a phrase in
        /// double quotes, a trailing prefix star, or a "column:" prefix). Anything else is
        /// treated as plain natural language and gets quoted before reaching FTS5.
        /// </summary>
        private static bool LooksLikeFtsSyntax(string q)
        {
            if (string.IsNullOrEmpty(q)) return false;
            if (q.IndexOf('"') >= 0) return true;

            foreach (var token in q.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "AND" || token == "OR" || token == "NOT" || token == "NEAR") return true;
                if (token.Length > 1 && token[token.Length - 1] == '*') return true;

                int colon = token.IndexOf(':');
                if (colon > 0)
                {
                    string column = token.Substring(0, colon).ToLowerInvariant();
                    foreach (var known in FtsColumns)
                    {
                        if (column == known) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Wraps each whitespace-separated token in an FTS5 phrase (internal double quotes
        /// doubled). Tokens with no letter or digit are dropped: they carry no index term
        /// and an empty phrase is itself a syntax error.
        /// </summary>
        private static string QuoteTokens(string q)
        {
            var sb = new StringBuilder();
            foreach (var token in q.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool hasWordChar = false;
                foreach (var c in token)
                {
                    if (char.IsLetterOrDigit(c)) { hasWordChar = true; break; }
                }
                if (!hasWordChar) continue;

                if (sb.Length > 0) sb.Append(' ');
                sb.Append('"').Append(token.Replace("\"", "\"\"")).Append('"');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Last-resort form: the whole user query as one quoted FTS5 phrase.
        /// </summary>
        private static string QuoteWhole(string q)
        {
            return "\"" + (q ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Caps the output length and appends an explicit marker when text was cut.
        /// </summary>
        private static string Truncate(string output)
        {
            if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputChars)
                return output;

            return output.Substring(0, MaxOutputChars) + "\n... [output truncated - refine your query]";
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
