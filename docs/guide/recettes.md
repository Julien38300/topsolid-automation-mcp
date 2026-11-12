# Recettes

223 recettes pre-construites. Le LLM les appelle par leur nom via `topsolid_run_recipe` : aucune generation de code necessaire.

## Catalogue

Le tableau ci-dessous est construit a chaque publication de la doc a partir de `server/data/recipes.md`, le catalogue genere depuis le code du serveur (`server/scripts/gen-recipes-catalog.py`) : les noms sont exactement ceux a passer a `topsolid_run_recipe`. Dans un agent, `topsolid_list_recipes` donne la meme liste, filtrable par categorie.

<RecipeTable />

Il manque une recette ? [Demandez-la](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=recipe_request.yml) ou [ajoutez-la](./contribuer).

## Par categorie (detail)

::: warning Detail ecrit a la main
Cette partie explique la technique de chaque recette. Elle est maintenue a la main et peut avoir du retard sur le code : en cas de doute, le tableau ci-dessus et `topsolid_get_recipe` font foi.
:::

### Proprietes PDM

Les proprietes PDM (Product Data Management) sont les metadonnees du document TopSolid : designation, nom, reference et fabricant. Elles sont stockees dans le gestionnaire PDM et accessibles sans ouvrir le document en edition.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_designation` | Lit la description fonctionnelle du document (ex: "Bride de serrage"). Retourne `(vide)` si non renseignee. | `Pdm.GetDescription(pdmId)` | READ |
| `read_name` | Lit le nom unique du document dans l'arborescence projet (ex: "BRD-001"). | `Pdm.GetName(pdmId)` | READ |
| `read_reference` | Lit le numero de reference / part number (ex: "REF-2024-0042"). Retourne `(vide)` si non renseignee. | `Pdm.GetPartNumber(pdmId)` | READ |
| `read_manufacturer` | Lit le fabricant ou fournisseur associe au document. | `Pdm.GetManufacturer(pdmId)` | READ |
| `read_pdm_properties` | Retourne les 4 proprietes PDM d'un coup : nom, designation, reference, fabricant. Utile pour un apercu rapide. | `Pdm.Get{Name,Description,PartNumber,Manufacturer}` | READ |
| `set_designation` | Change la designation. Sauvegarde auto. | `Pdm.SetDescription` — `value` = texte libre | WRITE-PDM |
| `set_name` | Renomme le document dans le projet. Attention : peut casser des liens si d'autres documents referencent ce nom. | `Pdm.SetName` — `value` = nouveau nom | WRITE-PDM |
| `set_reference` | Change la reference / part number. Sauvegarde auto. | `Pdm.SetPartNumber` — `value` = nouvelle ref | WRITE-PDM |
| `set_manufacturer` | Change le fabricant. Sauvegarde auto. | `Pdm.SetManufacturer` — `value` = nom fabricant | WRITE-PDM |

### Navigation projet

Recettes pour explorer l'arborescence du projet, chercher des documents, lister des dossiers. Le projet TopSolid est une hierarchie PDM avec dossiers et documents de differents types (.TopPrt, .TopAsm, .TopDft...).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_current_project` | Retourne le nom du projet actif dans TopSolid. Premiere verification utile. | `Pdm.GetCurrentProject()` → `Pdm.GetName()` | READ |
| `read_project_contents` | Parcourt recursivement toute l'arborescence : dossiers, sous-dossiers et documents. Indente par niveau. Affiche le total dossiers/documents. | `Pdm.GetConstituents()` recursif | READ |
| `search_document` | Recherche un document par nom (match partiel, CONTAINS). Retourne nom + designation + reference pour chaque resultat. | `Pdm.SearchDocumentByName(projId, value)` — `value` = texte a chercher | READ |
| `search_folder` | Recherche un dossier par nom dans le projet (match partiel). | `Pdm.GetConstituents()` + filtre nom — `value` = texte a chercher | READ |
| `open_document_by_name` | Cherche puis ouvre un document dans l'editeur TopSolid. Le document devient le document actif. | `Documents.Open(ref docId)` — `value` = nom du document | READ |
| `list_project_documents` | Liste TOUS les documents du projet avec designation et reference. Format tableau. | `Pdm.GetConstituents()` iteration complete | READ |
| `list_folder_documents` | Liste les documents d'un dossier specifique. Utile pour explorer un sous-ensemble du projet. | `Pdm.GetConstituents(folderId)` — `value` = nom du dossier | READ |
| `summarize_project` | Resume synthetique : nombre de documents par type (.TopPrt, .TopAsm, .TopDft...), nombre de dossiers. | Comptage par extension via `Pdm.GetType()` | READ |
| `count_documents_by_type` | Compte les documents groupes par type. Retourne un tableau type → quantite. | `Pdm.GetType()` sur chaque doc | READ |
| `list_documents_without_reference` | Detecte les pieces/assemblages dont le champ reference est vide. Utile pour l'audit qualite. | Filtre `GetPartNumber() == ""` | READ |
| `list_documents_without_designation` | Detecte les documents sans designation. Utile pour l'audit qualite. | Filtre `GetDescription() == ""` | READ |
| `search_parts_by_material` | Liste les pieces avec leur materiau et masse. Filtre optionnel sur le nom du materiau. | `Parameters.GetRealValue(Mass)` + materiau — `value` = filtre optionnel | READ |
| `read_where_used` | Where-used : trouve tous les documents qui referencent le document courant (assemblages parents, mises en plan). | `Pdm.SearchMajorRevisionBackReferences()` | READ |
| `read_revision_history` | Timeline des revisions majeures et mineures du document courant avec auteur et date. | `Pdm.GetMajorRevisions()` + `GetMinorRevisions()` | READ |
| `find_modified_documents` | Liste les documents non sauvegardes (dirty) du projet. Utile avant un export ou une fermeture. | `Documents.IsDirty(docId)` sur chaque doc | READ |
| `batch_export_step` | Exporte TOUTES les pieces et assemblages du projet en STEP dans un dossier. Un fichier par document. | `Documents.Export()` en boucle — `value` = chemin dossier (optionnel) | WRITE-DISK |
| `batch_read_property` | Lit une propriete specifique sur tous les documents du projet. Retourne un tableau nom → valeur. | Iteration + `Pdm.Get{Description,PartNumber,...}` — `value` = nom propriete | READ |
| `batch_clear_author` | Vide le champ Auteur de tous les documents du projet. Utile pour anonymiser avant livraison. | `Pdm.SetAuthor(pdmId, "")` en boucle | WRITE-PDM |
| `batch_set_designation` **(v1.6.7)** | Applique la meme designation a TOUS les documents du projet. | `Pdm.SetDescription()` en boucle — `value` = texte | WRITE-PDM |
| `batch_set_reference` **(v1.6.7)** | Applique la meme reference a TOUS les documents du projet. | `Pdm.SetPartNumber()` en boucle — `value` = reference | WRITE-PDM |
| `batch_set_manufacturer` **(v1.6.7)** | Applique le meme fabricant a TOUS les documents du projet. | `Pdm.SetManufacturer()` en boucle — `value` = fabricant | WRITE-PDM |
| `clear_document_author` | Vide le champ Auteur du document courant uniquement. | `Pdm.SetAuthor(pdmId, "")` | WRITE-PDM |
| `batch_check_virtual` | Verifie la propriete "virtuel" (IsVirtualDocument) sur tous les documents. Liste les non-virtuels. | `Documents.IsVirtualDocument()` en boucle | READ |
| `create_project_tree` | Cree ou reutilise un projet PDM avec ses dossiers (separes par des virgules) et le definit comme projet courant. Premiere recette d'un flux autonome. | `Pdm.CreateProject()` + `Pdm.CreateFolder()` — `value` = `projet[dossier1,dossier2,...]` | WRITE-PDM |
| `update_where_used_and_save` | Recalcule et sauvegarde tous les documents du projet courant dans l'ordre bottom-up : pieces (`.TopPrt`), puis sous-assemblages, puis assemblage racine, pour que tous les liens Where-Used restent coherents. | `Documents.Update()` + `Pdm.Save()` en ordre de dependances — `value` = `[nom_projet]` | WRITE-PDM |

