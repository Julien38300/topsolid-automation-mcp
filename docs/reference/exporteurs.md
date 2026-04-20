# Exporteurs et importateurs

TopSolid expose un systeme de plugins pour les echanges de fichiers. Chaque format (STEP, DXF, PDF, glTF...) est un exporteur ou un importateur enregistre dans l'application avec un index dynamique. Ce document decrit comment les decouvrir, les parametrer et les utiliser depuis l'API Automation — directement en C# ou via les recettes MCP.

## Decouvrir les formats installes

L'index d'un exporteur n'est pas stable : il depend de la liste de plugins installes sur le poste de travail (et des licences actives). **Ne jamais hardcoder un index** — toujours le chercher a runtime par extension ou par nom.

```csharp
int total = TopSolidHost.Application.ExporterCount;
for (int i = 0; i < total; i++)
{
    if (!TopSolidHost.Application.IsExporterValid(i)) continue;   // licence manquante ?
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    Console.WriteLine($"{i}: {typeName} -> [{string.Join(", ", extensions)}]");
}
```

Le **symetrique** existe pour les importateurs : `ImporterCount`, `IsImporterValid`, `GetImporterFileType`.

## Workflow d'export

```
1. Trouver l'index par extension
     ↓
2. Verifier IsExporterValid(i)  (licence dispo)
     ↓
3. Verifier CanExport(i, docId)  (type de document compatible)
     ↓
4a. Export(i, docId, fullPath)                   ← simple
    OU
4b. ExportWithOptions(i, options, docId, path)   ← avec options
```

### Recette minimale (lookup + export)

```csharp
int idx = FindExporterIndex(".stp");
if (idx < 0) throw new Exception("Aucun exporteur STEP installe.");
if (!TopSolidHost.Application.IsExporterValid(idx)) throw new Exception("Licence STEP manquante.");
if (!TopSolidHost.Documents.CanExport(idx, docId)) throw new Exception("Ce document ne peut pas etre exporte en STEP.");
TopSolidHost.Documents.Export(idx, docId, @"C:\temp\part.stp");

int FindExporterIndex(string ext)
{
    int count = TopSolidHost.Application.ExporterCount;
    for (int i = 0; i < count; i++)
    {
        string name; string[] exts;
        TopSolidHost.Application.GetExporterFileType(i, out name, out exts);
        foreach (var e in exts)
            if (e.Equals(ext, StringComparison.OrdinalIgnoreCase)) return i;
    }
    return -1;
}
```

### Export avec options

Certains formats (FBX, glTF, STEP AP242, PDF 3D) acceptent des options cle-valeur :

```csharp
List<KeyValue> options = TopSolidHost.Application.GetExporterOptions(idx);
// options contient les defauts. Lister + inspecter pour decouvrir ce qui est dispo.
foreach (var kv in options) Console.WriteLine($"{kv.Key} = {kv.Value}");

// Modifier une option (remplacer l'entree dans la liste)
for (int i = 0; i < options.Count; i++)
{
    if (options[i].Key == "EXPORTS_CAMERAS")
        options[i] = new KeyValue("EXPORTS_CAMERAS", "True");
}

TopSolidHost.Documents.ExportWithOptions(idx, options, docId, @"C:\temp\part.gltf");
```

::: tip Options ne sont pas documentees officiellement
Les cles d'options varient par exporteur et par version de TopSolid. **Toujours lister via `GetExporterOptions()` d'abord** pour voir ce qui est expose, puis modifier. Ne pas supposer qu'une cle d'un format existe sur un autre.
:::

## Formats couramment installes

Le tableau ci-dessous est donne a titre indicatif — verifiez toujours l'installation reelle via `ExporterCount`. Les extensions peuvent etre en plusieurs variantes (`.stp` / `.step`, `.gltf` / `.glb`...).

| Format | Extension | Usage metier | Options courantes | Licence requise |
|--------|-----------|--------------|-------------------|----------------|
| **STEP AP214/AP242** | `.stp` `.step` | Echange 3D universel CAO-a-CAO. Le plus interoperable. | `WRITE_PMI`, `PROTOCOL` | Inclus |
| **IGES** | `.igs` `.iges` | Echange 3D legacy (precede STEP) | — | Inclus |
| **Parasolid** | `.x_t` `.x_b` | Echange natif avec SolidWorks, NX, Fusion | — | Inclus |
| **STL** | `.stl` | Impression 3D, maillage triangles | `RESOLUTION`, `BINARY` | Inclus |
| **DXF / DWG** | `.dxf` `.dwg` | Mises a plat toleric (laser), mises en plan 2D | `VERSION`, `LAYERS` | Inclus |
| **PDF** | `.pdf` | Diffusion mise en plan + 3D PDF | `COMPRESSION`, `LAYERS`, `EMBEDS_MODEL` (3D PDF) | Inclus |
| **FBX** | `.fbx` | Visualisation 3D, VR, moteurs jeu | `EXPORTS_CAMERAS`, `EXPORTS_LIGHTS`, `REPRESENTATION_ID` | Inclus |
| **glTF / GLB** | `.gltf` `.glb` | Web 3D, AR, digital twin | `EXPORTS_CAMERAS`, `EXPORTS_LIGHTS`, `EMBEDS_BUFFER`, `MAX_TEXTURE_SIZE` | Inclus |
| **IFC** | `.ifc` | Batiment (BIM) | `MVD`, `UNITS` | TopSolid'Steel ou TopSolid'Wood |
| **3D XML** | `.3dxml` | Diffusion Dassault Systemes | — | Inclus |
| **JT** | `.jt` | Echange Siemens (automotive, aero) | `LEVEL_OF_DETAIL` | Inclus |

## Importateurs

La mecanique est identique cote import :

