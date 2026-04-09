using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolidApiGraph.Core;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to find paths between types in the TopSolid API graph.
    /// </summary>
    public class FindPathTool
    {
        private readonly Func<TypeGraph> _graphProvider;
        private readonly Func<TypeNameResolver> _resolverProvider;

        public FindPathTool(Func<TypeGraph> graphProvider, Func<TypeNameResolver> resolverProvider)
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
                Name = "topsolid_find_path",
                Description = "Trouve le chemin de méthodes le plus court entre deux types TopSolid.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["sourceType"] = new JObject { ["type"] = "string", ["description"] = "Type source (ex: 'IPdm', 'PdmObjectId', 'void')" },
                        ["targetType"] = new JObject { ["type"] = "string", ["description"] = "Type cible (ex: 'String', 'ElementId')" }
                    },
                    ["required"] = new JArray { "sourceType", "targetType" }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes the pathfinding logic.
        /// </summary>
        public string Execute(JObject arguments)
        {
            var sourceArg = arguments["sourceType"]?.ToString();
            var targetArg = arguments["targetType"]?.ToString();

            if (string.IsNullOrEmpty(sourceArg) || string.IsNullOrEmpty(targetArg))
                return "Erreur : 'sourceType' et 'targetType' sont requis.";

            var resolver = _resolverProvider();
            var graph = _graphProvider();
            var pathFinder = new PathFinder(graph);

            var source = resolver.Resolve(sourceArg);
            var target = resolver.Resolve(targetArg);

            if (!source.Found) return FormatError(resolver, "Source", sourceArg, source.Alternatives);
            if (!target.Found) return FormatError(resolver, "Cible", targetArg, target.Alternatives);

            // Use weighted Dijkstra
            var path = pathFinder.FindPathWeighted(source.FullName, target.FullName);

            if (path == null || path.Count == 0)
            {
                if (source.FullName == target.FullName)
                    return $"Les types source et cible sont identiques ({source.FullName}). Aucun chemin nécessaire.";
                
                return $"Aucun chemin trouvé entre '{source.FullName}' et '{target.FullName}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Pour aller de {sourceArg} à {targetArg} (via {source.FullName} -> {target.FullName}), appelez dans l'ordre :");
            
            int totalWeight = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var edge = path[i];
                totalWeight += edge.Weight;
                var sourceShort = edge.Source.TypeName.Split('.').Last();
                var targetShort = edge.Target.TypeName.Split('.').Last();
                sb.AppendLine($"{i + 1}. {sourceShort}.{edge.MethodName} -> {targetShort} (coût : {edge.Weight})");
                sb.AppendLine($"   Signature: {edge.MethodSignature}");
                if (!string.IsNullOrEmpty(edge.SemanticHint))
                {
                    sb.AppendLine($"   Note: {edge.SemanticHint}");
                }
            }
            sb.AppendLine($"Coût total : {totalWeight}");

            return sb.ToString();
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