### Parametres

Les parametres TopSolid sont les variables du modele : cotes, dimensions, textes. Ils pilotent la geometrie parametrique. Les valeurs sont toujours en **SI** (metres, radians, kg).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_parameters` | Liste tous les parametres du document avec nom, valeur et type (Real, Integer, Boolean, Text). | `Parameters.GetParameters(docId)` + `GetParameterType` + `Get{Real,Integer,Boolean,Text}Value` | READ |
| `read_real_parameter` | Lit la valeur d'un parametre reel par nom (match partiel). Retourne la valeur en SI. Ex: 0.050000 = 50mm. | `Parameters.GetRealValue()` — `value` = nom du parametre | READ |
| `read_text_parameter` | Lit la valeur d'un parametre texte par nom (match partiel). | `Parameters.GetTextValue()` — `value` = nom du parametre | READ |
| `set_real_parameter` | Modifie un parametre reel. La valeur doit etre en SI (metres). Ex: 50mm → 0.05. | `Parameters.SetRealValue()` — `value` = `nom:valeurSI` (ex: `Longueur:0.15`) | WRITE-PDM |
| `set_text_parameter` | Modifie un parametre texte. | `Parameters.SetTextValue()` — `value` = `nom:valeur` (ex: `Materiau:Acier`) | WRITE-PDM |
| `compare_parameters` | Compare les parametres entre le document actif et un autre. Affiche les differences (valeur A vs B) et les parametres exclusifs a chacun. | Diff par nom de parametre — `value` = nom de l'autre document | READ |
| `creer_parametre_reel` | Cree un nouveau parametre reel dans le document actif. Unites supportees : Length, Mass, Angle, NoUnit. | `Parameters.CreateRealParameter()` + `Elements.SetName()` — `value` = `nom:unite:valeurSI` (ex: `Longueur:Length:0.05` pour 50 mm) | WRITE-PDM |
| `creer_parametre_formule` | Cree un parametre reel pilote par une formule TopSolid (syntaxe expression : noms de parametres, operateurs, unites SI). | `Parameters.CreateSmartRealParameter()` — `value` = `nom:unite:formule` (ex: `DiagBolt:Length:Longueur * 1.414`) | WRITE-PDM |

### Masse, volume, dimensions

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

### Materiaux et revetements

Un materiau TopSolid (`.TopMat`) porte les proprietes physiques de la piece (masse, resistance, comportement thermique...) ; un revetement (`.TopCot`) porte son aspect (couleur, brillance, texture). `set_material` et `set_coating` affectent une reference standard trouvee dans les bibliotheques ; les recettes `get_*` / `set_*` ci-dessous lisent ou reglent chaque propriete du materiau ou du revetement applique au document edite.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `set_material` | Cherche un materiau standard (`.TopMat`) dans les bibliotheques de materiaux et l'affecte a la piece, en activant les proprietes physiques (masse, centre de gravite). Recherche generique < 250 ms. | `Parts.SetMaterial()` + recherche PDM filtree `.TopMat` — `value` = `[nom_doc;]mot_cle` (ex: `S235JR`) | WRITE-PDM |
| `set_coating` | Cherche un revetement standard (`.TopCot`, ex: `RAL 7016`) dans les bibliotheques et l'applique a toutes les formes de la piece — ou applique une couleur RVB directe. | `Coatings` + `Elements.SetColor()` — `value` = `[nom_doc;]mot_cle[;R,G,B]` | WRITE-PDM |
| `get_material_brinell_hardness_factor` | Lit la propriete « durete Brinell » du materiau actif, depuis son fichier de normes. Signale aussi l'erreur quand aucun materiau n'est affecte. | `Materials.GetBrinellHardnessFactor()` | READ |
| `get_material_vickers_hardness_factor` | Lit la propriete « durete Vickers » du materiau actif, depuis son fichier de normes. Signale aussi l'erreur quand aucun materiau n'est affecte. | `Materials.GetVickersHardnessFactor()` | READ |
| `get_material_glossiness_factor` | Lit la propriete « brillance » du materiau actif. Signale aussi l'erreur quand aucun materiau n'est affecte. | `Materials.GetGlossinessFactor()` | READ |
| `get_material_bump_scale` | Lit la propriete « echelle de texture bump » du materiau actif. Signale aussi l'erreur quand aucun materiau n'est affecte. | `Materials.GetBumpScale()` | READ |
| `get_material_bump_rotation` | Lit la rotation de la texture bump du materiau actif (radians). Signale aussi l'erreur quand aucun materiau n'est affecte. | `Materials.GetBumpRotation()` | READ |
| `get_material_anisotropy_angle_factor` | Lit le facteur d'angle d'anisotropie du materiau actif. Signale aussi l'erreur quand aucun materiau n'est affecte. | `Materials.GetAnisotropyAngleFactor()` | READ |
| `get_coating_brinell_hardness_factor` | Lit la propriete « durete Brinell » du revetement actif. | `Coatings.GetBrinellHardnessFactor()` | READ |
| `get_coating_vickers_hardness_factor` | Lit la propriete « durete Vickers » du revetement actif. | `Coatings.GetVickersHardnessFactor()` | READ |
| `get_coating_glossiness_factor` | Lit la propriete « brillance » du revetement actif. | `Coatings.GetGlossinessFactor()` | READ |
| `get_coating_bump_scale` | Lit la propriete « echelle de texture bump » du revetement actif. | `Coatings.GetBumpScale()` | READ |
| `get_coating_bump_rotation` | Lit la rotation de la texture bump du revetement actif (radians). | `Coatings.GetBumpRotation()` | READ |
| `get_coating_anisotropy_angle_factor` | Lit le facteur d'angle d'anisotropie du revetement actif. | `Coatings.GetAnisotropyAngleFactor()` | READ |
| `set_angular_scale` | Regle la propriete « echelle angulaire » du materiau applique au document edite. | `Materials.SetAngularScale()` — `value` = nombre | WRITE-PDM |
| `set_anisotropy_angle_factor` | Regle la propriete « angle d'anisotropie » du materiau applique au document edite. | `Materials.SetAnisotropyAngleFactor()` — `value` = nombre | WRITE-PDM |
| `set_brinell_hardness_factor` | Regle la propriete « durete Brinell » du materiau applique au document edite. | `Materials.SetBrinellHardnessFactor()` — `value` = nombre | WRITE-PDM |
| `set_bulk_modulus` | Regle la propriete « module de compressibilite (bulk modulus) » du materiau applique au document edite. | `Materials.SetBulkModulus()` — `value` = nombre | WRITE-PDM |
| `set_bump_rotation` | Regle la propriete « rotation de texture bump » du materiau applique au document edite. | `Materials.SetBumpRotation()` — `value` = nombre | WRITE-PDM |
| `set_bump_scale` | Regle la propriete « echelle de texture bump » du materiau applique au document edite. | `Materials.SetBumpScale()` — `value` = nombre | WRITE-PDM |
| `set_cutting_force` | Regle la propriete « force de coupe » du materiau applique au document edite. | `Materials.SetCuttingForce()` — `value` = nombre | WRITE-PDM |
| `set_cutting_force_increment` | Regle la propriete « increment de force de coupe » du materiau applique au document edite. | `Materials.SetCuttingForceIncrement()` — `value` = nombre | WRITE-PDM |
| `set_elastic_limit` | Regle la propriete « limite elastique » du materiau applique au document edite. | `Materials.SetElasticLimit()` — `value` = nombre | WRITE-PDM |
| `set_elongation_at_break` | Regle la propriete « allongement a la rupture » du materiau applique au document edite. | `Materials.SetElongationAtBreak()` — `value` = nombre | WRITE-PDM |
| `set_glossiness_factor` | Regle la propriete « brillance » du materiau applique au document edite. | `Materials.SetGlossinessFactor()` — `value` = nombre | WRITE-PDM |
| `set_melting_temperature` | Regle la propriete « temperature de fusion » du materiau applique au document edite. | `Materials.SetMeltingTemperature()` — `value` = nombre | WRITE-PDM |
| `set_plastic_strain_ratio` | Regle la propriete « anisotropie plastique » du materiau applique au document edite. | `Materials.SetPlasticStrainRatio()` — `value` = nombre | WRITE-PDM |
| `set_poisson_ratio` | Regle la propriete « coefficient de Poisson » du materiau applique au document edite. | `Materials.SetPoissonRatio()` — `value` = nombre | WRITE-PDM |
| `set_section_reduction_at_break` | Regle la propriete « reduction de section a la rupture » du materiau applique au document edite. | `Materials.SetSectionReductionAtBreak()` — `value` = nombre | WRITE-PDM |
| `set_shear_modulus` | Regle la propriete « module de cisaillement » du materiau applique au document edite. | `Materials.SetShearModulus()` — `value` = nombre | WRITE-PDM |
| `set_shrinkage_factor` | Regle la propriete « retrait » du materiau applique au document edite. | `Materials.SetShrinkageFactor()` — `value` = nombre | WRITE-PDM |
| `set_specific_heat_capacity` | Regle la propriete « capacite thermique massique » du materiau applique au document edite. | `Materials.SetSpecificHeatCapacity()` — `value` = nombre | WRITE-PDM |
| `set_thermal_conductivity` | Regle la propriete « conductivite thermique » du materiau applique au document edite. | `Materials.SetThermalConductivity()` — `value` = nombre | WRITE-PDM |
| `set_thermal_expansion_coefficient` | Regle la propriete « dilatation thermique » du materiau applique au document edite. | `Materials.SetThermalExpansionCoefficient()` — `value` = nombre | WRITE-PDM |
| `set_ultimate_stress` | Regle la propriete « contrainte ultime » du materiau applique au document edite. | `Materials.SetUltimateStress()` — `value` = nombre | WRITE-PDM |
| `set_vertical_angular_scale` | Regle la propriete « echelle angulaire verticale » du materiau applique au document edite. | `Materials.SetVerticalAngularScale()` — `value` = nombre | WRITE-PDM |
| `set_vickers_hardness_factor` | Regle la propriete « durete Vickers » du materiau applique au document edite. | `Materials.SetVickersHardnessFactor()` — `value` = nombre | WRITE-PDM |
| `set_young_modulus` | Regle la propriete « module de Young » du materiau applique au document edite. | `Materials.SetYoungModulus()` — `value` = nombre | WRITE-PDM |
| `set_homogeneous_parameter_value` | Regle la valeur « homogene » (booleen) du materiau applique au document edite. | `Materials.SetHomogeneousParameterValue()` — `value` = vide (retire) / `true` / `false` | WRITE-PDM |
| `set_isotropic_parameter_value` | Regle la valeur « isotrope » (booleen) du materiau applique au document edite. | `Materials.SetIsotropicParameterValue()` — `value` = vide (retire) / `true` / `false` | WRITE-PDM |
| `set_rockwell_factors` | Regle les facteurs Rockwell (A, B, C) du materiau applique au document edite. | `Materials.SetRockwellFactors()` — `value` = `A,B,C` (nombre ou `null`) | WRITE-PDM |
| `set_specular_color` | Regle la couleur speculaire (RVB) du materiau applique au document edite. | `Materials.SetSpecularColor()` — `value` = `R,G,B` (0-255) | WRITE-PDM |
| `set_coating_brinell_hardness_factor` | Regle la propriete « durete Brinell » du revetement actif. | `Coatings.SetBrinellHardnessFactor()` — `value` = nombre | WRITE-PDM |
| `set_coating_bump_rotation` | Regle la propriete « rotation de texture bump » du revetement actif. | `Coatings.SetBumpRotation()` — `value` = nombre | WRITE-PDM |
| `set_coating_bump_scale` | Regle la propriete « echelle de texture bump » du revetement actif. | `Coatings.SetBumpScale()` — `value` = nombre | WRITE-PDM |
| `set_coating_vickers_hardness_factor` | Regle la propriete « durete Vickers » du revetement actif. | `Coatings.SetVickersHardnessFactor()` — `value` = nombre | WRITE-PDM |
| `set_coating_homogeneous_parameter_value` | Regle la valeur « homogene » (booleen) du revetement actif. | `Coatings.SetHomogeneousParameterValue()` — `value` = vide (retire) / `true` / `false` | WRITE-PDM |
| `set_coating_isotropic_parameter_value` | Regle la valeur « isotrope » (booleen) du revetement actif. | `Coatings.SetIsotropicParameterValue()` — `value` = vide (retire) / `true` / `false` | WRITE-PDM |
| `set_coating_rockwell_factors` | Regle les facteurs Rockwell (A, B, C) du revetement actif. | `Coatings.SetRockwellFactors()` — `value` = `A,B,C` (nombre ou `null`) | WRITE-PDM |

### Geometrie et visualisation

Acces a la geometrie du modele : points, reperes, esquisses, shapes (corps solides), operations (arbre de construction).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_3d_points` | Liste les points 3D avec coordonnees X, Y, Z en mm. | `Geometries3D.GetPoints()` → `GetPointGeometry()` (×1000 pour mm) | READ |
| `read_3d_frames` | Liste les reperes 3D (frames) par nom. | `Geometries3D.GetFrames()` | READ |
| `list_sketches` | Liste les esquisses 2D du document par nom. | `Sketches2D.GetSketches()` | READ |
| `read_shapes` | Liste les shapes (corps solides) avec nombre de faces par shape. | `Shapes.GetShapes()` → `GetFaceCount()` | READ |
| `read_operations` | Arbre de construction : liste les operations avec nom et type (Extrusion, Revolution, Pocket, etc.). | `Operations.GetOperations()` → `Elements.GetTypeFullName()` | READ |
| `get_profiles_folder` | Retourne l'entite « dossier des profils » du document actif (nom + identifiant). | `Geometries3D.GetProfilesFolder()` | READ |
| `get_operation_version` | Version d'algorithme d'une operation de l'arbre de construction. | `Operations.GetOperationVersion()` — `value` = nom de l'operation (trouvee via `read_operations`) | READ |

