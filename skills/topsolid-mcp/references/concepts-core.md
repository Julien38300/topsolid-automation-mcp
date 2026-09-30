---
type: skill-reference
scope: topsolid-mcp
domain: concepts-noyau-CAO
version: 1.0.0
created: 2026-09-30
sources:
  - data/chm-7.21/extracted-text/TopSolidDesign_Automation_Guide.txt (CHM 7.21 Design Automation, complet)
  - server/data/api/7.21.304.0/methods.json (1978 methodes)
  - server/src/Tools/RecipeTool.cs (132 recipes)
  - vault 01_TopSolid/ (T-1206, Transverse/, V8/Docs/, Guide_Utilisateur/)
---

# Concepts noyau TopSolid — pour un agent CAO

Toute signature citee est verifiee source en main : guide CHM Design Automation 7.21 `[CHM-D]`,
methods.json 7.21.304.0 `[API]`, code RecipeTool.cs `[REC]`, voûte 01_TopSolid `[V]`.
Nom sans source verifiee = interdit. Notion non confirmee = `[A VALIDER]`.

## 1. Identite produit

- Editeur : Missler Software / TopSolid SAS (ne JAMAIS ecrire Dassault). `[V8-AEC]`
- Noyau geometrique : Parasolid (Siemens), ferme, IP proprietaire. Les algorithmes de tolerie sont
  fermes (IP). `[V8-AEC]`
- Communication automation : WCF inter-process ; TopSolidHost (kernel) + TopSolidDesignHost (Design)
  + TopSolidDraftingHost (Drafting) ; TopSolidCamHost pour CAM. Si TopSolid ne tourne pas,
  TopSolidHost.Connect() le demarre. `[CHM-D]`
- Remote automation depuis v7.9 (TCP, TopSolidHost.DefineConnection(ip, port, null, 0)) ;
  multi-instances depuis v7.10 (TopSolidHostInstance, args -pipeName / -tcpPort). `[CHM-D]`
- Unites : SI partout. metres, radians, kg, m^3. Conversion mm->m = *0.001 (les recipes acceptent
  des mm et convertissent). `[CHM-D]`

## 2. Documents

- Extensions observees dans les recipes : .TopPrt (piece), .TopAsm (assemblage), .TopDft (mise en plan).
  Rôle exact de .TopPlm : `[A VALIDER]`.
- Type DocumentId = reference vers UNE revision mineure d'un objet PDM. Une nouvelle revision sauvegardee
  = nouveau DocumentId. GetPdmObject(DocumentId) et GetDocument(PdmObjectId) traduisent dans les deux sens.
  `[CHM-D]`
- Revisions : majeures (PdmMajorRevisionId) et mineures (PdmMinorRevisionId) par objet PDM ;
  GetMajorRevisions / GetMinorRevisions. Recipe : read_revision_history. `[CHM-D] [REC]`
- Recherche d'un document universel : SearchDocumentByUniversalId(PdmObjectId.Empty, "Domaine", "Nom") —
  les identifiants universels survivent aux replications PDM, pas aux imports par copie. `[CHM-D]`
- Ouvrir : GetDocument(pdmObjectId) puis Documents.Open(ref docId). EditedDocument = document edite
  (vide si aucun). GetOpenDocuments pour les documents ouverts. `[CHM-D] [API-7.21]`
- Document virtuel : IsVirtualDocument / SetVirtualDocumentMode (7.17). Recipe enable_virtual_document. `[API-7.21] [REC]`
- Freeze (7.12), Update, Refresh, Rebuild (recipes rebuild_document, save_document). `[API-7.21] [REC]`

## 3. Elements et feature tree

- Un document contient des elements (entites, operations...). Identifiant : ElementId, toujours rattache
  a un document. Sous-partie d'element (arete, face) = (ElementId, ItemLabel) encapsule en ElementItemId.
  `[CHM-D]`
- Recherche par nom : IElements.SearchByName(docId, nom). ATTENTION : les noms des elements systeme sont
  traduits dans la langue utilisateur ; le nom reel se lit en mode avance (-a) via la commande
  contextuelle "System Name". Exemple verbatim : repere absolu d'une piece =
  "$TopSolid.Kernel.DB.D3.Documents.ElementName.AbsoluteFrame". `[CHM-D]`
- Les recipes lisent le feature tree via read_operations ("Lists operations (feature tree)") et le
  comparent via compare_document_operations. Nommage convivial des elements de recipes :
  Elements.GetFriendlyName. `[REC]`
