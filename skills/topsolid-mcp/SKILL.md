---
name: topsolid-mcp
description: Pilote TopSolid via MCP. Utilise ce skill pour TOUTE question TopSolid (etat, designation, parametres, export, esquisses, assemblages, couleurs, audit, masse, dimensions).
version: 4.0.0
metadata:
  hermes:
    tags: [topsolid, cao, cad, pdm, mcp, automation]
    trigger_phrases: ["topsolid", "piece", "pièce", "pieces", "pièces", "assemblage", "esquisse", "parametre", "paramètre", "parametres", "paramètres", "designation", "désignation", "reference", "référence", "fabricant", "export", "step", "dxf", "pdf", "nomenclature", "mise en plan", "mise a plat", "couleur", "masse", "poids", "volume", "dimensions", "audit", "materiau", "matériau", "densite", "densité", "surface", "inertie", "stl", "iges"]
---

# TopSolid MCP — Skill de pilotage

## REGLE UNIQUE

Tu appelles `mcp_topsolid_topsolid_run_recipe` avec le bon nom. Tu ne generes JAMAIS de code C#. Tu ne decris JAMAIS ce que tu vas faire. Tu FAIS.

## EXEMPLES A SUIVRE EXACTEMENT

User: "combien de pieces dans l'assemblage?"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="compter_pieces_assemblage")
→ Reponse: "L'assemblage contient 4 pieces (2 references uniques)."

User: "c'est quoi la masse?"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="lire_masse_volume")
→ Reponse: "Masse: 9.922 kg, Volume: 1263905 mm3."

User: "change la designation en Bride support"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="modifier_designation", value="Bride support")
→ Reponse: "Designation modifiee: Bride support."

User: "exporte en STEP"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="exporter_step")
→ Reponse: "Export STEP OK: C:\...\fichier.step"

User: "quels parametres?"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="lire_parametres")
→ Reponse: "29 parametres: Parametre 1 = 10, Masse = 9.922 kg..."

User: "la masse de l'assemblage?"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="rapport_masse_assemblage")
→ Reponse: "Masse totale: 9.922 kg, 4 pieces."

User: "exporte en DXF"
→ Appel: mcp_topsolid_topsolid_run_recipe(recipe="exporter_dxf")
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
| designation | lire_designation | |
| nom | lire_nom | |
| reference | lire_reference | |
| fabricant | lire_fabricant | |
| toutes les proprietes | lire_proprietes_pdm | |
| changer designation | modifier_designation | nouvelle valeur |
| renommer | modifier_nom | nouveau nom |
| changer reference | modifier_reference | nouvelle ref |
| changer fabricant | modifier_fabricant | nouveau fabricant |

### Navigation projet
| Demande | recipe | value |
|---|---|---|
| projet courant | lire_projet_courant | |
| contenu projet | lire_contenu_projet | |
| chercher document | chercher_document | nom |
| ouvrir document | ouvrir_document_par_nom | nom |
| tous les documents | lister_documents_projet | |
| documents d'un dossier | lister_documents_dossier | nom dossier |
| resume du projet | resumer_projet | |
| documents par type | compter_documents_par_type | |
| sans reference | lister_documents_sans_reference | |
| sans designation | lister_documents_sans_designation | |
| pieces par materiau/masse | chercher_pieces_par_materiau | filtre (opt) |
| cas d'emploi, where-used | lire_cas_emploi | |
| historique revisions | lire_historique_revisions | |
| comparer revisions | comparer_revisions | |
| comparer parametres | comparer_parametres | nom autre doc |
| comparer operations | comparer_operations_documents | nom autre doc |
| comparer entites | comparer_entites_documents | nom autre doc |
| reporter parametres | reporter_parametres | nom doc cible |
| reporter proprietes PDM | reporter_proprietes_pdm | nom doc cible |
| export batch STEP | exporter_batch_step | dossier (opt) |
| lire propriete batch | lire_propriete_batch | nom propriete |
| documents modifies | chercher_documents_modifies | |
| vider auteur projet | vider_auteur_batch | |
| vider auteur doc | vider_auteur_document | |
| verifier virtuel | verifier_virtuel_batch | |
| activer virtuel projet | activer_virtuel_batch | |
| activer virtuel doc | activer_virtuel_document | |
| drivers famille sans designation | verifier_drivers_famille | |
| corriger drivers famille | corriger_drivers_famille | |
| audit drivers toutes familles | verifier_drivers_famille_batch | |

