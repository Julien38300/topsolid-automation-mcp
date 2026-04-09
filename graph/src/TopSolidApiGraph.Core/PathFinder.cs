using System;
using System.Collections.Generic;
using System.Linq;
using TopSolidApiGraph.Core.Models;

namespace TopSolidApiGraph.Core
{
    /// <summary>
    /// Implements search algorithms to find paths between types in the API graph.
    /// </summary>
    public class PathFinder
    {
        private readonly TypeGraph _graph;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathFinder"/> class with a given graph.
        /// </summary>
        /// <param name="graph">The graph to search in.</param>
        public PathFinder(TypeGraph graph)
        {
            _graph = graph;
        }

        /// <summary>
        /// Finds the shortest path (method chain) between source and target types using BFS.
        /// </summary>
        /// <param name="sourceType">The full name of the starting type.</param>
        /// <param name="targetType">The full name of the destination type.</param>
        /// <returns>A list of graph edges (the method chain), or an empty list if no path found.</returns>
        public List<GraphEdge> FindPath(string sourceType, string targetType)
        {
            if (sourceType == targetType) return new List<GraphEdge>();

            var queue = new Queue<List<GraphEdge>>();
            var visited = new HashSet<string>();

            // Initialize queue with initial neighbors
            var startNode = new TypeNode(sourceType);
            foreach (var edge in _graph.GetNeighbors(startNode))
            {
                queue.Enqueue(new List<GraphEdge> { edge });
            }
            visited.Add(sourceType);

            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                var lastEdge = currentPath.Last();
                var currentNodeName = lastEdge.Target.TypeName;

                if (currentNodeName == targetType)
                {
                    return currentPath;
                }

                if (!visited.Contains(currentNodeName))
                {
                    visited.Add(currentNodeName);
                    
                    var neighbors = _graph.GetNeighbors(new TypeNode(currentNodeName));
                    foreach (var edge in neighbors)
                    {
                        if (!visited.Contains(edge.Target.TypeName))
                        {
                            var newPath = new List<GraphEdge>(currentPath) { edge };
                            queue.Enqueue(newPath);
                        }
                    }
                }
            }

            return new List<GraphEdge>();
        }

        /// <summary>
        /// Finds all paths (method chains) between source and target types up to a maximum depth.
        /// </summary>
        /// <param name="sourceType">The full name of the starting type.</param>
        /// <param name="targetType">The full name of the destination type.</param>
        /// <param name="maxDepth">The maximum number of steps allowed.</param>
        /// <param name="maxPaths">The maximum number of paths to find.</param>
        /// <returns>A list of paths, where each path is a list of edges.</returns>
        public List<List<GraphEdge>> FindAllPaths(string sourceType, string targetType, int maxDepth, int maxPaths = 100, int timeoutMs = 5000)
        {
            var results = new List<List<GraphEdge>>();
            if (sourceType == targetType || maxDepth <= 0) return results;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var queue = new Queue<List<GraphEdge>>();
            const int maxQueueSize = 10000;

            var initialNeighbors = _graph.GetNeighbors(new TypeNode(sourceType));
            foreach (var edge in initialNeighbors)
            {
                queue.Enqueue(new List<GraphEdge> { edge });
            }

            while (queue.Count > 0 && results.Count < maxPaths)
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    Console.Error.WriteLine("[PathFinder] BFS timeout after " + sw.ElapsedMilliseconds + "ms, returning " + results.Count + " paths found so far.");
                    break;
                }

                if (queue.Count >= maxQueueSize)
                    continue;

                var currentPath = queue.Dequeue();
                var lastEdge = currentPath[currentPath.Count - 1];
                var currentNodeName = lastEdge.Target.TypeName;

                if (currentNodeName == targetType)
                {
                    results.Add(currentPath);
                    continue;
                }

                if (currentPath.Count < maxDepth)
                {
                    var neighbors = _graph.GetNeighbors(new TypeNode(currentNodeName));
                    foreach (var edge in neighbors)
                    {
                        if (currentPath.Any(e => e.Source.TypeName == edge.Target.TypeName))
                            continue;

                        var newPath = new List<GraphEdge>(currentPath) { edge };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return results.OrderBy(p => p.Sum(e => e.Weight)).ToList();
        }

        /// <summary>
        /// Finds the shortest path using Dijkstra's algorithm based on edge weights.
        /// </summary>
        /// <param name="currTypeName">The source type name.</param>
        /// <param name="targetTypeName">The target type name.</param>
        /// <returns>The path as a list of edges, or an empty list if no path exists.</returns>
        public List<GraphEdge> FindPathWeighted(string currTypeName, string targetTypeName, int timeoutMs = 5000)
        {
            if (string.IsNullOrEmpty(currTypeName) || string.IsNullOrEmpty(targetTypeName))
                return new List<GraphEdge>();

            if (currTypeName == targetTypeName)
                return new List<GraphEdge>();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var distances = new Dictionary<string, int>();
            var previous = new Dictionary<string, GraphEdge>();
            var priorityQueue = new SortedSet<NodeDistance>();

            distances[currTypeName] = 0;
            priorityQueue.Add(new NodeDistance(0, currTypeName));

            while (priorityQueue.Count > 0)
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    Console.Error.WriteLine("[PathFinder] Dijkstra timeout after " + sw.ElapsedMilliseconds + "ms, operation aborted.");
                    break;
                }

                var smallest = priorityQueue.Min;
                priorityQueue.Remove(smallest);

                string current = smallest.TypeName;

                if (current == targetTypeName) break;
                if (smallest.Distance > (distances.ContainsKey(current) ? distances[current] : int.MaxValue)) continue;

                var neighbors = _graph.GetNeighbors(new TypeNode(current));
                foreach (var edge in neighbors)
                {
                    string neighborName = edge.Target.TypeName;
                    int newDist = smallest.Distance + edge.Weight;

                    if (!distances.ContainsKey(neighborName) || newDist < distances[neighborName])
                    {
                        if (distances.ContainsKey(neighborName))
                        {
                            priorityQueue.Remove(new NodeDistance(distances[neighborName], neighborName));
                        }

                        distances[neighborName] = newDist;
                        previous[neighborName] = edge;
                        priorityQueue.Add(new NodeDistance(newDist, neighborName));
                    }
                }
            }

            var path = new List<GraphEdge>();
            string temp = targetTypeName;
            if (!previous.ContainsKey(temp)) return path;

            while (previous.ContainsKey(temp))
            {
                path.Add(previous[temp]);
                temp = previous[temp].Source.TypeName;
            }

            path.Reverse();
            return path;
        }

        private struct NodeDistance : IComparable<NodeDistance>
        {
            public int Distance;
            public string TypeName;

            public NodeDistance(int distance, string typeName)
            {
                Distance = distance;
                TypeName = typeName;
            }

            public int CompareTo(NodeDistance other)
            {
                int res = Distance.CompareTo(other.Distance);
                if (res == 0) res = string.Compare(TypeName, other.TypeName, StringComparison.Ordinal);
                return res;
            }
        }
    }
}
