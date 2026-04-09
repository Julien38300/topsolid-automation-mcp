using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolidApiGraph.Core;
using TopSolidApiGraph.Core.Models;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to explore multiple paths between types in the TopSolid API graph.
    /// </summary>
    public class ExplorePathsTool
    {
        private readonly Func<TypeGraph> _graphProvider;
        private readonly Func<TypeNameResolver> _resolverProvider;

        public ExplorePathsTool(Func<TypeGraph> graphProvider, Func<TypeNameResolver> resolverProvider)
        {
            _graphProvider = graphProvider;
            _resolverProvider = resolverProvider;
        }

        /// <summary>
        /// Registers the tool in the provided registry.
        /// </summary>
        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_explore_paths",
                Description = "Explore plusieurs chemins de méthodes possibles entre deux types TopSolid.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["sourceType"] = new JObject { ["type"] = "string", ["description"] = "Type source (ex: 'IPdm', 'void')" },
                        ["targetType"] = new JObject { ["type"] = "string", ["description"] = "Type cible (ex: 'PdmObjectId', 'String')" },
                        ["maxDepth"] = new JObject { ["type"] = "integer", ["description"] = "Profondeur maximale de recherche (1-5)", ["default"] = 3, ["minimum"] = 1, ["maximum"] = 5 }
                    },
                    ["required"] = new JArray { "sourceType", "targetType" }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes the exploration logic.
        /// </summary>
        public string Execute(JObject arguments)
        {
            var sourceArg = arguments["sourceType"]?.ToString();
            var targetArg = arguments["targetType"]?.ToString();
            var maxDepth = arguments["maxDepth"]?.Value<int>() ?? 3;

            // Enforce constraints
            if (maxDepth < 1) maxDepth = 1;
            if (maxDepth > 5) maxDepth = 5;

            if (string.IsNullOrEmpty(sourceArg) || string.IsNullOrEmpty(targetArg))
                return "Erreur : 'sourceType' et 'targetType' sont requis.";

            var resolver = _resolverProvider();
            var graph = _graphProvider();
            var pathFinder = new PathFinder(graph);

            var source = resolver.Resolve(sourceArg);
            var target = resolver.Resolve(targetArg);

            if (!source.Found) return FormatError(resolver, "Source", sourceArg, source.Alternatives);
            if (!target.Found) return FormatError(resolver, "Cible", targetArg, target.Alternatives);

            // Find all paths up to limit 10
            var paths = pathFinder.FindAllPaths(source.FullName, target.FullName, maxDepth, 10);

            if (paths == null || paths.Count == 0)
            {
                var errorSb = new StringBuilder();
                errorSb.AppendLine($"Aucun chemin trouvé entre '{source.FullName}' et '{target.FullName}' (maxDepth: {maxDepth}).");
                errorSb.AppendLine();
                errorSb.AppendLine("Suggestions :");
                errorSb.AppendLine($"- Augmenter maxDepth (actuel: {maxDepth}, max: 5)");
                errorSb.AppendLine("- Vérifier les noms de types avec topsolid_api_help");
                errorSb.AppendLine("- Les types Void, Int32, String sont des feuilles sans chemin sortant");
                return errorSb.ToString();
            }

            // 1. Sort paths by cost
            paths.Sort((a, b) => a.Sum(e => e.Weight).CompareTo(b.Sum(e => e.Weight)));

            var sb = new StringBuilder();
            sb.AppendLine($"{paths.Count} chemin(s) de {sourceArg} à {targetArg} (maxDepth: {maxDepth}) :");
            sb.AppendLine();

            // 2. Recommended path (first one)
            var bestPath = paths[0];
            int bestCost = bestPath.Sum(e => e.Weight);
            string label = paths.Count == 1 ? "Chemin unique" : "Chemin 1";
            sb.AppendLine($"★ RECOMMANDE — {label} (coût {bestCost}, {bestPath.Count} étape(s)) :");

            for (int j = 0; j < bestPath.Count; j++)
            {
                var edge = bestPath[j];
                var sourceShort = edge.Source.TypeName.Split('.').Last();
                var targetShort = edge.Target.TypeName.Split('.').Last();
                sb.AppendLine($"  {j + 1}. {sourceShort}.{edge.MethodName} → {targetShort}");
                if (!string.IsNullOrEmpty(edge.MethodSignature))
                {
                    sb.AppendLine($"     Signature: {edge.MethodSignature}");
                }
                if (!string.IsNullOrEmpty(edge.SemanticHint))
                {
                    sb.AppendLine($"     Note: {edge.SemanticHint}");
                }
            }

            // 3. Alternatives (the rest)
            if (paths.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine("Alternatives :");
                for (int i = 1; i < paths.Count; i++)
                {
                    var path = paths[i];
                    int totalCost = path.Sum(e => e.Weight);
                    sb.AppendLine($"  Chemin {i + 1} (coût {totalCost}, {path.Count} étapes) : {SummarizePath(path)}");
                }
            }

            return sb.ToString();
        }

        private string SummarizePath(List<GraphEdge> path)
        {
            var types = new List<string>();
            types.Add(path[0].Source.TypeName.Split('.').Last());
            foreach (var edge in path)
            {
                types.Add(edge.Target.TypeName.Split('.').Last());
            }
            string via = path.Count > 1
                ? " (via " + path[0].MethodName + " puis " + path[path.Count - 1].MethodName + ")"
                : " (via " + path[0].MethodName + ")";
            return string.Join(" → ", types) + via;
        }

        private string FormatError(TypeNameResolver resolver, string label, string original, List<string> alternatives)
        {
            if (alternatives != null && alternatives.Count > 0)
            {
                return $"{label} '{original}' est ambigu. Plusieurs types trouvés :\n- " + string.Join("\n- ", alternatives);
            }
            
            var suggestions = resolver.GetSuggestions(original);
            if (suggestions.Count > 0)
            {
                return $"{label} '{original}' non trouvé. Suggestions :\n- " + string.Join("\n- ", suggestions);
            }

            return $"{label} '{original}' non trouvé dans le graphe API.";
        }
    }
}
