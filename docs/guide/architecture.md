# Architecture

## Vue d'ensemble

```
      ┌────────────────────────────┐
      │  OpenClaw Main (cloud)     │
      │  Routing + conversation    │
      └────────┬───────────────────┘
               │
      ┌────────┴────────────────────────────┐
      │                                     │
┌─────v──────────┐             ┌────────────v──────────┐
│ topsolid-      │             │ codestral-topsolid    │
│ recipes (3B)   │             │ (22B Q4_K_M, vanilla) │
│ v6 conv. PROD  │             │ enhanced Modelfile    │
│ run_recipe     │             │ execute_script        │
│ 124 recettes   │             │ api_help, find_path   │
└─────┬──────────┘             │ explore_paths, compile│
      │                        └────────────┬──────────┘
      │                                     │
      └────────────────┬────────────────────┘
                       │ stdio JSON-RPC
              ┌────────v──────────┐
              │ TopSolidMcpServer │
              │  (.NET 4.8, C#7.3)│
              │                   │
              │  TypeGraph        │  graph.json (4119 edges)
              │  KeywordIdx       │  api-index.json (1728 methods)
              │  RecipeTool       │  124 recettes
              │                   │
              │  12 outils MCP :   │
              │  - run_recipe     │
              │  - get_state      │
              │  - api_help       │
              │  - execute_script │
              │  - modify_script  │
              │  - find_path      │
              │  - explore_paths  │
              │  - get_recipe     │
              │  - compile        │
              │  - search_examples│
              │  - whats_new      │
              │  - search_help    │
              └────────┬──────────┘
                       │ WCF/TCP :8090
              ┌────────v──────────┐
              │   TopSolid 7      │
              │   (Automation)    │
              └───────────────────┘
```

## Connexion TopSolid

Le serveur se connecte a TopSolid via WCF/TCP sur le port 8090 (natif).

::: warning
`TopSolidHost.Connect()` retourne toujours `false` en v7.20 — c'est un bug connu. Verifier la connexion via `TopSolidHost.Version > 0`.
:::

## Architecture Agent (DEC-011)

Deux sous-agents specialises dans OpenClaw, chacun avec ses propres outils MCP :

| Sous-agent | Modele | Outils MCP | Tache | Risque |
|------------|--------|------------|-------|--------|
| **topsolid-recipes** | 3B LoRA v6 conversational (ministral-topsolid, PROD) | `run_recipe` | Selection de recette par nom, multi-turn, error-handling | Faible (code pre-teste) |
| **codestral-topsolid** | Codestral 22B Q4_K_M vanilla + Modelfile enrichi | `execute_script` + `modify_script` + `api_help` + `find_path` + `explore_paths` + `compile` + `search_examples` | Generation C# via graphe pour cas custom | Eleve (code genere, a valider par `compile`) |

**Routing** : le Main (cloud) analyse l'intention. Si une recette couvre le besoin → 3B. Sinon → Codestral 22B qui navigue le graphe, genere du C#, et le valide via `compile` avant execution.

**VRAM** (RTX 5090, 24 GB) : modeles mutuellement exclusifs (un seul charge a la fois) — 3B ~2 GB OU 22B Q4 ~13 GB, swap Ollama ~5-10s. Cohabitation avec Main 14B (~9 GB) OK dans les deux cas.

**Codestral training** : le fine-tuning LoRA 22B a ete tente mais abandonne (VRAM saturee a 98%, training > 9h30 pour 61% → Option 1 retenue = vanilla + prompt engineering, Pattern D 100%, SI-units 97%, clean rate ~55%).

## Graphe API

Le graphe est un **graphe oriente** ou :
- **Noeuds** = types CLR (DocumentId, ElementId, PdmObjectId, etc.)
- **Edges** = methodes API (GetDocument, CreateInclusion, etc.)

Chaque edge porte :
- `MethodName` + `MethodSignature` : la methode
- `Interface` : IPdm, IDocuments, IParameters...
- `Description` : documentation officielle
- `SemanticHint` : mots-cles FR/EN pour la recherche
- `Weight` : priorite (1 = important, 10 = primitif, 20+ = niche)
- `Examples` : snippets C# reels (source: REDACTED-USER + Romain)

## Compilation de scripts

`execute_script` et `modify_script` compilent du C# 5 a la volee via `CSharpCodeProvider`. Le code est wrappe automatiquement :

```csharp
// Header auto-injecte
using TopSolid.Kernel.Automating;
// ...

public class Script {
  public static string Run() {
    // <-- votre code ici
  }
}
```

Pour les scripts de modification, un wrapper supplementaire gere `StartModification` / `EndModification` / `Save`.

## Donnees

| Fichier | Role | Taille |
|---------|------|--------|
| `data/graph.json` | Graphe API enrichi | ~2.9 MB |
| `data/api-index.json` | Index plat des 1728 methodes | ~400 KB |
| `server/src/Tools/RecipeTool.cs` | 124 recettes C# pre-construites | ~130 KB |
| `data/lora-dataset-en.jsonl` | 2114 entrees LoRA v6 (ShareGPT EN) | ~1.5 MB |
| `data/help-md/` | Aide en ligne convertie (FR+EN) | ~9 MB |
| `data/help.db` | Index SQLite FTS5 de l'aide (5809 pages) | ~20 MB |
| `tests/TestSuite.json` | 72 tests automatises | ~50 KB |
