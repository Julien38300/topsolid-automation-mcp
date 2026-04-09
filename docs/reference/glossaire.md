# Glossaire TopSolid FR / EN

Mapping officiel des termes metier TopSolid (francais) vers les termes API (anglais). Valide par Julien (PM TopSolid Steel & Design).

## Proprietes document (colonnes PDM)

| TopSolid FR | API EN | Methode API |
|-------------|--------|-------------|
| **Nom** | Name | `IPdm.SetName` / `GetName` |
| **Designation** | Description | `IPdm.SetDescription` / `GetDescription` |
| **Reference** | PartNumber | `IPdm.SetPartNumber` / `GetPartNumber` |
| **Fabricant** | Manufacturer | `IPdm.SetManufacturer` / `GetManufacturer` |
| **Reference fabricant** | ManufacturerPartNumber | `IPdm.SetManufacturerPartNumber` |
| **Proprietaire / Auteur** | Owner | `IPdm.GetOwner` |

::: danger Piege courant
"Designation" en TopSolid = `Description` dans l'API. **Pas** `Name` !
:::

## Operations PDM

| TopSolid FR | API EN | Notes |
|-------------|--------|-------|
| Mise au coffre | CheckIn | Archive la revision |
| Sorti de coffre | CheckOut | Reserve pour modification |
| En modification | EnsureIsDirty | Le docId change (ref) ! |
| Sauvegarder | Save | Obligatoire apres EndModification |

## Types de documents

| Extension | TopSolid FR | API |
|-----------|-------------|-----|
| `.TopPrt` | Piece | `IPdm.CreateDocument(id, ".TopPrt", true)` |
| `.TopAsm` | Assemblage | `IPdm.CreateDocument(id, ".TopAsm", true)` |
| `.TopFam` | Famille | `IPdm.CreateDocument(id, ".TopFam", true)` |
| `.TopDrf` | Mise en plan / Plan | IDraftings |

## Interfaces

| Interface | Termes FR acceptes |
|-----------|-------------------|
| ICoatings | Revetement, **peinture**, **traitement** |
| IUnfoldings | **Mise a plat** (pas depliage) |
| IEntities | **Entite** (pas fonction) |
| IDraftings | Mise en plan, plan, draft |
| IAnnotations | Annotation, cotation, **cote 3D** |
| IBoms | Nomenclature, **rafale**, fiche, liste de debit |
| ITables | Tableaux (uniquement en mise en plan) |

## Geometrie / Modelisation

| API EN | TopSolid FR |
|--------|-------------|
| Profile | Profil, **section**, **contour** |
| Loft / Lofted | **Lissage** ou **gabarit** (selon contexte) |
| Pattern | **Motif** (repetition = concept different) |
| Revolution | Revolution, **piece tournee** |
| Unfolding | **Mise a plat**, depliage |
| Shell | Coque |
| Sheet | Tole |
| Extrusion | Extrusion, **extrude** |

## Parametres

| API EN | TopSolid FR |
|--------|-------------|
| Smart (SmartReal, SmartText) | **Parametrise**, formule, entite dans un champ |
| UnitType | **Type d'unite** |
| Driver | **Pilote** |
| Working stage | **Etape courante** |

## Concepts metier

| Terme TopSolid | Definition |
|----------------|-----------|
| **Rafale** | Generation par lot : un plan ou une mise a plat par ligne de nomenclature |
| **Liasse de plans** | Collection structuree de mises en plan |
| **Modele** | Template reutilisable (fonctionne pour tous types de documents) |
| **Nomenclature filtree** | BOM avec filtre (toles, profiles, achetes...) |
| **Ensemble de projection** | Definit quoi projeter dans une mise en plan |
| **Propriete utilisateur** | Propriete custom (ex: "Type de production": Achete/Fabrique) |
| **Propriete d'occurrence** | Propriete ajoutee/surchargee sur une occurrence d'assemblage |
