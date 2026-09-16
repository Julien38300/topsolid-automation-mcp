using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolidApiGraph.Core;
using TopSolidApiGraph.Core.Models;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    public class ApiHelpTool
    {
        /// <summary>Maximum size of the returned text, to keep a single call from flooding the caller's context.</summary>
        private const int MaxOutputChars = 8000;

        private readonly Func<TypeGraph> _graphProvider;

        private static readonly Dictionary<string, string[]> Synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // --- PDM properties (TopSolid columns) ---
            { "designation", new[] { "Description" } },
            { "reference", new[] { "Part", "Number" } },
            { "fabricant", new[] { "Manufacturer" } },
            { "fournisseur", new[] { "Manufacturer" } },
            { "auteur", new[] { "Owner" } },
            { "renommer", new[] { "Set", "Name" } },
            { "rename", new[] { "Set", "Name" } },
            { "nom", new[] { "Name" } },
            // --- PDM operations (TopSolid terminology) ---
            { "coffre", new[] { "Check" } },
            { "archiver", new[] { "Check", "In" } },
            // --- Interfaces ---
            { "bom", new[] { "IBoms" } },
            { "nomenclature", new[] { "IBoms" } },
            { "collision", new[] { "ICollisions" } },
            { "collisions", new[] { "ICollisions" } },
            { "matiere", new[] { "IMaterials" } },
            { "materiau", new[] { "IMaterials" } },
            { "material", new[] { "IMaterials" } },
            { "draft", new[] { "IDraftings" } },
            { "drafting", new[] { "IDraftings" } },
            { "peinture", new[] { "ICoatings" } },
            { "traitement", new[] { "ICoatings" } },
            { "coating", new[] { "ICoatings" } },
            { "revetement", new[] { "ICoatings" } },
            { "tolerance", new[] { "Visualization", "Tolerances" } },
            { "simulation", new[] { "ISimulations" } },
            { "texture", new[] { "ITextures" } },
            { "calque", new[] { "ILayers" } },
            { "entite", new[] { "IEntities" } },
            // --- Export formats ---
            { "step", new[] { "Export" } },
            { "iges", new[] { "Export" } },
            // --- Common operations ---
            { "brut", new[] { "Stock" } },
            { "supprimer", new[] { "Delete" } },
            { "effacer", new[] { "Delete" } },
            { "creer", new[] { "Create" } },
            { "lister", new[] { "Get" } },
            // --- Domain geometry (French TopSolid terms) ---
            { "esquisse", new[] { "Sketch" } },
            { "piece", new[] { "Part" } },
            { "assemblage", new[] { "Assembly" } },
            { "famille", new[] { "Family" } },
            { "extrusion", new[] { "Extruded", "Shape" } },
            { "percage", new[] { "Drilling" } },
            { "pliage", new[] { "Bend" } },
            { "plan", new[] { "Plane" } },
            { "repere", new[] { "Frame" } },
            { "contrainte", new[] { "Constraint" } },
            { "inclusion", new[] { "Inclusion" } },
            { "contour", new[] { "Profile" } },
            { "section", new[] { "Profile" } },
            { "conge", new[] { "Fillet" } },
            { "chanfrein", new[] { "Chamfer" } },
            { "gabarit", new[] { "Lofted" } },
            { "lissage", new[] { "Lofted" } },
            { "balayage", new[] { "Sweep" } },
            { "motif", new[] { "Pattern" } },
            { "repetition", new[] { "Pattern" } },
            { "symetrie", new[] { "Mirror" } },
            { "coque", new[] { "Shell" } },
            { "tole", new[] { "Sheet" } },
            { "filetage", new[] { "Thread" } },
            { "cote", new[] { "IDimensions" } },
            { "cotation", new[] { "IAnnotations" } },
            // --- Flat pattern / unfolding ---
            { "depliage", new[] { "Unfolding" } },
            { "mise a plat", new[] { "IUnfoldings" } },
            // --- Drafting / BOM ---
            { "mise en plan", new[] { "IDraftings" } },
            { "liasse", new[] { "IDraftings" } },
            { "rafale", new[] { "IBoms" } },
            { "modele", new[] { "Template" } },
            { "liste de debit", new[] { "IBoms" } },
            { "fiche", new[] { "IBoms" } },
            // --- Export ---
            { "dxf", new[] { "Export" } },
            { "pdf", new[] { "Export" } },
            { "ifc", new[] { "Export" } }
        };

        private static string[] SplitCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return new string[0];
            return Regex.Split(name, @"(?=[A-Z])")
                .Where(s => s.Length > 1).ToArray();
        }

        private static readonly Dictionary<string, string> UsageTips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "parameter", "To list parameters: GetParameters(docId), then GetParameterType(p) to pick the right GetXxxValue." },
            { "sketch", "To list sketches: GetSketches(docId), or GetFunctions(docId) filtered on the Sketch type." },
            { "export", "To export: find the index with GetExporterFileType(), check CanExport(), then call Export()." },
            { "assembly", "For assemblies, use TopSolidDesignHost.Assemblies (not TopSolidHost)." },
            { "project", "SearchProjectByName does a CONTAINS match. Always confirm the exact name with GetName() afterwards." },
            { "folder", "GetConstituents() splits folders and documents. SearchFolderByName does a CONTAINS match." },
            { "document", "SearchDocumentByName does a CONTAINS match (not exact). GetType() returns the extension (.TopPrt, .TopAsm)." },
            { "name", "Three GetName methods: Pdm.GetName(PdmObjectId), Documents.GetName(DocumentId), Elements.GetName(ElementId)." },
            { "revision", "CheckIn = put back in the vault. CheckOut = take out of the vault. CheckIn is required before a life-cycle change." },
            { "family", "IsFamily() to test, GetCodes() for the codes, GetGenericDocument() for the generic document." },
            { "inclusion", "Call CreatePositioning() BEFORE CreateInclusion(). Use GetInclusionChildOccurrence to navigate." },
            { "modification", "ALWAYS use topsolid_modify_script instead of topsolid_execute_script for modifications." },
            { "designation", "Designation = IPdm.SetDescription (not SetName). Reference = IPdm.SetPartNumber. Name = IPdm.SetName." },
            { "description", "Description in the API = Designation in TopSolid. Use IPdm.SetDescription to change it." },
            { "drafting", "Drafting: create a .TopDrf, add views (main + auxiliary) through projection sets. Batch drafting generates one drawing per BOM line." },
            { "bom", "A BOM is a technical view of an assembly. It can be filtered (sheet metal, profiles, purchased parts...). Batch drafting generates one drawing per line." },
            { "unfolding", "Unfolding / flat pattern: for sheet metal. Folded part -> unfolded -> DXF export for laser cutting." }
        };

        private static readonly string[] Tier1Interfaces = { "ITopSolidHost", "IDocuments", "IPdm", "IParameters", "IElements", "ISketches2D", "IShapes", "IOperations" };

        private static readonly string[] OrderedCategories = {
            "Navigation / Queries",
            "Value reads",
            "Search",
            "Creation",
            "Value writes",
            "Export",
            "Deletion",
            "Other"
        };

        public ApiHelpTool(Func<TypeGraph> graphProvider)
        {
            _graphProvider = graphProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_api_help",
                Description = "Search the TopSolid Automation API reference. " +
                    "Use it BEFORE topsolid_execute_script to get the exact signatures. " +
                    "Accepts an interface name (IPdm), a keyword (sketch), or a filter (IDocuments.Export).",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Interface (e.g. IParameters), keyword (e.g. sketch), or Interface.Prefix (e.g. IDocuments.Get)"
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
            string query = arguments["query"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(query))
                return "Error: the 'query' argument is required.";

            var graph = _graphProvider();
            if (graph == null)
                return "Error: API graph not loaded.";

            // --- 0. Interface.Prefix support ---
            if (query.Contains(".") && !query.StartsWith("."))
            {
                var parts = query.Split('.');
                var interfaceName = parts[0].Trim();
                var prefix = parts[1].Trim();

                var filteredEdges = graph.GetEdges()
                    .Where(e => string.Equals(e.Interface, interfaceName, StringComparison.OrdinalIgnoreCase) && 
                                e.MethodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filteredEdges.Count > 0)
                    return FormatInterface(interfaceName, filteredEdges, true);
            }

            // --- 1. Exact interface mode ---
            var interfaceEdges = graph.GetEdges()
                .Where(e => string.Equals(e.Interface, query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (interfaceEdges.Count > 0)
            {
                return FormatInterface(query, interfaceEdges);
            }

            // --- 2. Keyword search mode ---
            var expandedKeywords = new List<string>();
            var rawTokens = query.Split(new[] { ' ', ',', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

            // Case-insensitive full match in Synonyms (for multi-word terms like "mise en plan")
            if (Synonyms.TryGetValue(query, out var querySyns))
            {
                expandedKeywords.AddRange(querySyns);
            }
            else
            {
                // CamelCase split each token (ex: "BackReferences" → "Back", "References")
                var splitTokens = new List<string>();
                foreach (var token in rawTokens)
                {
                    var camelParts = SplitCamelCase(token);
                    if (camelParts.Length > 0)
                        splitTokens.AddRange(camelParts);
                    else
                        splitTokens.Add(token);
                }

                // Expand synonyms (ex: "bom" → "IBoms", "rename" → "Set" + "Name")
                foreach (var token in splitTokens)
                {
                    if (Synonyms.TryGetValue(token, out var syns))
                        expandedKeywords.AddRange(syns);
                    else
                        expandedKeywords.Add(token);
                }
            }

            // Deduplicate case-insensitive
            string[] keywords = expandedKeywords
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var searchResults = graph.SearchByKeywords(keywords, 100); // Larger pool for scoring

            // Fallback: substring search in Description + SemanticHint + Interface
            if (searchResults.Count == 0)
            {
                searchResults = FallbackSubstringSearch(graph, rawTokens, 100);
            }

            if (searchResults.Count == 0)
            {
                return "No result for '" + query + "'. Suggestions:\n" +
                    "- Try an exact interface name: IParameters, IPdm, IDocuments, IShapes\n" +
                    "- Try an English keyword: sketch, export, family, assembly\n" +
                    "- Try Interface.Prefix: IDocuments.Export, IPdm.Search";
            }

            // Sort by relevance
            var sortedResults = searchResults
                .Select(e => new { Edge = e, Score = CalculateScore(e, query, keywords) })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Edge.MethodName)
                .Select(x => x.Edge)
                .Take(30)
                .ToList();

            return FormatKeywordResults(query, sortedResults);
        }

        private int CalculateScore(GraphEdge edge, string query, string[] keywords)
        {
            int score = 0;
            // Exact method-name match
            if (string.Equals(edge.MethodName, query, StringComparison.OrdinalIgnoreCase)) score += 10;
            // Interface match
            if (edge.Interface != null && edge.Interface.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) score += 5;
            // Description match
            if (edge.Description != null && edge.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) score += 2;
            // Match SemanticHint
            if (edge.SemanticHint != null && keywords.Any(k => edge.SemanticHint.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)) score += 2;
            // Tier 1
            if (edge.Interface != null && Tier1Interfaces.Any(t => string.Equals(t, edge.Interface, StringComparison.OrdinalIgnoreCase))) score += 3;
            
            return score;
        }

        private List<GraphEdge> FallbackSubstringSearch(TypeGraph graph, string[] queryTokens, int maxResults)
        {
            // Union: match if ANY token appears as substring in MethodName, Description, SemanticHint, or Interface
            return graph.GetEdges()
                .Where(e => queryTokens.Any(token =>
                    (e.MethodName != null && e.MethodName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.Description != null && e.Description.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.SemanticHint != null && e.SemanticHint.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.Interface != null && e.Interface.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                ))
                .Take(maxResults)
                .ToList();
        }

        private string FormatInterface(string name, List<GraphEdge> edges, bool isFiltered = false)
        {
            int total = edges.Count;
            int limit = 50;
            var displayEdges = edges.OrderBy(e => e.MethodName).Take(limit).ToList();

            var groups = displayEdges.GroupBy(e => GetCategory(e.MethodName))
                                     .OrderBy(g => Array.IndexOf(OrderedCategories, g.Key));

            var lines = new List<string>();
            string header = isFiltered ? $"=== {name} (filtered) - {total} method(s) ===" : $"=== {name} - {total} method(s) ===";
            lines.Add(header + "\n");

            foreach (var group in groups)
            {
                lines.Add($"--- {group.Key} ({group.Count()} methods) ---");
                foreach (var edge in group)
                {
                    lines.Add(FormatEdge(edge));
                }
                lines.Add("");
            }

            if (total > limit)
            {
                lines.Add($"... and {total - limit} more method(s). Use api_help(\"{name}.Prefix\") to filter.");
            }

            return string.Join("\n", lines);
        }

        private string FormatKeywordResults(string query, List<GraphEdge> edges)
        {
            var lines = new List<string>();
            lines.Add($"{edges.Count} result(s) for \"{query}\":\n");

            var groups = edges.GroupBy(e => e.Interface ?? "Other").OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                lines.Add($"{group.Key} ({group.Count()} method(s)):");
                foreach (var edge in group)
                {
                    lines.Add("  " + FormatEdge(edge));
                }
                lines.Add("");
            }

            // Append the contextual usage tip, when one matches the query.
            string tip = GetUsageTip(query);
            if (!string.IsNullOrEmpty(tip))
            {
                lines.Add("Tip: " + tip);
            }

            return string.Join("\n", lines);
        }

        private string GetUsageTip(string query)
        {
            foreach (var kvp in UsageTips)
            {
                if (query.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;
            }
            return null;
        }

        private string GetCategory(string methodName)
        {
            if (methodName.EndsWith("Value") && methodName.StartsWith("Get")) return "Value reads";
            if (methodName.EndsWith("Value") && methodName.StartsWith("Set")) return "Value writes";
            if (methodName.StartsWith("Get")) return "Navigation / Queries";
            if (methodName.StartsWith("Create")) return "Creation";
            if (methodName.StartsWith("Search")) return "Search";
            if (methodName.StartsWith("Delete") || methodName.StartsWith("Remove")) return "Deletion";
            if (methodName.StartsWith("Export")) return "Export";
            return "Other";
        }

        private string FormatEdge(GraphEdge edge)
        {
            string signature = !string.IsNullOrEmpty(edge.MethodSignature) 
                ? edge.MethodSignature 
                : $"{edge.Interface ?? "Unknown"}.{edge.MethodName}(...)";

            if (!string.IsNullOrEmpty(edge.Description))
                return string.Format("{0} — {1}", signature, edge.Description);

            return signature;
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
    }
}

