# Mission — Mise a jour site VitePress (DEC-011 + chiffres)

**Date** : 2026-04-15
**Repo** : noemid-topsolid-automation (C:\Users\jup\OneDrive\noemid-topsolid)
**Objectif** : Mettre a jour les 4 pages docs pour refleter DEC-011 (dual sub-agent) et les chiffres actuels

REGLE : aucune mention de "Hermes" ne doit subsister dans les docs apres cette mission.

---

## ETAPE 1 — `docs/guide/presentation.md`

### 1a. Remplacer le bloc "Architecture Hermes (agent)" (lignes 71-87)

Remplacer TOUT le bloc depuis `## Architecture Hermes (agent)` jusqu'a la fin de cette section par :

```markdown
## Architecture Agent (OpenClaw)

```
OpenClaw Main (cloud, leger — routing + conversation)
  |
  ├── topsolid-recipes (3B LoRA, local)
  |     → topsolid_run_recipe
  |     113 recettes pre-construites
  |     Classification : intent → nom de recette
  |     Latence : ~2-4 secondes
  |
  └── topsolid-automation (14-24B, local — a venir)
        → topsolid_execute_script + api_help + find_path
        Generation C# via le graphe API
        Cas hors-recettes, scripts ad-hoc
        Latence cible : < 30 secondes
```

Le Main (cloud) garde la coherence conversationnelle et route les demandes :
- **Recette connue** (80% des cas) → sous-agent 3B, rapide et fiable
- **Cas custom** (20%) → sous-agent 14-24B, generation de code via le graphe

Le LoRA cible le 3B pour ameliorer sa connaissance TopSolid. Un second LoRA (futur) ciblera le 14-24B pour la generation C#.
```

### 1b. Mettre a jour le dataset LoRA (ligne 66)

Remplacer :
```
732 paires d'entrainement au format ShareGPT pour fine-tuner un modele 3B sur la selection de recettes.
```
Par :
```
2161 paires d'entrainement au format ShareGPT pour fine-tuner un modele 3B sur la selection de recettes. Couvre les 112 recettes avec variantes francais naturel/metier et paires d'ambiguite Smart params.
```

### 1c. Remplacer la mention d'agent dans le schema du haut (ligne 8)

Remplacer :
```
Agent IA (Hermes / Claude / OpenClaw / tout client MCP)
```
Par :
```
Agent IA (OpenClaw / Claude / tout client MCP)
```

---

## ETAPE 2 — `docs/guide/architecture.md`

### 2a. Remplacer le schema ASCII en haut (lignes 5-38)

Remplacer le bloc ASCII qui montre `Agent IA (Hermes/LLM)` par :

