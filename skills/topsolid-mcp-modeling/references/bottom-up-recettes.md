# Recettes Bottom-Up & One-Shot Builder — Pont MCP TopSolid 7.20 / 7.21

Toutes les signatures ci-dessous sont vérifiées contre l'API TopSolid Automating 7.20/7.21 et compilées en C# via `topsolid_compile`.
**NE JAMAIS fouiller `methods.json` ou `types.json` avec des scripts Python** : toutes les signatures nécessaires à la création de projets, pièces, esquisses, extrusions, perçages, matériaux, assemblages et détection de collisions sont ci-dessous.

## 0. Répartition exacte `TopSolidHost` vs `TopSolidDesignHost`
- Sur **`TopSolidHost`** (`TopSolid.Kernel.Automating`) : `.Application`, `.Pdm`, `.Documents`, `.Elements`, `.Entities`, `.Operations`, `.Parameters`, `.Sketches2D`, `.Shapes`, `.Geometries3D`.
- Sur **`TopSolidDesignHost`** (`TopSolid.Cad.Design.Automating`) : `.Parts`, `.Assemblies`, `.Representations`, `.Mechanisms`.

## 1. Exécution Rapide via le CLI permanent `topsolid_mcp_cli.py` (Zéro réécriture de client !)

**Ne perds JAMAIS un tour d'outil à écrire un fichier `mcp_client.py` !** Utilise directement les outils MCP ou le script CLI `topsolid_mcp_cli.py` :
```bash
# 1. Vérifier l'état de TopSolid :
python topsolid_mcp_cli.py state

# 2. Compiler un fichier C# hors-ligne (< 0.5 s) :
python topsolid_mcp_cli.py compile build_project.cs

# 3. Exécuter le script C# One-Shot (< 15 s) :
python topsolid_mcp_cli.py exec build_project.cs

# 4. Lancer une recette unitaire :
python topsolid_mcp_cli.py recipe check_collisions
```

> [!CAUTION]
> **LES 4 RÈGLES « COMPILE-FIRST » (Zéro erreur de compilation au 1er essai) :**
> 1. **`CreateSketchIn3D` retourne un `ElementId`** (JAMAIS `ElementItemId` ! Seuls `CreateVertex`, `CreateLineSegment` et `CreateCircleSegment` retournent un `ElementItemId`).
> 2. **Pas d'opérateurs `==` ni `!=` sur `PdmObjectId`, `DocumentId`, `ElementId`** (`CS0019`) : toujours écrire **`!id.IsEmpty`**, **`id.IsEmpty`** ou **`id1.Equals(id2)`**.
> 3. **`SetMaterial` et `CreateInclusion` prennent des `DocumentId`** (`CS1503`) : toujours convertir un `PdmObjectId` en `DocumentId` avec `TopSolidHost.Documents.GetDocument(pdmId)`.
> 4. **Support des lambdas `Func<...>` avec `return`** : depuis `TopSolidMcpServer v1.8.8`, vous pouvez utiliser des helpers `Func<..., DocumentId> makePart = (...) => { ... return dId; };` dans votre script One-Shot !

> [!WARNING]
> **5 INTERDICTIONS ABSOLUES (causes de blocage de TopSolid) :**
> 1. **JAMAIS `TopSolidHost.Documents.Update(docId, true)` à l'intérieur d'un `StartModification` ouvert** : deadlock immédiat (timeout 60 s). C'est `TopSolidHost.Application.EndModification(true, true)` qui met à jour le document.
> 2. **TOUJOURS appeler `TopSolidHost.Sketches2D.CleanSketch();` juste avant `TopSolidHost.Sketches2D.EndModification();`** et passer **`true` (`inDefinesXDirection = true`)** en 4e argument de `CreateSketchIn3D`.
> 3. **FORMAT DU SCRIPT C# = CORPS DE MÉTHODE UNIQUEMENT** : ne jamais déclarer `class ...` ni `public static object Run()`.
> 4. **JAMAIS `GetShapeVolume(shapeId)` avant `EndModification(true, true)`**.
> 5. **JAMAIS de parcours non filtré des 55 bibliothèques PDM** (timeout 60 s sur les bibliothèques de visserie) ni d'affectation d'un `.TopLam` dans `SetMaterial` (crash `LaminateMaterialDocument`). Utiliser exclusivement le helper `< 500 ms` de la Section 7 ci-dessous.

---

