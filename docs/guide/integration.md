# Integration avec un client MCP

TopSolid MCP supporte deux modes de connexion. Le bridge HTTP/SSE est recommande pour la majorite des cas.

## Deux modes de connexion

| | **Bridge HTTP/SSE** (recommande) | **Stdio direct** (alternatif) |
|---|---|---|
| Demarrage | `.\start-bridge.ps1` une fois | Automatique par le client |
| Config client | `"url": "http://127.0.0.1:8080/mcp"` | `"command": "C:\\...\\TopSolidMcpServer.exe"` |
| Multi-clients simultanes | Oui | Non (singleton) |
| claude.ai web / app | Oui (via tunnel) | Non |
| Node.js requis | Oui (18+) | Non |

**Pour demarrer le bridge :**
```powershell
cd C:\TopSolidMCP\bridge
npm install        # premiere fois
.\start-bridge.ps1
```
Laissez le terminal ouvert. Le bridge reste actif jusqu'a ce que vous le fermiez.

---

## Clients compatibles

| Client | Stdio | HTTP/SSE bridge | Notes |
|--------|-------|-----------------|-------|
| **Claude Desktop** | Oui | Oui | |
| **Claude Code** (terminal) | Oui | Oui | |
| **Cursor** | Oui | Oui | |
| **Windsurf** | Oui | Oui | |
| **VS Code + GitHub Copilot** | Oui | Oui | Mode Agent uniquement |
| **JetBrains + AI Assistant** | Oui | Oui | |
| **Antigravity / Cline / Roo Code** | Oui | Oui | |
| **Continue** | Oui | Oui | |
| **OpenClaw** | Via sous-agent | Via URL | Config `openclaw.json` |
| **claude.ai** (web + app) | Non | Oui (tunnel requis) | Voir [bridge-http](./bridge-http) |
| **ChatGPT Desktop** | Non | Non | Pas de support MCP local |

---

## Claude Desktop

**Fichier de config :**
```
%APPDATA%\Claude\claude_desktop_config.json
```
Acces : Claude Desktop → menu hamburger → Settings → Developer → Edit Config.

::: code-group
```json [Via bridge (recommande)]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Via stdio]
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

Quittez completement Claude Desktop (clic droit icone notification > Quit) et relancez. L'icone marteau en bas du champ de saisie confirme la connexion.

---

## Claude Code (terminal)

::: code-group
```powershell [Via bridge (recommande)]
claude mcp add --transport http topsolid http://127.0.0.1:8080/mcp
claude mcp list   # verification
```
```powershell [Via stdio]
claude mcp add topsolid C:\TopSolidMCP\TopSolidMcpServer.exe
```
:::

Ou en fichier `.mcp.json` a la racine du projet :

::: code-group
```json [Via bridge]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Via stdio]
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

---

## Cursor

**Option A — Via l'interface :**
1. Settings (`Ctrl+,`) → MCP → Add new MCP server
2. Remplissez :
   - **Name** : `topsolid`
   - **Type** : `http` (ou `sse` pour la compat legacy)
   - **URL** : `http://127.0.0.1:8080/mcp`

**Option B — Via fichier** `%USERPROFILE%\.cursor\mcp.json` :

::: code-group
```json [Via bridge (recommande)]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Via stdio]
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

---

## Windsurf

`Ctrl+Shift+P` → `Windsurf: Configure MCP Servers`, ou editez directement `%USERPROFILE%\.codeium\windsurf\mcp_config.json` :

::: code-group
```json [Via bridge (recommande)]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Via stdio]
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

---

## VS Code + GitHub Copilot

GitHub Copilot supporte MCP depuis VS Code 1.99+ (avril 2025), en mode **Agent** uniquement.

**Etape 1 :** Settings (`Ctrl+,`) → cherchez `mcp enabled` → cochez **Chat > Mcp: Enabled**

**Etape 2 :** Creez `.vscode/mcp.json` a la racine du projet :

::: code-group
```json [Via bridge (recommande)]
{
  "servers": {
    "topsolid": {
      "type": "http",
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Via stdio]
{
  "servers": {
    "topsolid": {
      "type": "stdio",
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

::: warning Copilot Chat en mode Agent uniquement
Selectionnez le mode **Agent** en haut du panneau Chat (pas "Edit" ni "Ask").
:::

---

## JetBrains (IntelliJ, Rider, WebStorm...)

**File** > **Settings** > **Tools** > **AI Assistant** > **Model Context Protocol (MCP)** > **+** > **As JSON** :

::: code-group
```json [Via bridge (recommande)]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Via stdio]
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```
:::

::: tip Import depuis Claude Desktop
Si vous avez deja configure Claude Desktop, cliquez **Import from Claude Desktop** pour importer automatiquement.
:::

---

## Antigravity / Cline / Roo Code

Ces extensions VS Code ont leur propre gestion MCP. Ouvrez leur panel → Settings → MCP Servers → Edit Config :

::: code-group
```json [Via bridge (recommande)]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp",
      "disabled": false
    }
  }
}
```
```json [Via stdio]
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "disabled": false
    }
  }
}
```
:::

Sauvegardez (`Ctrl+S`). L'extension detecte le changement automatiquement. Verifiez que les outils TopSolid apparaissent dans le panneau MCP.

---

## Continue (VS Code / JetBrains)

Editez `~/.continue/config.json` :

::: code-group
```json [Via bridge (recommande)]
{
  "experimental": {
    "modelContextProtocolServers": [
      {
        "transport": {
          "type": "http",
          "url": "http://127.0.0.1:8080/mcp"
        }
      }
    ]
  }
}
```
```json [Via stdio]
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
:::

