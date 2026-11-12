using System;
using System.Collections.Generic;
using System.Globalization;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// UI localization core (GitHub issue #21): French on French Windows UI
    /// languages (fr-*), English everywhere else, with an English fallback when
    /// a French translation is unavailable.
    ///
    /// The language is selected from the Windows display/UI language
    /// (<see cref="CultureInfo.CurrentUICulture"/>), NOT from the regional
    /// date/number format (<see cref="CultureInfo.CurrentCulture"/>) nor the
    /// geographic location: a Dutch or German Windows gets English, a French
    /// regional format with an English UI language still gets English.
    ///
    /// Resolution order (first wins):
    ///   1. test override (unit tests only),
    ///   2. TOPSOLID_MCP_LANG environment variable ("fr" / "en"),
    ///   3. settings.json "language" injected via <see cref="SetConfiguredLanguage"/>
    ///      ("fr" / "en" — "auto" falls through),
    ///   4. automatic detection from the Windows UI culture.
    ///
    /// Pure static class with no WinForms dependency, so it can be source-linked
    /// into any test suite. Missing keys fall back to English, then to the key
    /// itself — the UI must never crash over a missing string.
    /// </summary>
    public static class L10n
    {
        internal const string LangFr = "fr";
        internal const string LangEn = "en";

        private static readonly object _lock = new object();
        private static string _forcedLanguage;      // unit tests only
        private static string _configuredLanguage; // settings.json ("fr"/"en"/"auto")
        static string _resolvedLanguage;            // cached resolution

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── Live status (tooltip, header items) ──
            ["tray.state.connected"] = "connected",
            ["tray.state.disconnected"] = "disconnected",
            ["tray.status.readOnlySuffix"] = " [read-only]",
            ["tray.tooltip"] = "TopSolid MCP v{0} — {1} (port {2}){3}",
            ["tray.tooltip.updateAvailable"] = "\nUpdate v{0} available",
            ["tray.status.topSolid"] = "{0}  TopSolid: {1} (port {2}){3}",
            ["tray.status.mcpServer"] = "\u25CF  MCP server: http://localhost:{0}/mcp",

            // ── Root menu ──
            ["tray.menu.instance"] = "TopSolid instance",
            ["tray.menu.reconnect"] = "Reconnect to TopSolid",
            ["tray.menu.mcpSettings"] = "MCP server settings…",
            ["tray.menu.copyApiKey"] = "Copy the API key",
            ["tray.menu.help"] = "Help && Maintenance",
            ["tray.menu.docs"] = "Online documentation",
            ["tray.menu.whatsNew"] = "What's new in this version",
            ["tray.menu.openInstallFolder"] = "Open the installation folder",
            ["tray.menu.reportBug"] = "Report a bug…",
            ["tray.menu.suggestImprovement"] = "Suggest an improvement…",
            ["tray.menu.githubPage"] = "Project GitHub page",
            ["tray.menu.checkUpdates"] = "Check for updates…",
            ["tray.menu.downloadUpdate"] = "Download update…",
            ["tray.menu.downloadUpdateVersion"] = "Download update v{0}…",
            ["tray.menu.quit"] = "Stop the server",

            // ── Update check / update dialogs ──
            ["tray.update.alreadyCheckingTitle"] = "Checking…",
            ["tray.update.alreadyCheckingBody"] = "An update check is already running…",
            ["tray.update.searchingTitle"] = "Searching for update…",
            ["tray.update.searchingBody"] = "Querying GitHub…",
            ["tray.update.title"] = "Update",
            ["tray.update.cannotReadRemote"] = "Could not read the remote version information from GitHub.",
            ["tray.update.upToDate"] = "You are already up to date (v{0}).",
            ["tray.update.cannotContact"] = "Could not reach GitHub (network?).\r\n\r\nDetails: {0}",
            ["tray.update.scriptNotFound"] = "update.ps1 was not found next to the executable.",
            ["tray.error.title"] = "Error",
            ["tray.update.launchFailed"] = "Could not start the update: {0}",
            ["tray.update.balloonTitle"] = "TopSolid MCP v{0} available",
            ["tray.update.balloonBody"] = "Update v{0} available! Right-click the tray icon to install.",
            ["tray.update.modalBody"] = "Update v{0} available! A \"Download update\" button has been added to the menu.",

            // ── Auto-reconnect notifications ──
            ["tray.reconnect.reconnectedTitle"] = "TopSolid reconnected",
            ["tray.reconnect.reconnectedBody"] = "Connection restored on port {0}.",
            ["tray.reconnect.disconnectedTitle"] = "TopSolid disconnected",
            ["tray.reconnect.disconnectedBody"] = "The TopSolid session was closed.",
            ["tray.reconnect.autoRetrySuffix"] = " Automatic reconnection is on.",

            // ── API key setup (the issue #21 example dialog) ──
            ["tray.apiKeyPrompt.title"] = "TopSolid MCP — API key setup",
            ["tray.apiKeyPrompt.body"] =
                "No API key is configured for the HTTP MCP server.\n\n" +
                "• Yes: automatically generate a secure key (recommended)\n" +
                "• No: open the MCP server settings to enter an existing key\n" +
                "• Cancel: configure it later",
            ["tray.apiKey.generatedTitle"] = "TopSolid MCP — API key",
            ["tray.apiKey.generatedBody"] =
                "New API key generated and copied to the clipboard:\n\n{0}\n\n" +
                "Paste it into the X-API-Key header of your MCP client.",
            ["tray.apiKey.newBalloonTitle"] = "New API key",
            ["tray.apiKey.newBalloonBody"] = "Generated and copied to the clipboard.",
            ["tray.apiKey.copiedTitle"] = "API key copied",
            ["tray.apiKey.copiedBody"] = "The API key has been copied to the clipboard.",
            ["tray.apiKey.regenTitle"] = "Regenerate the API key",
            ["tray.apiKey.regenBody"] =
                "Generate a new API key?\n\n" +
                "The old key will be invalidated immediately and the new key will be copied to the clipboard.",

            // ── Feedback dialog ──
            ["tray.feedback.thanksTitle"] = "Thanks for your feedback!",
            ["tray.feedback.thanksBody"] = "GitHub issue created — click to open it.",
            ["tray.feedback.bugTitle"] = "Report a bug — TopSolid MCP",
            ["tray.feedback.featureTitle"] = "Suggest an improvement — TopSolid MCP",
            ["tray.feedback.subject"] = "Subject:",
            ["tray.feedback.bugSteps"] = "Steps to reproduce / observed behavior:",
            ["tray.feedback.featureDesc"] = "Description of the desired improvement:",
            ["tray.feedback.includeLogs"] = "Attach the version, the OS and the last 50 log lines (API key masked)",
            ["tray.feedback.send"] = "Send",

            // ── MCP Server Settings dialog ──
            ["settings.title"] = "MCP server settings",
            ["settings.subtitle"] = "HTTP endpoint, API authentication key and server behavior",
            ["settings.cardHttp"] = "HTTP ENDPOINT (STREAMABLE HTTP)",
            ["settings.urlLabel"] = "MCP client URL:",
            ["settings.copyUrl"] = "Copy URL",
            ["settings.urlCopied"] = "\u2713 URL copied",
            ["settings.portLabel"] = "TCP listen port:",
            ["settings.apply"] = "Apply",
            ["settings.activeOnPort"] = "Active on port {0}",
            ["settings.cardAuth"] = "AUTHENTICATION (X-API-KEY HEADER)",
            ["settings.show"] = "Show",
            ["settings.hide"] = "Hide",
            ["settings.copy"] = "Copy",
            ["settings.noKeyDefined"] = "No API key defined.",
            ["settings.keyCopied"] = "\u2713 API key copied to the clipboard",
            ["settings.regenKey"] = "Regenerate a key…",
            ["settings.enterKey"] = "Enter manually…",
            ["settings.saveKey"] = "Save the key",
            ["settings.keyDpapi"] = "Encrypted with Windows DPAPI",
            ["settings.cardBehavior"] = "BEHAVIOR && SECURITY",
            ["settings.autoConnect"] = "Automatically connect to TopSolid",
            ["settings.readOnly"] = "Read-only mode (on restart)",
            ["settings.languageLabel"] = "Language:",
            ["settings.language.auto"] = "Automatic (Windows language)",
            ["settings.language.fr"] = "Français",
            ["settings.language.en"] = "English",
            ["settings.language.hint"] = "(applied on restart)",
            ["settings.autoStart"] = "Start with Windows",
            ["settings.autoStart.on"] = "Starts at Windows logon",
            ["settings.autoStart.off"] = "No longer starts with Windows",
            ["settings.autoStart.applying"] = "Applying...",
            ["settings.autoStart.failed"] = "Not changed (rights refused?)",
            ["settings.autoStart.unknown"] = "State unknown",
            ["settings.autoStart.cancelled"] = "Administrator rights refused.",
            ["tray.toast.title"] = "Version {0} is available",
            ["tray.toast.body"] = "You are running {0}. The update checks its SHA-256 and keeps your settings.",
            ["tray.toast.install"] = "Download and install",
            ["tray.toast.notes"] = "What's new",
            ["tray.toast.later"] = "Later",
            ["settings.openFolder"] = "Open the installation folder",
            ["settings.close"] = "Close",
            ["settings.noKeyPlaceholder"] = "(no key configured — open local access)",
            ["settings.portActive"] = "\u2713 Port {0} active",
            ["settings.portUnavailable"] = "Port {0} unavailable",
            ["settings.regenDone"] = "\u2713 New key generated and copied",
            ["settings.editHint"] = "Edit then click Save",
            ["settings.authDisabled"] = "Authentication disabled",
            ["settings.keySaved"] = "\u2713 API key saved",

            // ── Instance picker submenu & balloons ──
            ["tray.instance.loading"] = "(loading…)",
            ["tray.instance.noneRunning"] = "(no TopSolid running)",
            ["tray.instance.automationInactive"] = " (Automation inactive)",
            ["tray.instance.declaredRunning"] = " [running]",
            ["tray.instance.declaredStopped"] = " [not started]",
            ["tray.instance.autoMode"] = "Automatic (first TopSolid detected)",
            ["tray.instance.manage"] = "Manage TopSolid instances…",
            ["tray.instance.selectedTitle"] = "TopSolid instance selected",
            ["tray.instance.selectedBody"] = "Connected to TopSolid on port {0}.",
            ["tray.instance.unavailableTitle"] = "TopSolid not available",
            ["tray.instance.unavailableBody"] = "Port {0} saved, but no TopSolid answers on that port.",
            ["tray.instance.autoEnabledTitle"] = "Automatic mode enabled",
            ["tray.instance.autoEnabledOk"] = "Connected to TopSolid (port {0}).",
            ["tray.instance.autoEnabledWait"] = "No TopSolid active on port {0} — automatic reconnection pending.",

            // ── Manage TopSolid instances dialog ──
            ["manage.title"] = "Manage TopSolid instances",
            ["manage.header"] = "Declared TopSolid instances",
            ["manage.subtitle"] = "Set the display order of your CAD sessions and the default Automation port",
            ["manage.cardList"] = "ORDERED INSTANCE LIST",
            ["manage.moveUp"] = "\u25B2  Move up",
            ["manage.moveDown"] = "\u25BC  Move down",
            ["manage.default"] = "\u2605  Default",
            ["manage.edit"] = "\u270E  Edit",
            ["manage.remove"] = "\u2715  Remove",
            ["manage.addInstance"] = "ADD A TOPSOLID INSTANCE",
            ["manage.editInstance"] = "EDIT INSTANCE #{0}",
            ["manage.nameLabel"] = "Instance name",
            ["manage.portLabel"] = "Automation port",
            ["manage.add"] = "Add",
            ["manage.save"] = "Save",
            ["manage.cancel"] = "Cancel",
            ["manage.autosaveHint"] = "Changes are saved automatically.",
            ["manage.close"] = "Close",
            ["manage.emptyState"] = "No declared instance (automatic detection active)",
            ["manage.defaultBadge"] = "\u2605 DEFAULT",
        };

        private static readonly Dictionary<string, string> Fr = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── Live status (tooltip, header items) ──
            ["tray.state.connected"] = "connecté",
            ["tray.state.disconnected"] = "déconnecté",
            ["tray.status.readOnlySuffix"] = " [lecture seule]",
            ["tray.tooltip.updateAvailable"] = "\nMise à jour v{0} disponible",
            ["tray.status.topSolid"] = "{0}  TopSolid : {1} (port {2}){3}",
            ["tray.status.mcpServer"] = "\u25CF  Serveur MCP : http://localhost:{0}/mcp",

            // ── Root menu ──
            ["tray.menu.instance"] = "Instance TopSolid",
            ["tray.menu.reconnect"] = "Se reconnecter à TopSolid",
            ["tray.menu.mcpSettings"] = "Paramètres du serveur MCP…",
            ["tray.menu.copyApiKey"] = "Copier la clé API",
            ["tray.menu.help"] = "Aide && Maintenance",
            ["tray.menu.docs"] = "Documentation en ligne",
            ["tray.menu.whatsNew"] = "Nouveautés de cette version",
            ["tray.menu.openInstallFolder"] = "Ouvrir le dossier d'installation",
            ["tray.menu.reportBug"] = "Signaler un bug…",
            ["tray.menu.suggestImprovement"] = "Proposer une amélioration…",
            ["tray.menu.githubPage"] = "Page GitHub du projet",
            ["tray.menu.checkUpdates"] = "Vérifier les mises à jour…",
            ["tray.menu.downloadUpdate"] = "Télécharger la mise à jour…",
            ["tray.menu.downloadUpdateVersion"] = "Télécharger la mise à jour v{0}…",
            ["tray.menu.quit"] = "Arrêter le serveur",

            // ── Update check / update dialogs ──
            ["tray.update.alreadyCheckingTitle"] = "Vérification en cours",
            ["tray.update.alreadyCheckingBody"] = "Une vérification des mises à jour est déjà en cours…",
            ["tray.update.searchingTitle"] = "Recherche de mise à jour…",
            ["tray.update.searchingBody"] = "Interrogation de GitHub…",
            ["tray.update.title"] = "Mise à jour",
            ["tray.update.cannotReadRemote"] = "Impossible de lire les informations de version distante sur GitHub.",
            ["tray.update.upToDate"] = "Vous êtes déjà à jour (v{0}).",
            ["tray.update.cannotContact"] = "Impossible de contacter GitHub (connexion ?).\r\n\r\nDétail : {0}",
            ["tray.update.scriptNotFound"] = "update.ps1 est introuvable à côté de l'exécutable.",
            ["tray.error.title"] = "Erreur",
            ["tray.update.launchFailed"] = "Impossible de lancer la mise à jour : {0}",
            ["tray.update.balloonTitle"] = "TopSolid MCP v{0} disponible",
            ["tray.update.balloonBody"] = "Mise à jour v{0} disponible ! Clic droit sur l'icône pour l'installer.",
            ["tray.update.modalBody"] = "Mise à jour v{0} disponible ! Un bouton « Télécharger la mise à jour » a été ajouté au menu.",

            // ── Auto-reconnect notifications ──
            ["tray.reconnect.reconnectedTitle"] = "TopSolid reconnecté",
            ["tray.reconnect.reconnectedBody"] = "Connexion rétablie sur le port {0}.",
            ["tray.reconnect.disconnectedTitle"] = "TopSolid déconnecté",
            ["tray.reconnect.disconnectedBody"] = "La session TopSolid a été fermée.",
            ["tray.reconnect.autoRetrySuffix"] = " Reconnexion automatique activée.",

            // ── API key setup (unchanged French wording, per issue #21) ──
            ["tray.apiKeyPrompt.title"] = "TopSolid MCP — Configuration de la clé API",
            ["tray.apiKeyPrompt.body"] =
                "Aucune clé API n'est configurée pour le serveur MCP HTTP.\n\n" +
                "• Oui : générer automatiquement une clé sécurisée (recommandé)\n" +
                "• Non : ouvrir les paramètres du serveur MCP pour saisir une clé existante\n" +
                "• Annuler : configurer plus tard",
            ["tray.apiKey.generatedTitle"] = "TopSolid MCP — Clé API",
            ["tray.apiKey.generatedBody"] =
                "Nouvelle clé API générée et copiée dans le presse-papiers :\n\n{0}\n\n" +
                "Collez-la dans l'en-tête X-API-Key de votre client MCP.",
            ["tray.apiKey.newBalloonTitle"] = "Nouvelle clé API",
            ["tray.apiKey.newBalloonBody"] = "Générée et copiée dans le presse-papiers.",
            ["tray.apiKey.copiedTitle"] = "Clé API copiée",
            ["tray.apiKey.copiedBody"] = "La clé API a été copiée dans le presse-papiers.",
            ["tray.apiKey.regenTitle"] = "Régénérer la clé API",
            ["tray.apiKey.regenBody"] =
                "Générer une nouvelle clé API ?\n\n" +
                "L'ancienne clé sera immédiatement invalidée et la nouvelle clé sera copiée dans le presse-papiers.",

            // ── Feedback dialog ──
            ["tray.feedback.thanksTitle"] = "Merci pour votre retour !",
            ["tray.feedback.thanksBody"] = "Ticket GitHub créé — cliquez pour l'ouvrir.",
            ["tray.feedback.bugTitle"] = "Signaler un bug — TopSolid MCP",
            ["tray.feedback.featureTitle"] = "Proposer une amélioration — TopSolid MCP",
            ["tray.feedback.subject"] = "Sujet :",
            ["tray.feedback.bugSteps"] = "Étapes pour reproduire / comportement observé :",
            ["tray.feedback.featureDesc"] = "Description de l'amélioration souhaitée :",
            ["tray.feedback.includeLogs"] = "Joindre la version, l'OS et les 50 dernières lignes de log (clé API masquée)",
            ["tray.feedback.send"] = "Envoyer",

            // ── MCP Server Settings dialog ──
            ["settings.title"] = "Paramètres du serveur MCP",
            ["settings.subtitle"] = "Point d'accès HTTP, clé d'authentification API et comportement du serveur",
            ["settings.cardHttp"] = "POINT D'ACCÈS HTTP (STREAMABLE HTTP)",
            ["settings.urlLabel"] = "URL cliente MCP :",
            ["settings.copyUrl"] = "Copier l'URL",
            ["settings.urlCopied"] = "\u2713 URL copiée",
            ["settings.portLabel"] = "Port d'écoute TCP :",
            ["settings.apply"] = "Appliquer",
            ["settings.activeOnPort"] = "Actif sur le port {0}",
            ["settings.cardAuth"] = "AUTHENTIFICATION (EN-TÊTE X-API-KEY)",
            ["settings.show"] = "Afficher",
            ["settings.hide"] = "Masquer",
            ["settings.copy"] = "Copier",
            ["settings.noKeyDefined"] = "Aucune clé API définie.",
            ["settings.keyCopied"] = "\u2713 Clé API copiée dans le presse-papiers",
            ["settings.regenKey"] = "Régénérer une clé…",
            ["settings.enterKey"] = "Saisir manuellement…",
            ["settings.saveKey"] = "Enregistrer la clé",
            ["settings.keyDpapi"] = "Chiffrée via Windows DPAPI",
            ["settings.cardBehavior"] = "COMPORTEMENT && SÉCURITÉ",
            ["settings.autoConnect"] = "Connexion automatique à TopSolid",
            ["settings.readOnly"] = "Mode lecture seule (au redémarrage)",
            ["settings.languageLabel"] = "Langue :",
            ["settings.language.auto"] = "Automatique (langue de Windows)",
            ["settings.language.hint"] = "(appliqué au redémarrage)",
            ["settings.autoStart"] = "Démarrer avec Windows",
            ["settings.autoStart.on"] = "Démarre à l'ouverture de session",
            ["settings.autoStart.off"] = "Ne démarre plus avec Windows",
            ["settings.autoStart.applying"] = "Application…",
            ["settings.autoStart.failed"] = "Non modifié (droits refusés ?)",
            ["settings.autoStart.unknown"] = "État inconnu",
            ["settings.autoStart.cancelled"] = "Droits administrateur refusés.",
            ["tray.toast.title"] = "La version {0} est disponible",
            ["tray.toast.body"] = "Vous utilisez la {0}. La mise à jour vérifie son SHA-256 et garde vos réglages.",
            ["tray.toast.install"] = "Télécharger et installer",
            ["tray.toast.notes"] = "Nouveautés",
            ["tray.toast.later"] = "Plus tard",
            ["settings.openFolder"] = "Ouvrir le dossier d'installation",
            ["settings.close"] = "Fermer",
            ["settings.noKeyPlaceholder"] = "(aucune clé configurée — accès libre local)",
            ["settings.portActive"] = "\u2713 Port {0} actif",
            ["settings.portUnavailable"] = "Port {0} indisponible",
            ["settings.regenDone"] = "\u2713 Nouvelle clé générée et copiée",
            ["settings.editHint"] = "Modifiez puis cliquez sur Enregistrer",
            ["settings.authDisabled"] = "Authentification désactivée",
            ["settings.keySaved"] = "\u2713 Clé API enregistrée",

            // ── Instance picker submenu & balloons ──
            ["tray.instance.loading"] = "(chargement…)",
            ["tray.instance.noneRunning"] = "(aucun TopSolid en cours d'exécution)",
            ["tray.instance.automationInactive"] = " (Automation inactif)",
            ["tray.instance.declaredRunning"] = " [actif]",
            ["tray.instance.declaredStopped"] = " [non démarré]",
            ["tray.instance.autoMode"] = "Automatique (premier TopSolid détecté)",
            ["tray.instance.manage"] = "Gérer les instances TopSolid…",
            ["tray.instance.selectedTitle"] = "Instance TopSolid sélectionnée",
            ["tray.instance.selectedBody"] = "Connecté à TopSolid sur le port {0}.",
            ["tray.instance.unavailableTitle"] = "TopSolid non disponible",
            ["tray.instance.unavailableBody"] = "Port {0} enregistré, mais aucun TopSolid ne répond sur ce port.",
            ["tray.instance.autoEnabledTitle"] = "Mode automatique activé",
            ["tray.instance.autoEnabledOk"] = "Connecté à TopSolid (port {0}).",
            ["tray.instance.autoEnabledWait"] = "Aucun TopSolid actif sur le port {0} — reconnexion automatique en attente.",

            // ── Manage TopSolid instances dialog ──
            ["manage.title"] = "Gérer les instances TopSolid",
            ["manage.header"] = "Instances TopSolid déclarées",
            ["manage.subtitle"] = "Définissez l'ordre d'affichage de vos sessions CAO et le port Automation par défaut",
            ["manage.cardList"] = "LISTE ORDONNÉE DES INSTANCES",
            ["manage.moveUp"] = "\u25B2  Monter",
            ["manage.moveDown"] = "\u25BC  Descendre",
            ["manage.default"] = "\u2605  Par défaut",
            ["manage.edit"] = "\u270E  Modifier",
            ["manage.remove"] = "\u2715  Supprimer",
            ["manage.addInstance"] = "AJOUTER UNE INSTANCE TOPSOLID",
            ["manage.editInstance"] = "MODIFIER L'INSTANCE #{0}",
            ["manage.nameLabel"] = "Nom de l'instance",
            ["manage.portLabel"] = "Port Automation",
            ["manage.add"] = "Ajouter",
            ["manage.save"] = "Enregistrer",
            ["manage.cancel"] = "Annuler",
            ["manage.autosaveHint"] = "Les changements sont enregistrés automatiquement.",
            ["manage.close"] = "Fermer",
            ["manage.emptyState"] = "Aucune instance déclarée (détection automatique active)",
            ["manage.defaultBadge"] = "\u2605 PAR DÉFAUT",
        };

        /// <summary>The language every <see cref="T"/> call resolves to ("fr" or "en").</summary>
        public static string Language
        {
            get { return Resolve(); }
        }

        /// <summary>Returns the localized string for the key, or the key itself when unknown.</summary>
        public static string T(string key)
        {
            return GetForLanguage(key, Resolve());
        }

        /// <summary>Returns the localized string with <see cref="string.Format"/> placeholders filled.</summary>
        public static string T(string key, params object[] args)
        {
            string value = GetForLanguage(key, Resolve());
            return (args == null || args.Length == 0) ? value : string.Format(value, args);
        }

        /// <summary>
        /// Maps a culture name to the UI language: "fr*" (fr-FR, fr-BE, fr-CA…)
        /// is French, everything else — including null/empty — is English.
        /// </summary>
        public static string DetectFromCulture(string cultureName)
        {
            return !string.IsNullOrEmpty(cultureName)
                && cultureName.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
                    ? LangFr
                    : LangEn;
        }

        /// <summary>
        /// Injects the "language" preference from settings.json ("auto"/"fr"/"en").
        /// "auto" and any unrecognized value fall through to culture detection.
        /// </summary>
        public static void SetConfiguredLanguage(string language)
        {
            lock (_lock)
            {
                _configuredLanguage = Normalize(language);
                _resolvedLanguage = null;
            }
        }

        /// <summary>Test hook: forces the language regardless of the environment.</summary>
        internal static void SetLanguageOverrideForTests(string language)
        {
            lock (_lock)
            {
                _forcedLanguage = Normalize(language);
                _resolvedLanguage = null;
            }
        }

        internal static ICollection<string> AllFrenchKeys()
        {
            lock (_lock) return new List<string>(Fr.Keys);
        }

        internal static bool HasEnglish(string key)
        {
            lock (_lock) return En.ContainsKey(key);
        }

        /// <summary>Looks the key up in the requested language, then English, then the key.</summary>
        internal static string GetForLanguage(string key, string language)
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";
            lock (_lock)
            {
                if (language == LangFr)
                {
                    string fr;
                    if (Fr.TryGetValue(key, out fr) && !string.IsNullOrEmpty(fr)) return fr;
                }
                string en;
                if (En.TryGetValue(key, out en) && !string.IsNullOrEmpty(en)) return en;
            }
            return key;
        }

        private static string Resolve()
        {
            lock (_lock)
            {
                if (_resolvedLanguage != null) return _resolvedLanguage;

                string lang = _forcedLanguage;
                if (lang == null)
                    lang = Normalize(Environment.GetEnvironmentVariable("TOPSOLID_MCP_LANG"));
                if (lang == null)
                    lang = Normalize(_configuredLanguage);
                if (lang == null)
                    lang = DetectFromCulture(CultureInfo.CurrentUICulture.Name);

                _resolvedLanguage = lang;
                return lang;
            }
        }

        /// <summary>"fr" → fr, "en" → en, anything else (null, "auto", garbage) → null.</summary>
        private static string Normalize(string language)
        {
            if (string.IsNullOrEmpty(language)) return null;
            language = language.Trim();
            if (string.Equals(language, LangFr, StringComparison.OrdinalIgnoreCase)) return LangFr;
            if (string.Equals(language, LangEn, StringComparison.OrdinalIgnoreCase)) return LangEn;
            return null;
        }
    }
}
