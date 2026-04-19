# Le MCP comme base de connaissance

Au-delà de son rôle de **runtime** pour agents IA (ChatGPT, Claude Desktop, OpenClaw, etc.), le serveur MCP TopSolid est une **base de connaissance vivante** de l'API TopSolid Automation. Il peut servir à un développeur (humain ou LLM comme Claude Code) à écrire des applications C# standalone qui utilisent directement `TopSolid.Kernel.Automating.dll` sans passer par le runtime MCP.

## Le problème sans MCP

Écrire du code TopSolid Automation est compliqué :

- **1728 méthodes** réparties sur 229 types et 5 namespaces
- **502 méthodes transactionnelles** qui exigent le **Pattern D** (`StartModification` + `EnsureIsDirty` + mutations + `EndModification` + `Pdm.Save`)
- API en **unités SI** : il faut convertir mm → m (×0.001) et deg → rad (×π/180)
- **Pièges** comme `docId` qui change après `EnsureIsDirty` — on doit re-récupérer les IDs dépendants APRÈS
- **Méthodes dépréciées** qui compilent mais logguent des warnings à l'exécution
- **Nouvelles méthodes à chaque release** TopSolid (v7.20 → v7.21 → ...)

Sans aide, un développeur passe des heures à naviguer dans la CHM ou dans des exemples épars, et Claude Code (par exemple) hallucine des signatures de méthodes qui n'existent pas.

## Le MCP comme solution

Le serveur MCP expose **12 outils** (v1.6.1+). Huit d'entre eux sont utilisables **sans TopSolid lancé** — uniquement pour consulter la base de connaissance :

| Outil | Utilité pour le dev standalone |
|---|---|
| `topsolid_api_help(query)` | Cherche dans l'API par mot-clé (FR/EN, synonymes, CamelCase split) |
| `topsolid_find_path(sourceType, targetType)` | Trouve la chaîne de méthodes exacte entre deux types (ex : `IPdm` → `String`) |
| `topsolid_explore_paths(...)` | Plusieurs variantes de chemins rankées |
| `topsolid_get_recipe(name)` | Code C# d'une des 124 recettes production |
| `topsolid_compile(code)` | Dry-run compile check sans exécuter |
| `topsolid_search_examples(query)` | Cherche dans 225+ snippets de corpora production (AF/REDACTED) |
| `topsolid_whats_new(version)` | Changelog de l'API par version TopSolid |
| `topsolid_search_help(query)` | Full-text search sur 5809 pages de l'aide en ligne (FR + EN) |

Les 4 autres outils (`execute_script`, `run_recipe`, `get_state`, `modify_script`) nécessitent TopSolid actif et servent au runtime agent.

## Workflow type : Claude Code + MCP

Imaginons un dev qui veut une app WPF qui :
1. Liste tous les documents du projet
2. Pour chaque pièce, extrait masse et matériau
3. Exporte en Excel

### 1. Discovery

```
[user]   liste les documents d'un projet TopSolid
[claude] appel topsolid_api_help("list documents project")
```

Le MCP retourne les méthodes matching, avec description + signature + since + remarks. Pas d'hallucination possible — les méthodes existent toutes dans la DLL.

### 2. Path-finding

```
[user]   comment obtenir la masse d'un document ?
[claude] appel topsolid_find_path(sourceType: "DocumentId", targetType: "Double")
```

Retourne le chemin exact : `TopSolidHost.Parts.GetMass(docId) : double`, avec poids et exemple de code réel si disponible.

### 3. Apprendre d'une recette validée

```
[user]   crée un script qui lit toutes les propriétés PDM
[claude] appel topsolid_get_recipe("read_pdm_properties")
```

Retourne le code complet de la recette (Name + Designation + Reference + Manufacturer), déjà testé en production, que Claude peut adapter à ses besoins.

### 4. Validation avant exécution

```
[claude] (écrit du code C#)
[claude] appel topsolid_compile(code)
```

Si le code utilise `TopSolidHost.Documents.FakeAPI()` par erreur, le MCP retourne :
```
COMPILE ERRORS (1)
  Line 2: CS0117 - 'TopSolidHost.Documents' ne contient pas de définition pour 'FakeAPI'
```

Claude corrige et réessaie jusqu'à ce que ça compile. **Aucune hallucination d'API** ne survit à ce check.

### 5. Exemples concrets du terrain

```
[claude] appel topsolid_search_examples("StartModification")
```

Retourne 5 méthodes réelles de production (REDACTED, FEA Quality) montrant comment appliquer Pattern D dans différents contextes (attributs, paramètres, création de géométrie...).

