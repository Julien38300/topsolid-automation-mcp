using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// "Start with Windows" drives the TopSolidMcpServer logon task the installer creates.
    /// The state comes from `schtasks /Query /XML`, whose XML does not depend on the Windows language.
    /// </summary>
    [TestFixture]
    public class AutoStartLogicTests
    {
        private const string Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        private static string TaskXml(string settingsEnabled)
        {
            string enabled = settingsEnabled == null ? "" : "<Enabled>" + settingsEnabled + "</Enabled>";
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n<Task version=\"1.2\" xmlns=\"" + Ns + "\">" +
                   "<Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>" +
                   "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" + enabled + "</Settings>" +
                   "<Actions Context=\"Author\"><Exec><Command>conhost.exe</Command></Exec></Actions></Task>";
        }

        [Test]
        public void Query_Failed_MeansNoTask()
        {
            Assert.That(AutoStartLogic.ParseQuery(1, "ERROR: The system cannot find the file specified."), Is.EqualTo(AutoStartState.Absent));
        }

        [Test]
        public void Query_EnabledTask()
        {
            Assert.That(AutoStartLogic.ParseQuery(0, TaskXml("true")), Is.EqualTo(AutoStartState.Enabled));
        }

        [Test]
        public void Query_DisabledTask()
        {
            Assert.That(AutoStartLogic.ParseQuery(0, TaskXml("false")), Is.EqualTo(AutoStartState.Disabled));
        }

        [Test]
        public void Query_NoEnabledElement_MeansEnabled()
        {
            // Task Scheduler omits <Enabled> when it has its default value, true.
            Assert.That(AutoStartLogic.ParseQuery(0, TaskXml(null)), Is.EqualTo(AutoStartState.Enabled));
        }

        [Test]
        public void Query_UnreadableXml_MeansUnknown()
        {
            Assert.That(AutoStartLogic.ParseQuery(0, "not xml"), Is.EqualTo(AutoStartState.Unknown));
        }

        [Test]
        public void CreateArguments_MatchTheInstallerTask()
        {
            string args = AutoStartLogic.CreateArguments(@"C:\TopSolidMCP\TopSolidMcpServer.exe");
            Assert.That(args, Is.EqualTo(
                "/Create /F /TN \"TopSolidMcpServer\" /SC ONLOGON /RL HIGHEST /IT " +
                "/TR \"conhost.exe --headless \\\"C:\\TopSolidMCP\\TopSolidMcpServer.exe\\\" --http-standalone\""));
        }

        [Test]
        public void ChangeArguments()
        {
            Assert.That(AutoStartLogic.ChangeArguments(true), Is.EqualTo("/Change /TN \"TopSolidMcpServer\" /ENABLE"));
            Assert.That(AutoStartLogic.ChangeArguments(false), Is.EqualTo("/Change /TN \"TopSolidMcpServer\" /DISABLE"));
        }

        [Test]
        public void Plan_TurnOn()
        {
            Assert.That(AutoStartLogic.PlanFor(true, AutoStartState.Absent), Is.EqualTo(AutoStartAction.Create));
            Assert.That(AutoStartLogic.PlanFor(true, AutoStartState.Disabled), Is.EqualTo(AutoStartAction.Enable));
            Assert.That(AutoStartLogic.PlanFor(true, AutoStartState.Enabled), Is.EqualTo(AutoStartAction.Nothing));
        }

        [Test]
        public void Plan_TurnOff_NeverDeletesTheTask()
        {
            Assert.That(AutoStartLogic.PlanFor(false, AutoStartState.Enabled), Is.EqualTo(AutoStartAction.Disable));
            Assert.That(AutoStartLogic.PlanFor(false, AutoStartState.Disabled), Is.EqualTo(AutoStartAction.Nothing));
            Assert.That(AutoStartLogic.PlanFor(false, AutoStartState.Absent), Is.EqualTo(AutoStartAction.Nothing));
        }
    }
}
