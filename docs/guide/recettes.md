# Recettes

113 recettes pre-construites dans `RecipeTool`. Le LLM selectionne par nom via `topsolid_run_recipe` -- aucune generation de code necessaire.

## Statistiques

- **113 recettes** (95 auto + 18 batch/comparaison/audit)
- **Tests LIVE PASS** sur TopSolid vivant
- **13 categories** fonctionnelles

## Par categorie

### Proprietes PDM (9 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| lire_designation | Designation du document | READ |
| lire_nom | Nom du document | READ |
| lire_reference | Reference (part number) | READ |
| lire_fabricant | Fabricant/fournisseur | READ |
| lire_proprietes_pdm | Toutes les proprietes PDM | READ |
| modifier_designation | Changer la designation | WRITE |
| modifier_nom | Renommer le document | WRITE |
| modifier_reference | Changer la reference | WRITE |
| modifier_fabricant | Changer le fabricant | WRITE |

### Navigation projet (5 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| lire_projet_courant | Projet actif | READ |
| lire_contenu_projet | Contenu du projet | READ |
| chercher_document | Chercher un document par nom | READ |
| ouvrir_document_par_nom | Ouvrir un document | WRITE |
| lister_documents_projet | Tous les documents du projet | READ |

### Parametres (6 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| lire_parametres | Liste tous les parametres | READ |
| lire_parametre_reel | Valeur d'un parametre reel | READ |
| lire_parametre_texte | Valeur d'un parametre texte | READ |
| modifier_parametre_reel | Modifier un parametre reel (SI) | WRITE |
| modifier_parametre_texte | Modifier un parametre texte | WRITE |
| comparer_parametres | Comparer avec un autre document | READ |

### Masse, volume, dimensions (7 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| lire_masse_volume | Masse, volume, surface (system params) | READ |
| rapport_masse_assemblage | Masse totale assemblage + Part Count | READ |
| lire_densite_materiau | Densite calculee (masse/volume) | READ |
| lire_materiau | Materiau affecte | READ |
| lire_dimensions_piece | Height, Width, Length, Box Size | READ |
| lire_boite_englobante | Boite englobante (system params + API) | READ |
| lire_moments_inertie | Moments principaux X/Y/Z | READ |

### Geometrie et visualisation (8 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| lire_points_3d | Points 3D du document | READ |
| lire_reperes_3d | Reperes 3D | READ |
| lister_esquisses | Esquisses du document | READ |
| lire_shapes | Shapes/formes | READ |
| lire_operations | Arbre des operations | READ |
| lire_couleur_piece | Couleur du revetement | READ |
| lire_couleurs_faces | Couleurs par face | READ |
| modifier_couleur_piece | Changer la couleur (RGB) | WRITE |

### Attributs (6 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| attribut_lire_tout | Tous les attributs visuels | READ |
| attribut_lire_transparence | Transparence | READ |
| attribut_modifier_transparence | Changer transparence | WRITE |
| attribut_lire_couleur | Couleur piece | READ |
| attribut_lister_calques | Liste des calques | READ |
| attribut_affecter_calque | Affecter un calque | WRITE |

### Assemblages (6 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| detecter_assemblage | Detecter type assemblage | READ |
| lister_inclusions | Liste des inclusions | READ |
| lire_occurrences | Occurrences + definitions | READ |
| compter_pieces_assemblage | Nombre de pieces + references | READ |
| renommer_occurrence | Renommer une occurrence | WRITE |

### Export (8 recettes)
| Recette | Description | Format |
|---------|-------------|--------|
| exporter_step | Export STEP | .step |
| exporter_dxf | Export DXF | .dxf |
| exporter_pdf | Export PDF | .pdf |
| exporter_stl | Export STL | .stl |
| exporter_iges | Export IGES | .iges |
| lister_exporteurs | Formats disponibles | -- |
| exporter_nomenclature_csv | BOM en CSV | .csv |

### Audit et verification (5 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| audit_piece | Audit complet piece | READ |
| audit_assemblage | Audit assemblage | READ |
| verifier_piece | Verification qualite | READ |
| verifier_projet | Verification projet | READ |
| verifier_materiaux_manquants | Pieces sans materiau | READ |

