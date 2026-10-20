---
name: topsolid-railing-design
version: 1.3.0
author: Noemid-H
license: internal
metadata:
  hermes:
    tags: [topsolid, railing, garde-corps, serrurerie, metallerie, cad, nf-p01-012, platine, extruded-bar, s235, inox]
    related_skills: [topsolid-mcp, topsolid-mcp-modeling]
    trigger_phrases: ["garde-corps", "garde corps", "railing", "rambarde", "balustrade", "barreaudage", "main courante"]
description: Use when designing and modeling railings / guardrails (garde-corps) in TopSolid from photos, sketches, or specifications — structured visual inspection, French standards (NF P01-012 / NF P01-013), TopSolid'Steel trade practices (100% MCP recipes create_extruded_bar & create_sheet_metal_plate, mandatory drilled baseplates, welded tronçon sub-assemblies with bolted splice posts, Z=0 floor convention, through-drilled infill rods, L/U corners), and 0 B-Rep collision verification.
---

# TopSolid-Railing-Design — Conception & Modélisation Métier de Garde-Corps

## When to Use

Utiliser ce skill dès que la demande concerne la **conception ou modélisation d'un garde-corps (rambarde, balustrade)** dans TopSolid, à partir d'une **photo**, d'un croquis ou d'un cahier des charges. Ce skill s'utilise **en parallèle du skill socle TopSolid MCP** (`topsolid-mcp` / `topsolid-mcp-modeling`).

---

## 1. Protocole d'Inspection Visuelle d'une Photo (Anti-« Téléphone Arabe »)

Dans Hermes, le modèle principal délègue l'analyse d'image à `vision_analyze`. Un prompt générique (*« Décris le garde-corps »*) produit une description vague qui invente des « tubes carrés » et oublie les platines de pied.

**RÈGLE OBLIGATOIRE** : Appeler `vision_analyze` en lui imposant explicitement cette **grille de relevé de dessinateur-projeteur en 7 points** :

