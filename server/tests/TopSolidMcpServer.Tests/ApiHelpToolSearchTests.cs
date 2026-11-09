using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TopSolidApiGraph.Core;
using TopSolidApiGraph.Core.Models;
using TopSolidMcpServer.Tools;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// Field reports (MorphiGen batch import, 2026-11-09): natural-language queries missed
    /// IPdm.MoveSeveral and the PDM deletion methods, and an exact method name returned the
    /// wrong interface because the keyword pool was cut before scoring.
    /// </summary>
    [TestFixture]
    public class ApiHelpToolSearchTests
    {
        private ApiHelpTool _tool;

        private static ExtractedMethod Method(string iface, string name, params string[] parameters)
        {
            return new ExtractedMethod
            {
                DeclaringType = "TopSolid.Kernel.Automating." + iface,
                Name = name,
                ReturnType = "System.Void",
                Parameters = parameters.ToList()
            };
        }

        [SetUp]
        public void SetUp()
        {
            var methods = new List<ExtractedMethod>
            {
                Method("IPdm", "MoveSeveral", "System.Collections.Generic.List`1[PdmObjectId]", "PdmObjectId"),
                Method("IPdm", "MoveProject", "PdmObjectId"),
                Method("IPdm", "MoveProjectFolder", "PdmProjectFolderId"),
                Method("IPdm", "GetConstituents", "PdmObjectId"),
                Method("IElements", "GetConstituents", "ElementId"),
                Method("IPdm", "DeleteSeveral", "System.Collections.Generic.List`1[PdmObjectId]"),
                Method("IPdm", "DeleteProjectFolder", "PdmProjectFolderId"),
                Method("IElements", "DeleteSeveral", "System.Collections.Generic.List`1[ElementId]"),
                Method("IElements", "Delete", "ElementId"),
                Method("IDocuments", "GetDocument", "PdmObjectId"),
                Method("IDocuments", "GetOpenDocuments"),
                Method("IPdm", "CreateFolder", "PdmObjectId", "System.String"),
                Method("IPdm", "GetProjectFolderConstituents", "PdmProjectFolderId"),
                Method("IPdm", "HasProjectFolderConstituents", "PdmProjectFolderId"),
                Method("IParts", "SetMaterial", "DocumentId", "DocumentId"),
                Method("IParts", "GetMaterial", "DocumentId"),
                Method("IMaterials", "SetDensity", "DocumentId", "System.Double"),
                Method("IMaterials", "SetMaterialModel", "DocumentId", "MaterialModelType")
            };

            // Enough "Get..." and "...Document..." methods to overflow the 100-edge keyword pool,
            // as in the real graph (4 000+ edges).
            string[] letters = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa" };
            foreach (string a in letters)
                foreach (string b in letters)
                {
                    methods.Add(Method("IShapes", "Get" + a + b, "ElementId"));
                    methods.Add(Method("IDocuments", "Get" + a + b + "Document", "DocumentId"));
                }

            var graph = new TypeGraph();
            graph.Build(methods);
            foreach (var edge in graph.GetEdges())
            {
                // The real graph.json carries the interface (enrichment step); Build() does not.
                string qualified = edge.MethodSignature.Substring(0, edge.MethodSignature.IndexOf('('));
                string[] segments = qualified.Split('.');
                edge.Interface = segments[segments.Length - 2];

                if (edge.Interface == "IPdm" && edge.MethodName == "MoveSeveral") edge.SemanticHint = "deplacer";
                if (edge.MethodName == "DeleteSeveral") edge.SemanticHint = "supprimer";
            }
            graph.BuildKeywordIndex();
            _tool = new ApiHelpTool(() => graph);
        }

        private string Ask(string query)
        {
            return _tool.Execute(new JObject { ["query"] = query });
        }

        [TestCase("déplacer un document dans un dossier")]
        [TestCase("deplacer document dossier")]
        [TestCase("move document to folder")]
        [TestCase("déplacer dossier")]
        public void NaturalLanguageMoveQuery_FindsMoveSeveral(string query)
        {
            string result = Ask(query);

            // "déplacer dossier" matches MoveProjectFolder on every word; MoveSeveral must still be listed.
            Assert.That(result, Does.Contain("IPdm.MoveSeveral(System"), "MoveSeveral must be in the method list, not only in the tip:\n" + result);
            Assert.That(result, Does.Contain("Tip: ").And.Contain("IPdm.MoveSeveral(objectIds, targetFolderId)"), "the usage tip should name the method to use");
        }

        [Test]
        public void ExactMethodName_ReturnsEveryInterfaceDeclaringIt_PdmFirst()
        {
            string result = Ask("GetConstituents");

            int pdm = result.IndexOf("IPdm (");
            int elements = result.IndexOf("IElements (");
            Assert.That(pdm, Is.GreaterThanOrEqualTo(0), result);
            Assert.That(elements, Is.GreaterThanOrEqualTo(0), result);
            Assert.That(pdm, Is.LessThan(elements), "IPdm is a tier-1 interface and must come first");
            Assert.That(result, Does.Not.Contain("GetAlphaAlpha"), "exact-name mode must not list unrelated Get methods");
        }

        [TestCase("supprimer document pdm")]
        [TestCase("suppression PDM")]
        [TestCase("delete pdm object")]
        public void PdmDeletionQuery_FindsPdmDeleteSeveral(string query)
        {
            string result = Ask(query);

            Assert.That(result, Does.Contain("IPdm ("), result);
            Assert.That(result, Does.Contain("DeleteSeveral"));
            Assert.That(result, Does.Contain("IPdm.DeleteSeveral(objectIds)"), "the usage tip should name the PDM deletion method");
        }

        private static string FirstMethodLine(string result)
        {
            return result.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("TopSolid.")) ?? "";
        }

        [TestCase("GetConstituents folder", "IPdm.GetConstituents(")]
        [TestCase("SetMaterial part", "IParts.SetMaterial(")]
        public void QueryWordThatIsAMethodName_PutsThatMethodFirst(string query, string expectedFirst)
        {
            string result = Ask(query);

            Assert.That(FirstMethodLine(result), Does.Contain(expectedFirst), result);
        }

        [Test]
        public void PartMaterialQuery_TipNamesTheDesignHost()
        {
            Assert.That(Ask("SetMaterial part"), Does.Contain("TopSolidDesignHost.Parts.SetMaterial"));
        }

        [Test]
        public void IsOpen_TipPointsAtGetOpenDocuments()
        {
            Assert.That(Ask("IsOpen"), Does.Contain("GetOpenDocuments().Contains("));
        }

        [TestCase("PdmObjectId ToString")]
        [TestCase("PdmObjectId")]
        public void PdmObjectIdQueries_WarnAboutToString(string query)
        {
            Assert.That(Ask(query), Does.Contain("ToString() returns the type name"));
        }

        [Test]
        public void DuplicateEdges_AreListedOnce()
        {
            // MoveSeveral has one edge per parameter plus the declaring-type edge.
            string result = Ask("MoveSeveral");

            int signatureLines = result.Split('\n').Count(l => l.Contains("IPdm.MoveSeveral(System"));
            Assert.That(signatureLines, Is.EqualTo(1), result);
        }
    }
}
