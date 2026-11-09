using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    [TestFixture]
    public class ScriptOutputLimitTests
    {
        [Test]
        public void ShortOutput_IsUnchanged()
        {
            Assert.That(ScriptOutputLimit.Apply("{\"a\":1}", 1000), Is.EqualTo("{\"a\":1}"));
        }

        [Test]
        public void LongOutput_AnnouncesTheCutOnTheFirstLine()
        {
            string output = new string('x', 2500);

            string result = ScriptOutputLimit.Apply(output, 1000);

            string firstLine = result.Split('\n')[0];
            Assert.That(firstLine, Does.StartWith("[OUTPUT TRUNCATED: first 1000 of 2500 characters shown."));
            Assert.That(firstLine, Does.Contain(ScriptOutputLimit.EnvVarName));
            Assert.That(result, Does.EndWith("... [1500 characters omitted]"));
            Assert.That(result, Does.Contain(new string('x', 1000)).And.Not.Contain(new string('x', 1001)));
        }

        [TestCase(null, ScriptOutputLimit.DefaultMaxChars)]
        [TestCase("", ScriptOutputLimit.DefaultMaxChars)]
        [TestCase("abc", ScriptOutputLimit.DefaultMaxChars)]
        [TestCase("50", ScriptOutputLimit.DefaultMaxChars)]
        [TestCase(" 120000 ", 120000)]
        public void ResolveMaxChars_FallsBackToTheDefault(string raw, int expected)
        {
            Assert.That(ScriptOutputLimit.ResolveMaxChars(raw), Is.EqualTo(expected));
        }

        [Test]
        public void DefaultCap_IsWellAboveTheOld8000()
        {
            Assert.That(ScriptOutputLimit.DefaultMaxChars, Is.GreaterThan(8000));
        }
    }
}
