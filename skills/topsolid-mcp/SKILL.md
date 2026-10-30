---
name: topsolid-mcp
description: Pilote TopSolid via MCP. Utilise ce skill pour TOUTE question TopSolid (etat, designation, parametres, export, esquisses, assemblages, couleurs, transparence, calques, attributs, familles, audit, masse, dimensions, selection, cone, tore, usinage, cam).
version: 5.6.0
metadata:
  hermes:
    tags: [topsolid, cao, cad, pdm, mcp, automation, extruded-bar, sheet-metal]
    trigger_phrases: ["topsolid", "piece", "pièce", "pieces", "pièces", "assemblage", "esquisse", "parametre", "paramètre", "parametres", "paramètres", "designation", "désignation", "reference", "référence", "fabricant", "export", "step", "dxf", "pdf", "nomenclature", "mise en plan", "mise a plat", "couleur", "transparence", "calque", "attribut", "masse", "poids", "volume", "dimensions", "audit", "materiau", "matériau", "densite", "densité", "surface", "inertie", "stl", "iges", "famille", "driver", "selectionner", "face", "shape", "usinage", "cam", "cone", "tore", "bi-arc", "tolerance geometrique", "profile", "profilé", "extruded bar", "tole", "tôle", "platine"]
---

# TopSolid MCP — Skill de pilotage

## CHANGELOG

- **v5.6.0 (2026-10-20)** — Catalogue passe a **211 recettes** (compte verifie le 2026-10-30 dans `server/src/Tools/RecipeTool.cs` : 108 `R` + 96 `RW` + 7 `RD` ; ne jamais citer ce chiffre de memoire, le recompter dans le code) : ajout de la suite complete de **Recettes de Creation CAO, Metallerie (`Extruded Bar` & Toles/Platines), Assemblage Positionne (`BETWEEN`) et Cas d'Emploi (`Where-Used`)** permettant de modeliser un ouvrage complet **100 % via `topsolid_run_recipe`** sans script C# custom :
  - `create_project_tree` (creation projet PDM + arborescence de dossiers),
  - `create_part_document` (`.TopPrt`) et `create_assembly_document` (`.TopAsm`),
  - `create_extruded_bar` et `provide_extruded_bar_function` (profilé metallerie avec fonction système `Extruded Bar.TopPrtFct`, repère `XY` section / `X` horizontal / `+Z` longitudinal, paramètre système `Length` = `L`, perçages transversants optionnels, matériau `.TopMat` et revêtement `.TopCot`),
  - `create_sheet_metal_plate` (tôle / platine percée dans la même esquisse 2D, paramètre `Epaisseur`, boîte englobante minimale, matériau et revêtement),
  - `add_inclusion_positioned` et `batch_add_inclusions` (positionnement par translation/rotation Euler ou mode directeur `BETWEEN,x1,y1,z1,x2,y2,z2,ux,uy,uz`),
  - `update_where_used_and_save` (reconstruction et sauvegarde bottom-up `Pièces -> Sous-Assemblages -> Assemblage Général`).
