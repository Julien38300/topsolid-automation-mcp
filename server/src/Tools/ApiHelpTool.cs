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
        private readonly Func<TypeGraph> _graphProvider;

        private static readonly Dictionary<string, string[]> Synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Proprietes PDM (colonnes TopSolid) ---
            { "designation", new[] { "Description" } },
            { "reference", new[] { "Part", "Number" } },
            { "fabricant", new[] { "Manufacturer" } },
            { "fournisseur", new[] { "Manufacturer" } },
            { "auteur", new[] { "Owner" } },
            { "renommer", new[] { "Set", "Name" } },
            { "rename", new[] { "Set", "Name" } },
            { "nom", new[] { "Name" } },
            // --- PDM operations (termes TopSolid) ---
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
            // --- Formats export ---
            { "step", new[] { "Export" } },
            { "iges", new[] { "Export" } },
            // --- Operations courantes ---
            { "brut", new[] { "Stock" } },
            { "supprimer", new[] { "Delete" } },
            { "effacer", new[] { "Delete" } },
            { "creer", new[] { "Create" } },
            { "lister", new[] { "Get" } },
            // --- Geometrie metier (termes TopSolid francais) ---
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
            // --- Mise a plat / depliage ---
            { "depliage", new[] { "Unfolding" } },
            { "mise a plat", new[] { "IUnfoldings" } },
            // --- Mise en plan / Nomenclature ---
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
            { "parameter", "Pour lister les parametres : GetParameters(docId), puis GetParameterType(p) pour choisir le bon GetXxxValue." },
            { "sketch", "Pour lister les esquisses : GetSketches(docId) ou GetFunctions(docId) filtre par type Sketch." },
            { "export", "Pour exporter : trouver l'index avec GetExporterFileType(), verifier CanExport(), puis Export()." },
            { "assembly", "Pour les assemblages, utiliser TopSolidDesignHost.Assemblies (pas TopSolidHost)." },
            { "project", "SearchProjectByName fait un CONTAINS. Toujours verifier le nom exact avec GetName() apres la recherche." },
            { "folder", "GetConstituents() separe dossiers et documents. SearchFolderByName fait un CONTAINS." },
            { "document", "SearchDocumentByName fait un CONTAINS (pas exact). GetType() retourne l'extension (.TopPrt, .TopAsm)." },
            { "name", "Trois methodes GetName : Pdm.GetName(PdmObjectId), Documents.GetName(DocumentId), Elements.GetName(ElementId)." },
            { "revision", "CheckIn = mise au coffre. CheckOut = sorti de coffre. CheckIn obligatoire avant changement de cycle de vie." },
            { "family", "IsFamily() pour verifier, GetCodes() pour les codes, GetGenericDocument() pour le generique." },
            { "inclusion", "CreatePositioning() AVANT CreateInclusion(). Utiliser GetInclusionChildOccurrence pour naviguer." },
            { "modification", "TOUJOURS utiliser topsolid_modify_script au lieu de topsolid_execute_script pour les modifications." },
            { "designation", "Designation = IPdm.SetDescription (pas SetName). Reference = IPdm.SetPartNumber. Nom = IPdm.SetName." },
            { "description", "Description dans l'API = Designation dans TopSolid. IPdm.SetDescription pour modifier." },
            { "drafting", "Mise en plan : creer .TopDrf, ajouter vues (principale + auxiliaires) via ensembles de projection. Rafale = generation par lot depuis nomenclature." },
            { "bom", "Nomenclature = vue technique d'un assemblage. Filtrable (toles, profiles, achetes...). Rafale = generer un plan par ligne." },
            { "unfolding", "Mise a plat / depliage : pour la tolerie. Piece pliee → deplie → export DXF pour decoupe laser." }
        };

        private static readonly string[] Tier1Interfaces = { "ITopSolidHost", "IDocuments", "IPdm", "IParameters", "IElements", "ISketches2D", "IShapes", "IOperations" };

        private static readonly string[] OrderedCategories = {
            "Navigation / Interrogation",
            "Lecture de valeurs",
            "Recherche",
            "Creation",
            "Ecriture de valeurs",
            "Suppression",
            "Autres"
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
                Description = "Recherche dans la reference API TopSolid Automation. " +
                    "Utiliser AVANT topsolid_execute_script pour trouver les signatures. " +
                    "Supporte : nom d'interface (IPdm), mot-cle (sketch), ou filtrage (IDocuments.Export).",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Interface (ex: IParameters), mot-cle (ex: sketch), ou Interface.Prefixe (ex: IDocuments.Get)"
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            string query = arguments["query"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(query))
                return "Erreur : le parametre 'query' est requis.";

            var graph = _graphProvider();
            if (graph == null)
                return "Erreur : graphe API non charge.";

            // --- 0. Support Interface.Prefixe ---
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

            // --- 1. Mode Interface exact ---
            var interfaceEdges = graph.GetEdges()
                .Where(e => string.Equals(e.Interface, query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (interfaceEdges.Count > 0)
            {
                return FormatInterface(query, interfaceEdges);
            }

            // --- 2. Mode Recherche par mots-cles ---
            // Split by separators
            var rawTokens = query.Split(new[] { ' ', ',', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

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
            var expandedKeywords = new List<string>();
            foreach (var token in splitTokens)
            {
                if (Synonyms.TryGetValue(token, out var syns))
                    expandedKeywords.AddRange(syns);
                else
                    expandedKeywords.Add(token);
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
                return "Aucun resultat pour '" + query + "'. Suggestions :\n" +
                    "- Essayer un nom d'interface exact : IParameters, IPdm, IDocuments, IShapes\n" +
                    "- Essayer un mot-cle en anglais : sketch, export, family, assembly\n" +
                    "- Essayer Interface.Prefixe : IDocuments.Export, IPdm.Search";
            }

            // Tri par pertinence
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
            // Match exact du nom
            if (string.Equals(edge.MethodName, query, StringComparison.OrdinalIgnoreCase)) score += 10;
            // Match interface
            if (edge.Interface != null && edge.Interface.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) score += 5;
            // Match description
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
            string header = isFiltered ? $"=== {name} (Filtre) — {total} methode(s) ===" : $"=== {name} — {total} methode(s) ===";
            lines.Add(header + "\n");

            foreach (var group in groups)
            {
                lines.Add($"--- {group.Key} ({group.Count()} methodes) ---");
                foreach (var edge in group)
                {
                    lines.Add(FormatEdge(edge));
                }
                lines.Add("");
            }

            if (total > limit)
            {
                lines.Add($"... et {total - limit} autres methodes. Utiliser api_help(\"{name}.Prefix\") pour filtrer.");
            }

            return string.Join("\n", lines);
        }

        private string FormatKeywordResults(string query, List<GraphEdge> edges)
        {
            var lines = new List<string>();
            lines.Add($"{edges.Count} resultat(s) pour \"{query}\" :\n");

            var groups = edges.GroupBy(e => e.Interface ?? "Autres").OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                lines.Add($"{group.Key} ({group.Count()} methodes) :");
                foreach (var edge in group)
                {
                    lines.Add("  " + FormatEdge(edge));
                }
                lines.Add("");
            }

            // Ajout du conseil
            string tip = GetUsageTip(query);
            if (!string.IsNullOrEmpty(tip))
            {
                lines.Add("Conseil : " + tip);
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
            if (methodName.EndsWith("Value") && methodName.StartsWith("Get")) return "Lecture de valeurs";
            if (methodName.EndsWith("Value") && methodName.StartsWith("Set")) return "Ecriture de valeurs";
            if (methodName.StartsWith("Get")) return "Navigation / Interrogation";
            if (methodName.StartsWith("Create")) return "Creation";
            if (methodName.StartsWith("Search")) return "Recherche";
            if (methodName.StartsWith("Delete") || methodName.StartsWith("Remove")) return "Suppression";
            if (methodName.StartsWith("Export")) return "Export";
            return "Autres";
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
    }
}

