---
name: topsolid-mcp-modeling
version: 2.5.0
author: Noemid-H
license: internal
metadata:
  hermes:
    tags: [topsolid, mcp, cad, assemblage, stl, verification-perceptive, one-shot-builder, collisions, where-used, circuit-breaker]
    related_skills: [topsolid-mcp, topsolid-railing-design, topsolid-api-knowledge, topsolid-cage-from-image]
description: Use when modeling TopSolid via the MCP bridge (TopSolid user best practices, Circuit-Breaker & Token Discipline, CoT Reasoning Budget, Compile-First C# Rules, Generic <500ms Material Lookup, Where-Used update before save, One-Shot C# Builder + Native B-Rep Collision Check).
---

# Modélisation & Bonnes Pratiques Utilisateur TopSolid via MCP (LY458 :8080)

> **Skill vivant** : Ce skill centralise les pratiques utilisateur TopSolid et la modélisation via le pont MCP. Chaque nouveau piège ou apprentissage issu des tests MCP doit y être immédiatement ajouté et synchronisé.
> **Skills métiers spécialisés à charger en parallèle selon l'ouvrage** :
> - **Garde-corps / rambardes / balustrades** : charger obligatoirement **`topsolid-railing-design`** (`TopSolid-Railing-Design`) avant toute analyse d'image ou conception.
> - **Cages SubD depuis photo** : charger **`topsolid-cage-from-image`**.

## When to Use

Use when creating, modifying, or inspecting TopSolid geometry through the JSON-RPC bridge on LY458
(port 8080) — sketch-based parts, extrusions, drillings, assemblies with positioned inclusions,
Where-Used (`cas d'emploi`) propagation, native TopSolid B-Rep collision checks, and visual verification gates.

Classe de travail : analyser finement le besoin/photo (avec le skill métier spécialisé), ne poser de question que sur les vrais choix ou doutes, puis générer et exécuter le CAD en **1 seul script C# (« One-Shot Builder »)** compilé du premier coup (`topsolid_compile`) et exécuté (`topsolid_execute_script`), avec vérification native des collisions B-Rep TopSolid (`ShapeShapeCollisionEntity`). Cible : **< 2 minutes et < 25 000 tokens**.

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

4. **Architecture « One-Shot C# Builder » obligatoire** — Écrire **un seul script C# complet** (corps de méthode `Run()`, supportant les helpers `Func<...>` avec `return`) qui :
   - réutilise le projet cible existant (`SearchProjectByName`) ou crée le projet PDM hors `StartModification`,
   - recherche génériquement en `< 500 ms` les matériaux métier standards (`*S235*`, `*X2CrNi19-11*` / `*304*`, `*Chêne*` / `*Pin*`) avec vérification `ext == ".TopMat"`,
   - crée toutes les pièces unitaires (`.TopPrt`), y compris les **platines de fixation percées (`GC_Platine`)**,
   - appelle **`TopSolidHost.Sketches2D.CleanSketch();`** juste avant chaque `TopSolidHost.Sketches2D.EndModification();`,
   - passe **`true` en 4e argument de `CreateSketchIn3D`** (`bool inDefinesXDirection = true`),
   - ajoute toutes les formes à toutes les représentations,
   - affecte les matériaux PDM (`.TopMat`) + `SetMassPropertyManagement` + `SetPhysicalPropertiesManagementRefreshAuto` + couleurs RGB,
   - assemble toutes les inclusions (`CreateInclusion` à **16 arguments avec `Transform3D`**) dans le `.TopAsm`,
   - lance la détection de collisions native TopSolid (`SetCollisionsManagement`), vérifie les matériaux directement via `Parts.GetMaterial(dId)` (ne pas utiliser la recette `check_missing_materials` qui cherche un paramètre `"Mass"` en anglais) et retourne le rapport complet en **1 seul appel `topsolid_execute_script`**.

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
