# Outils MCP

Le serveur expose **15 outils** au protocole MCP. L'agent IA les appelle via JSON-RPC — sur stdin/stdout (mode stdio) ou via HTTP/SSE (mode bridge).

| Outil | TopSolid requis |
|---|---|
| `topsolid_run_recipe` | oui |
| `topsolid_list_recipes` | non |
| `topsolid_get_state` | oui |
| `topsolid_detect_dialog` | oui |
| `topsolid_compare_documents` | oui |
| `topsolid_execute_script` | oui |
| `topsolid_modify_script` | oui |
| `topsolid_api_help` | non |
| `topsolid_find_path` | non |
| `topsolid_explore_paths` | non |
| `topsolid_get_recipe` | non |
| `topsolid_compile` | non |
| `topsolid_search_examples` | non |
| `topsolid_search_help` | non |
| `topsolid_search_commands` | non |

::: warning `--read-only`
Lance avec `--read-only` (ou `TOPSOLID_MCP_READ_ONLY=1`) et `topsolid_modify_script` n'est pas enregistre du tout : il n'apparait pas dans `tools/list`, et les recettes tournent en mode lecture seule. Voir [Options de lancement](./quickstart#options-de-lancement-et-variables).
:::

## topsolid_run_recipe

**L'outil principal.** Execute une recette pre-construite par nom. Le LLM n'a pas besoin de generer du code C# — il choisit juste le nom de la recette.

**224 recettes** disponibles couvrant : PDM, parametres, masse/volume, assemblages, export (6 formats), mise en plan, nomenclature, mise a plat, comparaison de documents, report de modifications, audit batch, familles, creation de geometrie (parametres Smart, esquisses, extrusions, inclusions).

Le catalogue complet se consulte avec l'outil `topsolid_list_recipes` (section ci-dessous) — il n'est plus inline dans le descripteur de cet outil.

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
- `run_recipe` : pour les 216 operations pre-definies (rapide, fiable, pas besoin de C#)
- `execute_script` : pour du C# custom que le LLM genere a la volee (plus flexible, plus risque)

Un modele 3B (ex: `ministral-topsolid`) peut utiliser `run_recipe`. Seul un modele 24B+ (ex: `codestral:22b`) peut utiliser `execute_script` correctement.
:::

## topsolid_list_recipes

Catalogue des recettes : retourne une ligne par recette (mode `[READ]` / `[WRITE-PDM]` / `[WRITE-DISK]` / `[WRITE-PDM-NO-TRX]`, categorie, nom, description), filtrable par categorie et/ou mot-cle. C'est l'outil a appeler pour trouver le nom exact a passer a `topsolid_run_recipe`.

La liste n'est plus inlinee dans le descripteur de `topsolid_run_recipe` (elle coutait ~1300 tokens a chaque demarrage de session) : elle est servie a la demande par cet outil.

**Parametres** (tous optionnels) :

| Parametre | Type | Defaut | Role |
|---|---|---|---|
| `category` | string | — | Filtre par categorie (`EXPORT`, `DRAFTING`, `PDM PROPERTIES`...) |
| `search` | string | — | Mot-cle cherche dans le nom **et** la description |
| `max_results` | integer | 30 | Nombre de lignes retournees, plafonne a 100 |

```json
{ "name": "topsolid_list_recipes", "arguments": { "category": "EXPORT", "max_results": 20 } }
```

Le nombre total de correspondances est toujours annonce, meme quand l'affichage est tronque par `max_results` — relancez avec un `search` plus precis ou un `max_results` plus grand pour voir le reste.

**Sans TopSolid connecte.**

## topsolid_get_state

Retourne l'etat courant : document actif, projet, connexion TopSolid.

**Usage** : premier appel pour verifier que TopSolid est connecte.

```json
{ "name": "topsolid_get_state", "arguments": {} }
```

**Reponse type** :
```
TopSolid version : 7.20.258
Automation port : 8090
Bin path : C:\Program Files\TOPSOLID\TopSolid 7.20\bin\
Modules : Design = available, Drafting = not available
Edited document : MaPiece
Type : .TopPrt
Project : MonProjet
```

La ligne `Modules :` dit quels modules Automation optionnels ont ete charges — c'est la premiere chose a regarder quand une recette de mise en plan ou de geometrie echoue (voir [Depannage](./troubleshooting)).

## topsolid_detect_dialog (v1.8.16+)

Detecte une boite de dialogue ouverte dans TopSolid (erreur, avertissement, « aucune entite a exporter », demande de confirmation...) et renvoie son titre, son texte et ses boutons. Lecture seule.

L'outil lit les fenetres de TopSolid par Windows (Win32, et UI Automation pour les fenetres WPF) et n'appelle jamais l'API Automation : il repond donc meme quand TopSolid est bloque et que les autres outils expirent. Il cible l'instance TopSolid du port connecte, sinon toutes les instances.

```json
{ "name": "topsolid_detect_dialog", "arguments": {} }
```

Exemple de reponse :

```
1 dialog box(es) open in TopSolid:

[MODAL - TopSolid is blocked until it is closed] "TopSolid" (process 15664)
  Aucune entite a exporter.
  Buttons: [OK] [Annuler]
```

Quand un script depasse son delai (60 s par defaut), le message d'erreur inclut deja ce rapport s'il trouve une boite de dialogue. L'outil ne clique pas : c'est l'utilisateur qui ferme la boite dans TopSolid.

::: warning Serveur dans une autre session Windows
Si le serveur tourne comme service ou tache planifiee hors de la session de l'utilisateur, il ne voit pas les fenetres de TopSolid : l'outil le dit au lieu de repondre « aucune boite ».
:::

## topsolid_compare_documents (v1.8.18+)

Compare deux revisions d'une piece ou d'un assemblage et donne la liste rangee de tout ce qui a change : materiau, parametres, operations (ajoutees, supprimees, deplacees, activees, devenues invalides) et geometrie (trous, faces, encombrement). Avec une `target`, il dit pour chaque changement si cet autre document l'a deja, s'il peut etre reporte automatiquement ou s'il faut le faire a la main. Avec `apply: true`, il reporte la partie automatique.

Cas d'usage : la piece A et la piece B partent de la meme base, A a evolue depuis sa revision A.0, et il faut reporter ces evolutions sur B.

| Parametre | Type | Defaut | Role |
|---|---|---|---|
| `document` | string | document edite | Nom exact du document qui a evolue, dans le projet courant |
| `from_revision` | string | `previous` | Revision de base : `A.0`, `A` (derniere mineure de A) ou `previous` |
| `to_revision` | string | courante | Revision comparee a la base |
| `target` | string | — | Nom exact du document sur lequel reporter |
| `mode` | string | `revisions` | `documents` : compare directement `document` et `target` (ce qu'il faut changer dans `target` pour ressembler a `document`) |
| `apply` | boolean | `false` | Reporte les changements `AUTO` sur `target`, en une transaction, puis sauvegarde |

```json
{ "name": "topsolid_compare_documents", "arguments": { "document": "Piece A", "target": "Piece B" } }
```

Reponse (extrait, mesuree sur TopSolid 7.20) :

```
COMPARE Piece A: A.0 → B.0   target: Piece B A.0
Summary: 4 design change(s) — 3 automatic, 1 manual, 0 already in Piece B; 12 recomputed value(s).

MATERIAL
  ~ Material: Acier → Acier S235 EN 10025   | Piece B: Acier  [AUTO]

PARAMETERS
  + Diametre = 8 mm   | Piece B: absent  [AUTO: create]
  + JeuPercage = 20 mm   | Piece B: absent  [AUTO: create]

GEOMETRY (seen on the faces: says WHAT changed, not in which operation)
  ~ Shape Forme 1: 4 × Ø10 mm (L 7 mm) → 4 × Ø8 mm (L 7 mm)   | Piece B: 4 × Ø10 mm (L 7 mm)  [MANUAL]
      cylindrical faces changed; the API cannot change an operation's data; candidates in the target: Groupe de percage 1

RECOMPUTED BY TOPSOLID (never applied)
  Masse: 0.334417 kg → 0.340632 kg
  ...
```

Ce qui est reporte automatiquement (`AUTO`) :

- le **materiau** (celui de la revision courante du document source) ;
- les **parametres simples** (reel, texte, entier, booleen) : crees s'ils manquent, mis a la valeur sinon ;
- l'**etat des operations** : activee / desactivee, gelee / degelee.

Ce qui reste a la main (`MANUAL`), avec une indication :

- le **contenu d'une operation** (diametre d'un percage, cote d'une esquisse, dimensions d'un bloc) : l'API ne le lit pas. Le changement se voit sur la geometrie (groupes de faces cylindriques par diametre, faces par type, encombrement), et l'outil nomme les operations candidates de la cible ;
- une **operation ajoutee** (a creer) ou **supprimee** (a supprimer : l'outil ne supprime jamais d'operation) ;
- un **changement d'ordre** dans l'arbre ;
- un parametre pilote par une operation (tableau, formule) ou supprime.

Jamais reportes : les valeurs **recalculees** par TopSolid (masse, volume, boite englobante, nom du brut, proprietes du materiau) et l'**identite** du document (nom, designation, revision, auteur).

Apres `apply`, l'outil relit la cible et redonne le rapport : ce qui est fait passe en `already done`, il ne reste que le `MANUAL`. Un echec annule toute la transaction. `apply` est refuse en mode lecture seule et exige la revision courante (`to_revision` vide).

::: tip Appariement des elements
Deux revisions d'un meme document gardent les memes identifiants d'elements : les operations sont appariees par identifiant. Deux documents differents ne les partagent que jusqu'a leur separation (ensuite chacun reutilise les memes numeros pour autre chose) : l'outil les apparie par type et nom.
:::

## topsolid_api_help

Recherche dans les 1728 methodes API. Supporte les noms d'interface, mots-cles FR/EN, et filtrage.

**109 synonymes FR/EN** : designation → SetDescription, reference → SetPartNumber, esquisse → Sketch, mise a plat → IUnfoldings, deplacer → Move, dossier → Folder, pdm → IPdm... Les accents sont ignores (« déplacer » = « deplacer ») et les mots vides (un, dans, the, to...) sont retires.

**Modes** :
- Interface exacte : `api_help("IParameters")` → liste complete
- Mot-cle : `api_help("designation")` → trouve SetDescription
- Filtre : `api_help("IDocuments.Export")` → methodes Export de IDocuments
- Nom de methode exact : `api_help("GetConstituents")` → toutes les interfaces qui la declarent (`IPdm` d'abord, puis `IElements`)
- Phrase : `api_help("déplacer un document dans un dossier")` → `IPdm.MoveSeveral`, avec une astuce d'usage

```json
{ "name": "topsolid_api_help", "arguments": { "query": "designation" } }
```

::: tip Classement
Les methodes qui contiennent tous les mots passent en premier. La liste est completee par les methodes qui en contiennent une partie, classees par la rarete des mots trouves (« Move » pese plus que « Document ») dans les noms, interfaces, hints semantiques et descriptions. Chaque methode n'apparait qu'une fois (30 au plus), suivie de 3 astuces d'usage au plus.
:::

**Etiquettes tirees des remarques officielles** (v1.8.17+), en fin de ligne :

| Etiquette | Sens |
|---|---|
| `[outside StartModification]` | TopSolid refuse l'appel dans une transaction (`Pdm.DeleteSeveral`, `Pdm.Save`, `Documents.Open`...) |
| `[inside StartModification + EnsureIsDirty]` | Appeler `StartModification` puis `EnsureIsDirty(ref docId)` avant |
| `[inside StartModification]` | Appeler `StartModification` avant |
| `[OBSOLETE: ...]` | Methode obsolete, avec la methode de remplacement |
| `[since 7.21]` | Methode apparue en 7.20 ou apres : absente d'un TopSolid plus ancien |

Quand le resultat liste 3 methodes au plus, la remarque officielle complete suit chaque methode (`Remarks: ...`).

## topsolid_execute_script

Compile et execute du C# 5 **hors transaction de modification** TopSolid.

```json
{
  "name": "topsolid_execute_script",
  "arguments": {
    "code": "DocumentId docId = TopSolidHost.Documents.EditedDocument;\nreturn TopSolidHost.Documents.GetName(docId);"
  }
}
```

::: danger Ce n'est pas un bac a sable
« Lecture seule » veut dire ici **hors transaction de modification TopSolid**, rien de plus. Ce n'est pas un niveau de permission.

Le code envoye est compile puis execute **dans le processus du serveur, en pleine confiance**, sur votre poste. `System.IO` reste disponible (les recettes d'export en ont besoin) : un script peut donc lire et ecrire des fichiers partout ou votre compte Windows le peut.

Une liste noire refuse quelques symboles avant compilation (`System.Diagnostics.Process`, `System.Net`, `System.Reflection`, `DllImport`, `File.Delete`, `Directory.Delete`, registre, `Environment.Exit`). C'est un garde-fou contre un modele qui derape, **pas** une frontiere de securite — et `TOPSOLID_MCP_ALLOW_UNSAFE=1` la supprime entierement.

**N'ajoutez pas `topsolid_execute_script` ni `topsolid_modify_script` a la liste des outils auto-approuves de votre client MCP.** Relisez chaque script avant de le laisser tourner. Si vous n'avez besoin que de lire, lancez le serveur avec `--read-only`.
:::

::: tip Sortie longue
Le texte renvoye est limite a 30 000 caracteres (variable `TOPSOLID_MCP_SCRIPT_MAX_OUTPUT_CHARS`). Au-dela, la **premiere ligne** l'annonce (`[OUTPUT TRUNCATED: first N of M characters shown...]`) avant les donnees : un JSON coupe ne passe donc pas inapercu. Pour un gros resultat, l'ecrire dans un fichier et renvoyer son chemin.
:::

## topsolid_modify_script

Identique a `execute_script` mais pour les **modifications**. Gere automatiquement `StartModification` / `EndModification` / `Save`.

Non enregistre quand le serveur tourne avec `--read-only` / `TOPSOLID_MCP_READ_ONLY=1`.

::: info Operations PDM et document : jamais dans une transaction
TopSolid refuse entre `StartModification` et `EndModification` les appels PDM et document : `Pdm.DeleteSeveral`, `MoveSeveral`, `CreateFolder`, `SetDescription`, `SetName`, `SetPartNumber`, `Save`, `Documents.Open`, `Close`, `Export`, `Import`... (erreur `EndModification must be called before calling this method`). La liste vient de la reference API et a ete mesuree sur TopSolid 7.20.

- Un script fait **uniquement** de ces appels s'execute sans transaction : `execute_script` le lance tel quel, `modify_script` et les recettes `RW` gardent `docId`, `pdmId`, `__message` et la sauvegarde finale, sans `StartModification`. `topsolid_compile` l'annonce : `Mode: WRITE (PDM/document calls, no transaction)`.
- Un script qui **melange** ces appels et des modifications du modele est refuse avant execution, avec la liste des appels en cause : le couper en deux appels, ou gerer soi-meme la transaction et faire ces appels apres `EndModification`. `Pdm.Save` est inutile dans un script enveloppe : le wrapper sauve deja.
:::

::: warning
`EnsureIsDirty(ref docId)` change le docId. Toujours chercher les elements APRES cet appel.
:::

::: danger
Meme modele de confiance que `topsolid_execute_script` ci-dessus : execution en pleine confiance dans le processus, pas d'auto-approbation.
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

Sans `recipe` : retourne la liste des 224 recettes avec leur mode uniquement (nom + `[READ]` / `[WRITE-PDM]` / `[WRITE-DISK]` / `[WRITE-PDM-NO-TRX]`), sans description — pour les descriptions et les filtres, passez par `topsolid_list_recipes`.

## topsolid_compile (v1.5.1+)

Compile un script C# contre les assemblies TopSolid SANS l'executer. Detecte les APIs hallucinees, erreurs de syntaxe, types manquants.

La compilation passe par `CSharpCodeProvider`, c'est-a-dire le compilateur `csc` livre avec le .NET Framework — **pas Roslyn**. Le script est donc compile en **C# 5** : l'interpolation de chaine (`$"..."`), `?.` et `nameof` sont refuses. Voir le [tableau des syntaxes interdites](./troubleshooting#erreurs-de-compilation-de-scripts).

```json
{
  "name": "topsolid_compile",
  "arguments": {
    "code": "DocumentId docId = TopSolidHost.Documents.EditedDocument; return TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docId));"
  }
}
```

Reponse : `OK: code compiles successfully. Mode: READ` ou liste d'erreurs avec numeros de ligne. Un script qui appelle lui-meme `Application.StartModification` n'est pas enveloppe : `Mode: WRITE (manual transaction, not wrapped)` (v1.8.18+, il etait annonce `READ` avant).

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

::: info `topsolid_whats_new` a ete retire
Cet outil retournait un changelog markdown genere par le pipeline `sync-topsolid-api`. Aucun changelog n'est livre avec les releases, donc l'outil ne repondait rien d'utile sur une installation standard. Il a ete supprime. Le pipeline `make sync-api` reste disponible pour qui veut generer ce diff localement.
:::

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

::: tip Resolution insensible a la casse dans `invoke_command` (v1.8.11+)
Depuis la **v1.8.11**, les 2 428 commandes de `commands-catalog.json` sont normalisees selon la casse exacte des classes C# TopSolid (`.UI.`, PascalCase comme `TopSolid.Cad.Wood.UI.ConstructionVolumes.Sketches.CreateLayoutSketchCommand`), et la recette `invoke_command` resout automatiquement la commande **quelle que soit la casse** (qu'on lui passe le `FullName` complet, une variante en minuscules ou simplement le nom court `CreateLayoutSketchCommand`).
:::

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
