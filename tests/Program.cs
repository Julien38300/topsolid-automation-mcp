using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpTestRunner
{
    /// <summary>
    /// Entry point for the MCP integration test runner.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string mcpServerPath = args.Length > 0 ? args[0]
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    @"..\..\src\bin\Debug\net48\TopSolidMcpServer.exe"));

            bool updateBaselines = args.Length > 1 && args[1] == "--update-baselines";

            string testSuitePath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestSuite.json");
            string baselinesPath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Baselines.json");
            string resultsDir     = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestResults");

            Console.OutputEncoding = Encoding.UTF8;
            Directory.CreateDirectory(resultsDir);

            TestSuite suite = LoadTestSuite(testSuitePath);
            if (suite == null) return 1;

            BaselinesFile baselines = LoadBaselines(baselinesPath);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            Console.WriteLine($"=== MCP Test Suite — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

            if (!File.Exists(mcpServerPath))
            {
                Console.Error.WriteLine($"[ERROR] MCP server not found: {mcpServerPath}");
                return 1;
            }

            var runner = new TestRunner(mcpServerPath, suite, baselines, updateBaselines);
            List<TestResult> results = runner.RunAll();

            // Console summary
            int passed = 0, failed = 0;
            foreach (var r in results)
            {
                string status  = r.Pass ? "[PASS]" : "[FAIL]";
                string latency = $"{r.ElapsedMs / 1000.0:F2}s";
                string baselineTag = r.BaselineMs > 0
                    ? $" < {r.BaselineMs / 1000.0:F2}s baseline"
                    : " (no baseline)";

                string extra = r.Pass ? string.Empty : $" — {r.FailReason}";
                Console.WriteLine($"{status} {r.Id,-5} {r.Name,-35} ({latency}{(r.Pass ? baselineTag : string.Empty)}){extra}");

                if (r.Pass) passed++; else failed++;
            }

            Console.WriteLine(new string('=', 72));
            Console.WriteLine($"TOTAL: {passed}/{results.Count} PASS, {failed} FAIL");

            // Write JSON result file
            string resultPath = Path.Combine(resultsDir, $"{timestamp}.json");
            File.WriteAllText(resultPath, JsonConvert.SerializeObject(results, Formatting.Indented), Encoding.UTF8);
            Console.WriteLine($"Results written to: {resultPath}");

            // Optionally update baselines
            if (updateBaselines)
            {
                foreach (var r in results)
                {
                    if (r.Pass)
                        baselines.Baselines[r.Id] = r.ElapsedMs;
                }
                SaveBaselines(baselinesPath, baselines);
                Console.WriteLine("Baselines updated.");
            }

            return failed > 0 ? 1 : 0;
        }

        private static TestSuite LoadTestSuite(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[ERROR] TestSuite.json not found: {path}");
                return null;
            }
            return JsonConvert.DeserializeObject<TestSuite>(File.ReadAllText(path, Encoding.UTF8));
        }

        private static BaselinesFile LoadBaselines(string path)
        {
            if (!File.Exists(path))
                return new BaselinesFile { RegressionFactor = 2.0, Baselines = new Dictionary<string, long>() };

            return JsonConvert.DeserializeObject<BaselinesFile>(File.ReadAllText(path, Encoding.UTF8))
                   ?? new BaselinesFile { RegressionFactor = 2.0, Baselines = new Dictionary<string, long>() };
        }

        private static void SaveBaselines(string path, BaselinesFile baselines)
        {
            baselines.Created = DateTime.Now.ToString("yyyy-MM-dd");
            File.WriteAllText(path, JsonConvert.SerializeObject(baselines, Formatting.Indented), Encoding.UTF8);
        }
    }
}
