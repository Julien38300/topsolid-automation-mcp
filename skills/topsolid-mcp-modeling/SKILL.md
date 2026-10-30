---
name: topsolid-mcp-modeling
version: 2.8.0
author: Noemid-H
license: internal
metadata:
  hermes:
    tags: [topsolid, mcp, cad, assemblage, stl, verification-perceptive, recipes, extruded-bar, sheet-metal, collisions, where-used, circuit-breaker]
    related_skills: [topsolid-mcp, topsolid-railing-design, topsolid-api-knowledge, topsolid-cage-from-image]
description: Use when modeling TopSolid via the MCP bridge, including creating or replacing turned-part profiles, extruded bars, sheet metal, assemblies, and native B-Rep collision checks.
---

# Modélisation & Bonnes Pratiques Utilisateur TopSolid via MCP (LY458 :8080)

> **Skill vivant** : Ce skill centralise les pratiques utilisateur TopSolid et la modélisation via le pont MCP. Chaque nouveau piège ou apprentissage issu des tests MCP doit y être immédiatement ajouté et synchronisé.
> **Skills métiers spécialisés à charger en parallèle selon l'ouvrage** :
> - **Garde-corps / rambardes / balustrades** : charger obligatoirement **`topsolid-railing-design`** (`TopSolid-Railing-Design`) avant toute analyse d'image ou conception.
> - **Cages SubD depuis photo** : charger **`topsolid-cage-from-image`**.

## When to Use

Use when creating, modifying, or inspecting TopSolid geometry through the JSON-RPC bridge on LY458
(port 8080) — metalwork extruded bars (`create_extruded_bar`), sheet metal / drilled plates (`create_sheet_metal_plate`),
part/assembly creation (`create_part_document`, `create_assembly_document`), positioned inclusions (`batch_add_inclusions`),
turned parts (`create_revolved_part`, `revolve_active_part`), Where-Used (`cas d'emploi`) propagation (`update_where_used_and_save`), native TopSolid B-Rep collision checks (`check_collisions`), and visual verification gates.

## Reference-image modeling intake

Before changing an existing model, inspect its active document, shape count, and feature tree. Decide which features must be preserved, replaced, or removed before writing to PDM. For an image without dimensions, state the estimated overall height, maximum diameter, opening, wall thickness, and any assumptions; ask for source dimensions when they materially affect fit or function. Distinguish smooth surfaces from ribs or other texture rather than inferring that detail from a document name.

Classe de travail : analyser finement le besoin/photo (avec le skill métier spécialisé), ne poser de question que sur les vrais choix ou doutes, puis **privilégier en priorité absolue les Recettes MCP de création (`topsolid_run_recipe`)** — ou un script One-Shot C# uniquement si une géométrie complexe sort du cadre des recettes — avec vérification native des collisions B-Rep TopSolid (`check_collisions`). Cible : **< 90 secondes et < 20 000 tokens**.

## Règles permanentes & Pratiques Utilisateur TopSolid

0. **Cycle de vie PDM TopSolid : Modification d'un sous-document & Mise à jour des Cas d'Emploi (`Where-Used`) avant Save** :
   - Dans TopSolid, dès qu'on modifie une pièce (`.TopPrt`) ou un sous-assemblage déjà utilisé dans un ou plusieurs assemblages/plans parents, `EnsureIsDirty(ref childDocId)` crée une nouvelle révision mineure.
   - **Règle absolue d'utilisateur TopSolid** :
     1. Sauvegarder d'abord le sous-document modifié (`TopSolidHost.Pdm.Save(childPdm, true)`),
     2. Rechercher et ouvrir ses **cas d'emploi** (`TopSolidHost.Pdm.GetWhereUsed` / recette `read_where_used` / assemblages parents ouverts),
     3. Passer chaque assemblage parent en modification (`StartModification` → `EnsureIsDirty(ref parentDocId)` → `EndModification(true, true)`) pour mettre à jour ses inclusions sur la nouvelle révision de l'enfant et recalculer les collisions,
     4. Sauvegarder les parents **en dernier** (**ordre strict : Enfant → Sous-Assemblage → Assemblage Général → Mise en plan**).

