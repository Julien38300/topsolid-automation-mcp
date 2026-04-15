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
│ topsolid-      │             │ topsolid-             │
│ recipes (3B)   │             │ automation (14-24B)   │
│                │             │ (a venir — M-35)      │
│ run_recipe     │             │ execute_script        │
│ 113 recettes   │             │ api_help, find_path   │
└─────┬──────────┘             │ explore_paths         │
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
              │  RecipeTool       │  113 recettes
              │                   │
              │  7 outils MCP :   │
              │  - run_recipe     │
              │  - get_state      │
              │  - api_help       │
              │  - execute_script │
              │  - modify_script  │
              │  - find_path      │
              │  - explore_paths  │
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
| **topsolid-recipes** | 3B LoRA (ministral) | `run_recipe` | Selection de recette par nom | Faible (code pre-teste) |
| **topsolid-automation** | 14-24B (Mistral) | `execute_script` + `api_help` + `find_path` + `explore_paths` | Generation C# via graphe | Eleve (code genere) |

**Routing** : le Main (cloud) analyse l'intention. Si une recette couvre le besoin → 3B. Sinon → 14-24B qui navigue le graphe et genere un script C#.

**VRAM** (RTX 5090, 24 GB) : les deux modeles cohabitent (3B ~3 GB + 24B Q4 ~16 GB = ~19 GB).

**Benchmark prevu** (M-35) : Mistral Small 3.1 24B Q4, Codestral 22B Q4, et variante 14B Q8 pour comparer qualite C# / latence / VRAM.

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
| `server/src/Tools/RecipeTool.cs` | 113 recettes C# pre-construites | ~120 KB |
| `data/lora-dataset.jsonl` | 2161 paires LoRA (ShareGPT) | ~1.5 MB |
| `data/help-md/` | Aide en ligne convertie (FR+EN) | ~9 MB |
| `tests/TestSuite.json` | 72 tests automatises | ~50 KB |
