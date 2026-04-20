# MISSIONS INDEX - Cortana Project
Derniere mise a jour : 2026-04-09

## Legende
- ✅ Terminee et validee
- 🔄 En cours
- 📋 Prete (mission redigee, pas lancee)
- 📅 Planifiee (pas encore redigee)

---

## Phase 1 — Fondations (Extraction + Graphe + Pathfinding)

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-01 | Extracteur par reflexion | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-1-extractor.md` |
| M-02 | Construction du TypeGraph | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-2-typegraph.md` |
| M-03 | PathFinder (BFS + Dijkstra) | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-3-pathfinder.md` |
| M-06 | Refactor en Class Library | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-6-graph-library.md` |
| M-07 | SafeExecutionWrapper | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-7-safe-wrapper.md` |

## Phase 2 — Serveur MCP (Protocole + Outils de base)

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-08a | Protocole MCP stdio | McpServer | ✅ | `.agent/workflows/mcpServer/mission-8a-protocol.md` |
| M-08b | Outil find_path | McpServer | ✅ | `.agent/workflows/mcpServer/mission-8b-findpath.md` |
| M-08c | Outil explore_paths | McpServer | ✅ | `.agent/workflows/mcpServer/mission-8c-explore.md` |
| M-09 | Outils WCF (get_state + execute_script) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-9-wcf-tools.md` |

## Phase 3 — Intelligence semantique

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-12 | Regles semantiques (modele + integration) | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-12-semantic-rules.md` |
| M-12b | Propagation SemanticHints dans MCP | McpServer | ✅ | `.agent/workflows/mcpServer/mission-12b-semantic-hints-mcp.md` |
| M-13 | Tuning iteratif des regles (guide) | ApiGraph | 📋 | `.agent/workflows/apiGraph/mission-13-semantic-tuning.md` |
| M-14 | Regles generiques (regex + namespace) | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-14-generic-semantic-rules.md` |
| M-15 | Points d'entree statiques + soft pruning | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-15-static-entries-soft-pruning.md` |
| M-15b | Synchro DLL + graph.json dans MCP | McpServer | ✅ | `.agent/workflows/mcpServer/mission-15b-sync-artifacts.md` |

