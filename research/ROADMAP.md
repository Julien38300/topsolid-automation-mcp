# ROADMAP - Cortana Project
Derniere mise a jour : 2026-04-16

## Progression par Phases

```
Phase 1  ████████████████████ 100%  Fondations (extraction, graphe, pathfinding)
Phase 2  ████████████████████ 100%  Serveur MCP (protocole, 7 outils — RecipeTool ajouté)
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
Phase 9a ████████████████████ 100%  LoRA 3B recettes (100/100 eval, OpenClaw prefix, PROD)
Phase 9b ░░░░░░░░░░░░░░░░░░░░   0%  LoRA 14-24B automation (C# via graph API — DEC-011)
Phase 10 █████████████░░░░░░░  65%  Test & validation (85 tests, 68/72 PASS MCP + e2e Hermes)
Phase 11 ██░░░░░░░░░░░░░░░░░░  10%  Internationalisation (MCP EN ✅, dico 69K paires, multilingue futur)
Phase OC ████████████████████ 100%  Hermes v0.8.0 integre (e2e ministral-3:3b → MCP → TopSolid OK)
Phase L  ████████████████████ 100%  Launcher & infrastructure (L-2) ✅
```

## Etat actuel (2026-04-15)

**Graphe API : COMPLET**
- 4119 edges, 242 nodes, 1728 methodes uniques
- 0 methodes api-index manquantes
- Description : 90%, SemanticHint : 84%, Examples : 22%
- 52 synonymes FR dans api_help, 130+ traductions metier

**Recettes : 112 total (noms EN)**
- Tier 1 (PDM, documents, parametres) : 20 recettes
- Tier 2 (esquisses, assemblages, familles, geometrie) : 32 recettes
- Tier 3 (export, mise en plan, BOM, depliage, audit batch) : 48 recettes
- Interactif (selection shape/face/point) : 3 recettes
- Comparaison/Report : 9 recettes
- Migration FR -> EN complete (noms, descriptions, outputs, labels)

**LoRA Pipeline : AUTOMATISE**
- `make lora` = validate -> generate -> train -> export GGUF -> import Ollama -> eval
- Config centralisee : `scripts/lora-pipeline.yaml`
- Dataset : 2161 paires ShareGPT (112 recettes EN)
- Eval : 50 questions, 5 tiers (trivial -> piege)
- Training en cours (Ministral-3B, RTX 5090)

**Internationalisation**
- MCP layer : 100% anglais (noms recettes, descriptions, outputs, labels)
- Dico officiel TopSolid : 53 898 paires EN/FR, 16 domaines (`topsolid-translation-memory.json`)
- SKILL.md / eval-lora.py / Modelfile : migres EN
- Site VitePress : noms recettes EN, textes descriptions FR (locale FR)
- TopSolid(R) marque deposee de TOPSOLID SAS (corrige)

**Tests : 85 total, 68 PASS**
- T-00..T-86 (4 FAIL = bruit perf, pas de bug)
- 55 tests necessitent TopSolid vivant
- e2e Hermes -> MCP -> TopSolid : ministral-3:3b + run_recipe = SUCCESS (4s)

**Aide en ligne : convertie en MD**
- 5809 pages (2835 FR + 2974 EN), 9MB total
- Pret pour RAG / Noemid / LoRA

**Glossaire metier TopSolid FR : valide par Julien**
- Nom/Designation/Reference/Fabricant mappes correctement
- Mise au coffre/Sorti de coffre, mise a plat, gabarit, motif, rafale...

## Timeline cible (revisee 2026-04-15)

