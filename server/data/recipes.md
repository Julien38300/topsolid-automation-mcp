# TopSolid MCP Recipes Catalog

## read_designation
Pattern: R
Description: Reads the designation of the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string val = TopSolidHost.Pdm.GetDescription(pdmId);
return string.IsNullOrEmpty(val) ? "Designation: (empty)" : "Designation: " + val;
```

---

## read_name
Pattern: R
Description: Reads the name of the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
return "Name: " + TopSolidHost.Pdm.GetName(pdmId);
```

---

## read_reference
Pattern: R
Description: Reads the reference (part number) of the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string val = TopSolidHost.Pdm.GetPartNumber(pdmId);
return string.IsNullOrEmpty(val) ? "Reference: (empty)" : "Reference: " + val;
```

---

## read_manufacturer
Pattern: R
Description: Reads the manufacturer of the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string val = TopSolidHost.Pdm.GetManufacturer(pdmId);
return string.IsNullOrEmpty(val) ? "Manufacturer: (empty)" : "Manufacturer: " + val;
```

---

## read_pdm_properties
Pattern: R
Description: Reads all PDM properties (name, designation, reference, manufacturer)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Name: " + TopSolidHost.Pdm.GetName(pdmId));
string desc = TopSolidHost.Pdm.GetDescription(pdmId);
sb.AppendLine("Designation: " + (string.IsNullOrEmpty(desc) ? "(empty)" : desc));
string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);
sb.AppendLine("Reference: " + (string.IsNullOrEmpty(pn) ? "(empty)" : pn));
string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);
sb.AppendLine("Manufacturer: " + (string.IsNullOrEmpty(mfr) ? "(empty)" : mfr));
return sb.ToString();
```

---

## set_designation
Pattern: R
Description: Sets the designation. Param: value

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.SetDescription(pdmId, "{value}");
TopSolidHost.Pdm.Save(pdmId, true);
return "OK: Designation → {value}";
```

---

## set_name
Pattern: R
Description: Sets the name. Param: value

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.SetName(pdmId, "{value}");
return "OK: Name → {value}";
```

---

## set_reference
Pattern: R
Description: Sets the reference. Param: value

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.SetPartNumber(pdmId, "{value}");
TopSolidHost.Pdm.Save(pdmId, true);
return "OK: Reference → {value}";
```

---

## set_manufacturer
Pattern: R
Description: Sets the manufacturer. Param: value

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.SetManufacturer(pdmId, "{value}");
TopSolidHost.Pdm.Save(pdmId, true);
return "OK: Manufacturer → {value}";
```

---

## read_current_project
Pattern: R
Description: Returns the current project

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
return "Project: " + TopSolidHost.Pdm.GetName(projId);
```

---

## read_project_contents
Pattern: R
Description: Lists folders, subfolders and documents of the current project (full tree)

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
var sb = new System.Text.StringBuilder();
sb.AppendLine("Project: " + TopSolidHost.Pdm.GetName(projId));
int totalDocs = 0; int totalFolders = 0;
Action<PdmObjectId, string> listRecursive = null;
listRecursive = (parentId, indent) => {
    List<PdmObjectId> folders; List<PdmObjectId> docs;
    TopSolidHost.Pdm.GetConstituents(parentId, out folders, out docs);
    foreach (var f in folders) {
        totalFolders++;
        sb.AppendLine(indent + "[Folder] " + TopSolidHost.Pdm.GetName(f));
        listRecursive(f, indent + "  ");
    }
    foreach (var d in docs) {
        totalDocs++;
        sb.AppendLine(indent + TopSolidHost.Pdm.GetName(d));
    }
};
listRecursive(projId, "  ");
sb.Insert(sb.ToString().IndexOf('\
') + 1, "(" + totalFolders + " folders, " + totalDocs + " documents)\
");
return sb.ToString();
```

---

## search_document
Pattern: R
Description: Searches for a document by name (CONTAINS). Param: value

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
var sb = new System.Text.StringBuilder();
sb.AppendLine("Search '" + "{value}" + "': " + results.Count + " results");
foreach (var r in results)
{
    string name = TopSolidHost.Pdm.GetName(r);
    sb.AppendLine("  " + name);
}
return sb.ToString();
```

---

## search_folder
Pattern: R
Description: Searches for a folder by name (CONTAINS). Param: value

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
var results = TopSolidHost.Pdm.SearchFolderByName(projId, "{value}");
var sb = new System.Text.StringBuilder();
sb.AppendLine("Folder search '" + "{value}" + "': " + results.Count + " results");
foreach (var r in results)
    sb.AppendLine("  " + TopSolidHost.Pdm.GetName(r));
return sb.ToString();
```

---

## document_type
Pattern: R
Description: Detects the type of the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var sb = new System.Text.StringBuilder();
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
sb.AppendLine("Name: " + TopSolidHost.Pdm.GetName(pdmId));
string typeName = TopSolidHost.Documents.GetTypeFullName(docId);
sb.AppendLine("Type: " + typeName);
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch {}
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch {}
bool isAssembly = false;
try { isAssembly = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch {}
if (isDrafting) sb.AppendLine("→ Drafting");
else if (isBom) sb.AppendLine("→ BOM");
else if (isAssembly) sb.AppendLine("→ Assembly");
else sb.AppendLine("→ Part");
return sb.ToString();
```

---

## save_document
Pattern: R
Description: Saves the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.Save(pdmId, true);
return "OK: Document saved.";
```

---

## rebuild_document
Pattern: RW
Description: Rebuilds the active document

```csharp
TopSolidHost.Documents.Rebuild(docId);
__message = "OK: Document rebuilt.";
```

---

## read_parameters
Pattern: R
Description: Lists all parameters of the active document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Parameters: " + pList.Count);
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    var pType = TopSolidHost.Parameters.GetParameterType(p);
    string val = "";
    if (pType == ParameterType.Real) val = TopSolidHost.Parameters.GetRealValue(p).ToString("F6");
    else if (pType == ParameterType.Integer) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    else if (pType == ParameterType.Boolean) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
    else if (pType == ParameterType.Text) val = TopSolidHost.Parameters.GetTextValue(p);
    sb.AppendLine("  " + name + " = " + val + " (type=" + pType + ")");
}
return sb.ToString();
```

---

## read_real_parameter
Pattern: R
Description: Reads a real parameter by name. Param: value=name

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.IndexOf("{value}", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        double val = TopSolidHost.Parameters.GetRealValue(p);
        return name + " = " + val.ToString("F6") + " (SI)";
    }
}
return "Parameter '{value}' not found.";
```

---

## read_text_parameter
Pattern: R
Description: Reads a text parameter by name. Param: value=name

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.IndexOf("{value}", StringComparison.OrdinalIgnoreCase) >= 0)
        return name + " = " + TopSolidHost.Parameters.GetTextValue(p);
}
return "Parameter '{value}' not found.";
```

---

## set_real_parameter
Pattern: RW
Description: Sets a real parameter. Param: value=name:SIvalue (e.g. Length:0.15)

```csharp
string[] parts = "{value}".Split(':');
if (parts.Length != 2) return "Format: name:SIvalue (e.g. Length:0.15)";
string pName = parts[0].Trim();
double newVal;
if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out newVal))
    return "Invalid value: " + parts[1];
var pList = TopSolidHost.Parameters.GetParameters(docId);
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.IndexOf(pName, StringComparison.OrdinalIgnoreCase) >= 0)
    {
        TopSolidHost.Parameters.SetRealValue(p, newVal);
        __message = "OK: " + name + " → " + newVal.ToString("F6");
        return;
    }
}
__message = "Parameter '" + pName + "' not found.";
```

---

## set_text_parameter
Pattern: RW
Description: Sets a text parameter. Param: value=name:value

```csharp
int idx = "{value}".IndexOf(':');
if (idx < 0) { __message = "Format: name:value"; return; }
string pName = "{value}".Substring(0, idx).Trim();
string newVal = "{value}".Substring(idx + 1).Trim();
var pList = TopSolidHost.Parameters.GetParameters(docId);
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.IndexOf(pName, StringComparison.OrdinalIgnoreCase) >= 0)
    {
        TopSolidHost.Parameters.SetTextValue(p, newVal);
        __message = "OK: " + name + " → " + newVal;
        return;
    }
}
__message = "Parameter '" + pName + "' not found.";
```

---

## creer_parametre_reel
Pattern: RW
Description: Creates a new real parameter in the active document. Param: value=name:unit:SIvalue (e.g. Longueur:Length:0.05 for 50mm) or name::SIvalue for NoUnit. Supported units: Length, Mass, Angle, NoUnit.

```csharp
string[] parts = "{value}".Split(':');
if (parts.Length < 2) { __message = "Format: name:unit:SIvalue (e.g. Longueur:Length:0.05)"; return; }
string paramName = parts[0].Trim();
string unitStr = parts.Length >= 3 ? parts[1].Trim() : "";
string valStr = parts.Length >= 3 ? parts[2].Trim() : parts[1].Trim();
double siVal;
if (!double.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out siVal))
    { __message = "Invalid SI value: " + valStr; return; }
UnitType unit;
switch (unitStr.ToLowerInvariant()) {
    case "length": unit = UnitType.Length; break;
    case "mass":   unit = UnitType.Mass;   break;
    case "angle":  unit = UnitType.Angle;  break;
    default:       unit = UnitType.NoUnit; break;
}
if (string.IsNullOrWhiteSpace(paramName)) { __message = "Parameter name cannot be empty."; return; }
ElementId paramId = TopSolidHost.Parameters.CreateRealParameter(docId, unit, siVal);
TopSolidHost.Elements.SetName(paramId, paramName);
__message = "OK: parameter '" + paramName + "' created (" + unit + ", value=" + siVal.ToString("F6") + ")";
```

---

## creer_parametre_formule
Pattern: RW
Description: Creates a new formula-driven real parameter in the active document. Param: value=name:unit:formula (e.g. DiagBolt:Length:Longueur * 1.414). The formula uses TopSolid expression syntax (parameter names, operators, SI units). Supported units: Length, Mass, Angle, NoUnit.

```csharp
int sep1 = "{value}".IndexOf(':');
if (sep1 < 0) { __message = "Format: name:unit:formula"; return; }
int sep2 = "{value}".IndexOf(':', sep1 + 1);
if (sep2 < 0) { __message = "Format: name:unit:formula (unit required)"; return; }
string paramName = "{value}".Substring(0, sep1).Trim();
string unitStr = "{value}".Substring(sep1 + 1, sep2 - sep1 - 1).Trim();
string formula = "{value}".Substring(sep2 + 1).Trim();
if (string.IsNullOrWhiteSpace(paramName)) { __message = "Parameter name cannot be empty."; return; }
if (string.IsNullOrWhiteSpace(formula)) { __message = "Formula cannot be empty."; return; }
UnitType unit;
switch (unitStr.ToLowerInvariant()) {
    case "length": unit = UnitType.Length; break;
    case "mass":   unit = UnitType.Mass;   break;
    case "angle":  unit = UnitType.Angle;  break;
    default:       unit = UnitType.NoUnit; break;
}
ElementId paramId = TopSolidHost.Parameters.CreateSmartRealParameter(docId, new SmartReal(unit, formula));
TopSolidHost.Elements.SetName(paramId, paramName);
__message = "OK: formula parameter '" + paramName + "' created (" + unit + ", formula='" + formula + "')";
```

---

## creer_esquisse_rectangle
Pattern: RW
Description: Cree une esquisse 2D rectangulaire dans le document actif. Param: value=largeur:hauteur (mm)

```csharp
string[] parts = "{value}".Split(':');
if (parts.Length < 2) { __message = "ERROR: expected format width:height (mm)"; return; }
double widthMm, heightMm;
if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out widthMm) ||
    !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heightMm))
    { __message = "ERROR: invalid numeric values"; return; }
double w = widthMm * 0.001;
double h = heightMm * 0.001;
// docId is refreshed by the wrapper (EnsureIsDirty)
Plane3D xyPlane = new Plane3D(new Point3D(0, 0, 0), Direction3D.DX, Direction3D.DY);
ElementId sketchId = TopSolidHost.Sketches2D.CreateSketchIn3D(docId, new SmartPlane3D(xyPlane, -0.5, Math.Max(0.5, w + 0.2), -0.5, Math.Max(0.5, h + 0.2)), new SmartPoint3D(new Point3D(0, 0, 0)), true, SmartDirection3D.DX);
TopSolidHost.Elements.SetName(sketchId, "Esquisse_Rectangle");
TopSolidHost.Sketches2D.StartModification(sketchId);
ElementItemId pt1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, 0));
ElementItemId pt2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(w, 0));
ElementItemId pt3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(w, h));
ElementItemId pt4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, h));
TopSolidHost.Sketches2D.CreateLineSegment(pt1, pt2);
TopSolidHost.Sketches2D.CreateLineSegment(pt2, pt3);
TopSolidHost.Sketches2D.CreateLineSegment(pt3, pt4);
TopSolidHost.Sketches2D.CreateLineSegment(pt4, pt1);
TopSolidHost.Sketches2D.CleanSketch();
TopSolidHost.Sketches2D.EndModification();
__message = "OK: sketch Esquisse_Rectangle created (" + widthMm + "x" + heightMm + " mm)";
```

---

## extruder_esquisse
Pattern: RW
Description: Extrude la derniere esquisse 2D du document actif en solide 3D. Param: value=hauteur_mm

```csharp
double heightMm;
if (!double.TryParse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heightMm))
    { __message = "ERROR: invalid numeric height"; return; }
double heightM = heightMm * 0.001;
var sketches = TopSolidHost.Sketches2D.GetSketches(docId);
if (sketches == null || sketches.Count == 0)
    { __message = "ERROR: no sketch in this document"; return; }
ElementId lastSketch = sketches[sketches.Count - 1];
ElementId shId = TopSolidHost.Shapes.CreateExtrudedShape(docId, new SmartSection3D(lastSketch), SmartDirection3D.DZ, new SmartReal(UnitType.Length, heightM), new SmartReal(UnitType.Angle, 0), false, false);
foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(docId))
{
    if (!TopSolidDesignHost.Representations.GetRepresentationConstituents(rep).Contains(shId))
        TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, shId);
}
__message = "OK: extrusion of " + heightMm + "mm created (added to all representations)";
```

---

## ajouter_inclusion
Pattern: RW
Description: Ajoute une inclusion d'un document piece dans le document assemblage actif. Param: value=nom_document_piece

```csharp
if (!TopSolidDesignHost.Assemblies.IsAssembly(docId))
    { __message = "ERROR: le document actif n'est pas un assemblage"; return; }
DocumentId partDoc = DocumentId.Empty;
string targetName = "{value}".Trim();
var allDocs = TopSolidHost.Documents.GetOpenDocuments();
foreach (DocumentId d in allDocs) {
    string docName = "";
    try { docName = TopSolidHost.Documents.GetName(d); } catch { continue; }
    if (docName == targetName || docName.StartsWith(targetName + ".")) {
        partDoc = d;
        break;
    }
}
if (partDoc.IsEmpty)
    { __message = "ERROR: document '" + targetName + "' introuvable (ouvrir le document d'abord)"; return; }
ElementId positioningId = TopSolidDesignHost.Assemblies.CreatePositioning(docId);
TopSolidDesignHost.Assemblies.CreateInclusion(docId, positioningId, null, partDoc, null, null, null, true, ElementId.Empty, ElementId.Empty, false, false, false, false, Transform3D.Identity, false);
__message = "OK: document '" + targetName + "' inclus dans l'assemblage";
```

---

## read_3d_points
Pattern: R
Description: Lists 3D points of the document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var points = TopSolidHost.Geometries3D.GetPoints(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("3D Points: " + points.Count);
foreach (var pt in points)
{
    string name = TopSolidHost.Elements.GetFriendlyName(pt);
    Point3D geom = TopSolidHost.Geometries3D.GetPointGeometry(pt);
    sb.AppendLine("  " + name + " (" + (geom.X*1000).ToString("F1") + ", " + (geom.Y*1000).ToString("F1") + ", " + (geom.Z*1000).ToString("F1") + ") mm");
}
return sb.ToString();
```

---

## read_3d_frames
Pattern: R
Description: Lists 3D frames of the document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var frames = TopSolidHost.Geometries3D.GetFrames(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("3D Frames: " + frames.Count);
foreach (var f in frames)
    sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(f));
return sb.ToString();
```

---

## list_sketches
Pattern: R
Description: Lists sketches of the document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var sketches = TopSolidHost.Sketches2D.GetSketches(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Sketches: " + sketches.Count);
foreach (var s in sketches)
    sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(s));
return sb.ToString();
```

---

## read_shapes
Pattern: R
Description: Lists shapes of the document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var shapes = TopSolidHost.Shapes.GetShapes(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Shapes: " + shapes.Count);
foreach (var s in shapes)
{
    string name = TopSolidHost.Elements.GetFriendlyName(s);
    int faceCount = TopSolidHost.Shapes.GetFaceCount(s);
    sb.AppendLine("  " + name + " (" + faceCount + " faces)");
}
return sb.ToString();
```

---

## read_operations
Pattern: R
Description: Lists operations (feature tree)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var ops = TopSolidHost.Operations.GetOperations(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Operations: " + ops.Count);
foreach (var op in ops)
{
    string name = TopSolidHost.Elements.GetFriendlyName(op);
    string typeName = TopSolidHost.Elements.GetTypeFullName(op);
    string shortType = typeName.Substring(typeName.LastIndexOf('.') + 1);
    sb.AppendLine("  " + name + " [" + shortType + "]");
}
return sb.ToString();
```

---

## detect_assembly
Pattern: R
Description: Detects if the document is an assembly and lists parts

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isAsm = false;
try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return "Unable to verify (not a Design document)."; }
if (!isAsm) return "This document is NOT an assembly.";
var parts = TopSolidDesignHost.Assemblies.GetParts(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Assembly: " + parts.Count + " pieces");
foreach (var p in parts)
    sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(p));
return sb.ToString();
```

---

## list_inclusions
Pattern: R
Description: Lists inclusions of an assembly

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var ops = TopSolidHost.Operations.GetOperations(docId);
var sb = new System.Text.StringBuilder();
int count = 0;
foreach (var op in ops)
{
    bool isInclusion = false;
    try { isInclusion = TopSolidDesignHost.Assemblies.IsInclusion(op); } catch { continue; }
    if (isInclusion)
    {
        string name = TopSolidHost.Elements.GetFriendlyName(op);
        DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(op);
        string defName = "?";
        if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }
        sb.AppendLine("  " + name + " → " + defName);
        count++;
    }
}
sb.Insert(0, "Inclusions: " + count + "\
");
return sb.ToString();
```

---

## detect_family
Pattern: R
Description: Detects if the document is a family

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isFamily = TopSolidHost.Families.IsFamily(docId);
if (!isFamily) return "This document is NOT a family.";
bool isExplicit = TopSolidHost.Families.IsExplicit(docId);
return "Family detected (" + (isExplicit ? "explicit" : "implicit") + ").";
```

---

## read_family_codes
Pattern: R
Description: Reads family codes

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
if (!TopSolidHost.Families.IsFamily(docId)) return "Not a family.";
var codes = TopSolidHost.Families.GetCodes(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Codes: " + codes.Count);
foreach (var c in codes)
    sb.AppendLine("  " + c);
return sb.ToString();
```

---

## open_drafting
Pattern: R
Description: Finds and opens the drafting associated with the current part/assembly via PDM back-references

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);
PdmMajorRevisionId majorRev = TopSolidHost.Pdm.GetLastMajorRevision(pdmId);
var backRefs = TopSolidHost.Pdm.SearchMajorRevisionBackReferences(projId, majorRev);
foreach (var backRef in backRefs)
{
    PdmMajorRevisionId brMajor = TopSolidHost.Pdm.GetMajorRevision(backRef);
    PdmObjectId brObj = TopSolidHost.Pdm.GetPdmObject(brMajor);
    string brType = "";
    TopSolidHost.Pdm.GetType(brObj, out brType);
    if (brType == ".TopDft")
    {
        string name = TopSolidHost.Pdm.GetName(brObj);
        DocumentId draftDoc = TopSolidHost.Documents.GetDocument(brObj);
        TopSolidHost.Documents.Open(ref draftDoc);
        return "Drafting opened: " + name;
    }
}
return "No drafting found for this document.";
```

---

## detect_drafting
Pattern: R
Description: Detects if the document is a drafting and provides info

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return "Unable to verify."; }
if (!isDrafting) return "This document is NOT a drafting.";
var sb = new System.Text.StringBuilder();
sb.AppendLine("Drafting detected.");
int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);
sb.AppendLine("Pages: " + pages);
string format = TopSolidDraftingHost.Draftings.GetDraftingFormatName(docId);
sb.AppendLine("Format: " + format);
return sb.ToString();
```

---

## list_drafting_views
Pattern: R
Description: Lists views of a drafting

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return "Not a drafting."; }
if (!isDrafting) return "Not a drafting.";
var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Views: " + views.Count);
foreach (var v in views)
{
    string title = TopSolidDraftingHost.Draftings.GetViewTitle(v);
    string name = TopSolidHost.Elements.GetFriendlyName(v);
    sb.AppendLine("  " + name + " - " + title);
}
return sb.ToString();
```

---

## detect_bom
Pattern: R
Description: Detects if the document is a BOM

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return "Unable to verify."; }
if (!isBom) return "This document is NOT a BOM.";
int cols = TopSolidDesignHost.Boms.GetColumnCount(docId);
return "BOM detected (" + cols + " columns).";
```

---

## read_bom_columns
Pattern: R
Description: Reads BOM columns

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return "Not a BOM."; }
if (!isBom) return "Not a BOM.";
int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Columns: " + colCount);
for (int i = 0; i < colCount; i++)
{
    string title = TopSolidDesignHost.Boms.GetColumnTitle(docId, i);
    bool visible = TopSolidDesignHost.Boms.IsColumnVisible(docId, i);
    sb.AppendLine("  [" + i + "] " + title + (visible ? "" : " (masquee)"));
}
return sb.ToString();
```

---

## read_drafting_scale
Pattern: R
Description: Reads global and per-view scale of a drafting

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return "Not a drafting."; }
if (!isDrafting) return "Not a drafting.";
var sb = new System.Text.StringBuilder();
double globalScale = TopSolidDraftingHost.Draftings.GetScaleFactorParameterValue(docId);
sb.AppendLine("Global scale: 1:" + (1.0/globalScale).ToString("F0"));
var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);
foreach (var v in views)
{
    string name = TopSolidHost.Elements.GetFriendlyName(v);
    bool isRel; double rel; double abs; double refVal;
    TopSolidDraftingHost.Draftings.GetViewScaleFactor(v, out isRel, out rel, out abs, out refVal);
    sb.AppendLine("  " + name + ": 1:" + (1.0/abs).ToString("F0") + (isRel ? " (relative)" : ""));
}
return sb.ToString();
```

---

## read_drafting_format
Pattern: R
Description: Reads the format (size, margins) of a drafting

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return "Not a drafting."; }
if (!isDrafting) return "Not a drafting.";
var sb = new System.Text.StringBuilder();
string format = TopSolidDraftingHost.Draftings.GetDraftingFormatName(docId);
sb.AppendLine("Format: " + format);
double w; double h;
TopSolidDraftingHost.Draftings.GetDraftingFormatDimensions(docId, out w, out h);
sb.AppendLine("Dimensions: " + (w*1000).ToString("F0") + " x " + (h*1000).ToString("F0") + " mm");
int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);
sb.AppendLine("Pages: " + pages);
var mode = TopSolidDraftingHost.Draftings.GetProjectionMode(docId);
sb.AppendLine("Projection mode: " + mode);
return sb.ToString();
```

