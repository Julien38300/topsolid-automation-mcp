# Recettes

130 recettes pre-construites dans `RecipeTool`. Le LLM selectionne par nom via `topsolid_run_recipe` -- aucune generation de code necessaire.

## Tableau interactif

Recherche, tri par colonne et filtres par categorie/mode.

<RecipeTable />

## Par categorie (detail)

### Proprietes PDM (9 recettes)

Les proprietes PDM (Product Data Management) sont les metadonnees du document TopSolid : designation, nom, reference et fabricant. Elles sont stockees dans le gestionnaire PDM et accessibles sans ouvrir le document en edition.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_designation` | Lit la description fonctionnelle du document (ex: "Bride de serrage"). Retourne `(vide)` si non renseignee. | `Pdm.GetDescription(pdmId)` | READ |
| `read_name` | Lit le nom unique du document dans l'arborescence projet (ex: "BRD-001"). | `Pdm.GetName(pdmId)` | READ |
| `read_reference` | Lit le numero de reference / part number (ex: "REF-2024-0042"). Retourne `(vide)` si non renseignee. | `Pdm.GetPartNumber(pdmId)` | READ |
| `read_manufacturer` | Lit le fabricant ou fournisseur associe au document. | `Pdm.GetManufacturer(pdmId)` | READ |
| `read_pdm_properties` | Retourne les 4 proprietes PDM d'un coup : nom, designation, reference, fabricant. Utile pour un apercu rapide. | `Pdm.Get{Name,Description,PartNumber,Manufacturer}` | READ |
| `set_designation` | Change la designation. Sauvegarde auto. | `Pdm.SetDescription` — `value` = texte libre | WRITE |
| `set_name` | Renomme le document dans le projet. Attention : peut casser des liens si d'autres documents referencent ce nom. | `Pdm.SetName` — `value` = nouveau nom | WRITE |
| `set_reference` | Change la reference / part number. Sauvegarde auto. | `Pdm.SetPartNumber` — `value` = nouvelle ref | WRITE |
| `set_manufacturer` | Change le fabricant. Sauvegarde auto. | `Pdm.SetManufacturer` — `value` = nom fabricant | WRITE |

### Navigation projet (22 recettes)

Recettes pour explorer l'arborescence du projet, chercher des documents, lister des dossiers. Le projet TopSolid est une hierarchie PDM avec dossiers et documents de differents types (.TopPrt, .TopAsm, .TopDft...).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_current_project` | Retourne le nom du projet actif dans TopSolid. Premiere verification utile. | `Pdm.GetCurrentProject()` → `Pdm.GetName()` | READ |
| `read_project_contents` | Parcourt recursivement toute l'arborescence : dossiers, sous-dossiers et documents. Indente par niveau. Affiche le total dossiers/documents. | `Pdm.GetConstituents()` recursif | READ |
| `search_document` | Recherche un document par nom (match partiel, CONTAINS). Retourne nom + designation + reference pour chaque resultat. | `Pdm.SearchDocumentByName(projId, value)` — `value` = texte a chercher | READ |
| `search_folder` | Recherche un dossier par nom dans le projet (match partiel). | `Pdm.GetConstituents()` + filtre nom — `value` = texte a chercher | READ |
| `open_document_by_name` | Cherche puis ouvre un document dans l'editeur TopSolid. Le document devient le document actif. | `Documents.Open(ref docId)` — `value` = nom du document | WRITE |
| `list_project_documents` | Liste TOUS les documents du projet avec designation et reference. Format tableau. | `Pdm.GetConstituents()` iteration complete | READ |
| `list_folder_documents` | Liste les documents d'un dossier specifique. Utile pour explorer un sous-ensemble du projet. | `Pdm.GetConstituents(folderId)` — `value` = nom du dossier | READ |
| `summarize_project` | Resume synthetique : nombre de documents par type (.TopPrt, .TopAsm, .TopDft...), nombre de dossiers. | Comptage par extension via `Pdm.GetType()` | READ |
| `count_documents_by_type` | Compte les documents groupes par type. Retourne un tableau type → quantite. | `Pdm.GetType()` sur chaque doc | READ |
| `list_documents_without_reference` | Detecte les pieces/assemblages dont le champ reference est vide. Utile pour l'audit qualite. | Filtre `GetPartNumber() == ""` | READ |
| `list_documents_without_designation` | Detecte les documents sans designation. Utile pour l'audit qualite. | Filtre `GetDescription() == ""` | READ |
| `search_parts_by_material` | Liste les pieces avec leur materiau et masse. Filtre optionnel sur le nom du materiau. | `Parameters.GetRealValue(Mass)` + materiau — `value` = filtre optionnel | READ |
| `read_where_used` | Where-used : trouve tous les documents qui referencent le document courant (assemblages parents, mises en plan). | `Pdm.SearchMajorRevisionBackReferences()` | READ |
| `read_revision_history` | Timeline des revisions majeures et mineures du document courant avec auteur et date. | `Pdm.GetMajorRevisions()` + `GetMinorRevisions()` | READ |
| `compare_revisions` | Compare les parametres entre la revision courante et la precedente. Montre les valeurs qui ont change. | Diff `GetParameters()` entre 2 revisions | READ |
| `find_modified_documents` | Liste les documents non sauvegardes (dirty) du projet. Utile avant un export ou une fermeture. | `Documents.IsDirty(docId)` sur chaque doc | READ |
| `batch_export_step` | Exporte TOUTES les pieces et assemblages du projet en STEP dans un dossier. Un fichier par document. | `Documents.Export()` en boucle — `value` = chemin dossier (optionnel) | READ |
| `batch_read_property` | Lit une propriete specifique sur tous les documents du projet. Retourne un tableau nom → valeur. | Iteration + `Pdm.Get{Description,PartNumber,...}` — `value` = nom propriete | READ |
| `batch_clear_author` | Vide le champ Auteur de tous les documents du projet. Utile pour anonymiser avant livraison. | `Pdm.SetAuthor(pdmId, "")` en boucle | WRITE |
| `clear_document_author` | Vide le champ Auteur du document courant uniquement. | `Pdm.SetAuthor(pdmId, "")` | WRITE |
| `batch_check_virtual` | Verifie la propriete "virtuel" (IsVirtualDocument) sur tous les documents. Liste les non-virtuels. | `Documents.IsVirtualDocument()` en boucle | READ |
| `batch_enable_virtual` | Active le mode virtuel sur tous les documents non-virtuels du projet. | `Documents.SetVirtualDocumentMode(true)` en boucle | WRITE |

