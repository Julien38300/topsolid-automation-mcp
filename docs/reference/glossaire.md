# Glossaire TopSolid FR / EN

Mapping officiel des termes metier TopSolid (francais) vers les termes API (anglais). Valide par Julien (PM TopSolid Steel & Design).

## Proprietes document (colonnes PDM)

| TopSolid FR | API EN | Methode API |
|-------------|--------|-------------|
| **Nom** | Name | `IPdm.SetName` / `GetName` |
| **Designation** | Description | `IPdm.SetDescription` / `GetDescription` |
| **Reference** | PartNumber | `IPdm.SetPartNumber` / `GetPartNumber` |
| **Fabricant** | Manufacturer | `IPdm.SetManufacturer` / `GetManufacturer` |
| **Reference fabricant** | ManufacturerPartNumber | `IPdm.SetManufacturerPartNumber` / `GetManufacturerPartNumber` |
| **Auteur** | Author | `IPdm.SetAuthor` / `GetAuthor` |
| **Proprietaire** | Owner | `IPdm.GetOwner` |
| **Code** | Code | `IPdm.GetCode` |
| **Commentaire** | Comment | Parametre systeme |

::: danger Piege courant
"Designation" en TopSolid = `Description` dans l'API. **Pas** `Name` !
:::

## Acces direct aux proprietes document (via IParameters)

| TopSolid FR | Methode directe |
|-------------|----------------|
| Nom | `IParameters.GetNameParameter(docId)` |
| Designation | `IParameters.GetDescriptionParameter(docId)` |
| Reference | `IParameters.GetPartNumberParameter(docId)` |
| Fabricant | `IParameters.GetManufacturerParameter(docId)` |
| Ref fabricant | `IParameters.GetManufacturerPartNumberParameter(docId)` |
| Auteur | `IParameters.GetAuthorParameter(docId)` |
| Revision majeure | `IParameters.GetMajorRevisionParameter(docId)` |
| Revision mineure | `IParameters.GetMinorRevisionParameter(docId)` |

::: tip Formules parametriques
On peut lier des proprietes entre elles avec `SetTextParameterizedValue` :
- `[$PartNumber]` = Reference
- `[$Description]` = Designation
- `[$MajorRevision]` = Revision majeure
- `[$PartNumber][$MajorRevision]-[$Description]` = formule composee
:::

## Proprietes systeme (System Parameters)

Accessibles via `GetParameters(docId)` + `GetFriendlyName(paramId)` + `GetRealValue/GetTextValue/...`

### Sur piece (.TopPrt)

| Nom systeme (EN) | TopSolid FR | Type | Unite |
|-------------------|-------------|------|-------|
| Mass | Masse | Real | kg |
| Volume | Volume | Real | m3 |
| Surface Area | Surface | Real | m2 |
| Height | Hauteur | Real | m |
| Width | Largeur | Real | m |
| Length | Longueur | Real | m |
| Thickness | Epaisseur | Real | m |
| Box X/Y/Z Size | Dimensions boite | Real | m |
| Box X/Y/Z marged size | Dimensions brut | Real | m |
| Stock Marged Height/Width/Length | Dimensions brut stock | Real | m |
| Principal X/Y/Z Moment | Moments d'inertie | Real | kg.mm2 |
| Sheet Metal | Tolerie | Boolean | — |
| Unfoldable Shape | Forme depliable | Boolean | — |
| Bends Number | Nombre de plis | Integer | — |
| Perimeter (Unfolding) | Perimetre deplie | Real | m |
| Surface Area (Unfolding) | Surface depliee | Real | m2 |
| Box Width/Length (Unfolding) | Dimensions mise a plat | Real | m |

### Sur assemblage (.TopAsm)

| Nom systeme (EN) | TopSolid FR | Type |
|-------------------|-------------|------|
| Mass | Masse totale | Real |
| Volume | Volume total | Real |
| Surface Area | Surface totale | Real |
| Part Count | Nombre de pieces | Integer |
| Principal X/Y/Z Moment | Moments d'inertie | Real |
| Type for BOM | Type nomenclature | Enumeration |

### Sur tout document

| Nom systeme (EN) | TopSolid FR | Type |
|-------------------|-------------|------|
| Author | Auteur | Text |
| Name | Nom | Text |
| Description | Designation | Text |
| Part Number | Reference | Text |
| Manufacturer | Fabricant | Text |
| Standard | Standard | Enumeration |
| Major Revision | Revision majeure | Text |
| Minor Revision | Revision mineure | Text |
| Creation Date | Date creation | DateTime |
| Modification Date | Date modification | DateTime |
| Universal Identifier | Identifiant universel | Unclassified |
| Create new universal identifier at copy | Nouvel ID a la copie | Boolean |

