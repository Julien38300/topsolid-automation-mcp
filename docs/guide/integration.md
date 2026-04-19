# Integration avec un client MCP

TopSolid MCP est un serveur [Model Context Protocol](https://modelcontextprotocol.io/) standard qui communique via stdin/stdout. Il fonctionne avec tous les clients IA compatibles MCP.

## Prerequis

1. **TopSolid 7.15+** ouvert (teste sur 7.20)
2. **TopSolidMcpServer.exe** telecharge depuis la [release GitHub](https://github.com/julien38300/noemid-topsolid-automation/releases)
3. Decompresser le .zip dans un dossier, par exemple `C:\TopSolidMCP\`

::: warning Verification rapide
Ouvrez un terminal PowerShell dans le dossier de l'exe et tapez :
```powershell
.\TopSolidMcpServer.exe
```
Vous devez voir :
```
[MCP-INFO] TopSolid MCP Server starting...
[MCP-INFO] Server ready. Listening on stdin.
```
Fermez avec `Ctrl+C`. Si vous voyez `Graph data not found`, placez `graph.json` dans un sous-dossier `data\` a cote de l'exe.
:::

## Clients compatibles

| Client | Support MCP stdio | Difficulte |
|--------|-------------------|------------|
| **Claude Desktop** | Oui | Facile |
| **Claude Code** (terminal) | Oui | Facile |
| **Cursor** | Oui | Facile |
| **Windsurf** | Oui | Facile |
| **VS Code + GitHub Copilot** | Oui (mode Agent) | Moyen |
| **JetBrains + AI Assistant** | Oui | Moyen |
| **Antigravity / Cline / Roo Code** | Oui | Moyen |
| **Continue** | Oui | Moyen |
| **OpenClaw** | Via sous-agent | Avance |
| **ChatGPT Desktop** | Non | - |
| **Copilot standalone** | Non | - |

---

## Claude Desktop

**Ou se trouve le fichier de config :**
```
%APPDATA%\Claude\claude_desktop_config.json
```
Soit en clair : `C:\Users\VOTRE_NOM\AppData\Roaming\Claude\claude_desktop_config.json`

**Comment y acceder facilement :**
1. Ouvrez Claude Desktop
2. Cliquez le **menu hamburger** (3 barres, en haut a gauche)
3. **Settings**
4. **Developer** (dans la colonne de gauche)
5. **Edit Config** (le bouton ouvre le fichier dans votre editeur)

**Contenu a mettre :**

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

::: tip Si le fichier contient deja du contenu
Ne remplacez pas tout ! Ajoutez juste `"topsolid": {...}` dans le bloc `mcpServers` existant :
```json
{
  "mcpServers": {
    "mon-autre-serveur": { "..." },
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

**Apres modification :**
- **Quittez completement** Claude Desktop (clic droit sur l'icone dans la zone de notification Windows > **Quit**)
- Relancez Claude Desktop
- Une icone **marteau** apparait en bas a droite du champ de saisie = le serveur est connecte

---

## Claude Code (terminal)

Claude Code utilise un fichier de configuration projet ou global.

**Option 1 — Configuration projet** (recommande) :

Creez un fichier `.mcp.json` a la racine de votre projet :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

**Option 2 — Configuration globale :**

Ajoutez dans `~/.claude/settings.json` :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

Relancez Claude Code. Les outils TopSolid apparaissent automatiquement.

---

## Cursor

**Option A — Via l'interface (le plus simple) :**
1. Ouvrez les Settings de Cursor (`Ctrl+,`)
2. Cherchez **MCP** dans la barre de recherche
3. Cliquez **Add new MCP server**
4. Remplissez :
   - **Name** : `topsolid`
   - **Type** : `stdio`
   - **Command** : `C:\TopSolidMCP\TopSolidMcpServer.exe`

**Option B — Via fichier** (pour partager la config) :

Creez `%USERPROFILE%\.cursor\mcp.json` (configuration globale) :
```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

Ou `.cursor/mcp.json` a la racine du projet (configuration projet).

Redemarrez Cursor. Les outils apparaissent dans le mode **Agent** du chat.

---

## Windsurf

**Option A — Via la palette de commandes :**
1. `Ctrl+Shift+P`
2. Tapez `Windsurf: Configure MCP Servers`
3. Un fichier JSON s'ouvre, ajoutez :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

**Option B — Fichier direct :**

Le fichier se trouve dans :
```
%USERPROFILE%\.codeium\windsurf\mcp_config.json
```

Creez le fichier et les dossiers parents s'ils n'existent pas.

Redemarrez Windsurf.

---

## VS Code + GitHub Copilot

GitHub Copilot supporte les serveurs MCP depuis VS Code 1.99+ (avril 2025), en mode **Agent** uniquement.

**Etape 1 — Activez MCP dans VS Code :**
1. Ouvrez les Settings (`Ctrl+,`)
2. Cherchez `mcp enabled`
3. Cochez **Chat > Mcp: Enabled**

**Etape 2 — Ajoutez le serveur :**

**Option A — Configuration projet** (recommande, se partage via git) :

Creez `.vscode/mcp.json` a la racine du projet :
```json
{
  "servers": {
    "topsolid": {
      "type": "stdio",
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

**Option B — settings.json global :**

Ouvrez `%APPDATA%\Code\User\settings.json` et ajoutez :
```json
{
  "mcp": {
    "servers": {
      "topsolid": {
        "type": "stdio",
        "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
      }
    }
  }
}
```

::: warning Copilot Chat en mode Agent uniquement
Les outils MCP apparaissent dans **Copilot Chat** (panneau lateral), pas dans l'autocompletion inline.
En haut du panneau Chat, selectionnez le mode **Agent** (pas "Edit" ni "Ask").
:::

---

## JetBrains (IntelliJ, Rider, WebStorm...)

MCP est supporte via le plugin **AI Assistant** integre (depuis 2025.1).

1. **File** > **Settings** (`Ctrl+Alt+S`)
2. **Tools** > **AI Assistant** > **Model Context Protocol (MCP)**
3. Cliquez le bouton **+** (Add)
4. Choisissez **As JSON**
5. Collez :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

6. Cliquez **OK** et redemarrez l'IDE

::: tip Import automatique depuis Claude Desktop
Si vous avez deja configure Claude Desktop, cliquez **Import from Claude Desktop** dans l'ecran MCP de JetBrains — il reprend automatiquement vos serveurs.
:::

---

## Antigravity / Cline / Roo Code

Ces extensions VS Code ont leur **propre** gestion MCP, independante de VS Code.

### Antigravity (Gemini Code Assist)

1. Ouvrez le panneau Antigravity dans VS Code (icone dans la barre laterale)
2. Cliquez l'icone **engrenage** en haut du panneau
3. Section **MCP Servers**
4. Cliquez **Edit MCP Settings** — un fichier JSON s'ouvre

**Si vous n'avez aucun serveur MCP configure :**

Le fichier sera vide ou contiendra `{}`. Remplacez par :
```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "disabled": false
    }
  }
}
```

**Si vous avez deja des serveurs MCP :**

Ajoutez `"topsolid"` dans le bloc `mcpServers` existant, **sans supprimer les autres** :
```json
{
  "mcpServers": {
    "mon-serveur-existant": { "command": "...", "disabled": false },
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "disabled": false
    }
  }
}
```

Sauvegardez (`Ctrl+S`). L'extension detecte le changement automatiquement.
Verifiez dans le panneau MCP (icone prise electrique) que les 12 outils TopSolid apparaissent.

### Cline / Roo Code

Meme principe : **Settings** > **MCP Servers** > **Edit Config**, puis ajoutez le bloc `"topsolid"` comme ci-dessus.

---

## Continue (VS Code / JetBrains)

Continue est une extension open-source compatible MCP. Editez `~/.continue/config.json` :

```json
{
  "experimental": {
    "modelContextProtocolServers": [
      {
        "transport": {
          "type": "stdio",
          "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
        }
      }
    ]
  }
}
```

---

## ChatGPT Desktop

::: danger Non supporte
ChatGPT Desktop **ne supporte pas** les serveurs MCP locaux en stdio (avril 2026).
OpenAI travaille sur un support MCP via leur plateforme [mcp.run](https://mcp.run), mais c'est en beta fermee et uniquement pour les serveurs heberges dans le cloud — pas les executables locaux.

**Alternative :** utilisez Claude Desktop, Cursor, ou Claude Code.
:::

---

## Copilot standalone (app desktop)

::: danger Non supporte
L'application Copilot standalone (anciennement Bing Chat) ne supporte pas les serveurs MCP.
Pour utiliser MCP avec Copilot, passez par **VS Code + Copilot Chat** (voir section ci-dessus).
:::

---

## OpenClaw

Configuration avancee pour le framework multi-agents OpenClaw :

Le sous-agent TopSolid se configure dans `~/.openclaw/agents/topsolid/agent/system.md`.
Le serveur MCP est lance automatiquement par OpenClaw via stdio. Chaque sous-agent a son propre workspace isole et ses outils MCP autorises (tool scoping via `openclaw.json`).

---

## Client generique

Tout logiciel supportant le protocole MCP stdio peut utiliser le serveur.
- **Commande** : chemin vers `TopSolidMcpServer.exe`
- **Transport** : stdio (JSON-RPC 2.0 sur stdin/stdout)
- **Arguments** : aucun

---

## Outils disponibles

Une fois connecte, votre assistant IA dispose de **12 outils** :

| Outil | Description |
|-------|-------------|
| `topsolid_get_state` | Etat de connexion, document actif, projet courant |
| `topsolid_run_recipe` | Execute une des 124 recettes pre-construites |
| `topsolid_api_help` | Recherche dans 1728 methodes API (52 synonymes FR) |
| `topsolid_execute_script` | Compile et execute du C# contre TopSolid (lecture seule) |
| `topsolid_modify_script` | Compile et execute du C# (modification avec transaction) |
| `topsolid_find_path` | Chemin Dijkstra entre types API |
| `topsolid_explore_paths` | Exploration BFS multi-chemins |

::: tip Pour la plupart des usages
`topsolid_run_recipe` suffit. Les 124 recettes couvrent PDM, parametres, export, assemblages, familles, mise en plan, nomenclature, audit et bien plus. Demandez simplement a votre assistant ce que vous voulez faire en francais.
:::

---

## Troubleshooting

### `Graph data not found at expected locations`
Le fichier `graph.json` n'est pas au bon endroit. Le serveur cherche dans cet ordre :
1. `data\graph.json` (sous-dossier a cote de l'exe)
2. `graph.json` (a la racine, a cote de l'exe)
3. En remontant 3 niveaux (mode developpement)

### `Another TopSolidMcpServer instance is already running`
Le serveur est un singleton — une seule instance peut tourner a la fois.
Fermez l'autre client IA qui utilise le serveur, ou forcez l'arret :
```powershell
Get-Process TopSolidMcpServer -ErrorAction SilentlyContinue | Stop-Process
```

### `Connect() retourne false`
C'est **normal** sur TopSolid v7.20 (bug connu REDACTED). Le serveur verifie la connexion via `TopSolidHost.Version > 0` a la place. Si les outils fonctionnent, tout va bien.

### Les outils n'apparaissent pas dans mon client
1. Verifiez que **TopSolid est ouvert** avant de lancer votre client IA
2. Verifiez le **chemin vers l'exe** (pas de guillemets manquants, doubles backslashs `\\` dans le JSON)
3. **Quittez completement** votre client IA et relancez (pas juste fermer la fenetre)
4. Verifiez les **logs** de votre client pour des erreurs MCP
