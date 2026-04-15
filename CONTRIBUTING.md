# Contribuer a TopSolid MCP

## Ajouter une recette

Une recette est un script C# pre-construit que le LLM peut appeler par nom via `topsolid_run_recipe`.

### 1. Ajouter la recette dans le serveur

Fichier : `server/src/Tools/RecipeTool.cs`

Chaque recette est un dictionnaire `{ "nom_recette", R("description", @"script C#") }` pour READ
ou `{ "nom_recette", RW("description", @"script C# avec {value}") }` pour WRITE.

Pattern READ :
```csharp
{ "ma_nouvelle_recette", R(
    "Description courte en francais",
    @"
    DocumentId docId = TopSolidHost.Documents.EditedDocument;
    // ... logique de lecture ...
    return ""resultat"";
    "
) }
```

Pattern WRITE (modification) — TOUJOURS suivre le pattern transactionnel :
```csharp
{ "ma_recette_write", RW(
    "Description courte en francais",
    @"
    DocumentId docId = TopSolidHost.Documents.EditedDocument;
    TopSolidHost.Application.StartModification(""Description"", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // *** docId a potentiellement CHANGE ici ***
    // ... modifications avec le nouveau docId ...
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return ""OK"";
    "
) }
```

Le parametre `{value}` est remplace automatiquement par l'argument utilisateur.

### 2. Ajouter la recette dans la documentation

Fichier : `docs/.vitepress/theme/components/RecipeTable.vue`

Ajouter une entree dans le tableau `recipes` :
```javascript
{ name: "ma_nouvelle_recette", description: "Description fonctionnelle detaillee en francais", mode: "READ", category: "MaCategorie", api: "Interface.Methode" },
```

Categories existantes : PDM, Navigation, Parametres, Geometrie, Esquisse, Assemblage, Materiau, Physique, Attribut, Mise en plan, Export, Comparaison, Audit, Famille, Etat

### 3. Tester

Les tests necessitent **TopSolid 7 actif** avec le document de test ouvert (voir `tests/TestDocument.md`).

#### Prerequis

1. TopSolid 7.15+ ouvert
2. Le projet **"Test MCP"** importe, avec le document **"Test 01"** en edition
3. Automation active (Outils > Options > General > Automation, port 8090)
4. Le serveur MCP doit etre compile (`cd server && dotnet build`)

#### Lancer toute la suite (72 tests)

```powershell
cd tests
.\run-tests.ps1
```

Le script lance `TopSolidMcpServer.exe` en arriere-plan, envoie les requetes JSON-RPC depuis `TestSuite.json`, et verifie les assertions (contains / not_contains).

#### Tester les recettes en LIVE (113 recettes)

```powershell
cd tests
.\test_recipes_live.ps1
```

Ce script appelle chaque recette via `topsolid_run_recipe` et verifie que le resultat ne contient pas d'erreur. Resultat attendu : 59/61+ PASS (certaines recettes contextuelles necessitent un document specifique).

#### Ajouter un test pour une nouvelle recette

Dans `TestSuite.json`, ajouter une entree :

```json
{
  "id": "T-XX",
  "name": "ma_nouvelle_recette retourne un resultat",
  "tool": "topsolid_run_recipe",
  "request": {
    "jsonrpc": "2.0",
    "id": 100,
    "method": "tools/call",
    "params": {
      "name": "topsolid_run_recipe",
      "arguments": { "recipe": "ma_nouvelle_recette" }
    }
  },
  "assertions": {
    "not_contains": ["Erreur", "Exception", "non trouvee"]
  },
  "baseline_ms": 2000
}
```

#### Document de test de reference

Le fichier `tests/TestDocument.md` decrit le document TopSolid attendu par les tests : nom, type, parametres, esquisses, valeurs attendues. Tous les tests sont calibres sur ce document.

### 4. Conventions

- Noms de recettes : `verbe_objet` en francais (ex: `lire_masse_volume`, `modifier_designation`)
- Descriptions : en francais, langage metier TopSolid
- Code : en anglais (variables, commentaires inline)
- Toujours retourner un string (jamais void)
- Toujours gerer le cas "pas de document actif" avec un message clair

### 5. Build

```bash
# Serveur MCP
cd server && dotnet build TopSolidMcpServer.sln

# Documentation
cd docs && npm install && npm run dev
```