---

## read_main_projection
Pattern: R
Description: Reads the main projection of a drafting

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return "Not a drafting."; }
if (!isDrafting) return "Not a drafting.";
var sb = new System.Text.StringBuilder();
DocumentId mainDocId; ElementId repId;
TopSolidDraftingHost.Draftings.GetMainProjectionSet(docId, out mainDocId, out repId);
if (!mainDocId.IsEmpty)
{
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(mainDocId);
    string srcName = TopSolidHost.Pdm.GetName(pdm);
    sb.AppendLine("Source part: " + srcName);
}
var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);
sb.AppendLine("Views: " + views.Count);
foreach (var v in views)
{
    string title = TopSolidDraftingHost.Draftings.GetViewTitle(v);
    string name = TopSolidHost.Elements.GetFriendlyName(v);
    ElementId mainView = TopSolidDraftingHost.Draftings.GetMainView(v);
    bool isMain = (mainView.Equals(v));
    sb.AppendLine("  " + name + (isMain ? " [MAIN]" : " [auxiliary]") + " - " + title);
}
return sb.ToString();
```

---

## read_bom_contents
Pattern: R
Description: Reads the full BOM contents (rows and cells)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return "Not a BOM."; }
if (!isBom) return "Not a BOM.";
int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);
var sb = new System.Text.StringBuilder();
var headers = new List<string>();
for (int c = 0; c < colCount; c++) headers.Add(TopSolidDesignHost.Boms.GetColumnTitle(docId, c));
sb.AppendLine(string.Join(" | ", headers));
sb.AppendLine(new string('-', 60));
int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);
var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);
foreach (int rowId in children)
{
    if (!TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) continue;
    List<TopSolid.Kernel.SX.Types.Property> props; List<string> texts;
    TopSolidDesignHost.Boms.GetRowContents(docId, rowId, out props, out texts);
    sb.AppendLine(string.Join(" | ", texts));
}
return sb.ToString();
```

---

## count_bom_rows
Pattern: R
Description: Counts active BOM rows

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return "Not a BOM."; }
if (!isBom) return "Not a BOM.";
int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);
var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);
int active = 0; int inactive = 0;
foreach (int rowId in children)
{
    if (TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) active++;
    else inactive++;
}
return "Active rows: " + active + ", inactive: " + inactive + ", total: " + (active+inactive);
```

---

## detect_unfolding
Pattern: R
Description: Detects if the document is an unfolding (sheet metal)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isUnfolding = false;
try { isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); } catch { return "Unable to verify."; }
if (!isUnfolding) return "This document is NOT an unfolding.";
var sb = new System.Text.StringBuilder();
sb.AppendLine("Unfolding (flat pattern) detected.");
DocumentId partDoc; ElementId repId; ElementId shapeId;
TopSolidDesignHost.Unfoldings.GetPartToUnfold(docId, out partDoc, out repId, out shapeId);
if (!partDoc.IsEmpty)
{
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(partDoc);
    sb.AppendLine("Source part: " + TopSolidHost.Pdm.GetName(pdm));
}
return sb.ToString();
```

---

## read_bend_features
Pattern: R
Description: Lists bends of an unfolding (angles, radii, lengths)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isUnfolding = false;
try { isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); } catch { return "Not an unfolding."; }
if (!isUnfolding) return "Not an unfolding.";
List<TopSolid.Cad.Design.DB.Documents.BendFeature> bends;
TopSolidDesignHost.Unfoldings.GetBendFeatures(docId, out bends);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Bends: " + bends.Count);
foreach (var b in bends)
    sb.AppendLine("  Pli: angle=" + (b.Angle*180/3.14159).ToString("F1") + "deg, radius=" + (b.Radius*1000).ToString("F2") + "mm, length=" + (b.Length*1000).ToString("F2") + "mm");
return sb.ToString();
```

---

## read_unfolding_dimensions
Pattern: R
Description: Reads unfolding dimensions from system properties (sheet metal)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== UNFOLDING ===");
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.Contains("Unfolding") || name == "Sheet Metal" || name == "Thickness" ||
        name == "Bends Number" || name == "Unfoldable Shape")
    {
        var pType = TopSolidHost.Parameters.GetParameterType(p);
        if (pType == ParameterType.Real)
        {
            double val = TopSolidHost.Parameters.GetRealValue(p);
            if (name.Contains("Area") || name.Contains("Perimeter") || name.Contains("Width") || name.Contains("Length"))
                sb.AppendLine("  " + name + ": " + (val * 1000).ToString("F1") + " mm");
            else
                sb.AppendLine("  " + name + ": " + val.ToString("F4"));
        }
        else if (pType == ParameterType.Boolean)
            sb.AppendLine("  " + name + ": " + TopSolidHost.Parameters.GetBooleanValue(p));
        else if (pType == ParameterType.Integer)
            sb.AppendLine("  " + name + ": " + TopSolidHost.Parameters.GetIntegerValue(p));
    }
}
if (sb.Length <= 18) return "No unfolding properties found.";
return sb.ToString();
```

---

## set_drafting_scale
Pattern: RW
Description: Sets the global scale of a drafting. Param: value=denominator (e.g. '10' for 1:10)

```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = "Not a drafting."; return; }
if (!isDrafting) { __message = "Not a drafting."; return; }
double denom;
if (!double.TryParse("{value}", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out denom) || denom <= 0)
{ __message = "ERROR: value must be a positive number (e.g. '10' for 1:10)."; return; }
double factor = 1.0 / denom;
TopSolidDraftingHost.Draftings.SetScaleFactorParameterValue(docId, factor);
__message = "OK: drafting scale set to 1:" + denom;
```

---

## set_drafting_format
Pattern: RW
Description: Sets the drafting format (paper size). Param: value=format_name (e.g. 'A3', 'A4')

```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = "Not a drafting."; return; }
if (!isDrafting) { __message = "Not a drafting."; return; }
string fmt = "{value}";
if (string.IsNullOrWhiteSpace(fmt)) { __message = "ERROR: value required (e.g. 'A3')."; return; }
TopSolidDraftingHost.Draftings.SetDraftingFormatName(docId, fmt);
__message = "OK: drafting format set to " + fmt;
```

---

## set_projection_quality
Pattern: RW
Description: Sets the drafting projection quality. Param: value='exact' (precise) or 'fast' (quick)

```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = "Not a drafting."; return; }
if (!isDrafting) { __message = "Not a drafting."; return; }
string mode = ("{value}" ?? "").Trim().ToLowerInvariant();
TopSolid.Cad.Drafting.Automating.ProjectionMode pm;
if (mode == "exact" || mode == "precise" || mode == "precis") pm = TopSolid.Cad.Drafting.Automating.ProjectionMode.Exact;
else if (mode == "fast" || mode == "quick" || mode == "rapide") pm = TopSolid.Cad.Drafting.Automating.ProjectionMode.Fast;
else { __message = "ERROR: value must be 'exact' or 'fast'."; return; }
TopSolidDraftingHost.Draftings.SetProjectionMode(docId, pm);
__message = "OK: projection quality set to " + pm;
```

---

## print_drafting
Pattern: R
Description: Prints the current drafting (all pages, black & white, 300 DPI, printed to scale)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isDrafting = false;
try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return "Not a drafting."; }
if (!isDrafting) return "Not a drafting.";
int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);
try {
    TopSolidDraftingHost.Draftings.Print(docId,
        TopSolid.Cad.Drafting.Automating.PrintMode.PrintToScale,
        TopSolid.Cad.Drafting.Automating.PrintColorMapping.BlackAndWhite,
        300, 1, pages);
    return "OK: print job sent (" + pages + " pages, B&W, 300 DPI, to scale).";
} catch (Exception ex) {
    return "ERROR: " + ex.Message;
}
```

---

## activate_bom_row
Pattern: RW
Description: Activates a BOM row by its index (0-based, among root-children). Param: value=row_index

```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { __message = "Not a BOM."; return; }
if (!isBom) { __message = "Not a BOM."; return; }
int idx;
if (!int.TryParse("{value}", out idx) || idx < 0) { __message = "ERROR: value must be a non-negative integer."; return; }
int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);
var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);
if (idx >= children.Count) { __message = "ERROR: row_index out of range (0.." + (children.Count-1) + ")."; return; }
int rowId = children[idx];
TopSolidDesignHost.Boms.ActivateRow(docId, rowId);
__message = "OK: BOM row " + idx + " activated.";
```

---

## deactivate_bom_row
Pattern: RW
Description: Deactivates a BOM row by its index (0-based, among root-children). Param: value=row_index

```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { __message = "Not a BOM."; return; }
if (!isBom) { __message = "Not a BOM."; return; }
int idx;
if (!int.TryParse("{value}", out idx) || idx < 0) { __message = "ERROR: value must be a non-negative integer."; return; }
int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);
var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);
if (idx >= children.Count) { __message = "ERROR: row_index out of range (0.." + (children.Count-1) + ")."; return; }
int rowId = children[idx];
TopSolidDesignHost.Boms.DeactivateRow(docId, rowId);
__message = "OK: BOM row " + idx + " deactivated.";
```

---

## list_exporters
Pattern: R
Description: Lists all available exporters

```csharp
var sb = new System.Text.StringBuilder();
int count = TopSolidHost.Application.ExporterCount;
sb.AppendLine("Exporters: " + count);
for (int i = 0; i < count; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    sb.AppendLine(i + ": " + typeName + " [" + string.Join(", ", extensions) + "]");
}
return sb.ToString();
```

---

## export_step
Pattern: R
Description: Exporte en STEP. Param: value=chemin (ex: C:\\temp\\piece.stp)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
int count = TopSolidHost.Application.ExporterCount;
int idx = -1;
for (int i = 0; i < count; i++)
{
    string typeName; string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
        if (ext.ToLower().Contains("stp") || ext.ToLower().Contains("step")) { idx = i; break; }
    if (idx >= 0) break;
}
if (idx < 0) return "STEP exporter not found.";
string path = "{value}";
if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "export.stp");
TopSolidHost.Documents.Export(idx, docId, path);
return "OK: Exported to STEP → " + path;
```

---

## export_dxf
Pattern: R
Description: Exports to DXF. Param: value=path

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
int count = TopSolidHost.Application.ExporterCount;
int idx = -1;
for (int i = 0; i < count; i++)
{
    string typeName; string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
        if (ext.ToLower().Contains("dxf")) { idx = i; break; }
    if (idx >= 0) break;
}
if (idx < 0) return "DXF exporter not found.";
string path = "{value}";
if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "export.dxf");
TopSolidHost.Documents.Export(idx, docId, path);
return "OK: Exported to DXF → " + path;
```

---

## export_pdf
Pattern: R
Description: Exports to PDF. Param: value=path

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
int count = TopSolidHost.Application.ExporterCount;
int idx = -1;
for (int i = 0; i < count; i++)
{
    string typeName; string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
        if (ext.ToLower().Contains("pdf")) { idx = i; break; }
    if (idx >= 0) break;
}
if (idx < 0) return "PDF exporter not found.";
string path = "{value}";
if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "export.pdf");
TopSolidHost.Documents.Export(idx, docId, path);
return "OK: Exported to PDF → " + path;
```

---

## read_user_property
Pattern: R
Description: Reads a text user property. Param: value=property_name

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string val = TopSolidHost.Pdm.GetTextUserProperty(pdmId, "{value}");
return string.IsNullOrEmpty(val) ? "Propriete '{value}': (empty)" : "Propriete '{value}': " + val;
```

---

## audit_part
Pattern: R
Description: Full part audit: properties, parameters, shapes, mass, volume

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== PART AUDIT ===");
sb.AppendLine("Name: " + TopSolidHost.Pdm.GetName(pdmId));
string desc = TopSolidHost.Pdm.GetDescription(pdmId);
sb.AppendLine("Designation: " + (string.IsNullOrEmpty(desc) ? "(EMPTY!)" : desc));
string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);
sb.AppendLine("Reference: " + (string.IsNullOrEmpty(pn) ? "(EMPTY!)" : pn));
sb.AppendLine("Type: " + TopSolidHost.Documents.GetTypeFullName(docId));
// Parametres
var pList = TopSolidHost.Parameters.GetParameters(docId);
sb.AppendLine("\
Parameters: " + pList.Count);
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    var pType = TopSolidHost.Parameters.GetParameterType(p);
    string val = "";
    if (pType == ParameterType.Real) val = (TopSolidHost.Parameters.GetRealValue(p) * 1000).ToString("F2") + " mm";
    else if (pType == ParameterType.Integer) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    else if (pType == ParameterType.Boolean) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
    else if (pType == ParameterType.Text) val = TopSolidHost.Parameters.GetTextValue(p);
    sb.AppendLine("  " + name + " = " + val);
}
// Shapes
var shapes = TopSolidHost.Shapes.GetShapes(docId);
sb.AppendLine("\
Shapes: " + shapes.Count);
foreach (var s in shapes)
{
    string sName = TopSolidHost.Elements.GetFriendlyName(s);
    double vol = TopSolidHost.Shapes.GetShapeVolume(s);
    int faces = TopSolidHost.Shapes.GetFaceCount(s);
    sb.AppendLine("  " + sName + " : " + faces + " faces, volume=" + (vol * 1e9).ToString("F1") + " cm3");
}
return sb.ToString();
```

---

## audit_assembly
Pattern: R
Description: Full assembly audit: parts, inclusions, mass

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isAsm = false;
try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return "Unable to verify."; }
if (!isAsm) return "This document is NOT an assembly.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== ASSEMBLY AUDIT ===");
sb.AppendLine("Name: " + TopSolidHost.Pdm.GetName(pdmId));
string desc = TopSolidHost.Pdm.GetDescription(pdmId);
sb.AppendLine("Designation: " + (string.IsNullOrEmpty(desc) ? "(EMPTY!)" : desc));
// Pieces
var parts = TopSolidDesignHost.Assemblies.GetParts(docId);
sb.AppendLine("\
Parts: " + parts.Count);
// Inclusions
var ops = TopSolidHost.Operations.GetOperations(docId);
int inclCount = 0;
foreach (var op in ops)
{
    bool isInclusion = false;
    try { isInclusion = TopSolidDesignHost.Assemblies.IsInclusion(op); } catch { continue; }
    if (isInclusion)
    {
        string opName = TopSolidHost.Elements.GetFriendlyName(op);
        DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(op);
        string defName = "?";
        if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }
        sb.AppendLine("  " + opName + " -> " + defName);
        inclCount++;
    }
}
sb.Insert(sb.ToString().IndexOf("\
Pieces"), "Inclusions: " + inclCount + "\
");
return sb.ToString();
```

---

## check_part
Pattern: R
Description: Quality check: designation, reference, material filled?

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== QUALITY CHECK ===");
int warnings = 0;
string desc = TopSolidHost.Pdm.GetDescription(pdmId);
if (string.IsNullOrEmpty(desc)) { sb.AppendLine("WARNING: Designation EMPTY"); warnings++; } else sb.AppendLine("OK: Designation = " + desc);
string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);
if (string.IsNullOrEmpty(pn)) { sb.AppendLine("WARNING: Reference EMPTY"); warnings++; } else sb.AppendLine("OK: Reference = " + pn);
string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);
if (string.IsNullOrEmpty(mfr)) sb.AppendLine("INFO: Manufacturer not set");
// Parametres
var pList = TopSolidHost.Parameters.GetParameters(docId);
sb.AppendLine("Parameters: " + pList.Count);
if (pList.Count == 0) { sb.AppendLine("WARNING: No parameters"); warnings++; }
// Shapes
var shapes = TopSolidHost.Shapes.GetShapes(docId);
if (shapes.Count == 0) { sb.AppendLine("WARNING: No shape (empty part?)"); warnings++; } else sb.AppendLine("OK: " + shapes.Count + " shape(s)");
sb.AppendLine("\
" + (warnings == 0 ? "RESULT: Part OK" : "RESULT: " + warnings + " warning(s)"));
return sb.ToString();
```

---

## read_mass_volume
Pattern: R
Description: Reads mass, volume, surface from document system properties

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Mass" || name == "Volume" || name == "Surface Area")
    {
        double val = TopSolidHost.Parameters.GetRealValue(p);
        if (name == "Mass") sb.AppendLine("Mass: " + val.ToString("F3") + " kg");
        else if (name == "Volume") sb.AppendLine("Volume: " + (val * 1e9).ToString("F2") + " mm3");
        else sb.AppendLine("Surface: " + (val * 1e6).ToString("F2") + " mm2");
    }
}
if (sb.Length == 0) return "No physical properties found.";
return sb.ToString();
```

---

## read_material_density
Pattern: R
Description: Calculates density from document mass/volume

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
double mass = 0; double vol = 0;
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Mass") mass = TopSolidHost.Parameters.GetRealValue(p);
    else if (name == "Volume") vol = TopSolidHost.Parameters.GetRealValue(p);
}
if (vol > 0 && mass > 0) return "Density: " + (mass / vol).ToString("F0") + " kg/m3 (mass=" + mass.ToString("F3") + "kg, vol=" + (vol*1e6).ToString("F2") + "cm3)";
return "Mass or volume not available.";
```

---

## invoke_command
Pattern: R
Description: Executes a TopSolid menu command by name. Param: value=command_name

```csharp
bool result = TopSolidHost.Application.InvokeCommand("{value}");
return result ? "OK: Command '{value}' executee." : "ERREUR: Commande '{value}' non trouvee ou echec.";
```

---

## read_occurrences
Pattern: R
Description: Lists occurrences of an assembly with their definition

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isAsm = false;
try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return "Not an assembly."; }
if (!isAsm) return "Not an assembly.";
var parts = TopSolidDesignHost.Assemblies.GetParts(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Occurrences: " + parts.Count);
foreach (var p in parts)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    bool isOcc = TopSolidHost.Entities.IsOccurrence(p);
    string occName = "";
    try { occName = TopSolidHost.Entities.GetFunctionOccurrenceName(p); } catch {}
    DocumentId defDoc = DocumentId.Empty;
    try { defDoc = TopSolidDesignHost.Assemblies.GetOccurrenceDefinition(p); } catch {}
    string defName = "";
    if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }
    sb.AppendLine("  " + name + (isOcc ? " [occ]" : "") + (!string.IsNullOrEmpty(occName) ? " nom=" + occName : "") + (!string.IsNullOrEmpty(defName) ? " -> " + defName : ""));
}
return sb.ToString();
```

---

## rename_occurrence
Pattern: RW
Description: Renames an occurrence. Param: value=old_name:new_name

```csharp
int idx = "{value}".IndexOf(':');
if (idx < 0) { __message = "Format: old_name:new_name"; return; }
string oldName = "{value}".Substring(0, idx).Trim();
string newName = "{value}".Substring(idx + 1).Trim();
var parts = TopSolidDesignHost.Assemblies.GetParts(docId);
foreach (var p in parts)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) >= 0)
    {
        TopSolidHost.Entities.SetFunctionOccurrenceName(p, newName);
        __message = "OK: Occurrence '" + name + "' renamed to '" + newName + "'";
        return;
    }
}
__message = "Occurrence '" + oldName + "' not found.";
```

---

## set_user_property
Pattern: R
Description: Sets a text user property. Param: value=property_name:value

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
int idx = "{value}".IndexOf(':');
if (idx < 0) return "Format: property_name:value";
string propName = "{value}".Substring(0, idx).Trim();
string propVal = "{value}".Substring(idx + 1).Trim();
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.SetTextUserProperty(pdmId, propName, propVal);
TopSolidHost.Pdm.Save(pdmId, true);
return "OK: Property '" + propName + "' = '" + propVal + "'";
```

---

## read_bounding_box
Pattern: R
Description: Reads bounding box dimensions from system properties

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Bounding box:");
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Box X Size" || name == "Box Y Size" || name == "Box Z Size")
    {
        double val = TopSolidHost.Parameters.GetRealValue(p);
        sb.AppendLine("  " + name + ": " + (val * 1000).ToString("F1") + " mm");
    }
}
if (sb.Length <= 22)
{
    try
    {
        ElementId xSize, ySize, zSize;
        TopSolidDesignHost.Parts.GetEnclosingBoxParameters(docId, out xSize, out ySize, out zSize);
        if (!xSize.IsEmpty) sb.AppendLine("  X: " + (TopSolidHost.Parameters.GetRealValue(xSize) * 1000).ToString("F1") + " mm");
        if (!ySize.IsEmpty) sb.AppendLine("  Y: " + (TopSolidHost.Parameters.GetRealValue(ySize) * 1000).ToString("F1") + " mm");
        if (!zSize.IsEmpty) sb.AppendLine("  Z: " + (TopSolidHost.Parameters.GetRealValue(zSize) * 1000).ToString("F1") + " mm");
    }
    catch {}
}
if (sb.Length <= 22) return "No bounding box dimensions found.";
return sb.ToString();
```

---

## read_part_dimensions
Pattern: R
Description: Reads dimensions (Height, Width, Length, Box Size) from system properties

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== DIMENSIONS ===");
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Height" || name == "Width" || name == "Length" ||
        name == "Box X Size" || name == "Box Y Size" || name == "Box Z Size" ||
        name == "Box X marged size" || name == "Box Y marged size" || name == "Box Z marged size" ||
        name == "Thickness")
    {
        double val = TopSolidHost.Parameters.GetRealValue(p);
        if (val > 0) sb.AppendLine("  " + name + ": " + (val * 1000).ToString("F1") + " mm");
    }
}
if (sb.Length <= 22) return "No dimensions found (not a part?)";
return sb.ToString();
```

---

## read_inertia_moments
Pattern: R
Description: Reads principal inertia moments from system properties

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== INERTIA MOMENTS ===");
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Principal X Moment" || name == "Principal Y Moment" || name == "Principal Z Moment")
    {
        double val = TopSolidHost.Parameters.GetRealValue(p);
        sb.AppendLine("  " + name + ": " + val.ToString("F6") + " kg.mm2");
    }
}
if (sb.Length <= 28) return "No inertia moments found.";
return sb.ToString();
```

---

## list_project_documents
Pattern: R
Description: Lists ALL project documents with designation and reference

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var items = docs;
var sb = new System.Text.StringBuilder();
sb.AppendLine("Project: " + TopSolidHost.Pdm.GetName(projId));
int docCount = 0;
foreach (var item in items)
{
    string name = TopSolidHost.Pdm.GetName(item);
    string desc = TopSolidHost.Pdm.GetDescription(item);
    string pn = TopSolidHost.Pdm.GetPartNumber(item);
    sb.AppendLine("  " + name + " | Designation: " + (string.IsNullOrEmpty(desc) ? "-" : desc) + " | Ref: " + (string.IsNullOrEmpty(pn) ? "-" : pn));
    docCount++;
}
sb.Insert(0, "Documents: " + docCount + "\
");
return sb.ToString();
```

---

