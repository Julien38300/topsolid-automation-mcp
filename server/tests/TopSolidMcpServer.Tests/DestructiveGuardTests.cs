using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>GH #7: confirm=true before what cannot be undone.</summary>
    [TestFixture]
    public class DestructiveGuardTests
    {
        [TearDown]
        public void Reset()
        {
            DestructiveGuard.Disabled = false;
        }

        [Test]
        public void ReadAndOrdinaryWriteRecipes_RunWithoutConfirmation()
        {
            Assert.That(DestructiveGuard.CheckRecipe("read_designation", false, new JObject()), Is.Null);
            Assert.That(DestructiveGuard.CheckRecipe("set_designation", false, new JObject()), Is.Null);
            Assert.That(DestructiveGuard.CheckRecipe("create_extruded_bar", false, new JObject()), Is.Null, "deletes only its own temporary elements");
        }

        [Test]
        public void ExportsAndBatchRecipes_NeedConfirmation()
        {
            Assert.That(DestructiveGuard.CheckRecipe("export_step", true, new JObject()), Does.StartWith("Error: confirmation required"));
            Assert.That(DestructiveGuard.CheckRecipe("batch_set_designation", false, new JObject()), Does.Contain("confirm=true"));
            Assert.That(DestructiveGuard.CheckRecipe("BATCH_CLEAR_AUTHOR", false, new JObject()), Is.Not.Null);
        }

        [Test]
        public void ConfirmTrue_LetsItRun()
        {
            Assert.That(DestructiveGuard.CheckRecipe("export_step", true, JObject.Parse("{\"confirm\": true}")), Is.Null);
            Assert.That(DestructiveGuard.CheckRecipe("export_step", true, JObject.Parse("{\"confirm\": \"true\"}")), Is.Null);
            Assert.That(DestructiveGuard.CheckRecipe("export_step", true, JObject.Parse("{\"confirm\": false}")), Is.Not.Null);
        }

        [Test]
        public void Disabled_ForAutomation()
        {
            DestructiveGuard.Disabled = true;
            Assert.That(DestructiveGuard.CheckRecipe("export_step", true, new JObject()), Is.Null);
            Assert.That(DestructiveGuard.CheckScript("TopSolidHost.Pdm.DeleteSeveral(ids);", new JObject()), Is.Null);
        }

        [Test]
        public void Script_DeletingTopSolidData_NeedsConfirmation()
        {
            string refusal = DestructiveGuard.CheckScript(
                "TopSolidHost.Pdm.DeleteSeveral(ids);\nTopSolidHost.Elements.Delete(e);\nTopSolidHost.Pdm.DeleteSeveral(more);", new JObject());
            Assert.That(refusal, Does.Contain("(DeleteSeveral, Delete)"));
            Assert.That(DestructiveGuard.CheckScript("TopSolidHost.Pdm.DeleteSeveral(ids);", JObject.Parse("{\"confirm\": true}")), Is.Null);
        }

        [Test]
        public void Script_WithoutTopSolidDeletion_RunsDirectly()
        {
            Assert.That(DestructiveGuard.CheckScript("var d = new Dictionary<string,int>(); d.Remove(\"x\"); return TopSolidHost.Pdm.GetName(p);", new JObject()), Is.Null);
            Assert.That(DestructiveGuard.CheckScript("TopSolidDesignHost.Parts.SetMaterial(doc, mat);", new JObject()), Is.Null);
        }
    }
}