### Parametres
| Demande | recipe | value |
|---|---|---|
| liste parametres | lire_parametres | |
| parametre reel | lire_parametre_reel | nom |
| parametre texte | lire_parametre_texte | nom |
| modifier reel | modifier_parametre_reel | nom:valeurSI |
| modifier texte | modifier_parametre_texte | nom:valeur |
| comparer | comparer_parametres | nom autre piece |

### Masse, volume, dimensions
| Demande | recipe | value |
|---|---|---|
| masse, poids, volume | lire_masse_volume | |
| masse assemblage | rapport_masse_assemblage | |
| densite, materiau | lire_densite_materiau | |
| materiau | lire_materiau | |
| dimensions piece | lire_dimensions_piece | |
| boite englobante | lire_boite_englobante | |
| moments inertie | lire_moments_inertie | |

### Geometrie et visualisation
| Demande | recipe | value |
|---|---|---|
| points 3D | lire_points_3d | |
| reperes | lire_reperes_3d | |
| esquisses | lister_esquisses | |
| shapes, formes | lire_shapes | |
| operations, arbre | lire_operations | |
| couleur piece | lire_couleur_piece | |
| couleurs faces | lire_couleurs_faces | |
| changer couleur | modifier_couleur_piece | R,G,B |

### Assemblages
| Demande | recipe | value |
|---|---|---|
| c'est un assemblage? | detecter_assemblage | |
| inclusions | lister_inclusions | |
| occurrences | lire_occurrences | |
| renommer occurrence | renommer_occurrence | ancien:nouveau |
| compter pieces | compter_pieces_assemblage | |

### Export
| Demande | recipe | value |
|---|---|---|
| STEP | exporter_step | chemin (opt) |
| DXF | exporter_dxf | chemin (opt) |
| PDF | exporter_pdf | chemin (opt) |
| STL | exporter_stl | chemin (opt) |
| IGES | exporter_iges | chemin (opt) |
| formats dispo | lister_exporteurs | |
| nomenclature CSV | exporter_nomenclature_csv | |

### Audit
| Demande | recipe | value |
|---|---|---|
| audit piece | audit_piece | |
| audit assemblage | audit_assemblage | |
| verif piece | verifier_piece | |
| verif projet | verifier_projet | |
| materiaux manquants | verifier_materiaux_manquants | |

### Mise en plan
| Demande | recipe | value |
|---|---|---|
| mise en plan? | detecter_mise_en_plan | |
| ouvre le plan | ouvrir_mise_en_plan | |
| vues du plan | lister_vues_mise_en_plan | |
| echelle | lire_echelle_mise_en_plan | |
| format, taille papier | lire_format_mise_en_plan | |
| projection principale | lire_projection_principale | |

### Nomenclature (BOM)
| Demande | recipe | value |
|---|---|---|
| nomenclature? | detecter_nomenclature | |
| colonnes nomenclature | lire_colonnes_nomenclature | |
| contenu nomenclature | lire_contenu_nomenclature | |
| compter lignes | compter_lignes_nomenclature | |

### Mise a plat (depliage tolerie)
| Demande | recipe | value |
|---|---|---|
| mise a plat? depliage? | detecter_mise_a_plat | |
| plis, angles | lire_plis_depliage | |
| dimensions depliage | lire_dimensions_depliage | |

### Document
| Demande | recipe | value |
|---|---|---|
| type document | type_document | |
| sauvegarder | sauvegarder_document | |
| reconstruire | reconstruire_document | |
| sauvegarder tout | sauvegarder_tout_projet | |
| propriete utilisateur | lire_propriete_utilisateur | nom |
| modifier propriete | modifier_propriete_utilisateur | nom:valeur |
| commande TopSolid | invoquer_commande | nom commande |

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

## SI AUCUNE RECETTE NE CORRESPOND
Appelle `mcp_topsolid_topsolid_api_help` avec un mot-cle. Ne genere JAMAIS de code.
