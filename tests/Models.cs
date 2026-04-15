using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpTestRunner
{
    /// <summary>Root object of TestSuite.json.</summary>
    internal sealed class TestSuite
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("tests")]
        public List<TestCase> Tests { get; set; }
    }

    /// <summary>A single test case definition.</summary>
    internal sealed class TestCase
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tool")]
        public string Tool { get; set; }

        [JsonProperty("request")]
        public JObject Request { get; set; }

        [JsonProperty("assertions")]
        public Assertions Assertions { get; set; }

        [JsonProperty("baseline_ms")]
        public long BaselineMs { get; set; }
    }

    /// <summary>Functional assertions applied to the MCP response text.</summary>
    internal sealed class Assertions
    {
        [JsonProperty("contains")]
        public List<string> Contains { get; set; }

        [JsonProperty("not_contains")]
        public List<string> NotContains { get; set; }

        [JsonProperty("pattern")]
        public string Pattern { get; set; }
    }

    /// <summary>Root object of Baselines.json.</summary>
    internal sealed class BaselinesFile
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("regression_factor")]
        public double RegressionFactor { get; set; } = 2.0;

        [JsonProperty("baselines")]
        public Dictionary<string, long> Baselines { get; set; } = new Dictionary<string, long>();
    }

    /// <summary>Result of a single test execution.</summary>
    internal sealed class TestResult
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("pass")]
        public bool Pass { get; set; }

        [JsonProperty("elapsed_ms")]
        public long ElapsedMs { get; set; }

        [JsonProperty("baseline_ms")]
        public long BaselineMs { get; set; }

        [JsonProperty("fail_reason")]
        public string FailReason { get; set; }

        public static TestResult Passed(TestCase test, long elapsedMs, long baselineMs) =>
            new TestResult { Id = test.Id, Name = test.Name, Pass = true,  ElapsedMs = elapsedMs, BaselineMs = baselineMs };

        public static TestResult Failed(TestCase test, long elapsedMs, long baselineMs, string reason) =>
            new TestResult { Id = test.Id, Name = test.Name, Pass = false, ElapsedMs = elapsedMs, BaselineMs = baselineMs, FailReason = reason };

        public static TestResult Aborted(TestCase test, string reason) =>
            new TestResult { Id = test.Id, Name = test.Name, Pass = false, ElapsedMs = 0, FailReason = reason };
    }
}
