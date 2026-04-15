# Recettes

113 recettes pre-construites dans `RecipeTool`. Le LLM selectionne par nom via `topsolid_run_recipe` -- aucune generation de code necessaire.

## Tableau interactif

Recherche, tri par colonne et filtres par categorie/mode.

<RecipeTable />

## Par categorie (detail)

### Proprietes PDM (9 recettes)

Les proprietes PDM (Product Data Management) sont les metadonnees du document TopSolid : designation, nom, reference et fabricant. Elles sont stockees dans le gestionnaire PDM et accessibles sans ouvrir le document en edition.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `lire_designation` | Lit la description fonctionnelle du document (ex: "Bride de serrage"). Retourne `(vide)` si non renseignee. | `Pdm.GetDescription(pdmId)` | READ |
| `lire_nom` | Lit le nom unique du document dans l'arborescence projet (ex: "BRD-001"). | `Pdm.GetName(pdmId)` | READ |
| `lire_reference` | Lit le numero de reference / part number (ex: "REF-2024-0042"). Retourne `(vide)` si non renseignee. | `Pdm.GetPartNumber(pdmId)` | READ |
| `lire_fabricant` | Lit le fabricant ou fournisseur associe au document. | `Pdm.GetManufacturer(pdmId)` | READ |
| `lire_proprietes_pdm` | Retourne les 4 proprietes PDM d'un coup : nom, designation, reference, fabricant. Utile pour un apercu rapide. | `Pdm.Get{Name,Description,PartNumber,Manufacturer}` | READ |
| `modifier_designation` | Change la designation. Sauvegarde auto. | `Pdm.SetDescription` — `value` = texte libre | WRITE |
| `modifier_nom` | Renomme le document dans le projet. Attention : peut casser des liens si d'autres documents referencent ce nom. | `Pdm.SetName` — `value` = nouveau nom | WRITE |
| `modifier_reference` | Change la reference / part number. Sauvegarde auto. | `Pdm.SetPartNumber` — `value` = nouvelle ref | WRITE |
| `modifier_fabricant` | Change le fabricant. Sauvegarde auto. | `Pdm.SetManufacturer` — `value` = nom fabricant | WRITE |

### Navigation projet (22 recettes)

