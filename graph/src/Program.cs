using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TopSolidApiGraph.Core;
using TopSolidApiGraph.Core.Models;
using TopSolidApiGraph.Core.Utils;

namespace TopSolidApiGraph
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("[Program] Starting TopSolid API Graph Builder...");

            string binDir = @"C:\Program Files\TOPSOLID\TopSolid 7.20\bin\";
            string[] assemblies = {
                "TopSolid.Kernel.Automating.dll",
                "TopSolid.Cad.Design.Automating.dll",
                "TopSolid.Cad.Drafting.Automating.dll"
            };
            
            string outputDir = AppDomain.CurrentDomain.BaseDirectory;
            var allMethods = new List<ExtractedMethod>();
            var extractor = new ApiExtractor();

            Console.WriteLine($"[Program] Phase 1: Extraction from {assemblies.Length} assemblies");
            
            foreach (var assemblyName in assemblies)
            {
                string assemblyPath = Path.Combine(binDir, assemblyName);
                if (File.Exists(assemblyPath))
                {
                    Console.WriteLine($"[Program] Scanning: {assemblyName}");
                    var methods = extractor.Extract(assemblyPath);
                    allMethods.AddRange(methods);
                }
                else
                {
                    Console.WriteLine($"[Program] Error: Assembly not found at {assemblyPath}");
                }
            }

            JsonHelper.Save(allMethods, Path.Combine(outputDir, "methods.json"));

            // 2. Build Graph
            Console.WriteLine("\n[Program] Phase 2: Building Graph");
            var graph = new TypeGraph();
            string rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "semantic-rules.json");
            graph.LoadSemanticRules(rulesPath);
            graph.Build(allMethods);
            graph.SaveToJson(Path.Combine(outputDir, "graph.json"));

            // 3. User Targeted Pathfinding Runs
            Console.WriteLine("\n[Program] Phase 3: Targeted User Queries");
            var finder = new PathFinder(graph);

            // Q1: IPdm -> String
            RunWeightedQuery(finder, "TopSolid.Kernel.Automating.IPdm", "System.String", "Q1: IPdm -> String");

            // Q2: PdmObjectId -> ElementId
            RunWeightedQuery(finder, "TopSolid.Kernel.Automating.PdmObjectId", "TopSolid.Kernel.Automating.ElementId", "Q2: PdmObjectId -> ElementId");

            // Q3: DocumentId -> String
            RunWeightedQuery(finder, "TopSolid.Kernel.Automating.DocumentId", "System.String", "Q3: DocumentId -> String");

            // Q4: ElementId -> String
            RunWeightedQuery(finder, "TopSolid.Kernel.Automating.ElementId", "System.String", "Q4: ElementId -> String");

            // Q5: IPdm -> PdmObjectId (All Paths, maxDepth 3)
            Console.WriteLine("\n[Q5: IPdm -> PdmObjectId (All Paths, maxDepth 3)]");
            var allPathsPdm = finder.FindAllPaths("TopSolid.Kernel.Automating.IPdm", "TopSolid.Kernel.Automating.PdmObjectId", 3);
            DisplayAllPaths(allPathsPdm);

            Console.WriteLine("\n[Program] User Queries Completed.");
        }

        private static void RunWeightedQuery(PathFinder finder, string source, string target, string label)
        {
            Console.WriteLine($"\n[{label}]");
            var path = finder.FindPathWeighted(source, target);
            DisplayWeightedPath(path);
        }

        private static void DisplayWeightedPath(List<GraphEdge> path)
        {
            if (path.Count == 0)
            {
                Console.WriteLine("  No path found.");
                return;
            }

            int totalCost = 0;
            foreach (var edge in path)
            {
                string hint = !string.IsNullOrEmpty(edge.SemanticHint) ? $" [Hint: {edge.SemanticHint}]" : "";
                Console.WriteLine($"  -> {edge.MethodSignature} (Cost: {edge.Weight}){hint} -> {edge.Target.TypeName}");
                totalCost += edge.Weight;
            }
            Console.WriteLine($"  Total Path Cost: {totalCost}");
        }

        private static void DisplayAllPaths(List<List<GraphEdge>> paths)
        {
            if (paths.Count == 0)
            {
                Console.WriteLine("  No paths found.");
                return;
            }

            for (int i = 0; i < paths.Count && i < 10; i++)
            {
                Console.WriteLine($"  Path #{i + 1}:");
                foreach (var edge in paths[i])
                {
                    Console.WriteLine($"    -> {edge.MethodSignature} -> {edge.Target.TypeName}");
                }
            }
        }
    }
}
