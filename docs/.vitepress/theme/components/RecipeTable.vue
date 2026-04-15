<script setup>
import { ref, computed } from 'vue'

const recipes = [
  // PDM
  { name: "lire_designation", description: "Lit la designation du document actif, c'est-a-dire la description fonctionnelle de la piece ou de l'assemblage (ex: \"Bride de serrage\", \"Support moteur\"). Retourne \"(vide)\" si le champ n'est pas renseigne dans les proprietes PDM.", mode: "READ", category: "PDM", api: "Pdm.GetDescription(pdmId)" },
  { name: "lire_nom", description: "Lit le nom unique du document dans l'arborescence projet TopSolid (ex: \"BRD-001\", \"Plaque_base\"). C'est l'identifiant visible dans l'explorateur de projet, distinct de la designation.", mode: "READ", category: "PDM", api: "Pdm.GetName(pdmId)" },
  { name: "lire_reference", description: "Lit la reference du document, aussi appelee part number ou numero de piece. C'est le code utilise pour le suivi en production et l'approvisionnement (ex: \"REF-2024-0042\"). Retourne \"(vide)\" si non renseignee.", mode: "READ", category: "PDM", api: "Pdm.GetPartNumber(pdmId)" },
  { name: "lire_fabricant", description: "Lit le fabricant ou fournisseur associe au document. Utile pour les pieces achetees ou sous-traitees. Retourne \"(vide)\" si non renseigne.", mode: "READ", category: "PDM", api: "Pdm.GetManufacturer(pdmId)" },
  { name: "lire_proprietes_pdm", description: "Retourne les 4 proprietes PDM principales d'un seul appel : nom, designation, reference et fabricant. Pratique pour un apercu rapide du document sans faire 4 appels separes.", mode: "READ", category: "PDM", api: "Pdm.Get{Name,Description,PartNumber,Manufacturer}" },
  { name: "modifier_designation", description: "Change la designation (description fonctionnelle) du document actif. La sauvegarde PDM est automatique apres modification. Aucune transaction de modelisation necessaire.", mode: "WRITE", category: "PDM", api: "Pdm.SetDescription — value = texte libre" },
  { name: "modifier_nom", description: "Renomme le document dans l'arborescence projet. Attention : si d'autres documents referencent ce nom (inclusions, mises en plan), les liens peuvent etre impactes.", mode: "WRITE", category: "PDM", api: "Pdm.SetName — value = nouveau nom" },
  { name: "modifier_reference", description: "Change la reference / part number du document. Sauvegarde PDM automatique. Utile pour affecter ou corriger un code article en serie.", mode: "WRITE", category: "PDM", api: "Pdm.SetPartNumber — value = nouvelle reference" },
  { name: "modifier_fabricant", description: "Change le fabricant ou fournisseur associe au document. Sauvegarde PDM automatique.", mode: "WRITE", category: "PDM", api: "Pdm.SetManufacturer — value = nom fabricant" },
  // Navigation
  { name: "lire_projet_courant", description: "Retourne le nom du projet actif dans TopSolid. C'est la premiere verification a faire pour s'assurer qu'un projet est bien ouvert avant toute operation.", mode: "READ", category: "Navigation", api: "Pdm.GetCurrentProject → GetName" },
  { name: "lire_contenu_projet", description: "Parcourt recursivement toute l'arborescence du projet : dossiers, sous-dossiers et documents a chaque niveau. Affiche l'indentation par profondeur et le total de dossiers et documents trouves.", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents (recursif)" },
  { name: "chercher_document", description: "Recherche un document par nom dans le projet (match partiel, type CONTAINS insensible a la casse). Retourne nom, designation et reference de chaque resultat. Utile quand on connait un bout du nom.", mode: "READ", category: "Navigation", api: "Pdm.SearchDocumentByName — value = texte a chercher" },
  { name: "chercher_dossier", description: "Recherche un dossier par nom dans le projet (match partiel). Retourne le chemin et le contenu du dossier trouve.", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents + filtre — value = texte" },
  { name: "ouvrir_document_par_nom", description: "Cherche un document par nom dans le projet puis l'ouvre dans l'editeur TopSolid. Le document devient le document actif pour toutes les operations suivantes. Indispensable avant de lire parametres, shapes, etc.", mode: "WRITE", category: "Navigation", api: "Documents.Open(ref docId) — value = nom document" },
  { name: "lister_documents_projet", description: "Liste TOUS les documents du projet avec pour chacun : nom, designation et reference. Format tableau complet. Utile pour avoir une vue d'ensemble du contenu du projet.", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents iteration complete" },
  { name: "lister_documents_dossier", description: "Liste les documents contenus dans un dossier specifique du projet. Utile pour explorer un sous-ensemble sans tout lister.", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents(folderId) — value = nom du dossier" },
  { name: "resumer_projet", description: "Resume synthetique du projet : nombre de documents par type (.TopPrt, .TopAsm, .TopDft...), nombre de dossiers, structure globale. Vue macro avant de plonger dans le detail.", mode: "READ", category: "Navigation", api: "Comptage par Pdm.GetType" },
  { name: "compter_documents_par_type", description: "Compte les documents du projet groupes par type d'extension : .TopPrt (pieces), .TopAsm (assemblages), .TopDft (mises en plan), etc. Retourne un tableau type → quantite.", mode: "READ", category: "Navigation", api: "Pdm.GetType sur chaque doc" },
  { name: "chercher_pieces_par_materiau", description: "Liste toutes les pieces du projet avec leur materiau et leur masse. Filtre optionnel sur le nom du materiau (ex: \"Acier\"). Utile pour verifier les affectations materiaux ou calculer la masse totale par materiau.", mode: "READ", category: "Navigation", api: "Parameters $Mass + materiau — value = filtre optionnel" },
  { name: "lire_cas_emploi", description: "Where-used : recherche tous les documents du projet qui referencent le document courant. Trouve les assemblages parents, les mises en plan associees. Indispensable avant de modifier une piece partagee.", mode: "READ", category: "Navigation", api: "Pdm.SearchMajorRevisionBackReferences" },
  { name: "lire_historique_revisions", description: "Affiche la timeline complete des revisions du document : revisions majeures et mineures avec auteur, date et commentaire. Permet de tracer l'evolution du document dans le temps.", mode: "READ", category: "Navigation", api: "Pdm.GetMajorRevisions + GetMinorRevisions" },
  { name: "chercher_documents_modifies", description: "Liste les documents du projet qui ont ete modifies mais pas encore sauvegardes (flag dirty). Utile avant un export batch ou une fermeture de projet pour eviter de perdre des modifications.", mode: "READ", category: "Navigation", api: "Documents.IsDirty en boucle" },
  // Parametres
  { name: "lire_parametres", description: "Liste tous les parametres du document actif avec pour chacun : nom, valeur et type (Real, Integer, Boolean, Text). Les valeurs reelles sont en SI (metres, radians). C'est la vue complete de l'arbre parametrique.", mode: "READ", category: "Parametres", api: "Parameters.GetParameters + GetParameterType + Get*Value" },
  { name: "lire_parametre_reel", description: "Lit la valeur d'un parametre reel par son nom (match partiel insensible a la casse). La valeur retournee est en SI : 0.050000 signifie 50 mm, 0.785398 signifie 45 degres.", mode: "READ", category: "Parametres", api: "Parameters.GetRealValue — value = nom du parametre" },
  { name: "lire_parametre_texte", description: "Lit la valeur d'un parametre texte par son nom (match partiel). Les parametres texte servent souvent pour les references, noms de materiaux ou commentaires embarques dans le modele.", mode: "READ", category: "Parametres", api: "Parameters.GetTextValue — value = nom du parametre" },
  { name: "modifier_parametre_reel", description: "Modifie la valeur d'un parametre reel. La valeur DOIT etre en unites SI : les longueurs en metres (50 mm = 0.05), les angles en radians (45° = 0.785398). TopSolid recalcule automatiquement le modele apres modification.", mode: "WRITE", category: "Parametres", api: "Parameters.SetRealValue — value = nom:valeurSI (ex: Longueur:0.15)" },
  { name: "modifier_parametre_texte", description: "Modifie la valeur d'un parametre texte. Le format est nom:valeur, separes par deux-points. Le match du nom est partiel et insensible a la casse.", mode: "WRITE", category: "Parametres", api: "Parameters.SetTextValue — value = nom:valeur" },
  { name: "comparer_parametres", description: "Compare les parametres du document actif avec ceux d'un autre document du projet. Affiche les differences de valeur (A vs B), les parametres presents uniquement dans l'un ou l'autre. Ideal pour comparer deux variantes d'une piece.", mode: "READ", category: "Comparaison", api: "Diff par nom de parametre — value = nom autre document" },
  // Physique
  { name: "lire_masse_volume", description: "Retourne la masse (kg), le volume (affiche en mm³) et la surface (affiche en mm²) du document actif depuis les proprietes systeme TopSolid. Necessite qu'un materiau soit affecte pour que la masse soit calculee.", mode: "READ", category: "Physique", api: "Parameters systeme $Mass, $Volume, $Surface Area" },
  { name: "rapport_masse_assemblage", description: "Rapport de masse pour un assemblage complet : masse totale (kg), volume total, surface totale, et decompte des pieces. Donne une vue globale du poids de l'assemblage. Necessite un materiau sur chaque piece.", mode: "READ", category: "Physique", api: "GetParts + proprietes systeme agreges" },
  { name: "lire_densite_materiau", description: "Calcule la densite du materiau (kg/m³) a partir du rapport masse/volume des proprietes systeme. Utile pour verifier que le bon materiau est affecte (acier ~7800, alu ~2700, plastique ~1200).", mode: "READ", category: "Physique", api: "masse / volume depuis params systeme" },
  { name: "lire_materiau", description: "Retourne le nom du materiau affecte a la piece et sa densite calculee. Si aucun materiau n'est affecte, la masse sera nulle.", mode: "READ", category: "Physique", api: "Parameters systeme $Mass + $Volume" },
  { name: "lire_dimensions_piece", description: "Lit les dimensions principales de la piece depuis les proprietes systeme : Height (hauteur), Width (largeur), Length (longueur) et Box Size en mm. Ce sont les dimensions de la boite englobante orientee.", mode: "READ", category: "Physique", api: "Parameters systeme $Height, $Width, $Length, $Box Size" },
  { name: "lire_boite_englobante", description: "Retourne la boite englobante du modele : coordonnees min/max en X, Y, Z (en mm) plus les dimensions calculees Longueur × largeur × hauteur.", mode: "READ", category: "Physique", api: "Parameters systeme bounding box" },
  { name: "lire_moments_inertie", description: "Lit les moments principaux d'inertie en X, Y et Z depuis les proprietes systeme. Utilise en calcul de resistance des materiaux et dimensionnement mecanique.", mode: "READ", category: "Physique", api: "Parameters systeme $Principal Moment of Inertia X/Y/Z" },
  // Geometrie
  { name: "lire_points_3d", description: "Liste tous les points 3D de construction du document avec leurs coordonnees X, Y, Z converties en mm. Les points 3D servent de references geometriques pour les contraintes et les assemblages.", mode: "READ", category: "Geometrie", api: "Geometries3D.GetPoints → GetPointGeometry (×1000)" },
  { name: "lire_reperes_3d", description: "Liste les reperes 3D (frames/systemes de coordonnees) du document par nom. Les reperes definissent des systemes de reference locaux utilises pour le positionnement dans les assemblages.", mode: "READ", category: "Geometrie", api: "Geometries3D.GetFrames" },
  { name: "lister_esquisses", description: "Liste les esquisses 2D (sketches) du document par nom. Les esquisses sont les profils de base utilises par les operations d'extrusion, revolution, balayage, etc.", mode: "READ", category: "Geometrie", api: "Sketches2D.GetSketches" },
  { name: "lire_shapes", description: "Liste les shapes (corps solides) du document avec le nombre de faces de chaque shape. Un document peut contenir plusieurs shapes (multi-corps). Les faces sont les surfaces individuelles du solide.", mode: "READ", category: "Geometrie", api: "Shapes.GetShapes → GetFaceCount" },
  { name: "lire_operations", description: "Affiche l'arbre de construction complet : liste de toutes les operations (features) avec nom et type (Extrusion, Revolution, Pocket, Fillet, Chamfer, Pattern, etc.). C'est l'historique de modelisation de la piece.", mode: "READ", category: "Geometrie", api: "Operations.GetOperations → Elements.GetTypeFullName" },
  // Attributs
  { name: "attribut_lire_tout", description: "Vue complete des attributs visuels de tous les elements : couleur RGB, transparence (0-1), calque (layer) affecte, et visibilite. Donne un apercu global de l'apparence de la piece.", mode: "READ", category: "Attributs", api: "Elements.GetColor/Transparency + Layers.GetLayer" },
  { name: "attribut_lire_couleur", description: "Lit la couleur RGB de chaque shape du document. Retourne \"pas de couleur\" si la couleur est heritee du materiau (comportement par defaut TopSolid).", mode: "READ", category: "Attributs", api: "Elements.HasColor → GetColor" },
  { name: "attribut_lire_couleurs_faces", description: "Lit les couleurs face par face de chaque shape. Utile quand un shape a des faces colorees individuellement (peinture selective, zones fonctionnelles).", mode: "READ", category: "Attributs", api: "Shapes.GetFaces → Elements.GetColor par face" },
  { name: "attribut_modifier_couleur", description: "Change la couleur d'un shape. Si le document contient un seul shape, la couleur est appliquee directement. Si plusieurs shapes, TopSolid ouvre une boite de selection pour que l'utilisateur pointe le shape voulu.", mode: "WRITE", category: "Attributs", api: "Elements.SetColor — value = R,G,B (ex: 255,0,0 pour rouge)" },
  { name: "attribut_modifier_couleur_tout", description: "Change la couleur de TOUS les elements du document d'un coup, sans selection. Applique la meme couleur RGB a chaque shape dont la couleur est modifiable.", mode: "WRITE", category: "Attributs", api: "Elements.SetColor en boucle — value = R,G,B" },
  { name: "attribut_remplacer_couleur", description: "Recherche et remplace : trouve tous les elements ayant une couleur source et les passe a une couleur cible. Ex: remplacer tout le vert (0,128,0) par du rouge (255,0,0) en un seul appel.", mode: "WRITE", category: "Attributs", api: "Filtre couleur → SetColor — value = R1,G1,B1:R2,G2,B2" },
  { name: "attribut_lire_transparence", description: "Lit le niveau de transparence de chaque shape : 0.0 = totalement opaque, 1.0 = totalement invisible. Les shapes sans transparence explicite sont opaques par defaut.", mode: "READ", category: "Attributs", api: "Elements.HasTransparency → GetTransparency" },
  { name: "attribut_modifier_transparence", description: "Change la transparence d'un shape. Si plusieurs shapes, TopSolid demande une selection interactive. Valeur entre 0.0 (opaque) et 1.0 (invisible). Pratique pour voir l'interieur d'un assemblage.", mode: "WRITE", category: "Attributs", api: "Elements.SetTransparency — value = 0.0 a 1.0" },
  { name: "attribut_lister_calques", description: "Liste les calques (layers) definis dans le document, par nom. Les calques TopSolid permettent d'organiser et de masquer/afficher des groupes d'elements.", mode: "READ", category: "Attributs", api: "Layers.GetLayers" },
  { name: "attribut_affecter_calque", description: "Deplace un element (shape, esquisse...) vers un calque existant. Match par nom partiel sur l'element et le calque.", mode: "WRITE", category: "Attributs", api: "Layers.SetLayer — value = nom_element:nom_calque" },
  // Selection
  { name: "selectionner_shape", description: "Ouvre une boite de dialogue TopSolid demandant a l'utilisateur de cliquer un corps solide (shape) a l'ecran. Retourne le nom du shape et son nombre de faces. Utile quand l'IA ne peut pas determiner automatiquement quel element traiter.", mode: "ASK", category: "Selection", api: "User.AskShape" },
  { name: "selectionner_face", description: "Demande a l'utilisateur de cliquer une face dans la vue 3D. Retourne le shape parent et l'index de la face selectionnee. Utilise pour les operations face-specifiques (couleur, usinage).", mode: "ASK", category: "Selection", api: "User.AskFace" },
  { name: "selectionner_point_3d", description: "Demande a l'utilisateur de cliquer un point dans l'espace 3D de TopSolid. Retourne les coordonnees X, Y, Z en mm. Utile pour definir une position, un point de reference ou mesurer une distance.", mode: "ASK", category: "Selection", api: "User.AskPoint3D" },
  // Assemblage
  { name: "detecter_assemblage", description: "Verifie si le document courant est un assemblage TopSolid. Si oui, liste toutes les pieces (parts) contenues. Premiere etape avant d'explorer la structure d'un assemblage.", mode: "READ", category: "Assemblage", api: "Assemblies.IsAssembly → GetParts" },
  { name: "lister_inclusions", description: "Liste les inclusions de l'assemblage : chaque inclusion est une instance d'une piece (document de definition). Montre la relation inclusion → piece source. Permet de comprendre la structure de l'assemblage.", mode: "READ", category: "Assemblage", api: "IsInclusion → GetInclusionDefinitionDocument" },
  { name: "lire_occurrences", description: "Liste les occurrences visibles de l'assemblage avec leur document de definition. Les occurrences sont les representations dans l'arbre, les inclusions sont les operations sous-jacentes.", mode: "READ", category: "Assemblage", api: "Assemblies.GetOccurrences → GetOccurrenceDefinitionDocument" },
  { name: "compter_pieces_assemblage", description: "Compte les pieces groupees par type (reference unique) avec quantites. Ex: \"Vis M8×30 : 4, Plaque support : 2, Equerre : 1 → Total 7 pieces\". Vision nomenclature simplifiee.", mode: "READ", category: "Assemblage", api: "GetParts groupe par GetInclusionDefinitionDocument" },
  { name: "renommer_occurrence", description: "Renomme une occurrence dans l'assemblage. Le match sur l'ancien nom est partiel et insensible a la casse. Utile pour donner des noms significatifs aux instances (\"Vis_gauche\", \"Plaque_dessus\").", mode: "WRITE", category: "Assemblage", api: "Entities.SetFunctionOccurrenceName — value = ancien:nouveau" },
  // Famille
  { name: "detecter_famille", description: "Verifie si le document est une famille TopSolid. Indique si elle est explicite (catalogue avec codes predetermine) ou implicite (parametrique, les variantes se creent a la volee). Les familles sont le mecanisme de bibliotheque standard de TopSolid.", mode: "READ", category: "Famille", api: "Families.IsFamily + IsExplicit" },
  { name: "lire_codes_famille", description: "Liste les codes (variantes) d'une famille explicite. Chaque code correspond a une configuration du catalogue. Ex: pour une famille de vis, les codes sont M6×20, M8×30, M10×40, etc.", mode: "READ", category: "Famille", api: "Families.GetCodes" },
  { name: "verifier_drivers_famille", description: "Verifie que les parametres drivers de la famille (ceux qui pilotent le catalogue) ont une designation renseignee. Les drivers sans designation rendent le catalogue moins lisible. Liste ceux a corriger.", mode: "READ", category: "Famille", api: "Families.GetCatalogColumnParameters → GetDescription" },
  { name: "corriger_drivers_famille", description: "Genere automatiquement une designation pour les drivers qui n'en ont pas, en deduisant du nom du parametre : decoupe le CamelCase en mots, remplace les underscores par des espaces, met une majuscule.", mode: "WRITE", category: "Famille", api: "Elements.SetDescription auto-genere depuis nom" },
  { name: "verifier_drivers_famille_batch", description: "Audit les drivers de TOUTES les familles du projet d'un coup. Detecte les drivers sans designation sur chaque famille. Rapport global pour corriger en serie.", mode: "READ", category: "Famille", api: "IsFamily → GetCatalogColumnParameters en boucle" },
  // Export
  { name: "exporter_step", description: "Exporte le document actif en format STEP (AP214), le format universel d'echange entre logiciels CAO (Catia, SolidWorks, NX...). Retourne le chemin du fichier cree. Le chemin est optionnel (defaut = dossier temp).", mode: "READ", category: "Export", api: "Documents.Export STEP — value = chemin (optionnel)" },
  { name: "exporter_dxf", description: "Exporte en DXF (format AutoCAD). Principalement utilise pour les mises en plan 2D, les profils de decoupe laser ou les plans d'atelier. Compatible avec tous les logiciels 2D.", mode: "READ", category: "Export", api: "Documents.Export DXF — value = chemin (optionnel)" },
  { name: "exporter_pdf", description: "Exporte en PDF. Utilise principalement pour les mises en plan (plans de fabrication, plans d'ensemble). Genere un document lisible sans logiciel CAO.", mode: "READ", category: "Export", api: "Documents.Export PDF — value = chemin (optionnel)" },
  { name: "exporter_stl", description: "Exporte en STL (maillage de triangles). Format standard pour l'impression 3D, le prototypage rapide et la visualisation web. Converti la geometrie exacte en facettes triangulaires.", mode: "READ", category: "Export", api: "Documents.Export STL — value = chemin (optionnel)" },
  { name: "exporter_iges", description: "Exporte en IGES, format historique d'echange CAO encore utilise dans l'aeronautique et l'automobile. Moins precis que STEP mais plus largement supporte par les anciens systemes.", mode: "READ", category: "Export", api: "Documents.Export IGES — value = chemin (optionnel)" },
  { name: "lister_exporteurs", description: "Liste tous les formats d'export disponibles dans l'installation TopSolid courante. La liste depend des modules installes et des licences.", mode: "READ", category: "Export", api: "Documents.GetExporterNames" },
  { name: "exporter_nomenclature_csv", description: "Exporte le contenu de la nomenclature (BOM) du document en format texte avec colonnes separees par tabulation. Permet d'importer la BOM dans Excel ou un ERP.", mode: "READ", category: "Export", api: "Nomenclatures lecture → format texte" },
  { name: "exporter_batch_step", description: "Exporte TOUTES les pieces et assemblages du projet en STEP, un fichier par document, dans un dossier de destination. Utile pour livrer un projet complet a un partenaire utilisant un autre logiciel CAO.", mode: "READ", category: "Export", api: "Documents.Export en boucle — value = dossier destination" },
  // Audit
  { name: "audit_piece", description: "Audit complet de la piece : proprietes PDM (nom, designation, reference, fabricant), nombre et liste des parametres, shapes avec nombre de faces, masse, volume, surface et materiau. Rapport synthetique en un appel.", mode: "READ", category: "Audit", api: "Pdm + Parameters + Shapes + parametres systeme" },
  { name: "audit_assemblage", description: "Audit complet d'un assemblage : liste des pieces, inclusions, occurrences, masse totale, structure. Equivalent d'une fiche d'identite de l'assemblage.", mode: "READ", category: "Audit", api: "Assemblies.GetParts + GetOccurrences + masse systeme" },
  { name: "verifier_piece", description: "Check-list qualite pour la piece : designation renseignee ? Reference renseignee ? Materiau affecte ? Retourne OK ou ATTENTION pour chaque critere. A utiliser avant livraison ou archivage.", mode: "READ", category: "Audit", api: "GetDescription + GetPartNumber + presence materiau" },
  { name: "verifier_projet", description: "Verification qualite a l'echelle du projet entier : liste les pieces sans designation et les pieces sans reference (part number). Detecte les documents incomplets avant une livraison.", mode: "READ", category: "Audit", api: "Iteration complete avec filtres" },
  { name: "verifier_materiaux_manquants", description: "Parcourt le projet et liste les pieces qui n'ont pas de materiau affecte (masse = 0 kg). Indispensable avant un calcul de masse d'assemblage ou un export pour simulation.", mode: "READ", category: "Audit", api: "Filtre $Mass == 0 sur tous les docs" },
  { name: "lister_documents_sans_reference", description: "Detecte les pieces et assemblages du projet dont le champ reference (part number) est vide. Utile pour l'audit qualite avant mise en production ou archivage.", mode: "READ", category: "Audit", api: "Filtre GetPartNumber vide" },
  { name: "lister_documents_sans_designation", description: "Detecte les documents du projet dont la designation (description fonctionnelle) est vide. Une designation manquante rend les nomenclatures et les recherches inutilisables.", mode: "READ", category: "Audit", api: "Filtre GetDescription vide" },
  { name: "auditer_noms_parametres", description: "Analyse la syntaxe des noms de parametres du document : detecte les melanges de convention (CamelCase vs snake_case), les doublons proches (ex: \"longueur\" et \"Longueur\"), les caracteres speciaux. Recommandations d'harmonisation.", mode: "READ", category: "Audit", api: "Analyse syntaxique regex des noms" },
  { name: "auditer_noms_parametres_batch", description: "Meme audit que auditer_noms_parametres mais sur TOUS les documents du projet. Rapport global des incoherences de nommage a l'echelle du projet.", mode: "READ", category: "Audit", api: "Iteration complete + analyse syntaxique" },
  { name: "auditer_designations_drivers_batch", description: "Liste les designations des parametres drivers de toutes les familles du projet pour inspection visuelle. Detecte les fautes d'orthographe, les incoherences de formulation entre familles.", mode: "READ", category: "Audit", api: "GetCatalogColumnParameters → GetDescription sur toutes les familles" },
  // Mise en plan
  { name: "detecter_mise_en_plan", description: "Verifie si le document courant est une mise en plan TopSolid (Drafting). Retourne le nombre de vues, le format papier et les informations de base. Premiere verification avant d'explorer un plan.", mode: "READ", category: "Mise en plan", api: "DraftingDocuments.IsDraftingDocument" },
  { name: "ouvrir_mise_en_plan", description: "Cherche automatiquement la mise en plan associee a la piece ou l'assemblage courant en parcourant les back-references PDM (documents de type .TopDft qui referencent le doc actif), puis l'ouvre dans l'editeur.", mode: "WRITE", category: "Mise en plan", api: "SearchBackReferences filtre .TopDft → Documents.Open" },
  { name: "lister_vues_mise_en_plan", description: "Liste toutes les vues du plan avec pour chacune : nom, echelle et type (projection, coupe, detail, isometrique...). Les vues sont les representations 2D de la piece/assemblage 3D.", mode: "READ", category: "Mise en plan", api: "DraftingDocuments.GetViews → GetViewScale" },
  { name: "lire_echelle_mise_en_plan", description: "Lit l'echelle globale du plan et l'echelle individuelle de chaque vue. L'echelle globale definit le ratio par defaut (ex: 1:2), les vues peuvent avoir leur propre echelle.", mode: "READ", category: "Mise en plan", api: "GetDocumentScale + GetViewScale par vue" },
  { name: "lire_format_mise_en_plan", description: "Retourne les informations du format papier : taille (A4, A3, A2...), dimensions en mm, marges, orientation (paysage/portrait), nombre de pages.", mode: "READ", category: "Mise en plan", api: "GetSheetSize / Margins / PageCount" },
  { name: "lire_projection_principale", description: "Identifie la piece ou l'assemblage 3D source de la mise en plan et les vues principales (face, dessus, droite). Permet de remonter du plan 2D vers le modele 3D.", mode: "READ", category: "Mise en plan", api: "GetProjectionDocument" },
  // Nomenclature
  { name: "detecter_nomenclature", description: "Verifie si le document contient une nomenclature (BOM - Bill of Materials). Retourne le nombre de lignes. La nomenclature est generalement dans la mise en plan d'un assemblage.", mode: "READ", category: "Nomenclature", api: "Nomenclatures.GetNomenclatures" },
  { name: "lire_colonnes_nomenclature", description: "Liste les colonnes de la nomenclature : Repere, Designation, Quantite, Reference, Materiau, Masse... La structure depend du modele de nomenclature configure dans TopSolid.", mode: "READ", category: "Nomenclature", api: "Nomenclatures.GetColumns → GetColumnName" },
  { name: "lire_contenu_nomenclature", description: "Lit le tableau complet de la nomenclature : toutes les lignes avec toutes les cellules. Format texte tabulaire lisible. C'est le contenu brut de la BOM tel qu'il apparait dans le plan.", mode: "READ", category: "Nomenclature", api: "GetRows → GetCellValue par colonne" },
  { name: "compter_lignes_nomenclature", description: "Compte les lignes actives et inactives de la nomenclature. Les lignes inactives sont masquees dans le plan mais toujours presentes dans les donnees.", mode: "READ", category: "Nomenclature", api: "GetRows + IsRowActive" },
  // Depliage
  { name: "detecter_mise_a_plat", description: "Verifie si le document contient un depliage tolerie (flat pattern). Le depliage est le contour plat obtenu en \"deroulant\" une piece en tole. Liste les plis detectes. Specifique a la tolerie TopSolid.", mode: "READ", category: "Depliage", api: "Detection operations FlatPattern" },
  { name: "lire_plis_depliage", description: "Liste les plis du depliage tolerie avec pour chacun : angle de pliage (en degres), rayon interieur (en mm) et longueur developpee (en mm). Donnees essentielles pour programmer la plieuse.", mode: "READ", category: "Depliage", api: "Proprietes des operations de pliage" },
  { name: "lire_dimensions_depliage", description: "Retourne les dimensions du contour deplie (flan plat) : longueur et largeur en mm. Ce sont les dimensions de la tole brute a decouper avant pliage.", mode: "READ", category: "Depliage", api: "Parametres systeme de depliage" },
  // Document
  { name: "type_document", description: "Retourne le type du document actif : piece (.TopPrt), assemblage (.TopAsm), mise en plan (.TopDft), etc. Detecte aussi si c'est une famille ou une mise a plat. Premiere chose a verifier pour adapter le traitement.", mode: "READ", category: "Document", api: "IsAssembly + IsDraftingDocument + IsFamily + GetType" },
  { name: "sauvegarder_document", description: "Sauvegarde le document actif dans le PDM TopSolid. Equivalent du Ctrl+S dans l'interface. A faire apres des modifications de parametres ou de proprietes.", mode: "WRITE", category: "Document", api: "Pdm.Save(pdmId, true)" },
  { name: "reconstruire_document", description: "Force la reconstruction complete du modele : recalcul de toutes les operations de l'arbre de construction dans l'ordre. Utile apres modification de parametres pour voir le resultat final.", mode: "WRITE", category: "Document", api: "Documents.Rebuild(docId)" },
  { name: "sauvegarder_tout_projet", description: "Sauvegarde tous les documents du projet en une seule passe. Equivalent du \"Sauvegarder tout\" de TopSolid. A utiliser apres des modifications batch ou avant de fermer.", mode: "WRITE", category: "Document", api: "Pdm.Save en boucle sur tous les docs" },
  { name: "lire_propriete_utilisateur", description: "Lit une propriete utilisateur personnalisee (texte) du document. Ces proprietes sont definies par l'entreprise dans le modele PDM (ex: \"Indice\", \"Traitement\", \"Validation\").", mode: "READ", category: "Document", api: "Pdm.GetUserPropertyValue — value = nom de la propriete" },
  { name: "modifier_propriete_utilisateur", description: "Modifie une propriete utilisateur personnalisee. Le format est nom:valeur separes par deux-points. La propriete doit exister dans le modele PDM.", mode: "WRITE", category: "Document", api: "Pdm.SetUserPropertyValue — value = nom:valeur" },
  { name: "invoquer_commande", description: "Execute une commande du menu TopSolid par son nom interne. Attention : certaines commandes ouvrent des dialogues interactifs. A utiliser quand aucune recette dediee n'existe pour l'action souhaitee.", mode: "WRITE", category: "Document", api: "Application.InvokeCommand — value = nom commande" },
  // Comparaison
  { name: "comparer_revisions", description: "Compare les parametres de la revision courante avec la revision precedente du meme document. Montre les valeurs qui ont change entre les deux revisions. Utile pour tracer les modifications.", mode: "READ", category: "Comparaison", api: "Pdm.GetMajorRevisions → diff GetParameters" },
  { name: "comparer_operations_documents", description: "Compare l'arbre de construction entre le document courant et un autre document du projet. Montre les operations ajoutees, supprimees ou presentes dans les deux avec des noms differents.", mode: "READ", category: "Comparaison", api: "Operations.GetOperations diff — value = nom autre document" },
  { name: "comparer_entites_documents", description: "Compare les entites geometriques (shapes, esquisses, points 3D, reperes 3D) entre deux documents. Montre les differences de quantite par type d'entite.", mode: "READ", category: "Comparaison", api: "Comptage Shapes/Sketches/Points/Frames — value = nom autre doc" },
  // Batch
  { name: "reporter_parametres", description: "Copie les valeurs de parametres du document actif vers un autre document du projet. Le match se fait par nom de parametre. Ignore les parametres systeme ($Mass, $Volume...). Ideal pour propager une modification sur plusieurs pieces similaires.", mode: "WRITE", category: "Batch", api: "Parameters.Set{Real,Text,Integer,Boolean}Value — value = nom doc cible" },
  { name: "reporter_proprietes_pdm", description: "Copie la designation, la reference et le fabricant du document actif vers un autre document du projet. Utile pour homogeneiser les proprietes entre pieces d'une meme famille ou serie.", mode: "WRITE", category: "Batch", api: "Pdm.Set{Description,PartNumber,Manufacturer} — value = nom doc cible" },
  { name: "lire_propriete_batch", description: "Lit une propriete specifique (designation, reference, fabricant ou propriete utilisateur) sur tous les documents du projet. Retourne un tableau nom de document → valeur. Vue globale d'une propriete.", mode: "READ", category: "Batch", api: "Pdm.Get* en boucle — value = nom de la propriete" },
  { name: "vider_auteur_batch", description: "Vide le champ Auteur de tous les documents du projet. Utile pour anonymiser les fichiers avant une livraison client ou un transfert de projet.", mode: "WRITE", category: "Batch", api: "Pdm.SetAuthor(\"\") en boucle" },
  { name: "vider_auteur_document", description: "Vide le champ Auteur du document courant uniquement.", mode: "WRITE", category: "Document", api: "Pdm.SetAuthor(pdmId, \"\")" },
  { name: "verifier_virtuel_batch", description: "Verifie la propriete 'virtuel' (IsVirtualDocument) sur tous les documents du projet. Liste les documents non-virtuels. Un document virtuel n'est pas stocke physiquement sur le serveur.", mode: "READ", category: "Batch", api: "Documents.IsVirtualDocument en boucle" },
  { name: "activer_virtuel_batch", description: "Active le mode virtuel sur tous les documents non-virtuels du projet. Les documents virtuels ne sont pas sauvegardes physiquement — ils existent uniquement en memoire.", mode: "WRITE", category: "Batch", api: "Documents.SetVirtualDocumentMode(true) en boucle" },
  { name: "activer_virtuel_document", description: "Active le mode virtuel sur le document courant uniquement.", mode: "WRITE", category: "Document", api: "Documents.SetVirtualDocumentMode(docId, true)" },
]

const search = ref('')
const sortKey = ref('name')
const sortAsc = ref(true)
const filterMode = ref('ALL')
const filterCategory = ref('ALL')

const categories = computed(() => {
  const cats = [...new Set(recipes.map(r => r.category))].sort()
  return ['ALL', ...cats]
})

const modes = ['ALL', 'READ', 'WRITE', 'ASK']

const modeColors = { READ: '#3b82f6', WRITE: '#ef4444', ASK: '#f59e0b' }

const filtered = computed(() => {
  let list = recipes
  if (search.value) {
    const q = search.value.toLowerCase()
    list = list.filter(r =>
      r.name.includes(q) || r.description.toLowerCase().includes(q) || r.api.toLowerCase().includes(q)
    )
  }
  if (filterMode.value !== 'ALL') list = list.filter(r => r.mode === filterMode.value)
  if (filterCategory.value !== 'ALL') list = list.filter(r => r.category === filterCategory.value)
  list = [...list].sort((a, b) => {
    const va = a[sortKey.value] || ''
    const vb = b[sortKey.value] || ''
    return sortAsc.value ? va.localeCompare(vb) : vb.localeCompare(va)
  })
  return list
})

const stats = computed(() => ({
  total: recipes.length,
  read: recipes.filter(r => r.mode === 'READ').length,
  write: recipes.filter(r => r.mode === 'WRITE').length,
  ask: recipes.filter(r => r.mode === 'ASK').length,
  shown: filtered.value.length
}))

function toggleSort(key) {
  if (sortKey.value === key) sortAsc.value = !sortAsc.value
  else { sortKey.value = key; sortAsc.value = true }
}

function sortIcon(key) {
  if (sortKey.value !== key) return ' \u2195'
  return sortAsc.value ? ' \u2191' : ' \u2193'
}
</script>

<template>
  <div class="recipe-table-container">
    <div class="controls">
      <input v-model="search" type="text" placeholder="Rechercher (nom, description, API)..." class="search-input" />
      <select v-model="filterCategory" class="filter-select">
        <option v-for="c in categories" :key="c" :value="c">{{ c === 'ALL' ? 'Toutes categories' : c }}</option>
      </select>
      <select v-model="filterMode" class="filter-select">
        <option v-for="m in modes" :key="m" :value="m">{{ m === 'ALL' ? 'Tous modes' : m }}</option>
      </select>
    </div>

    <div class="stats-bar">
      <span class="stat">{{ stats.shown }}/{{ stats.total }} recettes</span>
      <span class="badge read">{{ stats.read }} READ</span>
      <span class="badge write">{{ stats.write }} WRITE</span>
      <span class="badge ask">{{ stats.ask }} ASK</span>
    </div>

    <div class="table-scroll">
      <table>
        <thead>
          <tr>
            <th @click="toggleSort('name')" class="sortable">Recette{{ sortIcon('name') }}</th>
            <th @click="toggleSort('description')" class="sortable">Description{{ sortIcon('description') }}</th>
            <th @click="toggleSort('api')" class="sortable">API{{ sortIcon('api') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in filtered" :key="r.name">
            <td class="col-name">
              <code>{{ r.name }}</code>
              <span class="mode-badge" :style="{ background: modeColors[r.mode] }">{{ r.mode }}</span>
              <span class="cat-badge">{{ r.category }}</span>
            </td>
            <td>{{ r.description }}</td>
            <td><code class="api-code">{{ r.api }}</code></td>
          </tr>
          <tr v-if="filtered.length === 0">
            <td colspan="3" class="no-results">Aucune recette trouvee</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.recipe-table-container {
  margin: 1rem 0;
}
.controls {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}
.search-input {
  flex: 1;
  min-width: 200px;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--vp-c-border);
  border-radius: 6px;
  background: var(--vp-c-bg-soft);
  color: var(--vp-c-text-1);
  font-size: 0.9rem;
}
.search-input:focus {
  outline: none;
  border-color: var(--vp-c-brand-1);
}
.filter-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--vp-c-border);
  border-radius: 6px;
  background: var(--vp-c-bg-soft);
  color: var(--vp-c-text-1);
  font-size: 0.9rem;
  cursor: pointer;
}
.stats-bar {
  display: flex;
  gap: 0.75rem;
  align-items: center;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  color: var(--vp-c-text-2);
}
.stat { font-weight: 600; }
.badge {
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 0.75rem;
  font-weight: 600;
  color: white;
}
.badge.read { background: #3b82f6; }
.badge.write { background: #ef4444; }
.badge.ask { background: #f59e0b; }
.table-scroll {
  overflow-x: auto;
  border: 1px solid var(--vp-c-border);
  border-radius: 8px;
}
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.78rem;
  table-layout: fixed;
}
col.col-recipe { width: 28%; }
col.col-desc { width: 40%; }
col.col-api { width: 32%; }
thead {
  background: var(--vp-c-bg-soft);
  position: sticky;
  top: 0;
}
th {
  padding: 0.4rem 0.5rem;
  text-align: left;
  font-weight: 600;
  font-size: 0.75rem;
  border-bottom: 2px solid var(--vp-c-border);
  white-space: nowrap;
}
th.sortable {
  cursor: pointer;
  user-select: none;
}
th.sortable:hover {
  color: var(--vp-c-brand-1);
}
td {
  padding: 0.35rem 0.5rem;
  border-bottom: 1px solid var(--vp-c-border);
  vertical-align: top;
  line-height: 1.35;
}
tr:hover td {
  background: var(--vp-c-bg-soft);
}
.col-name code {
  font-size: 0.72rem;
  padding: 1px 3px;
  border-radius: 3px;
  background: var(--vp-c-bg-mute);
  display: block;
  margin-bottom: 3px;
}
.api-code {
  font-size: 0.68rem;
  color: var(--vp-c-text-2);
  word-break: break-word;
}
.cat-badge {
  display: inline-block;
  padding: 0px 4px;
  border-radius: 3px;
  font-size: 0.62rem;
  background: var(--vp-c-bg-mute);
  color: var(--vp-c-text-3);
  white-space: nowrap;
}
.mode-badge {
  display: inline-block;
  padding: 0px 5px;
  border-radius: 8px;
  font-size: 0.6rem;
  font-weight: 700;
  color: white;
  text-transform: uppercase;
  margin-right: 3px;
}
.no-results {
  text-align: center;
  color: var(--vp-c-text-3);
  padding: 2rem !important;
}
</style>
