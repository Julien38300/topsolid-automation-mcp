# ROADMAP - Cortana Project
Derniere mise a jour : 2026-04-09

## Progression par Phases

```
Phase 1  ████████████████████ 100%  Fondations (extraction, graphe, pathfinding)
Phase 2  ████████████████████ 100%  Serveur MCP (protocole, 5 outils)
Phase 3  ████████████████████ 100%  Intelligence semantique (regles, regex, pruning)
Phase 4  ████████████████████ 100%  Connexion TopSolid & execution scripts
Phase 5  ████████████████████ 100%  Connaissance API (graphe enrichi, api_help)
Phase 5b ████████████████████ 100%  Read/Write split (M-29 ✅, M-51 ✅)
Phase 5c ████████████████████ 100%  Robustesse MCP (M-38..M-45) ✅
Phase 5d ████████████████████ 100%  Qualite Read/Graph (M-41/42/43) ✅
Phase 5e ████████████████████ 100%  Fix MCP Post-Tests (M-46 UTF-8) ✅
Phase 6a ████████████████████ 100%  Recettes Tier 1/2 (M-47/48 — 68 recettes) ✅
Phase 6b ████████████████████ 100%  Tests Write (M-49..M-53 — 72 tests, 68 PASS) ✅
Phase 6c ████████████████████ 100%  Graphe complet (M-50/27/52/13/55/56) ✅
Phase 6d ████████████████████ 100%  Fix api_help (M-54 synonymes+fallback) ✅
Phase 6e ██████████████░░░░░░  70%  Tier 3 recettes (M-58 mise en plan/BOM/export)
Phase 7  ░░░░░░░░░░░░░░░░░░░░   0%  Graphe multi-couche (commands, ADS)
Phase 8  ░░░░░░░░░░░░░░░░░░░░   0%  Outils metier & securite
Phase 9  ░░░░░░░░░░░░░░░░░░░░   0%  LoRA fine-tuning (projet Noemid)
Phase 10 ████████████░░░░░░░░  60%  Test & validation (72 tests, 68/72 PASS)
Phase OC █████████████████░░░  85%  OpenClaw → remplace par Hermes (D-018 Noemid)
Phase L  ████████████████████ 100%  Launcher & infrastructure (L-2) ✅
```

## Etat actuel (2026-04-09)

**Graphe API : COMPLET**
- 4119 edges, 242 nodes, 1728 methodes uniques
- 0 methodes api-index manquantes
- Description : 90%, SemanticHint : 84%, Examples : 22%
- 52 synonymes FR dans api_help, 130+ traductions metier

**Recettes : 68 total**
- Tier 1 (PDM, documents, parametres) : 20 recettes
- Tier 2 (esquisses, assemblages, familles, geometrie) : 32 recettes
- Tier 3 (export DXF/PDF/IFC, proprietes utilisateur) : 4 recettes
- Demo (scenarios TopSolid) : 12 recettes

**Tests : 72 total, 68 PASS**
- T-00..T-86 (4 FAIL = bruit perf, pas de bug)
- 55 tests necessitent TopSolid vivant

**Aide en ligne : convertie en MD**
- 5809 pages (2835 FR + 2974 EN), 9MB total
- Pret pour RAG / Noemid / LoRA

**Glossaire metier TopSolid FR : valide par Julien**
- Nom/Designation/Reference/Fabricant mappes correctement
- Mise au coffre/Sorti de coffre, mise a plat, gabarit, motif, rafale...

## Timeline cible (revisee 2026-04-09)

- ~~**Avril S1**~~ : ✅ M-38..M-45 (robustesse MCP)
- ~~**Avril S2**~~ : ✅ M-46..M-56 (recettes, tests, graphe complet, glossaire)
- **Avril S3** : M-58 (recettes Tier 3 mise en plan/BOM), M-59 (exporteurs), integration Hermes
- **Mai** : Phase 7 (graphe multi-couche), benchmark (M-36), aide en ligne dans RAG
- **Juin** : Phase 8 (securite/rollback), Phase 9 (LoRA — Noemid)

## Prochaines missions

| Priorite | Mission | Description |
|----------|---------|-------------|
| 1 | M-58 | Recettes Tier 3 : mise en plan, nomenclature, mise a plat |
| 2 | M-59 | Documenter exporteurs et options (batch, formats) |
| 3 | M-60 | Recettes proprietes utilisateur + occurrence |
| 4 | — | Integration Hermes Agent (Noemid D-018) |
| 5 | — | Tier 2 avance (S-087 DerivePartForModification, S-088 substitution) |
| 6 | M-57 | Injecter aide en ligne MD dans RAG/ChromaDB |
| 7 | M-36 | Benchmark latence/tokens/taux de succes |

## Risques

| Risque | Impact | Mitigation | Statut |
|--------|--------|------------|--------|
| ~~TopSolid:8090 ne repond pas~~ | ~~Bloquant~~ | ~~WCF/TCP~~ | ✅ Resolu (M-16) |
| ~~Pont WCF instable~~ | ~~Moyen~~ | ~~Graceful degradation~~ | ✅ Resolu (M-18-fix) |
| ~~Inference locale trop lente~~ | ~~Bloquant~~ | ~~Ecarter sous-agent~~ | ✅ Remplace par Hermes (D-018) |
| ~~PreprocessCode 100% echec~~ | ~~Bloquant~~ | ~~Fix M-38~~ | ✅ Resolu |
| ~~Multi-spawn MCP (10+ instances)~~ | ~~Haut~~ | ~~Mutex M-40~~ | ✅ Resolu |
| ~~Pas de tests automatises~~ | ~~Haut~~ | ~~Test suite M-39~~ | ✅ 72 tests |
| ~~Regles semantiques insuffisantes~~ | ~~Moyen~~ | ~~Iteration JSON~~ | ✅ 84% hints (M-56) |
| ~~api_help rate les queries FR~~ | ~~Moyen~~ | ~~Synonymes + fallback~~ | ✅ 52 synonymes (M-54) |
| Aide en ligne pas exploitee | Moyen | Conversion MD faite, injection RAG a faire | A faire |
| Pas de CI sans TopSolid | Moyen | 17/72 tests offline, mock possible | A surveiller |
| Hermes pas branche | Haut | Chaine agent → MCP non testee e2e | Prochaine etape |

## Decisions de reference

Voir `research/decisions/` pour les 10 decisions documentees (DEC-001 a DEC-010).