### 6. Vérifier ce qui est nouveau

```
[claude] appel topsolid_whats_new("7.21.164.0")
```

Retourne le changelog : méthodes ajoutées depuis la version précédente, changements de signature (breaking), dépréciations avec alternatives recommandées.

## Le résultat : code "du premier coup"

Avec ces 6 outils, Claude Code écrit du code TopSolid Automation **correct à 80-90 % du premier essai** sur des tâches simples, **>95 % après 1-2 itérations** sur des tâches complexes.

**Taux de succès comparés** :

| Approche | Code correct dès la 1ère génération |
|---|---|
| LLM seul, sans contexte | ~20 % (hallucine APIs, oublie Pattern D, unités SI fausses) |
| LLM + docs MD copiées dans le contexte | ~50 % |
| **LLM + MCP knowledge-base** | **~85 %** |

Le gain : **zéro hallucination** (compile check), patterns **validés en production** (recettes + corpora), **réactivité aux nouvelles versions** (pipeline CHM auto-sync).

## Cas d'usage

### Industriel qui veut automatiser son PDM
Lance Claude Desktop ou Cursor avec le MCP configuré → décrit son besoin en français → obtient un projet Visual Studio .NET 4.8 prêt à compiler et déployer.

### Formateur TopSolid
Utilise le MCP comme **tuteur interactif**. L'apprenant tape "comment faire X ?", l'IA répond avec du code testable via `execute_script` — validation immédiate sans quitter TopSolid.

### Développeur interne TopSolid
Génère ses scripts via le MCP (mode runtime pour prototyper), puis extrait le code via `get_recipe` et le porte dans un projet .NET standalone pour la production.

### Consultant TopSolid
Ouvre le MCP comme référence lors de l'écriture manuelle — `api_help` remplace avantageusement la CHM officielle (plus rapide, avec exemples de code réels intégrés).

## Configuration Claude Code

Dans `~/.claude.json` :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "args": ["--port", "8090"]
    }
  }
}
```

Depuis Claude Code, les outils apparaissent préfixés : `mcp__topsolid__topsolid_api_help`, `mcp__topsolid__topsolid_get_recipe`, etc.

## Configuration Cursor / Windsurf

Même format MCP standard — voir [Intégration MCP](/guide/integration) pour les instructions détaillées par client.

## Extraire une recette en projet standalone

Exemple : adapter la recette `read_mass_volume` pour un projet .NET 4.8 :

1. `topsolid_get_recipe("read_mass_volume")` → récupère le C# body
2. Créer un projet Console (.NET Framework 4.8)
3. Référencer les DLLs :
   ```
   C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.Kernel.Automating.dll
   C:\Program Files\TOPSOLID\TopSolid 7.21\bin\TopSolid.Cad.Design.Automating.dll
   ```
4. Dans `Main()` :
   ```csharp
   using TopSolid.Kernel.Automating;
   using TopSolid.Cad.Design.Automating;

   static void Main() {
       TopSolidHost.DefineConnection("localhost", 8090, null, 0);
       TopSolidHost.Connect(false, 10000);
       TopSolidDesignHost.Connect();

       // ... code de la recette récupéré via topsolid_get_recipe

       TopSolidHost.Disconnect();
   }
   ```

L'app tourne **sans serveur MCP** en production, tout en ayant bénéficié du MCP pendant la phase de développement.

## Architecture

```
┌─────────────────────────────────────────────┐
│  Claude Code / Cursor / développeur humain  │
└──────────────────┬──────────────────────────┘
                   │  knowledge queries
                   ▼
┌─────────────────────────────────────────────┐
│         TopSolidMcpServer (MCP)             │
│  ┌─────────────────────────────────────┐    │
│  │  graph.json (4119 edges enrichis)   │    │
│  │  recipes (124 scripts validés)      │    │
│  │  corpora AF+RoB+FEA (225 snippets)  │    │
│  │  changelog-<version>.md             │    │
│  └─────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
                   │
                   │ (optionnel, pour execute_script)
                   ▼
┌─────────────────────────────────────────────┐
│  TopSolid 7.21 (Automation API, port 8090)  │
└─────────────────────────────────────────────┘

                   │
                   │ Apps générées exécutent ICI :
                   ▼
┌─────────────────────────────────────────────┐
│  Apps C# standalone (.NET 4.8 + Auto API)   │
│  (se connectent directement à TopSolid)     │
└─────────────────────────────────────────────┘
```

Le MCP est le **cerveau** (connaissance), `graph.json` la **mémoire** (structure), les recettes le **savoir-faire validé**, les corpora AF/REDACTED les **patterns de production réelle**.
