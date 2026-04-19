# Roadmap

## Progression

| Phase | Description | Avancement |
|-------|-------------|------------|
| Phase 1 | Fondations (extraction, graphe, pathfinding) | 100% |
| Phase 2 | Serveur MCP (protocole, 12 outils) | 100% |
| Phase 3 | Intelligence semantique (regles, pruning) | 100% |
| Phase 4 | Connexion TopSolid & execution scripts | 100% |
| Phase 5 | Connaissance API (graphe enrichi, api_help, 52 synonymes) | 100% |
| Phase 5b-e | Robustesse, qualite, fixes (66/66 tests) | 100% |
| Phase 6a-d | Recettes Tier 1/2, tests, graphe complet | 100% |
| Phase 6a-d | Recettes Tier 1/2 + tests + graphe complet | 100% |
| Phase 6e | Recettes Tier 3 avancees (creation, Smart params, familles) | 100% |
| Phase 6f | Tests LIVE RecipeTool | 100% |
| Phase 6g-h | Graphe enrichi (+54 methodes VB.NET, IBoms, IDraftings) | 100% |
| Phase 7 | Graphe multi-couche (commands, ADS) | 0% |
| Phase 8 | Outils metier & securite | 0% |
| Phase 9a | LoRA 3B v6 conversational (100/100 eval, PROD) | 100% |
| Phase 9b | Codestral 22B vanilla + enhanced Modelfile (shipped Option 1) | 100% |
| Phase 10 | Test & validation complete | 85% |
| Phase 11 | Internationalisation (MCP EN, dico 69K paires, multilingue avance) | 10% |
| M-57 | RAG aide en ligne (SQLite FTS5, 5809 pages, tool search_help) — v1.6.0 | 100% |
| M-58 | Tier 3 write recipes (drafting/BOM : scale, format, print, BOM rows) — v1.6.1 | 80% |
| M-62 | Pipeline auto-sync CHM (extract/diff/propose/report par release) | 100% |
| M-70 | Guide "MCP as knowledge base for standalone C# dev" | 100% |
| M-71 | 4 nouveaux outils (get_recipe, compile, search_examples, whats_new) | 100% |
| M-72 | Corpus AF/REDACTED indexes method-level (225 snippets) | 100% |

## Chiffres cles (2026-04-19, v1.6.1)

- **4119 edges** dans le graphe API
- **1728 methodes** uniques couvertes, **242 nodes**, **46 interfaces**
- **1194 edges** avec exemples reels (2174 snippets, 29%)
- **85%** d'edges avec hints semantiques FR/EN, **90%** avec description
- **124 recettes** RecipeTool (Tier 1/2/3, comparaison, audit, drafting/BOM write v1.6.1)
- **12 outils MCP** : run_recipe, get_state, execute_script, modify_script, api_help, find_path, explore_paths, get_recipe (v1.5.0), compile (v1.5.1), search_examples (v1.5.2), whats_new (v1.5.2), search_help (v1.6.0)
- **2114 entrees** dataset LoRA v6 conversational (ShareGPT EN, eval 100/100)
- **5809 pages** d'aide en ligne indexees en SQLite FTS5 (~20 MB embedded)
- **225+ snippets** production AF/REDACTED method-level
- `ministral-topsolid` (3B v6 PROD) + `codestral-topsolid` (22B vanilla + Modelfile PROD)

## Prochaines etapes

| Priorite | Mission | Description |
|----------|---------|-------------|
| 1 | M-58 | Completer les recettes Tier 3 restantes (creation drafting, user props, occurrence) |
| 2 | M-59 | Documenter exporteurs et options (batch, formats) |
| 3 | M-60 | Recettes proprietes utilisateur + occurrence |
| 4 | M-61 | **Publier LoRA sur HuggingFace** (`ministral-topsolid` GGUF + Modelfile + SKILL.md) |
| 5 | M-36 | Benchmark latence/tokens/taux de succes (3B vs 22B) |
| 6 | — | Tier 2 avance (S-087 DerivePartForModification, S-088 substitution) |
| 7 | M-30/31 | Graphe multi-couche (Commands, ADS) |
| 8 | Phase 11 | Multilingue avance : agent repond dans la langue de l'utilisateur (dico 69K paires) |
