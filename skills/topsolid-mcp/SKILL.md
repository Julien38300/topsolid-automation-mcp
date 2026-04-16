---
name: topsolid-mcp
description: Pilote TopSolid via MCP. Utilise ce skill pour TOUTE question TopSolid (etat, designation, parametres, export, esquisses, assemblages, couleurs, transparence, calques, attributs, familles, audit, masse, dimensions, selection).
version: 5.0.0
metadata:
  hermes:
    tags: [topsolid, cao, cad, pdm, mcp, automation]
    trigger_phrases: ["topsolid", "piece", "pièce", "pieces", "pièces", "assemblage", "esquisse", "parametre", "paramètre", "parametres", "paramètres", "designation", "désignation", "reference", "référence", "fabricant", "export", "step", "dxf", "pdf", "nomenclature", "mise en plan", "mise a plat", "couleur", "transparence", "calque", "attribut", "masse", "poids", "volume", "dimensions", "audit", "materiau", "matériau", "densite", "densité", "surface", "inertie", "stl", "iges", "famille", "driver", "selectionner", "face", "shape"]
---

# TopSolid MCP — Skill de pilotage

## REGLE UNIQUE

Tu appelles `topsolid__topsolid_run_recipe` avec le bon nom. Tu ne generes JAMAIS de code C#. Tu ne decris JAMAIS ce que tu vas faire. Tu FAIS.

## EXEMPLES A SUIVRE EXACTEMENT

User: "combien de pieces dans l'assemblage?"
→ Appel: topsolid__topsolid_run_recipe(recipe="count_assembly_parts")
→ Reponse: "L'assemblage contient 4 pieces (2 references uniques)."

User: "c'est quoi la masse?"
→ Appel: topsolid__topsolid_run_recipe(recipe="read_mass_volume")
→ Reponse: "Masse: 9.922 kg, Volume: 1263905 mm3."

User: "change la designation en Bride support"
→ Appel: topsolid__topsolid_run_recipe(recipe="set_designation", value="Bride support")
→ Reponse: "Designation modifiee: Bride support."

User: "exporte en STEP"
→ Appel: topsolid__topsolid_run_recipe(recipe="export_step")
→ Reponse: "Export STEP OK: C:\...\fichier.step"

User: "quels parametres?"
→ Appel: topsolid__topsolid_run_recipe(recipe="read_parameters")
→ Reponse: "29 parametres: Parametre 1 = 10, Masse = 9.922 kg..."

User: "la masse de l'assemblage?"
→ Appel: topsolid__topsolid_run_recipe(recipe="assembly_mass_report")
→ Reponse: "Masse totale: 9.922 kg, 4 pieces."

User: "exporte en DXF"
→ Appel: topsolid__topsolid_run_recipe(recipe="export_dxf")
→ Reponse: "Export DXF OK."

## QUAND DEMANDER CLARIFICATION

| Situation | Question |
|---|---|
| "change la couleur" sans precision | "Quel element ? Ou tout ?" |
| "renomme" | "Le nom PDM, la designation, ou la reference ?" |
| "exporte" sans format | "Quel format ? STEP, DXF, PDF, STL, IGES ?" |
| "modifie le parametre" sans valeur | "Quelle valeur ?" |

## RECETTES DISPONIBLES

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
| comparer | compare_parameters | nom autre piece |

### Masse, volume, dimensions
| Demande | recipe | value |
|---|---|---|
| masse, poids, volume | read_mass_volume | |
| masse assemblage | assembly_mass_report | |
| densite, materiau | read_material_density | |
| materiau | read_material | |
| dimensions piece | read_part_dimensions | |
| boite englobante | read_bounding_box | |
| moments inertie | read_inertia_moments | |

### Geometrie et visualisation
| Demande | recipe | value |
|---|---|---|
| points 3D | read_3d_points | |
| reperes | read_3d_frames | |
| esquisses | list_sketches | |
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
| format, taille papier | read_drafting_format | |
| projection principale | read_main_projection | |

### Nomenclature (BOM)
| Demande | recipe | value |
|---|---|---|
| nomenclature? | detect_bom | |
| colonnes nomenclature | read_bom_columns | |
| contenu nomenclature | read_bom_contents | |
| compter lignes | count_bom_rows | |

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

## TOUS LES OUTILS MCP (7)

### 1. topsolid_run_recipe (outil principal — 90% des cas)
Appelle une des 113 recettes ci-dessus. Params: recipe, value (optionnel).

### 2. topsolid_get_state
Retourne l'etat de connexion, le document actif et le projet courant.
→ **Toujours appeler en premier** pour verifier que TopSolid est connecte.

Exemple:
→ topsolid__topsolid_get_state()
→ "Connected: true, Document: Bride.TopPrt, Project: MonProjet"

### 3. topsolid_api_help (fallback — quand aucune recette ne correspond)
Recherche dans 1728 methodes API TopSolid. Supporte 52 synonymes FR.
Param: query (mot-cle en francais ou anglais).

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
**Utiliser avec EXTREME PRUDENCE — peut modifier le modele.**
Param: code (code C# complet).

## REGLES D'UTILISATION DES OUTILS

1. **get_state** en premier (toujours)
2. **run_recipe** pour 90% des demandes
3. **api_help** si aucune recette ne correspond
4. **find_path / explore_paths** uniquement si l'utilisateur pose des questions sur l'API
5. **execute_script / modify_script** en DERNIER recours, jamais en premier choix
