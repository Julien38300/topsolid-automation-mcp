using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// GH #7: tools/list carries the MCP annotations (readOnlyHint, destructiveHint...) so that MCP
    /// clients can ask the user before a tool that writes, and auto-approve the ones that only read.
    /// </summary>
    [TestFixture]
    public class ToolAnnotationsTests
    {
        private static string ToolsDir
        {
            get { return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "Tools")); }
        }

        [Test]
        public void EveryToolOfTheServerHasAnnotations()
        {
            // A contributor who adds a tool must classify it here, or this test fails.
            var names = Directory.GetFiles(ToolsDir, "*.cs")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), "Name = \"(topsolid_[a-z_]+)\"").Cast<Match>())
                .Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(15), "tool names not found in " + ToolsDir);
            foreach (var name in names)
                Assert.That(McpToolAnnotations.For(name), Is.Not.Null, name + " has no annotations in McpToolAnnotations");
        }

        [TestCase("topsolid_api_help")]
        [TestCase("topsolid_list_recipes")]
        [TestCase("topsolid_get_state")]
        [TestCase("topsolid_compile")]
        public void ReadingTools_AreReadOnly(string name)
        {
            var a = McpToolAnnotations.For(name);
            Assert.That((bool)a["readOnlyHint"], Is.True);
            Assert.That(a["destructiveHint"], Is.Null, "destructiveHint only means something when readOnlyHint is false");
        }

        [TestCase("topsolid_run_recipe")]
        [TestCase("topsolid_execute_script")]
        [TestCase("topsolid_modify_script")]
        public void ToolsThatCanDeleteOrOverwrite_AreDestructive(string name)
        {
            var a = McpToolAnnotations.For(name);
            Assert.That((bool)a["readOnlyHint"], Is.False);
            Assert.That((bool)a["destructiveHint"], Is.True);
        }

        [Test]
        public void CompareDocuments_WritesWithoutDeleting()
        {
            var a = McpToolAnnotations.For("topsolid_compare_documents");
            Assert.That((bool)a["readOnlyHint"], Is.False, "apply=true writes the target");
            Assert.That((bool)a["destructiveHint"], Is.False, "it only adds or sets values, never deletes");
        }

        [Test]
        public void ToolsList_SerializesAnnotations()
        {
            var registry = new McpToolRegistry();
            registry.RegisterTool(new McpToolDescriptor { Name = "topsolid_run_recipe", Description = "d", InputSchema = new JObject() }, a => "");
            registry.RegisterTool(new McpToolDescriptor { Name = "not_a_known_tool", Description = "d", InputSchema = new JObject() }, a => "");
            var tools = JArray.FromObject(registry.ListTools());

            var recipe = tools.Single(t => (string)t["name"] == "topsolid_run_recipe");
            Assert.That((bool)recipe["annotations"]["destructiveHint"], Is.True);
            Assert.That((string)recipe["annotations"]["title"], Is.Not.Empty);
            var unknown = tools.Single(t => (string)t["name"] == "not_a_known_tool");
            Assert.That(unknown["annotations"], Is.Null, "no annotations rather than a wrong guess");
        }
    }
}
