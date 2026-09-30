---
type: skill-reference
scope: topsolid-mcp
domain: gabarits-et-recipes
version: 1.0.0
created: 2026-09-30
sources:
  - server/src/Tools/RecipeTool.cs (wrappers R/RW/RD l.2617-2630, catalogue verbatim)
  - server/src/Tools/ExecuteScriptTool.cs (contrat de script, verbatim)
  - server/data/api/7.21.304.0/methods.json (1978 methodes)
  - skills/topsolid-mcp/SKILL.md v5.0.0 (323 lignes)
  - data/chm-7.21/extracted-text/TopSolidDesign_Automation_Guide.txt
---

# Créer une recipe serveur MCP — patron C# et décomposition besoin → séquence

## 1. Architecture du catalogue (vérifiée dans RecipeTool.cs)

- 132 recipes, 3 modes : R (Read, 88), RW (WritePdm, 37), RD (WriteDisk, 7), réparties en 17 categorie
  (PDM PROPERTIES, PROJECTS, DOCUMENT, PARAMETERS, GEOMETRY, ASSEMBLIES, FAMILIES, DRAFTING, BOM,
  UNFOLDING, EXPORT, USER PROPERTIES, AUDIT, MEASUREMENTS, MATERIALS, BATCH, ATTRIBUTES) stocké dans
  un Dictionary<string, RecipeEntry>. `[REC]`
- Wrappers (l.2617-2630 verbatim) :
  - `R(category, description, code)` → `RecipeEntry(category, description, code, RecipeEntry.RecipeMode.Read)`
  - `RW(category, description, code)` → `RecipeEntry.RecipeMode.WritePdm` (transactionnel : passe par
    le pipeline StartModification/EndModification + EnsureIsDirty)
  - `RD(category, description, code)` → `RecipeEntry.RecipeMode.WriteDisk` (écrit sur disque, pas PDM —
    exports, impression)
- Enregistrement : le catalogue n'est PAS inliné dans les descriptions des outils —
  topsolid_list_recipes sert le catalogue à la demande.
  Registry : topsolid_run_recipe (input : recipe, value), topsolid_list_recipes, topsolid_get_recipe. `[REC]`
- Outils MCP complets (noms exacts, server/src) : topsolid_run_recipe, topsolid_list_recipes,
  topsolid_get_recipe, topsolid_get_state, topsolid_compile, topsolid_execute_script,
  topsolid_modify_script, topsolid_api_help, topsolid_explore_paths, topsolid_find_path,
  topsolid_search_commands, topsolid_search_examples, topsolid_search_help, topsolid_whats_new. `[REC]`

## 2. Patron de recipe C#

### 2.1 Lecture (R)

```csharp
{ "read_real_parameter", R("PARAMETERS", "Reads a real parameter by name. Param: value=name",
    "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
    "if (docId.IsEmpty) return \"No document open.\";\n" +
    "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
    "foreach (var p in pList)\n" +
    "{\n" +
    "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
    "    if (name.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
    "    {\n" +
    "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
    "        return name + \" = \" + val.ToString(\"F6\") + \" (SI)\";\n" +
    "    }\n" +
    "}\n" +
    "return \"Parameter '{value}' not found.\";") },
```
- Contrat R : code => string (retour affiché). `{value}` = paramètre d'appel (substitution runtime).
  GetFriendlyName pour apparier le paramètre à son libellé convivial. Valeurs affichées SI brutes
  (mm -> *1000, deg -> *180/PI si conversion voulue). `[REC]`

### 2.2 Écrans + écriture PDM (RW)

```csharp
{ "set_real_parameter", RW("PARAMETERS", "Sets a real parameter. Param: value=name:SIvalue (e.g. Length:0.15)",
    "string[] parts = \"{value}\".Split(':');\n" +
    "if (parts.Length != 2) { __message = \"Format: name:SIvalue (e.g. Length:0.15)\"; return; }\n" +
    "string pName = parts[0].Trim();\n" +
    "double newVal;\n" +
    "if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out newVal))\n" +
    "    { __message = \"Invalid value: \" + parts[1]; return; }\n" +
    "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
    "foreach (var p in pList)\n" +
    "{\n" +
    "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
    "    if (name.IndexOf(pName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
    "    {\n" +
    "        TopSolidHost.Parameters.SetRealValue(p, newVal);\n" +
    "        __message = \"OK: \" + name + \" → \" + newVal.ToString(\"F6\");\n" +
    "        return;\n" +
    "    }\n" +
    "}\n" +
    "__message = \"Parameter not found: \" + pName;") },
```
- Contrat RW : code => void ; signaler le résultat via `__message = "..."` + `return`.
  docId est fourni/rafraichi par le wrapper (EnsureIsDirty) — ne pas re-déclarer docId dans un code RW,
  contrairement à R où la 1re ligne le déclare : `DocumentId docId = TopSolidHost.Documents.EditedDocument;`.
  `[REC]`