## check_project
Pattern: R
Description: Full project quality check: parts without designation/reference

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var items = docs;
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== PROJECT CHECK ===");
sb.AppendLine("Project: " + TopSolidHost.Pdm.GetName(projId));
int total = 0; int alertes = 0;
foreach (var item in items)
{
    string name = TopSolidHost.Pdm.GetName(item);
    string desc = TopSolidHost.Pdm.GetDescription(item);
    string pn = TopSolidHost.Pdm.GetPartNumber(item);
    total++;
    if (string.IsNullOrEmpty(desc) || string.IsNullOrEmpty(pn))
    {
        sb.AppendLine("  ALERTE: " + name + (string.IsNullOrEmpty(desc) ? " [designation empty]" : "") + (string.IsNullOrEmpty(pn) ? " [reference empty]" : ""));
        alertes++;
    }
}
sb.AppendLine("\
Total: " + total + " documents, " + alertes + " warning(s)");
sb.AppendLine(alertes == 0 ? "RESULTAT: Projet OK" : "RESULT: " + alertes + " document(s) to complete");
return sb.ToString();
```

---

## compare_parameters
Pattern: R
Description: Compares parameters of the active document with another. Param: value=other_document_name

```csharp
DocumentId curDoc = TopSolidHost.Documents.EditedDocument;
if (curDoc.IsEmpty) return "No document open.";
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
if (results.Count == 0) return "Document '{value}' not found.";
DocumentId otherDocId = TopSolidHost.Documents.GetDocument(results[0]);
var paramsA = TopSolidHost.Parameters.GetParameters(curDoc);
var paramsB = TopSolidHost.Parameters.GetParameters(otherDocId);
var sb = new System.Text.StringBuilder();
string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(curDoc));
string nameB = TopSolidHost.Pdm.GetName(results[0]);
sb.AppendLine("=== COMPARISON ===");
sb.AppendLine(nameA + " vs " + nameB);
// Index params B by name
var dictB = new System.Collections.Generic.Dictionary<string, string>();
foreach (var p in paramsB)
{
    string n = TopSolidHost.Elements.GetFriendlyName(p);
    var t = TopSolidHost.Parameters.GetParameterType(p);
    string v = "";
    if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString("F6");
    else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p);
    else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    dictB[n] = v;
}
int diffs = 0;
foreach (var p in paramsA)
{
    string n = TopSolidHost.Elements.GetFriendlyName(p);
    var t = TopSolidHost.Parameters.GetParameterType(p);
    string vA = "";
    if (t == ParameterType.Real) vA = TopSolidHost.Parameters.GetRealValue(p).ToString("F6");
    else if (t == ParameterType.Text) vA = TopSolidHost.Parameters.GetTextValue(p);
    else if (t == ParameterType.Integer) vA = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    string vB;
    if (dictB.TryGetValue(n, out vB))
    {
        if (vA != vB) { sb.AppendLine("  DIFF: " + n + " : " + vA + " vs " + vB); diffs++; }
        dictB.Remove(n);
    }
    else { sb.AppendLine("  ONLY A: " + n + " = " + vA); diffs++; }
}
foreach (var kvp in dictB)
    { sb.AppendLine("  ONLY B: " + kvp.Key + " = " + kvp.Value); diffs++; }
sb.AppendLine("\
" + diffs + " difference(s)");
return sb.ToString();
```

---

## compare_document_operations
Pattern: R
Description: Compares operations (feature tree) between the current and another document. Param: value=other_doc_name

```csharp
DocumentId docIdA = TopSolidHost.Documents.EditedDocument;
if (docIdA.IsEmpty) return "No document open.";
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
if (results.Count == 0) return "Document '{value}' not found.";
DocumentId docIdB = TopSolidHost.Documents.GetDocument(results[0]);
var opsA = TopSolidHost.Operations.GetOperations(docIdA);
var opsB = TopSolidHost.Operations.GetOperations(docIdB);
string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docIdA));
string nameB = TopSolidHost.Pdm.GetName(results[0]);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== OPERATIONS COMPARISON ===");
sb.AppendLine(nameA + " (" + opsA.Count + " ops) vs " + nameB + " (" + opsB.Count + " ops)");
int max = System.Math.Max(opsA.Count, opsB.Count);
for (int i = 0; i < max; i++)
{
    string nA = i < opsA.Count ? TopSolidHost.Elements.GetFriendlyName(opsA[i]) : "--";
    string nB = i < opsB.Count ? TopSolidHost.Elements.GetFriendlyName(opsB[i]) : "--";
    string marker = (nA == nB) ? "  =" : "  *";
    sb.AppendLine(marker + " [" + i + "] " + nA + " | " + nB);
}
return sb.ToString();
```

---

## compare_document_entities
Pattern: R
Description: Compares entities (shapes, sketches, points, frames) between the current and another document. Param: value=other_doc_name

```csharp
DocumentId docIdA = TopSolidHost.Documents.EditedDocument;
if (docIdA.IsEmpty) return "No document open.";
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
if (results.Count == 0) return "Document '{value}' not found.";
DocumentId docIdB = TopSolidHost.Documents.GetDocument(results[0]);
string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docIdA));
string nameB = TopSolidHost.Pdm.GetName(results[0]);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== ENTITIES COMPARISON ===");
sb.AppendLine(nameA + " vs " + nameB);
// Shapes
int shA = TopSolidHost.Shapes.GetShapes(docIdA).Count;
int shB = TopSolidHost.Shapes.GetShapes(docIdB).Count;
sb.AppendLine((shA==shB?"  =":"  *") + " Shapes: " + shA + " vs " + shB);
// Esquisses
int skA = TopSolidHost.Sketches2D.GetSketches(docIdA).Count;
int skB = TopSolidHost.Sketches2D.GetSketches(docIdB).Count;
sb.AppendLine((skA==skB?"  =":"  *") + " Esquisses: " + skA + " vs " + skB);
// Points 3D
int ptA = TopSolidHost.Geometries3D.GetPoints(docIdA).Count;
int ptB = TopSolidHost.Geometries3D.GetPoints(docIdB).Count;
sb.AppendLine((ptA==ptB?"  =":"  *") + " Points 3D: " + ptA + " vs " + ptB);
// Reperes
int frA = TopSolidHost.Geometries3D.GetFrames(docIdA).Count;
int frB = TopSolidHost.Geometries3D.GetFrames(docIdB).Count;
sb.AppendLine((frA==frB?"  =":"  *") + " Reperes: " + frA + " vs " + frB);
// Operations
int opA = TopSolidHost.Operations.GetOperations(docIdA).Count;
int opB = TopSolidHost.Operations.GetOperations(docIdB).Count;
sb.AppendLine((opA==opB?"  =":"  *") + " Operations: " + opA + " vs " + opB);
// Parametres
int paA = TopSolidHost.Parameters.GetParameters(docIdA).Count;
int paB = TopSolidHost.Parameters.GetParameters(docIdB).Count;
sb.AppendLine((paA==paB?"  =":"  *") + " Parameters: " + paA + " vs " + paB);
return sb.ToString();
```

---

## copy_parameters_to
Pattern: RW
Description: Copies parameter values from the current document to another. Param: value=target_doc_name

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;
if (srcDocId.IsEmpty) { __message = "No document open."; return; }
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
if (results.Count == 0) { __message = "Document '{value}' not found."; return; }
// Lire les params source
var srcParams = TopSolidHost.Parameters.GetParameters(srcDocId);
var srcDict = new System.Collections.Generic.Dictionary<string, object[]>();
foreach (var p in srcParams)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.Contains("$") || name == "Mass" || name == "Volume" || name == "Surface Area") continue;
    var t = TopSolidHost.Parameters.GetParameterType(p);
    if (t == ParameterType.Real)
        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetRealValue(p) };
    else if (t == ParameterType.Text)
        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetTextValue(p) };
    else if (t == ParameterType.Integer)
        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetIntegerValue(p) };
    else if (t == ParameterType.Boolean)
        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetBooleanValue(p) };
}
// Appliquer sur le doc cible
DocumentId tgtDocId = TopSolidHost.Documents.GetDocument(results[0]);
TopSolidHost.Documents.EnsureIsDirty(ref tgtDocId);
var tgtParams = TopSolidHost.Parameters.GetParameters(tgtDocId);
int applied = 0; int skipped = 0;
foreach (var p in tgtParams)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    object[] src;
    if (!srcDict.TryGetValue(name, out src)) continue;
    var t = (ParameterType)src[0];
    try
    {
        if (t == ParameterType.Real) TopSolidHost.Parameters.SetRealValue(p, (double)src[1]);
        else if (t == ParameterType.Text) TopSolidHost.Parameters.SetTextValue(p, (string)src[1]);
        else if (t == ParameterType.Integer) TopSolidHost.Parameters.SetIntegerValue(p, (int)src[1]);
        else if (t == ParameterType.Boolean) TopSolidHost.Parameters.SetBooleanValue(p, (bool)src[1]);
        applied++;
    }
    catch { skipped++; }
}
string tgtName = TopSolidHost.Pdm.GetName(results[0]);
__message = "OK: " + applied + " parameters copied to '" + tgtName + "' (" + skipped + " skipped).";
```

---

## copy_pdm_properties_to
Pattern: RW
Description: Copies designation/reference/manufacturer from current to another document. Param: value=target_doc_name

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;
if (srcDocId.IsEmpty) { __message = "No document open."; return; }
PdmObjectId srcPdmId = TopSolidHost.Documents.GetPdmObject(srcDocId);
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
if (results.Count == 0) { __message = "Document '{value}' not found."; return; }
PdmObjectId tgtPdmId = results[0];
// Lire source
string desc = TopSolidHost.Pdm.GetDescription(srcPdmId);
string pn = TopSolidHost.Pdm.GetPartNumber(srcPdmId);
string mfr = TopSolidHost.Pdm.GetManufacturer(srcPdmId);
string mfrPn = TopSolidHost.Pdm.GetManufacturerPartNumber(srcPdmId);
// Ecrire cible
TopSolidHost.Pdm.SetDescription(tgtPdmId, desc);
TopSolidHost.Pdm.SetPartNumber(tgtPdmId, pn);
TopSolidHost.Pdm.SetManufacturer(tgtPdmId, mfr);
TopSolidHost.Pdm.SetManufacturerPartNumber(tgtPdmId, mfrPn);
string tgtName = TopSolidHost.Pdm.GetName(tgtPdmId);
__message = "OK: PDM properties copied to '" + tgtName + "' (designation=" + desc + ", ref=" + pn + ", manufacturer=" + mfr + ").";
```

---

## batch_export_step
Pattern: R
Description: Exports ALL project parts to STEP in a folder. Param: value=folder_path

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
string outputDir = "{value}";
if (string.IsNullOrEmpty(outputDir)) outputDir = @"C:\temp\export_step";
System.IO.Directory.CreateDirectory(outputDir);
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
int exported = 0; int skipped = 0;
int stepIdx = -1;
int count = TopSolidHost.Application.ExporterCount;
for (int i = 0; i < count; i++)
{
    string typeName; string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
        if (ext.ToLower().Contains("stp") || ext.ToLower().Contains("step")) { stepIdx = i; break; }
    if (stepIdx >= 0) break;
}
if (stepIdx < 0) return "STEP exporter not found.";
foreach (var d in docs)
{
    string t = "";
    TopSolidHost.Pdm.GetType(d, out t);
    if (t != ".TopPrt" && t != ".TopAsm") { skipped++; continue; }
    string name = TopSolidHost.Pdm.GetName(d);
    string path = System.IO.Path.Combine(outputDir, name + ".stp");
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        TopSolidHost.Application.Export(stepIdx, dId, path);
        exported++;
    }
    catch { skipped++; }
}
return "Batch STEP export: " + exported + " exported, " + skipped + " skipped. Folder: " + outputDir;
```

---

## batch_read_property
Pattern: R
Description: Reads a specific property across all project documents. Param: value=property_name

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== PROPRIETE '{value}' SUR TOUT LE PROJET ===");
foreach (var d in docs)
{
    string name = TopSolidHost.Pdm.GetName(d);
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        var pList = TopSolidHost.Parameters.GetParameters(dId);
        string found = "(absent)";
        foreach (var p in pList)
        {
            string pName = TopSolidHost.Elements.GetFriendlyName(p);
            if (pName.IndexOf("{value}", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var t = TopSolidHost.Parameters.GetParameterType(p);
                if (t == ParameterType.Real) found = TopSolidHost.Parameters.GetRealValue(p).ToString("F4");
                else if (t == ParameterType.Text) found = TopSolidHost.Parameters.GetTextValue(p);
                else if (t == ParameterType.Integer) found = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
                else if (t == ParameterType.Boolean) found = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
                break;
            }
        }
        sb.AppendLine("  " + name + ": " + found);
    }
    catch { sb.AppendLine("  " + name + ": (error)"); }
}
return sb.ToString();
```

---

## find_modified_documents
Pattern: R
Description: Lists unsaved (dirty) documents of the project

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
int dirty = 0;
foreach (var d in docs)
{
    try
    {
        if (TopSolidHost.Pdm.IsDirty(d))
        {
            sb.AppendLine("  MODIFIE: " + TopSolidHost.Pdm.GetName(d));
            dirty++;
        }
    }
    catch { continue; }
}
sb.Insert(0, "Modified documents (unsaved): " + dirty + "/" + docs.Count + "\
");
return sb.ToString();
```

---

## batch_clear_author
Pattern: RW
Description: Clears the Author field on all project documents

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) { __message = "No current project."; return; }
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
int cleared = 0;
foreach (var d in docs)
{
    string author = TopSolidHost.Pdm.GetAuthor(d);
    if (!string.IsNullOrEmpty(author))
    {
        TopSolidHost.Pdm.SetAuthor(d, "");
        cleared++;
    }
}
__message = "OK: Author cleared on " + cleared + "/" + docs.Count + " documents.";
```

---

## batch_set_designation
Pattern: RW
Description: Sets Designation (Description) on ALL documents of the current project. Param: value=new_designation

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) { __message = "No current project."; return; }
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
int updated = 0;
foreach (var d in docs)
{
    TopSolidHost.Pdm.SetDescription(d, "{value}");
    TopSolidHost.Pdm.Save(d, true);
    updated++;
}
__message = "OK: Designation set to '{value}' on " + updated + " document(s).";
```

---

## batch_set_reference
Pattern: RW
Description: Sets Reference (PartNumber) on ALL documents of the current project. Param: value=new_reference

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) { __message = "No current project."; return; }
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
int updated = 0;
foreach (var d in docs)
{
    TopSolidHost.Pdm.SetPartNumber(d, "{value}");
    TopSolidHost.Pdm.Save(d, true);
    updated++;
}
__message = "OK: Reference set to '{value}' on " + updated + " document(s).";
```

---

## batch_set_manufacturer
Pattern: RW
Description: Sets Manufacturer on ALL documents of the current project. Param: value=new_manufacturer

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) { __message = "No current project."; return; }
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
int updated = 0;
foreach (var d in docs)
{
    TopSolidHost.Pdm.SetManufacturer(d, "{value}");
    TopSolidHost.Pdm.Save(d, true);
    updated++;
}
__message = "OK: Manufacturer set to '{value}' on " + updated + " document(s).";
```

---

## clear_document_author
Pattern: RW
Description: Clears the Author field on the current document

```csharp
TopSolidHost.Pdm.SetAuthor(pdmId, "");
__message = "OK: Author cleared on current document.";
```

---

## batch_check_virtual
Pattern: R
Description: Checks the virtual property (IsVirtualDocument) on all project documents

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
int virtuel = 0; int nonVirtuel = 0;
foreach (var d in docs)
{
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        bool isVirtual = TopSolidHost.Documents.IsVirtualDocument(dId);
        string name = TopSolidHost.Pdm.GetName(d);
        if (isVirtual) { virtuel++; }
        else { sb.AppendLine("  NON-VIRTUEL: " + name); nonVirtuel++; }
    }
    catch { continue; }
}
sb.Insert(0, "Virtual: " + virtuel + ", Non-virtual: " + nonVirtuel + "/" + docs.Count + "\
");
return sb.ToString();
```

---

## batch_enable_virtual
Pattern: RW
Description: Enables virtual mode on ALL non-virtual project documents

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) { __message = "No current project."; return; }
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
int activated = 0;
foreach (var d in docs)
{
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        if (!TopSolidHost.Documents.IsVirtualDocument(dId))
        {
            TopSolidHost.Documents.SetVirtualDocumentMode(dId, true);
            activated++;
        }
    }
    catch { continue; }
}
__message = "OK: Virtual mode enabled on " + activated + " document(s).";
```

---

## enable_virtual_document
Pattern: RW
Description: Enables virtual mode on the current document

```csharp
TopSolidHost.Documents.SetVirtualDocumentMode(docId, true);
__message = "OK: Virtual mode enabled on current document.";
```

---

## check_family_drivers
Pattern: R
Description: Checks that family drivers have a designation. Lists those without.

```csharp
DocumentId famDocId = TopSolidHost.Documents.EditedDocument;
if (famDocId.IsEmpty) return "No document open.";
bool isFam = false;
try { isFam = TopSolidHost.Families.IsFamily(famDocId); } catch { return "Unable to verify."; }
if (!isFam) return "This document is NOT a family.";
var drivers = TopSolidHost.Families.GetCatalogColumnParameters(famDocId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== FAMILY DRIVERS ===");
sb.AppendLine("Drivers: " + drivers.Count);
int withDesc = 0; int withoutDesc = 0;
foreach (var d in drivers)
{
    string name = TopSolidHost.Elements.GetFriendlyName(d);
    string desc = "";
    try { desc = TopSolidHost.Elements.GetDescription(d); } catch {}
    if (string.IsNullOrEmpty(desc))
    {
        sb.AppendLine("  SANS DESIGNATION: " + name);
        withoutDesc++;
    }
    else
    {
        sb.AppendLine("  OK: " + name + " -> " + desc);
        withDesc++;
    }
}
sb.AppendLine("\
" + withDesc + " with designation, " + withoutDesc + " without.");
return sb.ToString();
```

---

## fix_family_drivers
Pattern: RW
Description: Assigns a designation to family drivers that lack one (inferred from parameter name)

```csharp
DocumentId famDocId = TopSolidHost.Documents.EditedDocument;
if (famDocId.IsEmpty) { __message = "No document open."; return; }
bool isFam = false;
try { isFam = TopSolidHost.Families.IsFamily(famDocId); } catch { __message = "Unable to verify."; return; }
if (!isFam) { __message = "This document is NOT a family."; return; }
var drivers = TopSolidHost.Families.GetCatalogColumnParameters(famDocId);
TopSolidHost.Documents.EnsureIsDirty(ref famDocId);
int fixed_ = 0;
foreach (var d in drivers)
{
    string desc = "";
    try { desc = TopSolidHost.Elements.GetDescription(d); } catch {}
    if (string.IsNullOrEmpty(desc))
    {
        string name = TopSolidHost.Elements.GetFriendlyName(d);
        // Deduire designation du nom: CamelCase split + espaces
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '_' || c == '-') { result.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i-1])) result.Append(' ');
            result.Append(c);
        }
        string newDesc = result.ToString().Trim();
        if (newDesc.Length > 0)
        {
            newDesc = char.ToUpper(newDesc[0]) + newDesc.Substring(1);
            TopSolidHost.Elements.SetDescription(d, newDesc);
            fixed_++;
        }
    }
}
__message = "OK: " + fixed_ + " drivers fixed with designation inferred from name.";
```

---

## batch_check_family_drivers
Pattern: R
Description: Checks drivers of all families in the project

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== FAMILY DRIVERS AUDIT ===");
int famCount = 0; int totalMissing = 0;
foreach (var d in docs)
{
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        if (!TopSolidHost.Families.IsFamily(dId)) continue;
        famCount++;
        string famName = TopSolidHost.Pdm.GetName(d);
        var drivers = TopSolidHost.Families.GetCatalogColumnParameters(dId);
        int missing = 0;
        foreach (var drv in drivers)
        {
            string desc = "";
            try { desc = TopSolidHost.Elements.GetDescription(drv); } catch {}
            if (string.IsNullOrEmpty(desc)) missing++;
        }
        if (missing > 0)
        {
            sb.AppendLine("  " + famName + ": " + missing + "/" + drivers.Count + " without designation");
            totalMissing += missing;
        }
        else sb.AppendLine("  " + famName + ": OK (" + drivers.Count + " drivers)");
    }
    catch { continue; }
}
sb.Insert(0, "Families: " + famCount + ", Drivers without designation: " + totalMissing + "\
");
return sb.ToString();
```

---

## audit_parameter_names
Pattern: R
Description: Audits parameter name syntax: detects convention inconsistencies and near-duplicates

```csharp
DocumentId curDocId = TopSolidHost.Documents.EditedDocument;
if (curDocId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(curDocId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== PARAMETER NAMES AUDIT ===");
var names = new List<string>();
int camel = 0; int under = 0; int space = 0; int upper = 0; int lower = 0;
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name.Contains("$")) continue;
    names.Add(name);
    bool hasCamel = false; bool hasUnder = false; bool hasSpace = false;
    for (int i = 1; i < name.Length; i++)
    {
        if (char.IsUpper(name[i]) && char.IsLower(name[i-1])) hasCamel = true;
        if (name[i] == '_') hasUnder = true;
        if (name[i] == ' ') hasSpace = true;
    }
    if (hasCamel) camel++;
    if (hasUnder) under++;
    if (hasSpace) space++;
    if (name == name.ToUpper()) upper++;
    if (name == name.ToLower()) lower++;
}
sb.AppendLine("User parameters: " + names.Count);
sb.AppendLine("Detected conventions:");
if (camel > 0) sb.AppendLine("  CamelCase: " + camel);
if (under > 0) sb.AppendLine("  underscore: " + under);
if (space > 0) sb.AppendLine("  spaces: " + space);
if (upper > 0) sb.AppendLine("  UPPERCASE: " + upper);
if (lower > 0) sb.AppendLine("  lowercase: " + lower);
int conventions = (camel>0?1:0) + (under>0?1:0) + (space>0?1:0) + (upper>0?1:0) + (lower>0?1:0);
if (conventions > 1) sb.AppendLine("  *** ATTENTION: " + conventions + " mixed conventions! ***");
else sb.AppendLine("  Single convention: OK");
// Doublons proches (Levenshtein simple)
sb.AppendLine("\
Doublons potentiels (casse differente):");
int dupes = 0;
for (int i = 0; i < names.Count; i++)
    for (int j = i + 1; j < names.Count; j++)
        if (names[i].ToLower() == names[j].ToLower())
        { sb.AppendLine("  '" + names[i] + "' vs '" + names[j] + "'"); dupes++; }
if (dupes == 0) sb.AppendLine("  No duplicates.");
// Liste tous les noms pour inspection visuelle par le LLM
sb.AppendLine("\
Liste complete:");
foreach (var n in names) sb.AppendLine("  " + n);
return sb.ToString();
```

---

## batch_audit_parameter_names
Pattern: R
Description: Audits parameter name syntax across all project documents

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== PROJECT PARAMETER NAMES AUDIT ===");
var allNames = new Dictionary<string, List<string>>();
foreach (var d in docs)
{
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        string docName = TopSolidHost.Pdm.GetName(d);
        var pList = TopSolidHost.Parameters.GetParameters(dId);
        foreach (var p in pList)
        {
            string name = TopSolidHost.Elements.GetFriendlyName(p);
            if (name.Contains("$")) continue;
            string key = name.ToLower();
            if (!allNames.ContainsKey(key)) allNames[key] = new List<string>();
            if (!allNames[key].Contains(name)) allNames[key].Add(name);
        }
    }
    catch { continue; }
}
// Trouver les variantes (meme nom, casse differente)
sb.AppendLine("Unique names (excl. system): " + allNames.Count);
sb.AppendLine("\
Variantes de casse detectees:");
int variants = 0;
foreach (var kvp in allNames)
{
    if (kvp.Value.Count > 1)
    {
        sb.AppendLine("  " + string.Join(" / ", kvp.Value));
        variants++;
    }
}
if (variants == 0) sb.AppendLine("  No variants.");
// Lister tous les noms pour inspection par le LLM (fautes de frappe)
sb.AppendLine("\
Tous les noms de parametres du projet:");
foreach (var kvp in allNames)
    sb.AppendLine("  " + kvp.Value[0]);
return sb.ToString();
```

