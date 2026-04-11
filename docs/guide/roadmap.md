# Roadmap

## Progression

| Phase | Description | Avancement |
|-------|-------------|------------|
| Phase 1 | Fondations (extraction, graphe, pathfinding) | 100% |
| Phase 2 | Serveur MCP (protocole, 7 outils) | 100% |
| Phase 3 | Intelligence semantique (regles, pruning) | 100% |
| Phase 4 | Connexion TopSolid & execution scripts | 100% |
| Phase 5 | Connaissance API (graphe enrichi, api_help, 52 synonymes) | 100% |
| Phase 5b-e | Robustesse, qualite, fixes (66/66 tests) | 100% |
| Phase 6a-d | Recettes Tier 1/2, tests, graphe complet | 100% |
| Phase 6e | Recettes Tier 3 (mise en plan, BOM, tolerie) — M-58 | 100% |
| Phase 6f | Tests LIVE RecipeTool (59/61 PASS, 7 bugs corriges) — M-61 | 100% |
| Phase 6g | Recettes avancees (comparaison, report, batch, audit) | 100% |
| Phase 6h | Graphe enrichi (+54 methodes VB.NET, IBoms, IDraftings) | 100% |
| Phase 6i | Dataset LoRA v3 (732 paires) — M-33 prep | 100% |
| Phase 7 | Graphe multi-couche (commands, ADS) | 0% |
| Phase 8 | Outils metier & securite | 0% |
| Phase 9 | LoRA fine-tuning (Noemid) — M-33/M-34 | 0% |
| Phase 10 | Test & validation complete | 80% |

## Chiffres cles (2026-04-11)

- **4119 edges** dans le graphe API
- **1728 methodes** uniques couvertes
- **1194 edges** avec exemples reels (2174 snippets, 29%)
- **85%** d'edges avec hints semantiques FR/EN
- **113 recettes** RecipeTool — tests LIVE sur TopSolid vivant
- **7 outils MCP** (run_recipe, get_state, execute_script, modify_script, api_help, find_path, explore_paths)
- **732 paires** dataset LoRA (format ShareGPT/Axolotl)
- **5/5 exports** testes LIVE (STEP, STL, IGES, DXF, PDF)
- **5809 pages** d'aide en ligne converties en MD
- **46 interfaces** TopSolid couvertes (Kernel + Design + Drafting)

## Prochaines etapes

| Priorite | Mission | Description |
|----------|---------|-------------|
| 1 | M-33 | Fine-tuning LoRA sur ministral-3:3b avec le dataset 732 paires |
| 2 | M-34 | Evaluation du modele fine-tune (taux selection recettes) |
| 3 | — | Guide integration (Claude Desktop, Hermes, OpenClaw, generique) |
| 4 | — | Troubleshooting guide (Connect()=false, Mutex, port 8090) |
| 5 | M-30 | Graphe multi-couche : parser Commands TopSolid |
| 6 | M-31 | Graphe multi-couche : parser ADS |
| 7 | — | Tests LIVE des recettes contextuelles (ouvrir piece, plan, nomenclature, famille) |
| 8 | — | Recettes creation (esquisse, extrusion, inclusion) |