## 2. Création ou Réutilisation de Projet PDM, Dossiers et Documents (`.TopPrt`, `.TopAsm`)

Les créations PDM (`CreateProject`, `CreateFolder`, `CreateDocument`, `Pdm.Save`) s'appellent **HORS** `StartModification` :

```csharp
// 1. Réutiliser un projet existant ou en créer un nouveau HORS StartModification :
PdmObjectId projId = PdmObjectId.Empty;
List<PdmObjectId> foundProjs = TopSolidHost.Pdm.SearchProjectByName("MonProjet");
if (foundProjs != null && foundProjs.Count > 0)
    projId = foundProjs[0];
else
    projId = TopSolidHost.Pdm.CreateProject("MonProjet", false);

// Optionnel : créer un sous-dossier dans le projet pour isoler un essai :
PdmObjectId folderId = TopSolidHost.Pdm.CreateFolder(projId, "Run_V2");

// 2. Créer un document pièce (.TopPrt) ou assemblage (.TopAsm) avec gabarit par défaut (true) HORS StartModification :
PdmObjectId partPdm = TopSolidHost.Pdm.CreateDocument(folderId, ".TopPrt", true);
PdmObjectId asmPdm  = TopSolidHost.Pdm.CreateDocument(folderId, ".TopAsm", true);

// 3. Convertir PdmObjectId -> DocumentId :
DocumentId partDoc = TopSolidHost.Documents.GetDocument(partPdm);
```

---

## 3. Repères d'Esquisse 3D (Déterministes) + `CleanSketch()` + Extrusion + Représentations

### Correspondance exacte 2D `(u, v)` → 3D `(X, Y, Z)` dans `CreateSketchIn3D`
Signature : `ElementId CreateSketchIn3D(DocumentId inDoc, SmartPlane3D inPlane, SmartPoint3D inOrigin, bool inDefinesXDirection, SmartDirection3D inDirection)`
Avec **`inDefinesXDirection = true` (4e argument TOUJOURS à `true`, JAMAIS `false`)** :
- **Plan XY (`Z = z0`)** : `Plane3D plXY = new Plane3D(new Point3D(0, 0, z0), Direction3D.DX, Direction3D.DY);` et `inDirection = SmartDirection3D.DX`
  → dans l'esquisse 2D : **`u = +X`, `v = +Y`**. Direction d'extrusion : **`SmartDirection3D.DZ` (`+Z`)**.
- **Plan XZ (`Y = y0`)** : `Plane3D plXZ = new Plane3D(new Point3D(0, y0, 0), Direction3D.DX, Direction3D.DZ);` et `inDirection = SmartDirection3D.DX`
  → dans l'esquisse 2D : **`u = +X`, `v = +Z`**. Direction d'extrusion : **`SmartDirection3D.DY` (`+Y`)**.
- **Plan YZ (`X = x0`)** : `Plane3D plYZ = new Plane3D(new Point3D(x0, 0, 0), Direction3D.DY, Direction3D.DZ);` et `inDirection = SmartDirection3D.DY`
  → dans l'esquisse 2D : **`u = +Y`, `v = +Z`**. Direction d'extrusion : **`SmartDirection3D.DX` (`+X`)**.

