# TopSolid Exporters Live Output

Ce document contient la capture réelle des exporteurs et importeurs disponibles sur l'instance TopSolid testée le 2026-04-09, ainsi que les options de configuration pour chaque format.

## Liste des Exporteurs (T-87)

L'instance TopSolid actuelle dispose de **33 exporteurs** valides :

| Index | Nom | Extensions |
|-------|-----|------------|
| 0 | Redway | .red |
| 1 | CSV | .csv |
| 2 | Text | .txt |
| 4 | Acis (Spatial) | .sat |
| 5 | Catia V4 (Spatial) | .model |
| 6 | Catia V5 (Spatial) | .CatPart, .CatProduct |
| 8 | Step (Spatial) | .stp, .step |
| 10 | Acrobat PDF3D | .Pdf |
| 11 | AutoCad | .dxf, .dwg |
| 15 | IFC | .ifc |
| 16 | IFCZIP | .ifczip |
| 20 | 3MF | .3mf |
| 27 | STL | .stl |
| ... | ... | ... |

## Liste des Importeurs (T-87)

**43 importeurs** sont disponibles. 
> [!NOTE]
> L'importeur XML V7 Method (Index 42) est marqué comme non valide sur cette instance.

## Options des Exporteurs (T-91)

Extraits des options de configuration disponibles via l'API :

### Step (Spatial)
- `SAVE_VERSION` = 1 (AP214 par défaut probablement)
- `SIMPLIFY_ASSEMBLY_STRUCTURE` = True

### AutoCad (.dxf, .dwg)
- `SAVE_FORMAT` = 1
- `BASIFY_DIMENSIONS` = True
- `WRITE_BLOCKS` = True

### IFC (.ifc)
- `SCHEMA_VERSION` = IFC2X3
- `PARAMETERS_TO_EXPORT` = All
- `EXPORTS_DRILLING_GEOMETRY` = True

### Acrobat PDF3D (.Pdf)
- `EXPORT_MODE` = Front
- `LINEAR_TOLERANCE` = 0,0002

## Validation des nouvelles recettes

Les tests de validation ont confirmé la présence et le fonctionnement des exporteurs demandés :
- **T-88 (DXF)** : PASS (Index 11)
- **T-89 (PDF)** : PASS (Index 10)
- **T-90 (User Properties)** : PASS (Lecture de Désignation, Référence, Fabricant)