### Mesures B-Rep (selection interactive)

L'utilisateur pointe une face ou une arete dans TopSolid (meme selection interactive que `select_face`), puis la recette lit la geometrie B-Rep exacte de l'element pointe : cone, tore, ellipse, faces adjacentes. Sans ces recettes, un rayon ou un demi-angle doit etre deduit manuellement des parametres du modele.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `get_face_cone_length` | Longueur de la face conique pointee (mm). | `User.AskFace()` → `Shapes.GetFaceConeLength()` | READ |
| `get_face_cone_radius` | Rayon de base de la face conique pointee (mm). | `User.AskFace()` → `Shapes.GetFaceConeRadius()` | READ |
| `get_face_cone_semi_angle` | Demi-angle de la face conique pointee (degres). | `User.AskFace()` → `Shapes.GetFaceConeSemiAngle()` | READ |
| `get_face_torus_major_radius` | Rayon majeur de la face torique pointee (mm). | `User.AskFace()` → `Shapes.GetFaceTorusMajorRadius()` | READ |
| `get_face_torus_minor_radius` | Rayon mineur de la face torique pointee (mm). | `User.AskFace()` → `Shapes.GetFaceTorusMinorRadius()` | READ |
| `get_item_last_operation_name` | Nom de la derniere operation qui a produit la face pointee (quelle fonction a genere cette face). | `User.AskFace()` → `Operations.GetItemLastOperationName()` | READ |
| `get_edge_faces` | Faces adjacentes a l'arete pointee (nombre, noms et aires). | `User.AskEdge()` → `Shapes.GetEdgeFaces()` | READ |
| `get_edge_ellipse_curve` | Ellipse de l'arete elliptique pointee (plan + rayons, mm) ; verifie d'abord le type de courbe. | `User.AskEdge()` → `Shapes.GetEdgeCurveType()` + `Shapes.GetEdgeEllipseCurve()` | READ |