## Phase 4 — Connexion directe TopSolid & Execution

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-16 | Connexion directe TopSolid (Named Pipe) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-16-direct-topsolid-connection.md` |
| M-16-fix | Connexion WCF/TCP correcte (DefineConnection) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-16-fix-wcf-connection.md` |
| M-16-fix2 | Ignorer retour Connect(), verifier via Version | McpServer | ✅ | `.agent/workflows/mcpServer/mission-16-fix2-ignore-connect-return.md` |
| M-17 | Execute Script contre TopSolid vivant | McpServer | ✅ | `.agent/workflows/mcpServer/mission-17-execute-script-live.md` |
| M-18-fix | Reconnexion lazy TopSolidConnector | McpServer | ✅ | `.agent/workflows/mcpServer/mission-18-fix-reconnect.md` |
| M-20-fix | Fix ScriptExecutor (C#5, preprocesseur, $"") | McpServer | ✅ | `.agent/workflows/mcpServer/mission-20-fix-script-executor.md` |

## Phase 5 — Connaissance API (Le Graphe Enrichi)

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-22 | Outil MCP api_help (index plat temporaire) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-22-api-help-tool.md` |
| M-23 | Enrichir GraphEdge (description, since, interface) | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-23-enrich-graph.md` |
| M-24 | Design DLL dans ScriptExecutor | McpServer | ✅ | `.agent/workflows/mcpServer/mission-24-design-dll-scripts.md` |
| M-25 | Performance graphe (timeout BFS, index inverse) | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-25-graph-performance.md` |
| M-25b | Lazy startup MCP server (<2s handshake) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-25b-lazy-startup.md` |
| M-26 | api_help sur graphe enrichi (remplace index plat) | McpServer | ✅ | `.agent/workflows/apiGraph/mission-26-api-help-on-graph.md` |

## Phase 5b — MCP Read/Write Split

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-29 | Tool topsolid_modify_script (write explicite) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-29-modify-script-tool.md` |

## Phase 5c — Robustesse MCP (Post-diagnostic M-18)

**Contexte** : Le diagnostic M-18 (DEC-005, EXP-001) a revele 3 problemes critiques dans le serveur MCP lui-meme, independamment de la couche LLM/OpenClaw.

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-38 | Fix PreprocessCode (using + Run() sans namespace) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-38-fix-preprocess-using-run.md` |
| M-39 | Suite de tests d'integration MCP (10/10 PASS) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-39-mcp-test-suite.md` |
| M-40 | Singleton MCP Server (Mutex) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-40-singleton-mcp-mutex.md` |
| M-41 | api_help : priorisation, groupement, guidance | McpServer | ✅ | `.agent/workflows/mcpServer/mission-41-api-help-priorisation.md` |
| M-42 | explore_paths : recommandation meilleur chemin | McpServer | ✅ | `.agent/workflows/mcpServer/mission-42-explore-paths-recommandation.md` |
| M-43 | execute_script : diagnostic erreurs compilation | McpServer | ✅ | `.agent/workflows/mcpServer/mission-43-execute-script-diagnostic.md` |
| M-44 | Tests T-11 a T-20 (scripts reels multi-etapes) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-44-tests-t11-t20-scripts-reels.md` |
| M-45 | Test T-00 setup (ouverture auto projet/document) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-45-test-setup-t00.md` |

## Phase 6 — Layer 1 Parfait (Read & Write)

**Objectif** : 129 scenarios catalogues, tous realisables via prompt → MCP.
**Catalogue** : `research/SCENARIOS_CATALOG.md` (16 categories, 129 scenarios)

### Phase 6a — Recettes & Contexte LLM

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-47 | Catalogue de scenarios (analyse exemples Automation) | Research | ✅ | `research/SCENARIOS_CATALOG.md` |
| M-48 | Recettes MCP Tier 1 (10 recettes R-001 a R-010) + Tier 2 (12 recettes R-040 a R-051b) | McpServer | ✅ | `TopSolidMcpServer/data/recipes.md` |

### Phase 6b — Tests Write

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-49 | Tests T-30 a T-51 (write + Tier 2 read/write) | McpServer | ✅ | `tests/TestSuite.json` (45 tests) |

### Phase 6b-fix — Bugs decouverts pendant les tests

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-51 | Fix modify_script wrapper (return + docId) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-51-fix-modify-script.md` |

### Phase 6c — Gaps & Enrichissement

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-50 | Combler gaps graphe — 5 APIs manquantes injectees | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-50-graph-gaps.md` |

**Gaps identifies (Phase 4, 2026-04-07) : 8 total, 5 fixables**

APIs manquantes fixables (dans les DLLs Kernel) :
- `IPdm.MoveSeveral` (S-016)
- `IPdm.DeleteSeveral` (S-017)
- `IPdm.SetMajorRevisionLifeCycleMainState` (S-029)
- `IPdm.UpdateDocumentReferences` (S-031)
- `IFamilies.GetConstrainedEntityCount` (S-107)

APIs hors scope (modules separes non charges) :
- `CaeHost.*` (FEA/CAE — S-170 a S-173)
- `NCPostProcessor.*` (CAM — S-180 a S-182)
- `ITables.GetDraftTables` (Drafting — a verifier si dans DLL chargee)

**Couverture graphe : 30/38 APIs verifiees (79%) — excellent pour Layer 1**
| M-27 | Extraire exemples corpora prives (local) → champ `Examples` dans graphe (792 edges) | ApiGraph | ✅ | `scripts/enrich-graph.py` Phase 3 |
| M-52 | Extraire exemples Romain (RoB) → graphe (897 edges) + 8 recettes R-064..R-071 + 8 tests T-73..T-80 | ApiGraph | ✅ | `.agent/workflows/apiGraph/mission-52-inject-rob-examples.md` |
| M-13 | Tuning semantique data-driven (130 regles, 339 hints, Phase 4 enrich-graph.py) | ApiGraph | ✅ | `scripts/enrich-graph.py` Phase 4 |
| M-53 | Fix DetectModification + return + wrapper (66/66 fonctionnel) | McpServer | ✅ | `.agent/workflows/mcpServer/mission-53-fix-detect-modification.md` |
| M-54 | Fix api_help : CamelCase split + synonymes + fallback | McpServer | ✅ | `.agent/workflows/mcpServer/mission-54-fix-api-help-search.md` |
| M-55 | Injecter 64 methodes api-index manquantes dans graphe | ApiGraph | ✅ | Impl directe (enrich-graph.py Phase 2 etendue) |
| M-56 | Enrichir SemanticHints (8% → 83%) auto-generation | ApiGraph | ✅ | Impl directe (enrich-graph.py Phase 5) |
| M-57 | Convertir aide en ligne HTM → MD (5809 pages FR+EN) | Research | ✅ | `scripts/convert-help-to-md.py` + `data/help-md/` |
| M-58 | Recettes Tier 3 : mise en plan, nomenclature, mise a plat | McpServer | 📅 | A rediger |
| M-59 | Documenter exporteurs + recettes DXF/PDF/IFC | McpServer | 🔄 | R-072/073/074 faites, audit dans `research/audit-exporters-M59.md` |
| M-60 | Recettes proprietes utilisateur | McpServer | 🔄 | R-075 faite, hints Phase 4 ajoutes |
| M-19 | Enrichissement semantique depuis exemples Automation | ApiGraph | 📋 | `.agent/workflows/apiGraph/mission-19-semantic-enrichment-from-examples.md` |

### Avancement scenarios par tier

**Tier 1 — Indispensable** (usage quotidien) :
| Categorie | Scenarios | Couverts test | Recettes | Status |
|-----------|-----------|---------------|----------|--------|
| PDM Navigation | S-001 a S-013 | 5/13 | ❌ | 🔄 |
| Documents | S-021 a S-031 | 3/11 | ❌ | 🔄 |
| Parametres CRUD | S-040 a S-059 | 2/20 | ❌ | 🔄 |
| Import/Export | S-110 a S-119 | 0/10 | ❌ | 📋 |

**Tier 2 — Important** :
| Categorie | Scenarios | Couverts test | Recettes | Status |
|-----------|-----------|---------------|----------|--------|
| Esquisses + Geo 3D | S-060 a S-077 | 1/12 | R-030 a R-034, R-039 a R-042, R-047, R-048 | ✅ 11/12 |
| Assemblages | S-080 a S-090 | 2/11 | R-035, R-036, R-043 a R-046, R-051b | ✅ 7/11 |
| Familles | S-100 a S-109 | 0/10 | R-037, R-038, R-049, R-050b | ✅ 6/10 |

**Tier 3 — Avance** :
| Categorie | Scenarios | Status |
|-----------|-----------|--------|
| Drafting, BOM, Materiaux, Multi-couches, FEA, CAM, Batch | S-120 a S-194 | 📅 |

## Phase 7 — Graphe Multi-Couche (Commands, ADS, Vision)

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-30 | Parser doc Commands → noeuds Layer 2 | ApiGraph | 📅 | A rediger |
| M-31 | Parser doc ADS → noeuds Layer 3 | ApiGraph | 📅 | A rediger |
| M-32 | Liens cross-couche (Command <-> API, ADS <-> Command) | ApiGraph | 📅 | A rediger |

## Phase 8 — Outils metier & Securite

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-20 | Outils MCP batch PDM (4 outils haut niveau) | McpServer | 📋 | `.agent/workflows/mcpServer/mission-20-batch-pdm-tools.md` |
| M-21 | Securite, rollback et confirmation utilisateur | McpServer | 📋 | `.agent/workflows/mcpServer/mission-21-safety-and-rollback.md` |

## Phase 9 — LoRA & Fine-tuning (Projet Noemid)

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-33 | Generation dataset LoRA (paires instruction→code) | Noemid | 📅 | A rediger |
| M-34 | Fine-tuning LoRA sur mistral-small3.1 ou codestral | Noemid | 📅 | A rediger |
| M-35 | Integration modele LoRA comme agent topsolid | OpenClaw | 📅 | A rediger |

## Phase 5e — Fix MCP Post-Tests OpenClaw

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-46 | Fix stdin encoding UTF-8 (accents corrompus) | McpServer | ✅ | Fix direct : McpStdioServer.cs |

## Phase 10 — Test & Validation

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| M-18 | Test e2e Cortana (4 scenarios) — sous-agent Gemini DEC-009 | McpServer | 🔄 | `.agent/workflows/mcpServer/mission-18-e2e-cortana-test.md` |
| M-36 | Benchmark : mesurer latence, tokens, taux de succes | McpServer | 📅 | A rediger |
| M-37 | Suite de tests automatises (CI sans TopSolid) | McpServer | 📅 | A rediger |

## Phase OC — OpenClaw (Gateway IA)

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| OC-1 | Sync OpenClaw avec Cortana | OpenClaw | ✅ | `.agent/workflows/openclaw/mission-OC1-sync-cortana.md` |
| OC-2 | Agent topsolid (system.md + config) | OpenClaw | ✅ | Config directe (pas de mission) |
| OC-3 | Fix delegation Main → topsolid (workspaces isoles + tools.deny) | OpenClaw | ✅ | Config directe |
| OC-4 | Patterns TopSolid dans TOOLS.md agent | OpenClaw | ✅ | workspace-topsolid/TOOLS.md |

## Phase L — Launcher & Infrastructure

| # | Mission | Bloc | Statut | Fichier |
|---|---------|------|--------|---------|
| L-2 | Launcher watchdog + crash recovery + cleanup | Launcher | ✅ | `.agent/workflows/launcher/mission-L2-watchdog.md` |

---

## Ordre d'execution recommande (prochaines missions)

1. ~~**M-38**~~ ✅ Fix PreprocessCode (2026-04-04)
2. ~~**M-39**~~ ✅ Test Suite MCP — 10/10 PASS, baselines figees (2026-04-05)
3. ~~**M-40**~~ ✅ Singleton Mutex (2026-04-04)
4. ~~**M-29**~~ ✅ ModifyScriptTool (2026-04-05)
5. ~~**L-2**~~ ✅ Launcher watchdog (2026-04-05)
6. ~~**M-41**~~ ✅ api_help priorisation + guidance (2026-04-05)
7. ~~**M-42**~~ ✅ explore_paths recommandation (2026-04-05)
8. ~~**M-43**~~ ✅ execute_script diagnostic erreurs (2026-04-05)
9. ~~**M-44**~~ ✅ Tests T-11 a T-20 scripts reels (2026-04-05)
10. ~~**M-45**~~ ✅ Test T-00 setup auto (2026-04-05)
11. **M-57** 📅 Convertir aide en ligne HTM → MD (2868 pages FR+EN)
12. **M-58** 📅 Recettes Tier 3 : mise en plan, nomenclature, mise a plat
13. **M-59** 📅 Documenter les exporteurs et leurs options (STEP, DXF, PDF, IFC)
14. **M-60** 📅 Recettes proprietes utilisateur (SearchUserPropertyParameter)

## Notes

- Tous les fichiers mission sont dans `.agent/workflows/` a la racine de Cortana (gitignored)
- `apiGraph/` pour ApiGraph, `mcpServer/` pour McpServer
- Les regles AG sont dans `.agent/rules/` (auto-chargees par Antigravity)
- Les decisions sont dans `research/decisions/` (DEC-001 a DEC-007)
- Les resultats d'experiences sont dans `research/experiments/` (EXP-001+)
- Source de doc MD : `C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\`
