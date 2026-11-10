using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// topsolid_compare_documents on snapshots read from a real TopSolid 7.20 session: part A went from
    /// revision A.0 to B.0 (material Acier → Acier S235, parameters Diamètre and JeuPerçage added,
    /// 4 holes Ø10 → Ø8). Part B shares the same base plus a boss; B.0 already has the material and
    /// the parameters of A, A.0 has none of them.
    /// </summary>
    [TestFixture]
    public class CompareDocumentsTests
    {
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "compare", name + ".txt"));
        }

        private static DocumentSnapshot Snap(string name)
        {
            return DocumentSnapshot.Parse(Fixture(name));
        }

        private static List<DocumentChange> RevisionChanges()
        {
            return DocumentComparison.Diff(Snap("partA-A.0"), Snap("partA-B.0"), true);
        }

        [Test]
        public void Parse_ReadsRealSnapshot()
        {
            var s = Snap("partA-B.0");
            Assert.That(s.Document, Is.EqualTo("Pièce A"));
            Assert.That(s.Revision, Is.EqualTo("B.0"));
            Assert.That(s.Revisions, Is.EqualTo(new[] { "A.0", "B.0" }));
            Assert.That(s.Material, Is.EqualTo("Acier S235 EN 10025"));
            Assert.That(s.Operations.Count, Is.EqualTo(45));
            Assert.That(s.FindParameter("Diamètre").Display(), Is.EqualTo("8 mm"));
            var shape = s.Shapes.Single();
            Assert.That(shape.FaceCount, Is.EqualTo(10));
            Assert.That(shape.Cylinders.Single().ToString(), Is.EqualTo("4 × Ø8 mm (L 7 mm)"));
        }

        [Test]
        public void Parse_ErrorLine()
        {
            var s = DocumentSnapshot.Parse("ERR\trevision 'Z' not found for Pièce A. Revisions: A.0, B.0");
            Assert.That(s.Document, Is.Empty);
            Assert.That(s.Errors.Single(), Does.Contain("Revisions: A.0, B.0"));
        }

        [Test]
        public void Escape_RoundTrips()
        {
            string raw = "a\tb\nc\\d";
            Assert.That(DocumentSnapshot.Unescape(DocumentSnapshot.Escape(raw)), Is.EqualTo(raw));
            Assert.That(DocumentScripts.Literal("Pièce \"A\"\\"), Is.EqualTo("\"Pièce \\\"A\\\"\\\\\""));
        }

        [Test]
        public void Diff_Revisions_FindsTheDesignChanges()
        {
            var changes = RevisionChanges();
            var design = changes.Where(c => c.Area != ChangeArea.Consequence && c.Area != ChangeArea.Identity).ToList();

            var material = design.Single(c => c.Area == ChangeArea.Material);
            Assert.That(material.Before, Is.EqualTo("Acier"));
            Assert.That(material.After, Is.EqualTo("Acier S235 EN 10025"));

            var parameters = design.Where(c => c.Area == ChangeArea.Parameter).ToList();
            Assert.That(parameters.Select(c => c.Kind + " " + c.Subject + " " + c.After),
                Is.EquivalentTo(new[] { "added Diamètre 8 mm", "added JeuPerçage 20 mm" }));

            var geometry = design.Single(c => c.Area == ChangeArea.Geometry);
            Assert.That(geometry.Before, Is.EqualTo("4 × Ø10 mm (L 7 mm)"));
            Assert.That(geometry.After, Is.EqualTo("4 × Ø8 mm (L 7 mm)"));

            Assert.That(design.Where(c => c.Area == ChangeArea.Operation), Is.Empty, "no operation was added, moved or switched off");
            Assert.That(design.Count, Is.EqualTo(4));
        }

        [Test]
        public void Diff_Revisions_RecomputedAndIdentityValuesAreSetApart()
        {
            var changes = RevisionChanges();
            Assert.That(changes.Single(c => c.Subject == "Masse").Area, Is.EqualTo(ChangeArea.Consequence));
            Assert.That(changes.Single(c => c.Subject == "Limite d'élasticité").Area, Is.EqualTo(ChangeArea.Consequence));
            Assert.That(changes.Single(c => c.Subject == "Révision majeure").Area, Is.EqualTo(ChangeArea.Identity));
            var stockName = changes.Single(c => c.Subject == "NomBrut");
            Assert.That(stockName.Area, Is.EqualTo(ChangeArea.Consequence), "NomBrut is computed by its table parameter operation");
            Assert.That(stockName.Hint, Does.Contain("computed by"));
        }

        [Test]
        public void Plan_AgainstTargetBefore_SaysWhatIsAutomatic()
        {
            var changes = RevisionChanges();
            DocumentComparison.Plan(changes, Snap("partB-A.0"), Snap("partA-B.0"), true);

            var material = changes.Single(c => c.Area == ChangeArea.Material);
            Assert.That(material.Action, Is.EqualTo(PlanAction.Auto));
            Assert.That(material.TargetState, Is.EqualTo("Acier"));

            foreach (var p in changes.Where(c => c.Area == ChangeArea.Parameter))
            {
                Assert.That(p.Action, Is.EqualTo(PlanAction.Auto), p.Subject);
                Assert.That(p.TargetState, Is.EqualTo("absent"), p.Subject);
            }

            var geometry = changes.Single(c => c.Area == ChangeArea.Geometry);
            Assert.That(geometry.Action, Is.EqualTo(PlanAction.Manual));
            Assert.That(geometry.TargetState, Is.EqualTo("4 × Ø10 mm (L 7 mm)"));
            Assert.That(geometry.Hint, Does.Contain("Groupe de perçage 1"));

            Assert.That(changes.Where(c => c.Area == ChangeArea.Consequence || c.Area == ChangeArea.Identity).All(c => c.Action == PlanAction.Info));
        }

        [Test]
        public void Plan_AgainstTargetAfter_SeesWhatIsAlreadyDone()
        {
            var changes = RevisionChanges();
            DocumentComparison.Plan(changes, Snap("partB-B.0"), Snap("partA-B.0"), true);
            Assert.That(changes.Single(c => c.Area == ChangeArea.Material).Action, Is.EqualTo(PlanAction.Already));
            Assert.That(changes.Where(c => c.Area == ChangeArea.Parameter).All(c => c.Action == PlanAction.Already));
            Assert.That(changes.Single(c => c.Area == ChangeArea.Geometry).Action, Is.EqualTo(PlanAction.Manual), "B still has its Ø10 holes");
        }

        [Test]
        public void Plan_GeometryAlreadyThere_IsAlreadyDone()
        {
            var changes = RevisionChanges();
            DocumentComparison.Plan(changes, Snap("partA-B.0"), Snap("partA-B.0"), true);
            Assert.That(changes.Single(c => c.Area == ChangeArea.Geometry).Action, Is.EqualTo(PlanAction.Already));
        }

        [Test]
        public void Diff_Documents_ListsWhatTheTargetHasOfItsOwn()
        {
            // What to change in B (A.0) to match A (B.0): B has a boss A does not have.
            var b = Snap("partB-A.0");
            var changes = DocumentComparison.Diff(b, Snap("partA-B.0"), false);
            DocumentComparison.Plan(changes, b, Snap("partA-B.0"), false);
            var boss = changes.Single(c => c.Area == ChangeArea.Operation && c.Subject == "Bossage 1");
            Assert.That(boss.Kind, Is.EqualTo("removed"));
            Assert.That(boss.Action, Is.EqualTo(PlanAction.Manual), "the tool never deletes an operation");
            Assert.That(changes.Single(c => c.Area == ChangeArea.Material).Action, Is.EqualTo(PlanAction.Auto));

            // The block is 100 × 85 × 70 in B, 80 × 80 × 7 in A: a design change, not only a recomputed value.
            var size = changes.Single(c => c.Subject == "Overall size");
            Assert.That(size.Area, Is.EqualTo(ChangeArea.Geometry));
            Assert.That(size.Before, Is.EqualTo("100 × 85 × 70 mm"));
            Assert.That(size.After, Is.EqualTo("80 × 80 × 7 mm"));
            Assert.That(size.Action, Is.EqualTo(PlanAction.Manual));
        }

        [Test]
        public void Display_UsesReadableUnits()
        {
            Assert.That(SnapshotParameter.DisplayValue("Real", "0.02", "Length", "mm"), Is.EqualTo("20 mm"));
            Assert.That(SnapshotParameter.DisplayValue("Real", "235000000", "Pressure", "MPa"), Is.EqualTo("235 MPa"));
            Assert.That(SnapshotParameter.DisplayValue("Real", "4.3392566491191772E-05", "Volume", "mm3"), Is.EqualTo("43392.57 mm3"));
            Assert.That(SnapshotParameter.DisplayValue("Real", "0.00018449", "InertiaMoment", "kg.mm2"), Is.EqualTo("0.000184 kg·m2"));
            Assert.That(SnapshotParameter.DisplayValue("Text", "Acier", "", ""), Is.EqualTo("Acier"));
        }

        [Test]
        public void Diff_OperationStateAndOrder()
        {
            var from = Snap("partA-A.0");
            var to = Snap("partA-A.0");
            var drill = to.Operations.Single(o => o.Name == "Groupe de perçage 1");
            drill.Active = false;
            var block = to.Operations.Single(o => o.Name == "Bloc (Forme 1)");
            to.Operations.Remove(block);
            to.Operations.Insert(to.Operations.IndexOf(drill) + 1, block);

            var changes = DocumentComparison.Diff(from, to, true);
            var state = changes.Single(c => c.Kind == "state");
            Assert.That(state.Subject, Is.EqualTo("Groupe de perçage 1"));
            Assert.That(state.After, Is.EqualTo("inactive"));
            var order = changes.Single(c => c.Kind == "order");
            Assert.That(order.After, Does.Contain("after Groupe de perçage 1"));

            DocumentComparison.Plan(changes, Snap("partB-A.0"), to, true);
            Assert.That(state.Action, Is.EqualTo(PlanAction.Auto));
            Assert.That(order.Action, Is.EqualTo(PlanAction.Manual));
            string script = DocumentComparison.BuildApplyScript("Pièce A", "Pièce B", changes);
            Assert.That(script, Does.Contain("TopSolidHost.Operations.Deactivate(o)"));
        }

        [Test]
        public void ApplyScript_HoldsOnlyTheAutomaticChanges_InOneTransaction()
        {
            var changes = RevisionChanges();
            DocumentComparison.Plan(changes, Snap("partB-A.0"), Snap("partA-B.0"), true);
            string script = DocumentComparison.BuildApplyScript("Pièce A", "Pièce B", changes);

            Assert.That(script, Does.Contain("TopSolidDesignHost.Parts.SetMaterial(target, material);"));
            Assert.That(script, Does.Contain("CreateRealParameter(target, (UnitType)Enum.Parse(typeof(UnitType), \"Length\"), double.Parse(\"0.008\""));
            Assert.That(script, Does.Contain("SetName(e, \"JeuPerçage\")"));
            Assert.That(script, Does.Not.Contain("Masse"), "recomputed values are never written");
            Assert.That(script, Does.Not.Contain("NomBrut"));

            int start = script.IndexOf("StartModification", StringComparison.Ordinal);
            int ensure = script.IndexOf("EnsureIsDirty(ref target)", StringComparison.Ordinal);
            int end = script.IndexOf("EndModification(true, true)", StringComparison.Ordinal);
            int save = script.IndexOf("Pdm.Save", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0));
            Assert.That(ensure, Is.GreaterThan(start));
            Assert.That(end, Is.GreaterThan(ensure));
            Assert.That(save, Is.GreaterThan(end), "Pdm.Save must run outside the transaction");
            Assert.That(script, Does.Contain("EndModification(false, false)"), "a failure rolls everything back");
        }

        [Test]
        public void Render_ReportIsSortedAndNamesTheActions()
        {
            var changes = RevisionChanges();
            var target = Snap("partB-A.0");
            DocumentComparison.Plan(changes, target, Snap("partA-B.0"), true);
            string report = DocumentComparison.Render("COMPARE", changes, target, "footer");

            Assert.That(report, Does.Contain("Summary: 4 design change(s) — 3 automatic, 1 manual, 0 already in Pièce B"));
            Assert.That(report.IndexOf("MATERIAL", StringComparison.Ordinal), Is.LessThan(report.IndexOf("PARAMETERS", StringComparison.Ordinal)));
            Assert.That(report.IndexOf("PARAMETERS", StringComparison.Ordinal), Is.LessThan(report.IndexOf("GEOMETRY", StringComparison.Ordinal)));
            Assert.That(report, Does.Contain("+ Diamètre = 8 mm   | Pièce B: absent  [AUTO: create]"));
            Assert.That(report, Does.Contain("4 × Ø10 mm (L 7 mm) → 4 × Ø8 mm (L 7 mm)"));
            Assert.That(report, Does.Contain("[MANUAL]"));
            Assert.That(report, Does.Contain("RECOMPUTED BY TOPSOLID"));
            Assert.That(report.TrimEnd(), Does.EndWith("footer"));
        }

        // ---- Runner flow, TopSolid replaced by the fixtures ----

        private sealed class FakeTopSolid
        {
            public readonly List<string> Scripts = new List<string>();
            public bool TargetApplied;

            public string Run(string code)
            {
                Scripts.Add(code);
                if (code.Contains("Compare documents: apply"))
                {
                    TargetApplied = true;
                    return "OK material: Acier S235 EN 10025\nOK created Diamètre = 8 mm\nOK created JeuPerçage = 20 mm\n";
                }
                string doc = Regex.Match(code, "DocumentId doc = __find\\(\"([^\"]*)\"\\)").Groups[1].Value;
                string rev = Regex.Match(code, "string wanted = \"([^\"]*)\";").Groups[1].Value;
                if (doc == "" || doc == "Pièce A")
                    return rev == "Z" ? "ERR\trevision 'Z' not found for Pièce A. Revisions: A.0, B.0"
                        : Fixture(rev == "previous" || rev == "A.0" || rev == "A" ? "partA-A.0" : "partA-B.0");
                if (doc == "Pièce B") return Fixture(TargetApplied ? "partB-B.0" : "partB-A.0");
                return "ERR\tdocument not found in the current project: " + doc;
            }
        }

        [Test]
        public void Runner_Report_DefaultsToPreviousRevisionOfTheEditedDocument()
        {
            var fake = new FakeTopSolid();
            string report = new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"target\": \"Pièce B\"}"));
            Assert.That(report, Does.StartWith("COMPARE Pièce A: A.0 → B.0   target: Pièce B A.0"));
            Assert.That(report, Does.Contain("Call again with apply=true to apply the 3 AUTO change(s) to Pièce B"));
            Assert.That(fake.TargetApplied, Is.False, "a report never writes");
        }

        [Test]
        public void Runner_Apply_ThenReadsTheTargetBack()
        {
            var fake = new FakeTopSolid();
            string report = new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"document\": \"Pièce A\", \"target\": \"Pièce B\", \"apply\": true}"));
            Assert.That(fake.TargetApplied, Is.True);
            Assert.That(report, Does.StartWith("APPLIED to Pièce B B.0:\nOK material"));
            Assert.That(report, Does.Contain("0 automatic, 1 manual, 3 already in Pièce B"));
            Assert.That(report, Does.Contain("1 change(s) left to do by hand in Pièce B"));
        }

        [Test]
        public void Runner_RefusesApplyInReadOnlyModeOrWithoutTarget()
        {
            var fake = new FakeTopSolid();
            Assert.That(new DocumentCompareRunner(fake.Run, true).Run(JObject.Parse("{\"target\": \"Pièce B\", \"apply\": true}")), Does.Contain("read-only"));
            Assert.That(new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"apply\": true}")), Does.Contain("needs a 'target'"));
            Assert.That(new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"target\": \"Pièce B\", \"apply\": true, \"to_revision\": \"A.0\"}")), Does.Contain("CURRENT revision"));
            Assert.That(fake.Scripts, Is.Empty, "invalid arguments are refused before touching TopSolid");
        }

        [Test]
        public void Runner_PassesTopSolidErrorsThrough()
        {
            var fake = new FakeTopSolid();
            Assert.That(new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"from_revision\": \"Z\"}")), Is.EqualTo("Error: revision 'Z' not found for Pièce A. Revisions: A.0, B.0"));
            Assert.That(new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"target\": \"Pièce C\"}")), Does.Contain("document not found in the current project: Pièce C"));
            Assert.That(new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"target\": \"Pièce A\"}")),
                Is.EqualTo("Error: 'target' is the compared document itself (Pièce A, the edited document): name the document that evolved with 'document'."));
        }

        [Test]
        public void Runner_DocumentsMode_ComparesWithTheTarget()
        {
            var fake = new FakeTopSolid();
            string report = new DocumentCompareRunner(fake.Run, false).Run(JObject.Parse("{\"document\": \"Pièce A\", \"target\": \"Pièce B\", \"mode\": \"documents\"}"));
            Assert.That(report, Does.StartWith("COMPARE Pièce B A.0 with Pièce A B.0 (what to change in Pièce B to match Pièce A)"));
            Assert.That(report, Does.Contain("- Bossage 1"));
        }
    }
}