### Patron complet de création d'une pièce extrudée (avec trous éventuels et matériau PDM)
```csharp
DocumentId dId = TopSolidHost.Documents.GetDocument(partPdm);
TopSolidHost.Documents.Open(ref dId);
TopSolidHost.Application.StartModification("BuildPart", false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref dId);
    TopSolidHost.Documents.SetName(dId, "GC_Platine_100x80x10");

    // 1. Esquisse 3D sur le plan XY (Z = 0) : u = +X, v = +Y (4e argument = true ! Type de retour = ElementId !)
    Point3D orig = new Point3D(0, 0, 0);
    Plane3D plXY = new Plane3D(orig, Direction3D.DX, Direction3D.DY);
    ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(
        dId,
        new SmartPlane3D(plXY, -1.0, 1.0, -1.0, 1.0),
        new SmartPoint3D(orig),
        true,
        SmartDirection3D.DX
    );

    TopSolidHost.Sketches2D.StartModification(sk);
    // Contour extérieur rectangle X=[x0..x1], Y=[y0..y1] :
    ElementItemId v1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0));
    ElementItemId v2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x1, y0));
    ElementItemId v3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x1, y1));
    ElementItemId v4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y1));
    TopSolidHost.Sketches2D.CreateLineSegment(v1, v2);
    TopSolidHost.Sketches2D.CreateLineSegment(v2, v3);
    TopSolidHost.Sketches2D.CreateLineSegment(v3, v4);
    TopSolidHost.Sketches2D.CreateLineSegment(v4, v1);

    // Trous intérieurs éventuels (ex: 2 trous Ø12mm -> rayon 0.006m) :
    ElementItemId c1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx1, hy1));
    TopSolidHost.Sketches2D.CreateCircleSegment(c1, 0.006);
    ElementItemId c2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx2, hy2));
    TopSolidHost.Sketches2D.CreateCircleSegment(c2, 0.006);

    // OBLIGATOIRE avant EndModification() :
    TopSolidHost.Sketches2D.CleanSketch();
    TopSolidHost.Sketches2D.EndModification();

    // 2. Extrusion directe de l'esquisse via SmartSection3D(sk) selon +DZ
    ElementId shId = TopSolidHost.Shapes.CreateExtrudedShape(
        dId,
        new SmartSection3D(sk),
        SmartDirection3D.DZ,
        new SmartReal(UnitType.Length, heightMeters),
        new SmartReal(UnitType.Angle, 0),
        false,
        false
    );

    // 3. Affectation du matériau PDM (.TopMat) + calcul automatique de la masse + Couleur RGB :
    if (!matDocId.IsEmpty)
    {
        TopSolidDesignHost.Parts.SetMaterial(dId, matDocId);
        TopSolidDesignHost.Parts.SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0));
        TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(dId, true);
    }
    TopSolidHost.Elements.SetColor(shId, new Color(25, 25, 28));

    // 4. OBLIGATOIRE : ajouter toutes les formes à TOUTES les représentations (Conception, Détaillée, Simplifiée)
    var shapes = TopSolidHost.Shapes.GetShapes(dId);
    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(dId))
    {
        var repCons = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep);
        foreach (ElementId s in shapes)
        {
            if (!repCons.Contains(s))
                TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, s);
        }
    }

    TopSolidHost.Application.EndModification(true, true);
}
catch
{
    try { TopSolidHost.Application.EndModification(false, false); } catch { }
    throw;
}
TopSolidHost.Pdm.Save(partPdm, true);
```

---

## 4. Perçages (`TopSolidDesignHost.Parts.CreateDrillingOperation`)

Il n'existe aucune opération booléenne (`Subtract`/`Union`/`Pocket`) dans `IShapes`.
Pour un trou traversant une plaque ou un montant extrudé :
- **Option A (recommandée, 10x plus rapide et inratable)** : dessiner directement les cercles (`CreateCircleSegment(centerVertex, radius)`) à l'intérieur du contour rectangle dans la même esquisse 2D avant `CreateExtrudedShape` ! TopSolid soustrait automatiquement tous les contours intérieurs lors de l'extrusion.
- **Option B (sur une face transverse)** : utiliser `CreateDrillingOperation` (vecteur `Z` du `Frame3D` = normale sortante à la face, constructeur à 3 arguments `new DrillingHolePrimitive(dia, depth, bottomType)` avec `depth > 0`, et `SmartFrame3D` inline).

---

## 5. Assemblage et positionnement des Inclusions (`CreatePositioning` + `CreateInclusion` à 16 arguments)

Toujours sauvegarder toutes les pièces enfants **avant** de les insérer dans l'assemblage parent.
Attention : le 4e argument `childDocId` est de type **`DocumentId`** (jamais `PdmObjectId`) !

```csharp
TopSolidHost.Documents.Open(ref dAsm);
TopSolidHost.Application.StartModification("BuildAsm", false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref dAsm);

    // Translation pure à (tx, ty, tz) en mètres (sans rotation) :
    Transform3D tr = new Transform3D(
        1, 0, 0, tx,
        0, 1, 0, ty,
        0, 0, 1, tz,
        0, 0, 0, 1
    );

    ElementId posId = TopSolidDesignHost.Assemblies.CreatePositioning(dAsm);
    ElementId incId = TopSolidDesignHost.Assemblies.CreateInclusion(
        dAsm,
        posId,
        "NomOccurrence",
        childDocId, // DocumentId !
        null,
        new List<string>(),
        new List<SmartObject>(),
        true,
        ElementId.Empty,
        ElementId.Empty,
        false,
        false,
        false,
        false,
        tr,
        true
    );

    TopSolidHost.Application.EndModification(true, true);
}
catch
{
    try { TopSolidHost.Application.EndModification(false, false); } catch { }
    throw;
}
TopSolidHost.Pdm.Save(TopSolidHost.Documents.GetPdmObject(dAsm), true);
```

