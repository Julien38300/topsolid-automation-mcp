using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpTestRunner
{
    /// <summary>
    /// Manages the lifecycle of the MCP server process and runs all tests.
    /// </summary>
    internal sealed class TestRunner
    {
        private const int TimeoutMs      = 30_000;
        private const int InitTimeoutMs  = 10_000;

        private readonly string       _mcpServerPath;
        private readonly TestSuite    _suite;
        private readonly BaselinesFile _baselines;
        private readonly bool         _updateBaselines;

        public TestRunner(string mcpServerPath, TestSuite suite, BaselinesFile baselines, bool updateBaselines)
        {
            _mcpServerPath   = mcpServerPath;
            _suite           = suite;
            _baselines       = baselines;
            _updateBaselines = updateBaselines;
        }

        /// <summary>Runs all tests and returns one result per test case.</summary>
        public List<TestResult> RunAll()
        {
            var results = new List<TestResult>();

            var psi = new ProcessStartInfo
            {
                FileName               = _mcpServerPath,
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            Process process = null;
            try
            {
                process = Process.Start(psi);

                // Drain stderr in background to avoid deadlock
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        Console.Error.WriteLine($"[SERVER] {e.Data}");
                };
                process.BeginErrorReadLine();

                // MCP handshake: initialize
                if (!Initialize(process))
                {
                    Console.Error.WriteLine("[ERROR] MCP initialize failed — aborting tests.");
                    foreach (var t in _suite.Tests)
                        results.Add(TestResult.Aborted(t, "MCP initialize failed"));
                    return results;
                }

                foreach (var test in _suite.Tests)
                    results.Add(RunTest(process, test));
            }
            finally
            {
                try { process?.Kill(); } catch { }
                try { process?.Dispose(); } catch { }
            }

            return results;
        }

        private bool Initialize(Process process)
        {
            var initRequest = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = 1,
                ["method"]  = "initialize",
                ["params"]  = new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"]    = new JObject(),
                    ["clientInfo"]      = new JObject { ["name"] = "McpTestRunner", ["version"] = "1.0" }
                }
            };

            SendRequest(process, initRequest.ToString(Formatting.None));
            string response = ReadLineWithTimeout(process, InitTimeoutMs);
            if (string.IsNullOrEmpty(response)) return false;

            // Send notifications/initialized
            var notif = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"]  = "notifications/initialized",
                ["params"]  = new JObject()
            };
            SendRequest(process, notif.ToString(Formatting.None));
            return true;
        }

        private TestResult RunTest(Process process, TestCase test)
        {
            long baselineMs = 0;
            _baselines.Baselines.TryGetValue(test.Id, out baselineMs);
            if (baselineMs == 0) baselineMs = test.BaselineMs;

            string requestJson = JsonConvert.SerializeObject(test.Request);

            var sw = Stopwatch.StartNew();
            SendRequest(process, requestJson);
            string response = ReadLineWithTimeout(process, TimeoutMs);
            sw.Stop();

            if (response == null)
                return TestResult.Failed(test, sw.ElapsedMilliseconds, baselineMs, "Timeout (>30s)");

            // Extract text content from MCP response
            string text = ExtractText(response);

            // Functional assertions
            var assertions = test.Assertions;
            if (assertions != null)
            {
                if (assertions.Contains != null)
                {
                    foreach (var token in assertions.Contains)
                    {
                        if (!text.Contains(token))
                        {
                            Console.Error.WriteLine(string.Format("[DEBUG] Test {0} FAILED. Expected: {1}, Got: {2}", test.Id, token, text));
                            return TestResult.Failed(test, sw.ElapsedMilliseconds, baselineMs,
                                string.Format("Missing expected text: \"{0}\"", token));
                        }
                    }
                }

                if (assertions.NotContains != null)
                {
                    foreach (var token in assertions.NotContains)
                    {
                        if (text.Contains(token))
                        {
                            Console.Error.WriteLine(string.Format("[DEBUG] Test {0} FAILED. Unexpected: {1}, Got: {2}", test.Id, token, text));
                            return TestResult.Failed(test, sw.ElapsedMilliseconds, baselineMs,
                                string.Format("Unexpected text found: \"{0}\"", token));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(assertions.Pattern))
                {
                    if (!Regex.IsMatch(text, assertions.Pattern))
                        return TestResult.Failed(test, sw.ElapsedMilliseconds, baselineMs,
                            $"Pattern not matched: \"{assertions.Pattern}\"");
                }
            }

            // Performance assertion
            double regressionFactor = _baselines.RegressionFactor > 0 ? _baselines.RegressionFactor : 2.0;
            if (baselineMs > 0 && sw.ElapsedMilliseconds > baselineMs * regressionFactor)
                return TestResult.Failed(test, sw.ElapsedMilliseconds, baselineMs,
                    $"REGRESSION PERF ({sw.ElapsedMilliseconds}ms > {baselineMs * regressionFactor}ms)");

            return TestResult.Passed(test, sw.ElapsedMilliseconds, baselineMs);
        }

        private static void SendRequest(Process process, string json)
        {
            process.StandardInput.WriteLine(json);
            process.StandardInput.Flush();
        }

        private static string ReadLineWithTimeout(Process process, int timeoutMs)
        {
            string result  = null;
            bool   done    = false;
            var    thread  = new Thread(() =>
            {
                try { result = process.StandardOutput.ReadLine(); }
                catch { }
                finally { done = true; }
            });
            thread.IsBackground = true;
            thread.Start();

            int elapsed = 0;
            while (!done && elapsed < timeoutMs)
            {
                Thread.Sleep(50);
                elapsed += 50;
            }

            return done ? result : null;
        }

        /// <summary>Extracts the plain text content from a MCP tools/call JSON response.</summary>
        private static string ExtractText(string responseJson)
        {
            try
            {
                var obj = JObject.Parse(responseJson);
                var content = obj["result"]?["content"];
                if (content is JArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item["type"]?.Value<string>() == "text")
                            return item["text"]?.Value<string>() ?? string.Empty;
                    }
                }
                // Fallback: return full response
                return responseJson;
            }
            catch
            {
                return responseJson;
            }
        }
    }
}
