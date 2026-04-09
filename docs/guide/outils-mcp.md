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

**10 recettes disponibles** :

| Nom | Description | Pattern |
|-----|-------------|---------|
| `lire_designation` | Lire la designation du document actif | READ |
| `lire_nom` | Lire le nom du document actif | READ |
| `lire_reference` | Lire la reference (part number) | READ |
| `lire_fabricant` | Lire le fabricant | READ |
| `lire_proprietes_pdm` | Lire toutes les proprietes PDM d'un coup | READ |
| `modifier_designation` | Changer la designation | WRITE |
| `modifier_nom` | Changer le nom du document | WRITE |
| `lire_parametres` | Lister les parametres du document | READ |
| `lister_exporteurs` | Lister les formats d'export disponibles | READ |
| `type_document` | Detecter le type de document (piece, assemblage, mise en plan) | READ |

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
Avec un modele 3B, `run_recipe` est le seul outil necessaire. Le modele n'a pas besoin de generer du C# — il choisit simplement la bonne recette parmi les 10 disponibles.
:::