- **v5.5.0 (2026-10-20)** — Ajout de la **Recherche Generique Ultra-Rapide de Materiaux PDM (`< 500 ms`)** (filtrage des bibliotheques materiaux `CEN`/`Metal`/`Mat`/`Bois`/`Wood` + verification stricte `ext == ".TopMat"` pour eviter les timeouts sur les bibliotheques de visserie et les exceptions `.TopLam`), des **4 Regles Compile-First C#** (`CreateSketchIn3D` -> `ElementId`, pas d'operateur `!=`/`==` sur `PdmObjectId`/`DocumentId`, `DocumentId` pour `SetMaterial`/`CreateInclusion`), du **Budget CoT (zero tableau de coordonnees dans `reasoning`)** et du client CLI `topsolid_mcp_cli.py`.
- **v5.4.0 (2026-10-20)** — Ajout du **Coupe-Circuit (Circuit Breaker) MCP** et de la discipline stricte des tokens : interdiction absolue d'auto-depanner le serveur MCP (lecture de `ScriptExecutor.cs`, compilation de serveur bis) ou de crawler le Web / fouiller le depot pendant une session CAO + support des `return` dans les lambdas C# (`Func<...>`) dans `ScriptExecutor.cs` (`v1.8.8`) + invocation directe `csc.exe` (`CreateNoWindow = true`) eliminant le bug `.dll introuvable` en mode tache planifiee headless.
- **v5.3.0 (2026-10-19)** — Catalogue passe a **199 recettes** (+4 nouvelles : `create_drilling`, `set_material`, `set_coating`, `check_collisions`) + correctifs serveur MCP (`DetectModification` regex multi-variables dans `ScriptExecutor.cs`, auto-detection `7.21` dans `TopSolidPathResolver.cs`, synchronisation de toutes les representations `GetRepresentations` dans `extruder_esquisse`, pas besoin de `CreateProfile` dans les esquisses) + documentation des **Etapes TopSolid** (`GetModelingStage` / `SetWorkingStage` / `ResetInsertionOperation`), de la detection de collisions B-Rep (`SetCollisionsManagement` + `ShapeShapeCollisionEntity`) et du paradigme **One-Shot C# Builder** dans `references/concepts-core.md` et `references/gabarits-recipes.md`.
- **v5.2.0 (2026-09-30)** — Catalogue passe a **151 recettes** : lots GREEN de la veille 7.21 implementes (19 nouvelles) — `list_positionings`, `is_positioning_underconstrained` (assemblage), getters durete/etats de surface (`get_material_*` 6 + `get_coating_*` 6), `get_profiles_folder` (esquisse), `get_operation_version`, lectures d'aretes `get_edge_faces`/`get_edge_ellipse_curve` (param `value`=AskEdge), `smooth_sketch_profiles` (RW, AskEdge + smoothing ISketches2D). Methode : membres confirmes via snapshot CHM 7.21 (`methods.json`) + reflection 7.20/7.21 ; corps compile-verifies (csc v4.0.30319 refs Automating) — les 7 recettes utilisant des membres 7.21-only (GetEdgeFaces, GetEdgeEllipseCurve, SmoothSketchProfiles, ICoatings Brinell/Vickers/Bump*) restent compile-verify en signatures seulement.
- **v5.1.0 (2026-09-30)** — Veille API 7.21 : snapshot `7.21.304.0` (1978 méthodes, 3209 pages CHM) ; diff `7.20.311.0` → `7.21.304.0` = **+190 méthodes, 0 retirées, 1 signature changée** (`IPdmAdmin.GetUserGroups`, OBSOLETE → `GetGroupsOfUsers`). 20 recettes du catalogue désormais documentées dans les tables ci-dessous. Nouvelle section « Surface API 7.21 » (méthodes hors recettes) et « Nouveautés produit 7.21 » (What's New). Constat runtime : le graphe embarqué (`data/graph.json`, 4119 edges, 242 types, 1728 methodes x interface sur 46 interfaces, datant du 09/09) ne contient NI les méthodes 7.20 NI les 7.21 — `topsolid_api_help` peut donc ne pas connaître une méthode très récente (vérifier alors dans la section « Surface API 7.21 »).
- v5.0.0 — catalogue 132 recettes, mode R/RW/RD, outils MCP détaillés.

## REGLES & BONNES PRATIQUES UTILISATEUR TOPSOLID (SKILL VIVANT)

> **Regle d'evolution continue (Skill vivant)** : Ce skill centralise les bonnes pratiques d'utilisation de TopSolid et du pont MCP. **Chaque fois qu'un nouveau piege API, un bug d'execution ou une bonne pratique CAO est decouvert en test**, ce fichier (ainsi que `references/concepts-core.md` et `topsolid-mcp-modeling`) **doit etre immediatement mis a jour et synchronise**.
> **Skills metiers specialises** : Des que la tache porte sur la conception d'un ouvrage metier specifique, **charger imperativement en parallele le skill metier correspondant** :
> - **Garde-corps / rambardes / balustrades** : charger **`topsolid-railing-design`** (`TopSolid-Railing-Design`).
> - **Cages SubD depuis photo** : charger **`topsolid-cage-from-image`**.

### A. Bonnes pratiques CAO & PDM TopSolid (obligatoires)
1. **Modification d'un sous-document & Mise a jour des Cas d'Emploi (`Where-Used`) avant sauvegarde** :
   - Dans TopSolid, modifier une piece (`.TopPrt`) ou un sous-assemblage incremente sa revision mineure (`DocumentId` change apres `EnsureIsDirty(ref docId)`).
   - **Regle d'or** : chaque fois qu'un sous-document est modifie, il faut :
     1. Sauvegarder le sous-document enfant (`Pdm.Save(childPdmId, true)` / `save_document`),
     2. Consulter ses **cas d'emploi** (`read_where_used` / `TopSolidHost.Pdm.GetWhereUsed`),
     3. Ouvrir et reconstruire les assemblages / mises en plan parents (`EnsureIsDirty(ref parentDocId)` + `EndModification(true, true)` / `rebuild_document`) pour qu'ils pointent sur la nouvelle revision de l'enfant,
     4. Sauvegarder les parents **dans l'ordre Enfant -> Parent**.
2. **Materiaux PDM (`.TopMat`) et Revetements (`.TopCot`) obligatoires & Recherche Generique `< 500 ms`** :
   - Une piece TopSolid sans materiau PDM (`.TopMat`) est invalide. Ne jamais se contenter d'un `SetColor` RGB : toujours affecter un vrai materiau (`Parts.SetMaterial(docId, matDocId)`) + `Parts.SetMassPropertyManagement(docId, true, new SmartReal(UnitType.Mass, 0))` + `Parts.SetPhysicalPropertiesManagementRefreshAuto(docId, true)`.
   - **Recherche generique ultra-rapide (`< 500 ms`)** : Ne JAMAIS appeler `SearchProjectByName("Mécanique CEN TopSolid")` (une requete multi-mots retourne 0) et ne JAMAIS parcourir les 55 bibliotheques de `GetProjects(false, true)` sans filtre (timeout 60 s sur les bibliotheques de visserie). Filtrer `GetProjects(false, true)` uniquement sur les bibliotheques contenant `"CEN"`, `"Metal"`, `"Mat"`, `"Bois"`, `"Wood"`, chercher les designations metier standards (`*S235*`, `*S355*`, `*X2CrNi19-11*`, `*304*`, `*Chêne*`, `*Pin*`), et verifier que `TopSolidHost.Pdm.GetType(h, out ext)` vaut **`".TopMat"`** (pour exclure les `.TopLam` stratifies bois qui levent `InvalidCastException`).
   - Verifier le materiau d'une piece via `TopSolidDesignHost.Parts.GetMaterial(docId)` (et non via `check_missing_materials` qui cherche un parametre `"Mass"` en anglais).
3. **Synchronisation de TOUTES les Representations (`Conception`, `Detaillee`, `Simplifiee`)** :
   - Seule la toute premiere forme (`Shape 1`) d'une piece neuve est ajoutee par defaut aux representations. Toute forme supplementaire ou recreee doit etre ajoutee a toutes les representations (`GetRepresentations(docId)` -> `AddRepresentationConstituent(rep, shId)`), sinon la piece apparait vide dans l'assemblage parent.
4. **Esquisses 2D & Extrusions sans fenetre modale** :
   - Toujours passer **`true` en 4e argument de `CreateSketchIn3D`** (`bool inDefinesXDirection = true`, sinon les axes `(u, v)` de l'esquisse sont tournes de 90 degres).
   - Toujours appeler **`TopSolidHost.Sketches2D.CleanSketch()`** juste avant `TopSolidHost.Sketches2D.EndModification()` (egalement auto-injecte par le serveur MCP depuis la `v1.8.7`) pour calculer les profils fermes (`GetProfileCount > 0`) avant toute extrusion dans la meme transaction.
5. **Etapes TopSolid (`Stages`) & Collisions B-Rep** :
   - Activer `SetCollisionsManagement` bascule le document en **Etape d'Analyse (`AnalysisStage`)**. Pour ajouter ou modifier ensuite des operations de modelisation ou des inclusions, revenir imperativement en **Etape de Modelisation (`ModelingStage`) a l'interieur d'un `StartModification`** :
     `TopSolidHost.Operations.SetWorkingStage(TopSolidHost.Operations.GetModelingStage(docId)); TopSolidHost.Operations.ResetInsertionOperation(docId);`
6. **Architecture d'Assemblage reelle (ZERO multi-corps fourre-tout)** :
   - **Interdiction absolue** de modeliser un ouvrage complet en creant des dizaines ou centaines d'esquisses/extrusions dans des fichiers `.TopPrt` multi-corps fourre-tout (ex: 16 poteaux extrudes dans 1 seul fichier).
   - Toujours creer **1 document `.TopPrt` par composant reel unique** (avec son `.TopMat` et `.TopCot`), puis l'instancier $N$ fois par inclusion (`Assemblies.CreateInclusion`) dans un document d'assemblage `.TopAsm`.

### B. Coupe-Circuit (Circuit Breaker) & Discipline de Tokens (CRITIQUE)
1. **Coupe-Circuit immediat en cas de panne systeme MCP (Max 2 essais / < 15 secondes)** :
   - Si `topsolid_compile`, `topsolid_execute_script` ou `topsolid_modify_script` renvoie une erreur systeme ou d'infrastructure (ex: `System error`, `.dll introuvable`, `csc.exe failed`, serveur HTTP injoignable), **ARRETE-TOI IMMEDIATEMENT apres 1 verification**.
   - **INTERDICTION FORMELLE** pendant une session CAO :
     - de lire le code source C# du serveur MCP (`ScriptExecutor.cs`, `Program.cs`, etc.),
     - d'ouvrir des sessions WinRM/SSH vers le poste TopSolid pour debugger les processus ou `%TEMP%`,
     - de compiler un exécutable wrapper de contournement (ex: `TaskMcpHost.exe`).
   - Remonte simplement l'erreur systeme exacte a l'utilisateur en 3 lignes (seul le skill `topsolid-mcp-ops` sur demande explicite de maintenance a le droit de diagnostiquer le serveur).
2. **Zero fouille de depot local & Zero Crawl Web normatif** :
   - Ne cherche jamais de scripts Python/C# dans le depot `topsolid-automation-mcp` (ex: `tools/steel_ai_generator`) et ne lance jamais de recherches Web (AFNOR, Legifrance, etc.) qui gonflent la fenetre de contexte de +50 000 tokens relus a chaque tour. Tout est dans les skills metiers et les outils MCP.
3. **Zero Monologue de Coordonnees dans le `reasoning` (CoT) & Client CLI pret a l'emploi** :
   - Ne calcule jamais des tableaux de coordonnees $(X, Y, Z)$ piece par piece dans ta reflexion interne (`reasoning`) avant de coder (gain : 4 a 5 minutes) : ecris directement les boucles `for` parametriques en C#.
   - Utilise directement les outils MCP ou `python topsolid_mcp_cli.py compile|exec|state|recipe` sans reecrire de client `mcp_client.py`.
4. **Les 4 Regles Compile-First C#** :
   - `CreateSketchIn3D(...)` retourne un **`ElementId`** (jamais `ElementItemId`).
   - `PdmObjectId`, `DocumentId` et `ElementId` n'ont **pas d'operateur `==` ou `!=`** (`CS0019`) : utiliser `.IsEmpty`, `!id.IsEmpty` ou `.Equals(...)`.
   - `Parts.SetMaterial` et `Assemblies.CreateInclusion` prennent des **`DocumentId`** (jamais `PdmObjectId`).
   - Depuis `v1.8.8`, les lambdas `Func<...>` avec `return` sont supportees dans `topsolid_compile` / `topsolid_execute_script`.

### C. Regles d'appel des recettes (`topsolid_run_recipe`) — Priorite Absolue aux Recettes MCP
- **Priorite n°1 (`topsolid_run_recipe`)** : Utiliser systematiquement `topsolid_run_recipe` aussi bien pour les actions unitaires que pour la **creation complete de projets, pieces de metallerie (`create_extruded_bar`, `create_sheet_metal_plate`), sous-assemblages et assemblages (`create_part_document`, `create_assembly_document`, `batch_add_inclusions`), mise a jour Where-Used (`update_where_used_and_save`) et verification des collisions B-Rep (`check_collisions`)**.
- Ne recourir a `topsolid_execute_script` / `topsolid_modify_script` qu'en dernier ressort pour des geometries complexes hors catalogue de recettes.

**Recettes de LECTURE** - `read_*`, `list_*`, `count_*`, `detect_*`, `search_*`, `compare_*`, `audit_*`, `check_*`, `summarize_*`, `document_type` (note : `check_collisions` active l'analyse de collisions dans une transaction mais ne modifie pas la geometrie). Elles ne modifient rien. Tu agis directement : pas d'annonce, pas de demande de permission.

**Recettes d'ECRITURE** - `set_*`, `rename_*`, `fix_*`, `copy_*`, `enable_*`, `clear_*`, `save_*`, `rebuild_*`, `export_*`, `batch_*`, `attr_set_*`, `attr_replace_*`, `attr_assign_*`, `invoke_command`, plus `create_*`, `provide_*`, `add_*`, `update_*`, `creer_*`, `ajouter_inclusion`, `extruder_esquisse`, `activate_bom_row`, `deactivate_bom_row`, `print_drafting`, `set_drafting_*`, `set_projection_quality`. Elles modifient le modele, les donnees PDM, ou ecrivent des fichiers sur le disque.

**Regle de secours (celle qui tranche)** : si une recette ne commence PAS par un des prefixes de LECTURE ci-dessus, tu la traites comme une ECRITURE et tu demandes confirmation. Le doute va toujours vers la confirmation.

`server/data/recipe-list.txt` est un export figé du catalogue avec la colonne de mode `R` / `RW` / `RD` (lecture / ecriture PDM / ecriture disque). Il reste toutefois un export fige, pas une source : la source de verite est `server/src/Tools/RecipeTool.cs` (`R(` / `RW(` / `RD(`). En cas de doute entre les deux, c'est le code qui tranche.

**Version API** : la surface 7.21 vit dans `server/data/api/7.21.304.0/` (snapshot CHM : 1978 methodes) + `server/data/recipe-proposals-7.21.304.0.md` (propositions) + `server/data/changelog-7.21.304.0.md`, regeneres par `python server/scripts/sync-topsolid-api.py all` (stages extract→parse→diff→enrich→propose→report, baseline 7.20.311.0). Les propositions (tiers Green/Yellow/Red) ne deviennent des recettes qu'apres validation et implementation C#.

## EXEMPLES A SUIVRE EXACTEMENT

User: "combien de pieces dans l'assemblage?"
→ Appel: topsolid__topsolid_run_recipe(recipe="count_assembly_parts")
→ Reponse: "L'assemblage contient 4 pieces (2 references uniques)."

User: "c'est quoi la masse?"
→ Appel: topsolid__topsolid_run_recipe(recipe="read_mass_volume")
→ Reponse: "Masse: 9.922 kg, Volume: 1263905 mm3."

User: "change la designation en Bride support"   (ECRITURE : on confirme d'abord)
→ Reponse: "set_designation, value = Bride support, sur le document actif. Je lance ?"
User: "oui"
→ Appel: topsolid__topsolid_run_recipe(recipe="set_designation", value="Bride support")
→ Reponse: "Designation modifiee: Bride support."

User: "exporte en STEP"   (ECRITURE : ecrit un fichier sur le disque)
→ Reponse: "export_step, chemin par defaut a cote du document. Je lance ?"
User: "vas-y"
→ Appel: topsolid__topsolid_run_recipe(recipe="export_step")
→ Reponse: "Export STEP OK: C:\...\fichier.step"

User: "quels parametres?"
→ Appel: topsolid__topsolid_run_recipe(recipe="read_parameters")
→ Reponse: "29 parametres: Parametre 1 = 10, Masse = 9.922 kg..."

User: "la masse de l'assemblage?"
→ Appel: topsolid__topsolid_run_recipe(recipe="assembly_mass_report")
→ Reponse: "Masse totale: 9.922 kg, 4 pieces."

User: "exporte en DXF"   (ECRITURE : et l'utilisateur peut dire non)
→ Reponse: "export_dxf, chemin par defaut. Je lance ?"
User: "non, laisse tomber"
→ Reponse: "OK, rien n'a ete modifie."

## QUAND DEMANDER CLARIFICATION

| Situation | Question |
|---|---|
| "change la couleur" sans precision | "Quel element ? Ou tout ?" |
| "renomme" | "Le nom PDM, la designation, ou la reference ?" |
| "exporte" sans format | "Quel format ? STEP, DXF, PDF, STL, IGES ?" |
| "modifie le parametre" sans valeur | "Quelle valeur ?" |

## RECETTES DISPONIBLES

### Creation CAO, Metallerie (Extruded Bar & Toles) et Assemblage Positionne
| Demande | recipe | value |
|---|---|---|
| creer projet PDM + dossiers | create_project_tree | `nom_projet[;dossier1,dossier2,...]` |
| creer document Piece (`.TopPrt`) | create_part_document | `nom_doc[;designation[;reference[;dossier[;projet]]]]` |
| creer document Assemblage (`.TopAsm`) | create_assembly_document | `nom_doc[;designation[;reference[;dossier[;projet]]]]` |
| creer un profilé (`Extruded Bar`) complet | create_extruded_bar | `nom;desig;ref;dossier;PLAT\|TUBE_RECT\|ROND\|TUBE_ROND\|CORNIERE;dims_mm;L_mm[;trous_z_offX_diam[;mat[;cot_ou_rgb]]]` |
| fournir la fonction `Extruded Bar` + repere XY/X/Z | provide_extruded_bar_function | `[nom_doc;]largeur_x_mm;hauteur_y_mm;longueur_z_mm` |
| creer une tole / platine percee | create_sheet_metal_plate | `nom;desig;ref;dossier;Lx_mm;Wy_mm;Ep_mm[;trous_x_y_diam[;mat[;cot_ou_rgb[;CENTER\|ORIGIN]]]]` |
| affecter materiau (`.TopMat`) + Masse/CdG | set_material | `[nom_doc;]mot_cle_mat` (ex: `S235JR` ou `GC_Montant;S235JR`) |
| affecter revetement (`.TopCot`) + RGB | set_coating | `[nom_doc;]mot_cle_cot[;R,G,B]` (ex: `RAL 7016;56,62,66`) |
| inclure et positionner 1 composant | add_inclusion_positioned | `[asm\|]enfant;tx,ty,tz[,rx,ry,rz]` ou `[asm\|]enfant;BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]` |
| inclure et positionner N composants en lot | batch_add_inclusions | `[asm\|][CLEAR\|]item1;item2;...` (avec `tx,ty,tz` ou `BETWEEN,x1,y1,z1,x2,y2,z2,ux,uy,uz,nomOcc`) |
| mettre a jour Where-Used et sauvegarder | update_where_used_and_save | `[nom_projet]` (reconstruit Pieces -> Sous-Assemblages -> Assemblage General) |
| verifier collisions B-Rep 3D | check_collisions | `[nom_asm]` |

### Proprietes PDM
| Demande | recipe | value |
|---|---|---|
| designation | read_designation | |
| nom | read_name | |
| reference | read_reference | |
| fabricant | read_manufacturer | |
| toutes les proprietes | read_pdm_properties | |
| changer designation | set_designation | nouvelle valeur |
| renommer | set_name | nouveau nom |
| changer reference | set_reference | nouvelle ref |
| changer fabricant | set_manufacturer | nouveau fabricant |

### Navigation projet
| Demande | recipe | value |
|---|---|---|
| projet courant | read_current_project | |
| contenu projet | read_project_contents | |
| chercher document | search_document | nom |
| chercher dossier | search_folder | nom |
| ouvrir document | open_document_by_name | nom |
| tous les documents | list_project_documents | |
| documents d'un dossier | list_folder_documents | nom dossier |
| resume du projet | summarize_project | |
| documents par type | count_documents_by_type | |
| sans reference | list_documents_without_reference | |
| sans designation | list_documents_without_designation | |
| pieces par materiau/masse | search_parts_by_material | filtre (opt) |
| cas d'emploi, where-used | read_where_used | |
| historique revisions | read_revision_history | |
| comparer revisions | compare_revisions | |
| comparer parametres | compare_parameters | nom autre doc |
| comparer operations | compare_document_operations | nom autre doc |
| comparer entites | compare_document_entities | nom autre doc |
| reporter parametres | copy_parameters_to | nom doc cible |
| reporter proprietes PDM | copy_pdm_properties_to | nom doc cible |
| export batch STEP | batch_export_step | dossier (opt) |
| lire propriete sur tout le projet | batch_read_property | nom propriete |
| designation sur TOUT le projet | batch_set_designation | nouvelle designation |
| reference sur TOUT le projet | batch_set_reference | nouvelle reference |
| fabricant sur TOUT le projet | batch_set_manufacturer | nouveau fabricant |
| documents modifies | find_modified_documents | |
| vider auteur projet | batch_clear_author | |
| vider auteur doc | clear_document_author | |
| verifier virtuel | batch_check_virtual | |
| activer virtuel projet | batch_enable_virtual | |
| activer virtuel doc | enable_virtual_document | |
| drivers famille sans designation | check_family_drivers | |
| corriger drivers famille | fix_family_drivers | |
| audit drivers toutes familles | batch_check_family_drivers | |

### Parametres
| Demande | recipe | value |
|---|---|---|
| liste parametres | read_parameters | |
| parametre reel | read_real_parameter | nom |
| parametre texte | read_text_parameter | nom |
| modifier reel | set_real_parameter | nom:valeurSI |
| modifier texte | set_text_parameter | nom:valeur |
| creer parametre reel | creer_parametre_reel | nom:unite:valeurSI (ex: Longueur:Length:0.05 pour 50mm ; unite Longueur/Masse/Angle/NoUnit) |
| creer parametre formule | creer_parametre_formule | nom:unite:formule (ex: DiagBolt:Length:Longueur * 1.414) |
| comparer | compare_parameters | nom autre piece |

### Masse, volume, dimensions
| Demande | recipe | value |
|---|---|---|
| masse, poids, volume | read_mass_volume | |
| masse assemblage | assembly_mass_report | |
| densite, materiau | read_material_density | |
| materiau | read_material | |
| affecter materiau (.TopMat) | set_material | filtre nom (ex: S235) |
| affecter revetement (.TopCot) | set_coating | filtre nom (ex: Galvanisé) |
| dimensions piece | read_part_dimensions | |
| boite englobante | read_bounding_box | |
| moments inertie | read_inertia_moments | |

### Geometrie et visualisation
| Demande | recipe | value |
|---|---|---|
| points 3D | read_3d_points | |
| reperes | read_3d_frames | |
| esquisses | list_sketches | |
| cone d'une face : rayon | get_face_cone_radius | |
| cone d'une face : demi-angle | get_face_cone_semi_angle | |
| cone d'une face : longueur | get_face_cone_length | |
| tore d'une face : grand rayon | get_face_torus_major_radius | |
| tore d'une face : petit rayon | get_face_torus_minor_radius | |
| creer esquisse rectangle | creer_esquisse_rectangle | largeur:hauteur (mm) |
| extruder la derniere esquisse | extruder_esquisse | hauteur (mm) |
| creer percage sur face | create_drilling | diam_mm:prof_mm:X_mm:Y_mm:Z_mm:NX:NY:NZ |
| operation d'origine d'une face (apres select_face) | get_item_last_operation_name | |
| shapes, formes | read_shapes | |
| operations, arbre | read_operations | |

### Attributs (couleurs, transparence, calques)
| Demande | recipe | value |
|---|---|---|
| tout lire (couleur, transparence, calque) | attr_read_all | |
| couleur des elements | attr_read_color | |
| couleurs par face | attr_read_face_colors | |
| changer couleur (1 element) | attr_set_color | R,G,B |
| changer couleur (tout) | attr_set_color_all | R,G,B |
| remplacer une couleur par une autre | attr_replace_color | R1,G1,B1:R2,G2,B2 |
| transparence | attr_read_transparency | |
| changer transparence | attr_set_transparency | 0.0 a 1.0 |
| calques (layers) | attr_list_layers | |
| affecter un calque | attr_assign_layer | nom_element:nom_calque |

### Selection interactive
| Demande | recipe | value |
|---|---|---|
| selectionner un shape | select_shape | |
| selectionner une face | select_face | |
| cliquer un point 3D | select_3d_point | |

### Assemblages
| Demande | recipe | value |
|---|---|---|
| c'est un assemblage? | detect_assembly | |
| inclusions | list_inclusions | |
| inserer une piece (inclusion) | ajouter_inclusion | nom du document piece |
| detecter collisions B-Rep | check_collisions | |
| occurrences | read_occurrences | |
| renommer occurrence | rename_occurrence | ancien:nouveau |
| compter pieces | count_assembly_parts | |

### Familles
| Demande | recipe | value |
|---|---|---|
| c'est une famille? | detect_family | |
| codes de la famille | read_family_codes | |

### Export
| Demande | recipe | value |
|---|---|---|
| STEP | export_step | chemin (opt) |
| DXF | export_dxf | chemin (opt) |
| PDF | export_pdf | chemin (opt) |
| STL | export_stl | chemin (opt) |
| IGES | export_iges | chemin (opt) |
| formats dispo | list_exporters | |
| nomenclature CSV | export_bom_csv | |

### Audit
| Demande | recipe | value |
|---|---|---|
| audit piece | audit_part | |
| audit assemblage | audit_assembly | |
| verif piece | check_part | |
| verif projet | check_project | |
| materiaux manquants | check_missing_materials | |
| audit noms parametres | audit_parameter_names | |
| audit noms parametres (projet) | batch_audit_parameter_names | |
| audit designations drivers | batch_audit_driver_designations | |

### Mise en plan
| Demande | recipe | value |
|---|---|---|
| mise en plan? | detect_drafting | |
| ouvre le plan | open_drafting | |
| vues du plan | list_drafting_views | |
| echelle | read_drafting_scale | |
| changer echelle | set_drafting_scale | denominateur (10 = 1:10) |
| format, taille papier | read_drafting_format | |
| changer format | set_drafting_format | nom format (A3, A4) |
| qualite de projection | set_projection_quality | exact ou fast |
| imprimer le plan (N&B, 300 DPI, a l'echelle) | print_drafting | |
| projection principale | read_main_projection | |

### Nomenclature (BOM)
| Demande | recipe | value |
|---|---|---|
| nomenclature? | detect_bom | |
| colonnes nomenclature | read_bom_columns | |
| contenu nomenclature | read_bom_contents | |
| compter lignes | count_bom_rows | |
| activer une ligne | activate_bom_row | index de ligne (0-based, fils racine) |
| desactiver une ligne | deactivate_bom_row | index de ligne (0-based, fils racine) |

### Mise a plat (depliage tolerie)
| Demande | recipe | value |
|---|---|---|
| mise a plat? depliage? | detect_unfolding | |
| plis, angles | read_bend_features | |
| dimensions depliage | read_unfolding_dimensions | |

### Document
| Demande | recipe | value |
|---|---|---|
| type document | document_type | |
| sauvegarder | save_document | |
| reconstruire | rebuild_document | |
| sauvegarder tout | save_all_project | |
| propriete utilisateur | read_user_property | nom |
| modifier propriete | set_user_property | nom:valeur |
| commande TopSolid | invoke_command | nom commande |

### Surface API 7.21 (methodes hors recettes)
Ces methodes TopSolid 7.21 n'ont PAS de recette. Pour les utiliser : `topsolid_execute_script` / `topsolid_modify_script` (dernier recours, confirmation obligatoire), ou proposer une nouvelle recette (propositions completes : `server/data/recipe-proposals-7.21.304.0.md`, tiers Green/Yellow/Red).
| Besoin | Methode API (since 7.21) | Interface |
|---|---|---|
| cone d'une face : rayon, demi-angle, longueur | GetFaceConeRadius / GetFaceConeSemiAngle / GetFaceConeLength (ElementItemId) | IShapes |
| surface cone d'une face (frame + rayon + semi-angle) | GetFaceConeSurface | IShapes |
| tore d'une face : rayons major/minor | GetFaceTorusMajorRadius / GetFaceTorusMinorRadius / GetFaceTorusSurface | IShapes |
| point le plus proche sur une face | FindFaceNearestPointToPoint (face, point) → (point, u, v) | IShapes |
| points les plus proches (batch) | FindFaceNearestPointsToPoints | IShapes |
| courbe elliptique d'une arete | GetEdgeEllipseCurve (arete) → plan + rayons | IShapes |
| faces adjacentes d'une arete | GetEdgeFaces | IShapes |
| spline par points d'interpolation (nouveau Bi-arc 7.21) | CreateBSplineSegmentByInterpolationPoints | ISketches2D / ISketches3D |
| lisser des profils d'esquisse | SmoothSketchProfiles | ISketches2D / ISketches3D |
| options de creation/lissage d'esquisse | Get/SetSketchCreationOptions (+ SketchOptions, SketchCleanOptions, SketchAdvancedOptions) | ISketches2D / ISketches3D |
| tolerancage geometrique : texte d'un cadre | GetGeometricToleranceFrameToleranceValueText (by tolerance, index cadre) | IAnnotations |
| apercu bitmap d'une mise en plan | DrawToBitmap (doc, page, largeur, hauteur) | IDraftings |
| pages d'un bundle, formats | GetBundleDraftings / GetBundlePageFormat / SetBundlePageFormat etc. | IDraftings |
| parametres de matériaux (Young, Poisson, duretes Brinell/Vickers/Rockwell, thermiques...) | Get/SetYoungModulus, SetPoissonRatio, Get/SetBrinellHardnessFactor... | IMaterials / ICoatings |

## NOUVEAUTES TOPSOLID 7.21 (produit, What's New 7.21)

- **Sketch** : nouveau type de courbe **Bi-arc** ; deux nouvelles options de validation d'esquisse (rotation et zoom a la validation, dans Tools > Options > Sketch). Cote API : `CreateBSplineSegmentByInterpolationPoints` + `SmoothSketchProfiles` + options completes (`SketchOptions`/`SketchAdvancedOptions`).
- **Shape** : cote API, nouveaux acces geometriques sur les faces (**cone/tore : rayon, semi-angle, longueurs**), points les plus proches sur face, aretes elliptiques.
- **Unfolding / Tolerie** : Work Manager et reutilisation des processus ; **Nesting** (module optionnel) pour l'imbrication des pieces planes.
- **Stringers (escaliers)** : nouvelle option **Smoothing** pour lisser les esquisses de lisses creees par Staircase Layout.
- Detail complet : PDF *What's New TopSolid 7.21* (`Help/en` du repertoire d'installation TopSolid).

## COULEURS

| Nom | RGB |
|---|---|
| rouge | 255,0,0 |
| vert | 0,128,0 |
| bleu | 0,0,255 |
| jaune | 255,255,0 |
| orange | 255,165,0 |
| blanc | 255,255,255 |
| noir | 0,0,0 |
| gris | 128,128,128 |

## UNITES (TopSolid = SI)
- Longueurs: metres (50mm = 0.05)
- Angles: radians (45deg = 0.785398)
- Masses: kg

## LES OUTILS MCP

Le serveur enregistre 13 outils (12 en mode `--read-only`, ou `topsolid_modify_script`
n'est pas enregistre). Les 7 detailles ci-dessous couvrent l'essentiel ; les autres
(`topsolid_list_recipes`, `topsolid_get_recipe`, `topsolid_compile`,
`topsolid_search_examples`, `topsolid_search_help`, `topsolid_search_commands`)
servent a explorer le catalogue et la documentation.

### 1. topsolid_run_recipe (outil principal — 90% des cas)
Appelle une recette par son nom. Les tableaux ci-dessus couvrent les cas courants ;
le catalogue complet se lit avec `topsolid_list_recipes`, et le detail
d'une recette avec `topsolid_get_recipe`. Params: recipe, value (optionnel).

### 2. topsolid_get_state
Retourne l'etat de connexion, le document actif et le projet courant.
→ **Toujours appeler en premier** pour verifier que TopSolid est connecte.

Exemple:
→ topsolid__topsolid_get_state()
→ "Connected: true, Document: Bride.TopPrt, Project: MonProjet"

### 3. topsolid_api_help (fallback — quand aucune recette ne correspond)
Recherche dans le graphe embarque de l'API (base anterieure a 7.21 : 4119 aretes,
1728 methodes uniques par interface, 46 interfaces). Supporte des synonymes FR.
Param: query (mot-cle en francais ou anglais).
**Limite 7.21** : les methodes ajoutees en 7.20/7.21 (ex. cone/tore, spline d'interpolation)
ne sont pas encore dans le graphe — utiliser la section « Surface API 7.21 » ci-dessus.

Exemples:
→ topsolid__topsolid_api_help(query="contrainte assemblage")
→ topsolid__topsolid_api_help(query="tolerance")

### 4. topsolid_find_path (expert — exploration API)
Trouve le chemin le plus court (Dijkstra) entre deux types API.
Utile pour comprendre comment aller de IDocumentId a IShapeId par exemple.
Params: from_type, to_type.

Exemple:
→ topsolid__topsolid_find_path(from_type="IDocumentId", to_type="IShapeId")
→ "IDocumentId → GetShapes() → IShapeId (2 etapes)"

### 5. topsolid_explore_paths (expert — exploration multi-chemins)
Exploration BFS multi-chemins entre deux types API. Plus large que find_path.
Params: from_type, to_type, max_depth (opt).

### 6. topsolid_execute_script (expert — lecture seule)
Compile et execute du C# contre l'API TopSolid. Lecture seule (pas de transaction).
**Utiliser UNIQUEMENT si aucune recette ne correspond ET apres reflexion.**
Param: code (code C# complet).

### 7. topsolid_modify_script (expert — modification avec transaction)
Comme execute_script mais avec transaction TopSolid (begin/end modification).
Pour les modifications qui n'ont pas de recette.
**Utiliser avec EXTREME PRUDENCE — peut modifier le modele. **
Param: code (code C# complet).

## REGLES D'UTILISATION DES OUTILS

1. **get_state** en premier (toujours)
2. **run_recipe** pour 90% des demandes - direct en lecture, apres confirmation en ecriture
3. **api_help** si aucune recette ne correspond
4. **find_path / explore_paths** uniquement si l'utilisateur pose des questions sur l'API
5. **execute_script / modify_script** en DERNIER recours, jamais en premier choix, et jamais
   sans confirmation explicite : ces deux outils compilent et executent du C# directement dans
   le processus TopSolid, sans bac a sable.