---
type: skill-reference
scope: topsolid-mcp
domain: assemblages-pilotes
version: 1.0.0
created: 2026-09-30
sources:
  - server/data/api/7.21.304.0/methods.json (IAssemblies 64 méthodes, IDocuments, ITable*)
  - server/src/Tools/RecipeTool.cs (ASSEMBLIES, FAMILIES, BATCH)
  - vault 01_TopSolid/Guide_Utilisateur/Pilotes optionnels - Choix manuel des familles et codes.md (ticket 238848, 7.21)
  - vault 01_TopSolid/Transverse/ (Familles_et_Pilotes, Parametrage_Des_Composants, Gestion_Des_Composants)
---

# Assemblages pilotés — contraintes, codes, familles de variantes

## 1. Vocabulaire topologique vérifié (API + CHM)

- Inclusion : le fait, dans un assemblage, de référencer une pièce (ou sous-assemblage) externe.
  Recipe ajouter_inclusion : CreatePositioning(docId) puis CreateInclusion(...) avec
  Transform3D.Identity ; la partie incluse doit être un document OUVERT (GetOpenDocuments).
  Occurrence : l'instance d'une inclusion dans l'arbre. Recipes : read_occurrences (avec définition),
  list_inclusions, rename_occurrence (value=ancien:nouveau), count_assembly_parts, detect_assembly. `[REC] [API-7.21]`
- Occurrence vs définition : GetOccurrenceDefinition / GetOccurrencePublishing (7.8+) distinguent
  l'instance du document référencé ; IsAssemblyOccurrence / IsPartOccurrence typent l'occurrence. `[API-7.21]`
- L'assemblage sait lister ses pièces : GetParts (7.8). Métriques d'assemblage gérables par API :
  Get/SetAssemblyMassManagement, ...CenterOfMassManagement, ...MomentsOfInertiaManagement,
  ...SurfaceAreaManagement, ...VolumeManagement, ...StrictMode (7.13). GetCollisionsManagement /
  SetCollisionsManagement(+InvalidWhenColliding, CheckingMechanisms) pour la détection d'interférence. `[API-7.21]`

## 2. Contraintes

- API contraintes IAssemblies (7.21) : CreateFixedConstraint (7.15), CreateFrameOnFrameConstraint (7.8),
  CreateFrameOnFrameConstraintWithXYOffsets (7.9). Pas de méthode pour LISTER ou SUPPRIMER les
  contraintes existantes dans l'interface scannée ; IsPositioningUnderconstrained (sans since) test si
  le positionnement est sous-contraint. `[API-7.21]`
- Aucune recette n'expose les contraintes aujourd'hui — assemblage piloté = gabarit d'assemblage posé à
  la main, puis occurrences/gabarits pilotés par paramètres. `[REC-aucune-trace]`
- Positionnement : CreatePositioning (7.6) est l'objet ancré par les contraintes ; TransformInclusion
  (7.7.201.100) repositionne une inclusion, RedirectInclusion(WithCodeAndDrivers)(2) la réoriente vers
  un autre gabarit — c'est LE mécanisme de bascule de variante. `[API-7.21]`

## 3. Codes et pilotes d'inclusion — le cœur du pilotage de variantes

- Une inclusion de composant "catalogue" (bibliothèque) porte un CODE et des PILOTES (driver list) qui
  sélectionnent la variante du gabarit. API vérifiée :
  GetInclusionCodeAndDrivers (7.10) / GetInclusionCodeAndDrivers2 (7.17),
  SetInclusionCodeAndDrivers / SetInclusionCodeAndDrivers2, GetFamilyDriverRelatedToCodeDriver,
  GetDriversFromFamily (7.19), HasOptionalDrivers / IsOptionalDriver / GetOptionalDriverDefaultValue,
  GetConditionedDriversVisibilityStatus, GetLocalizedDriverName, GetDriverSubFolderName (7.17). `[API-7.21]`
- Conséquence conception : pour piloter une variante par serveur MCP, il faut (gabarit)
  1 paramètre code + les pilotes exposés en inclusion, puis les bascules d'exécution passent par
  set_real_parameter / set_text_parameter — pas par C# ad-hoc — car SetInclusionCodeAndDrivers n'est
  pas encore wrappé en recette. `[REC-aucune-trace] [A VALIDER: recette SetInclusionCodeAndDrivers]`
- Piège documenté (Guide Utilisateur 7.21, ticket 238848) : quand famille ET code sont tous deux
  "pilotes optionnels" (paramètres tabulés / aiguillage), le choix de la famille met le paramètre code
  en erreur (sa liste ne correspond plus). Solution produit — structuration de gabarit :
  1. booléen FamilleCodeManuel (False par défaut),
  2. paramètre CodePilote référencé sur le paramètre famille,
  3. paramètre tabulé CodeFinal : source = Facteur FamilleCodeManuel ; ligne 0 -> paramètre code
     initial, ligne 1 -> CodePilote,
  4. les inclusions consomment CodeFinal (plus le code initial),
  5. pilotes reconfigurés : supprimer l'ancien code des pilotes, ajouter FamilleCodeManuel +
     CodePilote en pilotes optionnels, avec condition FamilleCodeManuel == 1 (clic droit, Autres puis
     Condition).
  Comportement : False = automatique sans erreur ; True = choix manuel famille puis code, sans erreur. `[V:ticket 238848]`
- Ce pattern est génériquement applicable aux cascades famille->code de tous les gabarits dont le code
  est tabulé — il découple le choix du code du pilotage automatique. `Regle generique [V]`

## 4. Familles — ce qui est exposé côté recettes

