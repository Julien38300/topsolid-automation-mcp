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
        /// <summary>Maximum size of the returned text, to keep a single call from flooding the caller's context.</summary>
        private const int MaxOutputChars = 8000;

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
                Description = "Find the shortest chain of API methods between two TopSolid types.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["sourceType"] = new JObject { ["type"] = "string", ["description"] = "Source type (e.g. 'IPdm', 'PdmObjectId', 'void')" },
                        ["targetType"] = new JObject { ["type"] = "string", ["description"] = "Target type (e.g. 'String', 'ElementId')" }
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
            return Truncate(ExecuteCore(arguments));
        }

        private string ExecuteCore(JObject arguments)
        {
            var sourceArg = arguments["sourceType"]?.ToString();
            var targetArg = arguments["targetType"]?.ToString();

            if (string.IsNullOrEmpty(sourceArg) || string.IsNullOrEmpty(targetArg))
                return "Error: 'sourceType' and 'targetType' are required.";

            var resolver = _resolverProvider();
            var graph = _graphProvider();
            var pathFinder = new PathFinder(graph);

            var source = resolver.Resolve(sourceArg);
            var target = resolver.Resolve(targetArg);

            if (!source.Found) return FormatError(resolver, "Source", sourceArg, source.Alternatives);
            if (!target.Found) return FormatError(resolver, "Target", targetArg, target.Alternatives);

            // Use weighted Dijkstra
            var path = pathFinder.FindPathWeighted(source.FullName, target.FullName);

            if (path == null || path.Count == 0)
            {
                if (source.FullName == target.FullName)
                    return $"Source and target types are identical ({source.FullName}). No path needed.";

                return $"No path found between '{source.FullName}' and '{target.FullName}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"To go from {sourceArg} to {targetArg} (via {source.FullName} -> {target.FullName}), call in order:");

            int totalWeight = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var edge = path[i];
                totalWeight += edge.Weight;
                var sourceShort = edge.Source.TypeName.Split('.').Last();
                var targetShort = edge.Target.TypeName.Split('.').Last();
                sb.AppendLine($"{i + 1}. {sourceShort}.{edge.MethodName} -> {targetShort} (cost: {edge.Weight})");
                sb.AppendLine($"   Signature: {edge.MethodSignature}");
                if (!string.IsNullOrEmpty(edge.SemanticHint))
                {
                    sb.AppendLine($"   Note: {edge.SemanticHint}");
                }
            }
            sb.AppendLine($"Total cost: {totalWeight}");

            return sb.ToString();
        }

        private string FormatError(TypeNameResolver resolver, string label, string original, List<string> alternatives)
        {
            if (alternatives != null && alternatives.Count > 0)
            {
                return $"{label} '{original}' is ambiguous. Several types found:\n- " + string.Join("\n- ", alternatives);
            }

            var suggestions = resolver.GetSuggestions(original);
            if (suggestions.Count > 0)
            {
                return $"{label} '{original}' not found. Suggestions:\n- " + string.Join("\n- ", suggestions);
            }

            return $"{label} '{original}' not found in the API graph.";
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