- Validation d'entrée en tête de code (split/format/TryParse InvariantCulture) AVANT toute écriture. `[REC]`
- RW = le PDM est modifié → tout ce qui modifie paramètres/entités/nom de document. N'incline jamais
  une recipe R vers RW par facilité : le mode conditionne la transaction. `[REC]`

### 2.3 Écriture disque (RD)

```csharp
{ "export_step", RD("EXPORT", "Exports to STEP. Param: value=path (e.g. C:\\temp\\part.stp)",
    "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
    "if (docId.IsEmpty) return \"No document open.\";\n" +
    "int count = TopSolidHost.Application.ExporterCount;\n" +
    "int idx = -1;\n" +
    "for (int i = 0; i < count; i++)\n" +
    "{\n" +
    "    string typeName; string[] extensions;\n" +
    "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
    "    foreach (string ext in extensions)\n" +
    "        if (ext.ToLower().Contains(\"stp\") || ext.ToLower().Contains(\"step\")) { idx = i; break; }\n" +
    "    if (idx >= 0) break;\n" +
    "}\n" +
    "// ... securite idx, chemin depuis {value}, exporter.Execute ... (pipeline RD)")
```
- Contrat RD : écrit sur DISQUE (export_step, export_dxf, export_pdf, export_stl, export_iges,
  batch_export_step, print_drafting). Détails verbatim :
  - l'exporter est cherché par extension : GetExporterFileType(i, out typeName, out extensions) sur
    ExporterCount ; match "stp"/"step" (insensible casse) ; ensuite un appel Export est exécuté par le
    pipeline. `[REC]`
  - print_drafting : Draftings.IsDrafting(docId) (try/catch), GetPageCount, puis Draftings.Print(docId,
    PrintMode.PrintToScale, PrintColorMapping.BlackAndWhite, 300, 1, pages). `[REC]`
- Aucune transaction PDM : un RD ne doit pas appeler StartModification. `[REC]`

### 2.4 Règles transverses (issues du code vérifié)

- `{value}` est substitué tel quel dans le code — PAS de protection injection : un `value` malveillant
  (") peut casser le code C# compilé. Les recipes existantes valident le format en tête (Split/TryParse)
  et retournent un message d'erreur clair. Toute nouvelle recipe DOIT valider `{value}` avant usage. `[REC]`
- Conversion unités : les recipes acceptent des mm/hz en entrée et convertissent SI (*0.001 pour m). Ne
  renvoie jamais de valeur "à deviner" : l'agent doit lire la description de la recipe (format du
  Param). `[REC]`
- Ne JAMAIS mettre un appel TopSolid non vérifié dans une recipe : vérifier l'existence de la méthode
  dans server/data/api/<version>/methods.json (champ since) avant de l'utiliser. `[T-1206]`
- Format de script (verbatim ExecuteScriptTool.cs) : «Method body ONLY (no using/namespace/class),
  C# 5 - no string interpolation, use string.Format; end with return "..."». Usings fixés par le
  wrapper : System, System.Collections.Generic, System.Linq, System.Text, System.IO,
  TopSolid.Kernel.Automating, TopSolid.Cad.Design.Automating. Les 132 codes du catalogue suivent ce
  contrat (aucun using/namespace/class dans les codes vérifiés). `[REC]`
- topsolid_execute_script : «NOT sandboxed and NOT read-only: it runs fully trusted and is
  auto-wrapped in a write transaction when it mutates. Do not auto-approve.» → valider chaque appel
  par topsolid_api_help AVANT, jamais auto-approbation. `[REC]`

## 3. Décomposition besoin → gabarit + paramètres (méthodo)

Contexte contraignant (verifié) : l'API Automation ne crée PAS la géométrie A-Z. Seules primitives
vérifiées disponibles : CreateSketchIn2D/In3D, CreatePoint/CreateLineSegment/CreateProfile,
CreateExtrudedShape/CreateLoftedShape/CreateRevolvedShape, et les entités de référence
(point/axe/plan/repère). Tout le reste = gabarits. `[T-1206] [API-7.21] [REC]`

### 3.1 Arbre de décision

1. Le besoin est-il couvert par une recipe existante ? topsolid_list_recipes + lecture des 17
   categories (PDM, PROJECTS, DOCUMENT, PARAMETERS, GEOMETRY, ASSEMBLIES, FAMILIES, DRAFTING, BOM,
   UNFOLDING, EXPORT, USER PROPERTIES, AUDIT, MEASUREMENTS, MATERIALS, BATCH, ATTRIBUTES). `[REC]`
