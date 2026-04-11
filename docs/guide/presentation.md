# Presentation

**TopSolid MCP** est un serveur [Model Context Protocol](https://modelcontextprotocol.io/) qui permet a un agent IA de piloter le logiciel CAO/PDM **TopSolid 7** via son API Automation.

## Comment ca marche

```
Agent IA (Hermes / Claude / OpenClaw / tout client MCP)
  |
  v
TopSolidMcpServer.exe (stdio JSON-RPC)
  |  - run_recipe : execute une des 113 recettes pre-construites
  |  - api_help : cherche les bonnes methodes API (52 synonymes FR)
  |  - execute_script : compile et execute du C# contre TopSolid
  |  - find_path / explore_paths : navigue dans le graphe de types
  |
  v
TopSolid 7 (WCF/TCP port 8090)
```

L'agent pose une question en langage naturel &rarr; le serveur MCP traduit en appels API &rarr; TopSolid execute.

## Composants

### Graphe API enrichi (`graph.json`)
Le coeur du systeme. Un graphe oriente representant toutes les methodes de l'API TopSolid Automation :

| Metrique | Valeur |
|----------|--------|
| Edges | 4119 |
| Methodes uniques | 1728 |
| Interfaces | 46 |
| Description | 90% |
| Hints semantiques | 85% |
| Edges avec exemples reels | 1194 (29%) |
| Snippets de code | 2174 |

### Serveur MCP (`TopSolidMcpServer.exe`)
Executable .NET Framework 4.8, communique en stdio JSON-RPC. **7 outils** exposes a l'agent.

### RecipeTool — 113 recettes
L'outil principal. Le LLM choisit une recette par nom, aucune generation de code necessaire.

| Categorie | Recettes | Exemples |
|-----------|----------|----------|
| PDM (lecture/ecriture) | 9 | designation, reference, fabricant |
| Navigation projet | 5 | chercher, ouvrir, lister documents |
| Parametres | 6 | lire, modifier, comparer |
| Masse/Volume/Dimensions | 7 | masse, volume, surface, inertie, boite englobante |
| Geometrie/Visualisation | 8 | shapes, esquisses, operations, couleurs |
| Assemblages | 6 | inclusions, occurrences, comptage pieces |
| Export | 8 | STEP, DXF, PDF, STL, IGES, CSV |
| Mise en plan | 6 | vues, echelle, format, projection, ouvrir plan |
| Nomenclature (BOM) | 4 | colonnes, contenu, comptage lignes |
| Mise a plat / Tolerie | 3 | detection, plis, dimensions depliage |
| Comparaison documents | 4 | parametres, operations, entites, revisions |
| Report modifications | 2 | copier parametres ou proprietes PDM vers un autre doc |
| Batch projet | 13 | audit refs/desig, masse batch, export batch, auteur, virtuel |
| Audit qualite | 6 | noms parametres, drivers famille, materiaux |
| Familles | 5 | detection, catalogue, drivers |
| Historique/Revisions | 2 | timeline revisions, comparaison revisions |
| Document | 7 | type, sauvegarder, reconstruire, proprietes utilisateur |
| Interactif | 3 | selection shape/face/point dans TopSolid |

### Dataset LoRA (`lora-dataset.jsonl`)
732 paires d'entrainement au format ShareGPT pour fine-tuner un modele 3B sur la selection de recettes.

### Tests
Suite de tests automatises contre une instance TopSolid vivante. Scripts PowerShell executables en batch.

## Architecture Hermes (agent)

```
ministral 8B (main agent — conversation, raisonnement)
  |
  v
ministral 3B (sous-agent TopSolid — selection de recettes)
  |
  v
TopSolidMcpServer.exe (MCP stdio)
  |
  v
TopSolid 7 (WCF port 8090)
```

Le modele 8B garde la coherence conversationnelle. Le modele 3B execute les recettes via `run_recipe`. Le LoRA (futur) cible le 3B pour ameliorer sa connaissance TopSolid.

## Projet Cortana / Noemid

TopSolid MCP fait partie du projet **Cortana** (prototype) qui migre vers **Noemid** (produit final). Le serveur MCP et le graphe API sont les composants qui survivent a la migration.