---

## batch_audit_driver_designations
Pattern: R
Description: Lists driver designations of all project families for inspection (typos, inconsistencies)

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== DRIVER DESIGNATIONS AUDIT ===");
foreach (var d in docs)
{
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        if (!TopSolidHost.Families.IsFamily(dId)) continue;
        string famName = TopSolidHost.Pdm.GetName(d);
        sb.AppendLine("\
" + famName + ":");
        var drivers = TopSolidHost.Families.GetCatalogColumnParameters(dId);
        foreach (var drv in drivers)
        {
            string name = TopSolidHost.Elements.GetFriendlyName(drv);
            string desc = "";
            try { desc = TopSolidHost.Elements.GetDescription(drv); } catch {}
            sb.AppendLine("  " + name + " -> " + (string.IsNullOrEmpty(desc) ? "(EMPTY)" : desc));
        }
    }
    catch { continue; }
}
return sb.ToString();
```

---

## read_revision_history
Pattern: R
Description: Lists all major/minor revisions of the current document

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var majors = TopSolidHost.Pdm.GetMajorRevisions(pdmId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== REVISION HISTORY ===");
sb.AppendLine("Document: " + TopSolidHost.Pdm.GetName(pdmId));
sb.AppendLine("Major revisions: " + majors.Count);
foreach (var maj in majors)
{
    string majText = TopSolidHost.Pdm.GetMajorRevisionText(maj);
    var state = TopSolidHost.Pdm.GetMajorRevisionLifeCycleMainState(maj);
    var minors = TopSolidHost.Pdm.GetMinorRevisions(maj);
    sb.AppendLine("  Rev " + majText + " (" + state + ") - " + minors.Count + " minor(s)");
    foreach (var min in minors)
    {
        string minText = TopSolidHost.Pdm.GetMinorRevisionText(min);
        sb.AppendLine("    ." + minText);
    }
}
return sb.ToString();
```

---

## compare_revisions
Pattern: R
Description: Compares parameters of the current revision with the previous one

```csharp
DocumentId curDocId = TopSolidHost.Documents.EditedDocument;
if (curDocId.IsEmpty) return "No document open.";
PdmObjectId curPdmId = TopSolidHost.Documents.GetPdmObject(curDocId);
// Lire les parametres du document courant
var paramsNow = TopSolidHost.Parameters.GetParameters(curDocId);
var dictNow = new System.Collections.Generic.Dictionary<string, string>();
foreach (var p in paramsNow)
{
    string n = TopSolidHost.Elements.GetFriendlyName(p);
    var t = TopSolidHost.Parameters.GetParameterType(p);
    string v = "";
    if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString("F6");
    else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p);
    else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    else if (t == ParameterType.Boolean) v = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
    dictNow[n] = v;
}
// Trouver la revision precedente
var majors = TopSolidHost.Pdm.GetMajorRevisions(curPdmId);
if (majors.Count == 0) return "No revision found.";
var lastMajor = majors[majors.Count - 1];
var minors = TopSolidHost.Pdm.GetMinorRevisions(lastMajor);
if (minors.Count < 2) return "Only one revision, nothing to compare.";
var prevMinor = minors[minors.Count - 2];
// Ouvrir la revision precedente en lecture seule
DocumentId prevDocId = TopSolidHost.Documents.GetMinorRevisionDocument(prevMinor);
var paramsPrev = TopSolidHost.Parameters.GetParameters(prevDocId);
var dictPrev = new System.Collections.Generic.Dictionary<string, string>();
foreach (var p in paramsPrev)
{
    string n = TopSolidHost.Elements.GetFriendlyName(p);
    var t = TopSolidHost.Parameters.GetParameterType(p);
    string v = "";
    if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString("F6");
    else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p);
    else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    else if (t == ParameterType.Boolean) v = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
    dictPrev[n] = v;
}
// Comparer
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== REVISION COMPARISON ===");
string curMinText = TopSolidHost.Pdm.GetMinorRevisionText(minors[minors.Count - 1]);
string prevMinText = TopSolidHost.Pdm.GetMinorRevisionText(prevMinor);
sb.AppendLine("Rev ." + prevMinText + " vs Rev ." + curMinText + " (current)");
int diffs = 0;
foreach (var kvp in dictNow)
{
    string vPrev;
    if (dictPrev.TryGetValue(kvp.Key, out vPrev))
    {
        if (kvp.Value != vPrev) { sb.AppendLine("  MODIFIE: " + kvp.Key + " : " + vPrev + " -> " + kvp.Value); diffs++; }
        dictPrev.Remove(kvp.Key);
    }
    else { sb.AppendLine("  AJOUTE: " + kvp.Key + " = " + kvp.Value); diffs++; }
}
foreach (var kvp in dictPrev)
    { sb.AppendLine("  SUPPRIME: " + kvp.Key + " = " + kvp.Value); diffs++; }
if (diffs == 0) sb.AppendLine("  No parameter differences.");
else sb.AppendLine("\
" + diffs + " difference(s)");
return sb.ToString();
```

---

## export_bom_csv
Pattern: R
Description: Exports the BOM as text (separated columns)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isBom = false;
try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return "Unable to verify."; }
if (!isBom) return "This document is not a BOM.";
int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);
int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);
var sb = new System.Text.StringBuilder();
// En-tetes
var headers = new System.Collections.Generic.List<string>();
for (int i = 0; i < colCount; i++)
    headers.Add(TopSolidDesignHost.Boms.GetColumnTitle(docId, i));
sb.AppendLine(string.Join(";", headers));
// Lignes
var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);
foreach (int rowId in children)
{
    if (!TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) continue;
    System.Collections.Generic.List<Property> props;
    System.Collections.Generic.List<string> texts;
    TopSolidDesignHost.Boms.GetRowContents(docId, rowId, out props, out texts);
    sb.AppendLine(string.Join(";", texts));
}
return sb.ToString();
```

---

## check_missing_materials
Pattern: R
Description: Lists project parts without assigned material

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var items = docs;
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== VERIFICATION MATERIALS ===");
int missing = 0; int total = 0;
foreach (var item in items)
{
    string name = TopSolidHost.Pdm.GetName(item);
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(item);
        if (dId.IsEmpty) continue;
        total++;
        var pList = TopSolidHost.Parameters.GetParameters(dId);
        double mass = 0;
        foreach (var p in pList)
        {
            if (TopSolidHost.Elements.GetFriendlyName(p) == "Mass")
            { mass = TopSolidHost.Parameters.GetRealValue(p); break; }
        }
        if (mass <= 0) { sb.AppendLine("  SANS MATERIAU: " + name); missing++; }
    }
    catch { continue; }
}
sb.AppendLine("\
" + missing + "/" + total + " part(s) without material.");
return sb.ToString();
```

---

## assembly_mass_report
Pattern: R
Description: Reads total mass, volume, surface and part count of an assembly

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isAsm = false;
try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return "Not an assembly."; }
if (!isAsm) return "Not an assembly.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== ASSEMBLY MASS REPORT ===");
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Mass")
        sb.AppendLine("Total mass: " + TopSolidHost.Parameters.GetRealValue(p).ToString("F3") + " kg");
    else if (name == "Volume")
        sb.AppendLine("Total volume: " + (TopSolidHost.Parameters.GetRealValue(p) * 1e9).ToString("F2") + " mm3");
    else if (name == "Surface Area")
        sb.AppendLine("Total surface: " + (TopSolidHost.Parameters.GetRealValue(p) * 1e6).ToString("F2") + " mm2");
    else if (name == "Part Count")
    {
        var pType = TopSolidHost.Parameters.GetParameterType(p);
        if (pType == ParameterType.Integer) sb.AppendLine("Part count: " + TopSolidHost.Parameters.GetIntegerValue(p));
        else sb.AppendLine("Part count: " + TopSolidHost.Parameters.GetRealValue(p).ToString("F0"));
    }
}
return sb.ToString();
```

---

## read_material
Pattern: R
Description: Reads part material (mass and calculated density)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var pList = TopSolidHost.Parameters.GetParameters(docId);
double mass = 0; double vol = 0;
foreach (var p in pList)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Mass") mass = TopSolidHost.Parameters.GetRealValue(p);
    else if (name == "Volume") vol = TopSolidHost.Parameters.GetRealValue(p);
}
var sb = new System.Text.StringBuilder();
if (mass > 0)
{
    sb.AppendLine("Material assigned.");
    sb.AppendLine("Mass: " + mass.ToString("F3") + " kg");
    if (vol > 0) sb.AppendLine("Calculated density: " + (mass / vol).ToString("F0") + " kg/m3");
}
else sb.AppendLine("No material assigned (mass = 0).");
return sb.ToString();
```

---

## export_stl
Pattern: R
Description: Exports to STL (3D printing). Param: value=path

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
int count = TopSolidHost.Application.ExporterCount;
int idx = -1;
for (int i = 0; i < count; i++)
{
    string typeName; string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
        if (ext.ToLower().Contains("stl")) { idx = i; break; }
    if (idx >= 0) break;
}
if (idx < 0) return "STL exporter not found.";
string path = "{value}";
if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "export.stl");
TopSolidHost.Documents.Export(idx, docId, path);
return "OK: Exported to STL → " + path;
```

---

## export_iges
Pattern: R
Description: Exports to IGES. Param: value=path

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
int count = TopSolidHost.Application.ExporterCount;
int idx = -1;
for (int i = 0; i < count; i++)
{
    string typeName; string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
        if (ext.ToLower().Contains("igs") || ext.ToLower().Contains("iges")) { idx = i; break; }
    if (idx >= 0) break;
}
if (idx < 0) return "IGES exporter not found.";
string path = "{value}";
if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "export.igs");
TopSolidHost.Documents.Export(idx, docId, path);
return "OK: Exported to IGES → " + path;
```

---

## count_assembly_parts
Pattern: R
Description: Counts parts grouped by type with quantities

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
bool isAsm = false;
try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return "Not an assembly."; }
if (!isAsm) return "Not an assembly.";
var ops = TopSolidHost.Operations.GetOperations(docId);
var counts = new System.Collections.Generic.Dictionary<string, int>();
foreach (var op in ops)
{
    bool isInclusion = false;
    try { isInclusion = TopSolidDesignHost.Assemblies.IsInclusion(op); } catch { continue; }
    if (!isInclusion) continue;
    DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(op);
    string defName = "?";
    if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }
    if (counts.ContainsKey(defName)) counts[defName]++; else counts[defName] = 1;
}
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== PARTS COUNT ===");
int total = 0;
foreach (var kvp in counts)
{
    sb.AppendLine("  " + kvp.Value + "x " + kvp.Key);
    total += kvp.Value;
}
sb.AppendLine("Total: " + total + " parts (" + counts.Count + " unique references)");
return sb.ToString();
```

---

## save_all_project
Pattern: R
Description: Saves all documents of the current project

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var items = docs;
int saved = 0;
foreach (var item in items)
{
    try { TopSolidHost.Pdm.Save(item, true); saved++; } catch { continue; }
}
return "OK: " + saved + "/" + items.Count + " documents saved.";
```

---

## open_document_by_name
Pattern: R
Description: Searches and opens a document by name. Param: value=name

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
if (results.Count == 0) return "Document '{value}' not found.";
DocumentId dId = TopSolidHost.Documents.GetDocument(results[0]);
TopSolidHost.Documents.Open(ref dId);
return "OK: Document '" + TopSolidHost.Pdm.GetName(results[0]) + "' opened.";
```

---

## list_folder_documents
Pattern: R
Description: Lists documents of a specific project folder. Param: value=folder_name

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
PdmObjectId targetFolder = PdmObjectId.Empty;
foreach (var f in folders)
{
    if (TopSolidHost.Pdm.GetName(f).IndexOf("{value}", StringComparison.OrdinalIgnoreCase) >= 0)
    { targetFolder = f; break; }
}
if (targetFolder.IsEmpty) return "Folder '{value}' not found.";
List<PdmObjectId> subFolders; List<PdmObjectId> subDocs;
TopSolidHost.Pdm.GetConstituents(targetFolder, out subFolders, out subDocs);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Folder: " + TopSolidHost.Pdm.GetName(targetFolder) + " (" + subDocs.Count + " docs)");
foreach (var d in subDocs)
{
    string name = TopSolidHost.Pdm.GetName(d);
    string desc = TopSolidHost.Pdm.GetDescription(d);
    string pn = TopSolidHost.Pdm.GetPartNumber(d);
    string type = "";
    TopSolidHost.Pdm.GetType(d, out type);
    sb.AppendLine("  " + name + " | " + desc + " | " + pn + " | " + type);
}
return sb.ToString();
```

---

## summarize_project
Pattern: R
Description: Project summary: document count by type, folders, size

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var types = new Dictionary<string, int>();
foreach (var d in docs)
{
    string t = "";
    TopSolidHost.Pdm.GetType(d, out t);
    if (types.ContainsKey(t)) types[t]++;
    else types[t] = 1;
}
var sb = new System.Text.StringBuilder();
sb.AppendLine("Project: " + TopSolidHost.Pdm.GetName(projId));
sb.AppendLine("Folders: " + folders.Count);
sb.AppendLine("Documents: " + docs.Count);
foreach (var kv in types)
    sb.AppendLine("  " + kv.Key + ": " + kv.Value);
return sb.ToString();
```

---

## list_documents_without_reference
Pattern: R
Description: Lists project documents without reference (empty part number)

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
int missing = 0;
foreach (var d in docs)
{
    string pn = TopSolidHost.Pdm.GetPartNumber(d);
    if (string.IsNullOrEmpty(pn))
    {
        sb.AppendLine("  " + TopSolidHost.Pdm.GetName(d));
        missing++;
    }
}
sb.Insert(0, "Documents sans reference: " + missing + "/" + docs.Count + "\
");
return sb.ToString();
```

---

## list_documents_without_designation
Pattern: R
Description: Lists project documents without designation (empty description)

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
int missing = 0;
foreach (var d in docs)
{
    string desc = TopSolidHost.Pdm.GetDescription(d);
    if (string.IsNullOrEmpty(desc))
    {
        sb.AppendLine("  " + TopSolidHost.Pdm.GetName(d));
        missing++;
    }
}
sb.Insert(0, "Documents sans designation: " + missing + "/" + docs.Count + "\
");
return sb.ToString();
```

---

## count_documents_by_type
Pattern: R
Description: Counts project documents grouped by type (.TopPrt, .TopAsm, .TopDft...)

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var types = new Dictionary<string, int>();
foreach (var d in docs)
{
    string t = "";
    TopSolidHost.Pdm.GetType(d, out t);
    if (types.ContainsKey(t)) types[t]++;
    else types[t] = 1;
}
var sb = new System.Text.StringBuilder();
sb.AppendLine("Total: " + docs.Count + " documents");
foreach (var kv in types)
    sb.AppendLine("  " + kv.Key + ": " + kv.Value);
return sb.ToString();
```

---

## search_parts_by_material
Pattern: R
Description: Lists parts with their material (via mass > 0). Param: value=optional filter

```csharp
PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
if (projId.IsEmpty) return "No current project.";
List<PdmObjectId> folders; List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);
var sb = new System.Text.StringBuilder();
int count = 0;
foreach (var d in docs)
{
    string t = "";
    TopSolidHost.Pdm.GetType(d, out t);
    if (t != ".TopPrt") continue;
    string name = TopSolidHost.Pdm.GetName(d);
    try
    {
        DocumentId dId = TopSolidHost.Documents.GetDocument(d);
        var pList = TopSolidHost.Parameters.GetParameters(dId);
        double mass = 0;
        foreach (var p in pList)
        {
            if (TopSolidHost.Elements.GetFriendlyName(p) == "Mass")
            { mass = TopSolidHost.Parameters.GetRealValue(p); break; }
        }
        string info = name + " | mass=" + mass.ToString("F3") + "kg";
        if ("{value}" == "" || info.IndexOf("{value}", StringComparison.OrdinalIgnoreCase) >= 0)
        { sb.AppendLine("  " + info); count++; }
    }
    catch { continue; }
}
sb.Insert(0, "Parts found: " + count + "\
");
return sb.ToString();
```

---

## read_where_used
Pattern: R
Description: Finds where-used references of the current document in the project

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);
PdmMajorRevisionId majorRev = TopSolidHost.Pdm.GetLastMajorRevision(pdmId);
var backRefs = TopSolidHost.Pdm.SearchMajorRevisionBackReferences(projId, majorRev);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Where-used: " + backRefs.Count);
foreach (var br in backRefs)
{
    PdmMajorRevisionId brMajor = TopSolidHost.Pdm.GetMajorRevision(br);
    PdmObjectId brObj = TopSolidHost.Pdm.GetPdmObject(brMajor);
    string name = TopSolidHost.Pdm.GetName(brObj);
    string type = "";
    TopSolidHost.Pdm.GetType(brObj, out type);
    sb.AppendLine("  " + name + " (" + type + ")");
}
return sb.ToString();
```

---

## attr_read_all
Pattern: R
Description: Reads color, transparency, layer and visibility of all elements

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var shapes = TopSolidHost.Shapes.GetShapes(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("=== ATTRIBUTES ===");
foreach (var s in shapes)
{
    string name = TopSolidHost.Elements.GetFriendlyName(s);
    sb.Append(name + ": ");
    if (TopSolidHost.Elements.HasColor(s))
    {
        Color c = TopSolidHost.Elements.GetColor(s);
        sb.Append("color=RGB(" + c.R + "," + c.G + "," + c.B + ") ");
    }
    if (TopSolidHost.Elements.HasTransparency(s))
        sb.Append("transparency=" + TopSolidHost.Elements.GetTransparency(s).ToString("F2") + " ");
    sb.Append("visible=" + TopSolidHost.Elements.IsVisible(s));
    try
    {
        ElementId layerId = TopSolidHost.Layers.GetLayer(docId, s);
        if (!layerId.IsEmpty) sb.Append(" layer=" + TopSolidHost.Elements.GetFriendlyName(layerId));
    } catch {}
    sb.AppendLine();
}
return sb.ToString();
```

---

## attr_set_color
Pattern: RW
Description: Sets color. If 1 shape: direct. If multiple: asks selection. Param: value=R,G,B (e.g. 0,0,255)

```csharp
string[] rgb = "{value}".Split(',');
if (rgb.Length != 3) { __message = "Format: R,G,B (e.g. 255,0,0 for red)"; return; }
int r, g, b;
if (!int.TryParse(rgb[0].Trim(), out r) || !int.TryParse(rgb[1].Trim(), out g) || !int.TryParse(rgb[2].Trim(), out b))
{ __message = "Format: R,G,B (ex: 255,0,0)"; return; }
var shapes = TopSolidHost.Shapes.GetShapes(docId);
ElementId target = ElementId.Empty;
if (shapes.Count == 1) { target = shapes[0]; }
else if (shapes.Count > 1)
{
    UserQuestion q = new UserQuestion("Multiple elements. Select the one to color", "");
    UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out target);
    if (answer != UserAnswerType.Ok || target.IsEmpty) { __message = "Selection cancelled."; return; }
}
else { __message = "No shape in the document."; return; }
TopSolidHost.Elements.SetColor(target, new Color((byte)r, (byte)g, (byte)b));
__message = "OK: " + TopSolidHost.Elements.GetFriendlyName(target) + " → RGB(" + r + "," + g + "," + b + ")";
```

---

## attr_set_color_all
Pattern: RW
Description: Sets color on ALL elements. Param: value=R,G,B

```csharp
string[] rgb = "{value}".Split(',');
if (rgb.Length != 3) { __message = "Format: R,G,B"; return; }
int r, g, b;
if (!int.TryParse(rgb[0].Trim(), out r) || !int.TryParse(rgb[1].Trim(), out g) || !int.TryParse(rgb[2].Trim(), out b))
{ __message = "Format: R,G,B"; return; }
var shapes = TopSolidHost.Shapes.GetShapes(docId);
int count = 0;
foreach (var s in shapes)
{
    if (TopSolidHost.Elements.IsColorModifiable(s))
    { TopSolidHost.Elements.SetColor(s, new Color((byte)r, (byte)g, (byte)b)); count++; }
}
__message = "OK: " + count + " element(s) → RGB(" + r + "," + g + "," + b + ")";
```

---

## attr_read_color
Pattern: R
Description: Reads element colors

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var shapes = TopSolidHost.Shapes.GetShapes(docId);
var sb = new System.Text.StringBuilder();
foreach (var s in shapes)
{
    string name = TopSolidHost.Elements.GetFriendlyName(s);
    if (TopSolidHost.Elements.HasColor(s))
    {
        Color c = TopSolidHost.Elements.GetColor(s);
        sb.AppendLine(name + ": RGB(" + c.R + "," + c.G + "," + c.B + ")");
    }
    else sb.AppendLine(name + ": (pas de couleur)");
}
return sb.ToString();
```

---

## attr_set_transparency
Pattern: RW
Description: Sets transparency. If 1 shape: direct. If multiple: asks. Param: value=0.0 to 1.0

```csharp
double transp;
if (!double.TryParse("{value}", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out transp))
{ __message = "Format: number between 0.0 and 1.0"; return; }
var shapes = TopSolidHost.Shapes.GetShapes(docId);
ElementId target = ElementId.Empty;
if (shapes.Count == 1) { target = shapes[0]; }
else if (shapes.Count > 1)
{
    UserQuestion q = new UserQuestion("Select the element", "");
    UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out target);
    if (answer != UserAnswerType.Ok || target.IsEmpty) { __message = "Selection cancelled."; return; }
}
else { __message = "No shape."; return; }
TopSolidHost.Elements.SetTransparency(target, transp);
__message = "OK: transparence " + transp.ToString("F1") + " sur " + TopSolidHost.Elements.GetFriendlyName(target);
```

---

## attr_read_transparency
Pattern: R
Description: Reads element transparency

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var shapes = TopSolidHost.Shapes.GetShapes(docId);
var sb = new System.Text.StringBuilder();
foreach (var s in shapes)
{
    string name = TopSolidHost.Elements.GetFriendlyName(s);
    if (TopSolidHost.Elements.HasTransparency(s))
        sb.AppendLine(name + ": " + TopSolidHost.Elements.GetTransparency(s).ToString("F2"));
    else sb.AppendLine(name + ": (pas de transparence)");
}
return sb.ToString();
```

---

## attr_list_layers
Pattern: R
Description: Lists document layers

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var layers = TopSolidHost.Layers.GetLayers(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Layers: " + layers.Count);
foreach (var l in layers)
    sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(l));
