using System;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// PrivacyScrub (issue #15): the user profile directory must be redacted as
    /// %USERPROFILE% in everything that leaves the machine — case-insensitive,
    /// "\" and "/" separators — so the Windows account name never reaches a
    /// public GitHub issue. API key scrubbing is covered separately
    /// (GitHubFeedbackLogic.ScrubKey / ServerLog.ScrubLine tests).
    /// </summary>
    [TestFixture]
    public class PrivacyScrubTests
    {
        private const string Profile = @"C:\Users\LeakMcGee";

        [Test]
        public void ScrubUserProfile_BackslashPath_Replaced()
        {
            string line = @"[MCP-LOG] stderr redirected to " + Profile + @"\AppData\Local\TopSolidMcp\logs\server.log";
            Assert.AreEqual(
                @"[MCP-LOG] stderr redirected to %USERPROFILE%\AppData\Local\TopSolidMcp\logs\server.log",
                PrivacyScrub.ScrubUserProfile(line, Profile));
        }

        [Test]
        public void ScrubUserProfile_ForwardSlashes_Matched_RemainderPreserved()
        {
            Assert.AreEqual("script at %USERPROFILE%/source/recipe.cs",
                PrivacyScrub.ScrubUserProfile("script at C:/Users/LeakMcGee/source/recipe.cs", Profile));
        }

        [Test]
        public void ScrubUserProfile_CaseInsensitive()
        {
            Assert.AreEqual("open %USERPROFILE%",
                PrivacyScrub.ScrubUserProfile("open c:/users/leakmcgee", Profile));
        }

        [Test]
        public void ScrubUserProfile_MixedSeparators()
        {
            Assert.AreEqual("mixed %USERPROFILE%\\Local/file ok",
                PrivacyScrub.ScrubUserProfile(@"mixed C:\Users/LeakMcGee\Local/file ok", Profile));
        }

        [Test]
        public void ScrubUserProfile_TrailingSeparatorInProfile_StillMatches()
        {
            Assert.AreEqual("in %USERPROFILE%\\Documents",
                PrivacyScrub.ScrubUserProfile(@"in C:\Users\LeakMcGee\Documents", Profile + @"\"));
        }

        [Test]
        public void ScrubUserProfile_MultipleOccurrences_AllReplaced()
        {
            Assert.AreEqual("from %USERPROFILE% to %USERPROFILE%",
                PrivacyScrub.ScrubUserProfile("from " + Profile + " to " + Profile.ToLower(), Profile));
        }

        [Test]
        public void ScrubUserProfile_FollowedByQuote_StillReplaced()
        {
            Assert.AreEqual("cannot open \"%USERPROFILE%\"",
                PrivacyScrub.ScrubUserProfile("cannot open \"" + Profile + "\"", Profile));
        }

        [Test]
        public void ScrubUserProfile_NoMatch_Unchanged()
        {
            string line = @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.exe";
            Assert.AreEqual(line, PrivacyScrub.ScrubUserProfile(line, Profile));
        }

        [TestCase(null)]
        [TestCase("")]
        public void ScrubUserProfile_NullOrEmptyText_ReturnedAsIs(string text)
        {
            Assert.AreEqual(text, PrivacyScrub.ScrubUserProfile(text, Profile));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("C:")]     // bare drive: would wrongly rewrite C:\Program Files
        [TestCase("C:\\")]   // drive root
        public void ScrubUserProfile_UnusableProfile_NoChange(string profile)
        {
            Assert.AreEqual(@"C:\Users\other\file.txt",
                PrivacyScrub.ScrubUserProfile(@"C:\Users\other\file.txt", profile));
        }

        [Test]
        public void ResolveProfile_ExplicitValueWins()
        {
            Assert.AreEqual(@"X:\custom\home", PrivacyScrub.ResolveProfile(@"X:\custom\home"));
        }

        [Test]
        public void ResolveProfile_NullOrEmpty_FallsBackToCurrentUser()
        {
            string resolved = PrivacyScrub.ResolveProfile(null);
            if (string.IsNullOrEmpty(resolved)) Assert.Ignore("no user profile on this host");
            Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), resolved);
            Assert.AreEqual(resolved, PrivacyScrub.ResolveProfile(""));
        }
    }
}