2. Couvert par une primitive API (esquisse+profil+extrusion) && forme simple ? → chaîne
   creer_esquisse_rectangle / create + extruder_esquisse puis set_real_parameter. Sinon → gabarit. `[REC]`
3. Gabarit : identifier les DDL (paramètres qui pilotent la forme) et les lire :
   read_parameters, read_operations (arbre), read_shapes, list_sketches. `[REC]`
4. Écrire la séquence de pilotage : get_state -> read (baseline) -> set (paramètres SI) ->
   rebuild_document -> relire (read_mass_volume/read_part_dimensions/audit_part) -> save_document. `[REC]`
5. Si le besoin exige une méthode API absente des recipes : créer la recipe (section 2) via
   issue + PR (processus release repo MCP).

### 3.2 Exemple canon "équerre à N lumières" (chaîne vérifiée)

- Périmètre vérifié : ce qui suit utilise UNIQUEMENT des recipes existantes. Le perçage des lumières
  par API (drilling) est en LECTURE seule (IFeatures GetDrilling*Primitive 7.14) — c'est le gabarit
  qui porte les lumières. `[API-7.21]`
- Gabarit : piece TopPrt avec Largeur/Hauteur/Epaisseur (Length) + NbLumieres + cotes positions en
  paramètres nommés. Lumières : esquisses+extrusions ou primitive drilling dans le gabarit (main). `[T-1206]`
- Séquence serveur (noms réels) :
  1. topsolid_list_recipes -> vérifier les noms au lieu de les supposer
  2. topsolid_get_state -> document actif, version TopSolid
  3. read_parameters (baseline)
  4. set_real_parameter (Largeur:0.12 etc.)
  5. rebuild_document
  6. read_part_dimensions + read_mass_volume (validation)
  7. export_step / export_dxf si demande
- Extension serveur (si besoins répétitifs) : une recipe RW `analyze_part`-like agregant
  parametres+dimensions+masse en un appel. Comme toujours : issue+PR, pas de recipe à la volée. `[T-1206]`

## 4. Outils d'exploration API (usage en contexte de conception)

- topsolid_find_path / topsolid_explore_paths : arpenter l'arbre API vers une capacité. `[REC]`
- topsolid_api_help / topsolid_search_help : doc d'une méthode (since, signatures) — avant tout usage
  dans une recipe. `[REC]`
- topsolid_search_examples / topsolid_search_commands : exemples/syntaxe de commandes menu. `[REC]`
- topsolid_compile / topsolid_execute_script : boucle rapide pour valider un extrait C# hors recipe. `[REC]`
- topsolid_whats_new : diff d'API par version (le serveur embarque l'équivalent du methods.json). `[REC]`
- Ordre canonique dans SKILL.md v5.0.0 : get_state -> run_recipe -> api_help -> scripts ; à conserver
  dans toute nouvelle doc. `[SKILL.md]`

## 5. Processus d'ajout d'une recipe (conforme repo)

1. Ne jamais modifier sans issue : le tray "signalement bug/feature" pré-remplit une issue GitHub —
   ou écrire l'issue manuellement (bug/feat).
2. Ajouter l'entrée dans le catalogue RecipeTool.cs (patron section 2) avec mode correct (R/RW/RD) et
   catégorie existante (pas de nouvelle catégorie sans discussion).
3. Tests : les 3 suites (`TopSolidMcpServer.Tests` 12, `TopSolidMcp.Http.Tests` 37,
   `TopSolidMcp.UpdateCheck.Tests` 10) à 100 % avant commit. NE JAMAIS committer sans tests verts
   (règle repo). Si la recipe ne touche que le catalogue string, les tests unitaires du serveur
   doivent rester verts — le runtime TopSolid n'est pas mockable localement (LY458 obligatoire pour
   le runtime réel). `[SOUL-tests]`
4. Doc : README/docs/guide si visible client (tray/CLI/HTTP) — même commit ou immédiat.
5. Conventional commit (`feat:`/`fix:`/`docs:`), CI verte avant tout push final ; release : build via
   scripts/build-release.ps1, publication VALIDATION EXPLICITE de Julien via Noemid-H obligatoire.
   `[SOUL]`

## 6. Anti-patterns (vérifiés par lecture du code)

- Inliner le catalogue dans les descripteurs MCP (coût ~1300 tokens/session — corrigé par
  topsolid_list_recipes). `[REC]`
- Retourner une valeur RW sans `__message` : le caller MCP s'attend à un message. `[REC]`
- Écrire dans une recipe RD une opération PDM (save, set) : violera le mode WriteDisk. `[REC]`
- Supposer qu'une méthode existe (ex. SetInclusionCodeAndDrivers) sans methods.json ni
  topsolid_api_help. `T-1206`
- Dupliquer la logique d'une recipe côté agent : si un besoin revient, PR de recipe (cœur = code
  serveur, pas prompt). `[T-1206]`