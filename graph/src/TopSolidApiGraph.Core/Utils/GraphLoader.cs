using System;
using System.IO;

namespace TopSolidApiGraph.Core.Utils
{
    /// <summary>
    /// Utility class to load a pre-built graph from JSON.
    /// </summary>
    public static class GraphLoader
    {
        /// <summary>
        /// Loads a pre-built graph from a JSON file.
        /// </summary>
        /// <param name="graphJsonPath">The path to the graph.json file.</param>
        /// <returns>A functional TypeGraph instance.</returns>
        public static TypeGraph LoadFromJson(string graphJsonPath)
        {
            if (!File.Exists(graphJsonPath))
            {
                throw new FileNotFoundException("Graph JSON file not found.", graphJsonPath);
            }

            Console.WriteLine($"[GraphLoader] Loading graph from {graphJsonPath}...");
            
            // Use JsonHelper to deserialize directly into TypeGraph
            // Note: TypeGraph must have its fields marked with [JsonProperty] or be public for this to work
            // I've adjusted TypeGraph to support this.
            TypeGraph graph = JsonHelper.Load<TypeGraph>(graphJsonPath);

            if (graph != null)
            {
                // Rebuild the adjacency list after deserialization
                graph.RebuildAdjacencyList();
                Console.WriteLine($"[GraphLoader] Successfully loaded graph with {graph.GetEdges().Count} edges.");
            }

            return graph;
        }
    }
}
