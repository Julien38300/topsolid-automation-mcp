using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using TopSolidApiGraph.Core.Models;
using TopSolidApiGraph.Core.Utils;
using TopSolidApiGraph.Core.Semantics;

namespace TopSolidApiGraph.Core
{
    /// <summary>
    /// Builds and manages the typed directed graph from extracted methods.
    /// </summary>
    public class TypeGraph
    {
        [JsonProperty]
        private readonly Dictionary<string, TypeNode> _nodes = new Dictionary<string, TypeNode>();
        
        [JsonProperty]
        private readonly List<GraphEdge> _edges = new List<GraphEdge>();
        
        
        private readonly Dictionary<string, List<GraphEdge>> _adjacencyList = new Dictionary<string, List<GraphEdge>>();

        [JsonIgnore]
        private Dictionary<string, List<GraphEdge>> _keywordIndex;

        [JsonIgnore]
        private SemanticRuleSet _semanticRules = new SemanticRuleSet(null);

        /// <summary>
        /// Loads semantic rules to apply during graph building.
        /// Must be called before Build().
        /// </summary>
        public void LoadSemanticRules(string rulesFilePath)
        {
            _semanticRules = SemanticRuleSet.LoadFromJson(rulesFilePath);
        }

        /// <summary>
        /// Builds the graph from a list of extracted methods.
        /// </summary>
        /// <param name="methods">The list of methods to process.</param>
        public void Build(List<ExtractedMethod> methods)
        {
            if (methods == null || methods.Count == 0)
            {
                Console.WriteLine("[TypeGraph] Warning: Empty method list provided.");
                return;
            }

            _nodes.Clear();
            _edges.Clear();

            foreach (var method in methods)
            {
                // Create target node
                TypeNode targetNode = GetOrCreateNode(method.ReturnType);

                // Start from System.Void for static methods with no parameters
                if (method.IsStatic && method.Parameters.Count == 0)
                {
                    TypeNode voidNode = GetOrCreateNode("System.Void");
                    AddEdge(voidNode, targetNode, method);
                }

                // Create source nodes from parameters
                foreach (var paramType in method.Parameters)
                {
                    TypeNode sourceNode = GetOrCreateNode(paramType);
                    AddEdge(sourceNode, targetNode, method);
                }

                // Always add edge from declaring type to return type.
                // For static methods, this makes the host class (e.g. TopSolidHost) visible.
                // For instance methods, this was already the behavior.
                {
                    TypeNode sourceNode = GetOrCreateNode(method.DeclaringType);
                    AddEdge(sourceNode, targetNode, method);
                }
            }

            RebuildAdjacencyList();

            Console.WriteLine($"[TypeGraph] Built graph with {_nodes.Count} nodes and {_edges.Count} edges.");
        }

        /// <summary>
        /// Rebuilds the internal adjacency list from the list of edges.
        /// Must be called after loading from JSON.
        /// </summary>
        public void RebuildAdjacencyList()
        {
            _adjacencyList.Clear();
            foreach (var edge in _edges)
            {
                if (!_adjacencyList.ContainsKey(edge.Source.TypeName))
                {
                    _adjacencyList[edge.Source.TypeName] = new List<GraphEdge>();
                }
                _adjacencyList[edge.Source.TypeName].Add(edge);
            }
        }

        /// <summary>
        /// Builds an inverted index mapping keywords to graph edges.
        /// </summary>
        public void BuildKeywordIndex()
        {
            _keywordIndex = new Dictionary<string, List<GraphEdge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in _edges)
            {
                // Index by method name words
                var words = SplitCamelCase(edge.MethodName);
                foreach (var word in words)
                {
                    if (!_keywordIndex.ContainsKey(word))
                        _keywordIndex[word] = new List<GraphEdge>();

                    // Avoid duplicates if multiple words map to the same list
                    if (!_keywordIndex[word].Contains(edge))
                    {
                        _keywordIndex[word].Add(edge);
                    }
                }

                // Index by interface name if available
                if (!string.IsNullOrEmpty(edge.Interface))
                {
                    if (!_keywordIndex.ContainsKey(edge.Interface))
                        _keywordIndex[edge.Interface] = new List<GraphEdge>();

                    if (!_keywordIndex[edge.Interface].Contains(edge))
                    {
                        _keywordIndex[edge.Interface].Add(edge);
                    }
                }
            }
        }