::: info Astuce
Les parametres systeme ont un `$` dans leur nom interne (`GetName`). Utiliser `GetFriendlyName` pour le nom lisible (Mass, Volume, etc.).
:::

## Operations PDM

| TopSolid FR | API EN | Methode | Notes |
|-------------|--------|---------|-------|
| Mise au coffre | CheckIn | `IPdm.CheckIn(pdmId, true)` | Archive la revision |
| Sorti de coffre | CheckOut | `IPdm.CheckOut(pdmId)` | Reserve pour modification |
| En modification | EnsureIsDirty | `IDocuments.EnsureIsDirty(ref docId)` | **Le docId change (ref) !** |
| Sauvegarder | Save | `IPdm.Save(pdmId, true)` | Obligatoire apres EndModification |
| Document modifie ? | IsDirty | `IPdm.IsDirty(pdmId)` | True si non sauvegarde |
| Rechercher par nom | SearchByName | `IPdm.SearchDocumentByName(projId, name)` | Retourne List |
| Deplacer documents | MoveSeveral | `IPdm.MoveSeveral(list, destId)` | Batch move |
| Projets references | GetReferencedProjects | `IPdm.GetReferencedProjects(projId)` | Bibliotheques liees |
| Contenu dossier | GetConstituents | `IPdm.GetConstituents(id, out folders, out docs)` | Recursif |
| Creer document | CreateDocument | `IPdm.CreateDocument(parentId, ".TopPrt", true)` | Par type |
| Import fichier | ImportFile | `IPdm.ImportFile(path, projId, name)` | Raccourci direct |
| Import avec options | ImportWithOptions | `IDocuments.ImportWithOptions(idx, opts, path, ...)` | Parametre fin |

## Revisions

| TopSolid FR | API EN | Methode |
|-------------|--------|---------|
| Revision majeure | MajorRevision | `IPdm.GetMajorRevisions(pdmId)` |
| Revision mineure | MinorRevision | `IPdm.GetMinorRevisions(majorRevId)` |
| Derniere revision | LastMajorRevision | `IPdm.GetLastMajorRevision(pdmId)` |
| Ouvrir une revision | OpenMinorRevision | `IPdm.OpenMinorRevision(minorRevId, readOnly)` |
| Cas d'emploi | BackReferences | `IPdm.SearchMajorRevisionBackReferences(projId, majorRev)` |
| Etat cycle de vie | LifeCycleMainState | `IPdm.GetMajorRevisionLifeCycleMainState(majorRev)` |
| Texte revision | RevisionText | `IPdm.GetMajorRevisionText(majorRev)` |
| Preview document | PreviewBitmap | `IPdm.GetMinorRevisionPreviewBitmap(minorRev)` |

## Types de documents

| Extension | TopSolid FR | Utilisation |
|-----------|-------------|-------------|
| `.TopPrt` | Piece | Piece 3D modelisee |
| `.TopAsm` | Assemblage | Ensemble de pieces |
| `.TopDft` | Mise en plan | Dessin 2D (vues, cotation) |
| `.TopMat` | Materiau | Definition materiau (acier, alu...) |
| `.TopTex` | Texture | Image PBR (albedo, roughness, normal...) |
| `.TopPrd` | Propriete utilisateur | Enumeration custom (Type production, Type composant...) |
| `.TopFam` | Famille | Document avec catalogue d'instances |

## Mode virtuel

| TopSolid FR | API EN | Methode |
|-------------|--------|---------|
| Document virtuel | VirtualDocument | `IDocuments.IsVirtualDocument(docId)` |
| Activer mode virtuel | SetVirtualDocumentMode | `IDocuments.SetVirtualDocumentMode(docId, true)` |
| Piece derivee | DerivedPart | `IParts.IsDerivedPart(docId)` |

::: tip
Le mode virtuel empeche les mises a jour automatiques. Utile pour les composants standards dans les bibliotheques.
:::

## Interfaces