Recettes pour explorer l'arborescence du projet, chercher des documents, lister des dossiers. Le projet TopSolid est une hierarchie PDM avec dossiers et documents de differents types (.TopPrt, .TopAsm, .TopDft...).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `lire_projet_courant` | Retourne le nom du projet actif dans TopSolid. Premiere verification utile. | `Pdm.GetCurrentProject()` → `Pdm.GetName()` | READ |
| `lire_contenu_projet` | Parcourt recursivement toute l'arborescence : dossiers, sous-dossiers et documents. Indente par niveau. Affiche le total dossiers/documents. | `Pdm.GetConstituents()` recursif | READ |
| `chercher_document` | Recherche un document par nom (match partiel, CONTAINS). Retourne nom + designation + reference pour chaque resultat. | `Pdm.SearchDocumentByName(projId, value)` — `value` = texte a chercher | READ |
| `chercher_dossier` | Recherche un dossier par nom dans le projet (match partiel). | `Pdm.GetConstituents()` + filtre nom — `value` = texte a chercher | READ |
| `ouvrir_document_par_nom` | Cherche puis ouvre un document dans l'editeur TopSolid. Le document devient le document actif. | `Documents.Open(ref docId)` — `value` = nom du document | WRITE |
| `lister_documents_projet` | Liste TOUS les documents du projet avec designation et reference. Format tableau. | `Pdm.GetConstituents()` iteration complete | READ |
| `lister_documents_dossier` | Liste les documents d'un dossier specifique. Utile pour explorer un sous-ensemble du projet. | `Pdm.GetConstituents(folderId)` — `value` = nom du dossier | READ |
| `resumer_projet` | Resume synthetique : nombre de documents par type (.TopPrt, .TopAsm, .TopDft...), nombre de dossiers. | Comptage par extension via `Pdm.GetType()` | READ |
| `compter_documents_par_type` | Compte les documents groupes par type. Retourne un tableau type → quantite. | `Pdm.GetType()` sur chaque doc | READ |
| `lister_documents_sans_reference` | Detecte les pieces/assemblages dont le champ reference est vide. Utile pour l'audit qualite. | Filtre `GetPartNumber() == ""` | READ |
| `lister_documents_sans_designation` | Detecte les documents sans designation. Utile pour l'audit qualite. | Filtre `GetDescription() == ""` | READ |
| `chercher_pieces_par_materiau` | Liste les pieces avec leur materiau et masse. Filtre optionnel sur le nom du materiau. | `Parameters.GetRealValue(Mass)` + materiau — `value` = filtre optionnel | READ |
| `lire_cas_emploi` | Where-used : trouve tous les documents qui referencent le document courant (assemblages parents, mises en plan). | `Pdm.SearchMajorRevisionBackReferences()` | READ |
| `lire_historique_revisions` | Timeline des revisions majeures et mineures du document courant avec auteur et date. | `Pdm.GetMajorRevisions()` + `GetMinorRevisions()` | READ |
| `comparer_revisions` | Compare les parametres entre la revision courante et la precedente. Montre les valeurs qui ont change. | Diff `GetParameters()` entre 2 revisions | READ |
| `chercher_documents_modifies` | Liste les documents non sauvegardes (dirty) du projet. Utile avant un export ou une fermeture. | `Documents.IsDirty(docId)` sur chaque doc | READ |
| `exporter_batch_step` | Exporte TOUTES les pieces et assemblages du projet en STEP dans un dossier. Un fichier par document. | `Documents.Export()` en boucle — `value` = chemin dossier (optionnel) | READ |
| `lire_propriete_batch` | Lit une propriete specifique sur tous les documents du projet. Retourne un tableau nom → valeur. | Iteration + `Pdm.Get{Description,PartNumber,...}` — `value` = nom propriete | READ |
| `vider_auteur_batch` | Vide le champ Auteur de tous les documents du projet. Utile pour anonymiser avant livraison. | `Pdm.SetAuthor(pdmId, "")` en boucle | WRITE |
| `vider_auteur_document` | Vide le champ Auteur du document courant uniquement. | `Pdm.SetAuthor(pdmId, "")` | WRITE |
| `verifier_virtuel_batch` | Verifie la propriete "virtuel" (IsVirtualDocument) sur tous les documents. Liste les non-virtuels. | `Documents.IsVirtualDocument()` en boucle | READ |
| `activer_virtuel_batch` | Active le mode virtuel sur tous les documents non-virtuels du projet. | `Documents.SetVirtualDocumentMode(true)` en boucle | WRITE |

### Parametres (6 recettes)

Les parametres TopSolid sont les variables du modele : cotes, dimensions, textes. Ils pilotent la geometrie parametrique. Les valeurs sont toujours en **SI** (metres, radians, kg).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `lire_parametres` | Liste tous les parametres du document avec nom, valeur et type (Real, Integer, Boolean, Text). | `Parameters.GetParameters(docId)` + `GetParameterType` + `Get{Real,Integer,Boolean,Text}Value` | READ |
| `lire_parametre_reel` | Lit la valeur d'un parametre reel par nom (match partiel). Retourne la valeur en SI. Ex: 0.050000 = 50mm. | `Parameters.GetRealValue()` — `value` = nom du parametre | READ |
| `lire_parametre_texte` | Lit la valeur d'un parametre texte par nom (match partiel). | `Parameters.GetTextValue()` — `value` = nom du parametre | READ |
| `modifier_parametre_reel` | Modifie un parametre reel. La valeur doit etre en SI (metres). Ex: 50mm → 0.05. | `Parameters.SetRealValue()` — `value` = `nom:valeurSI` (ex: `Longueur:0.15`) | WRITE |
| `modifier_parametre_texte` | Modifie un parametre texte. | `Parameters.SetTextValue()` — `value` = `nom:valeur` (ex: `Materiau:Acier`) | WRITE |
| `comparer_parametres` | Compare les parametres entre le document actif et un autre. Affiche les differences (valeur A vs B) et les parametres exclusifs a chacun. | Diff par nom de parametre — `value` = nom de l'autre document | READ |

### Masse, volume, dimensions (7 recettes)