1. **Étape 0 — Analyse visuelle fine & Questions UNIQUEMENT en cas de vrai doute ou choix de conception** :
   - **INTERDICTION de poser des questions "de politesse" ou des questions dont la réponse est visible sur la photo** (ex. ne JAMAIS demander « pose à la française ou à l'anglaise ? » quand on voit les platines vissées sur le sol, ni demander quel livrable TopSolid produire).
   - **Extraire 100 % de ce qui est visible sur l'image** : géométrie globale (linéaire droit vs retour en L/U, nombre de travées visibles), forme réelle des profilés (fer plat vs tube carré vs barre ronde), rythme réel du remplissage (ex. 7 barres rondes serrées en partie basse + 2 barres espacées en partie haute), géométrie des platines (centrée vs déportée) et contrastes de matériaux (ex. structure acier peint noir + barres en inox brossé + platelage bois).
   - **Appliquer d'office les normes et règles de l'art** pour les cotes standards non cotées (hauteur main courante $1000\text{ mm}$, entraxe poteaux courant $1000\text{ à }1200\text{ mm}$, platines percées $10\text{ mm}$ sous chaque poteau, NF P01-012).
   - **N'interroger l'utilisateur QUE s'il existe un vrai choix de modélisation ou une incertitude réelle**. Si tout est clair, attaquer directement.

2. **Discipline de Raisonnement (Anti-Monologue CoT) & Client CLI Prêt à l'Emploi** :
   - **ZÉRO TABLEAU DE COORDONNÉES DANS LE `reasoning` (CoT)** : Il est **STRICTEMENT INTERDIT** de calculer à la main les coordonnées $(X, Y, Z)$ de chaque poteau, lame ou barre dans ta réflexion interne (ce qui gaspille 4 à 5 minutes de génération de texte !). Pose uniquement les équations paramétriques (origine, pas, nombre) en moins de 15 lignes de réflexion et laisse les boucles `for` du script C# calculer les coordonnées !
   - **ZÉRO RÉÉCRITURE DE `mcp_client.py`** : Utilise directement l'utilitaire permanent `python N:\Noemid_System\hermes\scripts\topsolid_mcp_cli.py compile mon_script.cs` et `python N:\Noemid_System\hermes\scripts\topsolid_mcp_cli.py exec mon_script.cs` (ou `state`, `recipe <nom> [val]`) sans jamais perdre 1 minute à réécrire un client HTTP JSON-RPC.

3. **Les 4 Règles « Compile-First » C# (Zéro erreur `CS0029`, `CS0019`, `CS1503` au 1er essai)** :
   1. **`CreateSketchIn3D` retourne `ElementId` (JAMAIS `ElementItemId` !)** : Seuls les éléments internes d'esquisse (`CreateVertex`, `CreateLineSegment`, `CreateCircleSegment`) retournent un `ElementItemId`. L'esquisse elle-même est un `ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(...)`.
   2. **AUCUN opérateur `==` ou `!=` sur `PdmObjectId`, `DocumentId`, `ElementId` (`CS0019`)** : Ces 3 types sont des `struct` sans opérateur `==`/`!=`. Toujours utiliser **`!id.IsEmpty`**, **`id.IsEmpty`** ou **`id1.Equals(id2)`**.
   3. **`DocumentId` vs `PdmObjectId` (`CS1503`)** : `Parts.SetMaterial(partDocId, matDocId)` et `Assemblies.CreateInclusion(asmDocId, posId, name, childDocId, ...)` prennent obligatoirement des **`DocumentId`** (obtenus via `TopSolidHost.Documents.GetDocument(pdmId)`), jamais des `PdmObjectId`.
   4. **Recherche Générique de Matériaux PDM en `< 500 ms` (Zéro Timeout, Zéro Crash `.TopLam`)** :
      - Ne **JAMAIS** appeler `SearchProjectByName("Mécanique CEN TopSolid")` (une requête multi-mots retourne 0 résultat), et ne **JAMAIS** parcourir les 55 bibliothèques de `GetProjects(false, true)` sans filtre (les bibliothèques de visserie font 60 s de timeout).
      - Filtrer `GetProjects(false, true)` uniquement sur les bibliothèques dont le nom contient `"CEN"`, `"Metal"`, `"Mat"`, `"Bois"`, `"Wood"`, vérifier que `TopSolidHost.Pdm.GetType(h, out ext)` vaut **`".TopMat"`** (sinon un `.TopLam` stratifié bois lève `InvalidCastException: LaminateMaterialDocument`), et activer `SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0))` + `SetPhysicalPropertiesManagementRefreshAuto(dId, true)`.

4. **Workflow Prioritaire « 100 % Recettes MCP » (`topsolid_run_recipe`) pour la Métallerie & l'Assemblage** :
   - **Toujours utiliser les recettes MCP natives avant d'écrire du C# sur mesure** :
     1. **`create_project_tree`** : `nom_projet;01_Environnement,02_Composants_GC,03_Assemblages`
     2. **`create_sheet_metal_plate`** (tôles, platines percées, dalles/murs) :
        `nom_doc;designation;reference;dossier;Lx_mm;Wy_mm;Ep_mm;x1,y1,d1|x2,y2,d2;S235JR;RAL 7016,56,62,66;CENTER`
     3. **`create_extruded_bar`** (profilés de métallerie : `PLAT`, `TUBE_RECT`, `ROND`, `TUBE_ROND`, `CORNIERE` avec fonction système **`Extruded Bar.TopPrtFct`**, repère **`XY` section / `X` horizontal / `+Z` longitudinal**, paramètre système **`Length` = `L`**, alésages transversaux optionnels `zPos,offX,diam`, matériau `.TopMat` et revêtement `.TopCot`) :
        `nom_doc;designation;reference;dossier;PLAT;50,10;988;98,0,17|528,0,17;S235JR;RAL 7016,56,62,66`
     4. **`create_part_document`** / **`create_assembly_document`** : crée un `.TopPrt` ou `.TopAsm` avec ses propriétés PDM.
     5. **`batch_add_inclusions`** : assemble en une seule transaction toutes les pièces d'un sous-assemblage soudé (`GC_Troncon_*`) ou de l'assemblage général, soit par coordonnées `(tx,ty,tz,rx,ry,rz,nomOcc)`, soit par vecteur directeur **`BETWEEN,x1,y1,z1,x2,y2,z2,ux,uy,uz,nomOcc`** (qui aligne automatiquement le `+Z` longitudinal du profilé `Extruded Bar` de `P1` vers `P2` et l'axe `X` de sa section selon `uX`).
     6. **`update_where_used_and_save`** : reconstruit et sauvegarde en bottom-up (`Pièces .TopPrt -> Sous-Assemblages .TopAsm -> Assemblage Général .TopAsm`).
     7. **`check_collisions`** : active `SetCollisionsManagement`, compte les `ShapeShapeCollisionEntity` B-Rep et restaure `ModelingStage`.
   8. **`create_revolved_part`** : creates a new `.TopPrt` from an ordered `radius_mm,axial_mm` profile, with an optional angle, designation, and reference.
   9. **`revolve_active_part`** : adds the same feature to the active empty `.TopPrt` and refuses to proceed when it already contains a solid.
   10. **`replace_revolved_profile`** : replaces a single revolve using explicit line and circular-arc segments; refuses additional solid features to avoid deleting dependent geometry.
   - **Fallback « One-Shot C# Builder »** : uniquement si une géométrie gauche ou opération spéciale n'est pas couverte par les 208 recettes. Dans ce cas, écrire 1 seul script C# complet compilé via `topsolid_compile` et exécuté via `topsolid_execute_script`.

5. **COUPE-CIRCUIT (CIRCUIT-BREAKER) & 5 INTERDICTIONS ABSOLUES** :
   - **COUPE-CIRCUIT IMMÉDIAT SUR PANNE INFRASTRUCTURE / SERVEUR MCP** : Si `topsolid_compile` ou `topsolid_execute_script` renvoie une erreur système interne, **ARRÊT IMMÉDIAT** (interdiction de lire le code source du serveur MCP, de bricoler un exécutable ou d'ouvrir WinRM).
   - **ZÉRO PIÈCE MULTI-CORPS « FOURRE-TOUT »** : 1 fichier `.TopPrt` par référence unitaire distincte, instancié $N$ fois dans `.TopAsm`.
   - **ZÉRO CRAWL WEB NORMATIF** : tout est dans `topsolid-railing-design`.
   - **JAMAIS `TopSolidHost.Documents.Update(docId, true)` à l'intérieur d'un `StartModification` ouvert** (deadlock 60 s).
   - **JAMAIS `GetShapeVolume(shapeId)` avant `EndModification(true, true)`** et **JAMAIS de scripts Python pour fouiller `methods.json` ou `types.json`**.

6. **Étapes (Stages) TopSolid & Détection de collisions native B-Rep** :
   - Quand on appelle `SetCollisionsManagement(asmId, designRep, true, true, true)`, TopSolid passe en **étape d'Analyse** (`ShapeShapeCollisionEntity`).
   - Pour ajouter ou modifier des éléments après une analyse, **revenir en étape de Modélisation À L'INTÉRIEUR d'un `StartModification`** :
     `TopSolidHost.Operations.SetWorkingStage(TopSolidHost.Operations.GetModelingStage(docId)); TopSolidHost.Operations.ResetInsertionOperation(docId);`

## Références
- `references/bottom-up-recettes.md` — **LIRE IMMÉDIATEMENT AVANT D'ÉCRIRE DU C#** : patron complet « One-Shot Builder » C#, CLI `topsolid_mcp_cli.py`, helper générique matériaux `< 500 ms`, esquisses 3D (`inDefinesXDirection = true` + `CleanSketch()`), `CreateExtrudedShape`, `CreateInclusion` (16 arguments), détection native des collisions `ShapeShapeCollisionEntity`.
- `scripts/verify_stl.py` — segmentation manifold des solides, détection de collisions 3D AABB et rendu PNG multi-vues (`iso`, `face`, `profil`).