| Interface | Termes FR acceptes | Methodes cles |
|-----------|-------------------|---------------|
| IPdm | PDM, coffre, projet | GetName, SetDescription, Save, CheckIn |
| IDocuments | Document, ouvrir, fermer | EditedDocument, Open, Import, Export |
| IParameters | Parametre, cote, valeur | GetParameters, GetRealValue, SetTextValue |
| IElements | Element, entite, nommer | GetFriendlyName, SearchByName, SetDescription |
| IFamilies | Famille, catalogue, code | IsFamily, GetCatalogColumnParameters |
| IAssemblies | Assemblage, inclusion, occurrence | IsAssembly, GetParts, GetOccurrenceDefinition |
| IParts | Piece, masse, inertie | IsDerivedPart, GetMassPropertyManagement |
| ICoatings | Revetement, **peinture**, **traitement** | SetColor, GetColor |
| IShapes | Forme, solide, tole | GetShapes, GetShapeType, GetShapeVolume |
| ISketches2D | Esquisse, profil, sketch | GetSketches, CreateSketchIn3D |
| IOperations | Operation, feature, arbre | GetOperations, GetChildren |
| IDraftings | Mise en plan, plan, draft | IsDrafting, GetDraftingViews, GetPageCount |
| IBoms | Nomenclature, **rafale**, fiche, BOM | IsBom, GetRowContents, GetRootRow |
| ITables | Tableaux (en mise en plan) | GetDraftTables, GetDraftTableCellText |
| IUnfoldings | **Mise a plat**, depliage | IsUnfolding, GetBendFeatures |
| ITextures | Texture, image PBR | SetCategory, SetPicture, SetTextureScale |
| IMaterials | Materiau, matiere | GetDensity (v7.16+) |
| IVisualization3D | Vue 3D, viewport | GetActiveView, GetViewRectangle |
| IAnnotations | Annotation, cotation, **cote 3D** | GetAnnotationType |
| IUser | Utilisateur, selection | Ask* (selection interactive dans TopSolid) |

## Geometrie / Modelisation

| API EN | TopSolid FR |
|--------|-------------|
| Profile | Profil, **section**, **contour** |
| Loft / Lofted | **Lissage** ou **gabarit** |
| Pattern | **Motif** (repetition) |
| Revolution | Revolution, **piece tournee** |
| Extrusion | Extrusion, **extrude** |
| Shell | Coque |
| Sheet | Tole |
| Unfolding | **Mise a plat**, depliage |
| Bend | Pli, pliage |
| Enclosing Box | Boite englobante, brut |

## Parametres avances

| API EN | TopSolid FR | Usage |
|--------|-------------|-------|
| SmartReal / SmartText | Parametrise, formule | Valeur avec formule ou lien |
| UnitType | Type d'unite | Length, Angle, Mass, etc. |
| Driver | **Pilote** | Parametre qui pilote une famille |
| Working stage | **Etape courante** | Position dans l'arbre de construction |
| UserEnumeration | Enumeration utilisateur | Propriete custom (.TopPrd) |
| ParameterType | Type de parametre | Real=1, Integer=2, Boolean=3, Text=4, DateTime=5, Family=6, Code=7, Enumeration=8, UserEnumeration=9 |

## Proprietes utilisateur (.TopPrd)

Workflow complet pour affecter une propriete utilisateur :
1. `IPdm.GetReferencedProjects(projId)` — trouver les projets references
2. `IPdm.SearchDocumentByName(refProj, "Type Composant")` — chercher le .TopPrd
3. `IPdm.GetType(objId, out type)` — filtrer par `.TopPrd`
4. `IParameters.CreateUserPropertyParameter(docId, propDocId)` — creer le parametre
5. `IParameters.GetUserEnumerationDefinition(paramId)` — lire la definition
6. `IParameters.GetUserEnumerationValues(defId, out values, out texts)` — valeurs possibles
7. `IParameters.SetUserEnumerationValue(paramId, valueIndex)` — affecter une valeur

## Concepts metier

| Terme TopSolid | Definition |
|----------------|-----------|
| **Rafale** | Generation par lot : un plan ou une mise a plat par ligne de nomenclature |
| **Liasse de plans** | Collection structuree de mises en plan |
| **Modele** | Template reutilisable (fonctionne pour tous types de documents) |
| **Nomenclature filtree** | BOM avec filtre (toles, profiles, achetes...) |
| **Ensemble de projection** | Definit quoi projeter dans une mise en plan |
| **Propriete utilisateur** | Propriete custom stockee dans .TopPrd (ex: Type production) |
| **Propriete d'occurrence** | Propriete ajoutee/surchargee sur une occurrence d'assemblage |
| **Cas d'emploi** | Where-used — dans quels documents cette piece est utilisee |
| **Document virtuel** | Document qui ne se met pas a jour automatiquement |
| **Piece derivee** | Piece creee par derivation d'une autre (lien parametrique) |
| **Driver / Pilote** | Parametre qui pilote les instances d'une famille |
| **Catalogue famille** | Tableau des instances (codes x colonnes/drivers) |
| **Composant standard** | Piece de bibliotheque non modifiable (vis, ecrou...) |
| **Inclusion** | Composant place dans un assemblage |
| **Occurrence** | Instance d'une inclusion dans l'assemblage |
