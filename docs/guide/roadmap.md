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
| Phase 6e | Recettes Tier 3 (mise en plan, BOM, tolerie) — M-58 | 100% |
| Phase 6f | Tests LIVE RecipeTool (59/61 PASS, 7 bugs corriges) — M-61 | 100% |
| Phase 6g | Recettes avancees (comparaison, report, batch, audit) | 100% |
| Phase 6h | Graphe enrichi (+54 methodes VB.NET, IBoms, IDraftings) | 100% |
| Phase 6i | Dataset LoRA v3 (732 paires) — M-33 prep | 100% |
| Phase 7 | Graphe multi-couche (commands, ADS) | 0% |
| Phase 8 | Outils metier & securite | 0% |
| Phase 9a | LoRA recettes (M-33 dataset + M-34 training 3B) | 50% |
| Phase 9b | Benchmark automation (M-35 — 14B/24B/Codestral) — DEC-011 | 0% |
| Phase 10 | Test & validation complete | 80% |

## Chiffres cles (2026-04-15)

- **4119 edges** dans le graphe API
- **1728 methodes** uniques couvertes
- **1194 edges** avec exemples reels (2174 snippets, 29%)
- **85%** d'edges avec hints semantiques FR/EN
- **124 recettes** RecipeTool — tests LIVE sur TopSolid vivant
- **12 outils MCP** (run_recipe, get_state, execute_script, modify_script, api_help, find_path, explore_paths)
- **2161 paires** dataset LoRA (format ShareGPT, 112 recettes + domaine)
- **5/5 exports** testes LIVE (STEP, STL, IGES, DXF, PDF)
- **5809 pages** d'aide en ligne converties en MD
- **46 interfaces** TopSolid couvertes (Kernel + Design + Drafting)

## Prochaines etapes

| Priorite | Mission | Description |
|----------|---------|-------------|
| 1 | M-34a-c | Training + Eval LoRA 3B recettes (Unsloth, QLoRA 4-bit) |
| 2 | M-35 | Benchmark modeles automation (14B Q8, 24B Q4, Codestral 22B) — DEC-011 |
| 3 | M-35b | Skill automation (instructions graphe + patterns C# pour 14-24B) |
| 4 | M-35c | Dataset LoRA automation (si benchmark justifie fine-tune) |
| 5 | — | Guide integration (Claude Desktop, OpenClaw, generique) |
| 6 | — | Troubleshooting guide (Connect()=false, Mutex, port 8090) |
| 7 | M-30/31 | Graphe multi-couche (Commands, ADS) |
| 8 | M-62-65 | Recettes creation (Smart params, esquisse, extrusion, inclusion) |
