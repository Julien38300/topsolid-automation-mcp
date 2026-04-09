# SCENARIOS CATALOG — TopSolid Automation Layer 1
Derniere mise a jour : 2026-04-06
Sources : Exemples REDACTED-USER, TopSolidKernelAutomationExamples, exemples unitaires VB/C#

## Legende
- R = READ, W = WRITE, RW = READ+WRITE
- ✅ = couvert par test existant (T-XX), ❌ = pas couvert
- 🟢 = APIs dans le graphe, 🟡 = partiellement, 🔴 = manquant

---

## CAT-1 : PDM — Navigation & Recherche

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-001 | Lister les projets ouverts | R | Pdm.GetOpenProjects, Pdm.GetName | Simple | ✅ T-11 | 🟢 | KernelExamples |
| S-002 | Lister le contenu d'un projet (dossiers + docs) | R | Pdm.GetCurrentProject, Pdm.GetConstituents | Medium | ✅ T-12 | 🟢 | KernelExamples |
| S-003 | Lister le contenu d'un dossier avec types | R | Pdm.GetConstituents, Pdm.GetType | Medium | ✅ T-13 | 🟢 | KernelExamples |
| S-004 | Chercher un dossier par nom (CONTAINS) | R | Pdm.SearchFolderByName | Simple | ✅ T-14 | 🟢 | KernelExamples |
| S-005 | Chercher un document par nom (CONTAINS) | R | Pdm.SearchDocumentByName, Pdm.GetType | Simple | ✅ T-20 | 🟢 | KernelExamples |
| S-006 | Arbre recursif projet complet | R | Pdm.GetConstituents (recursif), Pdm.GetName | Complex | ❌ | 🟢 | ExtractionCSV |
| S-007 | Lire metadata PDM (description, reference, fabricant) | R | Pdm.GetDescription, Pdm.GetPartNumber, Pdm.GetManufacturer | Simple | ❌ | 🟡 | ExtractionCSV |
| S-008 | Chercher objets par proprietes | R | Pdm.SearchObjectsWithProperties | Medium | ❌ | 🟡 | REDACTED-USER |
| S-009 | Lister tous les projets (working + libraries) | R | Pdm.GetProjects(bool, bool) | Simple | ❌ | 🟢 | REDACTED-USER |
| S-010 | Lister dossiers de projets (racine) | R | Pdm.WorkingProjectsRootFolder, Pdm.GetProjectFolderConstituents | Simple | ❌ | 🟡 | REDACTED-USER |
| S-011 | Date sauvegarde et commentaire PDM | R | Pdm.GetSavingDate, Pdm.GetComment | Simple | ❌ | 🟡 | REDACTED-USER |
| S-012 | Afficher dans arbre projets | R | Pdm.ShowInProjectTree | Simple | ❌ | 🟡 | REDACTED-USER |
| S-013 | Lister connexions PDM disponibles | R | Pdm.GetAvailablePdmConnections | Simple | ❌ | 🟡 | REDACTED-USER |

### PDM — Ecriture

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-014 | Creer un document dans un projet | W | Pdm.CreateDocument, Pdm.SetName, Pdm.Save | Medium | ❌ | 🟢 | MaterialCreator |
| S-015 | Creer un dossier dans un projet | W | Pdm.CreateFolder | Simple | ❌ | 🟢 | ProjectOrganizer |
| S-016 | Deplacer documents entre dossiers | W | Pdm.MoveSeveral | Simple | ❌ | 🟡 | ProjectOrganizer |
| S-017 | Supprimer documents | W | Pdm.DeleteSeveral | Simple | ❌ | 🟡 | ProjectOrganizer |
| S-018 | Check-in / Check-out | RW | Pdm.CheckIn, Pdm.CheckOut, Pdm.UndoCheckOut | Medium | ❌ | 🟡 | KernelExamples |
| S-019 | Sauvegarder plusieurs documents | W | Pdm.SaveSeveral | Simple | ❌ | 🟡 | KernelExamples |
| S-020 | Import/export package projet | RW | Pdm.ExportPackage, Pdm.ImportPackageAsDistinctCopy | Medium | ❌ | 🟡 | KernelExamples |

---