---

## 6. Étapes (Stages) TopSolid & Détection native des collisions B-Rep

```csharp
TopSolidHost.Application.StartModification("CheckCollisions", false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref dAsm);
    ElementId designRep = TopSolidDesignHost.Representations.GetDesignRepresentation(dAsm);
    TopSolidDesignHost.Assemblies.SetCollisionsManagement(dAsm, designRep, true, true, true);
    TopSolidDesignHost.Assemblies.SetCollisionsManagementCheckingMechanisms(dAsm, false);
    TopSolidDesignHost.Assemblies.SetCollisionsManagementInvalidWhenColliding(dAsm, false);
    ElementId modStage = TopSolidHost.Operations.GetModelingStage(dAsm);
    if (!modStage.IsEmpty)
    {
        TopSolidHost.Operations.SetWorkingStage(modStage);
        TopSolidHost.Operations.ResetInsertionOperation(dAsm);
    }
    TopSolidHost.Application.EndModification(true, true);
}
catch
{
    try { TopSolidHost.Application.EndModification(false, false); } catch { }
    throw;
}

int collCount = 0;
foreach (var el in TopSolidHost.Elements.GetElements(dAsm))
{
    if (TopSolidHost.Elements.GetTypeFullName(el) == "TopSolid.Cad.Design.DB.Collisions.ShapeShapeCollisionEntity")
    {
        collCount++;
        var chs = TopSolidHost.Elements.GetConstituents(el);
        var pair = new List<string>();
        foreach (var ch in chs)
        {
            ElementId target = TopSolidHost.Entities.IsShortcut(ch) ? TopSolidHost.Entities.GetShortcutTarget(ch) : ElementId.Empty;
            ElementId occ = target.IsEmpty ? ElementId.Empty : TopSolidHost.Elements.GetParent(target);
            pair.Add(TopSolidHost.Elements.GetFriendlyName(ch) + " (" + (occ.IsEmpty ? "" : TopSolidHost.Elements.GetFriendlyName(occ)) + ")");
        }
        sb.AppendLine("COLLISION: " + string.Join(" <-> ", pair));
    }
}
sb.AppendLine("Total B-Rep Collisions: " + collCount);
```

---

## 7. Recherche Générique Ultra-Rapide des Matériaux (`.TopMat`, `< 500 ms`, Zéro ID en dur)

Ne **JAMAIS** utiliser `SearchProjectByName("Mécanique CEN TopSolid")` (multi-mots = 0 résultat) ni parcourir les 55 bibliothèques sans filtre (timeout 60 s).
Copier-coller ce bloc **HORS `StartModification`** au début du script One-Shot :

```csharp
// 1. Filtrer uniquement les bibliothèques matériaux (< 10 ms) :
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

// 2. Chercher par désignation normalisée métier en filtrant strictement l'extension ".TopMat" (< 400 ms) :
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
                    {
                        List<PdmObjectId> refs = TopSolidHost.Pdm.GetReferencedProjects(projId);
                        if (!refs.Contains(lib))
                            TopSolidHost.Pdm.AddReferencedProjects(projId, new List<PdmObjectId> { lib });
                        return TopSolidHost.Documents.GetDocument(h);
                    }
                }
            }
        }
    }
    return DocumentId.Empty;
};

DocumentId matSteel = findMaterial(new string[] { "*S235*", "*S275*", "*S355*" });
DocumentId matInox  = findMaterial(new string[] { "*X2CrNi19-11*", "*X5CrNi18-10*", "*304*", "*316*", "*CrNi*" });
DocumentId matWood  = findMaterial(new string[] { "*Chêne*", "*Chene*", "*Pin*", "*Mélèze*", "*Oak*", "*Sapin*" });
```

Et pour vérifier en fin de script que 100 % des pièces ont bien leur matériau PDM affecté (sans utiliser la recette `check_missing_materials` qui cherche un paramètre `"Mass"` en anglais) :
```csharp
DocumentId assignedMat = TopSolidDesignHost.Parts.GetMaterial(dId);
string matName = assignedMat.IsEmpty ? "MISSING" : TopSolidHost.Documents.GetName(assignedMat);
```