1. **Implantation en plan (Topologie globale)** :
   - Le garde-corps est-il un linéaire droit, un **garde-corps en L** (retour d'angle à 90°), en **U**, ou rampant d'escalier ?
   - Quelle est son orientation par rapport au sol (ex. : branche principale parallèle aux lames de bois à droite + retour à 90° perpendiculaire aux lames au fond jusqu'au mur) ?
2. **Comptage exact des poteaux et travées par branche** :
   - Compter un par un les poteaux visibles sur la branche principale ET sur chaque branche en retour (ex. : *6 poteaux = 5 travées sur la branche droite ; 4 travées sur le retour du fond*).
3. **Étalonnage proportionnel sur $H_{\text{MC}} = 1000\text{ mm}$** :
   - En utilisant la hauteur réglementaire de la main courante ($1000\text{ mm}$) comme étalon sur l'image, estimer par proportion :
     - la largeur et l'épaisseur des montants,
     - le diamètre ou la section des lisses/barres,
     - l'épaisseur et la largeur de la main courante,
     - la hauteur de la zone basse resserrée.
4. **Morphologie réelle des montants (Poteaux)** :
   - Distinguer : **fer plat sur chant** (très fin vu de face, large perpendiculairement au garde-corps pour reprendre la poussée, ex. $50 \times 10\text{ mm}$), **double fer plat** ($2 \times 40 \times 10\text{ mm}$), **tube carré/rectangulaire** ($40 \times 40$), ou **tube rond** ($\varnothing 42{,}4$).
5. **Géométrie des platines de pied et boulonnerie (OBLIGATOIRE)** :
   - Pose **à la française** (sur dalle/platelage) ou **à l'anglaise** (en applique sur nez de dalle) ?
   - La platine est-elle **centrée** sous le poteau ou **déportée vers l'intérieur** (montant soudé au bord extérieur de la platine + 2 ou 4 trous/boulons du côté intérieur de la terrasse) ?
6. **Nature et rythme vertical du remplissage (Lisses / Barreaudage / Soubassement)** :
   - Section : **barres rondes pleines / tubes ronds** ($\varnothing 12\text{ ou }14\text{ mm}$), **câbles inox**, **fers plats**, ou **tôle/verre de soubassement** ?
   - **Rythme vertical (critique NF P01-012)** : Les lisses sont-elles équidistantes, ou **resserrées en partie basse ($0\text{–}450\text{ mm}$) et espacées en partie haute ($450\text{–}1000\text{ mm}$)** ? Compter exactement le nombre de barres dans la zone basse et dans la zone haute (ex. : **7 barres rondes serrées en bas + 2 barres rondes espacées en haut = 9 barres**).
   - Mode d'assemblage : les barres **traversent-elles les montants** (trous alésés dans le fer plat) ou sont-elles fixées en applique ?
7. **Matériaux et Contrastes Visuels (Bi-matière)** :
   - Identifier précisément les matériaux visibles : ex. ossature (montants + platines + main courante) en **Acier S235 peint noir mat (RAL 9005)**, barres filantes et boulonnerie en **Acier Inoxydable 304/A4 brossé (gris clair métallisé)**, platelage en **Bois**.

---

## 2. Règle de Cadrage avec l'Utilisateur (Zéro question de politesse)

1. **INTERDICTION ABSOLUE** de poser des questions dont la réponse se voit sur l'image (ne jamais demander *« Pose à la française ou à l'anglaise ? »* quand on voit la platine vissée sur le bois) ni des questions génériques inutiles (*« Quel livrable TopSolid veux-tu ? »*).
2. **Exposer d'abord le relevé technique** extrait de la photo (topologie droite/L, nombre de travées, sections, platines, rythme 7+2, matériaux).
3. **Ne poser de question QUE s'il existe un vrai choix de modélisation ou une incertitude réelle hors-image** :
   - *Choix de périmètre* : modéliser l'ensemble complet en L (ex. $5 + 4$ travées avec terrasse) ou uniquement un tronçon / une travée type ?
   - *Cotes d'implantation hors-photo* : si l'utilisateur a des longueurs exactes de terrasse à respecter (sinon appliquer l'entraxe standard serrurerie $1000\text{ à }1100\text{ mm}$ par travée).
   - *Niveau de détail quincaillerie* : modéliser les boulons H + rondelles sur les platines ou uniquement les platines percées ?

---

## 3. Règles de l'Art Métallerie & Pratiques TopSolid'Steel (Normes NF P01-012 / NF P01-013)

> **INTERDICTION ABSOLUE de faire des recherches Web (AFNOR, Légifrance, etc.)** pour vérifier la norme NF P01-012/013. Toutes les règles métier et TopSolid'Steel sont codifiées ci-dessous. **La fidélité visuelle à la photo prime toujours** (ne JAMAIS ajouter des panneaux de verre ou des tôles en soubassement s'ils ne sont pas présents sur la photo !).

### A. Convention Altimétrique TopSolid'Steel ($Z = 0{,}0\text{ mm}$)
- Dans TopSolid'Steel, le plan **$Z = 0{,}0\text{ mm}$ représente TOUJOURS le niveau fini du sol / dessus de dalle ou dessus du platelage bois**.
  - **Platelage / Dalle** : situé sous $Z = 0$ (ex. lames de terrasse de $Z = -22\text{ mm}$ à $Z = 0\text{ mm}$, lambourdes de $Z = -72\text{ mm}$ à $Z = -22\text{ mm}$).
  - **Pose à la française (sur platelage/dalle)** :
    - La **platine de fixation (`GC_Platine.TopPrt`)** repose directement sur le sol fini : **$Z \in [0\text{ mm}, +10\text{ mm}]$** (épaisseur standard $e_{\text{platine}} = 10\text{ mm}$).
    - Le **montant (`GC_Montant.TopPrt`)** est soudé **SUR le dessus de la platine** : il démarre à **$Z = +10\text{ mm}$** et monte jusqu'à **$Z = +990\text{ mm}$** (hauteur du fer plat $= 980\text{ mm}$). **Un montant ne doit JAMAIS flotter en l'air ni traverser la platine jusqu'à $Z=0$ sans platine !**
    - La **main courante (`GC_MainCourante.TopPrt`)** repose sur le sommet des montants : **$Z \in [+990\text{ mm}, +1000\text{ mm}]$** (hauteur finie réglementaire $H = 1000\text{ mm}$).
  - **Pose à l'anglaise (en applique sur nez de dalle)** :
    - La platine verticale descend sous $Z = 0$ ($Z \in [-125\text{ mm}, 0\text{ mm}]$) et est déportée du voile béton par 2 entretoises rondes $\varnothing 20\text{ mm}$.

### B. Platines de Pied Obligatoires (`GC_Platine.TopPrt`) — Zéro Poteau sans Platine !
Un garde-corps de métallerie comporte **obligatoirement une platine de fixation percée sous chaque poteau** :
- **Épaisseur standard** : **$10\text{ mm}$** (ou $12\text{ mm}$ en usage public fort ; minimum absolu $8\text{ mm}$). Désignation brute : `Plat 100x10` ou `Tôle ép.10 S235`.
- **Cas 1 — Platine déportée vers l'intérieur (terrasse bois / bord de dalle)** :
  - Dimensions : **$100\text{ mm}$ (dans le sens perpendiculaire au GC, parallèle au montant de $50\text{ mm}$) $\times 80\text{ mm}$ (largeur le long du GC) $\times 10\text{ mm}$ (épaisseur)**.
  - Implantation : le montant ($50 \times 10\text{ mm}$) est soudé sur la moitié extérieure de la platine ($50\text{ mm}$ côté vide), laissant une plage libre de $50 \times 80\text{ mm}$ côté intérieur terrasse.
  - Perçages d'ancrage : **2 trous traversants $\varnothing 12\text{ mm}$** (pour goujons/tirefonds M10 Inox A4) dessinés directement dans l'esquisse 2D de la platine côté intérieur terrasse (ex. à $25\text{ mm}$ du bord intérieur, entraxe $40\text{ mm}$ suivant la longueur du GC).
- **Cas 2 — Platine centrée 4 trous (dalle béton / mezzanine type `GC MEZZA`)** :
  - Dimensions : **$100 \times 110 \times 10\text{ mm}$** ou **$160 \times 140 \times 10\text{ mm}$**, avec **4 trous $\varnothing 12\text{ mm}$** (M10) ou **$\varnothing 14\text{ mm}$** (M12) aux 4 coins (retrait au bord $20\text{–}25\text{ mm}$).

### C. Montants et Remplissage Traversant (NF P01-012 / NF P01-013)
- **Orientation mécanique d'un poteau en fer plat ($50 \times 10\text{ mm}$)** :
  - La largeur ($50\text{ mm}$) est **toujours perpendiculaire à la lisse** (inertie maximale $I_y = \frac{10 \times 50^3}{12}$ face à la poussée horizontale NF P01-013).
  - L'épaisseur ($10\text{ mm}$) est orientée dans le sens du linéaire du garde-corps.
- **Cas type du barreaudage horizontal « 7 barres basses + 2 barres hautes » (sans tôle ni verre)** :
  - **Zone basse de sécurité ($0\text{ à }450\text{ mm}$)** : **7 barres rondes pleines Inox $\varnothing 12\text{ mm}$** (`NF A 45-003`), centres à **$Z = 60, 125, 190, 255, 320, 385, 450\text{ mm}$** (pas régulier de $65\text{ mm}$ → vide libre entre barres $= 53\text{ mm} \le 50\text{–}55\text{ mm}$ anti-escalade ; vide sous la 1re barre par rapport au platelage $Z=0$ : $60 - 6 = 54\text{ mm} \le 110\text{ mm}$, et $44\text{ mm}$ au-dessus de la platine $Z=10\text{ mm}$).
  - **Zone haute ($450\text{ à }1000\text{ mm}$)** : **2 barres rondes pleines Inox $\varnothing 12\text{ mm}$**, centres à **$Z = 630\text{ mm}$** et **$Z = 815\text{ mm}$** (vides libres de $168\text{ mm}$, $173\text{ mm}$ et $169\text{ mm} \le 180\text{ mm}$ réglementaires).
  - **Alésages traversants dans les montants** : les 9 trous **$\varnothing 13\text{ mm}$** (jeu radial de $0{,}5\text{ mm}$ autour des barres $\varnothing 12\text{ mm}$) sont dessinés directement via `CreateCircleSegment` dans l'esquisse 2D latérale du montant ($50 \times 980\text{ mm}$, de $Z=10$ à $Z=990\text{ mm}$) avant `CreateExtrudedShape` !
- **Gestion propre de l'angle à 90° (Garde-corps en L)** :
  - À la jonction des deux branches (Branche 1 selon $X$, Branche 2 selon $Y$), disposer un poteau en fin de Branche 1 et un poteau en début de Branche 2 décalés de $50\text{ à }60\text{ mm}$ de l'arête d'angle (chacun avec sa platine sans chevauchement B-Rep !), et arrêter les barres de chaque branche à $+20\text{ mm}$ après leur dernier poteau respectif pour garantir **0 collision B-Rep** dans l'angle.

### D. Structuration Modulaire par Tronçons Soudés & Poteaux de Raccord / Jonction
- **ZÉRO pièce multi-corps fourre-tout** : chaque composant distinct est un `.TopPrt` unitaire (`GC_Platine_150x100x12`, `GC_Platine_Jonction_70x100x12`, `GC_Montant_50x10`, `GC_Montant_Jonction_50x10`, `GC_MainCourante_*`, `GC_LisseBasse_*`, `GC_Barreau_*`).
- **Découpage en Tronçons Soudés (`GC_Troncon_*.TopAsm`) & Poteaux de Raccord Boulonnés** :
  - Un garde-corps réel est fabriqué en atelier par **tronçons soudés transportables** (longueur typique $1{,}5\text{ à }2{,}5\text{ m}$), chaque tronçon étant un **sous-assemblage TopSolid (`.TopAsm`)** regroupant ses platines, montants, main courante, lisses et barreaux soudés ensemble.
  - Dans 99 % des cas, la jonction entre deux tronçons adjacents se fait par un **poteau de raccord (jonction)** constitué de **2 fers plats boulonnés dos à dos** ($2 \times 50 \times 10\text{ mm}$ percés de $3 \times \varnothing 12\text{ mm}$ pour boulons M10, avec un jeu de pose de $2\text{ mm}$ entre les deux plats et chacune sa demi-platine `GC_Platine_Jonction_70x100x12`) : chaque fer plat et sa demi-platine appartiennent à leur sous-assemblage de tronçon respectif !
- **Modélisation 100 % via Recettes MCP (`topsolid_run_recipe`)** :
  - Toujours utiliser **`create_extruded_bar`** pour tous les profilés (`PLAT`, `TUBE_RECT`, `ROND`, `TUBE_ROND`, `CORNIERE`) : la recette câble automatiquement la fonction système TopSolid **`Extruded Bar.TopPrtFct`**, son repère **`Repere_ExtrudedBar_XY_Z`** (section dans `XY` avec `X` horizontal, extrusion longitudinale suivant `+Z`) et la propriété système **`Length` = `L`** visible en nomenclature BOM.
  - Toujours utiliser **`create_sheet_metal_plate`** pour toutes les tôles et platines percées (contour extérieur + cercles des trous dans la même esquisse 2D + paramètre `Epaisseur`).
  - Toujours utiliser **`create_assembly_document`** + **`batch_add_inclusions`** (mode `BETWEEN,x1,y1,z1,x2,y2,z2,ux,uy,uz,nomOcc` pour orienter automatiquement le `+Z` des profilés de `P1` vers `P2` et leur section `X` selon `uX`) + **`update_where_used_and_save`** + **`check_collisions`**.

---

## 4. Affectation Générique Ultra-Rapide des Matériaux Métier (`< 500 ms`, Zéro ID en dur)

Ne **JAMAIS** coder en dur d'identifiant PDM spécifique à une machine, et ne **JAMAIS** parcourir les 55 bibliothèques du PDM sans filtre (les bibliothèques de visserie type *Emile Maurin* contiennent des milliers de composants et provoquent un timeout de 60 s !).
En métallerie, rechercher directement les désignations normalisées (`*S235*`, `*S355*`, `*304*`, `*CrNi*`, `*Chêne*`, `*Pin*`) **en filtrant uniquement les bibliothèques de matériaux** et en vérifiant que l'extension PDM est **`.TopMat`** (pour exclure les `.TopLam` stratifiés bois qui lèvent une exception de cast) :

```csharp
// Helper C# générique (< 500 ms sur n'importe quel TopSolid) — à placer HORS StartModification :
List<PdmObjectId> allLibs = TopSolidHost.Pdm.GetProjects(false, true);
List<PdmObjectId> matLibs = new List<PdmObjectId>();
foreach (PdmObjectId lib in allLibs)
{
    string ln = TopSolidHost.Pdm.GetName(lib);
    if (ln.IndexOf("CEN", StringComparison.OrdinalIgnoreCase) >= 0 ||
        ln.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 ||
        ln.IndexOf("Mat", StringComparison.OrdinalIgnoreCase) >= 0 ||
        ln.IndexOf("Bois", StringComparison.OrdinalIgnoreCase) >= 0 ||
        ln.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        matLibs.Add(lib);
    }
}

Func<string[], DocumentId> findMaterial = (string[] patterns) =>
{
    foreach (string pat in patterns)
    {
        foreach (PdmObjectId lib in matLibs)
        {
            List<PdmObjectId> hits = TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, pat);
            if (hits != null)
            {
                foreach (PdmObjectId h in hits)
                {
                    string ext;
                    TopSolidHost.Pdm.GetType(h, out ext);
                    if (string.Equals(ext, ".TopMat", StringComparison.OrdinalIgnoreCase))
                        return TopSolidHost.Documents.GetDocument(h);
                }
            }
        }
    }
    return DocumentId.Empty;
};

// Désignations métier standards (Acier de construction S235/S355, Inox austénitique 304/316/CrNi, Bois) :
DocumentId matSteel = findMaterial(new string[] { "*S235*", "*S275*", "*S355*", "*Fe E*" });
DocumentId matInox  = findMaterial(new string[] { "*X2CrNi19-11*", "*X5CrNi18-10*", "*304*", "*316*", "*CrNi*" });
DocumentId matWood  = findMaterial(new string[] { "*Chêne*", "*Chene*", "*Pin*", "*Mélèze*", "*Oak*", "*Sapin*" });
```

Puis, **à l'intérieur du `StartModification` de chaque pièce `.TopPrt`** (après `EnsureIsDirty(ref dId)`) :
```csharp
if (!matDocId.IsEmpty)
{
    TopSolidDesignHost.Parts.SetMaterial(dId, matDocId);
    TopSolidDesignHost.Parts.SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0));
    TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(dId, true);
}
```

---

## 5. Gate de Contrôle Qualité avant Livraison

Avant d'annoncer à l'utilisateur que le garde-corps est terminé, vérifier dans le rapport du script One-Shot :
1. **Présence des platines percées** : chaque poteau possède sa platine ($10\text{ mm}$ d'épaisseur, percée de 2 ou 4 trous $\varnothing 12\text{ mm}$) posée à $Z \in [0, +10\text{ mm}]$, et chaque montant démarre à $Z = +10\text{ mm}$.
2. **Zéro collision B-Rep** (`ShapeShapeCollisionEntity == 0`) dans l'assemblage `.TopAsm` (ni entre barres et montants, ni entre platines d'angle, ni entre montants et platines).
3. **Matériaux PDM réels affectés** sur 100 % des pièces (`Parts.GetMaterial(docId)` non vide : `S235JR` sur montants/platines/MC, `Inox 304/X2CrNi19-11` sur barres, `Bois` sur terrasse).
4. **Budget d'exécution respecté** : 1 seul script C# Builder compilé du premier coup (`topsolid_compile`) et exécuté en `< 20 s` (`topsolid_execute_script`).
