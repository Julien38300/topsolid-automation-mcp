# Presentation

**TopSolid MCP** est un serveur [Model Context Protocol](https://modelcontextprotocol.io/) qui permet a un agent IA de piloter le logiciel CAO/PDM **TopSolid 7** via son API Automation.

## Comment ca marche

```
Agent IA (Hermes / LLM)
  |
  v
TopSolidMcpServer.exe (stdio JSON-RPC)
  |  - api_help : cherche les bonnes methodes API
  |  - execute_script : compile et execute du C# contre TopSolid
  |  - find_path : navigue dans le graphe de types
  |
  v
TopSolid 7 (WCF/TCP port 8090)
```

L'agent pose une question en langage naturel → le serveur MCP traduit en appels API → TopSolid execute.

## Composants

### Graphe API enrichi (`graph.json`)
Le coeur du systeme. Un graphe oriente de 4119 edges representant toutes les methodes de l'API TopSolid Automation :

| Metrique | Valeur |
|----------|--------|
| Edges | 4119 |
| Methodes uniques | 1728 |
| Interfaces | 46 |
| Description | 90% |
| Hints semantiques | 84% |
| Exemples reels (.cs) | 22% |

### Serveur MCP (`TopSolidMcpServer.exe`)
Executable .NET Framework 4.8, communique en stdio JSON-RPC. 7 outils exposes a l'agent (dont `run_recipe` pour les petits modeles 3B).

### Recettes (`recipes.md`)
68 scripts C# documentes, couvrant les scenarios les plus courants : navigation PDM, lecture/ecriture parametres, esquisses, assemblages, familles, export multi-format. Plus 10 recettes RecipeTool pour les modeles 3B.

### Tests (`TestSuite.json`)
72 tests automatises, executables contre une instance TopSolid vivante. 68/72 PASS.

### Integration Hermes Agent
Le serveur MCP est teste avec **Hermes** (agent Noemid) utilisant **ministral-3b** (3B parametres). Grace a `topsolid_run_recipe`, un modele 3B peut piloter TopSolid sans generer de code C#.

**Resultat e2e** : la commande "Change la designation en Piece Test Noemid" s'execute en **4 secondes** via Hermes + run_recipe.

## Projet Cortana / Noemid

TopSolid MCP fait partie du projet **Cortana** (prototype) qui migre vers **Noemid** (produit final). Le serveur MCP et le graphe API sont les composants qui survivent a la migration.

Les autres blocs Cortana (Dashboard, Launcher, RagService, BridgeMonitor) sont progressivement remplaces par l'ecosysteme Noemid.