### Parametres (6 recettes)

Les parametres TopSolid sont les variables du modele : cotes, dimensions, textes. Ils pilotent la geometrie parametrique. Les valeurs sont toujours en **SI** (metres, radians, kg).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_parameters` | Liste tous les parametres du document avec nom, valeur et type (Real, Integer, Boolean, Text). | `Parameters.GetParameters(docId)` + `GetParameterType` + `Get{Real,Integer,Boolean,Text}Value` | READ |
| `read_real_parameter` | Lit la valeur d'un parametre reel par nom (match partiel). Retourne la valeur en SI. Ex: 0.050000 = 50mm. | `Parameters.GetRealValue()` — `value` = nom du parametre | READ |
| `read_text_parameter` | Lit la valeur d'un parametre texte par nom (match partiel). | `Parameters.GetTextValue()` — `value` = nom du parametre | READ |
| `set_real_parameter` | Modifie un parametre reel. La valeur doit etre en SI (metres). Ex: 50mm → 0.05. | `Parameters.SetRealValue()` — `value` = `nom:valeurSI` (ex: `Longueur:0.15`) | WRITE |
| `set_text_parameter` | Modifie un parametre texte. | `Parameters.SetTextValue()` — `value` = `nom:valeur` (ex: `Materiau:Acier`) | WRITE |
| `compare_parameters` | Compare les parametres entre le document actif et un autre. Affiche les differences (valeur A vs B) et les parametres exclusifs a chacun. | Diff par nom de parametre — `value` = nom de l'autre document | READ |

### Masse, volume, dimensions (7 recettes)

Proprietes physiques calculees par TopSolid. Necessitent un materiau affecte pour la masse. Les valeurs systeme sont accessibles via les parametres speciaux ($Mass, $Volume, etc.).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_mass_volume` | Retourne masse (kg), volume (mm³), surface (mm²) depuis les proprietes systeme du document. Necessite un materiau pour la masse. | `Parameters` systeme : `$Mass`, `$Volume`, `$Surface Area` | READ |
| `assembly_mass_report` | Masse totale de l'assemblage, volume, surface, et nombre de pieces. Vue globale assemblage. | `GetParts()` + proprietes systeme agreges | READ |
| `read_material_density` | Calcule la densite (kg/m³) a partir de masse/volume. Utile pour verifier le materiau affecte. | `masse / volume` depuis parametres systeme | READ |
| `read_material` | Retourne le nom du materiau affecte et la densite calculee. | Parametres systeme `$Mass` + `$Volume` | READ |
| `read_part_dimensions` | Lit Height, Width, Length et Box Size depuis les proprietes systeme. Valeurs en mm. | `Parameters` systeme : `$Height`, `$Width`, `$Length`, `$Box Size` | READ |
| `read_bounding_box` | Boite englobante : min/max XYZ en mm. Calcule aussi les dimensions LxlxH. | Parametres systeme bounding box | READ |
| `read_inertia_moments` | Moments principaux d'inertie X, Y, Z. Utile pour le calcul de resistance. | `Parameters` systeme : `$Principal Moment of Inertia X/Y/Z` | READ |

