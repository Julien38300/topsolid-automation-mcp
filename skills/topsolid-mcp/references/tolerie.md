---
type: skill-reference
scope: topsolid-mcp
domain: tolerie
version: 1.0.0
created: 2026-09-30
sources:
  - server/src/Tools/RecipeTool.cs (recipes UNFOLDING M-58)
  - server/data/api/7.21.304.0/methods.json (IUnfoldings, IShapes)
  - data/chm-7.21/extracted-text/TopSolidDesign_Automation_Guide.txt (lu integralement — 0 hit unfold/bend/sheet/fold)
  - vault V8/Docs/Analyse_Echecs_CAD.md (algorithmes de tolerie fermes IP)
---

# Tôlerie via l'API Automation — état des lieux vérifié

Ce qui est sourcé : la mécanique de lecture unfolding (recipes + interface IUnfoldings 7.21) et
l'absence d'API de pliage. Ce qui ne l'est PAS : la méthodologie produit de pliage (sens du pli,
surépaisseurs, K-factor) — le guide CHM Design Automation 7.21 (1 121 lignes, lu intégralement) ne
contient AUCUNE mention de unfold/bend/sheet/fold (comptages : 0 partout), et l'API 7.21 n'expose pas
de création de plis. Toute règle de conception ci-dessous non sourcée est marquée `[A VALIDER]`.

## 1. Le document unfolding

