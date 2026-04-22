# Presentation

**TopSolid MCP** est un serveur [Model Context Protocol](https://modelcontextprotocol.io/) qui permet a un agent IA de piloter le logiciel CAO/PDM **TopSolid 7** via son API Automation.

## Comment ca marche

```
Agent IA (OpenClaw / Claude / tout client MCP)
  |
  v
TopSolidMcpServer.exe (stdio JSON-RPC)
  |  - run_recipe : execute une des 124 recettes pre-construites
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
Executable .NET Framework 4.8, communique en stdio JSON-RPC. **12 outils** exposes a l'agent.

### RecipeTool — 124 recettes
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

### Dataset LoRA (`lora-dataset-en.jsonl`)
2114 entrees d'entrainement au format ShareGPT (v6 conversational) pour fine-tuner le sous-agent 3B. Couvre les 124 recettes + patterns multi-turn + error-handling + acknowledgments. Eval : **100/100** (50 questions, 5 tiers). Deploye en PROD comme `ministral-topsolid` via Ollama.

### Tests
Suite de tests automatises contre une instance TopSolid vivante. Scripts PowerShell executables en batch.

## Architecture Agent (OpenClaw)

```
OpenClaw Main (cloud, leger — routing + conversation)
  |
  ├── topsolid-recipes (3B LoRA, local)
  |     → topsolid_run_recipe
  |     124 recettes pre-construites
  |     Classification : intent → nom de recette
  |     Latence : ~2-4 secondes
  |
  └── codestral-topsolid (22B Q4_K_M vanilla, local — PROD)
        → execute_script + modify_script + api_help + find_path
           + explore_paths + compile + search_examples
        Generation C# via le graphe API + validation Roslyn
        Cas hors-recettes, scripts ad-hoc
        Latence : ~20-30 secondes
```

Le Main (cloud) garde la coherence conversationnelle et route les demandes :
- **Recette connue** (80% des cas) → sous-agent 3B, rapide et fiable
- **Cas custom** (20%) → Codestral 22B, generation de code via le graphe + `compile` avant execution

Le LoRA 3B v6 est en PROD (eval 100/100). Le fine-tuning LoRA 22B a ete tente mais abandonne (VRAM saturee, training > 9h30) — on shippe Codestral vanilla avec Modelfile enrichi (48 accessors `TopSolidHost.*` listes, Pattern D, SI-units, 6 few-shot examples).

## Independance

`topsolid-automation-mcp` est un projet open-source (MIT) maintenu par la **communaute**. Il n'est ni endosse, ni sponsorise, ni affilie a TOPSOLID SAS. **TopSolid®** est une marque deposee de TOPSOLID SAS.

Le serveur reste autonome : il n'a pas besoin de l'ecosysteme d'agents qui l'a fait naitre. N'importe quel client MCP compatible stdio (Claude Desktop, Claude Code, Cursor, Windsurf, OpenClaw, etc.) peut s'y connecter, et la specification fonctionnelle ne depend d'aucun produit tiers au-dela de TopSolid 7 lui-meme.
