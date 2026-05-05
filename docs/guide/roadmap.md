# Roadmap

## Progression

| Phase | Description | Avancement |
|-------|-------------|------------|
| Phase 1 | Fondations (extraction, graphe, pathfinding) | 100% |
| Phase 2 | Serveur MCP (protocole, outils de base) | 100% |
| Phase 3 | Intelligence semantique (regles, pruning) | 100% |
| Phase 4 | Connexion TopSolid & execution scripts | 100% |
| Phase 5 | Connaissance API (graphe enrichi, api_help, 52 synonymes) | 100% |
| Phase 5b-e | Robustesse, qualite, fixes (66/66 tests) | 100% |
| Phase 6 | 129 recettes Tier 1/2/3 + tests LIVE + graphe complet | 100% |
| Phase 7 | Graphe multi-couche — Layer 2 Commands (2428 commandes) | 50% |
| Phase 8 | Outils metier batch PDM (list/modify/info) | 100% |
| Phase 9a | LoRA 3B v7 (96% eval, PROD) | 100% |
| Phase 9b | Codestral 22B vanilla + enhanced Modelfile (PROD) | 100% |
| Phase 10 | Test & validation complete | 85% |
| Phase 11 | Bridge HTTP/SSE (mcp-proxy, Streamable HTTP + SSE legacy) | 100% |

## Chiffres cles (2026-05-05, v1.6.6)

- **4119 edges** dans le graphe API
- **1728 methodes** uniques couvertes, **242 nodes**, **46 interfaces**
- **1194 edges** avec exemples reels (2174 snippets, 29%)
- **85%** d'edges avec hints semantiques FR/EN, **90%** avec description
- **129 recettes** RecipeTool (Tier 1/2/3 — PDM, parametres, export, assemblages, familles, mise en plan, BOM, creation, proprietes utilisateur)
- **2428 commandes UI** indexees en Layer 2 (outil `topsolid_search_commands`)
- **17 outils MCP** : run_recipe, get_state, execute_script, modify_script, api_help, find_path, explore_paths, get_recipe, compile, search_examples, whats_new, search_help, search_commands, list_documents, list_elements, modify_documents, get_document_info
- **2164 paires** dataset LoRA v7 (ShareGPT EN, eval 96%)
- **5809 pages** d'aide en ligne indexees en SQLite FTS5 (~20 MB embedded)
- `ministral-topsolid:latest` (3B LoRA v7, 96%) + `codestral:22b` (vanilla, 97.5% Pattern D)
- **Bridge HTTP/SSE** integre (`bridge/`) — Streamable HTTP + SSE legacy, expose le serveur stdio via `mcp-proxy`

## Prochaines etapes

| Priorite | Mission | Description |
|----------|---------|-------------|
| 1 | M-31 | Graphe multi-couche Layer 3 — ADS (documentation non publique, a differer) |
| 2 | M-21 | Securite : confirmation utilisateur avant operations destructives, rollback PDM |
| 3 | M-36 | Benchmark latence / tokens / taux de succes (3B vs 22B, recettes vs scripts) |
| 4 | M-35b | Skill automation Codestral (instructions graphe + patterns C# pour modeles 14-24B) |
| 5 | — | Tier 1 Gaps : PDM Navigation (S-001→S-013), Documents (S-021→S-031), Parametres CRUD (S-040→S-059) |