return sb.ToString();
```

---

## attr_assign_layer
Pattern: RW
Description: Assigns an element to a layer. Param: value=element_name:layer_name

```csharp
int idx = "{value}".IndexOf(':');
if (idx < 0) { __message = "Format: element_name:layer_name"; return; }
string elemName = "{value}".Substring(0, idx).Trim();
string layerName = "{value}".Substring(idx + 1).Trim();
// Trouver le calque
var layers = TopSolidHost.Layers.GetLayers(docId);
ElementId layerId = ElementId.Empty;
foreach (var l in layers)
{
    if (TopSolidHost.Elements.GetFriendlyName(l).IndexOf(layerName, StringComparison.OrdinalIgnoreCase) >= 0)
    { layerId = l; break; }
}
if (layerId.IsEmpty) { __message = "Layer '" + layerName + "' not found."; return; }
// Trouver l'element
var elems = TopSolidHost.Elements.GetElements(docId);
foreach (var e in elems)
{
    if (TopSolidHost.Elements.GetFriendlyName(e).IndexOf(elemName, StringComparison.OrdinalIgnoreCase) >= 0)
    {
        TopSolidHost.Layers.SetLayer(e, layerId);
        __message = "OK: " + TopSolidHost.Elements.GetFriendlyName(e) + " -> calque " + layerName;
        return;
    }
}
__message = "Element '" + elemName + "' not found.";
```

---

## attr_replace_color
Pattern: RW
Description: Replaces a color with another on elements. Param: value=R1,G1,B1:R2,G2,B2 (e.g. 0,128,0:255,0,0 = green->red)

```csharp
string[] parts = "{value}".Split(':');
if (parts.Length != 2) { __message = "Format: R1,G1,B1:R2,G2,B2 (e.g. 0,128,0:255,0,0)"; return; }
string[] src = parts[0].Split(',');
string[] dst = parts[1].Split(',');
if (src.Length != 3 || dst.Length != 3) { __message = "Format: R1,G1,B1:R2,G2,B2"; return; }
int sr, sg, sb2, dr, dg, db;
if (!int.TryParse(src[0].Trim(), out sr) || !int.TryParse(src[1].Trim(), out sg) || !int.TryParse(src[2].Trim(), out sb2) ||
    !int.TryParse(dst[0].Trim(), out dr) || !int.TryParse(dst[1].Trim(), out dg) || !int.TryParse(dst[2].Trim(), out db))
{ __message = "Invalid RGB values."; return; }
// Chercher dans les shapes du document
var shapes = TopSolidHost.Shapes.GetShapes(docId);
int changed = 0;
int tolerance = 50; // tolerance RGB
foreach (var s in shapes)
{
    if (!TopSolidHost.Elements.HasColor(s)) continue;
    Color c = TopSolidHost.Elements.GetColor(s);
    if (System.Math.Abs(c.R - sr) < tolerance && System.Math.Abs(c.G - sg) < tolerance && System.Math.Abs(c.B - sb2) < tolerance)
    {
        if (TopSolidHost.Elements.IsColorModifiable(s))
        {
            TopSolidHost.Elements.SetColor(s, new Color((byte)dr, (byte)dg, (byte)db));
            changed++;
        }
    }
}
// Chercher aussi dans les operations (inclusions dans un assemblage)
var ops = TopSolidHost.Operations.GetOperations(docId);
foreach (var op in ops)
{
    if (!TopSolidHost.Elements.HasColor(op)) continue;
    Color c = TopSolidHost.Elements.GetColor(op);
    if (System.Math.Abs(c.R - sr) < tolerance && System.Math.Abs(c.G - sg) < tolerance && System.Math.Abs(c.B - sb2) < tolerance)
    {
        if (TopSolidHost.Elements.IsColorModifiable(op))
        {
            TopSolidHost.Elements.SetColor(op, new Color((byte)dr, (byte)dg, (byte)db));
            changed++;
        }
    }
}
__message = changed + " element(s) changed from RGB(" + sr + "," + sg + "," + sb2 + ") to RGB(" + dr + "," + dg + "," + db + ")";
```

---

## select_shape
Pattern: R
Description: Asks the user to select a shape and returns its info

```csharp
ElementId selected = ElementId.Empty;
UserQuestion q = new UserQuestion("Select a shape", "");
UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out selected);
if (answer != UserAnswerType.Ok || selected.IsEmpty) return "Selection cancelled.";
string name = TopSolidHost.Elements.GetFriendlyName(selected);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Selected element: " + name);
if (TopSolidHost.Elements.HasColor(selected))
{
    Color c = TopSolidHost.Elements.GetColor(selected);
    sb.AppendLine("Color: RGB(" + c.R + "," + c.G + "," + c.B + ")");
}
if (TopSolidHost.Elements.HasTransparency(selected))
    sb.AppendLine("Transparency: " + TopSolidHost.Elements.GetTransparency(selected).ToString("F2"));
sb.AppendLine("Visible: " + TopSolidHost.Elements.IsVisible(selected));
sb.AppendLine("Type: " + TopSolidHost.Elements.GetTypeFullName(selected));
return sb.ToString();
```

---

## select_face
Pattern: R
Description: Asks the user to select a face and returns its info

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
var sb = new System.Text.StringBuilder();
Color c = TopSolidHost.Shapes.GetFaceColor(selected);
sb.AppendLine("Face color: RGB(" + c.R + "," + c.G + "," + c.B + ")");
double area = TopSolidHost.Shapes.GetFaceArea(selected);
sb.AppendLine("Area: " + (area * 1e6).ToString("F2") + " cm2");
int surfType = (int)TopSolidHost.Shapes.GetFaceSurfaceType(selected);
sb.AppendLine("Surface type: " + surfType);
return sb.ToString();
```

---

## get_face_cone_length
Pattern: R
Description: Gets the length of a selected cone face (mm). Requires a cone face selection.

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a cone face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try {
    double lengthMeters = TopSolidHost.Shapes.GetFaceConeLength(selected);
    return "Cone length: " + (lengthMeters * 1000).ToString("F3") + " mm";
} catch (Exception ex) {
    return "Error (is the selection a cone face?): " + ex.Message;
}
```

---

## get_face_cone_radius
Pattern: R
Description: Gets the base radius of a selected cone face (mm). Requires a cone face selection.

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a cone face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try {
    double radiusMeters = TopSolidHost.Shapes.GetFaceConeRadius(selected);
    return "Cone radius: " + (radiusMeters * 1000).ToString("F3") + " mm";
} catch (Exception ex) {
    return "Error (is the selection a cone face?): " + ex.Message;
}
```

---

## get_face_cone_semi_angle
Pattern: R
Description: Gets the half-angle of a selected cone face (degrees). Requires a cone face selection.

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a cone face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try {
    double angleRadians = TopSolidHost.Shapes.GetFaceConeSemiAngle(selected);
    double angleDegrees = angleRadians * 180.0 / System.Math.PI;
    return "Cone semi-angle: " + angleDegrees.ToString("F2") + " deg (" + angleRadians.ToString("F4") + " rad)";
} catch (Exception ex) {
    return "Error (is the selection a cone face?): " + ex.Message;
}
```

---

## get_face_torus_major_radius
Pattern: R
Description: Gets the major radius of a selected torus face (mm). Requires a torus face selection.

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a torus face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try {
    double radiusMeters = TopSolidHost.Shapes.GetFaceTorusMajorRadius(selected);
    return "Torus major radius: " + (radiusMeters * 1000).ToString("F3") + " mm";
} catch (Exception ex) {
    return "Error (is the selection a torus face?): " + ex.Message;
}
```

---

## get_face_torus_minor_radius
Pattern: R
Description: Gets the minor radius of a selected torus face (mm). Requires a torus face selection.

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a torus face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try {
    double radiusMeters = TopSolidHost.Shapes.GetFaceTorusMinorRadius(selected);
    return "Torus minor radius: " + (radiusMeters * 1000).ToString("F3") + " mm";
} catch (Exception ex) {
    return "Error (is the selection a torus face?): " + ex.Message;
}
```

---

## get_item_last_operation_name
Pattern: R
Description: Gets the name of the last operation that produced a selected face.

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select a face", "");
UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try {
    string opName = TopSolidHost.Operations.GetItemLastOperationName(selected);
    return string.IsNullOrEmpty(opName) ? "Last operation: (none)" : "Last operation: " + opName;
} catch (Exception ex) {
    return "Error: " + ex.Message;
}
```

---

## select_3d_point
Pattern: R
Description: Asks the user to click a 3D point and returns coordinates

```csharp
SmartPoint3D selected;
UserQuestion q = new UserQuestion("Click a 3D point", "");
UserAnswerType answer = TopSolidHost.User.AskPoint3D(q, default(SmartPoint3D), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
return "Point: (" + (selected.X * 1000).ToString("F2") + ", " + (selected.Y * 1000).ToString("F2") + ", " + (selected.Z * 1000).ToString("F2") + ") mm";
```

---

## attr_read_face_colors
Pattern: R
Description: Reads individual face colors

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var shapes = TopSolidHost.Shapes.GetShapes(docId);
if (shapes.Count == 0) return "No shape.";
var sb = new System.Text.StringBuilder();
foreach (var s in shapes)
{
    string sName = TopSolidHost.Elements.GetFriendlyName(s);
    var faces = TopSolidHost.Shapes.GetFaces(s);
    sb.AppendLine(sName + " (" + faces.Count + " faces):");
    foreach (var f in faces)
    {
        Color c = TopSolidHost.Shapes.GetFaceColor(f);
        sb.AppendLine("  R=" + c.R + " G=" + c.G + " B=" + c.B);
    }
}
return sb.ToString();
```

---

## list_positionings
Pattern: R
Description: Lists positioning operations of the active assembly with their constraint status

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var ops = TopSolidHost.Operations.GetOperations(docId);
var sb = new System.Text.StringBuilder();
int count = 0;
int underCount = 0;
foreach (var op in ops)
{
    try
    {
        bool under = TopSolidDesignHost.Assemblies.IsPositioningUnderconstrained(op);
        string name = TopSolidHost.Elements.GetFriendlyName(op);
        if (under) underCount++;
        sb.AppendLine("  " + name + " -> " + (under ? "UNDERCONSTRAINED" : "fully constrained"));
        count++;
    }
    catch { continue; }
}
if (count == 0) return "No positioning found (is the active document an assembly?).";
sb.Insert(0, "Positionings: " + count + " (" + underCount + " underconstrained)" + System.Environment.NewLine);
return sb.ToString();
```

---

## is_positioning_underconstrained
Pattern: R
Description: Tells if a positioning is underconstrained. Param: value=positioning_name (exact or substring)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
string target = "{value}".Trim();
if (target.Length == 0) return "Param required: value=positioning_name (use list_positionings first).";
var ops = TopSolidHost.Operations.GetOperations(docId);
foreach (var op in ops)
{
    try
    {
        bool under = TopSolidDesignHost.Assemblies.IsPositioningUnderconstrained(op);
        string name = TopSolidHost.Elements.GetFriendlyName(op);
        if (name.IndexOf(target, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "Positioning '" + name + "' is " + (under ? "UNDERCONSTRAINED" : "fully constrained");
    }
    catch { continue; }
}
return "Positioning '" + target + "' not found (use list_positionings).";
```

---

## get_material_brinell_hardness_factor
Pattern: R
Description: Get the Brinell hardness factor of the active material (from its standards file). Reports (also) error when no material is set

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Materials.GetBrinellHardnessFactor(docId);
    return "GetBrinellHardnessFactor of the active material: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no material set on this document?): " + ex.Message;
}
```

---

## get_material_vickers_hardness_factor
Pattern: R
Description: Get the Vickers hardness factor of the active material (from its standards file). Reports (also) error when no material is set

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Materials.GetVickersHardnessFactor(docId);
    return "GetVickersHardnessFactor of the active material: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no material set on this document?): " + ex.Message;
}
```

---

## get_material_glossiness_factor
Pattern: R
Description: Get the glossiness factor of the active material. Reports (also) error when no material is set

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Materials.GetGlossinessFactor(docId);
    return "GetGlossinessFactor of the active material: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no material set on this document?): " + ex.Message;
}
```

---

## get_material_bump_scale
Pattern: R
Description: Get the bump texture scale factor of the active material. Reports (also) error when no material is set

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Materials.GetBumpScale(docId);
    return "GetBumpScale of the active material: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no material set on this document?): " + ex.Message;
}
```

---

## get_material_bump_rotation
Pattern: R
Description: Get the bump texture rotation of the active material (radians). Reports (also) error when no material is set

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Materials.GetBumpRotation(docId);
    return "GetBumpRotation of the active material: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no material set on this document?): " + ex.Message;
}
```

---

## get_material_anisotropy_angle_factor
Pattern: R
Description: Get the anisotropy angle factor of the active material. Reports (also) error when no material is set

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Materials.GetAnisotropyAngleFactor(docId);
    return "GetAnisotropyAngleFactor of the active material: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no material set on this document?): " + ex.Message;
}
```

---

## get_coating_brinell_hardness_factor
Pattern: R
Description: Get the Brinell hardness factor of the active coating

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Coatings.GetBrinellHardnessFactor(docId);
    return "GetBrinellHardnessFactor of the active coating: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no coating set on this document?): " + ex.Message;
}
```

---

## get_coating_vickers_hardness_factor
Pattern: R
Description: Get the Vickers hardness factor of the active coating

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Coatings.GetVickersHardnessFactor(docId);
    return "GetVickersHardnessFactor of the active coating: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no coating set on this document?): " + ex.Message;
}
```

---

## get_coating_glossiness_factor
Pattern: R
Description: Get the glossiness factor of the active coating

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Coatings.GetGlossinessFactor(docId);
    return "GetGlossinessFactor of the active coating: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no coating set on this document?): " + ex.Message;
}
```

---

## get_coating_bump_scale
Pattern: R
Description: Get the bump texture scale factor of the active coating

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Coatings.GetBumpScale(docId);
    return "GetBumpScale of the active coating: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no coating set on this document?): " + ex.Message;
}
```

---

## get_coating_bump_rotation
Pattern: R
Description: Get the bump texture rotation of the active coating (radians)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Coatings.GetBumpRotation(docId);
    return "GetBumpRotation of the active coating: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no coating set on this document?): " + ex.Message;
}
```

---

## get_coating_anisotropy_angle_factor
Pattern: R
Description: Get the anisotropy angle factor of the active coating

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    double factor = TopSolidDesignHost.Coatings.GetAnisotropyAngleFactor(docId);
    return "GetAnisotropyAngleFactor of the active coating: " + factor.ToString("F4");
}
catch (Exception ex)
{
    return "Error (no coating set on this document?): " + ex.Message;
}
```

---

## get_profiles_folder
Pattern: R
Description: Gets the profiles folder entity of the active document (name + id)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
try
{
    ElementId folder = TopSolidHost.Geometries3D.GetProfilesFolder(docId);
    if (folder.IsEmpty) return "No profiles folder (this document type has none).";
    string name = "?";
    try { name = TopSolidHost.Elements.GetFriendlyName(folder); } catch {}
    return "Profiles folder: " + name + " [" + folder.ToString() + "]";
}
catch (Exception ex)
{
    return "Error: " + ex.Message;
}
```

---

## get_operation_version
Pattern: R
Description: Gets the algorithm version of an operation. Param: value=operation_name (use list_operations to find it)

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
string target = "{value}".Trim();
if (target.Length == 0) return "Param required: value=operation_name (use list_operations to find operations).";
var ops = TopSolidHost.Operations.GetOperations(docId);
foreach (var op in ops)
{
    string name = "?";
    try { name = TopSolidHost.Elements.GetFriendlyName(op); } catch { continue; }
    if (name.IndexOf(target, System.StringComparison.OrdinalIgnoreCase) >= 0)
    {
        try
        {
            string version = TopSolidHost.Operations.GetOperationVersion(op);
            return "Operation '" + name + "' algorithm version: " + (string.IsNullOrEmpty(version) ? "(unknown)" : version);
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }
}
return "Operation '" + target + "' not found (use list_operations).";
```

---

## get_edge_faces
Pattern: R
Description: Lists the faces bordering a selected edge (count + names)

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select an edge", "");
UserAnswerType answer = TopSolidHost.User.AskEdge(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try
{
    var faces = TopSolidHost.Shapes.GetEdgeFaces(selected);
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("Faces bordering the edge: " + faces.Count);
    int i = 0;
    foreach (var f in faces)
    {
        i++;
        string fInfo = "?";
        try
        {
            double area = TopSolidHost.Shapes.GetFaceArea(f);
            fInfo = "area=" + (area * 1e6).ToString("F1") + " cm2";
        }
        catch {}
        sb.AppendLine("  face " + i + ": " + fInfo);
    }
    return sb.ToString();
}
catch (Exception ex)
{
    return "Error: " + ex.Message;
}
```

---

## get_edge_ellipse_curve
Pattern: R
Description: Gets the ellipse (plane + radii, mm) of a selected elliptical edge

```csharp
ElementItemId selected = default(ElementItemId);
UserQuestion q = new UserQuestion("Select an elliptical edge", "");
UserAnswerType answer = TopSolidHost.User.AskEdge(q, default(ElementItemId), out selected);
if (answer != UserAnswerType.Ok) return "Selection cancelled.";
try
{
    CurveType ct = TopSolidHost.Shapes.GetEdgeCurveType(selected);
    if ((int)ct == 3)
    {
        Plane3D plane;
        double radiusX, radiusY;
        TopSolidHost.Shapes.GetEdgeEllipseCurve(selected, out plane, out radiusX, out radiusY);
        Point3D o = plane.Origin;
        Direction3D n = plane.Normal;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Ellipse edge:");
        sb.AppendLine("  RadiusX: " + (radiusX * 1000).ToString("F3") + " mm");
        sb.AppendLine("  RadiusY: " + (radiusY * 1000).ToString("F3") + " mm");
        sb.AppendLine("  Center: (" + (o.X * 1000).ToString("F2") + ", " + (o.Y * 1000).ToString("F2") + ", " + (o.Z * 1000).ToString("F2") + ") mm");
        sb.AppendLine("  Normal: (" + n.X.ToString("F3") + ", " + n.Y.ToString("F3") + ", " + n.Z.ToString("F3") + ")");
        return sb.ToString();
    }
    return "The selected edge is not elliptical (curve type: " + ct + "). Select an ellipse edge.";
}
catch (Exception ex)
{
    return "Error (is the selection an elliptical edge?): " + ex.Message;
}
```

---

## smooth_sketch_profiles
Pattern: RW
Description: Smooths the profiles of the last 2D sketch (G1 continuity). WARNING: modifies the sketch. Param: value=empty (defaults) or tolerance_mm

```csharp
if (docId.IsEmpty) { __message = "No document open."; return; }
var sketches = TopSolidHost.Sketches2D.GetSketches(docId);
if (sketches.Count == 0) { __message = "No 2D sketch in this document."; return; }
ElementId sketchId = sketches[sketches.Count - 1];
string skName = "?";
try { skName = TopSolidHost.Elements.GetFriendlyName(sketchId); } catch {}
string tolText = "{value}".Trim();
double linearTolerance = 1e-4;
if (tolText.Length > 0)
{
    double parsed;
    if (double.TryParse(tolText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed > 0)
        linearTolerance = parsed / 1000.0;
}
var profiles = TopSolidHost.Sketches2D.GetProfiles(sketchId);
if (profiles == null || profiles.Count == 0) { __message = "No profile found in sketch '" + skName + "'."; return; }
try
{
    TopSolidHost.Sketches2D.StartModification(sketchId);
    SmartReal zeroInfluence = new SmartReal(UnitType.Length, 0.0);
    SmartReal linTol = new SmartReal(UnitType.Length, linearTolerance);
    SmartReal angTol = new SmartReal(UnitType.Angle, 0.05);
    SmartInteger nbPts = new SmartInteger(0);
    TopSolidHost.Sketches2D.SmoothSketchProfiles(sketchId, profiles, SmoothingType.LocalWithPunctualInfluence, zeroInfluence, linTol, angTol, nbPts, true);
    TopSolidHost.Sketches2D.EndModification();
    __message = "OK: profiles of sketch '" + skName + "' smoothed (linear tolerance=" + (linearTolerance * 1000).ToString("F3") + " mm)";
}
catch (Exception ex)
{
    try { TopSolidHost.Sketches2D.EndModification(); } catch {}
    __message = "Error (sketch already modified?): " + ex.Message;
}
```

---

## set_angular_scale
Pattern: RW
Description: Sets the angular scale factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetAngularScale(docId, value);
    __message = "OK: angular scale factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_anisotropy_angle_factor
Pattern: RW
Description: Sets the anisotropy angle factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetAnisotropyAngleFactor(docId, value);
    __message = "OK: anisotropy angle factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_brinell_hardness_factor
Pattern: RW
Description: Sets the Brinell hardness factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetBrinellHardnessFactor(docId, value);
    __message = "OK: Brinell hardness factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_bulk_modulus
Pattern: RW
Description: Sets the bulk modulus of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetBulkModulus(docId, value);
    __message = "OK: bulk modulus set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_bump_rotation
Pattern: RW
Description: Sets the bump texture rotation angle of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetBumpRotation(docId, value);
    __message = "OK: bump texture rotation angle set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_bump_scale
Pattern: RW
Description: Sets the bump texture scale factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetBumpScale(docId, value);
    __message = "OK: bump texture scale factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_cutting_force
Pattern: RW
Description: Sets the cutting force of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetCuttingForce(docId, value);
    __message = "OK: cutting force set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_cutting_force_increment
Pattern: RW
Description: Sets the cutting force increment of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetCuttingForceIncrement(docId, value);
    __message = "OK: cutting force increment set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_elastic_limit
Pattern: RW
Description: Sets the elastic limit of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetElasticLimit(docId, value);
    __message = "OK: elastic limit set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_elongation_at_break
Pattern: RW
Description: Sets the elongation at break of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetElongationAtBreak(docId, value);
    __message = "OK: elongation at break set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_glossiness_factor
Pattern: RW
Description: Sets the glossiness factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetGlossinessFactor(docId, value);
    __message = "OK: glossiness factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_melting_temperature
Pattern: RW
Description: Sets the melting temperature of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetMeltingTemperature(docId, value);
    __message = "OK: melting temperature set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_plastic_strain_ratio
Pattern: RW
Description: Sets the plastic strain ratio of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetPlasticStrainRatio(docId, value);
    __message = "OK: plastic strain ratio set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_poisson_ratio
Pattern: RW
Description: Sets the Poisson ratio of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetPoissonRatio(docId, value);
    __message = "OK: Poisson ratio set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_section_reduction_at_break
Pattern: RW
Description: Sets the section reduction at break of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetSectionReductionAtBreak(docId, value);
    __message = "OK: section reduction at break set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_shear_modulus
Pattern: RW
Description: Sets the shear modulus of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetShearModulus(docId, value);
    __message = "OK: shear modulus set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_shrinkage_factor
Pattern: RW
Description: Sets the shrinkage factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetShrinkageFactor(docId, value);
    __message = "OK: shrinkage factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_specific_heat_capacity
Pattern: RW
Description: Sets the specific heat capacity of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetSpecificHeatCapacity(docId, value);
    __message = "OK: specific heat capacity set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_thermal_conductivity
Pattern: RW
Description: Sets the thermal conductivity of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetThermalConductivity(docId, value);
    __message = "OK: thermal conductivity set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_thermal_expansion_coefficient
Pattern: RW
Description: Sets the thermal expansion coefficient of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetThermalExpansionCoefficient(docId, value);
    __message = "OK: thermal expansion coefficient set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_ultimate_stress
Pattern: RW
Description: Sets the ultimate stress of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetUltimateStress(docId, value);
    __message = "OK: ultimate stress set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_vertical_angular_scale
Pattern: RW
Description: Sets the vertical angular scale factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetVerticalAngularScale(docId, value);
    __message = "OK: vertical angular scale factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_vickers_hardness_factor
Pattern: RW
Description: Sets the Vickers hardness factor of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetVickersHardnessFactor(docId, value);
    __message = "OK: Vickers hardness factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_young_modulus
Pattern: RW
Description: Sets the Young modulus of the material applied to the edited document. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Materials.SetYoungModulus(docId, value);
    __message = "OK: Young modulus set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_homogeneous_parameter_value
Pattern: RW
Description: Sets the homogeneous parameter value (bool) of the material. Param: value=empty(unset)/true/false

```csharp
bool parsed = false;
if (raw != null) { bool.TryParse(raw, out parsed); }
if (raw != null && !parsed && raw != "true" && raw != "false")
{
    __message = "Invalid value: use empty (unset) or true/false."; return;
}
try
{
    bool result = TopSolidDesignHost.Materials.SetHomogeneousParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);
    __message = "OK: homogeneous parameter value (bool) of the material set to " + raw + " (API result=" + result + ")";
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_isotropic_parameter_value
Pattern: RW
Description: Sets the isotropic parameter value (bool) of the material. Param: value=empty(unset)/true/false

```csharp
bool parsed = false;
if (raw != null) { bool.TryParse(raw, out parsed); }
if (raw != null && !parsed && raw != "true" && raw != "false")
{
    __message = "Invalid value: use empty (unset) or true/false."; return;
}
try
{
    bool result = TopSolidDesignHost.Materials.SetIsotropicParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);
    __message = "OK: isotropic parameter value (bool) of the material set to " + raw + " (API result=" + result + ")";
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_rockwell_factors
Pattern: RW
Description: Sets the Rockwell factors (A,B,C) of the material applied to the edited document. Param: value=A,B,C (double or 'null')

```csharp
    string[] parts = raw.Split(',');
    if (parts.Length != 3)
    {
        __message = "Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset)."; return;
    }
    double?[] vals = new double?[3];
    for (int i = 0; i < 3; i++)
    {
        string p = parts[i].Trim();
        if (p.ToLowerInvariant() == "null") { vals[i] = null; continue; }
        double d;
        if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
        {
            __message = "Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset)."; return;
        }
        vals[i] = d;
    }
try
{
    TopSolidDesignHost.Materials.SetRockwellFactors(docId, vals[0], vals[1], vals[2]);
    __message = "OK: Rockwell factors set to A=" + vals[0] + " B=" + vals[1] + " C=" + vals[2];
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_specular_color
Pattern: RW
Description: Sets the specular color (R,G,B) of the material applied to the edited document. Param: value=R,G,B (0-255)

```csharp
    string[] parts = raw.Split(',');
    if (parts.Length != 3)
    {
        __message = "Invalid value: use R,G,B (0-255)."; return;
    }
    int r, g, b;
    if (!int.TryParse(parts[0].Trim(), out r) || !int.TryParse(parts[1].Trim(), out g) || !int.TryParse(parts[2].Trim(), out b) || r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
    {
        __message = "Invalid value: use R,G,B (0-255)."; return;
    }
try
{
    TopSolidDesignHost.Materials.SetSpecularColor(docId, new Color((byte)r, (byte)g, (byte)b));
    __message = "OK: specular color set to R=" + r + " G=" + g + " B=" + b;
}
catch (Exception ex)
{
    __message = "Error (no material applied to this document?): " + ex.Message;
}
```

---

## set_coating_brinell_hardness_factor
Pattern: RW
Description: Sets the Brinell hardness factor of the active coating. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Coatings.SetBrinellHardnessFactor(docId, value);
    __message = "OK: Brinell hardness factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## set_coating_bump_rotation
Pattern: RW
Description: Sets the bump texture rotation angle of the active coating. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Coatings.SetBumpRotation(docId, value);
    __message = "OK: bump texture rotation angle set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## set_coating_bump_scale
Pattern: RW
Description: Sets the bump texture scale factor of the active coating. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Coatings.SetBumpScale(docId, value);
    __message = "OK: bump texture scale factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## set_coating_vickers_hardness_factor
Pattern: RW
Description: Sets the Vickers hardness factor of the active coating. Param: value=<number>

```csharp
{
    double value = double.Parse("{value}".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    TopSolidDesignHost.Coatings.SetVickersHardnessFactor(docId, value);
    __message = "OK: Vickers hardness factor set to " + value.ToString("F4");
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## set_coating_homogeneous_parameter_value
Pattern: RW
Description: Sets the homogeneous parameter value (bool) of the active coating. Param: value=empty(unset)/true/false

```csharp
bool parsed = false;
if (raw != null) { bool.TryParse(raw, out parsed); }
if (raw != null && !parsed && raw != "true" && raw != "false")
{
    __message = "Invalid value: use empty (unset) or true/false."; return;
}
try
{
    bool result = TopSolidDesignHost.Coatings.SetHomogeneousParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);
    __message = "OK: homogeneous parameter value (bool) set to " + raw + " (API result=" + result + ")";
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## set_coating_isotropic_parameter_value
Pattern: RW
Description: Sets the isotropic parameter value (bool) of the active coating. Param: value=empty(unset)/true/false

```csharp
bool parsed = false;
if (raw != null) { bool.TryParse(raw, out parsed); }
if (raw != null && !parsed && raw != "true" && raw != "false")
{
    __message = "Invalid value: use empty (unset) or true/false."; return;
}
try
{
    bool result = TopSolidDesignHost.Coatings.SetIsotropicParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);
    __message = "OK: isotropic parameter value (bool) set to " + raw + " (API result=" + result + ")";
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## set_coating_rockwell_factors
Pattern: RW
Description: Sets the Rockwell factors (A,B,C) of the active coating. Param: value=A,B,C (double or 'null')

```csharp
    string[] parts = raw.Split(',');
    if (parts.Length != 3)
    {
        __message = "Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset)."; return;
    }
    double?[] vals = new double?[3];
    for (int i = 0; i < 3; i++)
    {
        string p = parts[i].Trim();
        if (p.ToLowerInvariant() == "null") { vals[i] = null; continue; }
        double d;
        if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
        {
            __message = "Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset)."; return;
        }
        vals[i] = d;
    }
try
{
    TopSolidDesignHost.Coatings.SetRockwellFactors(docId, vals[0], vals[1], vals[2]);
    __message = "OK: Rockwell factors set to A=" + vals[0] + " B=" + vals[1] + " C=" + vals[2];
}
catch (Exception ex)
{
    __message = "Error (no coating set on this document?): " + ex.Message;
}
```

---

## add_page_to_bundle
Pattern: RW
Description: Adds a new page to the opened drafting bundle. No value expected.

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    TopSolidDraftingHost.Draftings.AddPageToBundle(docId);
    int pageCount = TopSolidDraftingHost.Draftings.GetPageCount(docId);
    __message = "OK: new page added, bundle now has " + pageCount + " pages.";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## set_current_bundle_page
Pattern: RW
Description: Sets the current page of the opened drafting bundle. Param: value=page_number (e.g. 1)

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    int page = int.Parse("{value}".Trim(), System.Globalization.CultureInfo.InvariantCulture);
    if (page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))
    {
        __message = "Page number out of range (1 to " + TopSolidDraftingHost.Draftings.GetPageCount(docId) + "): " + page; return;
    }
    TopSolidDraftingHost.Draftings.SetCurrentBundlePage(docId, page);
    __message = "OK: current bundle page set to " + page;
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## set_bundle_page_format_name
Pattern: RW
Description: Sets the format name of a bundle page to 'A3'. Param: value=page_number (e.g. 1)

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    int page = int.Parse("{value}".Trim(), System.Globalization.CultureInfo.InvariantCulture);
    if (page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))
    {
        __message = "Page number out of range (1 to " + TopSolidDraftingHost.Draftings.GetPageCount(docId) + "): " + page; return;
    }
    TopSolidDraftingHost.Draftings.SetBundlePageFormatName(docId, page, "A3");
    __message = "OK: format name A3 set on page " + page;
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## set_bundle_page_format
Pattern: RW
Description: Sets the format of a bundle page. Param: value=page;format_name (e.g. 1;A3 landscape)

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    string raw = "{value}".Trim();
    int sep = raw.IndexOf(';');
    int page; string fmt;
    if (sep <= 0 || sep >= raw.Length - 1 || !int.TryParse(raw.Substring(0, sep).Trim(), out page) || (fmt = raw.Substring(sep + 1).Trim()).Length == 0 || page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))
    {
        __message = "Use value=page;format (e.g. 1;A3 landscape)."; return;
    }
    TopSolidDraftingHost.Draftings.SetBundlePageFormat(docId, page, fmt);
    __message = "OK: page " + page + " format set to '" + fmt + "'.";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## set_bundle_page_dimensions
Pattern: RW
Description: Sets the dimensions of a bundle page. Param: value=page;width;height (mm, e.g. 1;420;297)

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    string raw = "{value}".Trim();
    string[] parts = raw.Split(';');
    double w, h; int page;
    if (parts.Length != 3 || !int.TryParse(parts[0].Trim(), out page) || !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w) || !double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out h) || page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))
    {
        __message = "Use value=page;width;height (mm, e.g. 1;420;297)."; return;
    }
    TopSolidDraftingHost.Draftings.SetBundlePageDimensions(docId, page, new Real(UnitType.Length, "mm", w), new Real(UnitType.Length, "mm", h));
    __message = "OK: page " + page + " dimensions set to " + w + " x " + h + " mm.";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## include_draft_in_bundle