Proprietes physiques calculees par TopSolid. Necessitent un materiau affecte pour la masse. Les valeurs systeme sont accessibles via les parametres speciaux ($Mass, $Volume, etc.).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `lire_masse_volume` | Retourne masse (kg), volume (mm³), surface (mm²) depuis les proprietes systeme du document. Necessite un materiau pour la masse. | `Parameters` systeme : `$Mass`, `$Volume`, `$Surface Area` | READ |
| `rapport_masse_assemblage` | Masse totale de l'assemblage, volume, surface, et nombre de pieces. Vue globale assemblage. | `GetParts()` + proprietes systeme agreges | READ |
| `lire_densite_materiau` | Calcule la densite (kg/m³) a partir de masse/volume. Utile pour verifier le materiau affecte. | `masse / volume` depuis parametres systeme | READ |
| `lire_materiau` | Retourne le nom du materiau affecte et la densite calculee. | Parametres systeme `$Mass` + `$Volume` | READ |
| `lire_dimensions_piece` | Lit Height, Width, Length et Box Size depuis les proprietes systeme. Valeurs en mm. | `Parameters` systeme : `$Height`, `$Width`, `$Length`, `$Box Size` | READ |
| `lire_boite_englobante` | Boite englobante : min/max XYZ en mm. Calcule aussi les dimensions LxlxH. | Parametres systeme bounding box | READ |
| `lire_moments_inertie` | Moments principaux d'inertie X, Y, Z. Utile pour le calcul de resistance. | `Parameters` systeme : `$Principal Moment of Inertia X/Y/Z` | READ |

### Geometrie et visualisation (5 recettes)

Acces a la geometrie du modele : points, reperes, esquisses, shapes (corps solides), operations (arbre de construction).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `lire_points_3d` | Liste les points 3D avec coordonnees X, Y, Z en mm. | `Geometries3D.GetPoints()` → `GetPointGeometry()` (×1000 pour mm) | READ |
| `lire_reperes_3d` | Liste les reperes 3D (frames) par nom. | `Geometries3D.GetFrames()` | READ |
| `lister_esquisses` | Liste les esquisses 2D du document par nom. | `Sketches2D.GetSketches()` | READ |
| `lire_shapes` | Liste les shapes (corps solides) avec nombre de faces par shape. | `Shapes.GetShapes()` → `GetFaceCount()` | READ |
| `lire_operations` | Arbre de construction : liste les operations avec nom et type (Extrusion, Revolution, Pocket, etc.). | `Operations.GetOperations()` → `Elements.GetTypeFullName()` | READ |

### Attributs visuels (11 recettes)

Couleur, transparence, calques — tout ce qui concerne l'apparence visuelle des elements. Les couleurs sont en RGB (0-255). La transparence va de 0.0 (opaque) a 1.0 (invisible).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `attribut_lire_tout` | Lit couleur, transparence, calque et visibilite de tous les elements. Vue complete des attributs visuels. | `Elements.GetColor/Transparency` + `Layers.GetLayer` | READ |
| `attribut_lire_couleur` | Lit la couleur RGB de chaque shape. Retourne "pas de couleur" si heritee du materiau. | `Elements.HasColor()` → `GetColor()` | READ |
| `attribut_lire_couleurs_faces` | Lit les couleurs face par face. Utile quand chaque face a sa propre couleur. | `Shapes.GetFaces()` → `Elements.GetColor()` par face | READ |
| `attribut_modifier_couleur` | Change la couleur d'un shape. Si plusieurs shapes, TopSolid demande une selection interactive. | `Elements.SetColor(target, Color)` — `value` = `R,G,B` (ex: `255,0,0`) | WRITE |
| `attribut_modifier_couleur_tout` | Change la couleur de TOUS les elements d'un coup. Pas de selection. | `Elements.SetColor()` en boucle — `value` = `R,G,B` | WRITE |
| `attribut_remplacer_couleur` | Remplace une couleur specifique par une autre sur tous les elements. Ex: tout le vert → rouge. | Filtre par couleur source → `SetColor()` — `value` = `R1,G1,B1:R2,G2,B2` | WRITE |
| `attribut_lire_transparence` | Lit la transparence de chaque shape (0.0 a 1.0). | `Elements.HasTransparency()` → `GetTransparency()` | READ |
| `attribut_modifier_transparence` | Change la transparence. Si plusieurs shapes, selection interactive. | `Elements.SetTransparency()` — `value` = `0.0` a `1.0` | WRITE |
| `attribut_lister_calques` | Liste les calques (layers) du document par nom. | `Layers.GetLayers()` | READ |
| `attribut_affecter_calque` | Deplace un element vers un calque. | `Layers.SetLayer(elemId, layerId)` — `value` = `nom_element:nom_calque` | WRITE |
| `selectionner_shape` | Demande a l'utilisateur de cliquer sur un shape dans TopSolid. Retourne nom + nombre de faces. | `User.AskShape()` interactif | ASK |