### Mise en plan (5 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| detecter_mise_en_plan | Detecter mise en plan | READ |
| lister_vues_mise_en_plan | Liste des vues | READ |
| lire_echelle_mise_en_plan | Echelle globale + par vue | READ |
| lire_format_mise_en_plan | Format papier, dimensions, pages | READ |
| lire_projection_principale | Piece source, vues principales | READ |

### Nomenclature / BOM (4 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| detecter_nomenclature | Detecter nomenclature | READ |
| lire_colonnes_nomenclature | Colonnes du BOM | READ |
| lire_contenu_nomenclature | Tableau complet (lignes + cellules) | READ |
| compter_lignes_nomenclature | Lignes actives/inactives | READ |

### Mise a plat / Depliage (3 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| detecter_mise_a_plat | Detecter depliage tolerie | READ |
| lire_plis_depliage | Plis (angle, rayon, longueur) | READ |
| lire_dimensions_depliage | Dimensions depliage (system params) | READ |

### Document (7 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| type_document | Type de document | READ |
| sauvegarder_document | Sauvegarder | WRITE |
| reconstruire_document | Reconstruire | WRITE |
| sauvegarder_tout_projet | Sauvegarder tout le projet | WRITE |
| lire_propriete_utilisateur | Lire propriete utilisateur | READ |
| modifier_propriete_utilisateur | Modifier propriete utilisateur | WRITE |
| invoquer_commande | Lancer une commande TopSolid | WRITE |

### Interactif Ask* (3 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| selectionner_shape | Selection interactive shape | ASK |
| selectionner_face | Selection interactive face | ASK |
| selectionner_point_3d | Selection interactive point | ASK |

### Familles (5 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| detecter_famille | Detecter document famille | READ |
| lire_codes_famille | Codes/colonnes catalogue | READ |
| verifier_drivers_famille | Verifier designations des drivers | READ |
| corriger_drivers_famille | Auto-generer designation depuis nom | WRITE |
| verifier_drivers_famille_batch | Audit drivers toutes familles projet | READ |

### Comparaison de documents (4 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| comparer_parametres | Diff parametres entre 2 documents | READ |
| comparer_operations_documents | Diff arbre construction entre 2 docs | READ |
| comparer_entites_documents | Diff shapes/esquisses/points/reperes | READ |
| comparer_revisions | Diff parametres entre 2 revisions | READ |

### Report de modifications (2 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| reporter_parametres | Copie valeurs parametres doc A vers doc B | WRITE |
| reporter_proprietes_pdm | Copie designation/ref/fabricant vers doc B | WRITE |

### Batch projet / Bibliotheque (13 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| resumer_projet | Resume structure du projet | READ |
| compter_documents_par_type | Documents par type .TopPrt/.TopAsm/etc | READ |
| lister_documents_dossier | Documents d'un dossier specifique | READ |
| lister_documents_sans_reference | Pieces sans part number | READ |
| lister_documents_sans_designation | Pieces sans description | READ |
| chercher_pieces_par_materiau | Masse de toutes les pieces | READ |
| lire_cas_emploi | Where-used (back-references) | READ |
| lire_propriete_batch | Lire une propriete sur tous les docs | READ |
| chercher_documents_modifies | Documents non sauvegardes (dirty) | READ |
| exporter_batch_step | Export STEP toutes pieces/assemblages | READ |
| vider_auteur_batch | Vider le champ Auteur du projet | WRITE |
| verifier_virtuel_batch | Verifier mode virtuel sur tout le projet | READ |
| activer_virtuel_batch | Activer virtuel sur tous les docs | WRITE |

### Audit qualite (3 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| auditer_noms_parametres | Detecter conventions et doublons | READ |
| auditer_noms_parametres_batch | Audit noms sur tout le projet | READ |
| auditer_designations_drivers_batch | Designations drivers toutes familles | READ |

### Historique revisions (2 recettes)
| Recette | Description | Mode |
|---------|-------------|------|
| lire_historique_revisions | Timeline revisions majeures/mineures | READ |
| ouvrir_mise_en_plan | Ouvrir le plan associe (DraftSwitch) | WRITE |

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