- ~~**Avril S1**~~ : ✅ M-38..M-45 (robustesse MCP)
- ~~**Avril S2**~~ : ✅ M-46..M-56 (recettes, tests, graphe complet, glossaire)
- ~~**Avril S3**~~ : ✅ Migration MCP FR->EN, pipeline LoRA auto, integration Hermes
- **Avril S4** : Phase 9a (LoRA 3B recettes — training + eval), Phase 9b design (14-24B graph API)
- **Mai** : Phase 7 (graphe multi-couche), Phase 9b (LoRA automation C#), benchmark (M-36)
- **Juin** : Phase 8 (securite/rollback), Phase 11 (multilingue avance — agent repond en FR/EN)

## Prochaines missions

| Priorite | Mission | Description |
|----------|---------|-------------|
| ~~1~~ | ~~Phase 9a~~ | ✅ **DONE** — LoRA 3B recettes : 100/100 eval, OpenClaw prefix, PROD |
| 2 | Phase 9b | LoRA 22B automation : Codestral-22B qui genere du C# via graph API (DEC-011). Baseline vanilla OK sur Pattern D (100%) / SI units (97%), faiblesses : Tier 4 creation APIs (27%), Tier 6 refusals (60%). Training en cours. |
| ~~3~~ | ~~M-70~~ | ✅ **DONE** (v1.5.2) — Doc VitePress `/guide/knowledge-base.md` "MCP as knowledge base for standalone C# dev" |
| ~~4~~ | ~~M-71~~ | ✅ **DONE** (v1.5.0/1/2) — 4 nouveaux outils MCP : `topsolid_get_recipe`, `topsolid_compile`, `topsolid_search_examples`, `topsolid_whats_new`. MCP expose maintenant 11 outils au total. |
| ~~5~~ | ~~M-72~~ | ✅ **DONE** — Corpus AF/REDACTED indexes (225 snippets method-level) via `topsolid_search_examples`. FEA Quality (34 files) integre. |
| 6 | M-58 | Recettes Tier 3 restantes : mise en plan, nomenclature, mise a plat |
| 7 | M-59 | Documenter exporteurs et options (batch, formats) |
| 8 | M-60 | Recettes proprietes utilisateur + occurrence |
| 9 | M-61 | **Publier LoRA sur Hugging Face** (`ministral-topsolid` GGUF + Modelfile + SKILL.md) — que les users fassent `ollama pull hf.co/Julien38300/ministral-topsolid` |
| ~~10~~ | ~~M-62~~ | ✅ **DONE** — Pipeline auto-sync TopSolid API depuis CHM (extract/parse/diff/enrich/propose/report) |
| 11 | — | Tier 2 avance (S-087 DerivePartForModification, S-088 substitution) |
| 12 | M-57 | Injecter aide en ligne MD dans RAG/ChromaDB |
| 13 | M-36 | Benchmark latence/tokens/taux de succes |
| 14 | Phase 11 | Multilingue : agent repond dans la langue de l'utilisateur (dico 69K paires) |

## Vision élargie : MCP as Knowledge Base

Au-delà du runtime MCP pour agents IA, le serveur MCP est une **base de connaissance vivante** de l'API TopSolid Automation :

**Ressources exposées** :
- `graph.json` enrichi (4119 edges, 1728 methodes, 100% descriptions, 79% remarks, since versions, deprecation)
- `methods.json` / `types.json` (snapshot par version TopSolid via pipeline CHM)
- 118 recettes production (code C# self-contained validé)
- 500+ methodes transactionnelles avec Pattern D explicite dans les Remarks
- Pipeline auto-sync : nouveau CHM → graph + proposals a chaque release

**Cas d'usage "knowledge base"** (M-70, M-71, M-72) :
1. **Claude Code + MCP** = developpeur TopSolid senior instantane. Ecrit une app C# TopSolid "du premier coup" en 80-90% des cas en utilisant `find_path`, `api_help`, `get_recipe`, `compile` au fil de l'ecriture.
2. **Industriel** qui veut automatiser son PDM : lance Claude Code + MCP, decrit son besoin, obtient une app Visual Studio prete a deployer.
3. **Formateur TopSolid** : utilise le MCP comme tutor interactif avec code testable.
4. **Dev interne** : genere ses scripts via MCP, les extrait en projet .NET standalone.

Le MCP devient **le cerveau**, le graph **la memoire**, les recettes **le savoir-faire valide**, les exemples FEA/REDACTED **les patterns production**.

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
| ~~MCP full FR sur repo public~~ | ~~Haut~~ | ~~Migration EN~~ | ✅ 112 recettes + outputs (2026-04-15) |
| Aide en ligne pas exploitee | Moyen | Conversion MD faite, injection RAG a faire | A faire |
| Pas de CI sans TopSolid | Moyen | 17/72 tests offline, mock possible | A surveiller |
| ~~Hermes pas branche~~ | ~~Haut~~ | ~~Chaine agent → MCP non testee e2e~~ | ✅ e2e OK (ministral-3:3b → run_recipe → TopSolid, 4s) |
| LoRA 14-24B C# pas encore concu | Moyen | DEC-011 pose l'architecture, graph API disponible | Phase 9b |

## Decisions de reference

Voir `research/decisions/` pour les decisions documentees (DEC-001 a DEC-011).