Pattern: RW
Description: Includes a draft (all pages on the same bundle page) in the opened bundle. Param: value=draft_doc_name

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
    var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
    if (results.Count == 0)
    {
        __message = "Draft document not found: " + "{value}"; return;
    }
    DocumentId draftDoc = TopSolidHost.Documents.GetDocument(results[0]);
    TopSolidDraftingHost.Draftings.IncludeDraftInBundle(docId, draftDoc, true);
    __message = "OK: draft '" + "{value}" + "' included in the bundle (all pages on the same bundle page).";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## include_draft_page_in_bundle_current_page
Pattern: RW
Description: Includes a specific draft page in the current bundle page with offset. Param: value=draft_doc_name;page;offset (e.g. draft;1;0)

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    string raw = "{value}".Trim();
    string[] parts = raw.Split(';');
    int page; double off;
    if (parts.Length != 3 || !int.TryParse(parts[1].Trim(), out page) || !double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out off))
    {
        __message = "Use value=draft_doc_name;page;offset (e.g. draft;1;0)."; return;
    }
    PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
    var results = TopSolidHost.Pdm.SearchDocumentByName(projId, parts[0].Trim());
    if (results.Count == 0)
    {
        __message = "Draft document not found: " + parts[0].Trim(); return;
    }
    DocumentId draftDoc = TopSolidHost.Documents.GetDocument(results[0]);
    TopSolidDraftingHost.Draftings.IncludeDraftPageInBundleCurrentPage(docId, draftDoc, page, off, off);
    __message = "OK: page " + page + " of draft included at offset " + off + ".";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## add_draft_to_bundle
Pattern: RW
Description: Adds a draft to the opened bundle (its pages become separate bundle pages). Param: value=draft_doc_name

```csharp
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle. Open the bundle first."; return;
    }
    PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();
    var results = TopSolidHost.Pdm.SearchDocumentByName(projId, "{value}");
    if (results.Count == 0)
    {
        __message = "Draft document not found: " + "{value}"; return;
    }
    DocumentId draftDoc = TopSolidHost.Documents.GetDocument(results[0]);
    TopSolidDraftingHost.Draftings.AddDraftToBundle(docId, draftDoc);
    __message = "OK: draft '" + "{value}" + "' added (its pages are now bundle pages).";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
}
```

---

## get_page_count
Pattern: R
Description: Returns the number of pages of the opened drafting bundle.

```csharp
if (docId.IsEmpty) { __message = "No document open. Open the bundle in TopSolid first."; return; }
try
{
    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))
    {
        __message = "The edited document is not a drafting bundle."; return;
    }
    __message = "OK: " + TopSolidDraftingHost.Draftings.GetPageCount(docId) + " page(s).";
}
catch (Exception ex)
{
    __message = "Error: " + ex.Message;
```

---

## create_project_tree
Pattern: RW
Description: Creates or reuses a PDM project and optional comma-separated folders, and sets it as current project. Param: value=project_name[;folder1,folder2,...]

```csharp
if (false) { TopSolidHost.Application.StartModification("", false); TopSolidHost.Application.EndModification(false, false); }
string raw = "{value}".Trim();
if (string.IsNullOrEmpty(raw)) return "Error: Use value=project_name[;folder1,folder2,...]";
string[] parts = raw.Split(';');
string projName = parts[0].Trim();
PdmObjectId projPdm = PdmObjectId.Empty;
foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))
{
    if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }
}
bool createdProj = false;
if (projPdm.IsEmpty)
{
    projPdm = TopSolidHost.Pdm.CreateProject(projName, false);
    TopSolidHost.Pdm.Save(projPdm, true);
    createdProj = true;
}
int createdFolders = 0;
if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1].Trim()))
{
    List<PdmObjectId> existFolders; List<PdmObjectId> existDocs;
    TopSolidHost.Pdm.GetConstituents(projPdm, out existFolders, out existDocs);
    foreach (string fRaw in parts[1].Split(','))
    {
        string fName = fRaw.Trim();
        if (string.IsNullOrEmpty(fName)) continue;
        bool exists = false;
        foreach (var ef in existFolders)
        {
            if (string.Equals(TopSolidHost.Pdm.GetName(ef), fName, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
        }
        if (!exists) { TopSolidHost.Pdm.CreateFolder(projPdm, fName); createdFolders++; }
    }
    TopSolidHost.Pdm.Save(projPdm, true);
}
return "OK: Project '" + projName + "' (" + (createdProj ? "created" : "reused") + "), folders created: " + createdFolders + ".";
```

---

## create_part_document
Pattern: RW
Description: Creates or reuses a Part document (.TopPrt) in a project/folder, sets Name/Designation/Reference, and opens it. Param: value=doc_name[;designation[;reference[;folder_name[;project_name]]]]

```csharp
string raw = "{value}".Trim();
if (string.IsNullOrEmpty(raw)) return "Error: Use value=doc_name[;designation[;reference[;folder_name[;project_name]]]]";
string[] parts = raw.Split(';');
string docName = parts[0].Trim();
string desig = parts.Length > 1 ? parts[1].Trim() : "";
string refer = parts.Length > 2 ? parts[2].Trim() : "";
string folderName = parts.Length > 3 ? parts[3].Trim() : "";
string projName = parts.Length > 4 ? parts[4].Trim() : "";
PdmObjectId projPdm = PdmObjectId.Empty;
if (!string.IsNullOrEmpty(projName))
{
    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))
        if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }
}
if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();
if (projPdm.IsEmpty) return "Error: No target project found.";
PdmObjectId ownerPdm = projPdm;
if (!string.IsNullOrEmpty(folderName))
{
    List<PdmObjectId> fList; List<PdmObjectId> dList;
    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);
    foreach (var f in fList)
        if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }
    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);
}
List<PdmObjectId> subF; List<PdmObjectId> subD;
TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);
PdmObjectId docPdm = PdmObjectId.Empty;
foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }
if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, ".TopPrt", true);
TopSolidHost.Pdm.SetName(docPdm, docName);
if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);
if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);
DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);
TopSolidHost.Documents.Open(ref dId);
if (false) { TopSolidHost.Application.StartModification("", false); TopSolidHost.Application.EndModification(false, false); }
TopSolidHost.Pdm.Save(docPdm, true);
return "OK: Part document '" + docName + "' (.TopPrt) ready and saved.";
```

---

## create_assembly_document
Pattern: RW
Description: Creates or reuses an Assembly document (.TopAsm) in a project/folder, sets Name/Designation/Reference, and opens it. Param: value=doc_name[;designation[;reference[;folder_name[;project_name]]]]

```csharp
string raw = "{value}".Trim();
if (string.IsNullOrEmpty(raw)) return "Error: Use value=doc_name[;designation[;reference[;folder_name[;project_name]]]]";
string[] parts = raw.Split(';');
string docName = parts[0].Trim();
string desig = parts.Length > 1 ? parts[1].Trim() : "";
string refer = parts.Length > 2 ? parts[2].Trim() : "";
string folderName = parts.Length > 3 ? parts[3].Trim() : "";
string projName = parts.Length > 4 ? parts[4].Trim() : "";
PdmObjectId projPdm = PdmObjectId.Empty;
if (!string.IsNullOrEmpty(projName))
{
    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))
        if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }
}
if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();
if (projPdm.IsEmpty) return "Error: No target project found.";
PdmObjectId ownerPdm = projPdm;
if (!string.IsNullOrEmpty(folderName))
{
    List<PdmObjectId> fList; List<PdmObjectId> dList;
    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);
    foreach (var f in fList)
        if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }
    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);
}
List<PdmObjectId> subF; List<PdmObjectId> subD;
TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);
PdmObjectId docPdm = PdmObjectId.Empty;
foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }
if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, ".TopAsm", true);
TopSolidHost.Pdm.SetName(docPdm, docName);
if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);
if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);
DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);
TopSolidHost.Documents.Open(ref dId);
if (false) { TopSolidHost.Application.StartModification("", false); TopSolidHost.Application.EndModification(false, false); }
TopSolidHost.Pdm.Save(docPdm, true);
return "OK: Assembly document '" + docName + "' (.TopAsm) ready and saved.";
```

---

## set_material
Pattern: RW
Description: Searches a standard material (.TopMat) in material libraries (<250ms) and applies it to a part while enabling physical properties (Mass, CoG). Param: value=[doc_name;]material_keyword (e.g. S235JR or GC_Montant_50x10;S235JR)

```csharp
string raw = "{value}".Trim();
if (string.IsNullOrEmpty(raw)) return "Error: Provide material name or doc_name;material_name.";
string targetDocName = "";
string matQuery = raw;
if (raw.Contains(";"))
{
    string[] sp = raw.Split(';');
    targetDocName = sp[0].Trim();
    matQuery = sp[1].Trim();
}
DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;
PdmObjectId targetPdm = targetDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(targetDoc);
if (!string.IsNullOrEmpty(targetDocName))
{
    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
    var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetDocName);
    if (found.Count == 0) return "Error: Document '" + targetDocName + "' not found in current project.";
    targetPdm = found[0];
    targetDoc = TopSolidHost.Documents.GetDocument(targetPdm);
}
if (targetDoc.IsEmpty) return "Error: No active document and no doc_name specified.";
PdmObjectId matPdm = PdmObjectId.Empty;
string pattern = matQuery.Contains("*") ? matQuery : ("*" + matQuery + "*");
foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))
{
    string lName = TopSolidHost.Pdm.GetName(lib);
    bool matchLib = lName.IndexOf("CEN", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Mat", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Bois", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0;
    if (!matchLib) continue;
    var hits = TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, pattern);
    foreach (var h in hits)
    {
        string ext;
        TopSolidHost.Pdm.GetType(h, out ext);
        if (string.Equals(ext, ".TopMat", StringComparison.OrdinalIgnoreCase))
        {
            matPdm = h;
            if (string.Equals(TopSolidHost.Pdm.GetName(h), matQuery, StringComparison.OrdinalIgnoreCase)) break;
        }
    }
    if (!matPdm.IsEmpty && string.Equals(TopSolidHost.Pdm.GetName(matPdm), matQuery, StringComparison.OrdinalIgnoreCase)) break;
}
if (matPdm.IsEmpty) return "Error: Material matching '" + matQuery + "' (.TopMat) not found in standard libraries.";
DocumentId matDoc = TopSolidHost.Documents.GetDocument(matPdm);
string matRealName = TopSolidHost.Pdm.GetName(matPdm);
TopSolidHost.Application.StartModification("SetMaterial_" + matRealName, false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);
    TopSolidDesignHost.Parts.SetMaterial(targetDoc, matDoc);
    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(targetDoc, true, new SmartReal(UnitType.Mass, 0)); } catch { }
    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(targetDoc, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }
    try { TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(targetDoc, true); } catch { }
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
if (!targetPdm.IsEmpty) TopSolidHost.Pdm.Save(targetPdm, true);
return "OK: Material '" + matRealName + "' applied to '" + TopSolidHost.Documents.GetName(targetDoc) + "' (Mass & CoG enabled).";
```

---

## set_coating
Pattern: RW
Description: Searches a standard coating (.TopCot, e.g. RAL 7016) in libraries and applies it (or RGB color) to all shapes of a part. Param: value=[doc_name;]coating_keyword[;R,G,B]

```csharp
string raw = "{value}".Trim();
if (string.IsNullOrEmpty(raw)) return "Error: Provide coating name or doc_name;coating_name[;R,G,B].";
string[] parts = raw.Split(';');
string targetDocName = "";
string cotQuery = "";
string rgbSpec = "";
if (parts.Length == 1) { cotQuery = parts[0].Trim(); }
else if (parts.Length == 2)
{
    if (parts[1].Contains(",")) { cotQuery = parts[0].Trim(); rgbSpec = parts[1].Trim(); }
    else { targetDocName = parts[0].Trim(); cotQuery = parts[1].Trim(); }
}
else { targetDocName = parts[0].Trim(); cotQuery = parts[1].Trim(); rgbSpec = parts[2].Trim(); }
DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;
PdmObjectId targetPdm = targetDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(targetDoc);
if (!string.IsNullOrEmpty(targetDocName))
{
    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
    var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetDocName);
    if (found.Count == 0) return "Error: Document '" + targetDocName + "' not found in current project.";
    targetPdm = found[0];
    targetDoc = TopSolidHost.Documents.GetDocument(targetPdm);
}
if (targetDoc.IsEmpty) return "Error: No active document.";
PdmObjectId cotPdm = PdmObjectId.Empty;
if (!string.IsNullOrEmpty(cotQuery))
{
    string pattern = cotQuery.Contains("*") ? cotQuery : ("*" + cotQuery + "*");
    foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))
    {
        string lName = TopSolidHost.Pdm.GetName(lib);
        bool matchLib = lName.IndexOf("RAL", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Coating", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("MetalWork", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!matchLib) continue;
        var hits = TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, pattern);
        foreach (var h in hits)
        {
            string ext;
            TopSolidHost.Pdm.GetType(h, out ext);
            if (string.Equals(ext, ".TopCot", StringComparison.OrdinalIgnoreCase)) { cotPdm = h; break; }
        }
        if (!cotPdm.IsEmpty) break;
    }
}
DocumentId cotDoc = cotPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(cotPdm);
TopSolidHost.Application.StartModification("SetCoating", false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);
    string status = "";
    if (!cotDoc.IsEmpty)
    {
        try
        {
            Color alb = TopSolidDesignHost.Coatings.GetAlbedoColor(cotDoc);
            foreach (var s in TopSolidHost.Shapes.GetShapes(targetDoc))
                TopSolidHost.Elements.SetColor(s, alb);
        } catch { }
        status = "Coating '" + TopSolidHost.Pdm.GetName(cotPdm) + "'";
    }
    if (!string.IsNullOrEmpty(rgbSpec))
    {
        string[] rgb = rgbSpec.Split(',');
        if (rgb.Length == 3)
        {
            byte r = byte.Parse(rgb[0].Trim()); byte g = byte.Parse(rgb[1].Trim()); byte b = byte.Parse(rgb[2].Trim());
            foreach (var s in TopSolidHost.Shapes.GetShapes(targetDoc))
                try { TopSolidHost.Elements.SetColor(s, new Color(r, g, b)); } catch { }
            status += (status.Length > 0 ? " + " : "") + "RGB(" + r + "," + g + "," + b + ")";
        }
    }
    TopSolidHost.Application.EndModification(true, true);
    if (!targetPdm.IsEmpty) TopSolidHost.Pdm.Save(targetPdm, true);
    return "OK: " + (status.Length > 0 ? status : "No coating matched") + " on '" + TopSolidHost.Documents.GetName(targetDoc) + "'.";
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
```

---

## provide_extruded_bar_function
Pattern: RW
Description: Provides the TopSolid 'Extruded Bar' (Profilé) function on a part with its 3D Frame (XY section, X horizontal, Z longitudinal), Section sketch, Length parameter L, BottomLeft/TopRight points, and Minimal EnclosingBox. Param: value=[doc_name;]width_x_mm;height_y_mm;length_z_mm

