# Outils MCP

Le serveur expose **13 outils** au protocole MCP. L'agent IA les appelle via JSON-RPC — sur stdin/stdout (mode stdio) ou via HTTP/SSE (mode bridge).

## topsolid_run_recipe

**L'outil principal.** Execute une recette pre-construite par nom. Le LLM n'a pas besoin de generer du code C# — il choisit juste le nom de la recette.

**132 recettes** disponibles couvrant : PDM, parametres, masse/volume, assemblages, export (6 formats), mise en plan, nomenclature, mise a plat, comparaison de documents, report de modifications, audit batch, familles, creation de geometrie (parametres Smart, esquisses, extrusions, inclusions).

```json
{ "name": "topsolid_run_recipe", "arguments": { "recipe": "read_mass_volume" } }
```

**Avec parametre :**
```json
{ "name": "topsolid_run_recipe", "arguments": { "recipe": "set_designation", "value": "Bride de fixation" } }
```

**Reponse type :**
```
Masse: 9.922 kg
Volume: 1263904.82 mm3
Surface: 115447.61 mm2
```

::: tip Quand utiliser run_recipe vs execute_script ?
- `run_recipe` : pour les 129 operations pre-definies (rapide, fiable, pas besoin de C#)
- `execute_script` : pour du C# custom que le LLM genere a la volee (plus flexible, plus risque)

Un modele 3B (ex: `ministral-topsolid`) peut utiliser `run_recipe`. Seul un modele 24B+ (ex: `codestral:22b`) peut utiliser `execute_script` correctement.
:::

## topsolid_get_state

Retourne l'etat courant : document actif, projet, connexion TopSolid.

**Usage** : premier appel pour verifier que TopSolid est connecte.

```json
{ "name": "topsolid_get_state", "arguments": {} }
```

**Reponse type** :
```
Connected: True (v7.20.258)
Project: MonProjet
Document: MaPiece.TopPrt (DocumentId: 12345)
```

## topsolid_api_help

Recherche dans les 1728 methodes API. Supporte les noms d'interface, mots-cles FR/EN, et filtrage.

**52 synonymes FR** : designation → SetDescription, reference → SetPartNumber, esquisse → Sketch, mise a plat → IUnfoldings...

**Modes** :
- Interface exacte : `api_help("IParameters")` → liste complete
- Mot-cle : `api_help("designation")` → trouve SetDescription
- Filtre : `api_help("IDocuments.Export")` → methodes Export de IDocuments

```json
{ "name": "topsolid_api_help", "arguments": { "query": "designation" } }
```

::: tip Fallback
Si la recherche par mots-cles ne trouve rien, un fallback cherche dans les descriptions et hints semantiques.
:::

## topsolid_execute_script

Compile et execute du C# 5 contre TopSolid. Pour les operations **en lecture seule**.

```json
{
  "name": "topsolid_execute_script",
  "arguments": {
    "code": "DocumentId docId = TopSolidHost.Documents.EditedDocument;\nreturn TopSolidHost.Documents.GetName(docId);"
  }
}
```

## topsolid_modify_script

Identique a `execute_script` mais pour les **modifications**. Gere automatiquement `StartModification` / `EndModification` / `Save`.

::: warning
`EnsureIsDirty(ref docId)` change le docId. Toujours chercher les elements APRES cet appel.
:::

## topsolid_find_path

Trouve le chemin le plus court (Dijkstra) entre deux types dans le graphe API.

```json
{
  "name": "topsolid_find_path",
  "arguments": {
    "from": "TopSolid.Kernel.Automating.DocumentId",
    "to": "TopSolid.Kernel.Automating.ElementId"
  }
}
```

## topsolid_explore_paths

Explore plusieurs chemins (BFS) entre deux types. Timeout 5 secondes pour eviter les freezes.

## topsolid_get_recipe (v1.5.0+)

Retourne le code C# d'une recette par nom, sans l'executer. Utile pour apprendre les patterns valides ou adapter une recette dans une app standalone.

**Sans TopSolid connecte.** Pure knowledge-base lookup.

```json
{ "name": "topsolid_get_recipe", "arguments": { "recipe": "read_mass_volume" } }
```

Sans `recipe` : retourne la liste des 124 recettes avec mode READ/WRITE + description.

## topsolid_compile (v1.5.1+)

Compile un script C# contre l'API TopSolid SANS l'executer. Detecte les APIs hallucinees, erreurs de syntaxe, types manquants.

```json
{
  "name": "topsolid_compile",
  "arguments": {
    "code": "DocumentId docId = TopSolidHost.Documents.EditedDocument; return TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docId));"
  }
}
```

Reponse : `OK: code compiles successfully. Mode: READ` ou liste d'erreurs avec numeros de ligne.

**Sans TopSolid connecte.**

## topsolid_search_examples (v1.5.2+)

Cherche dans les corpora prives locaux de l'utilisateur (non livres avec le serveur, paths configures dans `SearchExamplesTool.cs`). Match sur corps de methode + nom.

```json
{
  "name": "topsolid_search_examples",
  "arguments": { "query": "StartModification", "max_results": 3, "corpus": "corp-a" }
}
```

Retourne des snippets method-level avec label corpus + chemin fichier. Cache 10 min.

**Sans TopSolid connecte.**

## topsolid_whats_new (v1.5.2+)

Retourne le changelog markdown de l'API TopSolid pour une version donnee (ou la plus recente), tel que genere par le pipeline `sync-topsolid-api`.

```json
{ "name": "topsolid_whats_new", "arguments": { "version": "7.21.164.0" } }
```

Lists : methodes ajoutees, changements de signature, deprecations, propositions de recettes.

**Sans TopSolid connecte.**

## topsolid_search_help (v1.6.0+)

Full-text search (SQLite FTS5) sur **5809 pages** de l'aide en ligne TopSolid (2974 EN + 2835 FR). Tokenizer unicode61 avec folding des diacritiques — `esquisse` matche `ésquissé`. Ranking bm25, snippets avec terme surligne `[...]`.

**Filtres** : `lang` (EN / FR), `domain` (Cad / Cae / Cam / Erp / Kernel / Pdm / WorkManager), `max_results` (defaut 5, max 20).

```json
{
  "name": "topsolid_search_help",
  "arguments": { "query": "mise en plan section", "lang": "FR", "max_results": 5 }
}
```

Base `data/help.db` embarquee (~20 MB). Pas de dependance externe (ChromaDB / Ollama / Python). Index construit offline via `scripts/build-help-index.py`.

**Sans TopSolid connecte.** Reponse typique :

```
Found 3 help pages (of 5809 indexed) for 'mise en plan section':

---
Title: Vue de coupe
Lang: FR  Domain: Cad
Path: help-md/FR/Cad/Drafting/UI/Views/Sections/...md
Excerpt: ... crée une vue de [coupe] à partir de la vue principale de la mise en plan ...
```

## topsolid_search_commands (v1.6.3+)

Recherche dans les **2428 commandes UI** de TopSolid (Layer 2 du graphe). Complement a `topsolid_api_help` qui couvre l'API Automation : ici on cherche les commandes TopSolid accessibles depuis les menus et rubans.

```json
{
  "name": "topsolid_search_commands",
  "arguments": { "query": "mise en plan", "max_results": 5 }
}
```

Retourne : nom de la commande, FullName, domaine, description. Index charge depuis `data/commands-catalog.json`.

**Sans TopSolid connecte.**

## Recettes batch PDM (v1.6.7+)

Les operations batch PDM sont exposees comme **recettes** via `topsolid_run_recipe`, pas comme outils dedies. C'est le pattern recommande — uniforme, coherent avec le reste du catalogue.

### batch_set_designation

Applique la meme designation (Description PDM) a **tous** les documents du projet courant.

```json
{ "name": "topsolid_run_recipe", "arguments": { "recipe": "batch_set_designation", "value": "Bride de fixation" } }
```

Reponse type :
```
OK: Designation set to 'Bride de fixation' on 12 document(s).
```

### batch_set_reference

Applique la meme reference (PartNumber PDM) a tous les documents du projet courant.

```json
{ "name": "topsolid_run_recipe", "arguments": { "recipe": "batch_set_reference", "value": "BRD-001" } }
```

### batch_set_manufacturer

Applique le meme fabricant a tous les documents du projet courant.

```json
{ "name": "topsolid_run_recipe", "arguments": { "recipe": "batch_set_manufacturer", "value": "Acme Corp" } }
```

::: tip Proprietes unitaires et utilisateur
- Pour modifier la designation d'un seul document : recette `set_designation`
- Pour les proprietes utilisateur : recette `set_user_property`
- Pour lister les documents du projet : recette `list_project_documents` ou `list_folder_documents`
:::