## CAT-2 : Documents — Etat & Gestion

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-021 | Lire document actif (nom, type, projet) | R | Documents.EditedDocument, Pdm.GetName, Documents.GetTypeFullName | Simple | ✅ T-00,T-02,T-08 | 🟢 | Tous |
| S-022 | Detecter type de document (assemblage, piece, plan) | R | Documents.GetTypeFullName | Simple | ✅ T-18 | 🟢 | KernelExamples |
| S-023 | Ouvrir un document par nom | RW | Pdm.SearchDocumentByName, Documents.GetDocument, Documents.Open | Medium | ❌ | 🟢 | KernelExamples |
| S-024 | Renommer un document | W | Documents.SetName + Pattern D | Simple | ❌ | 🟢 | MaterialCreator |
| S-025 | Fermer tous les documents | W | Documents.CloseAll | Simple | ❌ | 🟡 | ProjectOrganizer |
| S-026 | Reconstruire un document | W | Documents.Rebuild + Pattern D | Simple | ❌ | 🟡 | TraitementParLot |
| S-027 | Sauvegarder un document | W | Documents.Save | Simple | ❌ | 🟢 | TraitementParLot |

### Documents — Revisions

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-028 | Lire historique revisions (major/minor) | R | Pdm.GetMajorRevisions, Pdm.GetMinorRevisions, Pdm.GetMajorRevisionText | Medium | ❌ | 🟡 | RevisionManager |
| S-029 | Modifier etat cycle de vie | W | Pdm.SetMajorRevisionLifeCycleMainState | Simple | ❌ | 🟡 | RevisionManager |
| S-030 | Ouvrir revision specifique | R | Pdm.OpenMinorRevision, Documents.GetMinorRevisionDocument | Medium | ❌ | 🟡 | RevisionManager |
| S-031 | Mettre a jour references document | W | Pdm.UpdateDocumentReferences | Simple | ❌ | 🟡 | TraitementParLot |

---

## CAT-3 : Parametres — Lecture & Ecriture

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-040 | Lister tous les parametres avec types et valeurs | R | Parameters.GetParameters, GetParameterType, Get*Value, Elements.GetFriendlyName | Medium | ✅ T-15 | 🟢 | Tous |
| S-041 | Lire un parametre booleen par nom | R | Parameters.GetBooleanValue, Elements.GetFriendlyName | Medium | ✅ T-16 | 🟢 | KernelExamples |
| S-042 | Lire un parametre reel par nom | R | Parameters.GetRealValue, Elements.GetFriendlyName | Medium | ❌ | 🟢 | LectureParametre |
| S-043 | Modifier un parametre reel | W | Parameters.SetRealValue + Pattern D | Medium | ❌ | 🟢 | AffectationVB |
| S-044 | Modifier un parametre texte | W | Parameters.SetTextValue + Pattern D | Medium | ❌ | 🟢 | AffectationVB |
| S-045 | Modifier un parametre booleen | W | Parameters.SetBooleanValue + Pattern D | Medium | ❌ | 🟢 | TraitementParLot |
| S-046 | Modifier un parametre enumeration | W | Parameters.SetEnumerationValue + Pattern D | Medium | ❌ | 🟢 | TraitementParLot |
| S-047 | Creer un parametre reel | W | Parameters.CreateRealParameter + Pattern D | Medium | ❌ | 🟢 | CopieParametres |
| S-048 | Creer un parametre texte | W | Parameters.CreateTextParameter + Pattern D | Medium | ❌ | 🟢 | CopieParametres |
| S-049 | Creer un parametre entier | W | Parameters.CreateIntegerParameter + Pattern D | Medium | ❌ | 🟢 | CopieParametres |
| S-050 | Creer un parametre booleen | W | Parameters.CreateBooleanParameter + Pattern D | Medium | ❌ | 🟢 | CopieParametres |
| S-051 | Supprimer un parametre | W | Elements.Delete + Pattern D | Simple | ❌ | 🟢 | AffectationVB |
| S-052 | Copier parametres entre documents | RW | Get*Value sur source + Create*Parameter sur cible | Complex | ❌ | 🟢 | CopieParametres |
| S-053 | Creer parametre avec formule (SmartReal) | W | Parameters.CreateSmartRealParameter | Complex | ❌ | 🟢 | PreParametrage |
| S-054 | Parametrer le nom du document (formule) | W | Parameters.GetNameParameter, Parameters.SetTextParameterizedValue | Medium | ❌ | 🟡 | AffectationVB |
| S-055 | Creer propriete utilisateur | W | Parameters.CreateUserPropertyParameter, Pdm.SearchDocumentByName | Medium | ❌ | 🟡 | TraitementParLot |
| S-056 | Creer parametre couleur | W | Parameters.CreateColorParameter | Simple | ❌ | 🟢 | REDACTED-USER |
| S-057 | Creer parametres relayes (Real/Bool/Int/Text/Color) | W | Parameters.Create*RelayedParameter | Medium | ❌ | 🟢 | REDACTED-USER |
| S-058 | Gerer valeurs possibles couleur | RW | Parameters.Get/SetColorParameterPossibleValues | Medium | ❌ | 🟢 | REDACTED-USER |
| S-059 | Creer parametres table (Real/Color) | W | Parameters.CreateRealTableParameter, CreateColorTableParameter | Complex | ❌ | 🟢 | REDACTED-USER |

