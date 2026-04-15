# Noemid TopSolid — Regles Projet

## Structure
Ce repo contient le serveur MCP et le graphe API pour piloter TopSolid 7 par IA.

```
noemid-topsolid/
├── server/        — TopSolid MCP Server (.NET 4.8, stdio JSON-RPC)
├── graph/         — TopSolid API Graph builder (.NET 4.8)
├── plugin/        — TopSolid Plugin (WCF bridge)
├── scripts/       — Python scripts (enrich-graph, convert-help)
├── data/          — graph.json, api-index.json, recipes.md
├── tests/         — 72 tests automatises
├── docs/          — Site VitePress (documentation)
├── research/      — Roadmap, missions, decisions
└── skills/        — YAML skills for OpenClaw
```

## Contribuer
Voir le guide [CONTRIBUTING.md](CONTRIBUTING.md) pour ajouter des recettes ou modifier le serveur.

## Language
- Code : **anglais** (noms, variables, commentaires inline)
- Descriptions MCP / UI : **francais**
- Messages utilisateur : **francais**
- XML doc comments : **anglais**

## Tech Stack
- **.NET Framework 4.8** / C# 7.3 max (compatibilite TopSolid)
- **Newtonsoft.Json** pour JSON-RPC
- **VitePress** pour la documentation
- **Python 3.11+** pour les scripts d'enrichissement

## Conventions
- Un fichier = une classe (blocs .NET)
- Logs sur stderr (stdout = protocole MCP)
- Pas de secrets commites
- graph.json charge une seule fois au demarrage

## Glossaire TopSolid FR
- Designation = Description (pas Name)
- Reference = PartNumber
- Mise au coffre = CheckIn
- Sorti de coffre = CheckOut
- Mise a plat = Unfolding (pas depliage)
- Rafale = batch generation depuis nomenclature

## Relation avec Noemid
Ce repo est le composant TopSolid de l'ecosysteme Noemid.
L'agent TopSolid se connecte via OpenClaw (sous-agent dedie avec system.md + tool scoping).