### Selection interactive (3 recettes)

Recettes qui ouvrent une boite de dialogue TopSolid pour que l'utilisateur pointe un element a l'ecran. Utile quand l'IA ne peut pas determiner l'element automatiquement.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `selectionner_shape` | L'utilisateur clique sur un corps solide. Retourne nom et nombre de faces. | `User.AskShape()` | ASK |
| `selectionner_face` | L'utilisateur clique sur une face. Retourne le shape parent et l'index de la face. | `User.AskFace()` | ASK |
| `selectionner_point_3d` | L'utilisateur clique un point dans l'espace 3D. Retourne les coordonnees X, Y, Z en mm. | `User.AskPoint3D()` | ASK |

### Assemblages (5 recettes)

Un assemblage TopSolid contient des inclusions (instances de pieces) avec des contraintes de positionnement. Chaque inclusion reference un document "definition".

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detecter_assemblage` | Verifie si le document courant est un assemblage. Si oui, liste les pieces. | `Assemblies.IsAssembly()` → `GetParts()` | READ |
| `lister_inclusions` | Liste les inclusions avec le document de definition de chaque instance. Montre la structure assemblage → piece. | `Operations.GetOperations()` filtre `IsInclusion()` → `GetInclusionDefinitionDocument()` | READ |
| `lire_occurrences` | Liste les occurrences (instances visibles) avec leur document de definition. | `Assemblies.GetOccurrences()` → `GetOccurrenceDefinitionDocument()` | READ |
| `compter_pieces_assemblage` | Compte les pieces groupees par reference (type). Retourne quantite par piece unique et total. Ex: "Vis M8: 4, Plaque: 2 → 6 pieces". | `GetParts()` groupe par `GetInclusionDefinitionDocument()` | READ |
| `renommer_occurrence` | Renomme une occurrence dans l'assemblage. Match par nom partiel (insensible a la casse). | `Entities.SetFunctionOccurrenceName()` — `value` = `ancien_nom:nouveau_nom` | WRITE |

### Familles (5 recettes)

Une famille TopSolid est un document parametrique avec des configurations (codes). Les drivers sont les parametres qui pilotent les variantes du catalogue.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detecter_famille` | Verifie si le document est une famille. Indique si elle est explicite (catalogue) ou implicite (parametrique). | `Families.IsFamily()` + `IsExplicit()` | READ |
| `lire_codes_famille` | Liste les codes (variantes) de la famille. Chaque code correspond a une configuration du catalogue. | `Families.GetCodes()` | READ |
| `verifier_drivers_famille` | Verifie que les drivers (parametres pilotant le catalogue) ont une designation. Liste ceux sans description. | `Families.GetCatalogColumnParameters()` → `Elements.GetDescription()` | READ |
| `corriger_drivers_famille` | Genere automatiquement une designation pour les drivers sans description, en deduisant du nom du parametre (CamelCase → mots separes). | `Elements.SetDescription()` auto-genere — aucun `value` | WRITE |
| `verifier_drivers_famille_batch` | Audit des drivers de TOUTES les familles du projet. Detecte les drivers sans designation sur chaque famille. | Iteration `IsFamily()` → `GetCatalogColumnParameters()` | READ |

### Export (7 recettes)

Export du document actif vers differents formats CAO/dessin. Le chemin est optionnel — si omis, TopSolid utilise le dossier par defaut (temp ou bureau). L'exporteur est selectionne automatiquement par nom.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `exporter_step` | Export STEP (AP214). Format universel d'echange CAO. Retourne le chemin du fichier cree. | `Documents.Export()` avec exporteur STEP — `value` = chemin (optionnel) | READ |
| `exporter_dxf` | Export DXF (AutoCAD). Pour les mises en plan ou profils 2D. | `Documents.Export()` avec exporteur DXF — `value` = chemin (optionnel) | READ |
| `exporter_pdf` | Export PDF. Pour les mises en plan principalement. | `Documents.Export()` avec exporteur PDF — `value` = chemin (optionnel) | READ |
| `exporter_stl` | Export STL (maillage triangles). Pour l'impression 3D ou la visualisation. | `Documents.Export()` avec exporteur STL — `value` = chemin (optionnel) | READ |
| `exporter_iges` | Export IGES. Format historique d'echange CAO, encore utilise dans l'aeronautique. | `Documents.Export()` avec exporteur IGES — `value` = chemin (optionnel) | READ |
| `lister_exporteurs` | Liste tous les formats d'export disponibles dans l'installation TopSolid. | `Documents.GetExporterNames()` | READ |
| `exporter_nomenclature_csv` | Exporte la nomenclature (BOM) du document en format texte avec colonnes separees. | Lecture `Nomenclatures` → format texte | READ |