---

## CAT-4 : Esquisses & Geometrie 2D

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-060 | Lister les esquisses du document | R | Entities.GetFunctions, Elements.GetTypeFullName (filtre Sketch) | Simple | ✅ T-17 | 🟢 | KernelExamples |
| S-061 | Creer une esquisse sur un plan | W | Sketches2D.CreateSketchIn3D + Pattern D | Complex | ❌ | 🟢 | InclusionManager |
| S-062 | Creer geometrie 2D (vertex, segments, profil) | W | Sketches2D.StartModification, CreateVertex, CreateLineSegment, CreateProfile, EndModification | Complex | ❌ | 🟢 | InclusionManager |
| S-063 | Creer une extrusion depuis une esquisse | W | Shapes.CreateExtrudedShape | Complex | ❌ | 🟢 | InclusionManager |

---

## CAT-5 : Geometrie 3D & Shapes

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-070 | Lister les shapes du document | R | Shapes.GetShapes, Elements.GetFriendlyName | Simple | ❌ | 🟢 | REDACTED-USER |
| S-071 | Lire les faces d'un shape | R | Shapes.GetFaces, Shapes.GetFaceEnclosingCoordinatesWithGivenFrame | Medium | ❌ | 🟢 | REDACTED-USER |
| S-072 | Creer un point 3D | W | Geometries3D.CreatePoint + Pattern D | Simple | ❌ | 🟢 | REDACTED-USER |
| S-073 | Creer un repere (frame) | W | Geometries3D.CreateFrame + Pattern D | Simple | ❌ | 🟢 | REDACTED-USER |
| S-074 | Creer repere point + 2 directions | W | Geometries3D.CreateFrameByPointAndTwoDirections + Pattern D | Medium | ❌ | 🟢 | REDACTED-USER |
| S-075 | Creer repere avec offset | W | Geometries3D.CreateFrameWithOffset + Pattern D | Medium | ❌ | 🟢 | REDACTED-USER |
| S-076 | Lire plans absolus (XY, XZ, YZ) | R | Geometries3D.GetAbsoluteXYPlane, GetAbsoluteOriginPoint | Simple | ❌ | 🟢 | InclusionManager |
| S-077 | Transformer un element (translation/rotation) | W | Entities.Transform + Pattern D | Medium | ❌ | 🟢 | REDACTED-USER |

---