```markdown
```
          ┌────────────────────────────┐
          │  OpenClaw Main (cloud)     │
          │  Routing + conversation    │
          └────────┬───────────────────┘
                   │
          ┌────────┴────────────────────────────┐
          │                                     │
  ┌───────v────────┐               ┌────────────v──────────┐
  │ topsolid-      │               │ topsolid-             │
  │ recipes (3B)   │               │ automation (14-24B)   │
  │                │               │ (a venir — M-35)      │
  │ run_recipe     │               │ execute_script        │
  │ 113 recettes   │               │ api_help, find_path   │
  └───────┬────────┘               │ explore_paths         │
          │                        └────────────┬──────────┘
          │                                     │
          └────────────────┬────────────────────┘
                           │ stdio JSON-RPC
                  ┌────────v──────────┐
                  │ TopSolidMcpServer │
                  │  (.NET 4.8, C#7.3)│
                  │                   │
                  │  TypeGraph        │
                  │  KeywordIdx       │
                  │  RecipeTool       │
                  │  7 outils MCP     │
                  └────────┬──────────┘
                           │ WCF/TCP :8090
                  ┌────────v──────────┐
                  │   TopSolid 7      │
                  └───────────────────┘
```
```

### 2b. Ajouter une section "Architecture Agent" APRES "Connexion TopSolid" (apres ligne 47)

Inserer :

```markdown
## Architecture Agent (DEC-011)

Deux sous-agents specialises dans OpenClaw, chacun avec ses propres outils MCP :

| Sous-agent | Modele | Outils MCP | Tache | Risque |
|------------|--------|------------|-------|--------|
| **topsolid-recipes** | 3B LoRA (ministral) | `run_recipe` | Selection de recette par nom | Faible (code pre-teste) |
| **topsolid-automation** | 14-24B (Mistral) | `execute_script` + `api_help` + `find_path` + `explore_paths` | Generation C# via graphe | Eleve (code genere) |

**Routing** : le Main (cloud) analyse l'intention. Si une recette couvre le besoin → 3B. Sinon → 14-24B qui navigue le graphe et genere un script C#.

**VRAM** (RTX 5090, 24 GB) : les deux modeles cohabitent (3B ~3 GB + 24B Q4 ~16 GB = ~19 GB).

**Benchmark prevu** (M-35) : Mistral Small 3.1 24B Q4, Codestral 22B Q4, et variante 14B Q8 pour comparer qualite C# / latence / VRAM.
```

### 2c. Corriger les chiffres perimes dans le schema (lignes 19-20)

Remplacer `recipes.md (68 recettes)` par `RecipeTool (113 recettes)`.
Remplacer `api-index.json (1462 methods)` par `api-index.json (1728 methods)`.

### 2d. Corriger la liste d'outils (lignes 24-29)

Remplacer les 5 outils par 7 outils :
```
  7 outils MCP :
  - run_recipe
  - get_state
  - api_help
  - execute_script
  - modify_script
  - find_path
  - explore_paths
```

### 2e. Corriger le tableau Donnees (lignes 82-88)

Mettre a jour :
- `recipes.md (68 recettes)` → `RecipeTool.cs (113 recettes)`
- `api-index.json ... 1462 methodes` → `1728 methodes`
- Ajouter une ligne : `data/lora-dataset.jsonl` | 2161 paires LoRA (ShareGPT) | ~1.5 MB

---

## ETAPE 3 — `docs/guide/fonctionnement.md`

### 3a. Remplacer le bloc "HERMES AGENT" dans le schema (lignes 12-21)

Remplacer :
```
│  HERMES AGENT (WSL2, ministral-3:3b)                    │
│                                                         │
│  Le LLM comprend l'intention en francais.               │
│  Le Skill topsolid-mcp lui dit quoi faire.              │
```
Par :
```
│  SOUS-AGENT RECETTES (3B LoRA, local)                   │
│                                                         │
│  Le LLM comprend l'intention en francais.               │
│  Il selectionne la recette par nom.                     │
```

### 3b. Renommer les 2 modes (lignes 90-127)

Remplacer `### Mode 3B — Selection de recette (actuel)` par :
```
### Sous-agent Recettes (3B LoRA) — actuel
```

Remplacer `Pour les petits modeles (ministral-3:3b, ~2GB VRAM) qui ne savent pas generer du C# :` par :
```
Le sous-agent 3B + LoRA selectionne la bonne recette par nom. Pas de generation de code :
```

Remplacer `### Mode 7B+ — Generation de code (futur)` par :
```
### Sous-agent Automation (14-24B) — prevu M-35
```

Remplacer `Pour les modeles plus gros (qwen2.5:7b, mistral-small) ou fine-tuned (LoRA domain) :` par :
```
Le sous-agent 14-24B (Mistral Small ou Codestral) navigue le graphe API et genere du C# :
```

### 3c. Mettre a jour le tableau "Role de chaque brique" (ligne 131+)

Remplacer la ligne Skill :
```
| **Skill** (SKILL.md) | Instructions pour Hermes : quel outil appeler selon la question | Guide le LLM vers la bonne recette sans se perdre. |
```
Par :
```
| **Skill** (system.md) | Instructions pour chaque sous-agent : outils autorises et routing | Chaque agent a son propre system.md dans OpenClaw. |
```

Remplacer `recipes.md | 76 recettes` par `RecipeTool | 113 recettes`.

### 3d. Corriger "10 recettes" dans le schema des outils (ligne 62)

Remplacer `10 recettes` par `113 recettes`. Retirer le `(NOUVEAU)` de run_recipe.

---

## ETAPE 4 — `docs/guide/roadmap.md`

### 4a. Mettre a jour Phase 9 et ajouter Phase 9b

Remplacer :
```
| Phase 9 | LoRA fine-tuning (Noemid) — M-33/M-34 | 0% |
```
Par :
```
| Phase 9a | LoRA recettes (M-33 dataset + M-34 training 3B) | 50% |
| Phase 9b | Benchmark automation (M-35 — 14B/24B/Codestral) — DEC-011 | 0% |
```

### 4b. Mettre a jour les chiffres cles

Remplacer `732 paires` par `2161 paires`.
Changer la date de `2026-04-11` a `2026-04-15`.

### 4c. Remplacer la table "Prochaines etapes"

Remplacer TOUTE la table par :

```markdown
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
```

---

## Validation

- [ ] `npm run build` dans docs/ passe sans erreur
- [ ] Aucune mention de "Hermes" dans les 4 fichiers modifies
- [ ] Les chiffres sont a jour : 113 recettes, 7 outils, 2161 paires LoRA, 1728 methodes
- [ ] Le schema architecture montre les 2 sous-agents
- [ ] Les modes "3B" et "7B+" sont renommes en "Recettes" et "Automation"
