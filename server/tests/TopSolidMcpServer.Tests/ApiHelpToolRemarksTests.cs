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
    /// graph.json carries the official remarks of 3 600 edges (transaction rules, obsolescence),
    /// but topsolid_api_help printed only the signature and the one-line description: nothing said
    /// that Pdm.DeleteSeveral must not run inside StartModification (v1.8.16 field bug).
    /// </summary>
    [TestFixture]
    public class ApiHelpToolRemarksTests
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
                Method("IPdm", "DeleteSeveral", "System.Collections.Generic.List`1[PdmObjectId]"),
                Method("IPdm", "CopySeveral", "System.Collections.Generic.List`1[PdmObjectId]", "PdmObjectId"),
                Method("IParameters", "SetRealValue", "ElementId", "System.Double"),
                Method("IParameters", "SetTextValue", "ElementId", "System.String"),
                Method("IParameters", "GetRealValue", "ElementId"),
                Method("IParameters", "GetTextValue", "ElementId"),
                Method("IShapes", "CheckShape", "ElementId"),
                Method("IShapes", "FindFaceNearestPointToPoint", "ElementId", "Point3D"),
                Method("IUser", "AskShape", "System.String")
            };

            var graph = new TypeGraph();
            graph.Build(methods);
            foreach (var edge in graph.GetEdges())
            {
                string qualified = edge.MethodSignature.Substring(0, edge.MethodSignature.IndexOf('('));
                string[] segments = qualified.Split('.');
                edge.Interface = segments[segments.Length - 2];

                switch (edge.MethodName)
                {
                    case "DeleteSeveral":
                        edge.Remarks = "This method must not be called between StartModification(String, Boolean) and EndModification(Boolean, Boolean) methods calls. This method is available since v7.7.201.140.";
                        edge.Since = "7.7.201.140";
                        break;
                    case "CopySeveral":
                        edge.Remarks = "This method must not be called between StartModification(String, Boolean) and EndModification(Boolean, Boolean) methods calls.";
                        edge.Deprecated = true;
                        edge.ObsoleteMessage = "Use CopySeveralWithOptions.";
                        break;
                    case "SetRealValue":
                    case "SetTextValue":
                        edge.Remarks = "StartModification(String, Boolean) and EnsureIsDirty(DocumentId ) must be called before calling this method.";
                        break;
                    case "CheckShape":
                        edge.Remarks = "StartModification(String, Boolean) must be called before calling this method. This method is available since v7.20";
                        edge.Since = "7.20";
                        break;
                    case "FindFaceNearestPointToPoint":
                        edge.Since = "7.21.100";
                        break;
                    case "AskShape":
                        edge.Remarks = "StartModification(String, Boolean) and EnsureIsDirty(DocumentId ) may be called before calling this method to allow the user to create or modify entities during the interaction.";
                        break;
                }
            }
            graph.BuildKeywordIndex();
            _tool = new ApiHelpTool(() => graph);
        }

        private string Ask(string query)
        {
            return _tool.Execute(new JObject { ["query"] = query });
        }

        private static string LineOf(string result, string method)
        {
            return result.Split('\n').FirstOrDefault(l => l.Contains("." + method + "(")) ?? "";
        }

        [Test]
        public void OutsideTransactionMethod_IsTagged()
        {
            Assert.That(LineOf(Ask("IPdm"), "DeleteSeveral"), Does.Contain("[outside StartModification]"));
        }

        [Test]
        public void InsideTransactionMethods_AreTaggedWithEnsureIsDirtyWhenRequired()
        {
            string result = Ask("IParameters");

            Assert.That(LineOf(result, "SetRealValue"), Does.Contain("[inside StartModification + EnsureIsDirty]"));
            Assert.That(LineOf(result, "GetRealValue"), Does.Not.Contain("[inside").And.Not.Contain("[outside"));
            Assert.That(LineOf(Ask("IShapes.Check"), "CheckShape"), Does.Contain("[inside StartModification]").And.Not.Contain("EnsureIsDirty]"));
        }

        [Test]
        public void OptionalTransaction_IsNotTaggedAsRequired()
        {
            Assert.That(LineOf(Ask("IUser"), "AskShape"), Does.Not.Contain("[inside").And.Not.Contain("[outside"));
        }

        [Test]
        public void ObsoleteMethod_IsTaggedWithItsMessage()
        {
            Assert.That(LineOf(Ask("IPdm"), "CopySeveral"), Does.Contain("[OBSOLETE: Use CopySeveralWithOptions.]"));
        }

        [Test]
        public void RecentMethods_ShowTheirVersion_OldOnesDoNot()
        {
            string shapes = Ask("IShapes");

            Assert.That(LineOf(shapes, "FindFaceNearestPointToPoint"), Does.Contain("[since 7.21]"));
            Assert.That(LineOf(shapes, "CheckShape"), Does.Contain("[since 7.20]"));
            Assert.That(LineOf(Ask("IPdm"), "DeleteSeveral"), Does.Not.Contain("[since"));
        }

        [Test]
        public void ShortResult_PrintsTheFullRemark()
        {
            string result = Ask("DeleteSeveral");

            Assert.That(result, Does.Contain("Remarks: This method must not be called between StartModification"));
        }

        [Test]
        public void LongResult_DoesNotPrintFullRemarks()
        {
            // 4 methods listed: tags only, the full text would drown the list.
            Assert.That(Ask("IParameters"), Does.Not.Contain("Remarks:"));
            Assert.That(Ask("IParameters.Set"), Does.Contain("Remarks: StartModification"));
        }
    }
}
