# Outils MCP

Le serveur expose 6 outils au protocole MCP. L'agent IA les appelle via JSON-RPC sur stdin/stdout.

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
