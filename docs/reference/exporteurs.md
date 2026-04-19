# Exporteurs

TopSolid utilise un systeme d'exporteurs generique : chaque format est un plugin avec un index dynamique.

## Workflow d'export

```
1. ExporterCount → nombre d'exporteurs installes
2. GetExporterFileType(i) → nom du format + extensions
3. CanExport(i, docId) → verifie la compatibilite
4. Export(i, docId, path) → exporte
```

::: warning
L'index de l'exporteur change selon l'installation TopSolid. **Toujours le chercher dynamiquement** par extension.
:::

## Formats prioritaires

| Format | Extension | Usage |
|--------|-----------|-------|
| **STEP** | .stp, .step | Echange 3D universel (priorite #1) |
| **DXF** | .dxf | Mises a plat (decoupe laser), mises en plan |
| **PDF** | .pdf | Mises en plan pour diffusion |
| **IFC** | .ifc | Batiment (necessite licence Steel/Wood) |
| **Parasolid** | .x_t | Echange 3D avec NX, SolidWorks |
| **FBX** | .fbx | Visualisation 3D |
| **glTF** | .gltf, .glb | Web 3D |

## Recettes disponibles

| Recette | Format | Methode |
|---------|--------|---------|
| `export_step` | STEP | `Export` simple |
| `export_fbx` / `export_gltf` | FBX / glTF / Parasolid | `ExportWithOptions` |
| `import_step` | STEP import | `ImportWithOptions` |
| `list_exporters` | Lister tous les formats | `ExporterCount` + boucle |
| `export_dxf` | DXF | `Export` simple |
| `export_pdf` | PDF | `Export` simple |
| `export_ifc` | IFC | `Export` simple |

## Export avec options

Certains formats supportent des options (key-value) :

```csharp
// Recuperer les options par defaut
List<KeyValue> options = TopSolidHost.Application.GetExporterOptions(exporterIndex);

// Modifier une option
// options[i] = new KeyValue("REPRESENTATION_ID", "MyRep");

// Exporter avec options
TopSolidHost.Documents.ExportWithOptions(exporterIndex, options, docId, outputPath);
```

**Options connues** :
- `REPRESENTATION_ID` : pour FBX/glTF, specifie la representation
- `EXPORTS_CAMERAS` : True/False (glTF)
- `EXPORTS_LIGHTS` : True/False (glTF)
- `MAX_TEXTURE_SIZE` : taille max textures (glTF)

Les options disponibles dependent du format et sont decouvrables a runtime via `GetExporterOptions()`.

## Exports speciaux

| Methode | Usage |
|---------|-------|
| `IPdm.ExportPackage` | Package TopSolid (.TopPkg) |
| `IPdm.ExportViewerPackage` | Package viewer (.TopPkgViw) |
| `IPdm.ExportToBabylon` | Format Babylon.js |
| `IDocuments.zExportToTopglTF` | Export glTF direct (12 params) |
