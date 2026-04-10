# Roadmap

## Progression

| Phase | Description | Avancement |
|-------|-------------|------------|
| Phase 1 | Fondations (extraction, graphe, pathfinding) | ✅ 100% |
| Phase 2 | Serveur MCP (protocole, 5 outils) | ✅ 100% |
| Phase 3 | Intelligence semantique (regles, pruning) | ✅ 100% |
| Phase 4 | Connexion TopSolid & execution scripts | ✅ 100% |
| Phase 5 | Connaissance API (graphe enrichi, api_help) | ✅ 100% |
| Phase 5b-e | Robustesse, qualite, fixes | ✅ 100% |
| Phase 6a-d | Recettes, tests, graphe complet, glossaire | ✅ 100% |
| Phase 6e | Recettes Tier 3 (mise en plan, BOM, export) | 🔄 70% |
| Phase 7 | Graphe multi-couche (commands, ADS) | 📅 0% |
| Phase 8 | Outils metier & securite | 📅 0% |
| Phase 9 | LoRA fine-tuning (Noemid) | 📅 0% |
| Phase 10 | Test & validation | 🔄 60% |

## Chiffres cles (2026-04-09)

- **4119 edges** dans le graphe API
- **1728 methodes** uniques couvertes
- **76 recettes** C# documentees + **75 RecipeTool** pour modeles 3B
- **85 tests** automatises
- **84%** d'edges avec hints semantiques
- **5809 pages** d'aide en ligne converties en MD
- **Hermes e2e teste** : ministral-3:3b → run_recipe → TopSolid en 4 secondes

## Prochaines etapes

| Priorite | Mission | Description |
|----------|---------|-------------|
| 1 | M-58 | Recettes Tier 3 : mise en plan, nomenclature, mise a plat |
| 2 | M-59 | Documenter exporteurs et options (batch, formats) |
| 3 | M-60 | Recettes proprietes utilisateur + occurrence |
| 4 | — | ~~Integration Hermes Agent (Noemid)~~ ✅ Teste avec ministral-3b |
| 5 | M-57 | Injecter aide en ligne MD dans RAG/ChromaDB |
| 6 | M-36 | Benchmark latence/tokens/taux de succes |

## Historique des missions

### Avril 2026 — Semaine 1
- M-38 : Fix PreprocessCode (100% scripts echouaient)
- M-39 : Suite de tests MCP (10/10 PASS)
- M-40 : Singleton Mutex (plus de multi-spawn)
- M-29 : ModifyScriptTool (write explicite)
- M-41..M-45 : Qualite MCP (priorisation, diagnostic, tests)

### Avril 2026 — Semaine 2
- M-46 : Fix encodage UTF-8
- M-47..M-49 : 129 scenarios catalogues, 40 recettes, 33 tests
- M-50..M-51 : Gaps graphe combles, fix modify_script
- M-27 + M-52 : Exemples REDACTED-USER + Romain (897 edges enrichies)
- M-13 : Tuning semantique (130 regles, 339 hints)
- M-53 : Fix DetectModification (66/66 fonctionnel)
- M-54 : Fix api_help (synonymes, CamelCase, fallback)
- M-55 : Injection 64 methodes (0 manquantes)
- M-56 : SemanticHints 8% → 84%
- M-57 : Conversion aide en ligne (5809 pages)
- Glossaire metier FR valide par Julien
- RecipeTool : 75 recettes pre-construites pour modeles 3B (run_recipe)
- Integration Hermes Agent testee avec ministral-3b (e2e en 4s)