### Modelisation

Creation de geometrie parametrique. Les recettes `create_*` construisent une piece typee en une seule passe : document, esquisse, forme, materiau et revetement optionnels. Elles passent toutes par la transaction de modification (voir « Pattern de modification » plus bas) et ajoutent la forme a toutes les representations du document.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `creer_esquisse_rectangle` | Cree une esquisse 2D rectangulaire dans le document actif (plan XY, rectangle en 4 segments). | `Sketches2D.CreateSketchIn3D()` + `CreateVertex()` + `CreateLineSegment()` + `CleanSketch()` — `value` = `largeur:hauteur` (mm) | WRITE-PDM |
| `extruder_esquisse` | Extrude la derniere esquisse 2D du document en solide 3D et l'ajoute a toutes les representations. | `Shapes.CreateExtrudedShape()` + `Representations.AddRepresentationConstituent()` — `value` = hauteur en mm | WRITE-PDM |
| `smooth_sketch_profiles` | Lisse les profils de la derniere esquisse 2D (continuite G1 entre segments). ATTENTION : modifie l'esquisse. | `Sketches2D.GetProfiles()` + `SmoothSketchProfiles()` — `value` = vide (tolerances par defaut) ou tolerance en mm | WRITE-PDM |
| `create_sheet_metal_plate` | Cree en une passe une plaque / tole (`.TopPrt`) extrudee en XY selon +Z, avec trous circulaires debouchants optionnels dans la meme esquisse, parametre nomme `Epaisseur`, boite englobante minimale, materiau et revetement optionnels. | `Pdm.CreateDocument()` + `Sketches2D.CreateSketchIn3D()` (+ `CreateCircleSegment()` pour les trous) + `Shapes.CreateExtrudedShape()` + `Parts.SetMaterial()` — `value` = `nom;designation;ref;dossier;Lx_mm;Ly_mm;ep_mm[;trous_x_y_diam[;materiau[;revetement_ou_RVB[;ORIGIN\|CENTER]]]]` | WRITE-PDM |
| `create_extruded_bar` | Cree en une passe une piece profilee metallique (`.TopPrt`) : section typee `PLAT`, `TUBE_RECT`, `ROND`, `TUBE_ROND` ou `CORNIERE`, extrusion +Z (parametre `L`), trous transversaux optionnels, materiau et revetement. | `Pdm.CreateDocument()` + fonction « Extruded Bar » via `Entities.ProvideFunction()` + `Parts.CreateDrillingOperation()` + `Parts.SetMaterial()` — `value` = `nom;designation;ref;dossier;type_section;cotes_mm;longueur_mm[;trous[;materiau[;revetement]]]` | WRITE-PDM |
| `provide_extruded_bar_function` | Applique la fonction native TopSolid « Extruded Bar » sur une piece existante : repere 3D (section XY, X horizontal, Z longitudinal), esquisse de section, parametre `L`, points `BottomLeft`/`TopRight` publies et boite englobante minimale. | `Entities.ProvideFunction()` (fonction « Extruded Bar » de la bibliotheque) + `Geometries3D.CreateFrame()` + publications — `value` = `[nom_doc;]largeur_x_mm;hauteur_y_mm;longueur_z_mm` | WRITE-PDM |
| `create_drilling` | Perce un trou cylindrique sur la piece active au point (X, Y, Z) mm avec normale sortante (NX, NY, NZ), met a jour toutes les representations. | `Parts.CreateDrillingOperation()` avec `DrillingHolePrimitive` — `value` = `diam_mm:prof_mm:X:Y:Z:NX:NY:NZ` | WRITE-PDM |
| `create_revolved_part` | Cree une piece de revolution (`.TopPrt`) depuis un profil radial/axial ordonne (ferme automatiquement, revolution autour de l'axe Y absolu). | `Pdm.CreateDocument()` + `Sketches2D.CreateSketchIn3D()` + `Shapes.CreateRevolvedShape()` — `value` = `nom;points(rayon_mm,axial_mm\|...)[;angle_deg[;designation[;reference]]]` | WRITE-PDM |
| `revolve_active_part` | Ajoute un solide de revolution a la Piece active vide (refuse si elle contient deja un solide). Points en paires `rayon_mm,axial_mm` dans le plan XY, revolution autour de l'axe Y absolu. | `Sketches2D.CreateSketchIn3D()` + `Shapes.CreateRevolvedShape()` — `value` = `points(rayon_mm,axial_mm\|...)[;angle_deg]` | WRITE-PDM |
| `replace_revolved_profile` | Remplace le profil de l'unique solide de revolution de la Piece active (refuse les pieces avec d'autres fonctions volumiques). Segments `P` (point), `L` (ligne) et `A` (arc) en coordonnees XY mm. | `Sketches2D.GetSketches()` + `Elements.Delete()` de l'ancien profil + `Shapes.CreateRevolvedShape()` — `value` = `ancien_nom_esquisse;segments(P,r,z\|L,r,z\|A,r,z,cx,cz,CW_ou_CCW\|...)[;angle_deg]` | WRITE-PDM |

### Attributs visuels