### Audit et verification (8 recettes)

Outils de controle qualite : verification des proprietes, coherence des noms de parametres, materiaux manquants. Essentiels avant livraison ou archivage.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `audit_piece` | Audit complet : proprietes PDM, parametres (nombre + liste), shapes, masse, volume, surface, materiau. Rapport synthetique. | Combinaison `Pdm.*` + `Parameters.*` + `Shapes.*` + parametres systeme | READ |
| `audit_assemblage` | Audit assemblage : pieces, inclusions, occurrences, masse totale, structure. | `Assemblies.GetParts()` + `GetOccurrences()` + masse systeme | READ |
| `verifier_piece` | Check-list qualite : designation renseignee ? Reference renseignee ? Materiau affecte ? Retourne OK/ATTENTION par critere. | Verification `GetDescription`, `GetPartNumber`, presence materiau | READ |
| `verifier_projet` | Verification qualite sur tout le projet : liste les pieces sans designation et sans reference. | Iteration complete avec filtres | READ |
| `verifier_materiaux_manquants` | Liste les pieces du projet qui n'ont pas de materiau affecte (masse = 0). | Filtre `$Mass == 0` sur tous les docs | READ |
| `auditer_noms_parametres` | Detecte les incoherences de convention dans les noms de parametres (CamelCase vs snake_case, doublons proches, caracteres speciaux). | Analyse syntaxique des noms via regex | READ |
| `auditer_noms_parametres_batch` | Meme audit sur tous les documents du projet. Rapport global. | Iteration complete + analyse syntaxique | READ |
| `auditer_designations_drivers_batch` | Liste les designations de drivers de toutes les familles pour inspection visuelle (fautes, incoherences). | `GetCatalogColumnParameters()` → `GetDescription()` sur toutes les familles | READ |

### Mise en plan (6 recettes)

La mise en plan (Drafting) est le dessin technique 2D genere a partir d'une piece ou assemblage 3D. Elle contient des vues, une echelle, un format papier.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detecter_mise_en_plan` | Verifie si le document est une mise en plan. Retourne le nombre de vues et le format. | `DraftingDocuments.IsDraftingDocument()` | READ |
| `ouvrir_mise_en_plan` | Cherche la mise en plan associee a la piece courante (via back-references PDM) et l'ouvre. | `Pdm.SearchMajorRevisionBackReferences()` filtre `.TopDft` → `Documents.Open()` | WRITE |
| `lister_vues_mise_en_plan` | Liste les vues du plan avec nom, echelle et type (projection, coupe, detail, isometrique...). | `DraftingDocuments.GetViews()` → `GetViewScale()` | READ |
| `lire_echelle_mise_en_plan` | Lit l'echelle globale du plan et l'echelle de chaque vue individuellement. | `DraftingDocuments.GetDocumentScale()` + `GetViewScale()` par vue | READ |
| `lire_format_mise_en_plan` | Format papier : taille (A4, A3...), dimensions en mm, orientation, nombre de pages. | `DraftingDocuments.GetSheetSize/Margins/PageCount` | READ |
| `lire_projection_principale` | Identifie la piece source de la mise en plan et les vues principales. | `DraftingDocuments.GetProjectionDocument()` | READ |

### Nomenclature / BOM (4 recettes)

La nomenclature (Bill of Materials) est le tableau des composants d'un assemblage. Elle est generalement dans la mise en plan.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detecter_nomenclature` | Verifie si le document contient une nomenclature. Retourne le nombre de lignes. | `Nomenclatures.GetNomenclatures()` | READ |
| `lire_colonnes_nomenclature` | Liste les colonnes du tableau (Repere, Designation, Quantite, Reference...). | `Nomenclatures.GetColumns()` → `GetColumnName()` | READ |
| `lire_contenu_nomenclature` | Lit le tableau complet : toutes les lignes avec toutes les cellules. Format texte tabulaire. | `Nomenclatures.GetRows()` → `GetCellValue()` par colonne | READ |
| `compter_lignes_nomenclature` | Compte les lignes actives et inactives de la nomenclature. | `Nomenclatures.GetRows()` + `IsRowActive()` | READ |