## CAT-6 : Assemblages & Inclusions

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-080 | Detecter si document est un assemblage | R | Documents.GetTypeFullName (check "Assembly") | Simple | ✅ T-18 | 🟢 | KernelExamples |
| S-081 | Lister les inclusions et leur definition | R | Entities.GetFunctions, Assemblies.GetInclusionDefinitionDocument | Medium | ✅ T-19 | 🟢 | KernelExamples |
| S-082 | Lire codes et pilotes d'une inclusion | R | Assemblies.GetInclusionCodeAndDrivers | Medium | ❌ | 🟢 | REDACTED-USER |
| S-083 | Creer inclusion simple (CreateInclusion2) | W | Assemblies.CreateInclusion2 + Pattern D | Complex | ❌ | 🟢 | REDACTED-USER |
| S-084 | Creer inclusion parametree (SmartPoint/Profile/Real) | W | Assemblies.CreateInclusion + SmartObjects + Pattern D | Complex | ❌ | 🟢 | REDACTED-USER |
| S-085 | Creer contrainte frame-on-frame | W | Assemblies.CreateFrameOnFrameConstraint | Complex | ❌ | 🟢 | InclusionManager |
| S-086 | Modifier code/pilotes inclusion | W | Assemblies.SetInclusionCodeAndDrivers2 + Pattern D | Complex | ❌ | 🟢 | REDACTED-USER |
| S-087 | Derive Part for Modification | W | Assemblies.DerivePartForModification | Complex | ❌ | 🟡 | REDACTED-USER |
| S-088 | Substitution composants | RW | Substitutions.SubstituteGlobalComponent, GetSubstitutableElements | Complex | ❌ | 🟡 | REDACTED-USER |
| S-089 | Gerer collisions assemblage | W | Assemblies.SetCollisionsManagement | Medium | ❌ | 🟡 | REDACTED-USER |
| S-090 | Drop document dans assemblage | W | Documents.Drop | Simple | ❌ | 🟡 | InclusionManager |

---

## CAT-7 : Familles & Generiques

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-100 | Detecter document famille | R | Families.IsFamily, Families.IsExplicit | Simple | ❌ | 🟢 | KernelExamples |
| S-101 | Lire codes famille | R | Families.GetCodes | Simple | ❌ | 🟢 | REDACTED-USER |
| S-102 | Lire instances explicites | R | Families.GetExplicitInstances | Medium | ❌ | 🟢 | KernelExamples |
| S-103 | Lire document generique | R | Families.GetGenericDocument | Simple | ❌ | 🟢 | REDACTED-USER |
| S-104 | Creer famille explicite | W | Families.SetAsExplicit + Pattern D | Medium | ❌ | 🟢 | FamilyManager |
| S-105 | Ajouter instance explicite | W | Families.AddExplicitInstance | Medium | ❌ | 🟢 | FamilyManager |
| S-106 | Affecter document generique | W | Families.SetGenericDocument | Simple | ❌ | 🟢 | FamilyManager |
| S-107 | Lire contraintes famille | R | Families.GetConstrainedEntityCount, GetOrderedConstrainedDrivers | Medium | ❌ | 🟡 | REDACTED-USER |
| S-108 | Gerer colonnes catalogue famille | W | Families.AddCatalogColumn, RemoveCatalogColumn | Medium | ❌ | 🟡 | REDACTED-USER |
| S-109 | Lire/modifier conditions pilotes famille | RW | Families.GetDriverCondition, GetDriverFolderCondition | Complex | ❌ | 🟡 | REDACTED-USER |

---

## CAT-8 : Import / Export

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-110 | Lister les exporteurs disponibles | R | Application.ExporterCount, GetExporterFileType | Simple | ❌ | 🟢 | REDACTED-USER |
| S-111 | Lister les importeurs disponibles | R | Application.ImporterCount, GetImporterFileType | Simple | ❌ | 🟢 | REDACTED-USER |
| S-112 | Verifier si export possible | R | Documents.CanExport | Simple | ❌ | 🟢 | REDACTED-USER |
| S-113 | Exporter en STEP | W | Documents.Export (index STEP) | Medium | ❌ | 🟢 | REDACTED-USER |
| S-114 | Exporter avec options (CATPart, FBX, GLTF) | W | Documents.ExportWithOptions | Medium | ❌ | 🟢 | REDACTED-USER |
| S-115 | Importer STEP avec options | RW | Documents.ImportWithOptions | Complex | ❌ | 🟢 | REDACTED-USER |
| S-116 | Importer avec representations | RW | Documents.Import + Representations management | Complex | ❌ | 🟢 | REDACTED-USER |
| S-117 | Export Parasolid avec Enclosing Box | RW | Parts.SetEnclosingBoxManagement + ExportWithOptions | Complex | ❌ | 🟡 | REDACTED-USER |
| S-118 | Export Viewer Package | W | Pdm.ExportViewerPackage | Medium | ❌ | 🟡 | REDACTED-USER |
| S-119 | Imprimer un document | W | Documents.Print, Application.PrinterNames | Medium | ❌ | 🟡 | ExportManager |

---

