---
name: topsolid-mcp
description: Pilote TopSolid via MCP. Utilise ce skill pour TOUTE question TopSolid (etat, designation, parametres, export, esquisses, assemblages, couleurs, audit).
version: 3.0.0
metadata:
  hermes:
    tags: [topsolid, cao, cad, pdm, mcp, automation]
    trigger_phrases: ["topsolid", "piece", "assemblage", "esquisse", "parametre", "designation", "reference", "fabricant", "export", "step", "dxf", "pdf", "nomenclature", "mise en plan", "mise a plat", "couleur", "masse", "audit"]
---

# TopSolid MCP — Skill de pilotage

## REGLES ABSOLUES

1. Tu ne connais PAS l'API TopSolid. Tu NE GENERES JAMAIS de code C#.
2. Tu utilises UNIQUEMENT `mcp_topsolid_topsolid_run_recipe` avec le nom d'une recette.
3. **EN CAS DE DOUTE, TU DEMANDES CLARIFICATION A L'UTILISATEUR.** Ne devine pas.
4. **AUTOMATISME D'ABORD** : le but est de faire gagner du temps. Si l'action est claire, agis directement. La selection interactive (AskShape) ne s'active que s'il y a plusieurs choix possibles.

## Principe : automatique vs interactif

| Situation | Comportement |
|---|---|
| 1 seul element possible | Agir directement (pas de question) |
| Plusieurs elements, l'utilisateur n'a pas precise | La recette demande selection dans TopSolid |
| L'utilisateur dit "tout" ou "tous" | Utiliser la variante _tout (ex: attribut_modifier_couleur_tout) |
| L'utilisateur a nomme l'element | Chercher par nom, agir directement |

## Quand demander clarification (en TEXTE, avant d'appeler la recette)

Si la demande est ambigue, DEMANDE avant d'agir :

| Situation ambigue | Demande a poser |
|---|---|
| "change la couleur" sans precision | "Sur quel element ? Ou sur tout ?" |
| "renomme" | "Tu veux changer le nom PDM, la designation, ou la reference ?" |
| "exporte" sans format | "En quel format ? STEP, DXF, PDF, STL, IGES ?" |
| "modifie le parametre" sans valeur | "Quelle valeur ? (en mm pour les longueurs, en degres pour les angles)" |
| "verifie" sans precision | "Tu veux verifier cette piece seule ou tout le projet ?" |
| "supprime" | "Tu veux supprimer un parametre, un element, ou le document ?" |

## Workflow (2 etapes max)

### Etape 1 — Etat TopSolid (optionnel mais recommande)
Appelle `mcp_topsolid_topsolid_get_state` pour verifier la connexion.

### Etape 2 — Executer une recette
Appelle `mcp_topsolid_topsolid_run_recipe` avec le bon nom de recette.

## Mapping question → recette

### Proprietes PDM
| L'utilisateur demande | recipe | value |
|---|---|---|
| designation, description | lire_designation | |
| nom du document | lire_nom | |
| reference, part number | lire_reference | |
| fabricant, fournisseur | lire_fabricant | |
| toutes les proprietes | lire_proprietes_pdm | |
| changer la designation | modifier_designation | nouvelle valeur |
| renommer | modifier_nom | nouveau nom |
| changer la reference | modifier_reference | nouvelle reference |
| changer le fabricant | modifier_fabricant | nouveau fabricant |

### Navigation projet
| L'utilisateur demande | recipe | value |
|---|---|---|
| projet courant | lire_projet_courant | |
| contenu du projet | lire_contenu_projet | |
| chercher un document | chercher_document | nom a chercher |
| ouvrir un document | ouvrir_document_par_nom | nom du document |
| tous les documents du projet | lister_documents_projet | |

### Parametres
| L'utilisateur demande | recipe | value |
|---|---|---|
| liste des parametres | lire_parametres | |
| valeur d'un parametre reel | lire_parametre_reel | nom du parametre |
| valeur d'un parametre texte | lire_parametre_texte | nom du parametre |
| modifier un parametre reel | modifier_parametre_reel | nom:valeurSI |
| modifier un parametre texte | modifier_parametre_texte | nom:valeur |
| comparer avec une autre piece | comparer_parametres | nom de l'autre piece |

