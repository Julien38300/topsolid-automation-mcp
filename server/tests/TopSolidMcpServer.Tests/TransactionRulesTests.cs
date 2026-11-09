using System.Linq;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// Field test of v1.8.16-rc1 on TopSolid 7.20 (2026-11-09): inside StartModification, TopSolid
    /// refused Pdm.SetDescription, SetName, CreateFolder, MoveSeveral, Save and DeleteSeveral with
    /// "EndModification must be called before calling this method"; outside, all of them worked.
    /// topsolid_execute_script wrapped a lone Pdm.DeleteSeveral in a transaction and failed.
    /// </summary>
    [TestFixture]
    public class TransactionRulesTests
    {
        [TestCase("TopSolidHost.Pdm.DeleteSeveral(ids);")]
        [TestCase("TopSolidHost.Pdm.MoveSeveral(new List<PdmObjectId> { po }, folder);")]
        [TestCase("var f = TopSolidHost.Pdm.CreateFolder(root, x);")]
        [TestCase("TopSolidHost.Pdm.SetDescription(pdmId, x); TopSolidHost.Pdm.Save(pdmId, true);")]
        [TestCase("TopSolidHost.Pdm.SetName(pdmId, x);")]
        [TestCase("TopSolidHost.Documents.Open(ref d); TopSolidHost.Documents.Close(d, false, false);")]
        public void PdmAndDocumentCalls_DoNotModifyTheModel(string code)
        {
            Assert.That(TransactionRules.ModifiesModel(code), Is.False);
            Assert.That(TransactionRules.FindOutsideOnlyCalls(code), Is.Not.Empty);
        }

        [TestCase("TopSolidHost.Parameters.SetRealValue(p, 1.0);")]
        [TestCase("TopSolidDesignHost.Parts.SetMaterial(docId, mat);")]
        [TestCase("TopSolidHost.Documents.EnsureIsDirty(ref docId);")]
        [TestCase("TopSolidHost.Elements.Delete(e);")]
        public void ModelCalls_ModifyTheModel(string code)
        {
            Assert.That(TransactionRules.ModifiesModel(code), Is.True);
            Assert.That(TransactionRules.FindOutsideOnlyCalls(code), Is.Empty);
        }

        [TestCase("dict.Remove(key); list.AddRange(x); System.IO.Directory.CreateDirectory(path);")]
        [TestCase("var n = TopSolidHost.Pdm.GetName(po); var d = TopSolidHost.Documents.EditedDocument;")]
        public void ReadsAndBclCalls_ChangeNothing(string code)
        {
            Assert.That(TransactionRules.ModifiesModel(code), Is.False);
            Assert.That(TransactionRules.FindOutsideOnlyCalls(code), Is.Empty);
        }

        [Test]
        public void MixedScript_IsDetectedOnBothSides()
        {
            const string code = "TopSolidHost.Parameters.SetRealValue(p, 1.0); TopSolidHost.Pdm.Save(pdmId, true);";

            Assert.That(TransactionRules.ModifiesModel(code), Is.True);
            Assert.That(TransactionRules.FindOutsideOnlyCalls(code), Is.EqualTo(new[] { "Pdm.Save" }));
        }

        [Test]
        public void LongerMemberNames_AreNotTruncated()
        {
            const string code = "TopSolidHost.Pdm.SaveSeveral(a); TopSolidHost.Pdm.CopySeveral2(a, b); TopSolidHost.Documents.ExportWithOptions(1, o, d, p);";

            Assert.That(TransactionRules.FindOutsideOnlyCalls(code),
                Is.EqualTo(new[] { "Pdm.SaveSeveral", "Pdm.CopySeveral2", "Documents.ExportWithOptions" }));
        }

        [Test]
        public void EachCallIsListedOnce_WithSpacesAroundDots()
        {
            const string code = "TopSolidHost . Pdm . DeleteSeveral (a); TopSolidHost.Pdm.DeleteSeveral(b);";

            Assert.That(TransactionRules.FindOutsideOnlyCalls(code), Is.EqualTo(new[] { "Pdm.DeleteSeveral" }));
        }

        [Test]
        public void MixedTransactionError_NamesTheCallsAndBothWaysOut()
        {
            string error = TransactionRules.MixedTransactionError(new[] { "Pdm.DeleteSeveral", "Pdm.Save" }.ToList());

            Assert.That(error, Does.StartWith("Error:"));
            Assert.That(error, Does.Contain("Pdm.DeleteSeveral, Pdm.Save"));
            Assert.That(error, Does.Contain("Split it into two calls"));
            Assert.That(error, Does.Contain("EndModification(true, true)"));
            Assert.That(error, Does.Contain("Pdm.Save is not needed"));
        }
    }
}