### Mise a plat / Depliage (3 recettes)

Le depliage (flat pattern) concerne la tolerie : une piece en tole est "depliee" pour obtenir le contour plat a decouper. Les plis ont un angle, un rayon et une longueur.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detecter_mise_a_plat` | Verifie si le document contient un depliage tolerie. Liste les plis detectes. | Detection via operations de type FlatPattern | READ |
| `lire_plis_depliage` | Liste les plis avec angle (degres), rayon interieur (mm) et longueur (mm). | Proprietes des operations de pliage | READ |
| `lire_dimensions_depliage` | Dimensions du contour deplie : longueur et largeur du flan plat en mm. | Parametres systeme de depliage | READ |

### Document (7 recettes)

Operations sur le document actif : sauvegarde, reconstruction, proprietes utilisateur personnalisees.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `type_document` | Retourne le type du document actif : piece, assemblage, mise en plan, etc. Detecte aussi famille et mise a plat. | `IsAssembly()`, `IsDraftingDocument()`, `IsFamily()`, `GetType()` | READ |
| `sauvegarder_document` | Sauvegarde le document actif. | `Pdm.Save(pdmId, true)` | WRITE |
| `reconstruire_document` | Force la reconstruction du modele (recalcul de toutes les operations). Utile apres modification de parametres. | `Documents.Rebuild(docId)` | WRITE |
| `sauvegarder_tout_projet` | Sauvegarde tous les documents du projet en une passe. | `Pdm.Save()` en boucle sur tous les docs | WRITE |
| `lire_propriete_utilisateur` | Lit une propriete utilisateur personnalisee (texte). Ces proprietes sont definies par l'entreprise. | `Pdm.GetUserPropertyValue()` — `value` = nom de la propriete | READ |
| `modifier_propriete_utilisateur` | Modifie une propriete utilisateur. | `Pdm.SetUserPropertyValue()` — `value` = `nom:valeur` | WRITE |
| `invoquer_commande` | Execute une commande menu TopSolid par son nom interne. Attention : certaines commandes ouvrent des dialogues. | `Application.InvokeCommand(value)` — `value` = nom de la commande | WRITE |

### Comparaison de documents (3 recettes)

Compare le document courant avec un autre document du meme projet. Utile pour verifier des differences entre revisions ou variantes.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `comparer_operations_documents` | Compare l'arbre de construction entre 2 documents. Montre les operations ajoutees, supprimees ou differentes. | `Operations.GetOperations()` diff par nom — `value` = nom de l'autre document | READ |
| `comparer_entites_documents` | Compare les entites (shapes, esquisses, points, reperes) entre 2 documents. Montre les differences de quantite. | Comptage `Shapes/Sketches/Points/Frames` — `value` = nom de l'autre document | READ |
| `comparer_revisions` | Compare les parametres de la revision courante avec la revision precedente du meme document. | `Pdm.GetMajorRevisions()` → diff parametres | READ |

### Report de modifications (2 recettes)

Copie des proprietes d'un document vers un autre. Utile pour propager des modifications en serie.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `reporter_parametres` | Copie les valeurs de parametres du document actif vers un autre. Match par nom de parametre. Ignore les parametres systeme ($Mass, etc.). | `Parameters.Set{Real,Text,Integer,Boolean}Value()` — `value` = nom du document cible | WRITE |
| `reporter_proprietes_pdm` | Copie designation, reference et fabricant du document actif vers un autre. | `Pdm.Set{Description,PartNumber,Manufacturer}()` — `value` = nom du document cible | WRITE |

### Document virtuel (2 recettes)

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `activer_virtuel_document` | Active le mode virtuel sur le document courant. Un document virtuel n'est pas sauvegarde physiquement. | `Documents.SetVirtualDocumentMode(docId, true)` | WRITE |
| `activer_virtuel_batch` | Active le mode virtuel sur tous les documents non-virtuels du projet. | `SetVirtualDocumentMode(true)` en boucle | WRITE |

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

732 paires d'entrainement dans `data/lora-dataset.jsonl` pour fine-tuner le sous-agent 3B. Script regenerable : `scripts/generate-lora-dataset.py`.
