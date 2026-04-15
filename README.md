# TopSolid MCP

Serveur MCP (Model Context Protocol) et Graphe API enrichi pour piloter **TopSolid 7** par intelligence artificielle.

> TopSolid est une marque deposee d'[Allplan](https://www.allplan.com/) (anciennement Missler Software). Ce projet est independant et n'est ni affilie ni endosse par Allplan.

## Composants

| Composant | Description |
|-----------|-------------|
| **server/** | Serveur MCP .NET 4.8 — 7 outils JSON-RPC via stdio |
| **graph/** | Constructeur du graphe API (extraction par reflexion DLL) |
| **plugin/** | Plugin TopSolid (bridge WCF/TCP port 8090) |
| **data/** | Graphe enrichi (4119 edges), 113 recettes, api-index (1728 methodes) |
| **scripts/** | Scripts Python — enrichissement graphe, conversion aide en ligne |
| **tests/** | Tests automatises contre TopSolid vivant |
| **docs/** | Site de documentation VitePress |

## Outils MCP

| Outil | Fonction |
|-------|----------|
| `topsolid_run_recipe` | Execute une des 113 recettes pre-construites |
| `topsolid_get_state` | Document actif, projet, connexion |
| `topsolid_api_help` | Recherche dans 1728 methodes (52 synonymes FR) |
| `topsolid_execute_script` | Compile et execute C# contre TopSolid |
| `topsolid_modify_script` | Idem pour les modifications (auto-wrap) |
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

Site complet : [https://julien38300.github.io/noemid-topsolid-automation/](https://julien38300.github.io/noemid-topsolid-automation/)

## Sources

- API Automation TopSolid : [help.topsolid.com](https://help.topsolid.com/7.20/en/TopSolid'Automation/) (documentation publique officielle)

## Contribuer

Pour ajouter une nouvelle recette ou ameliorer le serveur, consultez le guide [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

Projet par Julien