### Geometrie et visualisation
| L'utilisateur demande | recipe | value |
|---|---|---|
| points 3D | lire_points_3d | |
| reperes | lire_reperes_3d | |
| esquisses | lister_esquisses | |
| shapes, formes | lire_shapes | |
| operations, arbre | lire_operations | |
| couleur de la piece | lire_couleur_piece | |
| couleurs des faces | lire_couleurs_faces | |
| changer la couleur | modifier_couleur_piece | R,G,B |

### Assemblages
| L'utilisateur demande | recipe | value |
|---|---|---|
| c'est un assemblage ? | detecter_assemblage | |
| liste des inclusions | lister_inclusions | |
| occurrences | lire_occurrences | |
| renommer une occurrence | renommer_occurrence | ancien:nouveau |
| compter les pieces | compter_pieces_assemblage | |
| masse totale assemblage | rapport_masse_assemblage | |

### Audit et verification
| L'utilisateur demande | recipe | value |
|---|---|---|
| audit complet piece | audit_piece | |
| audit assemblage | audit_assemblage | |
| verification qualite piece | verifier_piece | |
| verification qualite projet | verifier_projet | |
| pieces sans materiau | verifier_materiaux_manquants | |

### Performance et physique
| L'utilisateur demande | recipe | value |
|---|---|---|
| masse, volume, poids | lire_masse_volume | |
| densite, materiau | lire_materiau | |
| boite englobante, dimensions brut | lire_boite_englobante | |

### Export
| L'utilisateur demande | recipe | value |
|---|---|---|
| exporter en STEP | exporter_step | chemin (optionnel) |
| exporter en DXF | exporter_dxf | chemin (optionnel) |
| exporter en PDF | exporter_pdf | chemin (optionnel) |
| exporter en STL | exporter_stl | chemin (optionnel) |
| exporter en IGES | exporter_iges | chemin (optionnel) |
| formats disponibles | lister_exporteurs | |
| nomenclature en CSV | exporter_nomenclature_csv | |

### Mise en plan et nomenclature
| L'utilisateur demande | recipe | value |
|---|---|---|
| c'est une mise en plan ? | detecter_mise_en_plan | |
| vues du plan | lister_vues_mise_en_plan | |
| c'est une nomenclature ? | detecter_nomenclature | |
| colonnes nomenclature | lire_colonnes_nomenclature | |

### Document et projet
| L'utilisateur demande | recipe | value |
|---|---|---|
| type de document | type_document | |
| sauvegarder | sauvegarder_document | |
| reconstruire | reconstruire_document | |
| sauvegarder tout le projet | sauvegarder_tout_projet | |
| propriete utilisateur | lire_propriete_utilisateur | nom propriete |
| modifier propriete utilisateur | modifier_propriete_utilisateur | nom:valeur |

### Commandes TopSolid directes
| L'utilisateur demande | recipe | value |
|---|---|---|
| lancer une commande | invoquer_commande | nom de la commande |

## Couleurs courantes

Si l'utilisateur dit une couleur par nom, convertir en RGB :
| Couleur | RGB |
|---|---|
| rouge | 255,0,0 |
| vert | 0,128,0 |
| bleu | 0,0,255 |
| jaune | 255,255,0 |
| orange | 255,165,0 |
| blanc | 255,255,255 |
| noir | 0,0,0 |
| gris | 128,128,128 |

## Unites

Les valeurs dans TopSolid sont en **SI** :
- Longueurs en metres : 50mm = 0.05
- Angles en radians : 45deg = 0.785398
- Masses en kg

## Glossaire

| Francais TopSolid | Signification |
|---|---|
| Designation | Description du document (colonne "Designation", PAS le nom) |
| Reference | Part number (colonne "Reference") |
| Mise au coffre | CheckIn |
| Sorti de coffre | CheckOut |
| Revetement | Coating (la couleur d'apparence de la piece) |

## Si la question ne correspond a aucune recette

Utilise `mcp_topsolid_topsolid_api_help` avec un mot-cle pour chercher.
Puis reponds avec l'information trouvee. NE GENERE PAS de code.
