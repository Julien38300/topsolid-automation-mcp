# Outils MCP

Le serveur expose 7 outils au protocole MCP. L'agent IA les appelle via JSON-RPC sur stdin/stdout.

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

## topsolid_run_recipe

Execute une recette pre-construite par nom. Concu pour les **petits modeles (3B)** comme ministral-3b : le LLM choisit un nom de recette, le serveur MCP execute le code C# correspondant. Aucune generation de code requise cote agent.

**75 recettes disponibles** couvrant 6 categories :

| Categorie | Nb | Exemples |
|-----------|----|----------|
| Attributs (PDM, parametres) | 8 | `lire_designation`, `modifier_nom`, `lire_parametres` |
| Audit / verification | 4 | `type_document`, `diagnostiquer_esquisse`, `lire_proprietes_pdm` |
| Performance (geometrie, assemblages) | 3 | `lire_faces`, `lister_inclusions`, `collisions_assemblage` |
| Batch operations | 4 | `copier_parametres`, `renommer_batch`, `sync_documents` |
| Interactive selection (IUser.Ask*) | 3 | `ask_select_document`, `ask_select_element`, `ask_select_face` |
| Export formats | 7 | `exporter_step`, `exporter_dxf`, `exporter_pdf`, `exporter_ifc`, `exporter_fbx`, `exporter_gltf`, `exporter_parasolid` |

Deux patterns d'execution :

- **Auto** : le serveur execute la recette directement (ex: `lire_designation`)
- **Interactive** : la recette utilise `IUser.Ask*` pour demander une selection a l'utilisateur dans TopSolid (ex: `ask_select_document`)

```json
{
  "name": "topsolid_run_recipe",
  "arguments": {
    "recipe": "modifier_designation",
    "params": { "value": "Piece Test Noemid" }
  }
}
```

::: tip Integration Hermes
Avec un modele 3B, `run_recipe` est le seul outil necessaire. Le modele n'a pas besoin de generer du C# — il choisit simplement la bonne recette parmi les 75 disponibles. Teste e2e avec ministral-3:3b en 4 secondes.
:::