- L'arbre d'operations est un historique : modifier une operation amont reconstruit l'aval (V7 : arbre
  lineaire monothread). La creation de geometrie complexe se pilote donc par gabarits + parametres,
  pas par reconstruction A-Z. `arbre lineaire [V8-AEC]` ; `conception = gabarits + parametres + validation [T-1206]`
- Reperage de provenance d'une face : get_item_last_operation_name (recipe). `[REC]`

## 4. Parametres

- Les parametres sont des entites (donc des elements, identifiants ElementId).
  Les recipes les enumerent par GetParameters(docId) et les apparient par FriendlyName. `[CHM-D] [REC]`
- Type via ParameterType (GetParameterType) ; valeur lue/ecrite par GetXyzValue / SetXyzValue selon le
  type (Real, Boolean, Integer, Text...). Unite reelle via GetRealUnit (ex. mm, kg) ; la valeur est SI. `[CHM-D]`
- Recipes parametres (noms exacts) : read_parameters, read_real_parameter (Param value=name),
  read_text_parameter, set_real_parameter (value=name:SIvalue, ex. Length:0.15), set_text_parameter,
  creer_parametre_reel (value=name:unite:SIvaleur, unites supportees Length/Mass/Angle/NoUnit —
  via Parameters.CreateRealParameter + Elements.SetName), creer_parametre_formule (value=name:unite:formule,
  via CreateSmartRealParameter + SmartReal(unite, formule)). Syntaxe de formule : noms de parametres,
  operateurs, unites SI (ex. DiagBolt:Length:Longueur * 1.414). `[REC]`
- Parametres utilisateur : document de definition de type "user property" ; recherche par identifiant
  universel ; SearchUserPropertyParameter(docId, userPropertyDocId) puis CreateUserPropertyParameter si
  vide ; ecriture SetTextValue. Recipes : read_user_property, set_user_property. `[CHM-D] [REC]`
- Parametre dedie courant : GetAuthorParameter ("Author"). Recipe clear_document_author. `[CHM-D] [REC]`

## 5. Smart objects (valeurs pilotables)

- Un champs geometrique TopSolid accepte plusieurs formes d'entree : valeur brute, reference de parametre,
  formule, reference geometrique. Side automation = SmartObjects. `[CHM-D]`
- SmartReal 3 modes verbatim : SmartReal(UnitType.Length, 0.5) (valeur), SmartReal(element) (associatif),
  SmartReal(UnitType.Length, "d + 3mm") (formule). Existe aussi SmartInteger, SmartBoolean, SmartText,
  SmartPoint3D, SmartDirection3D, SmartAxis3D, SmartSection3D. `[CHM-D] [REC:extruder_esquisse]`
- Consequence conception : tout champ d'un gabarit alimente par un SmartReal reference a un parametre
  devient pilotable depuis le serveur MCP (set_real_parameter), sans recoder la recipe.

## 6. Esquisses et formes (ce que l'API sait creer)

- Esquisses 2D dans plan : ISketches2D ; esquisses 3D : ISketches3D. Une esquisse 2D enchassee dans un
  document 3D est manipulee par ISketches2D et situee par GetPlane (Plan3D) au lieu de GetFrame. `[CHM-D]`
- Creation : Sketches2D.CreateSketchIn2D(docId, SmartPoint2D, SmartDirection2D, false) ;
  Sketches2D.CreateSketchIn3D ; Sketches3D.CreateSketch. Modification d'esquisse locale :
  Sketches2D.StartModification(inSketchId) → CreateVertex/CreatePoint, CreateLineSegment, CreateProfile,
  EndModification. Verbatim CHM l.822-833 (`CreateVertex(new ...)`, `CreateLineSegment(vertex1Id,
  vertex2Id)`) + methods.json (CreateVertex/CreatePoint/CreateLineSegment/CreateProfile présents).
  `[CHM-D] [API-7.21] [REC:creer_esquisse_rectangle]`
- Lecture topologique : GetProfiles -> GetProfileSegments -> GetSegmentCurveRange / GetSegmentCurveType
  (CurveType.Line, CurveType.Circle...) -> GetSegmentLineCurve / GetSegmentCircleCurve. `[CHM-D]`
- Formes : IShapes.CreateExtrudedShape (7.7.201.80), CreateLoftedShape (7.10), CreateRevolvedShape
  (7.7). Exemple verbatim extrusion : CreateExtrudedShape(docId, new SmartSection3D(esquisse),
  SmartDirection3D.DZ, SmartReal(Longueur, h), SmartReal(Angle, 0), false, false).
  Recipes : creer_esquisse_rectangle (value=largeur:hauteur mm), extruder_esquisse (value=hauteur_mm). `[API-7.21] [REC]`