```csharp
string raw = "{value}".Trim();
string[] parts = raw.Split(';');
if (parts.Length < 3) return "Error: Use value=[doc_name;]width_x_mm;height_y_mm;length_z_mm";
int idx = parts.Length >= 4 ? 1 : 0;
string targetDocName = idx == 1 ? parts[0].Trim() : "";
double wx = double.Parse(parts[idx].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double hy = double.Parse(parts[idx + 1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double lz = double.Parse(parts[idx + 2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;
PdmObjectId targetPdm = targetDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(targetDoc);
if (!string.IsNullOrEmpty(targetDocName))
{
    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
    var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetDocName);
    if (found.Count == 0) return "Error: Document '" + targetDocName + "' not found.";
    targetPdm = found[0];
    targetDoc = TopSolidHost.Documents.GetDocument(targetPdm);
}
PdmObjectId fncPdm = PdmObjectId.Empty;
foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))
{
    if (!string.Equals(TopSolidHost.Pdm.GetName(lib), "TopSolid", StringComparison.OrdinalIgnoreCase)) continue;
    var hits = TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, "Extruded Bar*");
    foreach (var h in hits) { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == ".TopPrtFct") { fncPdm = h; break; } }
    if (!fncPdm.IsEmpty) break;
}
if (fncPdm.IsEmpty) return "Error: 'Extruded Bar.TopPrtFct' function not found in TopSolid library.";
DocumentId fncDoc = TopSolidHost.Documents.GetDocument(fncPdm);
TopSolidHost.Application.StartModification("ProvideExtrudedBarFunction", false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);
    ElementId skId = ElementId.Empty;
    foreach (var e in TopSolidHost.Elements.GetElements(targetDoc))
        if (TopSolidHost.Elements.GetTypeFullName(e).IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0) { skId = e; break; }
    ElementId lParam = ElementId.Empty;
    foreach (var p in TopSolidHost.Parameters.GetParameters(targetDoc))
        if (TopSolidHost.Parameters.GetParameterType(p) == ParameterType.Real && TopSolidHost.Elements.GetName(p) == "L") { lParam = p; break; }
    if (lParam.IsEmpty)
    {
        lParam = TopSolidHost.Parameters.CreateRealParameter(targetDoc, UnitType.Length, lz);
        TopSolidHost.Elements.SetName(lParam, "L");
    }
    ElementId profFrame = TopSolidHost.Geometries3D.CreateFrame(targetDoc, new Frame3D(new Point3D(0, 0, 0), Direction3D.DX, Direction3D.DY, Direction3D.DZ));
    try { TopSolidHost.Elements.SetName(profFrame, "Repere_ExtrudedBar_XY_Z"); } catch { }
    ElementId fnc = ElementId.Empty;
    foreach (var f in TopSolidHost.Entities.GetFunctions(targetDoc)) { fnc = f; break; }
    if (fnc.IsEmpty) fnc = TopSolidHost.Entities.ProvideFunction(targetDoc, fncDoc, "");
    foreach (var pub in TopSolidHost.Entities.GetFunctionPublishings(fnc))
    {
        string pName = TopSolidHost.Elements.GetName(pub);
        if (pName == "Section" && !skId.IsEmpty) TopSolidHost.Geometries3D.SetSectionPublishingDefinition(pub, new SmartSection3D(skId));
        else if (pName == "Length" && !lParam.IsEmpty) TopSolidHost.Parameters.SetRealPublishingDefinition(pub, new SmartReal(lParam));
        else if (pName == "Frame" && !profFrame.IsEmpty) TopSolidHost.Geometries3D.SetFramePublishingDefinition(pub, new SmartFrame3D(profFrame, false));
        else if (pName == "BottomLeft") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(-wx / 2.0, -hy / 2.0, 0)));
        else if (pName == "TopRight") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(wx / 2.0, hy / 2.0, 0)));
    }
    try { TopSolidDesignHost.Parts.SetEnclosingBoxManagement(targetDoc, EnclosingBoxManagementType.Minimal, new SmartFrame3D(profFrame, false), new SmartFrame3D(profFrame, false), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0)); } catch { }
    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(targetDoc, true, new SmartReal(UnitType.Mass, 0)); } catch { }
    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(targetDoc, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
if (!targetPdm.IsEmpty) TopSolidHost.Pdm.Save(targetPdm, true);
return "OK: Function 'Extruded Bar' + Frame (XY section, X horizontal, Z longitudinal) + Length L wired on '" + TopSolidHost.Documents.GetName(targetDoc) + "'.";
```

---

## create_revolved_part
Pattern: RW
Description: Creates a saved `.TopPrt` from an ordered polygonal radial/axial profile. Input coordinates use millimeters; the profile is closed automatically and revolved around the absolute Y axis. Example: `Shaft;8,0|20,0|20,50|8,50;360;Shaft;SH-001`

The canonical C# body is available through `topsolid_get_recipe` (`create_revolved_part`) and is maintained in `server/src/Tools/RecipeTool.cs`.

---

## revolve_active_part
Pattern: RW
Description: Adds a revolved solid to the active empty Part (.TopPrt). Refuses to modify a document that already contains a solid.
Input example: `8,0|20,0|20,50|8,50;360` (radius_mm,axial_mm pairs; angle in degrees).

The canonical C# body is available through `topsolid_get_recipe` (`revolve_active_part`) and is maintained in `server/src/Tools/RecipeTool.cs`.

---

## replace_revolved_profile
Pattern: RW
Description: Replaces the named sketch and single revolve in an active single-solid Part using a closed profile made of lines and circular arcs. Refuses additional solid features.
Input example: `VaseProfile;P,0,0|L,50,0|A,60,200,-97.08,107.60,CCW|L,57.8,198.8|A,50.5,5,-97.08,107.60,CW|L,0,5|L,0,0;360`

The canonical C# body is available through `topsolid_get_recipe` (`replace_revolved_profile`) and is maintained in `server/src/Tools/RecipeTool.cs`.

---

## create_extruded_bar
Pattern: RW
Description: One-shot creation of a metalwork Extruded Bar part (.TopPrt) with XY section (X horizontal), +Z extrusion (Length parameter L), 'Extruded Bar' function + Frame (XY/X/Z), optional transverse through-holes, material, and coating. Param: value=doc_name;designation;reference;folder_name;section_type(PLAT|TUBE_RECT|ROND|TUBE_ROND|CORNIERE);dims_csv_mm;length_mm[;holes_spec[;material[;coating_or_rgb]]]

```csharp
string raw = "{value}".Trim();
string[] parts = raw.Split(';');
if (parts.Length < 7) return "Error: Use value=doc_name;designation;reference;folder_name;section_type;dims_csv_mm;length_mm[;holes_spec[;material[;coating_or_rgb]]]";
string docName = parts[0].Trim();
string desig = parts[1].Trim();
string refer = parts[2].Trim();
string folderName = parts[3].Trim();
string explicitProj = "";
if (folderName.Contains("/")) { int sl = folderName.IndexOf('/'); explicitProj = folderName.Substring(0, sl).Trim(); folderName = folderName.Substring(sl + 1).Trim(); }
string profType = parts[4].Trim().ToUpperInvariant();
string[] dims = parts[5].Trim().Split(',');
double lengthM = double.Parse(parts[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
string holesSpec = parts.Length > 7 ? parts[7].Trim() : "";
string matQuery = parts.Length > 8 ? parts[8].Trim() : "";
string cotSpec = parts.Length > 9 ? parts[9].Trim() : "";
PdmObjectId projPdm = PdmObjectId.Empty;
if (!string.IsNullOrEmpty(explicitProj))
{
    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))
        if (string.Equals(TopSolidHost.Pdm.GetName(p), explicitProj, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }
}
if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();
if (projPdm.IsEmpty) return "Error: No current project active.";
PdmObjectId ownerPdm = projPdm;
if (!string.IsNullOrEmpty(folderName))
{
    List<PdmObjectId> fList; List<PdmObjectId> dList;
    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);
    foreach (var f in fList) if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }
    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);
}
List<PdmObjectId> subF; List<PdmObjectId> subD;
TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);
PdmObjectId docPdm = PdmObjectId.Empty;
foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }
if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, ".TopPrt", true);
TopSolidHost.Pdm.SetName(docPdm, docName);
if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);
if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);
DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);
TopSolidHost.Documents.Open(ref dId);
PdmObjectId fncPdm = PdmObjectId.Empty;
PdmObjectId matPdm = PdmObjectId.Empty;
PdmObjectId cotPdm = PdmObjectId.Empty;
string cotNameOnly = cotSpec.Contains(",") ? cotSpec.Substring(0, cotSpec.IndexOf(',')).Trim() : cotSpec;
foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))
{
    string lName = TopSolidHost.Pdm.GetName(lib);
    if (fncPdm.IsEmpty && string.Equals(lName, "TopSolid", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, "Extruded Bar*"))
        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == ".TopPrtFct") { fncPdm = h; break; } }
    }
    if (!string.IsNullOrEmpty(matQuery) && matPdm.IsEmpty)
    {
        bool ok = lName.IndexOf("CEN", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Mat", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Bois", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0;
        if (ok)
        {
            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, "*" + matQuery + "*"))
            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == ".TopMat") { matPdm = h; if (string.Equals(TopSolidHost.Pdm.GetName(h), matQuery, StringComparison.OrdinalIgnoreCase)) break; } }
        }
    }
    if (!string.IsNullOrEmpty(cotNameOnly) && cotPdm.IsEmpty)
    {
        bool ok = lName.IndexOf("RAL", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Coating", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("MetalWork", StringComparison.OrdinalIgnoreCase) >= 0;
        if (ok)
        {
            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, "*" + cotNameOnly + "*"))
            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == ".TopCot") { cotPdm = h; break; } }
        }
    }
}
DocumentId fncDoc = fncPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(fncPdm);
DocumentId matDoc = matPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(matPdm);
DocumentId cotDoc = cotPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(cotPdm);
TopSolidHost.Application.StartModification("CreateExtrudedBar_" + docName, false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref dId);
    var oldOps = TopSolidHost.Operations.GetOperations(dId);
    for (int oi = oldOps.Count - 1; oi >= 0; oi--)
    {
        string tn = TopSolidHost.Elements.GetTypeFullName(oldOps[oi]);
        if (tn.IndexOf("Shape", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Extrud", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Drill", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
            try { TopSolidHost.Elements.Delete(oldOps[oi]); } catch { }
    }
    Point3D orig = new Point3D(0, 0, 0);
    Plane3D plXY = new Plane3D(orig, Direction3D.DX, Direction3D.DY);
    ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(dId, new SmartPlane3D(plXY, -1.0, 1.0, -1.0, 1.0), new SmartPoint3D(orig), true, SmartDirection3D.DX);
    TopSolidHost.Sketches2D.StartModification(sk);
    double wx = 0.05, hy = 0.01;
    if (profType == "PLAT" || profType == "FLAT" || profType == "TUBE_RECT" || profType == "RECT_TUBE")
    {
        wx = double.Parse(dims[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
        hy = double.Parse(dims[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
        double hx = wx / 2.0, hhy = hy / 2.0;
        ElementItemId p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-hx, -hhy));
        ElementItemId p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx, -hhy));
        ElementItemId p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx, hhy));
        ElementItemId p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-hx, hhy));
        TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);
        TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);
        TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);
        TopSolidHost.Sketches2D.CreateLineSegment(p4, p1);
        if ((profType == "TUBE_RECT" || profType == "RECT_TUBE") && dims.Length >= 3)
        {
            double t = double.Parse(dims[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double ix = hx - t, iy = hhy - t;
            ElementItemId q1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-ix, -iy));
            ElementItemId q2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(ix, -iy));
            ElementItemId q3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(ix, iy));
            ElementItemId q4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-ix, iy));
            TopSolidHost.Sketches2D.CreateLineSegment(q1, q2);
            TopSolidHost.Sketches2D.CreateLineSegment(q2, q3);
            TopSolidHost.Sketches2D.CreateLineSegment(q3, q4);
            TopSolidHost.Sketches2D.CreateLineSegment(q4, q1);
        }
    }
    else if (profType == "ROND" || profType == "ROUND_BAR" || profType == "TUBE_ROND" || profType == "ROUND_TUBE")
    {
        double d = double.Parse(dims[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
        wx = d; hy = d;
        ElementItemId c0 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, 0));
        TopSolidHost.Sketches2D.CreateCircleSegment(c0, d / 2.0);
        if ((profType == "TUBE_ROND" || profType == "ROUND_TUBE") && dims.Length >= 2)
        {
            double t = double.Parse(dims[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            TopSolidHost.Sketches2D.CreateCircleSegment(c0, (d / 2.0) - t);
        }
    }
    else if (profType == "CORNIERE" || profType == "L_ANGLE")
    {
        wx = double.Parse(dims[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
        hy = double.Parse(dims[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
        double t = double.Parse(dims[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
        double x0 = -wx / 2.0, y0 = -hy / 2.0;
        ElementItemId p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0));
        ElementItemId p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + wx, y0));
        ElementItemId p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + wx, y0 + t));
        ElementItemId p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + t, y0 + t));
        ElementItemId p5 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + t, y0 + hy));
        ElementItemId p6 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0 + hy));
        TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);
        TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);
        TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);
        TopSolidHost.Sketches2D.CreateLineSegment(p4, p5);
        TopSolidHost.Sketches2D.CreateLineSegment(p5, p6);
        TopSolidHost.Sketches2D.CreateLineSegment(p6, p1);
    }
    TopSolidHost.Sketches2D.CleanSketch();
    TopSolidHost.Sketches2D.EndModification();
    ElementId lParam = ElementId.Empty;
    foreach (var p in TopSolidHost.Parameters.GetParameters(dId))
        if (TopSolidHost.Parameters.GetParameterType(p) == ParameterType.Real && TopSolidHost.Elements.GetName(p) == "L") { lParam = p; break; }
    if (lParam.IsEmpty)
    {
        lParam = TopSolidHost.Parameters.CreateRealParameter(dId, UnitType.Length, lengthM);
        TopSolidHost.Elements.SetName(lParam, "L");
    }
    else { TopSolidHost.Parameters.SetRealValue(lParam, lengthM); }
    ElementId body = TopSolidHost.Shapes.CreateExtrudedShape(dId, new SmartSection3D(sk), SmartDirection3D.DZ, new SmartReal(lParam), new SmartReal(UnitType.Angle, 0), false, false);
    int holeCount = 0;
    if (!string.IsNullOrEmpty(holesSpec) && holesSpec != "-" && holesSpec != "none")
    {
        double span = Math.Max(wx, hy) * 2.0 + 0.02;
        foreach (string hItem in holesSpec.Split('|'))
        {
            string[] hp = hItem.Trim().Split(',');
            if (hp.Length < 3) continue;
            double zPos = double.Parse(hp[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double offX = double.Parse(hp[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double diam = double.Parse(hp[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            // CreateDrillingOperation: frame Z is outward normal (+DY), drilling goes along -Z (-DY) from Y = +hy/2
            Frame3D drF = new Frame3D(new Point3D(offX, hy / 2.0, zPos), Direction3D.DX, Direction3D.DZ, Direction3D.DY);
            var prims = new List<DrillingPrimitive> { new DrillingHolePrimitive(new SmartReal(UnitType.Length, diam), new SmartReal(UnitType.Length, span), DrillingHolePrimitiveBottomType.Flat) };
            TopSolidDesignHost.Parts.CreateDrillingOperation(dId, new SmartShape(body), new SmartFrame3D(drF, -0.02, 0.02, -0.02, 0.02, -0.02, 0.02), prims, false);
            holeCount++;
        }
    }
    var allShapes = TopSolidHost.Shapes.GetShapes(dId);
    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(dId))
    {
        var repCons = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep);
        foreach (ElementId s in allShapes)
            if (!repCons.Contains(s)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, s);
    }
    ElementId profFrame = TopSolidHost.Geometries3D.CreateFrame(dId, new Frame3D(new Point3D(0, 0, 0), Direction3D.DX, Direction3D.DY, Direction3D.DZ));
    try { TopSolidHost.Elements.SetName(profFrame, "Repere_ExtrudedBar_XY_Z"); } catch { }
    if (!fncDoc.IsEmpty)
    {
        ElementId fnc = ElementId.Empty;
        foreach (var f in TopSolidHost.Entities.GetFunctions(dId)) { fnc = f; break; }
        if (fnc.IsEmpty) fnc = TopSolidHost.Entities.ProvideFunction(dId, fncDoc, "");
        foreach (var pub in TopSolidHost.Entities.GetFunctionPublishings(fnc))
        {
            string pName = TopSolidHost.Elements.GetName(pub);
            if (pName == "Section") TopSolidHost.Geometries3D.SetSectionPublishingDefinition(pub, new SmartSection3D(sk));
            else if (pName == "Length") TopSolidHost.Parameters.SetRealPublishingDefinition(pub, new SmartReal(lParam));
            else if (pName == "Frame") TopSolidHost.Geometries3D.SetFramePublishingDefinition(pub, new SmartFrame3D(profFrame, false));
            else if (pName == "BottomLeft") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(-wx / 2.0, -hy / 2.0, 0)));
            else if (pName == "TopRight") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(wx / 2.0, hy / 2.0, 0)));
        }
    }
    try { TopSolidDesignHost.Parts.SetEnclosingBoxManagement(dId, EnclosingBoxManagementType.Minimal, new SmartFrame3D(profFrame, false), new SmartFrame3D(profFrame, false), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0)); } catch { }
    if (!matDoc.IsEmpty) TopSolidDesignHost.Parts.SetMaterial(dId, matDoc);
    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0)); } catch { }
    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(dId, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }
    try { TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(dId, true); } catch { }
    if (!cotDoc.IsEmpty) { try { Color alb = TopSolidDesignHost.Coatings.GetAlbedoColor(cotDoc); foreach (var s in allShapes) TopSolidHost.Elements.SetColor(s, alb); } catch { } }
    if (cotSpec.Contains(",")) {
        string[] cParts = cotSpec.Split(',');
        int startIdx = cParts.Length >= 4 ? 1 : 0;
        if (cParts.Length - startIdx >= 3) {
            byte r = byte.Parse(cParts[startIdx].Trim()); byte g = byte.Parse(cParts[startIdx+1].Trim()); byte b = byte.Parse(cParts[startIdx+2].Trim());
            foreach (var s in allShapes) try { TopSolidHost.Elements.SetColor(s, new Color(r, g, b)); } catch { }
        }
    }
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
TopSolidHost.Pdm.Save(docPdm, true);
return "OK: Extruded Bar part '" + docName + "' (" + profType + ", L=" + (lengthM * 1000.0) + "mm, holes=" + (string.IsNullOrEmpty(holesSpec) ? "0" : holesSpec) + ") created with Extruded Bar function (Frame XY/X/Z) and saved.";
```

---

## create_sheet_metal_plate
Pattern: RW
Description: One-shot creation of a metalwork sheet/baseplate (.TopPrt) in XY extruded along +Z, with optional circular through-holes in the same sketch, named parameter Epaisseur, Minimal EnclosingBox, material, and coating. Param: value=doc_name;designation;reference;folder_name;length_x_mm;width_y_mm;thickness_z_mm[;holes_x_y_diam_mm[;material[;coating_or_rgb[;ORIGIN|CENTER]]]]

```csharp
string raw = "{value}".Trim();
string[] parts = raw.Split(';');
if (parts.Length < 7) return "Error: Use value=doc_name;designation;reference;folder_name;length_x_mm;width_y_mm;thickness_z_mm[;holes_x_y_diam_mm[;material[;coating_or_rgb[;ORIGIN|CENTER]]]]";
string docName = parts[0].Trim();
string desig = parts.Length > 1 ? parts[1].Trim() : "";
string refer = parts.Length > 2 ? parts[2].Trim() : "";
string folderName = parts[3].Trim();
string explicitProj = "";
if (folderName.Contains("/")) { int sl = folderName.IndexOf('/'); explicitProj = folderName.Substring(0, sl).Trim(); folderName = folderName.Substring(sl + 1).Trim(); }
double lx = double.Parse(parts[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double wy = double.Parse(parts[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double tz = double.Parse(parts[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
string holesSpec = parts.Length > 7 ? parts[7].Trim() : "";
string matQuery = parts.Length > 8 ? parts[8].Trim() : "";
string cotSpec = parts.Length > 9 ? parts[9].Trim() : "";
string alignMode = parts.Length > 10 ? parts[10].Trim().ToUpperInvariant() : "CENTER";
PdmObjectId projPdm = PdmObjectId.Empty;
if (!string.IsNullOrEmpty(explicitProj))
{
    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))
        if (string.Equals(TopSolidHost.Pdm.GetName(p), explicitProj, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }
}
if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();
if (projPdm.IsEmpty) return "Error: No current project active.";
PdmObjectId ownerPdm = projPdm;
if (!string.IsNullOrEmpty(folderName))
{
    List<PdmObjectId> fList; List<PdmObjectId> dList;
    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);
    foreach (var f in fList) if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }
    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);
}
List<PdmObjectId> subF; List<PdmObjectId> subD;
TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);
PdmObjectId docPdm = PdmObjectId.Empty;
foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }
if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, ".TopPrt", true);
TopSolidHost.Pdm.SetName(docPdm, docName);
if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);
if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);
DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);
TopSolidHost.Documents.Open(ref dId);
PdmObjectId matPdm = PdmObjectId.Empty;
PdmObjectId cotPdm = PdmObjectId.Empty;
string cotNameOnly = cotSpec.Contains(",") ? cotSpec.Substring(0, cotSpec.IndexOf(',')).Trim() : cotSpec;
foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))
{
    string lName = TopSolidHost.Pdm.GetName(lib);
    if (!string.IsNullOrEmpty(matQuery) && matPdm.IsEmpty)
    {
        bool ok = lName.IndexOf("CEN", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Mat", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Bois", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0;
        if (ok)
        {
            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, "*" + matQuery + "*"))
            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == ".TopMat") { matPdm = h; if (string.Equals(TopSolidHost.Pdm.GetName(h), matQuery, StringComparison.OrdinalIgnoreCase)) break; } }
        }
    }
    if (!string.IsNullOrEmpty(cotNameOnly) && cotPdm.IsEmpty)
    {
        bool ok = lName.IndexOf("RAL", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("Coating", StringComparison.OrdinalIgnoreCase) >= 0 || lName.IndexOf("MetalWork", StringComparison.OrdinalIgnoreCase) >= 0;
        if (ok)
        {
            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, "*" + cotNameOnly + "*"))
            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == ".TopCot") { cotPdm = h; break; } }
        }
    }
}
DocumentId matDoc = matPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(matPdm);
DocumentId cotDoc = cotPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(cotPdm);
TopSolidHost.Application.StartModification("CreateSheetPlate_" + docName, false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref dId);
    var oldOps = TopSolidHost.Operations.GetOperations(dId);
    for (int oi = oldOps.Count - 1; oi >= 0; oi--)
    {
        string tn = TopSolidHost.Elements.GetTypeFullName(oldOps[oi]);
        if (tn.IndexOf("Shape", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Extrud", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Drill", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
            try { TopSolidHost.Elements.Delete(oldOps[oi]); } catch { }
    }
    Point3D orig = new Point3D(0, 0, 0);
    Plane3D plXY = new Plane3D(orig, Direction3D.DX, Direction3D.DY);
    ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(dId, new SmartPlane3D(plXY, -1.0, 1.0, -1.0, 1.0), new SmartPoint3D(orig), true, SmartDirection3D.DX);
    TopSolidHost.Sketches2D.StartModification(sk);
    double x0 = alignMode == "ORIGIN" ? 0.0 : -lx / 2.0;
    double y0 = alignMode == "ORIGIN" ? 0.0 : -wy / 2.0;
    ElementItemId p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0));
    ElementItemId p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + lx, y0));
    ElementItemId p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + lx, y0 + wy));
    ElementItemId p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0 + wy));
    TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);
    TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);
    TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);
    TopSolidHost.Sketches2D.CreateLineSegment(p4, p1);
    if (!string.IsNullOrEmpty(holesSpec) && holesSpec != "-" && holesSpec != "none")
    {
        foreach (string hItem in holesSpec.Split('|'))
        {
            string[] hp = hItem.Trim().Split(',');
            if (hp.Length < 3) continue;
            double hx = double.Parse(hp[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double hy = double.Parse(hp[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double hd = double.Parse(hp[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            ElementItemId hc = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx, hy));
            TopSolidHost.Sketches2D.CreateCircleSegment(hc, hd / 2.0);
        }
    }
    TopSolidHost.Sketches2D.CleanSketch();
    TopSolidHost.Sketches2D.EndModification();
    ElementId pEp = ElementId.Empty;
    foreach (var p in TopSolidHost.Parameters.GetParameters(dId))
        if (TopSolidHost.Parameters.GetParameterType(p) == ParameterType.Real && TopSolidHost.Elements.GetName(p) == "Epaisseur") { pEp = p; break; }
    if (pEp.IsEmpty) { pEp = TopSolidHost.Parameters.CreateRealParameter(dId, UnitType.Length, tz); TopSolidHost.Elements.SetName(pEp, "Epaisseur"); }
    else { TopSolidHost.Parameters.SetRealValue(pEp, tz); }
    ElementId body = TopSolidHost.Shapes.CreateExtrudedShape(dId, new SmartSection3D(sk), SmartDirection3D.DZ, new SmartReal(pEp), new SmartReal(UnitType.Angle, 0), false, false);
    var allShapes = TopSolidHost.Shapes.GetShapes(dId);
    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(dId))
    {
        var repCons = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep);
        foreach (ElementId s in allShapes)
            if (!repCons.Contains(s)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, s);
    }
    if (!matDoc.IsEmpty) TopSolidDesignHost.Parts.SetMaterial(dId, matDoc);
    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0)); } catch { }
    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(dId, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }
    try { TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(dId, true); } catch { }
    if (!cotDoc.IsEmpty) { try { Color alb = TopSolidDesignHost.Coatings.GetAlbedoColor(cotDoc); foreach (var s in allShapes) TopSolidHost.Elements.SetColor(s, alb); } catch { } }
    if (cotSpec.Contains(",")) {
        string[] cParts = cotSpec.Split(',');
        int startIdx = cParts.Length >= 4 ? 1 : 0;
        if (cParts.Length - startIdx >= 3) {
            byte r = byte.Parse(cParts[startIdx].Trim()); byte g = byte.Parse(cParts[startIdx+1].Trim()); byte b = byte.Parse(cParts[startIdx+2].Trim());
            foreach (var s in allShapes) try { TopSolidHost.Elements.SetColor(s, new Color(r, g, b)); } catch { }
        }
    }
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
TopSolidHost.Pdm.Save(docPdm, true);
return "OK: Sheet/Plate part '" + docName + "' (" + (lx*1000) + "x" + (wy*1000) + "x" + (tz*1000) + "mm) created and saved.";
```