Couleur, transparence, calques — tout ce qui concerne l'apparence visuelle des elements. Les couleurs sont en RGB (0-255). La transparence va de 0.0 (opaque) a 1.0 (invisible).

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `attr_read_all` | Lit couleur, transparence, calque et visibilite de tous les elements. Vue complete des attributs visuels. | `Elements.GetColor/Transparency` + `Layers.GetLayer` | READ |
| `attr_read_color` | Lit la couleur RGB de chaque shape. Retourne "pas de couleur" si heritee du materiau. | `Elements.HasColor()` → `GetColor()` | READ |
| `attr_read_face_colors` | Lit les couleurs face par face. Utile quand chaque face a sa propre couleur. | `Shapes.GetFaces()` → `Elements.GetColor()` par face | READ |
| `attr_set_color` | Change la couleur d'un shape. Si plusieurs shapes, TopSolid demande une selection interactive. | `Elements.SetColor(target, Color)` — `value` = `R,G,B` (ex: `255,0,0`) | WRITE-PDM |
| `attr_set_color_all` | Change la couleur de TOUS les elements d'un coup. Pas de selection. | `Elements.SetColor()` en boucle — `value` = `R,G,B` | WRITE-PDM |
| `attr_replace_color` | Remplace une couleur specifique par une autre sur tous les elements. Ex: tout le vert → rouge. | Filtre par couleur source → `SetColor()` — `value` = `R1,G1,B1:R2,G2,B2` | WRITE-PDM |
| `attr_read_transparency` | Lit la transparence de chaque shape (0.0 a 1.0). | `Elements.HasTransparency()` → `GetTransparency()` | READ |
| `attr_set_transparency` | Change la transparence. Si plusieurs shapes, selection interactive. | `Elements.SetTransparency()` — `value` = `0.0` a `1.0` | WRITE-PDM |
| `attr_list_layers` | Liste les calques (layers) du document par nom. | `Layers.GetLayers()` | READ |
| `attr_assign_layer` | Deplace un element vers un calque. | `Layers.SetLayer(elemId, layerId)` — `value` = `nom_element:nom_calque` | WRITE-PDM |

### Selection interactive

Recettes qui ouvrent une boite de dialogue TopSolid pour que l'utilisateur pointe un element a l'ecran. Utile quand l'IA ne peut pas determiner l'element automatiquement.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `select_shape` | L'utilisateur clique sur un corps solide. Retourne nom et nombre de faces. | `User.AskShape()` | READ |
| `select_face` | L'utilisateur clique sur une face. Retourne le shape parent et l'index de la face. | `User.AskFace()` | READ |
| `select_3d_point` | L'utilisateur clique un point dans l'espace 3D. Retourne les coordonnees X, Y, Z en mm. | `User.AskPoint3D()` | READ |

### Assemblages

Un assemblage TopSolid contient des inclusions (instances de pieces) avec des contraintes de positionnement. Chaque inclusion reference un document "definition".

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_assembly` | Verifie si le document courant est un assemblage. Si oui, liste les pieces. | `Assemblies.IsAssembly()` → `GetParts()` | READ |
| `list_inclusions` | Liste les inclusions avec le document de definition de chaque instance. Montre la structure assemblage → piece. | `Operations.GetOperations()` filtre `IsInclusion()` → `GetInclusionDefinitionDocument()` | READ |
| `read_occurrences` | Liste les occurrences (instances visibles) avec leur document de definition. | `Assemblies.GetParts()` + `GetOccurrenceDefinition()` | READ |
| `count_assembly_parts` | Compte les pieces groupees par reference (type). Retourne quantite par piece unique et total. Ex: "Vis M8: 4, Plaque: 2 → 6 pieces". | `GetParts()` groupe par `GetInclusionDefinitionDocument()` | READ |
| `rename_occurrence` | Renomme une occurrence dans l'assemblage. Match par nom partiel (insensible a la casse). | `Entities.SetFunctionOccurrenceName()` — `value` = `ancien_nom:nouveau_nom` | WRITE-PDM |
| `ajouter_inclusion` | Ajoute une inclusion d'un document piece dans l'assemblage actif. Le document cible doit etre ouvert. | `Assemblies.CreatePositioning()` + `Assemblies.CreateInclusion()` — `value` = nom du document piece | WRITE-PDM |
| `add_inclusion_positioned` | Inclut une piece ou un sous-assemblage avec une position explicite : soit `tx,ty,tz[,rx,ry,rz]`, soit `BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]` qui aligne l'axe +Z d'un profile extrude de P1 vers P2 et la section X selon uX. | `Assemblies.CreatePositioning()` + `CreateInclusion()` avec transform — `value` = `[asm\|]doc;tx,ty,tz[,rx,ry,rz]` ou `[asm\|]doc;BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]` | WRITE-PDM |
| `batch_add_inclusions` | Inclusions multiples dans un assemblage en une seule transaction. Le prefixe `CLEAR\|` supprime d'abord les occurrences existantes ; chaque item est separe par `;`. | `Assemblies.CreateInclusion()` en boucle — `value` = `[asm\|][CLEAR\|]item1;item2` (item : `doc,tx,ty,tz[,rx,ry,rz[,nom_occ]]` ou `doc,BETWEEN,x1,...,z2[,...]`) | WRITE-PDM |
| `list_positionings` | Liste les operations de positionnement de l'assemblage actif avec leur statut de contrainte (sous-contraint ou completement contraint). | `Operations.GetOperations()` filtre positionnements + `Assemblies.IsPositioningUnderconstrained()` | READ |
| `is_positioning_underconstrained` | Indique si un positionnement de l'assemblage actif est sous-contraint. | `Assemblies.IsPositioningUnderconstrained()` — `value` = nom du positionnement (exact ou fragment ; voir `list_positionings`) | READ |

### Profils standard 7.21

Familles de profils standard des bibliotheques TopSolid 7.21 (`.TopFam` : Plat, Tube rectangle, Tube carre, Tube rond, Rond, Carre — projets « Composants Mecanique TopSolid » et « Mecanique AFNOR TopSolid »). Un profil est place dans un assemblage, entre deux points 3D donnes en mm ; `list_profile_families_and_codes` enumere les familles et codes disponibles.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `list_profile_families_and_codes` | Liste les familles de profils standard de bibliotheque (`.TopFam`) et leurs codes de section. | `Pdm.GetProjects()` filtre bibliotheques + `Families.GetCodes()` — `value` = `[nom_famille[:filtre_code]]` | READ |
| `create_profile_by_two_points` | Place un profil standard de bibliotheque entre deux points 3D (mm) dans un assemblage. | `Assemblies.CreateProfileByTwoPoints()` — `value` = `[asm:]famille,code,x1,y1,z1,x2,y2,z2[,dir(DX\|DY\|DZ),rot_deg,offX_mm,offY_mm,inverted]` | WRITE-PDM |
| `batch_create_profiles_by_two_points` | Place plusieurs profils standard dans un assemblage en une seule transaction, puis sauvegarde. | `Assemblies.CreateProfileByTwoPoints()` en boucle — `value` = `[asm[@clear]:]famille,code,x1,...,z2[,...];famille,code,...` | WRITE-PDM |

### Familles

Une famille TopSolid est un document parametrique avec des configurations (codes). Les drivers sont les parametres qui pilotent les variantes du catalogue.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_family` | Verifie si le document est une famille. Indique si elle est explicite (instances documents) ou implicite (instances issues du generique, avec catalogue possible). | `Families.IsFamily()` + `IsExplicit()` | READ |
| `read_family_codes` | Liste les codes (variantes) de la famille. Chaque code correspond a une configuration du catalogue. | `Families.GetCodes()` | READ |
| `read_family_catalog` | Lit les lignes, colonnes et valeurs typees du catalogue de la famille active. | `GetCodes()` + `GetCatalogColumnParameters()` + `GetCatalogCell*Value()` | READ |
| `add_family_catalog_column` | Ajoute une colonne a partir du selecteur exact d'un parametre existant de la famille. | `Families.AddCatalogColumn()` | WRITE-PDM |
| `add_family_catalog_row` | Ajoute une ligne avec un code unique et ses cellules typees. | `AddCatalogNewRow()` + `SetCatalogCell*Value()` | WRITE-PDM |
| `set_family_catalog_cell` | Modifie une cellule identifiee par le code de ligne et le selecteur exact de colonne. | `GetCatalogRow()` + `SetCatalogCell*Value()` | WRITE-PDM |
| `check_family_drivers` | Verifie que les drivers (parametres pilotant le catalogue) ont une designation. Liste ceux sans description. | `Families.GetCatalogColumnParameters()` → `Elements.GetDescription()` | READ |
| `fix_family_drivers` | Genere automatiquement une designation pour les drivers sans description, en deduisant du nom du parametre (CamelCase → mots separes). | `Elements.SetDescription()` auto-genere — aucun `value` | WRITE-PDM |
| `batch_check_family_drivers` | Audit des drivers de TOUTES les familles du projet. Detecte les drivers sans designation sur chaque famille. | Iteration `IsFamily()` → `GetCatalogColumnParameters()` | READ |