## CAT-9 : Drafting & Mise en plan

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-120 | Lire tableaux cotation | R | Tables.GetDraftTables, GetDraftTableCellType, GetDraftTableCellText | Medium | ❌ | 🟡 | REDACTED-USER |
| S-121 | Creer cotation lineaire | W | Draftings.CreateLinearDimensionBetweenTwoPoints | Medium | ❌ | 🟡 | REDACTED-USER |
| S-122 | Lire vues projection | R | Draftings.GetDraftingViews | Simple | ❌ | 🟡 | REDACTED-USER |
| S-123 | Creer ensemble projection | W | Draftings.CreateNewProjectionSet | Medium | ❌ | 🟡 | REDACTED-USER |
| S-124 | Modifier couleur fond cellule | W | Tables.SetDraftTableCellBackgroundColor | Simple | ❌ | 🟡 | REDACTED-USER |
| S-125 | Appliquer style texte | W | Styles.GetStyles, Styles.ApplyStyle | Simple | ❌ | 🟡 | REDACTED-USER |

---

## CAT-10 : Nomenclature (BOM)

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-130 | Detecter nomenclature | R | Boms.IsBom | Simple | ❌ | 🟡 | ExportMAP |
| S-131 | Lire colonnes nomenclature | R | Boms.GetColumnCount, GetColumnPropertyDefinition | Medium | ❌ | 🟡 | ExportMAP |
| S-132 | Parcourir lignes nomenclature | R | Boms.GetRootRow, GetRowChildrenRows, GetRowContents | Complex | ❌ | 🟡 | ExportMAP |

---

## CAT-11 : Materiaux, Coatings, Textures

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-140 | Affecter materiau a une piece | W | Parts.SetMaterial + Pattern D | Simple | ❌ | 🟢 | TraitementParLot |
| S-141 | Creer document materiau | W | Pdm.CreateDocument + Materials.SetCategory/SetDensity/SetMaterialModel | Complex | ❌ | 🟡 | REDACTED-USER |
| S-142 | Creer textures PBR (Albedo, Roughness, etc.) | W | Textures.SetPicture, SetCategory, Coatings.SetAlbedoTextureDocument | Complex | ❌ | 🟡 | REDACTED-USER |
| S-143 | Recuperer images textures | R | Materials.OpenTextureRetrievalSession, GetTextureImage | Medium | ❌ | 🟡 | REDACTED-USER |

---

## CAT-12 : Multi-couches

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-150 | Lire/modifier categorie multi-couche | RW | MultiLayers.GetCategory, SetCategory | Simple | ❌ | 🟡 | REDACTED-USER |
| S-151 | Gerer coatings multi-couche | RW | MultiLayers.Get/SetTopCoating, Get/SetBottomCoating | Medium | ❌ | 🟡 | REDACTED-USER |
| S-152 | Gerer finishings multi-couche | RW | MultiLayers.Get/SetTopFinishing, Get/SetBottomFinishing | Medium | ❌ | 🟡 | REDACTED-USER |
| S-153 | Definir couches multi-materiaux | W | MultiLayers.SetMultiMaterialLayers, AddMultiMaterialLayer | Medium | ❌ | 🟡 | REDACTED-USER |
| S-154 | Lire epaisseur totale | R | MultiLayers.GetMultiLayerTotalThickness | Simple | ❌ | 🟡 | REDACTED-USER |

---

## CAT-13 : Traitement par lot

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-160 | Batch modification proprietes utilisateur | RW | Parameters.SearchUserPropertyParameter, CreateUserPropertyParameter (boucle) | Complex | ❌ | 🟡 | TraitementParLot |
| S-161 | Batch creation symetrie plane | W | Tools.CreatePlaneSymmetry + Pattern D (boucle) | Complex | ❌ | 🟡 | TraitementParLot |
| S-162 | Batch modification tolerances modelisation | W | Options.SetModelingTolerances (boucle) | Simple | ❌ | 🟡 | TraitementParLot |
| S-163 | Batch modification type pour nomenclature | W | Parameters.SetEnumerationValue (boucle) | Medium | ❌ | 🟢 | TraitementParLot |
| S-164 | Batch affectation materiau | W | Parts.SetMaterial (boucle) | Medium | ❌ | 🟢 | TraitementParLot |

---