---

## create_drilling
Pattern: RW
Description: Drills a cylindrical hole on the active part at (X,Y,Z) (mm) with outward face normal (NX,NY,NZ) and updates all representations. Param: value=diam_mm:depth_mm:X_mm:Y_mm:Z_mm:NX:NY:NZ

```csharp
if (docId.IsEmpty) { __message = "No active document."; return; }
string[] p = "{value}".Split(':');
if (p.Length < 8) { __message = "Error: Use value=diam_mm:depth_mm:X_mm:Y_mm:Z_mm:NX:NY:NZ"; return; }
double diam = double.Parse(p[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double depth = double.Parse(p[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double x = double.Parse(p[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double y = double.Parse(p[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double z = double.Parse(p[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
double nx = double.Parse(p[5].Trim(), System.Globalization.CultureInfo.InvariantCulture);
double ny = double.Parse(p[6].Trim(), System.Globalization.CultureInfo.InvariantCulture);
double nz = double.Parse(p[7].Trim(), System.Globalization.CultureInfo.InvariantCulture);
List<ElementId> shapes = TopSolidHost.Shapes.GetShapes(docId);
if (shapes.Count == 0) { __message = "Error: No solid shape in document."; return; }
ElementId body = shapes[0];
Direction3D norm = new Direction3D(nx, ny, nz);
Direction3D xDir = Math.Abs(nx) < 0.9 ? Direction3D.DX : Direction3D.DY;
Direction3D yDir = Math.Abs(nx) < 0.9 ? Direction3D.DZ : Direction3D.DZ;
Frame3D drF = new Frame3D(new Point3D(x, y, z), xDir, yDir, norm);
var prims = new List<DrillingPrimitive> { new DrillingHolePrimitive(new SmartReal(UnitType.Length, diam), new SmartReal(UnitType.Length, depth), DrillingHolePrimitiveBottomType.Flat) };
TopSolidDesignHost.Parts.CreateDrillingOperation(docId, new SmartShape(body), new SmartFrame3D(drF, -0.02, 0.02, -0.02, 0.02, -0.02, 0.02), prims, false);
__message = "OK: Drilling d=" + (diam*1000) + "mm depth=" + (depth*1000) + "mm applied.";
```

---

## add_inclusion_positioned
Pattern: RW
Description: Includes a part or sub-assembly in an assembly using either (tx,ty,tz[,rx,ry,rz]) or BETWEEN(x1,y1,z1,x2,y2,z2[,ux,uy,uz]) (aligns Extruded Bar +Z from P1 to P2 and section X along uX). Param: value=[target_asm|]child_doc;tx,ty,tz[,rx,ry,rz] OR [target_asm|]child_doc;BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]

```csharp
string raw = "{value}".Trim();
string targetAsmName = "";
if (raw.Contains("|")) { int bar = raw.IndexOf('|'); targetAsmName = raw.Substring(0, bar).Trim(); raw = raw.Substring(bar + 1).Trim(); }
string[] parts = raw.Split(';');
if (parts.Length < 2) return "Error: Use value=[target_asm|]child_doc;tx,ty,tz[,rx,ry,rz] or child_doc;BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]";
string childName = parts[0].Trim();
string[] coords = parts[1].Trim().Split(',');
PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
Func<string, PdmObjectId> findDoc = (string dName) => {
    List<PdmObjectId> rf; List<PdmObjectId> rd;
    TopSolidHost.Pdm.GetConstituents(proj, out rf, out rd);
    for (int fi = rf.Count - 1; fi >= 0; fi--)
    {
        List<PdmObjectId> sf; List<PdmObjectId> sd;
        TopSolidHost.Pdm.GetConstituents(rf[fi], out sf, out sd);
        foreach (var d in sd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;
    }
    foreach (var d in rd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;
    var s = TopSolidHost.Pdm.SearchDocumentByName(proj, dName);
    return s.Count > 0 ? s[0] : PdmObjectId.Empty;
};
DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;
PdmObjectId asmPdm = asmDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(asmDoc);
if (!string.IsNullOrEmpty(targetAsmName))
{
    asmPdm = findDoc(targetAsmName);
    if (asmPdm.IsEmpty) return "Error: Target assembly '" + targetAsmName + "' not found.";
    asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);
}
PdmObjectId childPdm = findDoc(childName);
if (childPdm.IsEmpty) return "Error: Child document '" + childName + "' not found.";
DocumentId childDoc = TopSolidHost.Documents.GetDocument(childPdm);
Transform3D tf = Transform3D.Identity;
if (coords[0].Trim().ToUpperInvariant() == "BETWEEN")
{
    double x1 = double.Parse(coords[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double y1 = double.Parse(coords[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double z1 = double.Parse(coords[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double x2 = double.Parse(coords[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double y2 = double.Parse(coords[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double z2 = double.Parse(coords[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double dzx = x2 - x1, dzy = y2 - y1, dzz = z2 - z1;
    double len = Math.Sqrt(dzx*dzx + dzy*dzy + dzz*dzz);
    dzx /= len; dzy /= len; dzz /= len;
    double rxx = coords.Length >= 10 ? double.Parse(coords[7].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 1.0 : 0.0);
    double rxy = coords.Length >= 10 ? double.Parse(coords[8].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 0.0 : 1.0);
    double rxz = coords.Length >= 10 ? double.Parse(coords[9].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0.0;
    double dot = rxx*dzx + rxy*dzy + rxz*dzz;
    double uxx = rxx - dot*dzx, uxy = rxy - dot*dzy, uxz = rxz - dot*dzz;
    double nX = Math.Sqrt(uxx*uxx + uxy*uxy + uxz*uxz);
    uxx /= nX; uxy /= nX; uxz /= nX;
    double uyx = dzy*uxz - dzz*uxy, uyy = dzz*uxx - dzx*uxz, uyz = dzx*uxy - dzy*uxx;
    tf = new Transform3D(uxx, uyx, dzx, x1, uxy, uyy, dzy, y1, uxz, uyz, dzz, z1, 0, 0, 0, 1);
}
else
{
    double tx = double.Parse(coords[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double ty = double.Parse(coords[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double tz = double.Parse(coords[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
    double rx = coords.Length > 3 ? double.Parse(coords[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;
    double ry = coords.Length > 4 ? double.Parse(coords[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;
    double rz = coords.Length > 5 ? double.Parse(coords[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;
    double cx = Math.Cos(rx), sx = Math.Sin(rx), cy = Math.Cos(ry), sy = Math.Sin(ry), cz = Math.Cos(rz), sz = Math.Sin(rz);
    Direction3D vx = new Direction3D(cy*cz, cy*sz, -sy);
    Direction3D vy = new Direction3D(sx*sy*cz - cx*sz, sx*sy*sz + cx*cz, sx*cy);
    Direction3D vz = new Direction3D(cx*sy*cz + sx*sz, cx*sy*sz - sx*cz, cx*cy);
    tf = new Transform3D(vx.X, vy.X, vz.X, tx, vx.Y, vy.Y, vz.Y, ty, vx.Z, vy.Z, vz.Z, tz, 0, 0, 0, 1);
}
TopSolidHost.Application.StartModification("AddInclusion_" + childName, false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);
    ElementId posId = TopSolidDesignHost.Assemblies.CreatePositioning(asmDoc);
    TopSolidDesignHost.Assemblies.CreateInclusion(asmDoc, posId, childName, childDoc, "", new List<string>(), new List<SmartObject>(), true, ElementId.Empty, ElementId.Empty, false, true, false, false, tf, true);
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
if (!asmPdm.IsEmpty) TopSolidHost.Pdm.Save(asmPdm, true);
return "OK: Included '" + childName + "' in '" + TopSolidHost.Documents.GetName(asmDoc) + "'.";
```

---

## batch_add_inclusions
Pattern: RW
Description: Batch-includes multiple parts or sub-assemblies into an assembly in a single transaction. Optional 'CLEAR|' prefix deletes existing occurrences first. Each item separated by ';' is either 'docName,tx,ty,tz[,rx,ry,rz[,occName]]' or 'docName,BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz[,occName]]'. Param: value=[target_asm|][CLEAR|]item1;item2;...

```csharp
string raw = "{value}".Trim();
string targetAsmName = "";
bool clearExisting = false;
while (raw.Contains("|"))
{
    int bar = raw.IndexOf('|');
    string prefix = raw.Substring(0, bar).Trim();
    raw = raw.Substring(bar + 1).Trim();
    if (string.Equals(prefix, "CLEAR", StringComparison.OrdinalIgnoreCase)) clearExisting = true;
    else targetAsmName = prefix;
}
PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
Func<string, PdmObjectId> findDoc = (string dName) => {
    List<PdmObjectId> rf; List<PdmObjectId> rd;
    TopSolidHost.Pdm.GetConstituents(proj, out rf, out rd);
    for (int fi = rf.Count - 1; fi >= 0; fi--)
    {
        List<PdmObjectId> sf; List<PdmObjectId> sd;
        TopSolidHost.Pdm.GetConstituents(rf[fi], out sf, out sd);
        foreach (var d in sd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;
    }
    foreach (var d in rd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;
    var s = TopSolidHost.Pdm.SearchDocumentByName(proj, dName);
    return s.Count > 0 ? s[0] : PdmObjectId.Empty;
};
DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;
PdmObjectId asmPdm = asmDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(asmDoc);
if (!string.IsNullOrEmpty(targetAsmName))
{
    asmPdm = findDoc(targetAsmName);
    if (asmPdm.IsEmpty) return "Error: Target assembly '" + targetAsmName + "' not found.";
    asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);
}
if (asmDoc.IsEmpty) return "Error: No active assembly.";
TopSolidHost.Documents.Open(ref asmDoc);
string[] items = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
var docCache = new Dictionary<string, DocumentId>(StringComparer.OrdinalIgnoreCase);
foreach (string item in items)
{
    string cName = item.Split(',')[0].Trim();
    if (!docCache.ContainsKey(cName))
    {
        PdmObjectId fc = findDoc(cName);
        if (fc.IsEmpty) return "Error: Child document '" + cName + "' not found in project.";
        docCache[cName] = TopSolidHost.Documents.GetDocument(fc);
    }
}
TopSolidHost.Application.StartModification("BatchAddInclusions_" + TopSolidHost.Documents.GetName(asmDoc), false);
int count = 0;
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);
    ElementId modStage = TopSolidHost.Operations.GetModelingStage(asmDoc);
    if (!modStage.IsEmpty) { try { TopSolidHost.Operations.SetWorkingStage(modStage); TopSolidHost.Operations.ResetInsertionOperation(asmDoc); } catch { } }
    if (clearExisting)
    {
        foreach (var op in TopSolidHost.Operations.GetOperations(asmDoc))
            try { if (TopSolidDesignHost.Assemblies.IsInclusion(op)) TopSolidHost.Elements.Delete(op); } catch { }
    }
    foreach (string item in items)
    {
        string[] c = item.Trim().Split(',');
        if (c.Length < 4) continue;
        string cName = c[0].Trim();
        DocumentId childDoc = docCache[cName];
        Transform3D tf = Transform3D.Identity;
        string occName = cName + "_" + (count + 1);
        if (c[1].Trim().ToUpperInvariant() == "BETWEEN")
        {
            double x1 = double.Parse(c[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double y1 = double.Parse(c[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double z1 = double.Parse(c[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double x2 = double.Parse(c[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double y2 = double.Parse(c[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double z2 = double.Parse(c[7].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double dzx = x2 - x1, dzy = y2 - y1, dzz = z2 - z1;
            double len = Math.Sqrt(dzx*dzx + dzy*dzy + dzz*dzz);
            dzx /= len; dzy /= len; dzz /= len;
            double rxx = c.Length >= 11 ? double.Parse(c[8].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 1.0 : 0.0);
            double rxy = c.Length >= 11 ? double.Parse(c[9].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 0.0 : 1.0);
            double rxz = c.Length >= 11 ? double.Parse(c[10].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0.0;
            if (c.Length >= 12) occName = c[11].Trim();
            double dot = rxx*dzx + rxy*dzy + rxz*dzz;
            double uxx = rxx - dot*dzx, uxy = rxy - dot*dzy, uxz = rxz - dot*dzz;
            double nX = Math.Sqrt(uxx*uxx + uxy*uxy + uxz*uxz);
            uxx /= nX; uxy /= nX; uxz /= nX;
            double uyx = dzy*uxz - dzz*uxy, uyy = dzz*uxx - dzx*uxz, uyz = dzx*uxy - dzy*uxx;
            tf = new Transform3D(uxx, uyx, dzx, x1, uxy, uyy, dzy, y1, uxz, uyz, dzz, z1, 0, 0, 0, 1);
        }
        else
        {
            double tx = double.Parse(c[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double ty = double.Parse(c[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double tz = double.Parse(c[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;
            double rx = c.Length > 4 ? double.Parse(c[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;
            double ry = c.Length > 5 ? double.Parse(c[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;
            double rz = c.Length > 6 ? double.Parse(c[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;
            if (c.Length > 7) occName = c[7].Trim();
            double cx = Math.Cos(rx), sx = Math.Sin(rx), cy = Math.Cos(ry), sy = Math.Sin(ry), cz = Math.Cos(rz), sz = Math.Sin(rz);
            Direction3D vx = new Direction3D(cy*cz, cy*sz, -sy);
            Direction3D vy = new Direction3D(sx*sy*cz - cx*sz, sx*sy*sz + cx*cz, sx*cy);
            Direction3D vz = new Direction3D(cx*sy*cz + sx*sz, cx*sy*sz - sx*cz, cx*cy);
            tf = new Transform3D(vx.X, vy.X, vz.X, tx, vx.Y, vy.Y, vz.Y, ty, vx.Z, vy.Z, vz.Z, tz, 0, 0, 0, 1);
        }
        ElementId posId = TopSolidDesignHost.Assemblies.CreatePositioning(asmDoc);
        TopSolidDesignHost.Assemblies.CreateInclusion(asmDoc, posId, occName, childDoc, "", new List<string>(), new List<SmartObject>(), true, ElementId.Empty, ElementId.Empty, false, true, false, false, tf, true);
        count++;
    }
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
if (!asmPdm.IsEmpty) TopSolidHost.Pdm.Save(asmPdm, true);
return "OK: Batch-included " + count + " occurrence(s) into '" + TopSolidHost.Documents.GetName(asmDoc) + "' and saved.";
```

---

## check_collisions
Pattern: RW
Description: Activates TopSolid native B-Rep 3D collision analysis (SetCollisionsManagement + ShapeShapeCollisionEntity) on an assembly, reports any colliding pairs, and restores Modeling stage. Param: value=[asm_doc_name]

```csharp
string targetAsmName = "{value}".Trim();
DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;
PdmObjectId asmPdm = asmDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(asmDoc);
if (!string.IsNullOrEmpty(targetAsmName))
{
    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();
    PdmObjectId foundPdm = PdmObjectId.Empty;
    List<PdmObjectId> rf; List<PdmObjectId> rd;
    TopSolidHost.Pdm.GetConstituents(proj, out rf, out rd);
    for (int fi = rf.Count - 1; fi >= 0 && foundPdm.IsEmpty; fi--)
    {
        List<PdmObjectId> sf; List<PdmObjectId> sd;
        TopSolidHost.Pdm.GetConstituents(rf[fi], out sf, out sd);
        foreach (var d in sd) if (string.Equals(TopSolidHost.Pdm.GetName(d), targetAsmName, StringComparison.OrdinalIgnoreCase)) { foundPdm = d; break; }
    }
    if (foundPdm.IsEmpty)
    {
        var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetAsmName);
        if (found.Count > 0) foundPdm = found[0];
    }
    if (foundPdm.IsEmpty) return "Error: Assembly '" + targetAsmName + "' not found.";
    asmPdm = foundPdm; asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);
}
if (asmDoc.IsEmpty) return "Error: No assembly open or specified.";
TopSolidHost.Documents.Open(ref asmDoc);
TopSolidHost.Application.StartModification("CheckCollisions_" + TopSolidHost.Documents.GetName(asmDoc), false);
try
{
    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);
    ElementId designRep = TopSolidDesignHost.Representations.GetDesignRepresentation(asmDoc);
    TopSolidDesignHost.Assemblies.SetCollisionsManagement(asmDoc, designRep, true, true, true);
    TopSolidDesignHost.Assemblies.SetCollisionsManagementCheckingMechanisms(asmDoc, false);
    TopSolidDesignHost.Assemblies.SetCollisionsManagementInvalidWhenColliding(asmDoc, false);
    ElementId modStage = TopSolidHost.Operations.GetModelingStage(asmDoc);
    if (!modStage.IsEmpty)
    {
        TopSolidHost.Operations.SetWorkingStage(modStage);
        TopSolidHost.Operations.ResetInsertionOperation(asmDoc);
    }
    TopSolidHost.Application.EndModification(true, true);
}
catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return "Error: " + ex.Message; }
int collCount = 0;
var sb = new StringBuilder();
foreach (var el in TopSolidHost.Elements.GetElements(asmDoc))
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
        sb.AppendLine("  COLLISION: " + string.Join(" <-> ", pair));
    }
}
if (!asmPdm.IsEmpty) TopSolidHost.Pdm.Save(asmPdm, true);
return "OK: B-Rep Collision Check on '" + TopSolidHost.Documents.GetName(asmDoc) + "' — Collisions: " + collCount + (collCount > 0 ? "\
" + sb.ToString() : " (0 collision)");
```

---

## update_where_used_and_save
Pattern: RW
Description: Updates (rebuilds) and saves all documents of the current project in bottom-up dependency order (Parts .TopPrt -> Sub-Assemblies .TopAsm -> Root Assembly .TopAsm) so all Where-Used links stay up to date. Param: value=[project_name]

```csharp
if (false) { TopSolidHost.Application.StartModification("", false); TopSolidHost.Application.EndModification(false, false); }
string projName = "{value}".Trim();
PdmObjectId projPdm = TopSolidHost.Pdm.GetCurrentProject();
if (!string.IsNullOrEmpty(projName))
{
    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))
        if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }
}
if (projPdm.IsEmpty) return "Error: No current project.";
var allDocs = new List<PdmObjectId>();
var fStack = new Stack<PdmObjectId>();
fStack.Push(projPdm);
while (fStack.Count > 0)
{
    PdmObjectId cur = fStack.Pop();
    List<PdmObjectId> subF; List<PdmObjectId> subD;
    TopSolidHost.Pdm.GetConstituents(cur, out subF, out subD);
    foreach (var d in subD) allDocs.Add(d);
    foreach (var f in subF) fStack.Push(f);
}
var partsList = new List<PdmObjectId>();
var asmsList = new List<PdmObjectId>();
foreach (var d in allDocs)
{
    string ext; TopSolidHost.Pdm.GetType(d, out ext);
    if (string.Equals(ext, ".TopPrt", StringComparison.OrdinalIgnoreCase)) partsList.Add(d);
    else if (string.Equals(ext, ".TopAsm", StringComparison.OrdinalIgnoreCase)) asmsList.Add(d);
}
int updatedParts = 0, updatedAsms = 0;
foreach (var p in partsList)
{
    try { DocumentId dId = TopSolidHost.Documents.GetDocument(p); TopSolidHost.Documents.Update(dId, false); TopSolidHost.Pdm.Save(p, true); updatedParts++; } catch { }
}
foreach (var a in asmsList)
{
    try { DocumentId dId = TopSolidHost.Documents.GetDocument(a); TopSolidHost.Documents.Update(dId, false); TopSolidHost.Pdm.Save(a, true); updatedAsms++; } catch { }
}
foreach (var a in asmsList)
{
    try { DocumentId dId = TopSolidHost.Documents.GetDocument(a); TopSolidHost.Documents.Update(dId, false); TopSolidHost.Pdm.Save(a, true); } catch { }
}
TopSolidHost.Pdm.Save(projPdm, true);
return "OK: Where-Used bottom-up update & save complete on '" + TopSolidHost.Pdm.GetName(projPdm) + "' (Parts: " + updatedParts + ", Assemblies: " + updatedAsms + ").";
```

---