#### Modifier un catalogue implicite

Les quatre recettes travaillent sur la famille active. Une famille **implicite** peut avoir un catalogue : chaque ligne a un code, chaque colonne correspond a un parametre de cette famille. `read_family_catalog` lit aussi les familles explicites ou derivees ; les trois recettes d'ecriture les refusent.

`value` contient un objet JSON. Les selecteurs de parametres/colonnes doivent correspondre exactement au champ `name` retourne par `read_family_catalog` dans `availableParameters` ou `columns` : nom interne non vide, sinon nom convivial exact avec son suffixe `<id>`. Exemples de valeurs a transmettre :

| Recette | `value` |
|---------|---------|
| `read_family_catalog` | vide ou `{}` |
| `add_family_catalog_column` | `{"parameter":"D <71>"}` |
| `add_family_catalog_row` | `{"code":"A","cells":{"D <71>":{"type":"real","value":0.1}}}` |
| `set_family_catalog_cell` | `{"code":"A","column":"D <71>","type":"real","value":0.2}` |

Commencer par la lecture pour reperer les colonnes et les parametres disponibles. `D <71>` et `L <70>` sont des exemples : leurs identifiants varient selon la famille ; recopier les selecteurs de la reponse actuelle. Les pilotes de famille sont decouverts via `Elements.GetElements`, car `Parameters.GetParameters` peut les omettre. L'ajout de colonne utilise un parametre deja present dans la famille ; il ne cree ni pilote ni publication. `cells` est facultatif a l'ajout d'une ligne. Une ligne existante se modifie cellule par cellule.

Types pris en charge : `real`, `integer`, `boolean`, `text`, `code`. Un reel est en **unites SI** : `0.1` represente 100 mm pour une longueur, et un angle s'exprime en radians. Utiliser des nombres JSON avec un point decimal, des booleens `true`/`false` et des chaines JSON pour le texte. La lecture fournit le type et les informations d'unite disponibles ; les types non pris en charge sont signales.

Le champ JSON `code` identifie une ligne du catalogue. Il est distinct de la propriete PDM **Code** du document et des cellules de type `code` pouvant referencer une autre famille. La colonne d'identite est la premiere colonne du catalogue (`columns[0]`), de type texte dans le cas natif qualifie ; son ecriture utilise `SetCatalogCellTextValue`. Elle ne se deduit ni du nom `Code`, ni du type `ParameterType.Code`, qui peut decrire une autre cellule. `set_family_catalog_cell` refuse de modifier cette colonne : le renommage des codes n'est pas pris en charge.

Les codes dupliques, colonnes absentes et valeurs de type incompatible sont refuses. Les ecritures utilisent une transaction TopSolid ; elles sont indisponibles en mode lecture seule. Ces recettes ne suppriment pas de ligne ou de colonne et ne convertissent pas les familles explicites/derivees. Les cellules famille, couleur et enumeration ne sont pas modifiables par ces recettes.

### Export

Export du document actif vers differents formats CAO/dessin. Le chemin est optionnel — si omis, TopSolid utilise le dossier par defaut (temp ou bureau). L'exporteur est selectionne automatiquement par nom.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `export_file` **(v1.8.16)** | Export vers n'importe quel format, choisi par l'extension du chemin (`.step`, `.x_t`, `.igs`, `.stl`, `.dxf`, `.pdf`...). Verifie `CanExport` et **refuse** un export 3D d'un document sans forme, au lieu de laisser TopSolid se bloquer sur sa boite « aucune entite a exporter ». Verifie que le fichier ecrit n'est pas vide. | `ExportWithOptions()` en mode silencieux — `value` = chemin, `chemin\|force` pour passer outre le controle de forme | WRITE-DISK |
| `import_file` **(v1.8.16)** | Importe un fichier CAO (`.step`, `.igs`, `.x_t`, `.sldprt`, `.sldasm`...) dans le projet courant avec l'importeur de son extension, en mode silencieux, sauve les documents crees et les liste avec leur nombre de formes. Un fichier par appel. | `ImportWithOptions()` hors transaction — `value` = chemin, ou `chemin\|Dossier/Sous-dossier` (noms exacts, crees s'ils manquent) | WRITE-PDM-NO-TRX |
| `export_step` | Export STEP (AP214). Format universel d'echange CAO. Refuse un document sans forme depuis v1.8.16. | `Documents.Export()` avec exporteur STEP — `value` = chemin (optionnel), `chemin\|force` pour passer outre | WRITE-DISK |
| `export_dxf` | Export DXF (AutoCAD). Pour les mises en plan ou profils 2D. | `Documents.Export()` avec exporteur DXF — `value` = chemin (optionnel) | WRITE-DISK |
| `export_pdf` | Export PDF. Pour les mises en plan principalement. | `Documents.Export()` avec exporteur PDF — `value` = chemin (optionnel) | WRITE-DISK |
| `export_stl` | Export STL (maillage triangles). Pour l'impression 3D ou la visualisation. Refuse un document sans forme depuis v1.8.16. | `Documents.Export()` avec exporteur STL — `value` = chemin (optionnel), `chemin\|force` pour passer outre | WRITE-DISK |
| `export_iges` | Export IGES. Format historique d'echange CAO, encore utilise dans l'aeronautique. Refuse un document sans forme depuis v1.8.16. | `Documents.Export()` avec exporteur IGES — `value` = chemin (optionnel), `chemin\|force` pour passer outre | WRITE-DISK |
| `list_exporters` | Liste tous les formats d'export disponibles dans l'installation TopSolid. | `Documents.GetExporterNames()` | READ |
| `export_bom_csv` | Exporte la nomenclature (BOM) du document en format texte avec colonnes separees. | Lecture `Nomenclatures` → format texte | READ |
| `export_project_xliff` **(v1.8.13)** | Exporte le dictionnaire de traduction (`.xliff`) de tout un projet PDM recursivement (dossiers, documents, parametres, proprietes). | `Pdm.ExportLocalizations(projects, cultures, dir, ".xliff", false)` — `value=[projet;]culture;dossier` | WRITE-DISK |
| `import_project_xliff` **(v1.8.13)** | Importe un fichier ou dossier `.xliff` traduit dans un projet PDM sans modifier les noms invariants. | `Pdm.ImportDictionnaries()` / `Pdm.ImportLocalizations()` — `value=[projet;]chemin_xliff` | WRITE-PDM |

