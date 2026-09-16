# Presentation

**TopSolid MCP** est un serveur [Model Context Protocol](https://modelcontextprotocol.io/) qui permet a un agent IA de piloter le logiciel CAO/PDM **TopSolid 7** via son API Automation.

## Comment ca marche

```
Agent IA (OpenClaw / Claude / tout client MCP)
  |
  v
TopSolidMcpServer.exe (stdio JSON-RPC)
  |  - run_recipe : execute une des 132 recettes pre-construites
  |  - list_recipes : catalogue des recettes (filtre categorie / mot-cle)
  |  - api_help : cherche les bonnes methodes API (72 synonymes FR/EN)
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
| Edges portant un champ `Examples` | 1193 (29%) |
| Snippets de code **dans le graphe redistribue** | 0 |

::: warning Les exemples de code ne sont pas redistribues
Les 1193 champs `Examples` du `graph.json` livre sont des tableaux **vides**. Les snippets viennent des corpora prives de l'auteur et ne sont pas redistribuables : ils sont retires avant publication. Ne comptez pas dessus, `topsolid_get_recipe` et `topsolid_search_examples` (corpus local, opt-in) sont les sources d'exemples reellement disponibles.
:::

### Serveur MCP (`TopSolidMcpServer.exe`)
Executable .NET Framework 4.8, communique en stdio JSON-RPC. **13 outils** exposes a l'agent.

### RecipeTool — 132 recettes
L'outil principal. Le LLM choisit une recette par nom, aucune generation de code necessaire.

Les categories ci-dessous sont celles exposees par `topsolid_list_recipes`. Les **comptes** de ce
tableau sont recopies a la main depuis `RecipeTool.cs` (etat verifie : 132 recettes) et derivent
des qu'une recette est ajoutee — la seule source a jour reste `topsolid_list_recipes` :

| Categorie | Recettes | Exemples |
|-----------|----------|----------|
| `GEOMETRY` | 16 | shapes, esquisses, extrusions, faces, operations |
| `AUDIT` | 13 | coherence des noms, drivers de famille, materiaux |
| `PROJECTS` | 11 | chercher, ouvrir, lister les documents du projet |
| `DRAFTING` | 10 | vues, echelle, format, qualite de projection, impression |
| `ATTRIBUTES` | 10 | couleur, transparence, calques, visibilite |
| `PDM PROPERTIES` | 9 | designation, nom, reference, fabricant |
| `BATCH` | 9 | designation/reference/fabricant en masse, export batch, virtuel |
| `DOCUMENT` | 8 | type, sauvegarde, reconstruction, revisions |
| `PARAMETERS` | 7 | lire, creer, modifier, comparer, copier |
| `BOM` | 7 | colonnes, contenu, lignes actives |
| `EXPORT` | 7 | STEP, STL, IGES, DXF, PDF |
| `ASSEMBLIES` | 6 | inclusions, occurrences, comptage de pieces |
| `FAMILIES` | 5 | detection, catalogue, drivers |
| `MEASUREMENTS` | 5 | masse, volume, surface, inertie, boite englobante |
| `MATERIALS` | 4 | lecture et audit des materiaux |
| `UNFOLDING` | 3 | detection, plis, dimensions de depliage |
| `USER PROPERTIES` | 2 | lecture et ecriture des proprietes utilisateur |

**Total : 132 recettes.**

### Dataset LoRA
2164 entrees d'entrainement au format ShareGPT (v7 conversational) pour fine-tuner le sous-agent 3B. Couvre les 132 recettes + patterns multi-turn + error-handling + acknowledgments. Eval : **96%** (50 questions, 5 tiers). Deploye en PROD comme `ministral-topsolid` via Ollama.

### Tests
Suite de tests automatises contre une instance TopSolid vivante. Scripts PowerShell executables en batch.

## Architecture Agent (OpenClaw)

```
OpenClaw Main (cloud, leger — routing + conversation)
  |
  ├── topsolid-recipes (3B LoRA, local)
  |     → topsolid_run_recipe
  |     132 recettes pre-construites
  |     Classification : intent → nom de recette
  |     Latence : ~2-4 secondes
  |
  └── codestral-topsolid (22B Q4_K_M vanilla, local — PROD)
        → execute_script + modify_script + api_help + find_path
           + explore_paths + compile + search_examples
        Generation C# via le graphe API + dry-run compile (CSharpCodeProvider, C# 5)
        Cas hors-recettes, scripts ad-hoc
        Latence : ~20-30 secondes
```

Le Main (cloud) garde la coherence conversationnelle et route les demandes :
- **Recette connue** (80% des cas) → sous-agent 3B, rapide et fiable
- **Cas custom** (20%) → Codestral 22B, generation de code via le graphe + `compile` avant execution

Le LoRA 3B v7 est en PROD (eval 96%, 2164 paires ShareGPT EN). Le fine-tuning LoRA 22B a ete tente mais abandonne (VRAM saturee, training > 9h30) — on shippe Codestral vanilla avec Modelfile enrichi (48 accessors `TopSolidHost.*` listes, Pattern D, SI-units, 6 few-shot examples).

## Independance & sources

`topsolid-automation-mcp` est un projet open-source (MIT) maintenu par la **communaute**. Il n'est ni endosse, ni sponsorise, ni affilie a TOPSOLID SAS. **TopSolid®** est une marque deposee de TOPSOLID SAS.

**Tout le contenu redistribue vient de sources publiques TopSolid** :

- Graphe API (4119 edges, 1728 methodes) : extrait par reflexion des DLL `TopSolid.*.Automating.dll` livrees avec chaque installation TopSolid, croise avec la reference API officielle sur [help.topsolid.com](https://help.topsolid.com/).
- Index de l'aide (5809 pages EN+FR) : converti en Markdown depuis l'aide en ligne publique [help.topsolid.com](https://help.topsolid.com/).
- Catalogue de commandes UI (2428 commandes) : parse depuis ces memes pages d'aide (fichiers `*Command.md`).
- Recettes (132 snippets C#) : ecrites specifiquement pour ce projet, en reference a l'aide publique + graphe.

**Aucun** exemple SDK proprietaire, code client, ou code prive identifie n'est inclus dans ce qui est distribue. L'acces aux corpora prives (`topsolid_search_examples`) est opt-in via des variables d'environnement pointant vers le disque local du contributeur — rien n'est bundle.

Le serveur reste autonome : n'importe quel client MCP compatible stdio (Claude Desktop, Claude Code, Cursor, Windsurf, OpenClaw...) peut s'y connecter. La specification fonctionnelle ne depend d'aucun produit tiers au-dela de TopSolid 7 lui-meme.