---

## OpenClaw

Configuration pour le framework multi-agents OpenClaw.

Dans `~/.openclaw/openclaw.json`, le serveur MCP TopSolid peut etre configure en HTTP ou stdio :

::: code-group
```json [Via bridge (recommande)]
{
  "agents": {
    "topsolid": {
      "mcp": {
        "topsolid": {
          "url": "http://127.0.0.1:8080/mcp"
        }
      }
    }
  }
}
```
```json [Via stdio]
{
  "agents": {
    "topsolid": {
      "mcp": {
        "topsolid": {
          "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
        }
      }
    }
  }
}
```
:::

Le sous-agent TopSolid est configure dans `~/.openclaw/agents/topsolid/agent/system.md`. Le skill et les tool permissions ne dependent pas du mode de transport.

---

## claude.ai (web + app Windows)

claude.ai accepte uniquement des serveurs MCP distants via URL. Le bridge HTTP/SSE, expose via un tunnel, permet cette connexion.

Voir le **[guide complet Bridge HTTP/SSE](./bridge-http)** pour l'installation pas-a-pas, la securite (Cloudflare Access) et le troubleshooting.

Etapes resumees :
1. Demarrez le bridge (`.\start-bridge.ps1`)
2. Dans un second terminal : `cloudflared tunnel --url http://127.0.0.1:8080`
3. Copiez l'URL `https://<random>.trycloudflare.com`
4. claude.ai → Settings → Connecteurs → Ajouter → `https://<random>.trycloudflare.com/mcp`

---

## ChatGPT Desktop

::: danger Non supporte
ChatGPT Desktop ne supporte pas les serveurs MCP locaux (mai 2026). Utilisez Claude Desktop, Cursor ou Claude Code.
:::

---

## Outils disponibles

Une fois connecte, votre assistant IA dispose des outils suivants :

| Outil | Description |
|-------|-------------|
| `topsolid_get_state` | Etat de connexion, document actif, projet courant |
| `topsolid_run_recipe` | Execute une des 132 recettes pre-construites |
| `topsolid_get_recipe` | Retourne le code source C# d'une recette |
| `topsolid_api_help` | Recherche dans l'API TopSolid (1728 methodes, synonymes FR) |
| `topsolid_execute_script` | Compile et execute du C# contre TopSolid (lecture seule) |
| `topsolid_modify_script` | Compile et execute du C# avec transaction (Pattern D auto) |
| `topsolid_compile` | Compile-check Roslyn d'un script sans l'executer |
| `topsolid_find_path` | Chemin Dijkstra entre types dans le graphe API |
| `topsolid_explore_paths` | Exploration BFS multi-chemins |
| `topsolid_search_help` | FTS5 sur 5809 pages de l'aide en ligne TopSolid |
| `topsolid_search_commands` | Recherche dans 2428 commandes UI TopSolid (Layer 2) |
| `topsolid_search_examples` | Recherche dans les corpora prives locaux (opt-in, env var) |
| `topsolid_whats_new` | Diff API entre deux versions TopSolid |

::: tip Pour la plupart des usages
`topsolid_run_recipe` suffit. Les 132 recettes couvrent PDM, parametres, export, assemblages, familles, mise en plan, nomenclature et plus. Demandez simplement en francais.
:::

---

## Troubleshooting

### `Graph data not found at expected locations`
Le fichier `graph.json` n'est pas au bon endroit. Le serveur cherche dans :
1. `data\graph.json` (sous-dossier a cote de l'exe)
2. `graph.json` (a cote de l'exe)
3. En remontant 3 niveaux (mode developpement)

### `Another TopSolidMcpServer instance is already running`
Le serveur est un singleton (un seul processus). Avec le bridge, un seul processus sert tous les clients — ce message ne doit pas apparaitre.
En mode stdio avec plusieurs clients, forcez l'arret :
```powershell
Get-Process TopSolidMcpServer -ErrorAction SilentlyContinue | Stop-Process
```

### `Connect() retourne false`
Normal dans TopSolid v7.20 (bug connu). Le serveur verifie via `TopSolidHost.Version > 0` a la place. Si les outils fonctionnent, tout va bien.

### Les outils n'apparaissent pas
1. Verifiez que **TopSolid est ouvert** avant de lancer le client IA
2. En mode bridge : verifiez que `.\start-bridge.ps1` tourne dans un terminal
3. En mode stdio : verifiez le **chemin vers l'exe** (doubles backslashs `\\`)
4. **Quittez completement** le client IA et relancez (pas juste fermer la fenetre)