```csharp
int idx = FindImporterIndex(".stp");
PdmObjectId folder = /* un dossier PDM ou stocker les resultats */;
List<string> log;
List<DocumentId> badDocs;
List<DocumentId> importedDocs = TopSolidHost.Documents.Import(idx, @"C:\temp\data.stp", folder, out log, out badDocs);

foreach (var msg in log) Console.WriteLine("[Import] " + msg);
if (badDocs.Count > 0) Console.WriteLine("WARN: " + badDocs.Count + " documents importes avec erreurs.");
```

Pour `ImportWithOptions`, la logique est identique : `GetImporterOptions(idx)` puis passer la liste modifiee.

## Recettes MCP disponibles

Les recettes actuelles couvrent les 5 formats les plus demandes :

| Recette | Format | Mode | `value` |
|---------|--------|------|---------|
| `list_exporters` | — | R | (aucun) — liste tous les formats + leurs index runtime |
| `export_step` | STEP | R | chemin de sortie (`C:\temp\piece.stp`) |
| `export_dxf` | DXF | R | chemin de sortie |
| `export_pdf` | PDF | R | chemin de sortie |
| `export_stl` | STL | R | chemin de sortie |
| `export_iges` | IGES | R | chemin de sortie |
| `export_bom_csv` | CSV (nomenclature) | R | chemin de sortie |

Ces recettes encapsulent la boucle `FindExporterIndex()` et verifient `CanExport()` avant d'appeler `Export()`. Pour un format absent de la liste (FBX, glTF, IFC...), utiliser `topsolid_execute_script` avec le pattern ci-dessus, ou demander l'ajout d'une recette dediee.

## Patterns batch

### Exporter tout un projet en STEP

```csharp
PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
var allDocs = TopSolidHost.Pdm.GetConstituents(proj, true);  // true = recursif
int idx = FindExporterIndex(".stp");

foreach (var pdmId in allDocs)
{
    string type = ""; TopSolidHost.Pdm.GetType(pdmId, out type);
    if (type != ".TopPrt" && type != ".TopAsm") continue;  // pieces + assemblages seulement
    DocumentId docId = TopSolidHost.Documents.GetDocument(pdmId);
    if (!TopSolidHost.Documents.CanExport(idx, docId)) continue;
    string name = TopSolidHost.Pdm.GetName(pdmId);
    string path = Path.Combine(@"C:\exports", name + ".stp");
    try { TopSolidHost.Documents.Export(idx, docId, path); }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
```

### Exporter une mise en plan multi-pages en PDF

La mise en plan est naturellement multi-page : l'exporteur PDF produit un seul PDF avec toutes les pages dans l'ordre.

```csharp
// Le document actif doit etre une mise en plan (IsDrafting == true)
int idx = FindExporterIndex(".pdf");
TopSolidHost.Documents.Export(idx, docId, @"C:\exports\plan.pdf");
// 3 pages -> 1 PDF de 3 pages
```

## Pieges courants

| Piege | Symptome | Solution |
|-------|----------|----------|
| Index hardcode | Export silencieusement au mauvais format apres une maj TopSolid | Toujours faire `FindExporterIndex(ext)` a runtime |
| `IsExporterValid = false` | `Export()` leve une exception peu claire | Verifier la licence du format cible ; certains formats (IFC, 3D PDF avance) necessitent des modules TopSolid specifiques |
| Chemin sans le dossier cree | Exception `DirectoryNotFoundException` | `Directory.CreateDirectory(Path.GetDirectoryName(path))` avant l'export |
| Unites melangees | STL / glTF sortent en m au lieu de mm attendus | Verifier le parametre d'unite du document source (`Document.Units`) avant export. TopSolid utilise le SI (metres) en interne |
| Export d'assemblage sans les parts | Fichier vide ou incomplet | Verifier `CanExport` + que les inclusions sont a jour (`Rebuild` avant export) |
| Caracteres non-ASCII dans le chemin | Exception ou fichier corrompu selon l'exporteur | Prefixer avec `\\?\` sur Windows, ou migrer vers un chemin ASCII sur (C:\temp) |
| Export depuis un doc dirty | Le fichier contient la version SAUVEGARDEE, pas la version en cours | `Pdm.Save(pdmId, true)` avant l'export |

## Utilisation standalone (hors MCP)

Pour un developpeur qui ecrit une app .NET Framework 4.8 TopSolid Automation directement (pas via MCP), le code est identique — il suffit d'instancier `TopSolidHost` via `TopSolidHost.Connect()`. Voir [knowledge-base.md](/guide/knowledge-base) pour le workflow complet de generation d'apps C# standalone a partir des recettes MCP.

Les recettes de ce tableau sont **self-contained** — chacune peut etre extraite en un `Run()` method C# et compilee dans un exe separe. Utiliser `topsolid_get_recipe("export_step")` pour en obtenir le code source.

## Exports speciaux non-Documents

Quelques methodes d'export ne passent pas par `IDocuments` :

| Methode | Usage |
|---------|-------|
| `IPdm.ExportPackage(pdmId, path, options)` | Package TopSolid `.TopPkg` (archive PDM complete) |
| `IPdm.ExportViewerPackage(pdmId, path, options)` | Package viewer lisible par TopSolid Viewer gratuit |
| `IPdm.ExportToBabylon(pdmId, path)` | Format Babylon.js (web 3D) |
| `IDocuments.zExportToTopglTF(...)` | Export glTF direct avec 12 parametres cameras/lights/faces/sets/materials/textureSize/edgesColor (bypasse le systeme de plugins) |

La forme avec `z` en prefixe indique une API interne "non stable" — son nom peut changer entre versions. Preferez l'exporteur glTF standard via `Export(idx)` quand possible.