- Limites verifiees : PAS de methodes Fold/Bend/Unfold dans IShapes (7.21) ; drilling en LECTURE
  seulement (IFeatures.GetDrilling*Primitive, 7.14). La modelisation riche (contraintes d'esquisse,
  enlevements complexes) reste l'affaire d'un gabarit concu a la main. `[API-7.21] [T-1206]`
- Geometrie de reference : IGeometries3D.CreatePoint / CreateAxis / CreateAxisByTwoPoints (7.20) /
  CreateAxisWithExtent / CreateFrame / CreateFrameByPointAndTwoDirections / CreateFrameWithOffset /
  CreateOffsetPoint / CreatePlane / CreatePlaneWithExtent / CreateSmartFrame (7.15). `API-7.21`
- Formes (topologie) : sommet (point geometrique), arete (courbe), face (surface) ;
  Shapes.GetFaceEdges(faceId) relie une face a ses aretes. Types geometriques : Vector2D/3D,
  Direction2D/3D, Point2D/3D, Axis2D/3D, Plane3D, Frame2D/3D, Transform2D/3D + surcharges
  arithmetiques ((p1+p2)/2, ax.Origine + x*ax.Direction). `[CHM-D]`

## 7. Familles et pilotes

- Fonction produit : les familles de composants exposent des pilotes (codes) qui pilotent les inclusions ;
  certaines familles ont des contraintes internes (ex. PFF01 : variante pivot reservee au 50x50).
  `voûte Transverse (inference, faible densite)`
- Interface API verifiee (IAssemblies, namespace TopSolid.Cad.Design.Automating) :
  GetDriversFromFamily (7.19), GetFamilyDriverRelatedToCodeDriver, GetInclusionCodeAndDrivers (7.10) et
  ...2 (7.17), SetInclusionCodeAndDrivers(2), HasOptionalDrivers, IsOptionalDriver,
  GetOptionalDriverDefaultValue, GetConditionedDriversVisibilityStatus, GetLocalizedDriverName,
  GetDriverSubFolderName (7.17), HasDriverSubFolderOwner, IsWizardInclusion (7.19). `[API-7.21]`
- Recipes familles (noms exacts) : detect_family, read_family_codes, check_family_drivers,
  fix_family_drivers (affecte une designation aux pilotes qui en manquent, deduite du nom du parametre),
  batch_check_family_drivers, batch_audit_driver_designations. `[REC]`
- Regle fiabilite observee en prod : check_family_drivers / fix_family_drivers existent parce que les
  pilotes sans designation cassent l'usage des familles. Pattern de pilotage cascade famille->code :
  voir assemblages-pilotes.md (methode ticket 7.21).

## 8. Transactions et ecritures

- Toute modification passe par : StartModification("Action", false) -> try { ... EndModification(true, true) }
  catch { EndModification(false, false) } — enfin garantit la sortie du mode modif. Oublier EndModification = TopSolid
  verrouille. `CHM-D`
- Document doit etre modifiable : Documents.EnsureIsDirty(ref docId) — ref car une nouvelle revision peut
  etre produite : garder l'identifiant rafraichi, jamais une copie. `CHM-D`
- Les operations PDM de type check-in sont HORS StartModification/EndModification (non undoables). `CHM-D`
- StartModification peut echouer (commande de menu utilisateur en cours) : toujours tester le retour. `CHM-D`
- Verification de disponibilite d'une methode : TopSolidHost.Version (entier, ex. >= 706000000 pour 7.6)
  contre le champ since du methods.json. Une methode trop recente leve une exception. `CHM-D [API-7.21]`

## 9. Gabarits — modele mental

- Un gabarit = document piece (ou assemblage) concu a la main, dont les cotes cles sont des parametres
  nommes consommes par des SmartReal. L'agent ne dessine pas : il instancie/inclut le gabarit, fixe les
  parametres, reconstruit, valide. `T-1206`
- Seule creation de geometrie verifiee par API : primitives (esquisse rectangle -> profil -> extrusion/
  revolution/loft) et points/axes/plans/repere de reference. Pour tout le reste : gabarit + parametres.
  `API-7.21 [REC]`
- Boucle type (toutes recipes verifiees) : topsolid_list_recipes -> topsolid_get_state -> read_parameters
  / read_real_parameter -> set_real_parameter (ou creer_parametre_*) -> rebuild_document ->
  read_mass_volume / read_part_dimensions / audit_part -> save_document. `REC`
- Invariants anti-hallucination : ne jamais inventer un nom de parameter/family/recipe/method — les
  lire d'abord ; ne jamais supposer qu'une operation de gabarit existe sous un nom systeme sans la
  verifier (read_operations + read_parameters). `T-1206 [REC]`