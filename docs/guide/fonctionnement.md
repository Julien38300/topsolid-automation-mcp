# Comment ca fonctionne

## La chaine complete

```
┌─────────────────────────────────────────────────────────┐
│  UTILISATEUR                                            │
│  "Change la designation en Ma Piece"                    │
└───────────────────────┬─────────────────────────────────┘
                        │
                        v
┌─────────────────────────────────────────────────────────┐
│  SOUS-AGENT RECETTES (3B LoRA, local)                   │
│                                                         │
│  Le LLM comprend l'intention en francais.               │
│  Il selectionne la recette par nom.                     │
│  Il appelle run_recipe("modifier_designation",          │
│                         value="Ma Piece")               │
│                                                         │
│  Pas de code genere. Juste un choix de recette.         │
└───────────────────────┬─────────────────────────────────┘
                        │ stdio JSON-RPC
                        v
┌─────────────────────────────────────────────────────────┐
│  MCP SERVER (.NET 4.8)                                  │
│                                                         │
│  RecipeTool recoit "modifier_designation" + "Ma Piece"  │
│  → injecte la valeur dans le code C# pre-ecrit          │
│  → compile avec CSharpCodeProvider                      │
│  → execute dans le process TopSolid via WCF             │
│                                                         │
│  Code execute (le LLM ne voit jamais ca) :              │
│  ┌───────────────────────────────────────┐              │
│  │ PdmObjectId pdmId = ...GetPdmObject();│              │
│  │ Pdm.SetDescription(pdmId, "Ma Piece");│              │
│  │ Pdm.Save(pdmId, true);               │              │
│  └───────────────────────────────────────┘              │
└───────────────────────┬─────────────────────────────────┘
                        │ WCF/TCP :8090
                        v
┌─────────────────────────────────────────────────────────┐
│  TOPSOLID 7                                             │
│                                                         │
│  La designation de la piece change.                     │
│  Le document est sauvegarde.                            │
│  Resultat : "OK: Designation modifiee → Ma Piece"       │
└─────────────────────────────────────────────────────────┘
```

**Temps total : 4 secondes.**

## Les 12 outils MCP

```
┌─────────────────────────────────────────────────────────┐
│                   MCP SERVER                            │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ get_state   │  │ api_help     │  │ run_recipe    │  │
│  │             │  │              │  │               │  │
│  │ Etat de     │  │ Cherche dans │  │ 124 recettes  │  │
│  │ TopSolid    │  │ 1728 methodes│  │ pre-codees    │  │
│  │ connexion   │  │ 52 synonymes │  │ Le LLM choisit│  │
│  │ document    │  │ FR/EN        │  │ par nom       │  │
│  └─────────────┘  └──────┬───────┘  └───────────────┘  │
│                          │                              │
│                   ┌──────v───────┐                      │
│                   │ GRAPHE API   │                      │
│                   │ graph.json   │                      │
│                   │ 4119 edges   │                      │
│                   │ 84% hints FR │                      │
│                   └──────────────┘                      │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │find_path    │  │execute_script│  │ modify_script │  │
│  │             │  │              │  │               │  │
│  │ Dijkstra    │  │ Code C# libre│  │ Code C# +    │  │
│  │ entre types │  │ (lecture)    │  │ auto-save     │  │
│  │ API         │  │              │  │ (ecriture)    │  │
│  └─────────────┘  └──────────────┘  └───────────────┘  │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │explore_paths│  │ get_recipe   │  │ compile       │  │
│  │ BFS multi-  │  │ Code C# d'une│  │ Dry-run Roslyn│  │
│  │ chemins     │  │ recette      │  │ (sans TopSol.)│  │
│  └─────────────┘  └──────────────┘  └───────────────┘  │
│                                                         │
│  ┌─────────────────┐  ┌───────────┐  ┌───────────────┐ │
│  │ search_examples │  │ whats_new │  │ search_help   │ │
│  │ corpora prives  │  │ API diff  │  │ FTS5 sur 5809 │ │
│  │ (local only)    │  │ par ver.  │  │ pages d'aide  │ │
│  └─────────────────┘  └───────────┘  └───────────────┘ │
└─────────────────────────────────────────────────────────┘
```