        private static string[] SplitCamelCase(string name)
        {
            // "GetRealValue" → ["Get", "Real", "Value"]
            if (string.IsNullOrEmpty(name)) return new string[0];
            return Regex.Split(name, @"(?=[A-Z])").Where(s => s.Length > 1).ToArray();
        }

        /// <summary>
        /// Searches for edges that match all provided keywords.
        /// </summary>
        public List<GraphEdge> SearchByKeywords(string[] keywords, int maxResults = 30)
        {
            if (_keywordIndex == null || keywords == null || keywords.Length == 0)
                return new List<GraphEdge>();

            // Find match lists for each keyword
            var matchLists = new List<List<GraphEdge>>();
            foreach (var kw in keywords)
            {
                if (_keywordIndex.TryGetValue(kw, out var list))
                {
                    matchLists.Add(list);
                }
                else
                {
                    // If any keyword doesn't match anything, we fail fast (intersection will be empty)
                    // We could also do partial matches here if needed, but strict is safer to avoid huge outputs
                    return new List<GraphEdge>();
                }
            }

            if (matchLists.Count == 0) return new List<GraphEdge>();

            // Intersect all lists
            var resultKeys = new HashSet<GraphEdge>(matchLists[0]);
            for (int i = 1; i < matchLists.Count; i++)
            {
                resultKeys.IntersectWith(matchLists[i]);
            }

            return resultKeys.Take(maxResults).ToList();
        }

        private TypeNode GetOrCreateNode(string typeName)
        {
            if (!_nodes.ContainsKey(typeName))
            {
                _nodes[typeName] = new TypeNode(typeName);
            }
            return _nodes[typeName];
        }

        private void AddEdge(TypeNode source, TypeNode target, ExtractedMethod method)
        {
            if (source.TypeName == target.TypeName) return;

            // Apply semantic rules
            var rule = _semanticRules.FindMatch(method.Name, target.TypeName);

            // Pruning: exclude edge entirely if rule says so
            if (rule != null && rule.Exclude)
            {
                return;
            }

            // Weight: use rule override if present, otherwise default calculation
            int weight = (rule != null && rule.WeightOverride.HasValue)
                ? rule.WeightOverride.Value
                : GetWeight(target.TypeName);

            _edges.Add(new GraphEdge
            {
                Source = source,
                Target = target,
                MethodName = method.Name,
                MethodSignature = method.ToString(),
                IsStatic = method.IsStatic,
                Weight = weight,
                SemanticHint = rule?.Instruction
            });
        }

        private int GetWeight(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return 10;

            // Primitive types as defined by the user
            string[] primitives = { 
                "Boolean", "Int32", "String", "Object", "Void", "Byte[]", "Double",
                "System.Boolean", "System.Int32", "System.String", "System.Object", "System.Void", "System.Byte[]", "System.Double"
            };

            foreach (var p in primitives)
            {
                if (typeName.Equals(p, StringComparison.OrdinalIgnoreCase))
                    return 10;
            }

            return 1;
        }

        /// <summary>
        /// Saves the graph to a JSON file.
        /// </summary>
        /// <param name="outputFilePath">The path to the output JSON file.</param>
        public void SaveToJson(string outputFilePath)
        {
            JsonHelper.Save(this, outputFilePath);
        }

        /// <summary>
        /// Gets all edges in the graph.
        /// </summary>
        /// <returns>The list of all graph edges.</returns>
        public List<GraphEdge> GetEdges()
        {
            return _edges;
        }

        /// <summary>
        /// Gets all connections for a given source node.
        /// </summary>
        /// <param name="source">The source type node.</param>
        /// <returns>All edges originating from the source.</returns>
        public List<GraphEdge> GetNeighbors(TypeNode source)
        {
            if (source == null) return new List<GraphEdge>();
            if (_adjacencyList.TryGetValue(source.TypeName, out var neighbors))
            {
                return neighbors;
            }
            return new List<GraphEdge>();
        }
    }
}
