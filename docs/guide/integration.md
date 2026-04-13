# Integration avec un client MCP

TopSolid MCP est un serveur MCP stdio standard. Il fonctionne avec tout client qui supporte le protocole [Model Context Protocol](https://modelcontextprotocol.io/) via stdin/stdout.

## Claude Code

Ajouter dans le fichier `.claude/settings.json` du projet (ou `~/.claude/settings.json` global) :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\chemin\\vers\\TopSolidMcpServer.exe",
      "args": []
    }
  }
}
```

Relancer Claude Code. Les 7 outils TopSolid apparaitront automatiquement.

## Antigravity

Ajouter dans la configuration MCP d'Antigravity (`.gemini/settings.json` ou equivalent) :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\chemin\\vers\\TopSolidMcpServer.exe"
    }
  }
}
```

## Claude Desktop

Ajouter dans `claude_desktop_config.json` :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\chemin\\vers\\TopSolidMcpServer.exe",
      "args": []
    }
  }
}
```

Le fichier se trouve dans :
- **Windows** : `%APPDATA%\Claude\claude_desktop_config.json`

## Hermes (WSL2)

Le skill topsolid-mcp se configure dans `~/.hermes/skills/topsolid-mcp/SKILL.md`.
Le serveur MCP est lance automatiquement par Hermes via stdio.

## Client MCP generique

Tout client supportant le protocole MCP stdio peut utiliser le serveur.
La commande a configurer est simplement le chemin vers `TopSolidMcpServer.exe`.
Le serveur communique en JSON-RPC 2.0 sur stdin/stdout.

## Outils disponibles

| Outil | Description |
|-------|-------------|
| `topsolid_get_state` | Etat de connexion, document actif |
| `topsolid_run_recipe` | Execute une des 112 recettes pre-construites |
| `topsolid_api_help` | Recherche dans 1728 methodes API |
| `topsolid_execute_script` | Compile et execute du C# contre TopSolid (lecture) |
| `topsolid_modify_script` | Compile et execute du C# (modification) |
| `topsolid_find_path` | Chemin Dijkstra entre types API |
| `topsolid_explore_paths` | Exploration BFS multi-chemins |

::: tip Quel outil utiliser ?
Pour la plupart des usages, `topsolid_run_recipe` suffit. Les 112 recettes couvrent PDM, parametres, export, assemblages, familles, mise en plan, nomenclature, audit et bien plus. `execute_script` est reserve aux cas avances necessitant du C# custom.
:::