## 2 modes d'utilisation

### Sous-agent Recettes (3B LoRA) — actuel

Le sous-agent 3B + LoRA selectionne la bonne recette par nom. Pas de generation de code :

```
User : "Lis la designation"
  → LLM choisit : run_recipe("lire_designation")
  → MCP execute le code pre-ecrit
  → Resultat : "Designation: Ma Piece"
```

**Avantage** : fiable, rapide, zero hallucination.
**Limite** : ne peut faire que ce qui est pre-programme dans RecipeTool.

### Sous-agent Automation (14-24B) — prevu M-35

Le sous-agent 14-24B (Mistral Small ou Codestral) navigue le graphe API et genere du C# :

```
User : "Cree un point a 50mm, 100mm, 0"
  → LLM appelle api_help("point 3D")
  → api_help retourne : IGeometries3D.CreatePoint(docId, Point3D)
  → LLM compose le script C# avec les bonnes methodes
  → execute_script compile et execute
  → TopSolid cree le point
```

**Avantage** : peut faire n'importe quoi, meme des operations non prevues.
**Limite** : necessite un modele capable de generer du C# correct.

### Les 2 modes coexistent

Le RecipeTool couvre les **cas courants** (80% des demandes).
L'execute_script couvre les **cas avances** (20%).

A mesure qu'on ajoute des recettes, le mode 3B couvre de plus en plus de cas.

## Role de chaque brique

| Brique | Ce qu'elle fait | Pourquoi c'est necessaire |
|--------|----------------|--------------------------|
| **Graphe** (graph.json) | Stocke les 4119 methodes API avec descriptions, hints FR, exemples | C'est la "memoire" de l'API. Sans lui, impossible de chercher les bonnes methodes. |
| **api_help** | Cherche dans le graphe par mot-cle FR ou EN | Permet au LLM de trouver "comment faire X" sans connaitre l'API par coeur. |
| **find_path** | Trouve le chemin entre 2 types API (Dijkstra) | Pour les cas complexes : "comment passer de DocumentId a une Face ?" |
| **run_recipe** | Execute une recette pre-codee par nom | La solution pour les petits modeles : zero code a generer. |
| **execute_script** | Compile et execute du C# libre | La puissance brute : peut tout faire si le code est correct. |
| **modify_script** | Comme execute_script + auto-wrap modification/save | Pour les ecritures : gere StartModification/EndModification/Save. |
| **RecipeTool** | 124 recettes pre-construites en C# | La reference pour les humains et les LLM : comment faire chaque operation. |
| **Skill** (system.md) | Instructions pour chaque sous-agent : outils autorises et routing | Chaque agent a son propre system.md dans OpenClaw. |
| **Glossaire FR** | Mapping termes TopSolid FR → API EN | "Designation" → SetDescription, "mise au coffre" → CheckIn, etc. |

## Donnees du graphe

```
graph.json
├── 4119 edges (methodes API)
│   ├── MethodName : "GetDescription"
│   ├── MethodSignature : "string GetDescription(PdmObjectId)"
│   ├── Interface : "IPdm"
│   ├── Description : "Gets the description of a PDM object"  (90%)
│   ├── SemanticHint : "designation, description"              (84%)
│   ├── Weight : 2 (priorite)
│   ├── Since : "v7.6" (version minimum)
│   └── Examples : ["// snippet.cs ..."]                       (22%)
│
├── 242 nodes (types CLR)
│   ├── DocumentId, ElementId, PdmObjectId...
│   └── Point3D, Frame3D, Plane3D...
│
└── 46 interfaces
    ├── IPdm (139 methodes) — gestion documentaire
    ├── IParameters (161 methodes) — parametres
    ├── IDocuments (64 methodes) — documents
    └── ... (voir Reference > Interfaces)
```