### Geometrie et visualisation (5 recettes)

Acces a la geometrie du modele : points, reperes, esquisses, shapes (corps solides), operations (arbre de construction).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_3d_points` | Liste les points 3D avec coordonnees X, Y, Z en mm. | `Geometries3D.GetPoints()` → `GetPointGeometry()` (×1000 pour mm) | READ |
| `read_3d_frames` | Liste les reperes 3D (frames) par nom. | `Geometries3D.GetFrames()` | READ |
| `list_sketches` | Liste les esquisses 2D du document par nom. | `Sketches2D.GetSketches()` | READ |
| `read_shapes` | Liste les shapes (corps solides) avec nombre de faces par shape. | `Shapes.GetShapes()` → `GetFaceCount()` | READ |
| `read_operations` | Arbre de construction : liste les operations avec nom et type (Extrusion, Revolution, Pocket, etc.). | `Operations.GetOperations()` → `Elements.GetTypeFullName()` | READ |

### Attributs visuels (11 recettes)

Couleur, transparence, calques — tout ce qui concerne l'apparence visuelle des elements. Les couleurs sont en RGB (0-255). La transparence va de 0.0 (opaque) a 1.0 (invisible).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `attr_read_all` | Lit couleur, transparence, calque et visibilite de tous les elements. Vue complete des attributs visuels. | `Elements.GetColor/Transparency` + `Layers.GetLayer` | READ |
| `attr_read_color` | Lit la couleur RGB de chaque shape. Retourne "pas de couleur" si heritee du materiau. | `Elements.HasColor()` → `GetColor()` | READ |
| `attr_read_face_colors` | Lit les couleurs face par face. Utile quand chaque face a sa propre couleur. | `Shapes.GetFaces()` → `Elements.GetColor()` par face | READ |
| `attr_set_color` | Change la couleur d'un shape. Si plusieurs shapes, TopSolid demande une selection interactive. | `Elements.SetColor(target, Color)` — `value` = `R,G,B` (ex: `255,0,0`) | WRITE |
| `attr_set_color_all` | Change la couleur de TOUS les elements d'un coup. Pas de selection. | `Elements.SetColor()` en boucle — `value` = `R,G,B` | WRITE |
| `attr_replace_color` | Remplace une couleur specifique par une autre sur tous les elements. Ex: tout le vert → rouge. | Filtre par couleur source → `SetColor()` — `value` = `R1,G1,B1:R2,G2,B2` | WRITE |
| `attr_read_transparency` | Lit la transparence de chaque shape (0.0 a 1.0). | `Elements.HasTransparency()` → `GetTransparency()` | READ |
| `attr_set_transparency` | Change la transparence. Si plusieurs shapes, selection interactive. | `Elements.SetTransparency()` — `value` = `0.0` a `1.0` | WRITE |
| `attr_list_layers` | Liste les calques (layers) du document par nom. | `Layers.GetLayers()` | READ |
| `attr_assign_layer` | Deplace un element vers un calque. | `Layers.SetLayer(elemId, layerId)` — `value` = `nom_element:nom_calque` | WRITE |
| `select_shape` | Demande a l'utilisateur de cliquer sur un shape dans TopSolid. Retourne nom + nombre de faces. | `User.AskShape()` interactif | ASK |

### Selection interactive (3 recettes)

Recettes qui ouvrent une boite de dialogue TopSolid pour que l'utilisateur pointe un element a l'ecran. Utile quand l'IA ne peut pas determiner l'element automatiquement.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `select_shape` | L'utilisateur clique sur un corps solide. Retourne nom et nombre de faces. | `User.AskShape()` | ASK |
| `select_face` | L'utilisateur clique sur une face. Retourne le shape parent et l'index de la face. | `User.AskFace()` | ASK |
| `select_3d_point` | L'utilisateur clique un point dans l'espace 3D. Retourne les coordonnees X, Y, Z en mm. | `User.AskPoint3D()` | ASK |

### Assemblages (8 recettes)

Un assemblage TopSolid contient des inclusions (instances de pieces) avec des contraintes de positionnement. Chaque inclusion reference un document "definition".

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_assembly` | Verifie si le document courant est un assemblage. Si oui, liste les pieces. | `Assemblies.IsAssembly()` → `GetParts()` | READ |
| `list_inclusions` | Liste les inclusions avec le document de definition de chaque instance. Montre la structure assemblage → piece. | `Operations.GetOperations()` filtre `IsInclusion()` → `GetInclusionDefinitionDocument()` | READ |
| `read_occurrences` | Liste les occurrences (instances visibles) avec leur document de definition. | `Assemblies.GetParts()` + `GetOccurrenceDefinition()` | READ |
| `count_assembly_parts` | Compte les pieces groupees par reference (type). Retourne quantite par piece unique et total. Ex: "Vis M8: 4, Plaque: 2 → 6 pieces". | `GetParts()` groupe par `GetInclusionDefinitionDocument()` | READ |
| `rename_occurrence` | Renomme une occurrence dans l'assemblage. Match par nom partiel (insensible a la casse). | `Entities.SetFunctionOccurrenceName()` — `value` = `ancien_nom:nouveau_nom` | WRITE |
| `count_occurrences` **(v1.6.2)** | Compte total / inclusions / definitions uniques. | `GetParts()` + `IsOccurrence()` + `GetOccurrenceDefinition()` | READ |
| `list_inclusions_with_reference` **(v1.6.2)** | Pour chaque inclusion : nom + reference PDM + designation de la definition. Utile pour audit BOM. | `GetParts()` + `Pdm.GetPartNumber/GetDescription` | READ |
| `find_occurrence` **(v1.6.2)** | Recherche d'occurrence par fragment de nom. `value=fragment`. | Substring match sur `GetFriendlyName()` | READ |