- Recipes FAMILIES (noms exacts) : detect_family, read_family_codes, check_family_drivers,
  fix_family_drivers, batch_check_family_drivers, batch_audit_driver_designations. Usage :
  check_family_drivers détecte les pilotes sans désignation (casse l'usage des familles) ;
  fix_family_drivers les corrige en déduisant la désignation du nom du paramètre. `[REC]`
- En prod (vault Transverse, fiabilité inférée) : les familles sont initialisées au favori global et
  les combos sont remplis selon la famille ; certaines familles (PFF01, PSF01) ont des contraintes
  internes (ex. variante pivot uniquement pour 50x50) ; paramètres matériaux/revêtement spécifiques ;
  comportements contrôlés par booléens dans un dossier "Special Cases". `[V-transverse (inféré)]`
- Règle d'agent : pour toute famille d'un gabarit, AVANT écriture : read_family_codes +
  check_family_drivers. Jamais SetInclusionCodeAndDrivers sans avoir lu les drivers courants. `[REC]`

## 5. Where-used

- Recipe read_where_used : "Finds where-used references of the current document in the project". C'est
  la seule voie where-used vérifiée ; il n'existe ni recipe ni API wrappée pour le where-used
  multi-niveaux. Pour impact d'un changement de gabarit : read_where_used sur la pièce gabarit, puis
  list_project_documents / batch_read_property pour croiser. `[REC]`
- Références croisées documentaires : IDocuments.GetReferencedDocuments (7.6) liste les documents
  référencés par un document (sens direct), GetSynchronizedDocuments et IsSynchronized pour les
  documents synchronisés. GetDraftTableCellParameter (7.14) peut faire remonter un paramètre de gabarit
  jusque dans une cellule de tableau de mise en plan (ITables Drafting, 18 méthodes). `[API-7.21]`

## 6. Familles de variantes — structuration recommandée

Sources : recettes audit/comparaison + interface API. Cette section traduit les capacités API en
règles d'architecture de gabarit ; les règles de métier détaillées restent `[A VALIDER]`.
- Une "famille de variantes" se construit : un gabarit maître + des paramètres pilotes nommés +
  (si composant catalogue) pilotes d'inclusion exposés via GetInclusionCodeAndDrivers. La variante est
  sélectionnée par SetInclusionCodeAndDrivers (API) ou set_real_parameter sur un paramètre tabulé
  CodeFinal (pattern section 3). `[API-7.21] [V-ticket 238848]`
- Deux voies d'instanciation vérifiées : ajouter_inclusion par nom de document ouvert (simple,
  Transform3D.Identity, positioning vierge), ou Include/IncludeWithOptions de IDocuments (7.6).
  CanInclude teste la validité avant inclusion. `[REC] [API-7.21]`
- Baseline de pilotage d'une variante existante : read_parameters du gabarit -> set_real_parameter
  (SI) -> rebuild_document -> read_mass_volume / read_part_dimensions -> compare_parameters avec la
  variante de référence -> save_document. `REC + comparateurs audit`
- Comparaisons natives utiles pour valider une variante : compare_parameters,
  compare_document_operations (arbre), compare_document_entities (formes/esquisses/points/repères),
  compare_revisions. audit_parameter_names / batch_audit_parameter_names pour la conformité de
  nommage des pilotes. `[REC]`
- BOM d'assemblage : detect_bom / read_bom_columns / read_bom_contents / count_bom_rows /
  activate_bom_row / deactivate_bom_row / export_bom_csv ; index BOM par nœud : GetNodeBomIndex /
  SetNodeBomIndex (7.12), GetNodeProperties. Propriétés de nœud : GetNodeProperties (7.12) ;
  GetInclusionChildOccurrence (7.8) pour descendre l'arbre. `[REC] [API-7.21]`

## 7. Occurrences — lecture et renommage vérifiés

- read_occurrences liste les occurrences avec leur définition ; rename_occurrence
  (value=ancien_nom:nouveau_nom) renomme UNE occurrence. GetInclusionChildOccurrence (7.8) relie une
  inclusion à l'occurrence fille. Demande interactive : AskOccurrence (7.10), AskOccurrenceList (7.20)
  — à éviter dans un flux serveur sans UI. `[REC] [API-7.21]`
- Dérivation contrôlée : DerivePartForModification (7.10) pour modifier une pièce incluse sans
  impacter les autres occurrences (copie de travail). `[API-7.21]`
- Disassembly : CreateDisassembly (7.17) existe (usage atelier), pas encore wrappé en recipe.
  `[API-7.21] [REC-aucune-trace]`

## 8. Pièges récurrents

- ajouter_inclusion exige le document source OUVERT (GetOpenDocuments) — sinon "not found" ;
  ouvrir par open_document_by_name d'abord. `[REC]`
- Les pilotes sans désignation (check_family_drivers positif) rendent la sélection de code fragile :
  fixer avec fix_family_drivers avant tout pilotage de variante. `[REC]`
- Transformation d'inclusion : TransformInclusion prend une Transform3D SI-complete (origines en m),
  pas des mm. `[API-7.21] [CHM-D-unites]`
- IsPositioningUnderconstrained avant de considérer un assemblage posé comme "stable" pour un pilotage
  paramétrique. `[API-7.21] [A VALIDER : comportement si sous-contraint]`
- Les occurrences d'un assemblage strict : SetAssemblyStrictMode (7.13) peut contraindre les
  modifications ; verifier GetAssemblyStrictMode avant batch set. `[API-7.21]`

## 9. Frontière API/recettes (rappel)

Tout ce qui n'est pas dans cette liste de recettes (ASSEMBLIES 6, FAMILIES 5, BATCH 9) ou l'interface
IAssemblies ci-dessus n'est PAS pilotable aujourd'hui : l'écrire dans une automatisation revient à
inventer. Pour étendre : voir gabarits-recipes.md (patron de recipe + processus issue/PR). `[T-1206]`