# Interfaces API

Liste des 46 interfaces de l'API TopSolid Automation couvertes par le graphe.

## Kernel (TopSolidHost)

| Interface | Methodes | Description |
|-----------|----------|-------------|
| IPdm | 139 | Gestion documentaire : projets, documents, revisions, coffre |
| IDocuments | 64 | Operations documents : ouvrir, fermer, exporter, importer |
| IElements | 48 | Elements : noms, types, proprietes, hierarchie |
| IParameters | 161 | Parametres : lecture/ecriture, types, formules, proprietes utilisateur |
| IEntities | 31 | Entites : fonctions, publishings, transformations |
| IGeometries3D | 59 | Geometrie 3D : points, plans, reperes, frames |
| IGeometries2D | 34 | Geometrie 2D : points, lignes, cercles |
| ISketches2D | 59 | Esquisses 2D : sommets, segments, profils, contraintes |
| ISketches3D | 52 | Esquisses 3D |
| IShapes | 62 | Formes : faces, edges, volume, extrusion, revolution |
| IOperations | 29 | Arbre de construction : operations, etape courante |
| IMaterials | 57 | Materiaux : assignation, proprietes physiques |
| ITextures | 35 | Textures : application, parametres visuels |
| IApplication | 24 | Application : modification, exporteurs, importeurs |
| IUser | 18 | Interaction utilisateur : selections, questions |
| IUnits | 5 | Unites : conversion, types |
| ILayers | 8 | Calques |
| IOptions | 8 | Options globales |
| IHealing | 4 | Reparation geometrie |
| ILicenses | 8 | Licences |
| IClassifications | 14 | Classifications |

## Design (TopSolidDesignHost)

| Interface | Methodes | Description |
|-----------|----------|-------------|
| IAssemblies | 56 | Assemblages : inclusions, occurrences, contraintes |
| IFamilies | 43 | Familles : codes, catalogues, instances, pilotes |
| IParts | 47 | Pieces : stock, percages, cylindre englobant |
| ICoatings | 60 | Revetements / peintures / traitements |
| IRepresentations | 11 | Representations visuelles |
| ISubstitutions | 12 | Substitutions de composants |
| IFeatures | 11 | Features : percages, taraudages |
| IFinishings | 6 | Finitions de surface |
| ITools | 33 | Outils : stock, gestion brut |
| IProcesses | 5 | Procedes de fabrication |
| ISimulations | 24 | Simulations cinematiques/dynamiques |
| IMechanisms | 28 | Mecanismes : liaisons, joints, groupes rigides |
| IMultiLayer | 40 | Multi-couches |
| IUnfoldings | 4 | Mise a plat (tolerie) |

## Drafting (TopSolidDraftingHost)

| Interface | Methodes | Description |
|-----------|----------|-------------|
| IDraftings | 31 | Mise en plan : vues, ensembles de projection |
| IBoms | 24 | Nomenclatures : filtres, rafale, colonnes |
| ITables | 18 | Tableaux en mise en plan : cellules, lignes, colonnes |
| IAnnotations | 19 | Annotations : cotations, tolerances geometriques |
| IDimensions | 10 | Cotes : lineaires, angulaires |
| IStyles | 3 | Styles de mise en plan |

## Admin / Securite

| Interface | Methodes | Description |
|-----------|----------|-------------|
| IPdmAdmin | 13 | Administration PDM |
| IPdmSecurity | 9 | Securite et permissions |
| IPdmWorkflow | 8 | Workflows et etats |
| IDocumentsEvents | 2 | Evenements documents |