## CAT-14 : Simulation FEA (CAE)

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-170 | Lire Von Mises min/max | R | CaeHost.Results.GetVonMisesMinAndMaxResults | Simple | ❌ | 🔴 | REDACTED-USER |
| S-171 | Lire Displacement min/max | R | CaeHost.Results.GetDisplacementMinAndMaxResults | Simple | ❌ | 🔴 | REDACTED-USER |
| S-172 | Lire limite elastique materiau | R | CaeHost.Results.GetMaterialElasticLimit | Simple | ❌ | 🔴 | REDACTED-USER |
| S-173 | Modifier taille maillage + resoudre | W | CaeHost.Documents.SetMeshTargetSize, Solve | Medium | ❌ | 🔴 | REDACTED-USER |

---

## CAT-15 : Usinage CAM

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-180 | Generer code ISO (NC) | W | NCPostProcessor.GenerateNCCodesWithOptions | Complex | ❌ | 🔴 | REDACTED-USER |
| S-181 | Creer operation process usinage | W | Parts.CreateMachiningProcessOperation | Complex | ❌ | 🟡 | REDACTED-USER |
| S-182 | Lire conditions de coupe | R | Operations.GetNCOperation, GetAllCuttingConditionsAbacus | Complex | ❌ | 🔴 | REDACTED-USER |

---

## CAT-16 : Divers

| ID | Scenario | Type | APIs cles | Complexite | Test | Graphe | Source |
|----|----------|------|-----------|-----------|------|--------|--------|
| S-190 | Creer layer | W | Layers.CreateLayer + Pattern D | Simple | ❌ | 🟡 | REDACTED-USER |
| S-191 | Creer percage (drilling) | W | Parts.CreateDrillingOperation + Pattern D | Complex | ❌ | 🟡 | REDACTED-USER |
| S-192 | Definir process sur piece | W | Processes.DefineProcess | Medium | ❌ | 🟡 | REDACTED-USER |
| S-193 | Export parametres vers CSV | R | (boucle Parameters + Elements) | Complex | ❌ | 🟢 | AttributeImporter |
| S-194 | Import parametres depuis CSV | W | (boucle Parameters.Set*Value + Create*) | Complex | ❌ | 🟢 | AttributeImporter |

---

## RESUME

| Categorie | Total | READ | WRITE | RW | Couverts test | Couverts graphe 🟢 |
|-----------|-------|------|-------|----|---------------|-------------------|
| CAT-1 PDM | 20 | 13 | 7 | 0 | 5 | 10 |
| CAT-2 Documents | 11 | 5 | 6 | 0 | 3 | 6 |
| CAT-3 Parametres | 20 | 3 | 15 | 2 | 2 | 15 |
| CAT-4 Esquisses | 4 | 1 | 3 | 0 | 1 | 4 |
| CAT-5 Geometrie 3D | 8 | 3 | 5 | 0 | 0 | 7 |
| CAT-6 Assemblages | 11 | 3 | 7 | 1 | 2 | 6 |
| CAT-7 Familles | 10 | 5 | 5 | 0 | 0 | 6 |
| CAT-8 Import/Export | 10 | 3 | 6 | 1 | 0 | 5 |
| CAT-9 Drafting | 6 | 2 | 4 | 0 | 0 | 0 |
| CAT-10 BOM | 3 | 3 | 0 | 0 | 0 | 0 |
| CAT-11 Materiaux | 4 | 1 | 3 | 0 | 0 | 1 |
| CAT-12 Multi-couches | 5 | 1 | 3 | 1 | 0 | 0 |
| CAT-13 Batch | 5 | 0 | 5 | 0 | 0 | 2 |
| CAT-14 FEA | 4 | 3 | 1 | 0 | 0 | 0 |
| CAT-15 CAM | 3 | 1 | 2 | 0 | 0 | 0 |
| CAT-16 Divers | 5 | 1 | 3 | 1 | 0 | 2 |
| **TOTAL** | **129** | **48** | **75** | **6** | **13** | **64** |

## Couverture Recettes (recipes.md)

