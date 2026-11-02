using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TopSolidApiGraph.Core;
using TopSolidApiGraph.Core.Models;
using TopSolidMcpServer.Tools;

namespace TopSolidMcpServer.Tests
{
    [TestFixture]
    public class ApiHelpToolLocalizationTests
    {
        private ApiHelpTool _tool;

        [SetUp]
        public void SetUp()
        {
            var graph = new TypeGraph();
            graph.Build(new List<ExtractedMethod>
            {
                new ExtractedMethod
                {
                    DeclaringType = "TopSolid.Kernel.Automating.IPdm",
                    Name = "ExportLocalizations",
                    ReturnType = "System.Void",
                    Parameters = new List<string> { "System.String" }
                },
                new ExtractedMethod
                {
                    DeclaringType = "TopSolid.Kernel.Automating.IPdm",
                    Name = "ImportLocalizations",
                    ReturnType = "System.Void",
                    Parameters = new List<string> { "System.String" }
                },
                new ExtractedMethod
                {
                    DeclaringType = "TopSolid.Kernel.Automating.IPdm",
                    Name = "ImportDictionnaries",
                    ReturnType = "System.Void",
                    Parameters = new List<string> { "System.String" }
                }
            });
            graph.BuildKeywordIndex();
            _tool = new ApiHelpTool(() => graph);
        }

        [TestCase("traduction")]
        [TestCase("traduire")]
        [TestCase("translation")]
        [TestCase("translate")]
        [TestCase("localisation")]
        [TestCase("localization")]
        [TestCase("xliff")]
        [TestCase("topdic")]
        [TestCase("dictionnaire")]
        [TestCase("dictionary")]
        [TestCase("langue")]
        public void Execute_LocalizationQueries_ReturnExportAndImportLocalizationsWithWorkflowTip(string query)
        {
            string result = _tool.Execute(new JObject { ["query"] = query });

            Assert.That(result, Does.Contain("ExportLocalizations"));
            Assert.That(result, Does.Contain("ImportLocalizations"));
            Assert.That(result, Does.Contain("export_project_xliff"));
            Assert.That(result, Does.Contain("import_project_xliff"));
            Assert.That(result, Does.Contain("Never use IPdm.ExportDictionaries"));
        }
    }
}
