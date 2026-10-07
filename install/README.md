# Installation TopSolid MCP — guide noob

**Tout se fait en double-clic. Aucune ligne de commande à taper, aucun Node.js à installer.**

## Installation en 3 clics

1. **Double-clic sur `Installer_TopSolidMCP.bat`**
   - **Génère la clé API automatiquement** (48 caractères aléatoires, affichée et copiée dans le presse-papiers) — rien à inventer ; une clé existante est conservée
   - Nettoie toute ancienne installation héritée (tâche planifiée Node `TopSolidMcpBridge`, tâche tray PS1 `TopSolidMcpTray`, raccourci Startup bridge) qui bloquerait le port 8080
   - Crée la tâche planifiée `TopSolidMcpServer` : au démarrage du PC, le serveur natif repart tout seul (`--http-standalone`, via `conhost.exe --headless` : aucune fenêtre console, les logs vont dans `%LOCALAPPDATA%\TopSolidMcp\logs\server.log`)

2. **Cherche l'icône près de l'horloge** (systray) : c'est TopSolid MCP.

3. **C'est tout.** Le serveur tourne sur le port 8080, authentifié par clé API.

## L'icône systray

Clic droit sur l'icône :

| Menu | Effet |
|---|---|
| **Statut** | État du serveur (process, port, connexion TopSolid) |
| **Redémarrer** | Relance le serveur |
| **Arrêter** | Coupe le serveur |
| **Paramètres → Clé API...** | Affiche la clé actuelle (pré-remplie) |
| **Paramètres → Régénérer la clé API** | Révoque immédiatement l'ancienne clé, en génère une nouvelle et la copie dans le presse-papiers |
| **Quitter** | Arrête le serveur + l'icône |

## Comportement automatique

- **Au démarrage du PC** : le serveur natif repart tout seul (tâche planifiée `TopSolidMcpServer`)
- **Clé API** : stockée chiffrée via DPAPI dans `settings.json` à côté de l'exécutable — jamais en clair sur le disque ; l'ancienne variable d'environnement `TOPSOLID_MCP_API_KEY` reste acceptée comme source de migration
- **Connexion TopSolid** : reconnexion automatique (backoff 15/30/60 s) si TopSolid 7 redémarre
- **Après une mise à jour** (`update.ps1`) : le serveur est relancé tout seul

## URL à donner à votre assistant IA

```
http://127.0.0.1:8080/mcp
```

avec l'en-tête d'authentification `X-API-Key: <votre clé>` (la clé est dans le presse-papiers à l'installation, ou via le menu Paramètres de l'icône).

## Questions fréquentes

**L'icône n'apparaît pas ?**
Le systray masque parfois les icônes : clic sur le chevron `^` près de l'horloge, glisse l'icône TopSolid MCP vers la barre visible.

**Clé API perdue ou compromise ?**
Clic droit sur l'icône → **Paramètres → Clé API...** : la clé actuelle s'affiche pré-remplie. Compromise ? **Paramètres → Régénérer la clé API** révoque l'ancienne et en copie une nouvelle dans le presse-papiers. La nouvelle clé s'applique immédiatement (relecture à chaque requête, sans redémarrer le serveur).

**« Le port 8080 est déjà pris » / le serveur démarre en stdio seul ?**
Une ancienne installation (bridge Node v1.7) occupe encore le port : relance `Installer_TopSolidMCP.bat`, son étape de nettoyage supprime précisément ces process et tâches planifiées (elle ne tue pas vos autres logiciels Node).

**Comment tout supprimer ?**
Clic droit sur l'icône → **Quitter**, puis supprime la tâche planifiée `TopSolidMcpServer` (`schtasks /Delete /TN TopSolidMcpServer /F`) et le dossier d'installation.
