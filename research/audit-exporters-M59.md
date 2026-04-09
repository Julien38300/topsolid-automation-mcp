# Audit Exporteurs — Preparation M-59
Date: 2026-04-09

## Methodes export/import dans le graphe (toutes presentes)

### Generique (index-based)
- IApplication.ExporterCount / ImporterCount
- IApplication.GetExporterFileType / GetImporterFileType
- IApplication.GetExporterOptions / GetImporterOptions
- IApplication.IsExporterValid / IsImporterValid
- IDocuments.CanExport, Export, ExportWithOptions
- IDocuments.Import, ImportWithOptions

### Specialise
- IDocuments.zExportToTopglTF / zExportToTopglTFWithRepresentation (glTF direct)
- IPdm.ExportPackage / ExportViewerPackage / ExportExecutablePackage
- IPdm.ExportToBabylon, ExportMinorRevisionFile
- IPdm.ImportFile, ReimportFile, ImportPackageAsReplication, ImportPackageAsDistinctCopy
- ITables.ExportDraftTableCellValue

## Recettes existantes
- R-004 : Export STEP (generique)
- R-015 : Export avec options (FBX, GLTF, Parasolid)
- R-016 : Import STEP avec options
- R-019 : Lister tous les exporteurs/importeurs

## Manquant pour M-59
1. Recette DXF (mise a plat / mise en plan)
2. Recette PDF (mise en plan)
3. Recette IFC (batiment)
4. Recette batch export (tous les documents d'un projet)
5. Documentation des options par format (runtime only via GetExporterOptions)
6. Outil MCP dedie topsolid_export (vs script compose)
