using System;
using NUnit.Framework;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    [TestFixture]
    public class HttpAuthLogicTests
    {
        // ─────────────────────────────────────────────────────────────
        // Key shape — the tray and the HTTP server must agree on one format.
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void GenerateKey_ProducesValidShape()
        {
            string key = McpHttpAuthLogic.GenerateKey();
            Assert.IsTrue(McpHttpAuthLogic.IsValidKeyShape(key), "generated key must validate: " + key);
            StringAssert.StartsWith(McpHttpAuthLogic.KeyPrefix, key);
            Assert.AreEqual(McpHttpAuthLogic.KeyPrefix.Length + McpHttpAuthLogic.KeyLength, key.Length);
        }

        [Test]
        public void GenerateKey_IsUnique()
        {
            var a = McpHttpAuthLogic.GenerateKey();
            var b = McpHttpAuthLogic.GenerateKey();
            Assert.AreNotEqual(a, b);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("short")]
        [TestCase("tsmcp_onlyprefix")]
        [TestCase("tsmcp_zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]   // non-hex
        [TestCase("TSMCP_abcdefabcdefabcdefabcdefabcdef12")]  // uppercase prefix
        public void IsValidKeyShape_RejectsBad(string key)
        {
            Assert.IsFalse(McpHttpAuthLogic.IsValidKeyShape(key));
        }

        [Test]
        public void IsValidKeyShape_AcceptsGood()
        {
            Assert.IsTrue(McpHttpAuthLogic.IsValidKeyShape("tsmcp_0123456789abcdef0123456789abcdef"));
        }

        // ─────────────────────────────────────────────────────────────
        // Masking — the full key must NEVER appear in a menu label.
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void MaskKey_ShowsOnlyLast4()
        {
            string key = "tsmcp_0123456789abcdef0123456789abcdef";
            string masked = McpHttpAuthLogic.MaskKey(key);
            StringAssert.DoesNotContain(key, masked);
            StringAssert.DoesNotContain("0123456789", masked);
            StringAssert.EndsWith("cdef", masked);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ab")]
        public void MaskKey_EmptyOrShort(string key)
        {
            string masked = McpHttpAuthLogic.MaskKey(key);
            StringAssert.DoesNotContain("tsmcp", masked);
        }

        // ─────────────────────────────────────────────────────────────
        // Auth decision — empty expected key = auth disabled (local mode).
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void IsAuthorized_EmptyExpected_AllowsAll()
        {
            Assert.IsTrue(McpHttpAuthLogic.IsAuthorized("", null));
            Assert.IsTrue(McpHttpAuthLogic.IsAuthorized("", "whatever"));
        }

        [Test]
        public void IsAuthorized_MissingOrWrongKey_Rejected()
        {
            string expected = "tsmcp_0123456789abcdef0123456789abcdef";
            Assert.IsFalse(McpHttpAuthLogic.IsAuthorized(expected, null));
            Assert.IsFalse(McpHttpAuthLogic.IsAuthorized(expected, ""));
            Assert.IsFalse(McpHttpAuthLogic.IsAuthorized(expected, "tsmcp_ffffffffffffffffffffffffffffffff"));
        }

        [Test]
        public void IsAuthorized_CorrectKey_Accepted()
        {
            string expected = "tsmcp_0123456789abcdef0123456789abcdef";
            Assert.IsTrue(McpHttpAuthLogic.IsAuthorized(expected, expected));
        }

        [Test]
        public void KeysEqual_LengthMismatchIsFalse()
        {
            Assert.IsFalse(McpHttpAuthLogic.KeysEqual("tsmcp_0123456789abcdef0123456789abcdef", "short"));
        }

        // ─────────────────────────────────────────────────────────────
        // Sessions
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void NewSessionId_IsValidUuid()
        {
            string sid = McpHttpAuthLogic.NewSessionId();
            Assert.IsTrue(McpHttpAuthLogic.IsValidSessionId(sid));
            Assert.AreNotEqual(sid, McpHttpAuthLogic.NewSessionId());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-uuid")]
        public void IsValidSessionId_RejectsGarbage(string sid)
        {
            Assert.IsFalse(McpHttpAuthLogic.IsValidSessionId(sid));
        }
    }

    [TestFixture]
    public class ApiKeyStoreTests
    {
        [Test]
        public void ResolveKey_StoredWinsOverEnv()
        {
            Assert.AreEqual("stored", ApiKeyStore.ResolveKey("stored", "env"));
        }

        [Test]
        public void ResolveKey_EnvFallbackWhenNoStored()
        {
            Assert.AreEqual("env", ApiKeyStore.ResolveKey("", "env"));
            Assert.AreEqual("env", ApiKeyStore.ResolveKey(null, "env"));
        }

        [Test]
        public void ResolveKey_NothingAnywhere_Empty()
        {
            Assert.AreEqual("", ApiKeyStore.ResolveKey("", ""));
            Assert.AreEqual("", ApiKeyStore.ResolveKey(null, null));
        }

        [Test]
        public void ProtectUnprotect_RoundTrips()
        {
            string key = "tsmcp_0123456789abcdef0123456789abcdef";
            string enc = ApiKeyStore.Protect(key);
            Assert.AreNotEqual(key, enc);
            StringAssert.DoesNotContain(key, enc);
            Assert.AreEqual(key, ApiKeyStore.Unprotect(enc));
        }

        [Test]
        public void Unprotect_Garbage_ReturnsEmptyNeverThrows()
        {
            Assert.AreEqual("", ApiKeyStore.Unprotect("not base64 !!!"));
            Assert.AreEqual("", ApiKeyStore.Unprotect("AAAA"));
            Assert.AreEqual("", ApiKeyStore.Unprotect(null));
            Assert.AreEqual("", ApiKeyStore.Unprotect(""));
        }
    }

    [TestFixture]
    public class GitHubFeedbackLogicTests
    {
        [Test]
        public void BuildTitle_BugAndFeature()
        {
            Assert.AreEqual("[bug] Crash", GitHubFeedbackLogic.BuildTitle("bug", "Crash"));
            Assert.AreEqual("[feature] Export", GitHubFeedbackLogic.BuildTitle("feature", "Export"));
        }

        [Test]
        public void BuildTitle_TruncatesLongSubjects()
        {
            string title = GitHubFeedbackLogic.BuildTitle("bug", new string('x', 300));
            Assert.LessOrEqual(title.Length, 100);
        }

        [Test]
        public void BuildBody_ScrubsApiKey()
        {
            string key = "tsmcp_secretsecretsecretsecretsecret1";
            string body = GitHubFeedbackLogic.BuildBody("desc with " + key + " inside",
                "1.8.0", "Win11", true, 8090,
                new[] { "log line with " + key }, key);
            StringAssert.DoesNotContain(key, body);
            StringAssert.Contains("***", body);
        }

        [Test]
        public void BuildBody_ContainsEnvironmentBlock()
        {
            string body = GitHubFeedbackLogic.BuildBody("d", "1.8.0", "Microsoft Windows NT 10.2", false, 8090, null, null);
            StringAssert.Contains("1.8.0", body);
            StringAssert.Contains("Microsoft Windows NT 10.2", body);
            StringAssert.Contains("non", body);
        }

        [Test]
        public void BuildIssueJson_IsValidJsonWithLabels()
        {
            string json = GitHubFeedbackLogic.BuildIssueJson("[bug] t", "body", new[] { "tray-report" });
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            Assert.AreEqual("[bug] t", (string)obj["title"]);
            Assert.AreEqual("body", (string)obj["body"]);
            Assert.AreEqual("tray-report", (string)obj["labels"][0]);
        }

        [Test]
        public void BuildNewIssueUrl_EncodesAndPointsAtRepo()
        {
            string url = GitHubFeedbackLogic.BuildNewIssueUrl("bug", "Mon titre", " Corps\nmulti-ligne");
            StringAssert.StartsWith("https://github.com/Julien38300/topsolid-automation-mcp/issues/new?", url);
            StringAssert.Contains("template=bug_report.md", url);
            StringAssert.DoesNotContain(" ", url.Split('?')[1]);
        }

        [Test]
        public void BuildReleaseUrl_TagAndLatest()
        {
            Assert.AreEqual(
                "https://github.com/Julien38300/topsolid-automation-mcp/releases/tag/v1.8.0",
                GitHubFeedbackLogic.BuildReleaseUrl("1.8.0"));
            Assert.AreEqual(
                "https://github.com/Julien38300/topsolid-automation-mcp/releases/tag/v1.7.4",
                GitHubFeedbackLogic.BuildReleaseUrl("v1.7.4"));
            Assert.AreEqual(
                "https://github.com/Julien38300/topsolid-automation-mcp/releases/latest",
                GitHubFeedbackLogic.BuildReleaseUrl(null));
        }
    }

    [TestFixture]
    public class ReconnectBackoffTests
    {
        [Test]
        public void DelayFor_Steps15_30_60ThenCap()
        {
            Assert.AreEqual(15, ReconnectBackoff.DelayFor(1).TotalSeconds);
            Assert.AreEqual(30, ReconnectBackoff.DelayFor(2).TotalSeconds);
            Assert.AreEqual(60, ReconnectBackoff.DelayFor(3).TotalSeconds);
            Assert.AreEqual(60, ReconnectBackoff.DelayFor(4).TotalSeconds);
            Assert.AreEqual(60, ReconnectBackoff.DelayFor(100).TotalSeconds);
        }

        [Test]
        public void DelayFor_InvalidAttempt_FailsSafeToCap()
        {
            Assert.AreEqual(60, ReconnectBackoff.DelayFor(0).TotalSeconds);
            Assert.AreEqual(60, ReconnectBackoff.DelayFor(-5).TotalSeconds);
        }

        [Test]
        public void ShouldRetry_DisabledOrConnected_Never()
        {
            var now = DateTime.UtcNow;
            Assert.IsFalse(ReconnectBackoff.ShouldRetry(false, false, now.AddHours(-1), now, 1), "disabled");
            Assert.IsFalse(ReconnectBackoff.ShouldRetry(true, true, now.AddHours(-1), now, 1), "connected");
        }

        [Test]
        public void ShouldRetry_RespectsBackoff()
        {
            var last = DateTime.UtcNow;
            // DelayFor(1)=15s, DelayFor(2)=30s, DelayFor(3+)=60s — the delay is the
            // wait required AFTER attempt N, before trying attempt N+1.
            Assert.IsTrue(ReconnectBackoff.ShouldRetry(true, false, last.AddSeconds(-20), DateTime.UtcNow, 1), "20s after attempt 1 → due");
            Assert.IsFalse(ReconnectBackoff.ShouldRetry(true, false, last.AddSeconds(-10), DateTime.UtcNow, 1), "10s after attempt 1 → not yet");
            Assert.IsFalse(ReconnectBackoff.ShouldRetry(true, false, last.AddSeconds(-40), DateTime.UtcNow, 3), "40s after attempt 3 → not yet (60 cap)");
            Assert.IsTrue(ReconnectBackoff.ShouldRetry(true, false, last.AddSeconds(-70), DateTime.UtcNow, 3), "70s after attempt 3 → due");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Origin validation (MCP HTTP transport spec — DNS rebinding defense).
    // ValidateOrigin returns null = allowed, or an error message = reject.
    // ─────────────────────────────────────────────────────────────────
    [TestFixture]
    public class OriginValidationTests
    {
        [TearDown]
        public void ClearEnv()
        {
            Environment.SetEnvironmentVariable(McpOriginPolicy.AllowedOriginsEnvVar, null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NullOrEmptyOrigin_Allowed(string origin)
        {
            // No Origin header = CLI/MCP clients/Tailscale: the spec says allow.
            Assert.IsNull(McpOriginPolicy.Validate(origin));
        }

        [TestCase("http://localhost:3000")]
        [TestCase("https://localhost")]
        [TestCase("http://127.0.0.1:8080")]
        [TestCase("http://[::1]:9999")]
        [TestCase("http://LOCALHOST:5000")]   // host comparison is case-insensitive
        public void LocalOrigins_Allowed(string origin)
        {
            Assert.IsNull(McpOriginPolicy.Validate(origin));
        }

        [TestCase("http://intranet.corp.local")]
        [TestCase("http://evil-attacker.com")]
        [TestCase("https://192.168.1.50:8080")]
        [TestCase("ftp://localhost")]
        [TestCase("not a uri at all !!!")]
        public void ForeignOrMalformedOrigins_Rejected(string origin)
        {
            Assert.IsNotNull(McpOriginPolicy.Validate(origin), "must reject: " + origin);
        }

        [Test]
        public void AllowedOriginsEnv_ListsExtraHost()
        {
            Environment.SetEnvironmentVariable(McpOriginPolicy.AllowedOriginsEnvVar,
                "http://mybox.mydomain.lan, https://dashboard.example.org");
            Assert.IsNull(McpOriginPolicy.Validate("http://mybox.mydomain.lan"));
            Assert.IsNull(McpOriginPolicy.Validate("https://dashboard.example.org"));
            // The whitelist does not open everything: still reject foreign hosts.
            Assert.IsNotNull(McpOriginPolicy.Validate("http://other.box.lan"));
        }

        [Test]
        public void ErrorMessage_MentionsRebinding()
        {
            string error = McpOriginPolicy.Validate("http://evil.example.org");
            StringAssert.Contains("DNS-rebinding", error);
        }
    }
}