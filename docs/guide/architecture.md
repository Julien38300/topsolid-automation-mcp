# Architecture

## Vue d'ensemble

```
                    +-------------------+
                    |   Agent IA        |
                    |   (Hermes/LLM)    |
                    +--------+----------+
                             |
                        stdio JSON-RPC
                             |
                    +--------v----------+
                    | TopSolidMcpServer |
                    |  (.NET 4.8, C#7.3)|
                    |                   |
                    |  +-------------+  |
                    |  | TypeGraph   |  |  graph.json (4119 edges)
                    |  | KeywordIdx  |  |  api-index.json (1462 methods)
                    |  | Recipes     |  |  recipes.md (68 recettes)
                    |  +-------------+  |
                    |                   |
                    |  5 outils MCP :   |
                    |  - api_help       |
                    |  - execute_script |
                    |  - modify_script  |
                    |  - find_path      |
                    |  - explore_paths  |
                    |  - get_state      |
                    +--------+----------+
                             |
                        WCF/TCP :8090
                             |
                    +--------v----------+
                    |   TopSolid 7      |
                    |   (Automation)    |
                    +-------------------+
```

## Connexion TopSolid

Le serveur se connecte a TopSolid via WCF/TCP sur le port 8090 (natif).

::: warning
`TopSolidHost.Connect()` retourne toujours `false` en v7.20 — c'est un bug connu. Verifier la connexion via `TopSolidHost.Version > 0`.
:::

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
| `data/api-index.json` | Index plat des 1462 methodes | ~400 KB |
| `data/recipes.md` | 68 recettes C# documentees | ~80 KB |
| `data/help-md/` | Aide en ligne convertie (FR+EN) | ~9 MB |
| `tests/TestSuite.json` | 72 tests automatises | ~50 KB |
