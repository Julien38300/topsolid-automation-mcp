using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// L10n core (GitHub issue #21): French on French Windows UI languages,
    /// English everywhere else, English fallback for missing strings.
    /// </summary>
    [TestFixture]
    public class L10nTests
    {
        [TearDown]
        public void ResetOverrides()
        {
            L10n.SetLanguageOverrideForTests(null);
            L10n.SetConfiguredLanguage(null);
            Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", null);
        }

        // ── Detection: fr-* is French, everything else is English ──

        [Test]
        public void DetectFromCulture_FrenchVariants_AreFrench()
        {
            foreach (string name in new[] { "fr-FR", "fr-BE", "fr-CA", "fr-LU", "fr-CH", "fr-MC" })
                Assert.AreEqual("fr", L10n.DetectFromCulture(name), name);
        }

        [Test]
        public void DetectFromCulture_NonFrench_AreEnglish()
        {
            foreach (string name in new[] { "nl-NL", "nl-BE", "de-DE", "en-US", "en-GB", "ja-JP", "es-ES" })
                Assert.AreEqual("en", L10n.DetectFromCulture(name), name);
        }

        [Test]
        public void DetectFromCulture_EmptyOrNull_IsEnglish()
        {
            Assert.AreEqual("en", L10n.DetectFromCulture(""));
            Assert.AreEqual("en", L10n.DetectFromCulture(null));
        }

        [Test]
        public void DetectFromCulture_UsesUiCultureNotRegionalFormat()
        {
            // The Windows UI language drives the choice, not the regional format:
            // a machine with an English UI language and a French regional format
            // must resolve to English.
            var englishUiFrenchFormat = CultureInfo.GetCultureInfo("en-US");
            Assert.AreEqual("en", L10n.DetectFromCulture(englishUiFrenchFormat.Name));
        }

        // ── Resolution order: test override > env var > settings.json > culture ──

        [Test]
        public void Language_TestOverrideWins()
        {
            L10n.SetLanguageOverrideForTests("fr");
            Assert.AreEqual("fr", L10n.Language);
            L10n.SetLanguageOverrideForTests("en");
            Assert.AreEqual("en", L10n.Language);
        }

        [Test]
        public void Language_EnvVarForcesLanguage()
        {
            L10n.SetLanguageOverrideForTests(null);
            try
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", "fr");
                Assert.AreEqual("fr", L10n.Language);

                // Reset the cached resolution so the new env value is read back.
                L10n.SetLanguageOverrideForTests(null);
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", "en");
                Assert.AreEqual("en", L10n.Language);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", null);
            }
        }

        [Test]
        public void Language_EnvVarInvalidValueFallsThroughToCulture()
        {
            L10n.SetLanguageOverrideForTests(null);
            try
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", "auto");
                Assert.AreEqual(L10n.DetectFromCulture(CultureInfo.CurrentUICulture.Name), L10n.Language);

                Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", "klingon");
                Assert.AreEqual(L10n.DetectFromCulture(CultureInfo.CurrentUICulture.Name), L10n.Language);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_LANG", null);
            }
        }

        [Test]
        public void Language_ConfiguredSettingSelectsLanguage()
        {
            L10n.SetLanguageOverrideForTests(null);
            L10n.SetConfiguredLanguage("fr");
            Assert.AreEqual("fr", L10n.Language);

            L10n.SetConfiguredLanguage("en");
            Assert.AreEqual("en", L10n.Language);
        }

        [Test]
        public void Language_ConfiguredAutoOrGarbageFallsBackToCulture()
        {
            L10n.SetLanguageOverrideForTests(null);
            L10n.SetConfiguredLanguage("auto");
            Assert.AreEqual(L10n.DetectFromCulture(CultureInfo.CurrentUICulture.Name), L10n.Language);

            L10n.SetConfiguredLanguage("nonsense");
            Assert.AreEqual(L10n.DetectFromCulture(CultureInfo.CurrentUICulture.Name), L10n.Language);

            L10n.SetConfiguredLanguage(null);
            Assert.AreEqual(L10n.DetectFromCulture(CultureInfo.CurrentUICulture.Name), L10n.Language);
        }

        // ── Strings: exact dialog/menu texts in both languages ──

        [Test]
        public void T_FrenchStrings_WhenFrenchSelected()
        {
            L10n.SetLanguageOverrideForTests("fr");
            Assert.AreEqual("Arrêter le serveur", L10n.T("tray.menu.quit"));
            Assert.AreEqual("Se reconnecter à TopSolid", L10n.T("tray.menu.reconnect"));
            Assert.AreEqual("Paramètres du serveur MCP…", L10n.T("tray.menu.mcpSettings"));
            Assert.AreEqual("Copier la clé API", L10n.T("tray.menu.copyApiKey"));
        }

        [Test]
        public void T_EnglishStrings_WhenEnglishSelected()
        {
            L10n.SetLanguageOverrideForTests("en");
            Assert.AreEqual("Stop the server", L10n.T("tray.menu.quit"));
            Assert.AreEqual("Reconnect to TopSolid", L10n.T("tray.menu.reconnect"));
            Assert.AreEqual("MCP server settings…", L10n.T("tray.menu.mcpSettings"));
            Assert.AreEqual("Copy the API key", L10n.T("tray.menu.copyApiKey"));
        }

        [Test]
        public void T_ApiKeyDialog_MatchesIssue21Example()
        {
            // The exact English dialog requested in issue #21.
            L10n.SetLanguageOverrideForTests("en");
            Assert.AreEqual("TopSolid MCP — API key setup", L10n.T("tray.apiKeyPrompt.title"));
            Assert.AreEqual(
                "No API key is configured for the HTTP MCP server.\n\n" +
                "• Yes: automatically generate a secure key (recommended)\n" +
                "• No: open the MCP server settings to enter an existing key\n" +
                "• Cancel: configure it later",
                L10n.T("tray.apiKeyPrompt.body"));
        }

        [Test]
        public void T_ApiKeyDialog_FrenchUnchanged()
        {
            L10n.SetLanguageOverrideForTests("fr");
            Assert.AreEqual("TopSolid MCP — Configuration de la clé API", L10n.T("tray.apiKeyPrompt.title"));
            Assert.AreEqual(
                "Aucune clé API n'est configurée pour le serveur MCP HTTP.\n\n" +
                "• Oui : générer automatiquement une clé sécurisée (recommandé)\n" +
                "• Non : ouvrir les paramètres du serveur MCP pour saisir une clé existante\n" +
                "• Annuler : configurer plus tard",
                L10n.T("tray.apiKeyPrompt.body"));
        }

        [Test]
        public void T_FormatsArguments()
        {
            L10n.SetLanguageOverrideForTests("en");
            Assert.AreEqual("You are already up to date (v1.8.19).", L10n.T("tray.update.upToDate", "1.8.19"));
            L10n.SetLanguageOverrideForTests("fr");
            Assert.AreEqual("Vous êtes déjà à jour (v1.8.19).", L10n.T("tray.update.upToDate", "1.8.19"));
        }

        [Test]
        public void T_MissingFrenchEntry_FallsBackToEnglish()
        {
            // Keys that are identical in both languages exist only in the English
            // table: the French lookup must fall back to English, not fail.
            L10n.SetLanguageOverrideForTests("fr");
            Assert.AreEqual("Français", L10n.T("settings.language.fr"));
            Assert.AreEqual("English", L10n.T("settings.language.en"));
        }

        [Test]
        public void T_UnknownKey_ReturnsTheKeyItself()
        {
            L10n.SetLanguageOverrideForTests("fr");
            Assert.AreEqual("no.such.key", L10n.T("no.such.key"));
            Assert.AreEqual("no.such.key", L10n.T("no.such.key", "arg"));
        }

        // ── Dictionary integrity: French is a strict subset of English ──

        [Test]
        public void Tables_EveryFrenchKeyExistsInEnglish()
        {
            foreach (string key in L10n.AllFrenchKeys())
            {
                Assert.IsTrue(L10n.HasEnglish(key), "French key missing from the English table: " + key);
                Assert.IsFalse(string.IsNullOrEmpty(L10n.GetForLanguage(key, "en")), "empty English value: " + key);
                Assert.IsFalse(string.IsNullOrEmpty(L10n.GetForLanguage(key, "fr")), "empty French value: " + key);
            }
        }

        [Test]
        public void Tables_KnownKeysPresent()
        {
            foreach (string key in new[]
                     {
                         "tray.menu.quit", "tray.menu.reconnect", "tray.menu.mcpSettings", "tray.menu.copyApiKey",
                         "tray.apiKeyPrompt.title", "tray.apiKeyPrompt.body", "tray.update.upToDate",
                         "settings.language.fr", "settings.language.en"
                     })
            {
                Assert.IsTrue(L10n.HasEnglish(key), "missing English key: " + key);
            }
        }
    }
}
