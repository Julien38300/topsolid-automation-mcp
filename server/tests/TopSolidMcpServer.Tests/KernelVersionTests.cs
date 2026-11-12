using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// Kernel-version gate (GH #25): recipes that call Automation methods added in a
    /// given TopSolid version must be refused cleanly on older instances, BEFORE their
    /// body is compiled (a call to members missing from the local interop DLLs
    /// and reflection is a blocked API in scripts).
    /// </summary>
    [TestFixture]
    public class KernelVersionTests
    {
        [Test]
        public void Format_Decodes_The_Automation_Encoding()
        {
            // ((Major * 100 + Minor) * 1000 + Build) * 1000 + Revision
            Assert.AreEqual("7.21.304.0", KernelVersion.Format(721304000));
            Assert.AreEqual("7.20.400.161", KernelVersion.Format(720400161));
            Assert.AreEqual("7.20.400.0", KernelVersion.Format(720400000));
            Assert.AreEqual("7.21.0.0", KernelVersion.Format(721000000));
        }

        [Test]
        public void Format_Returns_Raw_Value_When_Not_Connected()
        {
            Assert.AreEqual("0", KernelVersion.Format(0));
        }

        [Test]
        public void No_Refusal_When_Recipe_Is_Not_Gated()
        {
            Assert.IsNull(KernelVersion.BuildRecipeRefusal("any_recipe", 0, 720400161));
        }

        [Test]
        public void No_Refusal_When_Running_Version_Is_New_Enough()
        {
            Assert.IsNull(KernelVersion.BuildRecipeRefusal("r", 721000000, 721304000));
            Assert.IsNull(KernelVersion.BuildRecipeRefusal("r", 721000000, 722000000));
            // Live case observed on 2026-11-12 (GH #25): the six IMaterials getters
            // execute and return values on a running 7.20.400.161 instance, so the
            // recipe's 7.20.400 floor must let it through.
            Assert.IsNull(KernelVersion.BuildRecipeRefusal("read_material_mechanical_properties", 720400000, 720400161));
        }

        [Test]
        public void Refusal_Names_Recipe_Required_And_Running_Versions()
        {
            // Real floor for read_material_mechanical_properties (GH #25): the six
            // IMaterials getters appear in the 7.20.400 interop and were live-verified
            // on a 7.20.400.161 instance; a genuine 7.20.311 install lacks them and
            // must get the explicit refusal instead of a raw C# compile error.
            string msg = KernelVersion.BuildRecipeRefusal(
                "read_material_mechanical_properties", 720400000, 720311000);
            Assert.IsNotNull(msg);
            StringAssert.Contains("read_material_mechanical_properties", msg);
            StringAssert.Contains("7.20.400.0", msg);
            StringAssert.Contains("7.20.311.0", msg);
        }

        [Test]
        public void Refusal_Reports_An_Unknown_Running_Version_As_Unknown()
        {
            string msg = KernelVersion.BuildRecipeRefusal("r", 721000000, 0);
            StringAssert.Contains("unknown", msg);
        }
    }
}
