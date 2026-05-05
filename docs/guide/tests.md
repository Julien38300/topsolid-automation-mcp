# Tests

Le projet inclut une suite de tests automatises qui verifient que le serveur MCP et les 132 recettes fonctionnent correctement contre une instance TopSolid vivante.

## Architecture de test

```
tests/
├── run-tests.ps1           ← Lance la suite complete (72 tests)
├── test_recipes_live.ps1   ← Teste les 132 recettes en LIVE
├── TestSuite.json          ← Definition des 72 tests (JSON-RPC)
├── TestSuite_Drafting.json ← Tests specifiques mise en plan
├── TestDocument.md         ← Reference du document de test attendu
├── McpTestRunner.csproj    ← Runner .NET (compile automatiquement)
├── TestRunner.cs           ← Moteur d'execution
├── Program.cs              ← Point d'entree
├── Models.cs               ← Classes de donnees
└── Baselines.json          ← Baselines de performance
```

## Comment ca marche

Le test runner :

1. **Lance** `TopSolidMcpServer.exe` en arriere-plan (stdin/stdout)
2. **Envoie** la requete MCP `initialize` (handshake JSON-RPC)
3. **Envoie** chaque test depuis `TestSuite.json` (requete `tools/call`)
4. **Verifie** la reponse avec des assertions :
   - `contains` : le texte de retour doit contenir ces mots
   - `not_contains` : le texte ne doit PAS contenir ces mots (ex: "Erreur de compilation")
   - `pattern` : regex optionnel
5. **Mesure** le temps de reponse et compare aux baselines

Resultat : PASS / FAIL pour chaque test, avec le temps en ms.

## Prerequis

1. **TopSolid 7.15+** ouvert
2. **Automation activee** (Outils > Options > General > Automation, port 8090)
3. Le projet **"Test MCP"** ouvert, avec le document **"Test 01"** en edition
4. Le serveur compile : `cd server && dotnet build TopSolidMcpServer.sln`

::: warning Document de test obligatoire
Les tests sont calibres sur un document precis decrit dans `tests/TestDocument.md` : un assemblage nomme "Test 01", avec 3 esquisses, 5 parametres utilisateur et des proprietes PDM specifiques. Sans ce document, la plupart des assertions echoueront.
:::

## Lancer les tests

### Suite complete (72 tests)

```powershell
cd tests
.\run-tests.ps1
```

Le script compile automatiquement le McpTestRunner si necessaire, puis execute tous les tests de `TestSuite.json`.

Exemple de sortie :
```
T-00  setup projet et document de test        PASS    142ms
T-01  get_state connexion + version           PASS     87ms
T-02  get_state document actif                PASS     91ms
T-03  api_help sketch                         PASS    203ms
...
TOTAL: 68/72 PASS (4 FAIL perf = bruit)
```

### Tests recettes LIVE (132 recettes)

```powershell
cd tests
.\test_recipes_live.ps1
```

Ce script appelle **chaque recette** via `topsolid_run_recipe` et verifie que le resultat ne contient pas d'erreur. C'est le test le plus complet — il valide que toutes les recettes compilent et s'executent sans exception.

Resultat attendu : **59/61+ PASS** (certaines recettes contextuelles necessitent un type de document specifique — piece, mise en plan, famille).

### Options avancees

```powershell
# Mode debug (affiche les requetes/reponses)
.\run-tests.ps1 -Debug

# Mettre a jour les baselines de performance
.\run-tests.ps1 -UpdateBaselines

# Utiliser un chemin specifique vers le serveur
.\run-tests.ps1 -McpServer "C:\MonChemin\TopSolidMcpServer.exe"
```

## Anatomie d'un test

Chaque test dans `TestSuite.json` suit cette structure :

```json
{
  "id": "T-07",
  "name": "execute_script simple (Version)",
  "tool": "topsolid_execute_script",
  "request": {
    "jsonrpc": "2.0",
    "id": 7,
    "method": "tools/call",
    "params": {
      "name": "topsolid_execute_script",
      "arguments": {
        "code": "return TopSolidHost.Version.ToString();"
      }
    }
  },
  "assertions": {
    "contains": ["7"],
    "not_contains": ["Erreur de compilation"]
  },
  "baseline_ms": 500
}
```

| Champ | Role |
|-------|------|
| `id` | Identifiant unique (T-00, T-01, ...) |
| `name` | Description lisible du test |
| `tool` | Outil MCP teste |
| `request` | Requete JSON-RPC exacte envoyee au serveur |
| `assertions.contains` | Textes qui DOIVENT apparaitre dans la reponse |
| `assertions.not_contains` | Textes qui ne doivent PAS apparaitre |
| `baseline_ms` | Temps de reference — FAIL si > 2x la baseline |

## Ajouter un test

Pour tester une nouvelle recette, ajoutez une entree dans `TestSuite.json` :

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

::: tip Conventions
- Les IDs suivent un ordre croissant (T-00, T-01, ..., T-99)
- Le test T-00 est toujours le setup (verifie que le bon document est ouvert)
- Les tests READ n'ont pas besoin de cleanup
- Les tests WRITE doivent restaurer l'etat initial si possible
:::

## Categories de tests

| Categorie | Tests | Ce qu'ils verifient |
|-----------|-------|---------------------|
| **Setup** | T-00 | Document de test ouvert et pret |
| **Connexion** | T-01, T-02 | get_state retourne version + document actif |
| **api_help** | T-03 a T-06, T-81 a T-86 | Recherche par mot-cle FR/EN, synonymes, fallback |
| **execute_script** | T-07 a T-13 | Compilation C# 5, patterns LLM, lectures simples |
| **find_path** | T-14, T-15 | Dijkstra entre types API |
| **explore_paths** | T-16, T-17 | BFS multi-chemins |
| **Parametres** | T-18 a T-25 | Lecture/ecriture parametres (Real, Boolean, Text) |
| **PDM** | T-26 a T-34 | Designation, nom, reference, fabricant (round-trip) |
| **Geometrie** | T-40 a T-45 | Esquisses, shapes, faces, points, reperes, operations |
| **Assemblage** | T-46, T-51 | Inclusions, occurrences, faces detaillees |
| **Write** | T-30 a T-39, T-50 | Modifications avec pattern transactionnel |
| **Recettes** | RA-01 a RA-61 | 61 recettes en LIVE (test_recipes_live.ps1) |

## Tests sans TopSolid

Les outils **find_path**, **explore_paths** et **api_help** fonctionnent sans TopSolid (ils utilisent uniquement le graphe en memoire). Les tests T-03 a T-06, T-14 a T-17 et T-81 a T-86 peuvent tourner sur n'importe quelle machine avec le graphe `data/graph.json`.

## Performance et regressions

Le runner compare chaque temps de reponse a la baseline enregistree. Si un test prend plus de **2x sa baseline**, il est marque FAIL (regression de performance).

Pour mettre a jour les baselines apres un changement d'infrastructure :

```powershell
.\run-tests.ps1 -UpdateBaselines
```

Les baselines sont stockees dans `Baselines.json`.
