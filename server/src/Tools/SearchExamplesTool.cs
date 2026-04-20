using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Searches local, user-private C# corpora for code snippets matching a
    /// keyword or API name. Returns method-level chunks with file reference.
    /// Useful for Claude Code / developers to learn from production patterns
    /// the user has on their own disk — these corpora are NEVER shipped with
    /// the public server and the paths/labels are user-local.
    ///
    /// Override paths via env var TOPSOLID_EXAMPLES_ROOT (not implemented yet)
    /// or by editing DefaultCorpora locally before build.
    /// No TopSolid connection required.
    /// </summary>
    public class SearchExamplesTool
    {
        // Default corpus paths — user-local only, NEVER shipped publicly.
        // Labels are opaque placeholders (corp-a/b/c) on purpose: the sources
        // are private (provided by author's network + internal scripts) and
        // should not be named in any MCP response that the client may log.
        // Replace the paths below to point at your own local corpora.
        private static readonly List<(string Label, string Path)> DefaultCorpora = new List<(string, string)>
        {
            ("corp-a", @"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples REDACTED-USER"),
            ("corp-b", @"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples RoB"),
            ("corp-c", @"C:\Users\jup\OneDrive\4 - VB\Projects\Script Qualité FEA"),
        };

        // Cache: lazily built index of method-level chunks
        private static List<MethodChunk> _cache;
        private static DateTime _cacheBuiltAt = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_search_examples",
                Description = "Search the user's LOCAL private corpora of TopSolid C# snippets " +
                    "(opaque labels, not shipped with the server) for method-level samples " +
                    "matching a keyword or API name. Returns file reference + code body. " +
                    "Useful for Claude Code / developers to learn from production patterns " +
                    "they already have on disk.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Keyword or API name to search for " +
                                "(e.g. 'StartModification', 'GetMass', 'PdmObject'). " +
                                "Case-insensitive substring match against method body + name."
                        },
                        ["max_results"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of matching snippets to return (default 5)."
                        },
                        ["corpus"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional: filter by local corpus label (opaque, user-defined). Omit to search all configured corpora."
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            }, Execute);
        }

        /// <summary>
        /// Execute the search across all (or a specific) corpus.
        /// </summary>
        public string Execute(JObject arguments)
        {
            try
            {
                string query = arguments?["query"]?.ToString();
                if (string.IsNullOrWhiteSpace(query))
                    return "Error: 'query' argument is required.";

                int maxResults = arguments?["max_results"]?.Value<int>() ?? 5;
                string corpusFilter = arguments?["corpus"]?.ToString();

                var index = GetOrBuildIndex();
                var candidates = index.AsEnumerable();

                if (!string.IsNullOrEmpty(corpusFilter))
                {
                    candidates = candidates.Where(c =>
                        c.Corpus.Equals(corpusFilter, StringComparison.OrdinalIgnoreCase));
                }

                // Case-insensitive match on method body or method name
                string q = query.ToLowerInvariant();
                var matches = candidates
                    .Where(c => c.Body.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                             || c.MethodName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(maxResults)
                    .ToList();

                if (matches.Count == 0)
                {
                    return "No matches found for query: '" + query + "'. " +
                        "Indexed " + index.Count + " methods across " + DefaultCorpora.Count + " corpora.";
                }

                var sb = new StringBuilder();
                sb.AppendLine("Found " + matches.Count + " matching snippets (of " + index.Count + " indexed):");
                sb.AppendLine();
                foreach (var m in matches)
                {
                    sb.AppendLine("---");
                    sb.AppendLine("Corpus: " + m.Corpus + "  File: " + m.RelativePath);
                    sb.AppendLine("Method: " + m.MethodName);
                    sb.AppendLine("```csharp");
                    sb.AppendLine(m.Body);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[SearchExamplesTool] Error: " + ex.Message);
                return "Error: " + ex.Message;
            }
        }

        private static List<MethodChunk> GetOrBuildIndex()
        {
            if (_cache != null && DateTime.UtcNow - _cacheBuiltAt < CacheTtl)
                return _cache;

            var chunks = new List<MethodChunk>();
            foreach (var (label, root) in DefaultCorpora)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var cs in EnumerateSourceFiles(root))
                {
                    try
                    {
                        var text = File.ReadAllText(cs);
                        if (text.IndexOf("TopSolid", StringComparison.Ordinal) < 0) continue;
                        chunks.AddRange(ExtractMethods(text, label, MakeRelative(root, cs)));
                    }
                    catch { /* skip unreadable file */ }
                }
            }
            _cache = chunks;
            _cacheBuiltAt = DateTime.UtcNow;
            Console.Error.WriteLine("[SearchExamplesTool] Indexed " + chunks.Count + " method chunks");
            return _cache;
        }

        private static IEnumerable<string> EnumerateSourceFiles(string root)
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (path.IndexOf(@"\.vs\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                yield return path;
            }
        }

        private static string MakeRelative(string root, string full)
        {
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Substring(root.Length).TrimStart('\\', '/');
            return full;
        }

        // Method signature regex (brace-matching is done manually below)
        private static readonly Regex MethodSigRe = new Regex(
            @"(?:public|private|protected|internal|static|\s)+\s+\w[\w\.<>,\[\]]*\s+" +
            @"(?<name>[A-Z][a-zA-Z0-9_]+)\s*\([^)]*\)\s*(?:where[^{]+)?\{",
            RegexOptions.Multiline);

        private static IEnumerable<MethodChunk> ExtractMethods(string text, string corpus, string relPath)
        {
            foreach (Match m in MethodSigRe.Matches(text))
            {
                int start = m.Index + m.Length - 1;
                int depth = 1;
                int i = start + 1;
                while (i < text.Length && depth > 0)
                {
                    char c = text[i];
                    if (c == '{') depth++;
                    else if (c == '}') depth--;
                    if (depth == 0)
                    {
                        string body = text.Substring(start + 1, i - start - 1).Trim();
                        int lineCount = body.Split('\n').Length;
                        if (lineCount >= 5 && lineCount <= 120 && body.IndexOf("TopSolid", StringComparison.Ordinal) >= 0)
                        {
                            yield return new MethodChunk
                            {
                                Corpus = corpus,
                                RelativePath = relPath,
                                MethodName = m.Groups["name"].Value,
                                Body = body,
                            };
                        }
                        break;
                    }
                    i++;
                }
            }
        }

        private class MethodChunk
        {
            public string Corpus;
            public string RelativePath;
            public string MethodName;
            public string Body;
        }
    }
}