### Audit et verification

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
| `check_collisions` | Active l'analyse native de collisions 3D B-Rep de TopSolid sur un assemblage, rapporte les paires d'elements en collision, puis retablit le stage Modelisation. Verification ADN : attendre `0 collision` avant de conclure. | `Assemblies.SetCollisionsManagement()` + analyse `ShapeShapeCollisionEntity` — `value` = `[nom_asm]` (assemblage actif par defaut) | WRITE-PDM |

### Mise en plan

La mise en plan (Drafting) est le dessin technique 2D genere a partir d'une piece ou assemblage 3D. Elle contient des vues, une echelle, un format papier.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_drafting` | Verifie si le document est une mise en plan. Retourne le nombre de vues et le format. | `Draftings.IsDrafting()` | READ |
| `open_drafting` | Cherche la mise en plan associee a la piece courante (via back-references PDM) et l'ouvre. | `Pdm.SearchMajorRevisionBackReferences()` filtre `.TopDft` → `Documents.Open()` | READ |
| `list_drafting_views` | Liste les vues du plan avec nom et titre. | `Draftings.GetDraftingViews()` | READ |
| `read_drafting_scale` | Lit l'echelle globale du plan et l'echelle de chaque vue individuellement. | `Draftings.GetScaleFactorParameterValue()` + `GetViewScaleFactor()` | READ |
| `read_drafting_format` | Format papier (A3/A4), dimensions en mm, nombre de pages, mode de projection. | `Draftings.GetDraftingFormatName/Dimensions/PageCount` | READ |
| `read_main_projection` | Identifie la piece source de la mise en plan et les vues principales. | `Draftings.GetMainProjectionSet()` | READ |
| `set_drafting_scale` **(v1.6.1)** | Change l'echelle globale du plan. `value=denominateur` (ex: `10` → 1:10). | `Draftings.SetScaleFactorParameterValue()` — Pattern D | WRITE-PDM |
| `set_drafting_format` **(v1.6.1)** | Change le format papier. `value=nom` (ex: `A3`, `A4`). | `Draftings.SetDraftingFormatName()` — Pattern D | WRITE-PDM |
| `set_projection_quality` **(v1.6.1)** | Qualite de projection. `value=exact` (precis) ou `fast` (rapide). | `Draftings.SetProjectionMode()` — Pattern D | WRITE-PDM |
| `print_drafting` **(v1.6.1)** | Imprime toutes les pages du plan (N&B, 300 DPI, a l'echelle). | `Draftings.Print()` | WRITE-DISK |

### Mise en plan — bundle

Un bundle de mise en plan est un document multi-pages (liasse) qui regroupe plusieurs mises en plan : plusieurs plans sur une meme page, ou un plan par page, chaque page pouvant avoir son propre format. Ces recettes pilotent le bundle ouvert dans l'editeur.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `get_page_count` | Retourne le nombre de pages du bundle de mise en plan ouvert. | `Draftings.GetPageCount()` | READ |
| `add_page_to_bundle` | Ajoute une nouvelle page au bundle ouvert. Aucun `value` attendu. | `Draftings.AddPageToBundle()` | WRITE-PDM |
| `set_current_bundle_page` | Definit la page courante du bundle ouvert. | `Draftings.SetCurrentBundlePage()` — `value` = numero de page (ex: `1`) | WRITE-PDM |
| `set_bundle_page_format_name` | Definit le nom de format d'une page du bundle (ex: `A3`). | `Draftings.SetBundlePageFormatName()` — `value` = numero de page | WRITE-PDM |
| `set_bundle_page_format` | Definit le format d'une page du bundle. | `Draftings.SetBundlePageFormat()` — `value` = `page;format` (ex: `1;A3 landscape`) | WRITE-PDM |
| `set_bundle_page_dimensions` | Definit les dimensions d'une page du bundle en mm. | `Draftings.SetBundlePageDimensions()` — `value` = `page;largeur;hauteur` (ex: `1;420;297`) | WRITE-PDM |
| `include_draft_in_bundle` | Inclut une mise en plan dans le bundle ouvert — toutes ses pages sur la meme page du bundle. | `Draftings.IncludeDraftInBundle()` — `value` = nom du document plan | WRITE-PDM |
| `include_draft_page_in_bundle_current_page` | Inclut une page specifique d'une mise en plan dans la page courante du bundle, avec decalage. | `Draftings.IncludeDraftPageInBundleCurrentPage()` — `value` = `plan;page;decalage` (ex: `draft;1;0`) | WRITE-PDM |
| `add_draft_to_bundle` | Ajoute une mise en plan au bundle ouvert — ses pages deviennent des pages separees du bundle. | `Draftings.AddDraftToBundle()` — `value` = nom du document plan | WRITE-PDM |

### Nomenclature / BOM

La nomenclature (Bill of Materials) est le tableau des composants d'un assemblage. Elle est generalement dans la mise en plan.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_bom` | Verifie si le document contient une nomenclature. Retourne le nombre de colonnes. | `Boms.IsBom()` | READ |
| `read_bom_columns` | Liste les colonnes du tableau (Repere, Designation, Quantite, Reference...). | `Boms.GetColumnCount()` + `GetColumnTitle()` | READ |
| `read_bom_contents` | Lit le tableau complet : toutes les lignes actives avec toutes les cellules. Format texte tabulaire. | `Boms.GetRowChildrenRows()` + `GetRowContents()` | READ |
| `count_bom_rows` | Compte les lignes actives et inactives de la nomenclature. | `Boms.IsRowActive()` sur chaque ligne | READ |
| `activate_bom_row` **(v1.6.1)** | Active une ligne BOM par index. `value=row_index`. | `Boms.ActivateRow()` — Pattern D | WRITE-PDM |
| `deactivate_bom_row` **(v1.6.1)** | Desactive une ligne BOM par index. `value=row_index`. | `Boms.DeactivateRow()` — Pattern D | WRITE-PDM |

### Mise a plat / Depliage

Le depliage (flat pattern) concerne la tolerie : une piece en tole est "depliee" pour obtenir le contour plat a decouper. Les plis ont un angle, un rayon et une longueur.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `detect_unfolding` | Verifie si le document contient un depliage tolerie. Liste les plis detectes. | Detection via operations de type FlatPattern | READ |
| `read_bend_features` | Liste les plis avec angle (degres), rayon interieur (mm) et longueur (mm). | Proprietes des operations de pliage | READ |
| `read_unfolding_dimensions` | Dimensions du contour deplie : longueur et largeur du flan plat en mm. | Parametres systeme de depliage | READ |

### Document

Creation de documents (piece, assemblage ou depuis un modele nomme), puis operations sur le document actif : sauvegarde, reconstruction, commande menu.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `create_part_document` | Cree (ou reutilise) un document Piece (`.TopPrt`) dans un projet et un dossier, renseigne nom, designation et reference, puis l'ouvre. | `Pdm.CreateDocument(owner, ".TopPrt")` + `Pdm.SetName/SetDescription/SetPartNumber()` + `Documents.Open()` — `value` = `nom[;designation[;reference[;dossier[;projet]]]]` | WRITE-PDM |
| `create_assembly_document` | Cree (ou reutilise) un document Assemblage (`.TopAsm`) dans un projet et un dossier, renseigne nom, designation et reference, puis l'ouvre. | `Pdm.CreateDocument(owner, ".TopAsm")` + `Documents.Open()` — `value` = `nom[;designation[;reference[;dossier[;projet]]]]` | WRITE-PDM |
| `create_document_with_template` | Cree un document dans le projet courant depuis un modele de document nomme (ex: `Wooden Part`), cherche dans les modeles utilisateur puis entreprise. Tourne hors transaction : TopSolid refuse une creation de document entre `StartModification` et `EndModification`. | `Pdm.GetTemplates()` + `Pdm.SearchDocumentByName()` + `Pdm.CreateDocumentWithTemplate()` — `value` = nom du modele | WRITE-PDM-NO-TRX |
| `document_type` | Retourne le type du document actif : piece, assemblage, mise en plan, etc. Detecte aussi famille et mise a plat. | `IsAssembly()`, `IsDraftingDocument()`, `IsFamily()`, `GetType()` | READ |
| `save_document` | Sauvegarde le document actif. | `Pdm.Save(pdmId, true)` | WRITE-PDM |
| `rebuild_document` | Force la reconstruction du modele (recalcul de toutes les operations). Utile apres modification de parametres. | `Documents.Rebuild(docId)` | WRITE-PDM |
| `save_all_project` | Sauvegarde tous les documents du projet en une passe. | `Pdm.Save()` en boucle sur tous les docs | WRITE-PDM |
| `invoke_command` | Execute une commande menu TopSolid par son nom interne. Attention : certaines commandes ouvrent des dialogues. | `Application.InvokeCommand(value)` — `value` = nom de la commande | WRITE-PDM |

### Proprietes utilisateur et document

Les proprietes utilisateur sont definies par l'entreprise (dossier PDM dedie) et materialisees dans chaque document comme des parametres speciaux. La resolution par `Pdm.GetTextUserProperty(pdmId, "name_string")` ne fonctionne pas — l'API attend une `PdmObjectId` pointant sur la definition. Le pattern fiable est d'iterer `Parameters.GetParameters()` et filtrer ceux avec une `UserPropertyDefinition` non-vide.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `read_user_property` **(v1.6.2 fixed)** | Lit une propriete utilisateur par son nom visible (text/real/integer/boolean, auto-type). | `Parameters.GetUserPropertyDefinition()` + match par `GetFriendlyName()` | READ |
| `set_user_property` **(v1.6.2 fixed)** | Modifie une propriete utilisateur par son nom visible, parse la valeur selon le type. `value=nom:valeur`. | `Parameters.SetTextValue/SetRealValue/...` apres lookup UserPropertyDefinition | WRITE-PDM |

### Comparaison de documents

Compare le document courant avec un autre document du meme projet. Utile pour verifier des differences entre revisions ou variantes.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `compare_document_operations` | Compare l'arbre de construction entre 2 documents. Montre les operations ajoutees, supprimees ou differentes. | `Operations.GetOperations()` diff par nom — `value` = nom de l'autre document | READ |
| `compare_document_entities` | Compare les entites (shapes, esquisses, points, reperes) entre 2 documents. Montre les differences de quantite. | Comptage `Shapes/Sketches/Points/Frames` — `value` = nom de l'autre document | READ |
| `compare_revisions` | Compare les parametres de la revision courante avec la revision precedente du meme document. | `Pdm.GetMajorRevisions()` → diff parametres | READ |

### Report de modifications

Copie des proprietes d'un document vers un autre. Utile pour propager des modifications en serie.

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `copy_parameters_to` | Copie les valeurs de parametres du document actif vers un autre. Match par nom de parametre. Ignore les parametres systeme ($Mass, etc.). | `Parameters.Set{Real,Text,Integer,Boolean}Value()` — `value` = nom du document cible | WRITE-PDM |
| `copy_pdm_properties_to` | Copie designation, reference et fabricant du document actif vers un autre. | `Pdm.Set{Description,PartNumber,Manufacturer}()` — `value` = nom du document cible | WRITE-PDM |

### Document virtuel

| Recette | Description | Technique | Mode |
|---------|-------------|-----------|------|
| `enable_virtual_document` | Active le mode virtuel sur le document courant. Un document virtuel n'est pas sauvegarde physiquement. | `Documents.SetVirtualDocumentMode(docId, true)` | WRITE-PDM |
| `batch_enable_virtual` | Active le mode virtuel sur tous les documents non-virtuels du projet. | `SetVirtualDocumentMode(true)` en boucle | WRITE-PDM |

## Pattern de modification

Toutes les recettes WRITE-PDM suivent le meme pattern :

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

59/61 tests PASS sur TopSolid vivant (assemblage REF-MCPTEST).

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

2164 entrees ShareGPT pour fine-tuner le sous-agent 3B (`ministral-topsolid` v7 conversational). Couvre les 216 recettes + patterns multi-turn + error-handling + acknowledgments. Le dataset n'est **pas** versionne dans le depot : il se regenere depuis `RecipeTool.cs` avec `scripts/generate-lora-dataset.py` (ou `make lora-dataset`).

Eval : **96%** sur 50 questions (5 tiers, trivial → piege), multi-turn verifie manuellement.

## Pattern d'ecriture dans modify_script

Les recettes WRITE passent par `topsolid_modify_script` qui wrappe automatiquement `StartModification` / `EnsureIsDirty` / `EndModification` / `Pdm.Save`. Contraintes :

- **NE PAS utiliser `return "..."`** — le wrapper l'interdit.
- Utiliser `__message = "..."` pour personnaliser le message de retour.
- `return;` (void) est autorise pour les early exits — transforme en `goto __done;`.
- Variables pre-declarees : `docId`, `pdmId`, `__message`.
- Une recette dont les seules ecritures sont des appels PDM (`Pdm.SetDescription`, `SetName`, `SetPartNumber`, `Save`...) tourne sans `StartModification` : TopSolid refuse ces appels dans une transaction. C'est le cas de `set_designation`, `set_name`, `set_reference`, `set_manufacturer`, `set_user_property`, `copy_pdm_properties_to`, `clear_document_author`, `save_all_project` et des `batch_set_*` / `batch_clear_author`, qui echouaient avant la v1.8.16. Melanger ces appels et des modifications du modele est refuse avant execution.

Exemple (extrait de `set_drafting_scale`) :
```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
double factor = 1.0 / denom;
TopSolidDraftingHost.Draftings.SetScaleFactorParameterValue(docId, factor);
__message = "OK: drafting scale set to 1:" + denom;
```