- Un *unfolding* = document TopSolid de mise à plat (flat pattern) généré depuis une pièce de tôlerie
  pliée : il porte les dimensions dépliées utilisées par la découpe. Detection : recipe detect_unfolding,
  implémentation verifiée : TopSolidDesignHost.Unfoldings.IsUnfolding(docId) (try/catch car une
  exception est levee si le document n'est pas un unfolding). `[REC] [API-7.21]`
- Un unfolding reférence sa pièce source : Unfoldings.GetPartToUnfold(docId, out partDoc, out repId,
  out shapeId) — 3 sorties (document piece, representation, forme) ; le nom PDM de la piece source
  s'obtient par Pdm.GetName(Documents.GetPdmObject(partDoc)). Le recipe detect_unfolding affiche
  "Source part: ..." à partir de ces identifiants. `[REC]`
- Propriétés système lues par read_unfolding_dimensions : filtre sur les FriendlyName contenant
  "Unfolding" ou égaux à "Sheet Metal", "Thickness", "Bends Number", "Unfoldable Shape". Types melanges
  gérés par la recipe : Real (Area/Perimeter/Width/Length convertis en mm, autres valeurs en SI brut),
  Boolean, Integer. Ces noms sont les libellés système ; leur traduction utilisateur peut différer.
  `[REC]`

## 2. Plis — lecture vérifiée

- read_bend_features : GetBendFeatures(docId, out List<BendFeature>) — type complet verbatim
  TopSolid.Cad.Design.DB.Documents.BendFeature. Champs utilisés par la recipe : b.Angl (radian, *180/PI
  pour afficher en degrés), b.Radius (m, *1000 -> mm), b.Length (m, *1000 -> mm). Format de sortie : une
  ligne par pli "Pli: angle=xx.xdeg, radius=xx.xxmm, length=xx.xxmm". `[REC]`
- Rappels unités : l'API fournit radian/metre ; toute lecture humaine doit convertir
  (deg = rad*180/PI ; mm = m*1000). Les recipes le font deja — ne pas re-convertir une valeur
  affichée en mm. `[REC] [CHM-D-unites]`
- Nombre de plis : propriété système "Bends Number" (Integer) lisible par read_unfolding_dimensions ;
  verifier la coherance bends.Count (read_bend_features) == Bends Number (propriete). `[REC]`
  `cross-check recipe : regle de bonne pratique, pas une obligation API`

## 3. Ce que l'API NE fait PAS (tôlerie)

- Aucune methode Fold/Bend/Unfold dans IShapes 7.21 (scan methods.json : zero hit sur fold/bend/unfold
  hors IUnfoldings lecture). La creation/mofification de plis par automation N'EXISTE PAS dans
  l'exposable verifié. `[API-7.21]`
- Conséquence serveur MCP : la tôlerie se pilote par GABARITS — une piece de tôlerie type est modelée à
  la main dans TopSolid (plis, déroulement), puis l'agent : ouvre le gabarit, modifie des paramètres
  (épaisseur, cotes du développé), reconstruit, exporte le flat pattern (export_dxf). Pas de création
  A-Z. `[T-1206]`
- Le recipe read_unfolding_dimensions lit des system properties, pas des paramètres utilisateur :
  "Sheet Metal", "Thickness", Area, Perimeter y sont exposés en lecture seule. L'écriture d'épaisseur
  n'a PAS de recipe dédiée et passe par le gabarit ou l'UI. `[REC] [A VALIDER : voie d'écriture]`

## 4. Pièges — règles de conception déclarées par les sources, non détaillées

Les points ci-dessous sont des RAPPELS DE BONNES PRACTIQUES génériques du métier, PAS des règles
TopSolid vérifiées. Chacun doit être confronté à la pratique TopSolid Steel/tôlerie du site avant usage
automatisé. `[A VALIDER]`

- Sens du pli [A VALIDER] : l'angle d'un BendFeature est mesuré sur le pli tel que l'API le renvoie
  (Angle*180/PI) ; la convention TOPSOLID (angle intérieur/extérieur, pli vers le haut/bas par rapport
  au flat pattern) n'est pas documentée dans les sources lues. Ne jamais supposer une convention —
  comparer un pli connu d'une pièce de référence.
- Surépaisseurs/K-factor [A VALIDER] : le développé calculé par TopSolid dépend du facteur de pliage
  configuré sur la pièce (pas exposé dans les propriétés listées par la recipe). Aucun calcul de
  longueur développée par l'agent : TOUJOURS lire read_unfolding_dimensions (Area/Perimeter) au lieu
  de recalculer l/dépliée par une formule K ou V pli maison.
- Ordre des plis [A VALIDER] : l'ordre (séquence de pliage) n'est pas exposé par GetBendFeatures
  (liste plate Angle/Radius/Length). Ne pas construire une séquence d'usinage sans la valider.
- Sketchs pliables [A VALIDER] : aucune source lue ne décrit le comportement d'un sketch utilisé
  dans une pièce de tôlerie après pliage (coïncidence flat/plié, référence au pli). Ne pas écrire de
  recipe C# qui assume un mapping sketch->pli sans vérification runtime (get_item_last_operation_name
  sur la face pliée pour remonter l'opération productrice).

## 5. Chaîne de recettes tôlerie recommandée (toutes vérifiées)

1. detect_unfolding — confirmer le type du document `[REC]`
2. read_bend_features — inventaire des plis (angle/rayon/longueur, mm-deg convertis) `[REC]`
3. read_unfolding_dimensions — épaisseur + Area/Perimeter/Width/Length du développé `[REC]`
4. read_mass_volume / read_bounding_box — masse et encombrement plié `[REC]`
5. si modification de gabarit : set_real_parameter sur les paramètres du GABARIT source (pas de
   l'unfolding), rebuild_document, puis re-lectures 2-4 `[REC]`
6. export_dxf (RD) — mise à plat vers CAM/découpe `REC`
7. export_step — remontée CAO si besoin (batch_export_step pour un projet entier) `[REC]`

## 6. Rêve d'évolution serveur (proposition, hors scope recettes existantes)

- Un future "analyze_unfolding" pourrait croiser : liste des plis + Bends Number + Thickness +
  Area/Perimeter + bounding box dépliée + source part, en un appel unique. À traiter comme toute
  proposition de recipe : issue + PR avec test (voir gabarits-recipes.md pour le patron). N'existe PAS
  aujourd'hui : ne pas appeler. `[REC-aucune-trace]`
- Les algorithmes de pliage restent un noyau ferme IP (Analyse_Echecs_CAD.md liste explicitement
  "Algorithmes de tôlerie fermés (IP propriétaire)" dans le noyau Parasolid/TopSolid). Les règles de
  calcul de développé ne seront donc JAMAIS exposées par API : la bonne stratégique agent = lire les
  résultats (dimensions, plis), jamais réimplémenter le calcul. `[V8-AEC]`