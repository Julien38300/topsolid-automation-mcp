# Integration avec un client MCP

TopSolid MCP est un serveur [Model Context Protocol](https://modelcontextprotocol.io/) standard qui communique via stdin/stdout. Il fonctionne avec tous les clients IA compatibles MCP.

## Clients compatibles

| Client | Support MCP | Configuration |
|--------|-------------|---------------|
| **ChatGPT Desktop** | Natif (beta) | Settings > Beta > MCP |
| **Claude Desktop** | Natif | claude_desktop_config.json |
| **Claude Code** | Natif | .claude/settings.json |
| **VS Code + Copilot** | Natif (v1.99+) | settings.json |
| **Cursor** | Natif | Settings > MCP Servers |
| **Windsurf** | Natif | Settings > MCP |
| **Continue** | Natif | config.json |
| **Antigravity** | Natif | .gemini/settings.json |
| **Hermes** | Via skill | ~/.hermes/skills/ |

## ChatGPT Desktop (Windows)

1. Ouvrir ChatGPT Desktop
2. **Settings > Beta features > Model Context Protocol** (activer)
3. Ajouter un serveur : commande = chemin vers `TopSolidMcpServer.exe`
4. Relancer ChatGPT Desktop

Les outils TopSolid apparaitront dans la conversation.

## Claude Desktop

Editer `%APPDATA%\Claude\claude_desktop_config.json` :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "args": []
    }
  }
}
```

Relancer Claude Desktop. Les 7 outils apparaitront.

## Claude Code (terminal)

Ajouter dans `.claude/settings.json` (projet ou global `~/.claude/settings.json`) :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "args": []
    }
  }
}
```

## VS Code + GitHub Copilot

VS Code 1.99+ avec GitHub Copilot supporte les serveurs MCP en mode Agent.

Dans `settings.json` de VS Code (`Ctrl+,` > icone fichier en haut a droite) :

```json
{
  "github.copilot.chat.mcp.servers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

Utilisez ensuite le mode **Agent** dans Copilot Chat (`@workspace` ou `/`) pour acceder aux outils TopSolid.

## Cursor

1. **Settings > MCP Servers > Add Server**
2. Name : `topsolid`
3. Command : `C:\TopSolidMCP\TopSolidMcpServer.exe`
4. Type : `stdio`

Les outils TopSolid seront disponibles dans le chat Agent de Cursor.

## Windsurf

1. **Settings > MCP** (ou fichier `~/.windsurf/mcp.json`)
2. Ajouter :

```json
{
  "servers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

## Continue (VS Code / JetBrains)

Continue est une extension open-source compatible MCP. Dans `~/.continue/config.json` :

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

## Antigravity (Gemini)

Ajouter dans la configuration MCP d'Antigravity :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

## Hermes (WSL2)

Le skill topsolid-mcp se configure dans `~/.hermes/skills/topsolid-mcp/SKILL.md`.
Le serveur MCP est lance automatiquement par Hermes via stdio.

## Client generique

Tout logiciel supportant le protocole MCP stdio peut utiliser le serveur.
La commande est simplement le chemin vers `TopSolidMcpServer.exe`.
Le serveur communique en JSON-RPC 2.0 sur stdin/stdout.

## Outils disponibles

Une fois connecte, votre assistant IA dispose de 7 outils :

| Outil | Description |
|-------|-------------|
| `topsolid_get_state` | Etat de connexion, document actif, projet courant |
| `topsolid_run_recipe` | Execute une des 112 recettes pre-construites |
| `topsolid_api_help` | Recherche dans 1728 methodes API |
| `topsolid_execute_script` | Compile et execute du C# contre TopSolid (lecture) |
| `topsolid_modify_script` | Compile et execute du C# (modification) |
| `topsolid_find_path` | Chemin Dijkstra entre types API |
| `topsolid_explore_paths` | Exploration BFS multi-chemins |

::: tip Pour la plupart des usages
`topsolid_run_recipe` suffit. Les 112 recettes couvrent PDM, parametres, export, assemblages, familles, mise en plan, nomenclature, audit et bien plus. Demandez simplement a votre assistant ce que vous voulez faire en francais.
:::
