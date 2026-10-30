---
name: topsolid-mcp-modeling
version: 2.7.0
author: TopSolid-MCP-Contributors
license: internal
metadata:
  hermes:
    tags: [topsolid, mcp, cad, assemblage, stl, verification-perceptive, recipes, extruded-bar, profiles-721, sheet-metal, collisions, where-used, circuit-breaker]
    related_skills: [topsolid-mcp, topsolid-railing-design, topsolid-api-knowledge, topsolid-cage-from-image]
description: Use when modeling TopSolid via the MCP bridge (TopSolid 7.21 native library profiles CreateProfileByTwoPoints, 100% MCP Recipe-First workflow for Extruded Bars, Sheet Metal Plates & Assemblies, Chronology & IsInvalid discipline, Circuit-Breaker & Token Discipline, CoT Reasoning Budget, Compile-First C# Rules, Generic <500ms Material Lookup, Where-Used update before save, Native B-Rep Collision Check).
---

# Modélisation & Bonnes Pratiques Utilisateur TopSolid via MCP (TopSolid 7.20 / 7.21)

> **Skill vivant** : Ce skill centralise les pratiques utilisateur TopSolid et la modélisation via le pont MCP. Chaque nouveau piège ou apprentissage issu des tests MCP doit y être immédiatement ajouté et synchronisé.
> **Skills métiers spécialisés à charger en parallèle selon l'ouvrage** :
> - **Garde-corps / rambardes / balustrades** : charger obligatoirement **`topsolid-railing-design`** (`TopSolid-Railing-Design`) avant toute analyse d'image ou conception.
> - **Cages SubD depuis photo** : charger **`topsolid-cage-from-image`**.

## When to Use

Use when creating, modifying, or inspecting TopSolid geometry through the JSON-RPC MCP bridge
— metalwork extruded bars (`create_extruded_bar`), sheet metal / drilled plates (`create_sheet_metal_plate`),
part/assembly creation (`create_part_document`, `create_assembly_document`), positioned inclusions (`batch_add_inclusions`),
Where-Used (`cas d'emploi`) propagation (`update_where_used_and_save`), native TopSolid B-Rep collision checks (`check_collisions`), and visual verification gates.

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
   - **ZÉRO RÉÉCRITURE DE `mcp_client.py`** : Utilise directement les outils MCP (`topsolid_compile`, `topsolid_execute_script`, `topsolid_get_state`, `topsolid_run_recipe`) ou l'utilitaire CLI `topsolid_mcp_cli.py` sans jamais perdre 1 minute à réécrire un client HTTP JSON-RPC.

3. **Les 4 Règles « Compile-First » C# (Zéro erreur `CS0029`, `CS0019`, `CS1503` au 1er essai)** :
   1. **`CreateSketchIn3D` retourne `ElementId` (JAMAIS `ElementItemId` !)** : Seuls les éléments internes d'esquisse (`CreateVertex`, `CreateLineSegment`, `CreateCircleSegment`) retournent un `ElementItemId`. L'esquisse elle-même est un `ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(...)`.
   2. **AUCUN opérateur `==` ou `!=` sur `PdmObjectId`, `DocumentId`, `ElementId` (`CS0019`)** : Ces 3 types sont des `struct` sans opérateur `==`/`!=`. Toujours utiliser **`!id.IsEmpty`**, **`id.IsEmpty`** ou **`id1.Equals(id2)`**.
   3. **`DocumentId` vs `PdmObjectId` (`CS1503`)** : `Parts.SetMaterial(partDocId, matDocId)` et `Assemblies.CreateInclusion(asmDocId, posId, name, childDocId, ...)` prennent obligatoirement des **`DocumentId`** (obtenus via `TopSolidHost.Documents.GetDocument(pdmId)`), jamais des `PdmObjectId`.
   4. **Recherche Générique de Matériaux PDM en `< 500 ms` (Zéro Timeout, Zéro Crash `.TopLam`)** :
      - Ne **JAMAIS** appeler `SearchProjectByName("Mécanique CEN TopSolid")` (une requête multi-mots retourne 0 résultat), et ne **JAMAIS** parcourir les 55 bibliothèques de `GetProjects(false, true)` sans filtre (les bibliothèques de visserie font 60 s de timeout).
      - Filtrer `GetProjects(false, true)` uniquement sur les bibliothèques dont le nom contient `"CEN"`, `"Metal"`, `"Mat"`, `"Bois"`, `"Wood"`, vérifier que `TopSolidHost.Pdm.GetType(h, out ext)` vaut **`".TopMat"`** (sinon un `.TopLam` stratifié bois lève `InvalidCastException: LaminateMaterialDocument`), et activer `SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0))` + `SetPhysicalPropertiesManagementRefreshAuto(dId, true)`.

4. **Workflow Prioritaire « 100 % Recettes MCP » (`topsolid_run_recipe`) pour la Métallerie & l'Assemblage (TopSolid 7.21)** :
   - **Toujours utiliser les recettes MCP natives avant d'écrire du C# sur mesure** :
     1. **`create_project_tree`** : `nom_projet;01_Composants,02_Sous_Assemblages,03_Assemblage_General`
     2. **`create_sheet_metal_plate`** (tôles, platines percées, dalles/murs) :
        `nom_doc;designation;reference;dossier;Lx_mm;Wy_mm;Ep_mm;x1,y1,d1|x2,y2,d2;S235;9005;CENTER`
     3. **`batch_create_profiles_by_two_points`** & **`create_profile_by_two_points`** (**Recommandé en TopSolid 7.21**) :
        Pose directement les profilés standards de la bibliothèque TopSolid (`Composants Mécanique TopSolid` -> `Profilés` : `Plat`, `Tube rectangle`, `Tube carré`, `Tube rond`, `Rond`, `Carré`) pilotés par leurs codes de famille (`50x10`, `50x8`, `50x20x2`, `12` — normalisés automatiquement en Unicode `\u00d7` `50 × 10`) via l'API native 7.21 `TopSolidDesignHost.Assemblies.CreateProfileByTwoPoints`.
        - Génère automatiquement les pièces synchronisées avec matériau `Acier S235 EN 10025`, propriété système `Longueur` (`$TopSolid.Kernel.TX.Properties.Length`), `Code`, `Désignation` et `Angle de coupe 1/2`.
        - **Orientation de la section (`dir`)** :
          - `Plat` (`50x10`, `50x8`) place son **épaisseur** (`10 mm` ou `8 mm`) selon `dir` (`DX` pour un poteau vertical sur garde-corps en `X`, `DY` pour un poteau sur garde-corps en `Y`, `DZ` pour une lisse plate horizontale).
          - `Tube rectangle` (`50x20x2`) place sa **grande dimension** (`50 mm`) selon `dir` : pour poser une main courante `50x20x2` à plat (`50 mm` horizontal, `20 mm` vertical), passer **`DY,0`** sur un tronçon le long de `X` et **`DX,0`** sur un tronçon le long de `Y`.
     4. **`create_extruded_bar`** (pièce profilée `.TopPrt` autonome avec perçages transversaux ou géométrie spécifique) :
        Respecte strictement la **chronologie de l'arbre d'opérations** (`Esquisse` -> `Extrusion` -> `Perçages` -> `Repère` -> `ProvideFunction("Extruded Bar")` en tout dernier dans `ModelingStage`) et vérifie `TopSolidHost.Elements.IsInvalid(el) == false`.
     5. **`create_part_document`** / **`create_assembly_document`** : crée un `.TopPrt` ou `.TopAsm` avec ses propriétés PDM.
     6. **`batch_add_inclusions`** : assemble en une seule transaction toutes les pièces (ex: platines) ou sous-assemblages (`Troncon_Droit_1`, `Troncon_Droit_2`, `Troncon_Retour_G`).
     7. **`update_where_used_and_save`** : reconstruit et sauvegarde en bottom-up (`Pièces .TopPrt -> Sous-Assemblages .TopAsm -> Assemblage Général .TopAsm`).
     8. **`check_collisions`** + **`audit_assembly`** : vérifie **0 collision B-Rep** (`ShapeShapeCollisionEntity`) et **0 élément invalide** (`Invalid elements: 0`).
   - **Fallback « One-Shot C# Builder »** : uniquement si une géométrie gauche ou opération spéciale n'est pas couverte par les 211 recettes. Dans ce cas, écrire 1 seul script C# complet compilé via `topsolid_compile` et exécuté via `topsolid_execute_script`.

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