### Familles (5 recettes)

Une famille TopSolid est un document parametrique avec des configurations (codes). Les drivers sont les parametres qui pilotent les variantes du catalogue.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_family` | Verifie si le document est une famille. Indique si elle est explicite (catalogue) ou implicite (parametrique). | `Families.IsFamily()` + `IsExplicit()` | READ |
| `read_family_codes` | Liste les codes (variantes) de la famille. Chaque code correspond a une configuration du catalogue. | `Families.GetCodes()` | READ |
| `check_family_drivers` | Verifie que les drivers (parametres pilotant le catalogue) ont une designation. Liste ceux sans description. | `Families.GetCatalogColumnParameters()` → `Elements.GetDescription()` | READ |
| `fix_family_drivers` | Genere automatiquement une designation pour les drivers sans description, en deduisant du nom du parametre (CamelCase → mots separes). | `Elements.SetDescription()` auto-genere — aucun `value` | WRITE |
| `batch_check_family_drivers` | Audit des drivers de TOUTES les familles du projet. Detecte les drivers sans designation sur chaque famille. | Iteration `IsFamily()` → `GetCatalogColumnParameters()` | READ |

### Export (7 recettes)

Export du document actif vers differents formats CAO/dessin. Le chemin est optionnel — si omis, TopSolid utilise le dossier par defaut (temp ou bureau). L'exporteur est selectionne automatiquement par nom.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `export_step` | Export STEP (AP214). Format universel d'echange CAO. Retourne le chemin du fichier cree. | `Documents.Export()` avec exporteur STEP — `value` = chemin (optionnel) | READ |
| `export_dxf` | Export DXF (AutoCAD). Pour les mises en plan ou profils 2D. | `Documents.Export()` avec exporteur DXF — `value` = chemin (optionnel) | READ |
| `export_pdf` | Export PDF. Pour les mises en plan principalement. | `Documents.Export()` avec exporteur PDF — `value` = chemin (optionnel) | READ |
| `export_stl` | Export STL (maillage triangles). Pour l'impression 3D ou la visualisation. | `Documents.Export()` avec exporteur STL — `value` = chemin (optionnel) | READ |
| `export_iges` | Export IGES. Format historique d'echange CAO, encore utilise dans l'aeronautique. | `Documents.Export()` avec exporteur IGES — `value` = chemin (optionnel) | READ |
| `list_exporters` | Liste tous les formats d'export disponibles dans l'installation TopSolid. | `Documents.GetExporterNames()` | READ |
| `export_bom_csv` | Exporte la nomenclature (BOM) du document en format texte avec colonnes separees. | Lecture `Nomenclatures` → format texte | READ |

### Audit et verification (8 recettes)

Outils de controle qualite : verification des proprietes, coherence des noms de parametres, materiaux manquants. Essentiels avant livraison ou archivage.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `audit_part` | Audit complet : proprietes PDM, parametres (nombre + liste), shapes, masse, volume, surface, materiau. Rapport synthetique. | Combinaison `Pdm.*` + `Parameters.*` + `Shapes.*` + parametres systeme | READ |
| `audit_assembly` | Audit assemblage : pieces, inclusions, occurrences, masse totale, structure. | `Assemblies.GetParts()` + `GetOccurrences()` + masse systeme | READ |
| `check_part` | Check-list qualite : designation renseignee ? Reference renseignee ? Materiau affecte ? Retourne OK/ATTENTION par critere. | Verification `GetDescription`, `GetPartNumber`, presence materiau | READ |
| `check_project` | Verification qualite sur tout le projet : liste les pieces sans designation et sans reference. | Iteration complete avec filtres | READ |
| `check_missing_materials` | Liste les pieces du projet qui n'ont pas de materiau affecte (masse = 0). | Filtre `$Mass == 0` sur tous les docs | READ |
| `audit_parameter_names` | Detecte les incoherences de convention dans les noms de parametres (CamelCase vs snake_case, doublons proches, caracteres speciaux). | Analyse syntaxique des noms via regex | READ |
| `batch_audit_parameter_names` | Meme audit sur tous les documents du projet. Rapport global. | Iteration complete + analyse syntaxique | READ |
| `batch_audit_driver_designations` | Liste les designations de drivers de toutes les familles pour inspection visuelle (fautes, incoherences). | `GetCatalogColumnParameters()` → `GetDescription()` sur toutes les familles | READ |

### Mise en plan (10 recettes)

La mise en plan (Drafting) est le dessin technique 2D genere a partir d'une piece ou assemblage 3D. Elle contient des vues, une echelle, un format papier.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_drafting` | Verifie si le document est une mise en plan. Retourne le nombre de vues et le format. | `Draftings.IsDrafting()` | READ |
| `open_drafting` | Cherche la mise en plan associee a la piece courante (via back-references PDM) et l'ouvre. | `Pdm.SearchMajorRevisionBackReferences()` filtre `.TopDft` → `Documents.Open()` | WRITE |
| `list_drafting_views` | Liste les vues du plan avec nom et titre. | `Draftings.GetDraftingViews()` | READ |
| `read_drafting_scale` | Lit l'echelle globale du plan et l'echelle de chaque vue individuellement. | `Draftings.GetScaleFactorParameterValue()` + `GetViewScaleFactor()` | READ |
| `read_drafting_format` | Format papier (A3/A4), dimensions en mm, nombre de pages, mode de projection. | `Draftings.GetDraftingFormatName/Dimensions/PageCount` | READ |
| `read_main_projection` | Identifie la piece source de la mise en plan et les vues principales. | `Draftings.GetMainProjectionSet()` | READ |
| `set_drafting_scale` **(v1.6.1)** | Change l'echelle globale du plan. `value=denominateur` (ex: `10` → 1:10). | `Draftings.SetScaleFactorParameterValue()` — Pattern D | WRITE |
| `set_drafting_format` **(v1.6.1)** | Change le format papier. `value=nom` (ex: `A3`, `A4`). | `Draftings.SetDraftingFormatName()` — Pattern D | WRITE |
| `set_projection_quality` **(v1.6.1)** | Qualite de projection. `value=exact` (precis) ou `fast` (rapide). | `Draftings.SetProjectionMode()` — Pattern D | WRITE |
| `print_drafting` **(v1.6.1)** | Imprime toutes les pages du plan (N&B, 300 DPI, a l'echelle). | `Draftings.Print()` | READ |

### Nomenclature / BOM (6 recettes)

La nomenclature (Bill of Materials) est le tableau des composants d'un assemblage. Elle est generalement dans la mise en plan.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_bom` | Verifie si le document contient une nomenclature. Retourne le nombre de colonnes. | `Boms.IsBom()` | READ |
| `read_bom_columns` | Liste les colonnes du tableau (Repere, Designation, Quantite, Reference...). | `Boms.GetColumnCount()` + `GetColumnTitle()` | READ |
| `read_bom_contents` | Lit le tableau complet : toutes les lignes actives avec toutes les cellules. Format texte tabulaire. | `Boms.GetRowChildrenRows()` + `GetRowContents()` | READ |
| `count_bom_rows` | Compte les lignes actives et inactives de la nomenclature. | `Boms.IsRowActive()` sur chaque ligne | READ |
| `activate_bom_row` **(v1.6.1)** | Active une ligne BOM par index. `value=row_index`. | `Boms.ActivateRow()` — Pattern D | WRITE |
| `deactivate_bom_row` **(v1.6.1)** | Desactive une ligne BOM par index. `value=row_index`. | `Boms.DeactivateRow()` — Pattern D | WRITE |

### Mise a plat / Depliage (3 recettes)

Le depliage (flat pattern) concerne la tolerie : une piece en tole est "depliee" pour obtenir le contour plat a decouper. Les plis ont un angle, un rayon et une longueur.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_unfolding` | Verifie si le document contient un depliage tolerie. Liste les plis detectes. | Detection via operations de type FlatPattern | READ |
| `read_bend_features` | Liste les plis avec angle (degres), rayon interieur (mm) et longueur (mm). | Proprietes des operations de pliage | READ |
| `read_unfolding_dimensions` | Dimensions du contour deplie : longueur et largeur du flan plat en mm. | Parametres systeme de depliage | READ |

### Document (7 recettes)

Operations sur le document actif : sauvegarde, reconstruction, proprietes utilisateur personnalisees.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `document_type` | Retourne le type du document actif : piece, assemblage, mise en plan, etc. Detecte aussi famille et mise a plat. | `IsAssembly()`, `IsDraftingDocument()`, `IsFamily()`, `GetType()` | READ |
| `save_document` | Sauvegarde le document actif. | `Pdm.Save(pdmId, true)` | WRITE |
| `rebuild_document` | Force la reconstruction du modele (recalcul de toutes les operations). Utile apres modification de parametres. | `Documents.Rebuild(docId)` | WRITE |
| `save_all_project` | Sauvegarde tous les documents du projet en une passe. | `Pdm.Save()` en boucle sur tous les docs | WRITE |
| `invoke_command` | Execute une commande menu TopSolid par son nom interne. Attention : certaines commandes ouvrent des dialogues. | `Application.InvokeCommand(value)` — `value` = nom de la commande | WRITE |

### Proprietes utilisateur et document (5 recettes)

Les proprietes utilisateur sont definies par l'entreprise (dossier PDM dedie) et materialisees dans chaque document comme des parametres speciaux. La resolution par `Pdm.GetTextUserProperty(pdmId, "name_string")` ne fonctionne pas — l'API attend une `PdmObjectId` pointant sur la definition. Le pattern fiable est d'iterer `Parameters.GetParameters()` et filtrer ceux avec une `UserPropertyDefinition` non-vide.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_user_property` **(v1.6.2 fixed)** | Lit une propriete utilisateur par son nom visible (text/real/integer/boolean, auto-type). | `Parameters.GetUserPropertyDefinition()` + match par `GetFriendlyName()` | READ |
| `list_user_properties` **(v1.6.2)** | Liste toutes les proprietes utilisateur du document avec type + valeur courante. | Iteration `GetParameters()` + filtre `GetUserPropertyDefinition() != Empty` | READ |
| `set_user_property` **(v1.6.2 fixed)** | Modifie une propriete utilisateur par son nom visible, parse la valeur selon le type. `value=nom:valeur`. | `Parameters.SetTextValue/SetRealValue/...` apres lookup UserPropertyDefinition | WRITE |
| `list_document_properties` **(v1.6.2)** | Liste TOUTES les proprietes document (systeme + user) via `IDocuments.GetProperties()` — utile pour decouvrir le fullName exact d'une propriete. | `Documents.GetProperties(docId)` + `GetPropertyType()` | READ |
| `read_document_property` **(v1.6.2)** | Lit n'importe quelle propriete document par son fullName (auto-detect type). `value=fullName`. | `Documents.GetPropertyType()` + `GetPropertyXxxValue()` | READ |

### Comparaison de documents (3 recettes)

Compare le document courant avec un autre document du meme projet. Utile pour verifier des differences entre revisions ou variantes.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `compare_document_operations` | Compare l'arbre de construction entre 2 documents. Montre les operations ajoutees, supprimees ou differentes. | `Operations.GetOperations()` diff par nom — `value` = nom de l'autre document | READ |
| `compare_document_entities` | Compare les entites (shapes, esquisses, points, reperes) entre 2 documents. Montre les differences de quantite. | Comptage `Shapes/Sketches/Points/Frames` — `value` = nom de l'autre document | READ |
| `compare_revisions` | Compare les parametres de la revision courante avec la revision precedente du meme document. | `Pdm.GetMajorRevisions()` → diff parametres | READ |

### Report de modifications (2 recettes)

Copie des proprietes d'un document vers un autre. Utile pour propager des modifications en serie.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `copy_parameters_to` | Copie les valeurs de parametres du document actif vers un autre. Match par nom de parametre. Ignore les parametres systeme ($Mass, etc.). | `Parameters.Set{Real,Text,Integer,Boolean}Value()` — `value` = nom du document cible | WRITE |
| `copy_pdm_properties_to` | Copie designation, reference et fabricant du document actif vers un autre. | `Pdm.Set{Description,PartNumber,Manufacturer}()` — `value` = nom du document cible | WRITE |

### Document virtuel (2 recettes)

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `enable_virtual_document` | Active le mode virtuel sur le document courant. Un document virtuel n'est pas sauvegarde physiquement. | `Documents.SetVirtualDocumentMode(docId, true)` | WRITE |
| `batch_enable_virtual` | Active le mode virtuel sur tous les documents non-virtuels du projet. | `SetVirtualDocumentMode(true)` en boucle | WRITE |

## Pattern de modification

Toutes les recettes WRITE suivent le meme pattern :

```csharp
TopSolidHost.Application.StartModification("Description", false);
TopSolidHost.Documents.EnsureIsDirty(ref docId);
// ... modifications ...
TopSolidHost.Application.EndModification(true, true);
TopSolidHost.Pdm.Save(pdmId, true);
```

::: danger
`EnsureIsDirty(ref docId)` change le `docId` ! Chercher les elements **APRES** cet appel, jamais avant.
:::

## Couleurs de reference

| Nom | RGB |
|-----|-----|
| rouge | `255,0,0` |
| vert | `0,128,0` |
| bleu | `0,0,255` |
| jaune | `255,255,0` |
| orange | `255,165,0` |
| blanc | `255,255,255` |
| noir | `0,0,0` |
| gris | `128,128,128` |

## Unites (TopSolid = SI)

| Grandeur | Unite SI | Conversion |
|----------|---------|------------|
| Longueurs | metres | 50 mm = `0.05` |
| Angles | radians | 45° = `0.785398` |
| Masses | kg | |
| Volumes | m³ | Affiche en mm³ (×10⁹) |
| Surfaces | m² | Affiche en mm² (×10⁶) |

## Tests LIVE

59/61 tests PASS sur TopSolid vivant (assemblage REF-NOEMID-TEST).

| Categorie | PASS | Total |
|-----------|------|-------|
| PDM read/write | 6 | 6 |
| Assemblage | 6 | 6 |
| Export (STEP/STL/IGES/DXF/PDF) | 5 | 5 |
| Attributs lecture | 5 | 5 |
| Parametres | 1 | 1 |
| Geometrie | 1 | 1 |
| Projet | 1 | 1 |

21 recettes non testees automatiquement (Ask* interactives, contexte specifique requis).

## Dataset LoRA

2114 entrees ShareGPT dans `data/lora-dataset-en.jsonl` pour fine-tuner le sous-agent 3B (`ministral-topsolid` v6 conversational). Couvre les 124 recettes + patterns multi-turn + error-handling + acknowledgments. Script regenerable : `scripts/generate-lora-dataset-en.py`.

Eval : **100/100** sur 50 questions (5 tiers, trivial → piege), multi-turn verifie manuellement.

## Pattern d'ecriture dans modify_script

Les recettes WRITE passent par `topsolid_modify_script` qui wrappe automatiquement `StartModification` / `EnsureIsDirty` / `EndModification` / `Pdm.Save`. Contraintes :

- **NE PAS utiliser `return "..."`** — le wrapper l'interdit.
- Utiliser `__message = "..."` pour personnaliser le message de retour.
- `return;` (void) est autorise pour les early exits — transforme en `goto __done;`.
- Variables pre-declarees : `docId`, `pdmId`, `__message`.

Exemple (extrait de `set_drafting_scale`) :
```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
double factor = 1.0 / denom;
TopSolidDraftingHost.Draftings.SetScaleFactorParameterValue(docId, factor);
__message = "OK: drafting scale set to 1:" + denom;
```
