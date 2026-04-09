# Noemid TopSolid

Serveur MCP (Model Context Protocol) et Graphe API enrichi pour piloter **TopSolid 7** par intelligence artificielle.

## Composants

| Composant | Description |
|-----------|-------------|
| **server/** | Serveur MCP .NET 4.8 — 6 outils JSON-RPC via stdio |
| **graph/** | Constructeur du graphe API (extraction par reflexion DLL) |
| **plugin/** | Plugin TopSolid (bridge WCF/TCP port 8090) |
| **data/** | Graphe enrichi (4119 edges), recettes (68), api-index (1462 methodes) |
| **scripts/** | Scripts Python — enrichissement graphe, conversion aide en ligne |
| **tests/** | 72 tests automatises (68 PASS) |
| **docs/** | Site de documentation VitePress |

## Outils MCP

| Outil | Fonction |
|-------|----------|
| `topsolid_get_state` | Document actif, projet, connexion |
| `topsolid_api_help` | Recherche dans 1728 methodes (52 synonymes FR) |
| `topsolid_execute_script` | Compile et execute C# contre TopSolid |
| `topsolid_modify_script` | Idem mais pour les modifications (auto-wrap) |
| `topsolid_find_path` | Chemin Dijkstra entre types API |
| `topsolid_explore_paths` | BFS multi-chemins |

## Quick Start

```bash
# Documentation
cd docs && npm install && npm run dev

# Build serveur
cd server && dotnet build TopSolidMcpServer.sln

# Enrichir le graphe
cd scripts && python enrich-graph.py
```

## Documentation

Site complet : [https://jup.github.io/noemid-topsolid/](https://jup.github.io/noemid-topsolid/)

## Licence

Projet prive — Julien / Noemid