### Tier 1 — 20 recettes (R-001 a R-020)
| Recette | Scenarios couverts | Type |
|---------|-------------------|------|
| R-001 | S-005, S-023 (ouvrir doc par nom) | RW |
| R-002 | S-043 (modifier param reel) | W |
| R-003 | S-002, S-003, S-006 (naviguer PDM) | R |
| R-004 | S-110, S-112, S-113 (export STEP) | W |
| R-005 | S-047 (creer param reel) | W |
| R-006 | S-052 (copier params entre docs) | RW |
| R-007 | S-040 (masse/volume/surface) | R |
| R-008 | S-051 (supprimer param) | W |
| R-009 | S-024 (renommer doc) | W |
| R-010 | S-007, S-011 (metadata PDM) | R |
| R-011 | S-044 (modifier param texte) | W |
| R-012 | S-045 (modifier param bool) | W |
| R-013 | S-046 (modifier param enum) | W |
| R-014 | S-053 (creer SmartReal formule) | W |
| R-015 | S-114 (export FBX/GLTF/Parasolid) | W |
| R-016 | S-115 (import STEP) | RW |
| R-017 | S-018 (check-in/check-out) | RW |
| R-018 | S-028 (historique revisions) | R |
| R-019 | S-110, S-111 (lister exporteurs/importeurs) | R |
| R-020 | S-026, S-027 (rebuild + save) | W |

### Tier 2 — 10 recettes (R-030 a R-039)
| Recette | Scenarios couverts | Type |
|---------|-------------------|------|
| R-030 | S-061 (creer esquisse XY) | W |
| R-031 | S-062 (rectangle dans esquisse) | W |
| R-032 | S-063 (extrusion) | W |
| R-033 | S-072 (creer point 3D) | W |
| R-034 | S-073 (creer repere) | W |
| R-035 | S-081, S-082 (lister inclusions) | R |
| R-036 | S-083 (creer inclusion simple) | W |
| R-037 | S-100-103 (info famille) | R |
| R-038 | S-104-106 (creer famille explicite) | W |
| R-039 | S-077 (transformer element) | W |

### Tier 3 — 10 recettes (R-050 a R-059)
| Recette | Scenarios couverts | Type |
|---------|-------------------|------|
| R-050 | S-120 (lire tableau drafting) | R |
| R-051 | S-130-132 (parcourir BOM) | R |
| R-052 | S-140 (affecter materiau) | W |
| R-053 | S-160, S-163 (batch modify params) | RW |
| R-054 | S-014 (creer document) | W |
| R-055 | S-015 (creer dossier) | W |
| R-056 | S-193 (export CSV params) | R |
| R-057 | S-070 (lister shapes) | R |
| R-058 | S-122 (lire vues mise en plan) | R |
| R-059 | S-162 (modifier tolerances) | W |

### Bilan couverture (mis a jour 2026-04-07)
| Metric | Avant session | Apres session |
|--------|--------------|---------------|
| Scenarios catalogues | 0 | **129** |
| Scenarios avec recette | 0 | **~85/129 (66%)** |
| Scenarios test (T-xx) | 21 (read) | **33 (21 read + 12 write)** — 30/33 PASS |
| Recettes total | 0 | **40** (14 READ + 22 WRITE + 4 RW) |
| APIs dans le graphe | ~50% estime | **79% verifie (30/38 APIs testees)** |
| Gaps fixables | inconnu | **5** (PDM + Familles) |
| Gaps hors scope | inconnu | **3** (CAE, CAM, Drafting tables) |

### Decouverte critique : Pattern D post-EnsureIsDirty
Le docId CHANGE apres EnsureIsDirty(ref docId). Toute recherche d'elements
(GetParameters, GetFunctions, etc.) doit etre faite APRES, pas avant.
Bug documente : DEC-010, fix : M-51 pour topsolid_modify_script.
En attendant : utiliser topsolid_execute_script avec Pattern D complet.

### Scenarios NON couverts (44 restants)
Principalement :
- Multi-couches avance (S-150 a S-154) — APIs tres specifiques
- FEA/CAE (S-170 a S-173) — module CAE, pas dans le graphe
- CAM/NC (S-180 a S-182) — module CAM, pas dans le graphe
- Substitutions assemblage (S-088) — patterns tres complexes
- Inclusion parametree SmartObjects (S-084) — necessite R&D sur les drivers
- Percage, process usinage (S-191, S-192) — module Design avance
- Textures/coatings PBR (S-142) — pipeline creation tres specifique
