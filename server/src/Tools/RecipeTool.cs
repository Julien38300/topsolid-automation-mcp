using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool that executes pre-built recipes by name. The LLM only needs to pick the recipe.
    /// No code generation required — designed for small models (3B).
    /// </summary>
    public class RecipeTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        /// <summary>When true, only read-only recipes may be executed.</summary>
        private readonly bool _readOnly;

        private static readonly Dictionary<string, RecipeEntry> Recipes = new Dictionary<string, RecipeEntry>(StringComparer.OrdinalIgnoreCase)
        {
            // =====================================================================
            // PDM PROPERTIES — Read
            // =====================================================================
            { "read_designation", R("PDM PROPERTIES", "Reads the designation of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Designation: (empty)\" : \"Designation: \" + val;") },
            { "read_name", R("PDM PROPERTIES", "Reads the name of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "return \"Name: \" + TopSolidHost.Pdm.GetName(pdmId);") },
            { "read_reference", R("PDM PROPERTIES", "Reads the reference (part number) of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Reference: (empty)\" : \"Reference: \" + val;") },
            { "read_manufacturer", R("PDM PROPERTIES", "Reads the manufacturer of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Manufacturer: (empty)\" : \"Manufacturer: \" + val;") },
            { "read_pdm_properties", R("PDM PROPERTIES", "Reads all PDM properties (name, designation, reference, manufacturer)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Name: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "sb.AppendLine(\"Designation: \" + (string.IsNullOrEmpty(desc) ? \"(empty)\" : desc));\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "sb.AppendLine(\"Reference: \" + (string.IsNullOrEmpty(pn) ? \"(empty)\" : pn));\n" +
                "string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "sb.AppendLine(\"Manufacturer: \" + (string.IsNullOrEmpty(mfr) ? \"(empty)\" : mfr));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // PDM PROPERTIES — Write
            // =====================================================================
            { "set_designation", RW("PDM PROPERTIES", "Sets the designation. Param: value",
                // docId, pdmId and __message are declared by the modification wrapper.
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "TopSolidHost.Pdm.SetDescription(pdmId, \"{value}\");\n" +
                "__message = \"OK: Designation → {value}\";") },
            { "set_name", RW("PDM PROPERTIES", "Sets the name. Param: value",
                // docId, pdmId and __message are declared by the modification wrapper.
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "TopSolidHost.Pdm.SetName(pdmId, \"{value}\");\n" +
                "__message = \"OK: Name → {value}\";") },
            { "set_reference", RW("PDM PROPERTIES", "Sets the reference. Param: value",
                // docId, pdmId and __message are declared by the modification wrapper.
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "TopSolidHost.Pdm.SetPartNumber(pdmId, \"{value}\");\n" +
                "__message = \"OK: Reference → {value}\";") },
            { "set_manufacturer", RW("PDM PROPERTIES", "Sets the manufacturer. Param: value",
                // docId, pdmId and __message are declared by the modification wrapper.
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "TopSolidHost.Pdm.SetManufacturer(pdmId, \"{value}\");\n" +
                "__message = \"OK: Manufacturer → {value}\";") },

            // =====================================================================
            // PROJECTS & PDM NAVIGATION
            // =====================================================================
            { "read_current_project", R("PROJECTS", "Returns the current project",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "return \"Project: \" + TopSolidHost.Pdm.GetName(projId);") },
            { "read_project_contents", R("PROJECTS", "Lists folders, subfolders and documents of the current project (full tree)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Project: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "int totalDocs = 0; int totalFolders = 0;\n" +
                "Action<PdmObjectId, string> listRecursive = null;\n" +
                "listRecursive = (parentId, indent) => {\n" +
                "    List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "    TopSolidHost.Pdm.GetConstituents(parentId, out folders, out docs);\n" +
                "    foreach (var f in folders) {\n" +
                "        totalFolders++;\n" +
                "        sb.AppendLine(indent + \"[Folder] \" + TopSolidHost.Pdm.GetName(f));\n" +
                "        listRecursive(f, indent + \"  \");\n" +
                "    }\n" +
                "    foreach (var d in docs) {\n" +
                "        totalDocs++;\n" +
                "        sb.AppendLine(indent + TopSolidHost.Pdm.GetName(d));\n" +
                "    }\n" +
                "};\n" +
                "listRecursive(projId, \"  \");\n" +
                "sb.Insert(sb.ToString().IndexOf('\\n') + 1, \"(\" + totalFolders + \" folders, \" + totalDocs + \" documents)\\n\");\n" +
                "return sb.ToString();") },
            { "search_document", R("PROJECTS", "Searches for a document by name (CONTAINS). Param: value",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Search '\" + \"{value}\" + \"': \" + results.Count + \" results\");\n" +
                "foreach (var r in results)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(r);\n" +
                "    sb.AppendLine(\"  \" + name);\n" +
                "}\n" +
                "return sb.ToString();") },
            { "search_folder", R("PROJECTS", "Searches for a folder by name (CONTAINS). Param: value",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "var results = TopSolidHost.Pdm.SearchFolderByName(projId, \"{value}\");\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Folder search '\" + \"{value}\" + \"': \" + results.Count + \" results\");\n" +
                "foreach (var r in results)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Pdm.GetName(r));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // DOCUMENT — State & Operations
            // =====================================================================
            { "document_type", R("DOCUMENT", "Detects the type of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "sb.AppendLine(\"Name: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string typeName = TopSolidHost.Documents.GetTypeFullName(docId);\n" +
                "sb.AppendLine(\"Type: \" + typeName);\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch {}\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch {}\n" +
                "bool isAssembly = false;\n" +
                "try { isAssembly = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch {}\n" +
                "if (isDrafting) sb.AppendLine(\"→ Drafting\");\n" +
                "else if (isBom) sb.AppendLine(\"→ BOM\");\n" +
                "else if (isAssembly) sb.AppendLine(\"→ Assembly\");\n" +
                "else sb.AppendLine(\"→ Part\");\n" +
                "return sb.ToString();") },
            { "save_document", RW("DOCUMENT", "Saves the active document",
                // docId, pdmId and __message are declared by the modification wrapper,
                // which also saves pdmId once the transaction is committed.
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "__message = \"OK: Document saved.\";") },
            { "rebuild_document", RW("DOCUMENT", "Rebuilds the active document",
                "TopSolidHost.Documents.Rebuild(docId);\n" +
                "__message = \"OK: Document rebuilt.\";") },

            // =====================================================================
            // PARAMETERS — Read
            // =====================================================================
            { "read_parameters", R("PARAMETERS", "Lists all parameters of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Parameters: \" + pList.Count);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    var pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string val = \"\";\n" +
                "    if (pType == ParameterType.Real) val = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F6\");\n" +
                "    else if (pType == ParameterType.Integer) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    else if (pType == ParameterType.Boolean) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();\n" +
                "    else if (pType == ParameterType.Text) val = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "    sb.AppendLine(\"  \" + name + \" = \" + val + \" (type=\" + pType + \")\");\n" +
                "}\n" +
                "return sb.ToString();") },
            { "read_real_parameter", R("PARAMETERS", "Reads a real parameter by name. Param: value=name",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        return name + \" = \" + val.ToString(\"F6\") + \" (SI)\";\n" +
                "    }\n" +
                "}\n" +
                "return \"Parameter '{value}' not found.\";") },
            { "read_text_parameter", R("PARAMETERS", "Reads a text parameter by name. Param: value=name",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "        return name + \" = \" + TopSolidHost.Parameters.GetTextValue(p);\n" +
                "}\n" +
                "return \"Parameter '{value}' not found.\";") },

            // =====================================================================
            // PARAMETERS — Write
            // =====================================================================
            { "set_real_parameter", RW("PARAMETERS", "Sets a real parameter. Param: value=name:SIvalue (e.g. Length:0.15)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length != 2) { __message = \"Format: name:SIvalue (e.g. Length:0.15)\"; return; }\n" +
                "string pName = parts[0].Trim();\n" +
                "double newVal;\n" +
                "if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out newVal))\n" +
                "    { __message = \"Invalid value: \" + parts[1]; return; }\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(pName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        TopSolidHost.Parameters.SetRealValue(p, newVal);\n" +
                "        __message = \"OK: \" + name + \" → \" + newVal.ToString(\"F6\");\n" +
                "        return;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"Parameter '\" + pName + \"' not found.\";") },
            { "set_text_parameter", RW("PARAMETERS", "Sets a text parameter. Param: value=name:value",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: name:value\"; return; }\n" +
                "string pName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string newVal = \"{value}\".Substring(idx + 1).Trim();\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(pName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        TopSolidHost.Parameters.SetTextValue(p, newVal);\n" +
                "        __message = \"OK: \" + name + \" → \" + newVal;\n" +
                "        return;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"Parameter '\" + pName + \"' not found.\";") },

            { "creer_parametre_reel", RW("PARAMETERS", "Creates a new real parameter in the active document. " +
                "Param: value=name:unit:SIvalue (e.g. Longueur:Length:0.05 for 50mm) or name::SIvalue for NoUnit. " +
                "Supported units: Length, Mass, Angle, NoUnit.",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length < 2) { __message = \"Format: name:unit:SIvalue (e.g. Longueur:Length:0.05)\"; return; }\n" +
                "string paramName = parts[0].Trim();\n" +
                "string unitStr = parts.Length >= 3 ? parts[1].Trim() : \"\";\n" +
                "string valStr = parts.Length >= 3 ? parts[2].Trim() : parts[1].Trim();\n" +
                "double siVal;\n" +
                "if (!double.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out siVal))\n" +
                "    { __message = \"Invalid SI value: \" + valStr; return; }\n" +
                "UnitType unit;\n" +
                "switch (unitStr.ToLowerInvariant()) {\n" +
                "    case \"length\": unit = UnitType.Length; break;\n" +
                "    case \"mass\":   unit = UnitType.Mass;   break;\n" +
                "    case \"angle\":  unit = UnitType.Angle;  break;\n" +
                "    default:       unit = UnitType.NoUnit; break;\n" +
                "}\n" +
                "if (string.IsNullOrWhiteSpace(paramName)) { __message = \"Parameter name cannot be empty.\"; return; }\n" +
                "ElementId paramId = TopSolidHost.Parameters.CreateRealParameter(docId, unit, siVal);\n" +
                "TopSolidHost.Elements.SetName(paramId, paramName);\n" +
                "__message = \"OK: parameter '\" + paramName + \"' created (\" + unit + \", value=\" + siVal.ToString(\"F6\") + \")\";") },

            { "creer_parametre_formule", RW("PARAMETERS", "Creates a new formula-driven real parameter in the active document. " +
                "Param: value=name:unit:formula (e.g. DiagBolt:Length:Longueur * 1.414). " +
                "The formula uses TopSolid expression syntax (parameter names, operators, SI units). " +
                "Supported units: Length, Mass, Angle, NoUnit.",
                "int sep1 = \"{value}\".IndexOf(':');\n" +
                "if (sep1 < 0) { __message = \"Format: name:unit:formula\"; return; }\n" +
                "int sep2 = \"{value}\".IndexOf(':', sep1 + 1);\n" +
                "if (sep2 < 0) { __message = \"Format: name:unit:formula (unit required)\"; return; }\n" +
                "string paramName = \"{value}\".Substring(0, sep1).Trim();\n" +
                "string unitStr = \"{value}\".Substring(sep1 + 1, sep2 - sep1 - 1).Trim();\n" +
                "string formula = \"{value}\".Substring(sep2 + 1).Trim();\n" +
                "if (string.IsNullOrWhiteSpace(paramName)) { __message = \"Parameter name cannot be empty.\"; return; }\n" +
                "if (string.IsNullOrWhiteSpace(formula)) { __message = \"Formula cannot be empty.\"; return; }\n" +
                "UnitType unit;\n" +
                "switch (unitStr.ToLowerInvariant()) {\n" +
                "    case \"length\": unit = UnitType.Length; break;\n" +
                "    case \"mass\":   unit = UnitType.Mass;   break;\n" +
                "    case \"angle\":  unit = UnitType.Angle;  break;\n" +
                "    default:       unit = UnitType.NoUnit; break;\n" +
                "}\n" +
                "ElementId paramId = TopSolidHost.Parameters.CreateSmartRealParameter(docId, new SmartReal(unit, formula));\n" +
                "TopSolidHost.Elements.SetName(paramId, paramName);\n" +
                "__message = \"OK: formula parameter '\" + paramName + \"' created (\" + unit + \", formula='\" + formula + \"')\";") },

            { "creer_esquisse_rectangle", RW("GEOMETRY", "Creates a 2D rectangular sketch in the active document. Param: value=width:height (mm)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length < 2) { __message = \"ERROR: expected format width:height (mm)\"; return; }\n" +
                "double widthMm, heightMm;\n" +
                "if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out widthMm) ||\n" +
                "    !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heightMm))\n" +
                "    { __message = \"ERROR: invalid numeric values\"; return; }\n" +
                "double w = widthMm * 0.001;\n" +
                "double h = heightMm * 0.001;\n" +
                "// docId is refreshed by the wrapper (EnsureIsDirty)\n" +
                "Plane3D xyPlane = new Plane3D(new Point3D(0, 0, 0), Direction3D.DX, Direction3D.DY);\n" +
                "ElementId sketchId = TopSolidHost.Sketches2D.CreateSketchIn3D(docId, new SmartPlane3D(xyPlane, -0.5, Math.Max(0.5, w + 0.2), -0.5, Math.Max(0.5, h + 0.2)), new SmartPoint3D(new Point3D(0, 0, 0)), true, SmartDirection3D.DX);\n" +
                "TopSolidHost.Elements.SetName(sketchId, \"Esquisse_Rectangle\");\n" +
                "TopSolidHost.Sketches2D.StartModification(sketchId);\n" +
                "ElementItemId pt1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, 0));\n" +
                "ElementItemId pt2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(w, 0));\n" +
                "ElementItemId pt3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(w, h));\n" +
                "ElementItemId pt4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, h));\n" +
                "TopSolidHost.Sketches2D.CreateLineSegment(pt1, pt2);\n" +
                "TopSolidHost.Sketches2D.CreateLineSegment(pt2, pt3);\n" +
                "TopSolidHost.Sketches2D.CreateLineSegment(pt3, pt4);\n" +
                "TopSolidHost.Sketches2D.CreateLineSegment(pt4, pt1);\n" +
                "TopSolidHost.Sketches2D.CleanSketch();\n" +
                "TopSolidHost.Sketches2D.EndModification();\n" +
                "__message = \"OK: sketch Esquisse_Rectangle created (\" + widthMm + \"x\" + heightMm + \" mm)\";") },

            { "extruder_esquisse", RW("GEOMETRY", "Extrudes the last 2D sketch of the active document into a 3D solid. Param: value=height_mm",
                "double heightMm;\n" +
                "if (!double.TryParse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heightMm))\n" +
                "    { __message = \"ERROR: invalid numeric height\"; return; }\n" +
                "double heightM = heightMm * 0.001;\n" +
                "var sketches = TopSolidHost.Sketches2D.GetSketches(docId);\n" +
                "if (sketches == null || sketches.Count == 0)\n" +
                "    { __message = \"ERROR: no sketch in this document\"; return; }\n" +
                "ElementId lastSketch = sketches[sketches.Count - 1];\n" +
                "ElementId shId = TopSolidHost.Shapes.CreateExtrudedShape(docId, new SmartSection3D(lastSketch), SmartDirection3D.DZ, new SmartReal(UnitType.Length, heightM), new SmartReal(UnitType.Angle, 0), false, false);\n" +
                "foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(docId))\n" +
                "{\n" +
                "    if (!TopSolidDesignHost.Representations.GetRepresentationConstituents(rep).Contains(shId))\n" +
                "        TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, shId);\n" +
                "}\n" +
                "__message = \"OK: extrusion of \" + heightMm + \"mm created (added to all representations)\";") },

            { "ajouter_inclusion", RW("ASSEMBLIES", "Adds an inclusion of a part document into the active assembly document. Param: value=part_document_name",
                "if (!TopSolidDesignHost.Assemblies.IsAssembly(docId))\n" +
                "    { __message = \"ERROR: the active document is not an assembly\"; return; }\n" +
                "DocumentId partDoc = DocumentId.Empty;\n" +
                "string targetName = \"{value}\".Trim();\n" +
                "var allDocs = TopSolidHost.Documents.GetOpenDocuments();\n" +
                "foreach (DocumentId d in allDocs) {\n" +
                "    string docName = \"\";\n" +
                "    try { docName = TopSolidHost.Documents.GetName(d); } catch { continue; }\n" +
                "    if (docName == targetName || docName.StartsWith(targetName + \".\")) {\n" +
                "        partDoc = d;\n" +
                "        break;\n" +
                "    }\n" +
                "}\n" +
                "if (partDoc.IsEmpty)\n" +
                "    { __message = \"ERROR: document '\" + targetName + \"' not found (open the document first)\"; return; }\n" +
                "ElementId positioningId = TopSolidDesignHost.Assemblies.CreatePositioning(docId);\n" +
                "TopSolidDesignHost.Assemblies.CreateInclusion(docId, positioningId, null, partDoc, null, null, null, true, ElementId.Empty, ElementId.Empty, false, false, false, false, Transform3D.Identity, false);\n" +
                "__message = \"OK: document '\" + targetName + \"' included in the assembly\";") },

            // =====================================================================
            // GEOMETRY — Read
            // =====================================================================
            { "read_3d_points", R("GEOMETRY", "Lists 3D points of the document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var points = TopSolidHost.Geometries3D.GetPoints(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"3D Points: \" + points.Count);\n" +
                "foreach (var pt in points)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(pt);\n" +
                "    Point3D geom = TopSolidHost.Geometries3D.GetPointGeometry(pt);\n" +
                "    sb.AppendLine(\"  \" + name + \" (\" + (geom.X*1000).ToString(\"F1\") + \", \" + (geom.Y*1000).ToString(\"F1\") + \", \" + (geom.Z*1000).ToString(\"F1\") + \") mm\");\n" +
                "}\n" +
                "return sb.ToString();") },
            { "read_3d_frames", R("GEOMETRY", "Lists 3D frames of the document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var frames = TopSolidHost.Geometries3D.GetFrames(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"3D Frames: \" + frames.Count);\n" +
                "foreach (var f in frames)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(f));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // SKETCHES — Read
            // =====================================================================
            { "list_sketches", R("GEOMETRY", "Lists sketches of the document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var sketches = TopSolidHost.Sketches2D.GetSketches(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Sketches: \" + sketches.Count);\n" +
                "foreach (var s in sketches)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(s));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // SHAPES — Read
            // =====================================================================
            { "read_shapes", R("GEOMETRY", "Lists shapes of the document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Shapes: \" + shapes.Count);\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    int faceCount = TopSolidHost.Shapes.GetFaceCount(s);\n" +
                "    sb.AppendLine(\"  \" + name + \" (\" + faceCount + \" faces)\");\n" +
                "}\n" +
                "return sb.ToString();") },
            { "read_operations", R("GEOMETRY", "Lists operations (feature tree)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Operations: \" + ops.Count);\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(op);\n" +
                "    string typeName = TopSolidHost.Elements.GetTypeFullName(op);\n" +
                "    string shortType = typeName.Substring(typeName.LastIndexOf('.') + 1);\n" +
                "    sb.AppendLine(\"  \" + name + \" [\" + shortType + \"]\");\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // ASSEMBLIES — Read
            // =====================================================================
            { "detect_assembly", R("ASSEMBLIES", "Detects if the document is an assembly and lists parts",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Unable to verify (not a Design document).\"; }\n" +
                "if (!isAsm) return \"This document is NOT an assembly.\";\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Assembly: \" + parts.Count + \" parts\");\n" +
                "foreach (var p in parts)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(p));\n" +
                "return sb.ToString();") },
            { "list_inclusions", R("ASSEMBLIES", "Lists inclusions of an assembly",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int count = 0;\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    bool isInclusion = false;\n" +
                "    try { isInclusion = TopSolidDesignHost.Assemblies.IsInclusion(op); } catch { continue; }\n" +
                "    if (isInclusion)\n" +
                "    {\n" +
                "        string name = TopSolidHost.Elements.GetFriendlyName(op);\n" +
                "        DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(op);\n" +
                "        string defName = \"?\";\n" +
                "        if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }\n" +
                "        sb.AppendLine(\"  \" + name + \" → \" + defName);\n" +
                "        count++;\n" +
                "    }\n" +
                "}\n" +
                "sb.Insert(0, \"Inclusions: \" + count + \"\\n\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // FAMILIES — Read
            // =====================================================================
            { "detect_family", R("FAMILIES", "Detects if the document is a family",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isFamily = TopSolidHost.Families.IsFamily(docId);\n" +
                "if (!isFamily) return \"This document is NOT a family.\";\n" +
                "bool isExplicit = TopSolidHost.Families.IsExplicit(docId);\n" +
                "return \"Family detected (\" + (isExplicit ? \"explicit\" : \"implicit\") + \").\";") },
            { "read_family_codes", R("FAMILIES", "Reads family codes",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "if (!TopSolidHost.Families.IsFamily(docId)) return \"Not a family.\";\n" +
                "var codes = TopSolidHost.Families.GetCodes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Codes: \" + codes.Count);\n" +
                "foreach (var c in codes)\n" +
                "    sb.AppendLine(\"  \" + c);\n" +
                "return sb.ToString();") },

            // =====================================================================
            // DRAFTING — Read
            // =====================================================================
            { "open_drafting", R("DRAFTING", "Finds and opens the drafting associated with the current part/assembly via PDM back-references",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);\n" +
                "PdmMajorRevisionId majorRev = TopSolidHost.Pdm.GetLastMajorRevision(pdmId);\n" +
                "var backRefs = TopSolidHost.Pdm.SearchMajorRevisionBackReferences(projId, majorRev);\n" +
                "foreach (var backRef in backRefs)\n" +
                "{\n" +
                "    PdmMajorRevisionId brMajor = TopSolidHost.Pdm.GetMajorRevision(backRef);\n" +
                "    PdmObjectId brObj = TopSolidHost.Pdm.GetPdmObject(brMajor);\n" +
                "    string brType = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(brObj, out brType);\n" +
                "    if (brType == \".TopDft\")\n" +
                "    {\n" +
                "        string name = TopSolidHost.Pdm.GetName(brObj);\n" +
                "        DocumentId draftDoc = TopSolidHost.Documents.GetDocument(brObj);\n" +
                "        TopSolidHost.Documents.Open(ref draftDoc);\n" +
                "        return \"Drafting opened: \" + name;\n" +
                "    }\n" +
                "}\n" +
                "return \"No drafting found for this document.\";") },

            { "detect_drafting", R("DRAFTING", "Detects if the document is a drafting and provides info",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isDrafting) return \"This document is NOT a drafting.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Drafting detected.\");\n" +
                "int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);\n" +
                "sb.AppendLine(\"Pages: \" + pages);\n" +
                "string format = TopSolidDraftingHost.Draftings.GetDraftingFormatName(docId);\n" +
                "sb.AppendLine(\"Format: \" + format);\n" +
                "return sb.ToString();") },
            { "list_drafting_views", R("DRAFTING", "Lists views of a drafting",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Not a drafting.\"; }\n" +
                "if (!isDrafting) return \"Not a drafting.\";\n" +
                "var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Views: \" + views.Count);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string title = TopSolidDraftingHost.Draftings.GetViewTitle(v);\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(v);\n" +
                "    sb.AppendLine(\"  \" + name + \" - \" + title);\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // BOM — Read
            // =====================================================================
            { "detect_bom", R("BOM", "Detects if the document is a BOM",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isBom) return \"This document is NOT a BOM.\";\n" +
                "int cols = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "return \"BOM detected (\" + cols + \" columns).\";") },
            { "read_bom_columns", R("BOM", "Reads BOM columns",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Not a BOM.\"; }\n" +
                "if (!isBom) return \"Not a BOM.\";\n" +
                "int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Columns: \" + colCount);\n" +
                "for (int i = 0; i < colCount; i++)\n" +
                "{\n" +
                "    string title = TopSolidDesignHost.Boms.GetColumnTitle(docId, i);\n" +
                "    bool visible = TopSolidDesignHost.Boms.IsColumnVisible(docId, i);\n" +
                "    sb.AppendLine(\"  [\" + i + \"] \" + title + (visible ? \"\" : \" (masquee)\"));\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // DRAFTING — Advanced Recipes (M-58)
            // =====================================================================
            { "read_drafting_scale", R("DRAFTING", "Reads global and per-view scale of a drafting",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Not a drafting.\"; }\n" +
                "if (!isDrafting) return \"Not a drafting.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "double globalScale = TopSolidDraftingHost.Draftings.GetScaleFactorParameterValue(docId);\n" +
                "sb.AppendLine(\"Global scale: 1:\" + (1.0/globalScale).ToString(\"F0\"));\n" +
                "var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(v);\n" +
                "    bool isRel; double rel; double abs; double refVal;\n" +
                "    TopSolidDraftingHost.Draftings.GetViewScaleFactor(v, out isRel, out rel, out abs, out refVal);\n" +
                "    sb.AppendLine(\"  \" + name + \": 1:\" + (1.0/abs).ToString(\"F0\") + (isRel ? \" (relative)\" : \"\"));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "read_drafting_format", R("DRAFTING", "Reads the format (size, margins) of a drafting",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Not a drafting.\"; }\n" +
                "if (!isDrafting) return \"Not a drafting.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "string format = TopSolidDraftingHost.Draftings.GetDraftingFormatName(docId);\n" +
                "sb.AppendLine(\"Format: \" + format);\n" +
                "double w; double h;\n" +
                "TopSolidDraftingHost.Draftings.GetDraftingFormatDimensions(docId, out w, out h);\n" +
                "sb.AppendLine(\"Dimensions: \" + (w*1000).ToString(\"F0\") + \" x \" + (h*1000).ToString(\"F0\") + \" mm\");\n" +
                "int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);\n" +
                "sb.AppendLine(\"Pages: \" + pages);\n" +
                "var mode = TopSolidDraftingHost.Draftings.GetProjectionMode(docId);\n" +
                "sb.AppendLine(\"Projection mode: \" + mode);\n" +
                "return sb.ToString();") },

            { "read_main_projection", R("DRAFTING", "Reads the main projection of a drafting",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Not a drafting.\"; }\n" +
                "if (!isDrafting) return \"Not a drafting.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "DocumentId mainDocId; ElementId repId;\n" +
                "TopSolidDraftingHost.Draftings.GetMainProjectionSet(docId, out mainDocId, out repId);\n" +
                "if (!mainDocId.IsEmpty)\n" +
                "{\n" +
                "    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(mainDocId);\n" +
                "    string srcName = TopSolidHost.Pdm.GetName(pdm);\n" +
                "    sb.AppendLine(\"Source part: \" + srcName);\n" +
                "}\n" +
                "var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);\n" +
                "sb.AppendLine(\"Views: \" + views.Count);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string title = TopSolidDraftingHost.Draftings.GetViewTitle(v);\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(v);\n" +
                "    ElementId mainView = TopSolidDraftingHost.Draftings.GetMainView(v);\n" +
                "    bool isMain = (mainView.Equals(v));\n" +
                "    sb.AppendLine(\"  \" + name + (isMain ? \" [MAIN]\" : \" [auxiliary]\") + \" - \" + title);\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // BOM — Advanced Recipes (M-58)
            // =====================================================================
            { "read_bom_contents", R("BOM", "Reads the full BOM contents (rows and cells)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Not a BOM.\"; }\n" +
                "if (!isBom) return \"Not a BOM.\";\n" +
                "int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "var headers = new List<string>();\n" +
                "for (int c = 0; c < colCount; c++) headers.Add(TopSolidDesignHost.Boms.GetColumnTitle(docId, c));\n" +
                "sb.AppendLine(string.Join(\" | \", headers));\n" +
                "sb.AppendLine(new string('-', 60));\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n" +
                "foreach (int rowId in children)\n" +
                "{\n" +
                "    if (!TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) continue;\n" +
                "    List<TopSolid.Kernel.SX.Types.Property> props; List<string> texts;\n" +
                "    TopSolidDesignHost.Boms.GetRowContents(docId, rowId, out props, out texts);\n" +
                "    sb.AppendLine(string.Join(\" | \", texts));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "count_bom_rows", R("BOM", "Counts active BOM rows",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Not a BOM.\"; }\n" +
                "if (!isBom) return \"Not a BOM.\";\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n" +
                "int active = 0; int inactive = 0;\n" +
                "foreach (int rowId in children)\n" +
                "{\n" +
                "    if (TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) active++;\n" +
                "    else inactive++;\n" +
                "}\n" +
                "return \"Active rows: \" + active + \", inactive: \" + inactive + \", total: \" + (active+inactive);") },

            // =====================================================================
            // UNFOLDING — Sheet Metal (M-58)
            // =====================================================================
            { "detect_unfolding", R("UNFOLDING", "Detects if the document is an unfolding (sheet metal)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isUnfolding = false;\n" +
                "try { isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isUnfolding) return \"This document is NOT an unfolding.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Unfolding (flat pattern) detected.\");\n" +
                "DocumentId partDoc; ElementId repId; ElementId shapeId;\n" +
                "TopSolidDesignHost.Unfoldings.GetPartToUnfold(docId, out partDoc, out repId, out shapeId);\n" +
                "if (!partDoc.IsEmpty)\n" +
                "{\n" +
                "    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(partDoc);\n" +
                "    sb.AppendLine(\"Source part: \" + TopSolidHost.Pdm.GetName(pdm));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "read_bend_features", R("UNFOLDING", "Lists bends of an unfolding (angles, radii, lengths)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isUnfolding = false;\n" +
                "try { isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); } catch { return \"Not an unfolding.\"; }\n" +
                "if (!isUnfolding) return \"Not an unfolding.\";\n" +
                "List<TopSolid.Cad.Design.DB.Documents.BendFeature> bends;\n" +
                "TopSolidDesignHost.Unfoldings.GetBendFeatures(docId, out bends);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Bends: \" + bends.Count);\n" +
                "foreach (var b in bends)\n" +
                "    sb.AppendLine(\"  Pli: angle=\" + (b.Angle*180/3.14159).ToString(\"F1\") + \"deg, radius=\" + (b.Radius*1000).ToString(\"F2\") + \"mm, length=\" + (b.Length*1000).ToString(\"F2\") + \"mm\");\n" +
                "return sb.ToString();") },

            { "read_unfolding_dimensions", R("UNFOLDING", "Reads unfolding dimensions from system properties (sheet metal)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== UNFOLDING ===\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.Contains(\"Unfolding\") || name == \"Sheet Metal\" || name == \"Thickness\" ||\n" +
                "        name == \"Bends Number\" || name == \"Unfoldable Shape\")\n" +
                "    {\n" +
                "        var pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "        if (pType == ParameterType.Real)\n" +
                "        {\n" +
                "            double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "            if (name.Contains(\"Area\") || name.Contains(\"Perimeter\") || name.Contains(\"Width\") || name.Contains(\"Length\"))\n" +
                "                sb.AppendLine(\"  \" + name + \": \" + (val * 1000).ToString(\"F1\") + \" mm\");\n" +
                "            else\n" +
                "                sb.AppendLine(\"  \" + name + \": \" + val.ToString(\"F4\"));\n" +
                "        }\n" +
                "        else if (pType == ParameterType.Boolean)\n" +
                "            sb.AppendLine(\"  \" + name + \": \" + TopSolidHost.Parameters.GetBooleanValue(p));\n" +
                "        else if (pType == ParameterType.Integer)\n" +
                "            sb.AppendLine(\"  \" + name + \": \" + TopSolidHost.Parameters.GetIntegerValue(p));\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length <= 18) return \"No unfolding properties found.\";\n" +
                "return sb.ToString();") },

            // =====================================================================
            // DRAFTING / BOM / UNFOLDING — Write Recipes (M-58)
            // =====================================================================
            { "set_drafting_scale", RW("DRAFTING", "Sets the global scale of a drafting. Param: value=denominator (e.g. '10' for 1:10)",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = \"Not a drafting.\"; return; }\n" +
                "if (!isDrafting) { __message = \"Not a drafting.\"; return; }\n" +
                "double denom;\n" +
                "if (!double.TryParse(\"{value}\", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out denom) || denom <= 0)\n" +
                "{ __message = \"ERROR: value must be a positive number (e.g. '10' for 1:10).\"; return; }\n" +
                "double factor = 1.0 / denom;\n" +
                "TopSolidDraftingHost.Draftings.SetScaleFactorParameterValue(docId, factor);\n" +
                "__message = \"OK: drafting scale set to 1:\" + denom;") },

            { "set_drafting_format", RW("DRAFTING", "Sets the drafting format (paper size). Param: value=format_name (e.g. 'A3', 'A4')",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = \"Not a drafting.\"; return; }\n" +
                "if (!isDrafting) { __message = \"Not a drafting.\"; return; }\n" +
                "string fmt = \"{value}\";\n" +
                "if (string.IsNullOrWhiteSpace(fmt)) { __message = \"ERROR: value required (e.g. 'A3').\"; return; }\n" +
                "TopSolidDraftingHost.Draftings.SetDraftingFormatName(docId, fmt);\n" +
                "__message = \"OK: drafting format set to \" + fmt;") },

            { "set_projection_quality", RW("DRAFTING", "Sets the drafting projection quality. Param: value='exact' (precise) or 'fast' (quick)",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = \"Not a drafting.\"; return; }\n" +
                "if (!isDrafting) { __message = \"Not a drafting.\"; return; }\n" +
                "string mode = (\"{value}\" ?? \"\").Trim().ToLowerInvariant();\n" +
                "TopSolid.Cad.Drafting.Automating.ProjectionMode pm;\n" +
                "if (mode == \"exact\" || mode == \"precise\" || mode == \"precis\") pm = TopSolid.Cad.Drafting.Automating.ProjectionMode.Exact;\n" +
                "else if (mode == \"fast\" || mode == \"quick\" || mode == \"rapide\") pm = TopSolid.Cad.Drafting.Automating.ProjectionMode.Fast;\n" +
                "else { __message = \"ERROR: value must be 'exact' or 'fast'.\"; return; }\n" +
                "TopSolidDraftingHost.Draftings.SetProjectionMode(docId, pm);\n" +
                "__message = \"OK: projection quality set to \" + pm;") },

            { "print_drafting", RD("DRAFTING", "Prints the current drafting (all pages, black & white, 300 DPI, printed to scale)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Not a drafting.\"; }\n" +
                "if (!isDrafting) return \"Not a drafting.\";\n" +
                "int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);\n" +
                "try {\n" +
                "    TopSolidDraftingHost.Draftings.Print(docId,\n" +
                "        TopSolid.Cad.Drafting.Automating.PrintMode.PrintToScale,\n" +
                "        TopSolid.Cad.Drafting.Automating.PrintColorMapping.BlackAndWhite,\n" +
                "        300, 1, pages);\n" +
                "    return \"OK: print job sent (\" + pages + \" pages, B&W, 300 DPI, to scale).\";\n" +
                "} catch (Exception ex) {\n" +
                "    return \"ERROR: \" + ex.Message;\n" +
                "}") },

            { "activate_bom_row", RW("BOM", "Activates a BOM row by its index (0-based, among root-children). Param: value=row_index",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { __message = \"Not a BOM.\"; return; }\n" +
                "if (!isBom) { __message = \"Not a BOM.\"; return; }\n" +
                "int idx;\n" +
                "if (!int.TryParse(\"{value}\", out idx) || idx < 0) { __message = \"ERROR: value must be a non-negative integer.\"; return; }\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n" +
                "if (idx >= children.Count) { __message = \"ERROR: row_index out of range (0..\" + (children.Count-1) + \").\"; return; }\n" +
                "int rowId = children[idx];\n" +
                "TopSolidDesignHost.Boms.ActivateRow(docId, rowId);\n" +
                "__message = \"OK: BOM row \" + idx + \" activated.\";") },

            { "deactivate_bom_row", RW("BOM", "Deactivates a BOM row by its index (0-based, among root-children). Param: value=row_index",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { __message = \"Not a BOM.\"; return; }\n" +
                "if (!isBom) { __message = \"Not a BOM.\"; return; }\n" +
                "int idx;\n" +
                "if (!int.TryParse(\"{value}\", out idx) || idx < 0) { __message = \"ERROR: value must be a non-negative integer.\"; return; }\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n" +
                "if (idx >= children.Count) { __message = \"ERROR: row_index out of range (0..\" + (children.Count-1) + \").\"; return; }\n" +
                "int rowId = children[idx];\n" +
                "TopSolidDesignHost.Boms.DeactivateRow(docId, rowId);\n" +
                "__message = \"OK: BOM row \" + idx + \" deactivated.\";") },

            // =====================================================================
            // EXPORT
            // =====================================================================
            { "list_exporters", R("EXPORT", "Lists all available exporters",
                "var sb = new System.Text.StringBuilder();\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "sb.AppendLine(\"Exporters: \" + count);\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName;\n" +
                "    string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    sb.AppendLine(i + \": \" + typeName + \" [\" + string.Join(\", \", extensions) + \"]\");\n" +
                "}\n" +
                "return sb.ToString();") },
            { "export_step", RD("EXPORT", "Exports to STEP. Param: value=path (e.g. C:\\temp\\part.stp)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "int idx = -1;\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName; string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    foreach (string ext in extensions)\n" +
                "        if (ext.ToLower().Contains(\"stp\") || ext.ToLower().Contains(\"step\")) { idx = i; break; }\n" +
                "    if (idx >= 0) break;\n" +
                "}\n" +
                "if (idx < 0) return \"STEP exporter not found.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.stp\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exported to STEP → \" + path;") },
            { "export_dxf", RD("EXPORT", "Exports to DXF. Param: value=path",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "int idx = -1;\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName; string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    foreach (string ext in extensions)\n" +
                "        if (ext.ToLower().Contains(\"dxf\")) { idx = i; break; }\n" +
                "    if (idx >= 0) break;\n" +
                "}\n" +
                "if (idx < 0) return \"DXF exporter not found.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.dxf\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exported to DXF → \" + path;") },
            { "export_pdf", RD("EXPORT", "Exports to PDF. Param: value=path",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "int idx = -1;\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName; string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    foreach (string ext in extensions)\n" +
                "        if (ext.ToLower().Contains(\"pdf\")) { idx = i; break; }\n" +
                "    if (idx >= 0) break;\n" +
                "}\n" +
                "if (idx < 0) return \"PDF exporter not found.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.pdf\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exported to PDF → \" + path;") },

            // =====================================================================
            // USER PROPERTIES
            // =====================================================================
            { "read_user_property", R("USER PROPERTIES", "Reads a text user property. Param: value=property_name",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetTextUserProperty(pdmId, \"{value}\");\n" +
                "return string.IsNullOrEmpty(val) ? \"Propriete '{value}': (empty)\" : \"Propriete '{value}': \" + val;") },

            // =====================================================================
            // PART AUDIT — High-value composite scenarios
            // =====================================================================
            { "audit_part", R("AUDIT", "Full part audit: properties, parameters, shapes, mass, volume",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== PART AUDIT ===\");\n" +
                "sb.AppendLine(\"Name: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "sb.AppendLine(\"Designation: \" + (string.IsNullOrEmpty(desc) ? \"(EMPTY!)\" : desc));\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "sb.AppendLine(\"Reference: \" + (string.IsNullOrEmpty(pn) ? \"(EMPTY!)\" : pn));\n" +
                "sb.AppendLine(\"Type: \" + TopSolidHost.Documents.GetTypeFullName(docId));\n" +
                "// Parametres\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "sb.AppendLine(\"\\nParameters: \" + pList.Count);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    var pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string val = \"\";\n" +
                "    if (pType == ParameterType.Real) val = (TopSolidHost.Parameters.GetRealValue(p) * 1000).ToString(\"F2\") + \" mm\";\n" +
                "    else if (pType == ParameterType.Integer) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    else if (pType == ParameterType.Boolean) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();\n" +
                "    else if (pType == ParameterType.Text) val = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "    sb.AppendLine(\"  \" + name + \" = \" + val);\n" +
                "}\n" +
                "// Shapes\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "sb.AppendLine(\"\\nShapes: \" + shapes.Count);\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string sName = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    double vol = TopSolidHost.Shapes.GetShapeVolume(s);\n" +
                "    int faces = TopSolidHost.Shapes.GetFaceCount(s);\n" +
                "    sb.AppendLine(\"  \" + sName + \" : \" + faces + \" faces, volume=\" + (vol * 1e9).ToString(\"F1\") + \" cm3\");\n" +
                "}\n" +
                "return sb.ToString();") },

            { "audit_assembly", R("AUDIT", "Full assembly audit: parts, inclusions, mass",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isAsm) return \"This document is NOT an assembly.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== ASSEMBLY AUDIT ===\");\n" +
                "sb.AppendLine(\"Name: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "sb.AppendLine(\"Designation: \" + (string.IsNullOrEmpty(desc) ? \"(EMPTY!)\" : desc));\n" +
                "// Pieces\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "sb.AppendLine(\"\\nParts: \" + parts.Count);\n" +
                "// Inclusions\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "int inclCount = 0;\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    bool isInclusion = false;\n" +
                "    try { isInclusion = TopSolidDesignHost.Assemblies.IsInclusion(op); } catch { continue; }\n" +
                "    if (isInclusion)\n" +
                "    {\n" +
                "        string opName = TopSolidHost.Elements.GetFriendlyName(op);\n" +
                "        DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(op);\n" +
                "        string defName = \"?\";\n" +
                "        if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }\n" +
                "        sb.AppendLine(\"  \" + opName + \" -> \" + defName);\n" +
                "        inclCount++;\n" +
                "    }\n" +
                "}\n" +
                "int idxParts = sb.ToString().IndexOf(\"\\nParts:\");\n" +
                "if (idxParts >= 0) sb.Insert(idxParts, \"Inclusions: \" + inclCount + \"\\n\"); else sb.AppendLine(\"Inclusions: \" + inclCount);\n" +
                "return sb.ToString();") },

            { "check_part", R("AUDIT", "Quality check: designation, reference, material filled?",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== QUALITY CHECK ===\");\n" +
                "int warnings = 0;\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "if (string.IsNullOrEmpty(desc)) { sb.AppendLine(\"WARNING: Designation EMPTY\"); warnings++; } else sb.AppendLine(\"OK: Designation = \" + desc);\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "if (string.IsNullOrEmpty(pn)) { sb.AppendLine(\"WARNING: Reference EMPTY\"); warnings++; } else sb.AppendLine(\"OK: Reference = \" + pn);\n" +
                "string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "if (string.IsNullOrEmpty(mfr)) sb.AppendLine(\"INFO: Manufacturer not set\");\n" +
                "// Parametres\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "sb.AppendLine(\"Parameters: \" + pList.Count);\n" +
                "if (pList.Count == 0) { sb.AppendLine(\"WARNING: No parameters\"); warnings++; }\n" +
                "// Shapes\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "if (shapes.Count == 0) { sb.AppendLine(\"WARNING: No shape (empty part?)\"); warnings++; } else sb.AppendLine(\"OK: \" + shapes.Count + \" shape(s)\");\n" +
                "sb.AppendLine(\"\\n\" + (warnings == 0 ? \"RESULT: Part OK\" : \"RESULT: \" + warnings + \" warning(s)\"));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // PERFORMANCE — Mass, volume, surface
            // =====================================================================
            { "read_mass_volume", R("MEASUREMENTS", "Reads mass, volume, surface from document system properties",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\" || name == \"Volume\" || name == \"Surface Area\")\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        if (name == \"Mass\") sb.AppendLine(\"Mass: \" + val.ToString(\"F3\") + \" kg\");\n" +
                "        else if (name == \"Volume\") sb.AppendLine(\"Volume: \" + (val * 1e9).ToString(\"F2\") + \" mm3\");\n" +
                "        else sb.AppendLine(\"Surface: \" + (val * 1e6).ToString(\"F2\") + \" mm2\");\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length == 0) return \"No physical properties found.\";\n" +
                "return sb.ToString();") },

            { "read_material_density", R("MATERIALS", "Calculates density from document mass/volume",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "double mass = 0; double vol = 0;\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\") mass = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "    else if (name == \"Volume\") vol = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "}\n" +
                "if (vol > 0 && mass > 0) return \"Density: \" + (mass / vol).ToString(\"F0\") + \" kg/m3 (mass=\" + mass.ToString(\"F3\") + \"kg, vol=\" + (vol*1e6).ToString(\"F2\") + \"cm3)\";\n" +
                "return \"Mass or volume not available.\";") },

            // =====================================================================
            // INVOKE COMMAND — TopSolid menu commands
            // =====================================================================
            { "invoke_command", RW("DOCUMENT", "Executes a TopSolid menu command by name. Param: value=command_name",
                // __message is declared by the modification wrapper.
                "bool result = TopSolidHost.Application.InvokeCommand(\"{value}\");\n" +
                "__message = result ? \"OK: Command '{value}' executed.\" : \"ERROR: Command '{value}' not found or failed.\";") },

            // =====================================================================
            // OCCURRENCES — Occurrence properties in assemblies
            // =====================================================================
            { "read_occurrences", R("ASSEMBLIES", "Lists occurrences of an assembly with their definition",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Not an assembly.\"; }\n" +
                "if (!isAsm) return \"Not an assembly.\";\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Occurrences: \" + parts.Count);\n" +
                "foreach (var p in parts)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    bool isOcc = TopSolidHost.Entities.IsOccurrence(p);\n" +
                "    string occName = \"\";\n" +
                "    try { occName = TopSolidHost.Entities.GetFunctionOccurrenceName(p); } catch {}\n" +
                "    DocumentId defDoc = DocumentId.Empty;\n" +
                "    try { defDoc = TopSolidDesignHost.Assemblies.GetOccurrenceDefinition(p); } catch {}\n" +
                "    string defName = \"\";\n" +
                "    if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }\n" +
                "    sb.AppendLine(\"  \" + name + (isOcc ? \" [occ]\" : \"\") + (!string.IsNullOrEmpty(occName) ? \" name=\" + occName : \"\") + (!string.IsNullOrEmpty(defName) ? \" -> \" + defName : \"\"));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "rename_occurrence", RW("ASSEMBLIES", "Renames an occurrence. Param: value=old_name:new_name",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: old_name:new_name\"; return; }\n" +
                "string oldName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string newName = \"{value}\".Substring(idx + 1).Trim();\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "foreach (var p in parts)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        TopSolidHost.Entities.SetFunctionOccurrenceName(p, newName);\n" +
                "        __message = \"OK: Occurrence '\" + name + \"' renamed to '\" + newName + \"'\";\n" +
                "        return;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"Occurrence '\" + oldName + \"' not found.\";") },

            // =====================================================================
            // USER PROPERTIES — Write
            // =====================================================================
            { "set_user_property", RW("USER PROPERTIES", "Sets a text user property. Param: value=property_name:value",
                // docId, pdmId and __message are declared by the modification wrapper.
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: property_name:value\"; return; }\n" +
                "string propName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string propVal = \"{value}\".Substring(idx + 1).Trim();\n" +
                "TopSolidHost.Pdm.SetTextUserProperty(pdmId, propName, propVal);\n" +
                "__message = \"OK: Property '\" + propName + \"' = '\" + propVal + \"'\";") },

            // =====================================================================
            // BOUNDING BOX / STOCK
            // =====================================================================
            { "read_bounding_box", R("MEASUREMENTS", "Reads bounding box dimensions from system properties",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Bounding box:\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Box X Size\" || name == \"Box Y Size\" || name == \"Box Z Size\")\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        sb.AppendLine(\"  \" + name + \": \" + (val * 1000).ToString(\"F1\") + \" mm\");\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length <= 22)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        ElementId xSize, ySize, zSize;\n" +
                "        TopSolidDesignHost.Parts.GetEnclosingBoxParameters(docId, out xSize, out ySize, out zSize);\n" +
                "        if (!xSize.IsEmpty) sb.AppendLine(\"  X: \" + (TopSolidHost.Parameters.GetRealValue(xSize) * 1000).ToString(\"F1\") + \" mm\");\n" +
                "        if (!ySize.IsEmpty) sb.AppendLine(\"  Y: \" + (TopSolidHost.Parameters.GetRealValue(ySize) * 1000).ToString(\"F1\") + \" mm\");\n" +
                "        if (!zSize.IsEmpty) sb.AppendLine(\"  Z: \" + (TopSolidHost.Parameters.GetRealValue(zSize) * 1000).ToString(\"F1\") + \" mm\");\n" +
                "    }\n" +
                "    catch {}\n" +
                "}\n" +
                "if (sb.Length <= 22) return \"No bounding box dimensions found.\";\n" +
                "return sb.ToString();") },

            { "read_part_dimensions", R("MEASUREMENTS", "Reads dimensions (Height, Width, Length, Box Size) from system properties",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== DIMENSIONS ===\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Height\" || name == \"Width\" || name == \"Length\" ||\n" +
                "        name == \"Box X Size\" || name == \"Box Y Size\" || name == \"Box Z Size\" ||\n" +
                "        name == \"Box X marged size\" || name == \"Box Y marged size\" || name == \"Box Z marged size\" ||\n" +
                "        name == \"Thickness\")\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        if (val > 0) sb.AppendLine(\"  \" + name + \": \" + (val * 1000).ToString(\"F1\") + \" mm\");\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length <= 22) return \"No dimensions found (not a part?)\";\n" +
                "return sb.ToString();") },

            { "read_inertia_moments", R("MEASUREMENTS", "Reads principal inertia moments from system properties",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== INERTIA MOMENTS ===\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Principal X Moment\" || name == \"Principal Y Moment\" || name == \"Principal Z Moment\")\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        sb.AppendLine(\"  \" + name + \": \" + val.ToString(\"F6\") + \" kg.mm2\");\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length <= 28) return \"No inertia moments found.\";\n" +
                "return sb.ToString();") },

            // =====================================================================
            // BATCH — Project operations
            // =====================================================================
            { "list_project_documents", R("PROJECTS", "Lists ALL project documents with designation and reference",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Project: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "int docCount = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(item);\n" +
                "    string desc = TopSolidHost.Pdm.GetDescription(item);\n" +
                "    string pn = TopSolidHost.Pdm.GetPartNumber(item);\n" +
                "    sb.AppendLine(\"  \" + name + \" | Designation: \" + (string.IsNullOrEmpty(desc) ? \"-\" : desc) + \" | Ref: \" + (string.IsNullOrEmpty(pn) ? \"-\" : pn));\n" +
                "    docCount++;\n" +
                "}\n" +
                "sb.Insert(0, \"Documents: \" + docCount + \"\\n\");\n" +
                "return sb.ToString();") },

            { "check_project", R("AUDIT", "Full project quality check: parts without designation/reference",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== PROJECT CHECK ===\");\n" +
                "sb.AppendLine(\"Project: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "int total = 0; int alertes = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(item);\n" +
                "    string desc = TopSolidHost.Pdm.GetDescription(item);\n" +
                "    string pn = TopSolidHost.Pdm.GetPartNumber(item);\n" +
                "    total++;\n" +
                "    if (string.IsNullOrEmpty(desc) || string.IsNullOrEmpty(pn))\n" +
                "    {\n" +
                "        sb.AppendLine(\"  ALERTE: \" + name + (string.IsNullOrEmpty(desc) ? \" [designation empty]\" : \"\") + (string.IsNullOrEmpty(pn) ? \" [reference empty]\" : \"\"));\n" +
                "        alertes++;\n" +
                "    }\n" +
                "}\n" +
                "sb.AppendLine(\"\\nTotal: \" + total + \" documents, \" + alertes + \" warning(s)\");\n" +
                "sb.AppendLine(alertes == 0 ? \"RESULTAT: Projet OK\" : \"RESULT: \" + alertes + \" document(s) to complete\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // MATERIALS
            // =====================================================================
            { "compare_parameters", R("AUDIT", "Compares parameters of the active document with another. Param: value=other_document_name",
                "DocumentId curDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDoc.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' not found.\";\n" +
                "DocumentId otherDocId = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "var paramsA = TopSolidHost.Parameters.GetParameters(curDoc);\n" +
                "var paramsB = TopSolidHost.Parameters.GetParameters(otherDocId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(curDoc));\n" +
                "string nameB = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "sb.AppendLine(\"=== COMPARISON ===\");\n" +
                "sb.AppendLine(nameA + \" vs \" + nameB);\n" +
                "// Index params B by name\n" +
                "var dictB = new System.Collections.Generic.Dictionary<string, string>();\n" +
                "foreach (var p in paramsB)\n" +
                "{\n" +
                "    string n = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    var t = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string v = \"\";\n" +
                "    if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F6\");\n" +
                "    else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "    else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    dictB[n] = v;\n" +
                "}\n" +
                "int diffs = 0;\n" +
                "foreach (var p in paramsA)\n" +
                "{\n" +
                "    string n = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    var t = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string vA = \"\";\n" +
                "    if (t == ParameterType.Real) vA = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F6\");\n" +
                "    else if (t == ParameterType.Text) vA = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "    else if (t == ParameterType.Integer) vA = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    string vB;\n" +
                "    if (dictB.TryGetValue(n, out vB))\n" +
                "    {\n" +
                "        if (vA != vB) { sb.AppendLine(\"  DIFF: \" + n + \" : \" + vA + \" vs \" + vB); diffs++; }\n" +
                "        dictB.Remove(n);\n" +
                "    }\n" +
                "    else { sb.AppendLine(\"  ONLY A: \" + n + \" = \" + vA); diffs++; }\n" +
                "}\n" +
                "foreach (var kvp in dictB)\n" +
                "    { sb.AppendLine(\"  ONLY B: \" + kvp.Key + \" = \" + kvp.Value); diffs++; }\n" +
                "sb.AppendLine(\"\\n\" + diffs + \" difference(s)\");\n" +
                "return sb.ToString();") },

            { "compare_document_operations", R("AUDIT", "Compares operations (feature tree) between the current and another document. Param: value=other_doc_name",
                "DocumentId docIdA = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docIdA.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' not found.\";\n" +
                "DocumentId docIdB = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "var opsA = TopSolidHost.Operations.GetOperations(docIdA);\n" +
                "var opsB = TopSolidHost.Operations.GetOperations(docIdB);\n" +
                "string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docIdA));\n" +
                "string nameB = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== OPERATIONS COMPARISON ===\");\n" +
                "sb.AppendLine(nameA + \" (\" + opsA.Count + \" ops) vs \" + nameB + \" (\" + opsB.Count + \" ops)\");\n" +
                "int max = System.Math.Max(opsA.Count, opsB.Count);\n" +
                "for (int i = 0; i < max; i++)\n" +
                "{\n" +
                "    string nA = i < opsA.Count ? TopSolidHost.Elements.GetFriendlyName(opsA[i]) : \"--\";\n" +
                "    string nB = i < opsB.Count ? TopSolidHost.Elements.GetFriendlyName(opsB[i]) : \"--\";\n" +
                "    string marker = (nA == nB) ? \"  =\" : \"  *\";\n" +
                "    sb.AppendLine(marker + \" [\" + i + \"] \" + nA + \" | \" + nB);\n" +
                "}\n" +
                "return sb.ToString();") },

            { "compare_document_entities", R("AUDIT", "Compares entities (shapes, sketches, points, frames) between the current and another document. Param: value=other_doc_name",
                "DocumentId docIdA = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docIdA.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' not found.\";\n" +
                "DocumentId docIdB = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docIdA));\n" +
                "string nameB = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== ENTITIES COMPARISON ===\");\n" +
                "sb.AppendLine(nameA + \" vs \" + nameB);\n" +
                "// Shapes\n" +
                "int shA = TopSolidHost.Shapes.GetShapes(docIdA).Count;\n" +
                "int shB = TopSolidHost.Shapes.GetShapes(docIdB).Count;\n" +
                "sb.AppendLine((shA==shB?\"  =\":\"  *\") + \" Shapes: \" + shA + \" vs \" + shB);\n" +
                "// Sketches\n" +
                "int skA = TopSolidHost.Sketches2D.GetSketches(docIdA).Count;\n" +
                "int skB = TopSolidHost.Sketches2D.GetSketches(docIdB).Count;\n" +
                "sb.AppendLine((skA==skB?\"  =\":\"  *\") + \" Sketches: \" + skA + \" vs \" + skB);\n" +
                "// Points 3D\n" +
                "int ptA = TopSolidHost.Geometries3D.GetPoints(docIdA).Count;\n" +
                "int ptB = TopSolidHost.Geometries3D.GetPoints(docIdB).Count;\n" +
                "sb.AppendLine((ptA==ptB?\"  =\":\"  *\") + \" Points 3D: \" + ptA + \" vs \" + ptB);\n" +
                "// Reperes\n" +
                "int frA = TopSolidHost.Geometries3D.GetFrames(docIdA).Count;\n" +
                "int frB = TopSolidHost.Geometries3D.GetFrames(docIdB).Count;\n" +
                "sb.AppendLine((frA==frB?\"  =\":\"  *\") + \" Reperes: \" + frA + \" vs \" + frB);\n" +
                "// Operations\n" +
                "int opA = TopSolidHost.Operations.GetOperations(docIdA).Count;\n" +
                "int opB = TopSolidHost.Operations.GetOperations(docIdB).Count;\n" +
                "sb.AppendLine((opA==opB?\"  =\":\"  *\") + \" Operations: \" + opA + \" vs \" + opB);\n" +
                "// Parametres\n" +
                "int paA = TopSolidHost.Parameters.GetParameters(docIdA).Count;\n" +
                "int paB = TopSolidHost.Parameters.GetParameters(docIdB).Count;\n" +
                "sb.AppendLine((paA==paB?\"  =\":\"  *\") + \" Parameters: \" + paA + \" vs \" + paB);\n" +
                "return sb.ToString();") },

            { "copy_parameters_to", RW("BATCH", "Copies parameter values from the current document to another. Param: value=target_doc_name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (srcDocId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) { __message = \"Document '{value}' not found.\"; return; }\n" +
                "// Read the source parameters\n" +
                "var srcParams = TopSolidHost.Parameters.GetParameters(srcDocId);\n" +
                "var srcDict = new System.Collections.Generic.Dictionary<string, object[]>();\n" +
                "foreach (var p in srcParams)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.Contains(\"$\") || name == \"Mass\" || name == \"Volume\" || name == \"Surface Area\") continue;\n" +
                "    var t = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    if (t == ParameterType.Real)\n" +
                "        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetRealValue(p) };\n" +
                "    else if (t == ParameterType.Text)\n" +
                "        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetTextValue(p) };\n" +
                "    else if (t == ParameterType.Integer)\n" +
                "        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetIntegerValue(p) };\n" +
                "    else if (t == ParameterType.Boolean)\n" +
                "        srcDict[name] = new object[] { t, TopSolidHost.Parameters.GetBooleanValue(p) };\n" +
                "}\n" +
                "// Apply to the target document\n" +
                "DocumentId tgtDocId = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "TopSolidHost.Documents.EnsureIsDirty(ref tgtDocId);\n" +
                "var tgtParams = TopSolidHost.Parameters.GetParameters(tgtDocId);\n" +
                "int applied = 0; int skipped = 0;\n" +
                "foreach (var p in tgtParams)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    object[] src;\n" +
                "    if (!srcDict.TryGetValue(name, out src)) continue;\n" +
                "    var t = (ParameterType)src[0];\n" +
                "    try\n" +
                "    {\n" +
                "        if (t == ParameterType.Real) TopSolidHost.Parameters.SetRealValue(p, (double)src[1]);\n" +
                "        else if (t == ParameterType.Text) TopSolidHost.Parameters.SetTextValue(p, (string)src[1]);\n" +
                "        else if (t == ParameterType.Integer) TopSolidHost.Parameters.SetIntegerValue(p, (int)src[1]);\n" +
                "        else if (t == ParameterType.Boolean) TopSolidHost.Parameters.SetBooleanValue(p, (bool)src[1]);\n" +
                "        applied++;\n" +
                "    }\n" +
                "    catch { skipped++; }\n" +
                "}\n" +
                "string tgtName = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "__message = \"OK: \" + applied + \" parameters copied to '\" + tgtName + \"' (\" + skipped + \" skipped).\";") },

            { "copy_pdm_properties_to", RW("BATCH", "Copies designation/reference/manufacturer from current to another document. Param: value=target_doc_name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (srcDocId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "PdmObjectId srcPdmId = TopSolidHost.Documents.GetPdmObject(srcDocId);\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) { __message = \"Document '{value}' not found.\"; return; }\n" +
                "PdmObjectId tgtPdmId = results[0];\n" +
                "// Read source\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(srcPdmId);\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(srcPdmId);\n" +
                "string mfr = TopSolidHost.Pdm.GetManufacturer(srcPdmId);\n" +
                "string mfrPn = TopSolidHost.Pdm.GetManufacturerPartNumber(srcPdmId);\n" +
                "// Write target\n" +
                "TopSolidHost.Pdm.SetDescription(tgtPdmId, desc);\n" +
                "TopSolidHost.Pdm.SetPartNumber(tgtPdmId, pn);\n" +
                "TopSolidHost.Pdm.SetManufacturer(tgtPdmId, mfr);\n" +
                "TopSolidHost.Pdm.SetManufacturerPartNumber(tgtPdmId, mfrPn);\n" +
                "string tgtName = TopSolidHost.Pdm.GetName(tgtPdmId);\n" +
                "__message = \"OK: PDM properties copied to '\" + tgtName + \"' (designation=\" + desc + \", ref=\" + pn + \", manufacturer=\" + mfr + \").\";") },

            { "batch_export_step", RD("EXPORT", "Exports ALL project parts to STEP in a folder. Param: value=folder_path",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "string outputDir = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(outputDir)) outputDir = @\"C:\\temp\\export_step\";\n" +
                "System.IO.Directory.CreateDirectory(outputDir);\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "int exported = 0; int skipped = 0;\n" +
                "int stepIdx = -1;\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName; string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    foreach (string ext in extensions)\n" +
                "        if (ext.ToLower().Contains(\"stp\") || ext.ToLower().Contains(\"step\")) { stepIdx = i; break; }\n" +
                "    if (stepIdx >= 0) break;\n" +
                "}\n" +
                "if (stepIdx < 0) return \"STEP exporter not found.\";\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string t = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(d, out t);\n" +
                "    if (t != \".TopPrt\" && t != \".TopAsm\") { skipped++; continue; }\n" +
                "    string name = TopSolidHost.Pdm.GetName(d);\n" +
                "    string path = System.IO.Path.Combine(outputDir, name + \".stp\");\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        TopSolidHost.Application.Export(stepIdx, dId, path);\n" +
                "        exported++;\n" +
                "    }\n" +
                "    catch { skipped++; }\n" +
                "}\n" +
                "return \"Batch STEP export: \" + exported + \" exported, \" + skipped + \" skipped. Folder: \" + outputDir;") },

            { "batch_read_property", R("BATCH", "Reads a specific property across all project documents. Param: value=property_name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== PROPRIETE '{value}' SUR TOUT LE PROJET ===\");\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(d);\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        var pList = TopSolidHost.Parameters.GetParameters(dId);\n" +
                "        string found = \"(absent)\";\n" +
                "        foreach (var p in pList)\n" +
                "        {\n" +
                "            string pName = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "            if (pName.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "            {\n" +
                "                var t = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "                if (t == ParameterType.Real) found = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F4\");\n" +
                "                else if (t == ParameterType.Text) found = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "                else if (t == ParameterType.Integer) found = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "                else if (t == ParameterType.Boolean) found = TopSolidHost.Parameters.GetBooleanValue(p).ToString();\n" +
                "                break;\n" +
                "            }\n" +
                "        }\n" +
                "        sb.AppendLine(\"  \" + name + \": \" + found);\n" +
                "    }\n" +
                "    catch { sb.AppendLine(\"  \" + name + \": (error)\"); }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "find_modified_documents", R("PROJECTS", "Lists unsaved (dirty) documents of the project",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int dirty = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        if (TopSolidHost.Pdm.IsDirty(d))\n" +
                "        {\n" +
                "            sb.AppendLine(\"  MODIFIE: \" + TopSolidHost.Pdm.GetName(d));\n" +
                "            dirty++;\n" +
                "        }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.Insert(0, \"Modified documents (unsaved): \" + dirty + \"/\" + docs.Count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "batch_clear_author", RW("BATCH", "Clears the Author field on all project documents",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"No current project.\"; return; }\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "int cleared = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string author = TopSolidHost.Pdm.GetAuthor(d);\n" +
                "    if (!string.IsNullOrEmpty(author))\n" +
                "    {\n" +
                "        TopSolidHost.Pdm.SetAuthor(d, \"\");\n" +
                "        cleared++;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"OK: Author cleared on \" + cleared + \"/\" + docs.Count + \" documents.\";") },

            { "batch_set_designation", RW("BATCH", "Sets Designation (Description) on ALL documents of the current project. Param: value=new_designation",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"No current project.\"; return; }\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "int updated = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    TopSolidHost.Pdm.SetDescription(d, \"{value}\");\n" +
                "    TopSolidHost.Pdm.Save(d, true);\n" +
                "    updated++;\n" +
                "}\n" +
                "__message = \"OK: Designation set to '{value}' on \" + updated + \" document(s).\";") },

            { "batch_set_reference", RW("BATCH", "Sets Reference (PartNumber) on ALL documents of the current project. Param: value=new_reference",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"No current project.\"; return; }\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "int updated = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    TopSolidHost.Pdm.SetPartNumber(d, \"{value}\");\n" +
                "    TopSolidHost.Pdm.Save(d, true);\n" +
                "    updated++;\n" +
                "}\n" +
                "__message = \"OK: Reference set to '{value}' on \" + updated + \" document(s).\";") },

            { "batch_set_manufacturer", RW("BATCH", "Sets Manufacturer on ALL documents of the current project. Param: value=new_manufacturer",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"No current project.\"; return; }\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "int updated = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    TopSolidHost.Pdm.SetManufacturer(d, \"{value}\");\n" +
                "    TopSolidHost.Pdm.Save(d, true);\n" +
                "    updated++;\n" +
                "}\n" +
                "__message = \"OK: Manufacturer set to '{value}' on \" + updated + \" document(s).\";") },

            { "clear_document_author", RW("DOCUMENT", "Clears the Author field on the current document",
                "TopSolidHost.Pdm.SetAuthor(pdmId, \"\");\n" +
                "__message = \"OK: Author cleared on current document.\";") },

            { "batch_check_virtual", R("BATCH", "Checks the virtual property (IsVirtualDocument) on all project documents",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int virtuel = 0; int nonVirtuel = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        bool isVirtual = TopSolidHost.Documents.IsVirtualDocument(dId);\n" +
                "        string name = TopSolidHost.Pdm.GetName(d);\n" +
                "        if (isVirtual) { virtuel++; }\n" +
                "        else { sb.AppendLine(\"  NON-VIRTUEL: \" + name); nonVirtuel++; }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.Insert(0, \"Virtual: \" + virtuel + \", Non-virtual: \" + nonVirtuel + \"/\" + docs.Count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "batch_enable_virtual", RW("BATCH", "Enables virtual mode on ALL non-virtual project documents",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"No current project.\"; return; }\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "int activated = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        if (!TopSolidHost.Documents.IsVirtualDocument(dId))\n" +
                "        {\n" +
                "            TopSolidHost.Documents.SetVirtualDocumentMode(dId, true);\n" +
                "            activated++;\n" +
                "        }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "__message = \"OK: Virtual mode enabled on \" + activated + \" document(s).\";") },

            { "enable_virtual_document", RW("DOCUMENT", "Enables virtual mode on the current document",
                "TopSolidHost.Documents.SetVirtualDocumentMode(docId, true);\n" +
                "__message = \"OK: Virtual mode enabled on current document.\";") },

            { "check_family_drivers", R("FAMILIES", "Checks that family drivers have a designation. Lists those without.",
                "DocumentId famDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (famDocId.IsEmpty) return \"No document open.\";\n" +
                "bool isFam = false;\n" +
                "try { isFam = TopSolidHost.Families.IsFamily(famDocId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isFam) return \"This document is NOT a family.\";\n" +
                "var drivers = TopSolidHost.Families.GetCatalogColumnParameters(famDocId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== FAMILY DRIVERS ===\");\n" +
                "sb.AppendLine(\"Drivers: \" + drivers.Count);\n" +
                "int withDesc = 0; int withoutDesc = 0;\n" +
                "foreach (var d in drivers)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(d);\n" +
                "    string desc = \"\";\n" +
                "    try { desc = TopSolidHost.Elements.GetDescription(d); } catch {}\n" +
                "    if (string.IsNullOrEmpty(desc))\n" +
                "    {\n" +
                "        sb.AppendLine(\"  SANS DESIGNATION: \" + name);\n" +
                "        withoutDesc++;\n" +
                "    }\n" +
                "    else\n" +
                "    {\n" +
                "        sb.AppendLine(\"  OK: \" + name + \" -> \" + desc);\n" +
                "        withDesc++;\n" +
                "    }\n" +
                "}\n" +
                "sb.AppendLine(\"\\n\" + withDesc + \" with designation, \" + withoutDesc + \" without.\");\n" +
                "return sb.ToString();") },

            { "fix_family_drivers", RW("FAMILIES", "Assigns a designation to family drivers that lack one (inferred from parameter name)",
                "DocumentId famDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (famDocId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isFam = false;\n" +
                "try { isFam = TopSolidHost.Families.IsFamily(famDocId); } catch { __message = \"Unable to verify.\"; return; }\n" +
                "if (!isFam) { __message = \"This document is NOT a family.\"; return; }\n" +
                "var drivers = TopSolidHost.Families.GetCatalogColumnParameters(famDocId);\n" +
                "TopSolidHost.Documents.EnsureIsDirty(ref famDocId);\n" +
                "int fixed_ = 0;\n" +
                "foreach (var d in drivers)\n" +
                "{\n" +
                "    string desc = \"\";\n" +
                "    try { desc = TopSolidHost.Elements.GetDescription(d); } catch {}\n" +
                "    if (string.IsNullOrEmpty(desc))\n" +
                "    {\n" +
                "        string name = TopSolidHost.Elements.GetFriendlyName(d);\n" +
                "        // Derive the designation from the name: CamelCase split + spaces\n" +
                "        var result = new System.Text.StringBuilder();\n" +
                "        for (int i = 0; i < name.Length; i++)\n" +
                "        {\n" +
                "            char c = name[i];\n" +
                "            if (c == '_' || c == '-') { result.Append(' '); continue; }\n" +
                "            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i-1])) result.Append(' ');\n" +
                "            result.Append(c);\n" +
                "        }\n" +
                "        string newDesc = result.ToString().Trim();\n" +
                "        if (newDesc.Length > 0)\n" +
                "        {\n" +
                "            newDesc = char.ToUpper(newDesc[0]) + newDesc.Substring(1);\n" +
                "            TopSolidHost.Elements.SetDescription(d, newDesc);\n" +
                "            fixed_++;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "__message = \"OK: \" + fixed_ + \" drivers fixed with designation inferred from name.\";") },

            { "batch_check_family_drivers", R("FAMILIES", "Checks drivers of all families in the project",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== FAMILY DRIVERS AUDIT ===\");\n" +
                "int famCount = 0; int totalMissing = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        if (!TopSolidHost.Families.IsFamily(dId)) continue;\n" +
                "        famCount++;\n" +
                "        string famName = TopSolidHost.Pdm.GetName(d);\n" +
                "        var drivers = TopSolidHost.Families.GetCatalogColumnParameters(dId);\n" +
                "        int missing = 0;\n" +
                "        foreach (var drv in drivers)\n" +
                "        {\n" +
                "            string desc = \"\";\n" +
                "            try { desc = TopSolidHost.Elements.GetDescription(drv); } catch {}\n" +
                "            if (string.IsNullOrEmpty(desc)) missing++;\n" +
                "        }\n" +
                "        if (missing > 0)\n" +
                "        {\n" +
                "            sb.AppendLine(\"  \" + famName + \": \" + missing + \"/\" + drivers.Count + \" without designation\");\n" +
                "            totalMissing += missing;\n" +
                "        }\n" +
                "        else sb.AppendLine(\"  \" + famName + \": OK (\" + drivers.Count + \" drivers)\");\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.Insert(0, \"Families: \" + famCount + \", Drivers without designation: \" + totalMissing + \"\\n\");\n" +
                "return sb.ToString();") },

            { "audit_parameter_names", R("AUDIT", "Audits parameter name syntax: detects convention inconsistencies and near-duplicates",
                "DocumentId curDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDocId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(curDocId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== PARAMETER NAMES AUDIT ===\");\n" +
                "var names = new List<string>();\n" +
                "int camel = 0; int under = 0; int space = 0; int upper = 0; int lower = 0;\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.Contains(\"$\")) continue;\n" +
                "    names.Add(name);\n" +
                "    bool hasCamel = false; bool hasUnder = false; bool hasSpace = false;\n" +
                "    for (int i = 1; i < name.Length; i++)\n" +
                "    {\n" +
                "        if (char.IsUpper(name[i]) && char.IsLower(name[i-1])) hasCamel = true;\n" +
                "        if (name[i] == '_') hasUnder = true;\n" +
                "        if (name[i] == ' ') hasSpace = true;\n" +
                "    }\n" +
                "    if (hasCamel) camel++;\n" +
                "    if (hasUnder) under++;\n" +
                "    if (hasSpace) space++;\n" +
                "    if (name == name.ToUpper()) upper++;\n" +
                "    if (name == name.ToLower()) lower++;\n" +
                "}\n" +
                "sb.AppendLine(\"User parameters: \" + names.Count);\n" +
                "sb.AppendLine(\"Detected conventions:\");\n" +
                "if (camel > 0) sb.AppendLine(\"  CamelCase: \" + camel);\n" +
                "if (under > 0) sb.AppendLine(\"  underscore: \" + under);\n" +
                "if (space > 0) sb.AppendLine(\"  spaces: \" + space);\n" +
                "if (upper > 0) sb.AppendLine(\"  UPPERCASE: \" + upper);\n" +
                "if (lower > 0) sb.AppendLine(\"  lowercase: \" + lower);\n" +
                "int conventions = (camel>0?1:0) + (under>0?1:0) + (space>0?1:0) + (upper>0?1:0) + (lower>0?1:0);\n" +
                "if (conventions > 1) sb.AppendLine(\"  *** ATTENTION: \" + conventions + \" mixed conventions! ***\");\n" +
                "else sb.AppendLine(\"  Single convention: OK\");\n" +
                "// Doublons proches (Levenshtein simple)\n" +
                "sb.AppendLine(\"\\nDoublons potentiels (casse differente):\");\n" +
                "int dupes = 0;\n" +
                "for (int i = 0; i < names.Count; i++)\n" +
                "    for (int j = i + 1; j < names.Count; j++)\n" +
                "        if (names[i].ToLower() == names[j].ToLower())\n" +
                "        { sb.AppendLine(\"  '\" + names[i] + \"' vs '\" + names[j] + \"'\"); dupes++; }\n" +
                "if (dupes == 0) sb.AppendLine(\"  No duplicates.\");\n" +
                "// List every name for visual inspection by the LLM\n" +
                "sb.AppendLine(\"\\nListe complete:\");\n" +
                "foreach (var n in names) sb.AppendLine(\"  \" + n);\n" +
                "return sb.ToString();") },

            { "batch_audit_parameter_names", R("AUDIT", "Audits parameter name syntax across all project documents",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== PROJECT PARAMETER NAMES AUDIT ===\");\n" +
                "var allNames = new Dictionary<string, List<string>>();\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        string docName = TopSolidHost.Pdm.GetName(d);\n" +
                "        var pList = TopSolidHost.Parameters.GetParameters(dId);\n" +
                "        foreach (var p in pList)\n" +
                "        {\n" +
                "            string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "            if (name.Contains(\"$\")) continue;\n" +
                "            string key = name.ToLower();\n" +
                "            if (!allNames.ContainsKey(key)) allNames[key] = new List<string>();\n" +
                "            if (!allNames[key].Contains(name)) allNames[key].Add(name);\n" +
                "        }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "// Find the variants (same name, different casing)\n" +
                "sb.AppendLine(\"Unique names (excl. system): \" + allNames.Count);\n" +
                "sb.AppendLine(\"\\nVariantes de casse detectees:\");\n" +
                "int variants = 0;\n" +
                "foreach (var kvp in allNames)\n" +
                "{\n" +
                "    if (kvp.Value.Count > 1)\n" +
                "    {\n" +
                "        sb.AppendLine(\"  \" + string.Join(\" / \", kvp.Value));\n" +
                "        variants++;\n" +
                "    }\n" +
                "}\n" +
                "if (variants == 0) sb.AppendLine(\"  No variants.\");\n" +
                "// List every name for LLM inspection (typos)\n" +
                "sb.AppendLine(\"\\nAll parameter names in the project:\");\n" +
                "foreach (var kvp in allNames)\n" +
                "    sb.AppendLine(\"  \" + kvp.Value[0]);\n" +
                "return sb.ToString();") },

            { "batch_audit_driver_designations", R("AUDIT", "Lists driver designations of all project families for inspection (typos, inconsistencies)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== DRIVER DESIGNATIONS AUDIT ===\");\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        if (!TopSolidHost.Families.IsFamily(dId)) continue;\n" +
                "        string famName = TopSolidHost.Pdm.GetName(d);\n" +
                "        sb.AppendLine(\"\\n\" + famName + \":\");\n" +
                "        var drivers = TopSolidHost.Families.GetCatalogColumnParameters(dId);\n" +
                "        foreach (var drv in drivers)\n" +
                "        {\n" +
                "            string name = TopSolidHost.Elements.GetFriendlyName(drv);\n" +
                "            string desc = \"\";\n" +
                "            try { desc = TopSolidHost.Elements.GetDescription(drv); } catch {}\n" +
                "            sb.AppendLine(\"  \" + name + \" -> \" + (string.IsNullOrEmpty(desc) ? \"(EMPTY)\" : desc));\n" +
                "        }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "read_revision_history", R("DOCUMENT", "Lists all major/minor revisions of the current document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var majors = TopSolidHost.Pdm.GetMajorRevisions(pdmId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== REVISION HISTORY ===\");\n" +
                "sb.AppendLine(\"Document: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "sb.AppendLine(\"Major revisions: \" + majors.Count);\n" +
                "foreach (var maj in majors)\n" +
                "{\n" +
                "    string majText = TopSolidHost.Pdm.GetMajorRevisionText(maj);\n" +
                "    var state = TopSolidHost.Pdm.GetMajorRevisionLifeCycleMainState(maj);\n" +
                "    var minors = TopSolidHost.Pdm.GetMinorRevisions(maj);\n" +
                "    sb.AppendLine(\"  Rev \" + majText + \" (\" + state + \") - \" + minors.Count + \" minor(s)\");\n" +
                "    foreach (var min in minors)\n" +
                "    {\n" +
                "        string minText = TopSolidHost.Pdm.GetMinorRevisionText(min);\n" +
                "        sb.AppendLine(\"    .\" + minText);\n" +
                "    }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "compare_revisions", R("AUDIT", "Compares parameters of the current revision with the previous one",
                "DocumentId curDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDocId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId curPdmId = TopSolidHost.Documents.GetPdmObject(curDocId);\n" +
                "// Read the parameters of the current document\n" +
                "var paramsNow = TopSolidHost.Parameters.GetParameters(curDocId);\n" +
                "var dictNow = new System.Collections.Generic.Dictionary<string, string>();\n" +
                "foreach (var p in paramsNow)\n" +
                "{\n" +
                "    string n = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    var t = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string v = \"\";\n" +
                "    if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F6\");\n" +
                "    else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "    else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    else if (t == ParameterType.Boolean) v = TopSolidHost.Parameters.GetBooleanValue(p).ToString();\n" +
                "    dictNow[n] = v;\n" +
                "}\n" +
                "// Find the previous revision\n" +
                "var majors = TopSolidHost.Pdm.GetMajorRevisions(curPdmId);\n" +
                "if (majors.Count == 0) return \"No revision found.\";\n" +
                "var lastMajor = majors[majors.Count - 1];\n" +
                "var minors = TopSolidHost.Pdm.GetMinorRevisions(lastMajor);\n" +
                "if (minors.Count < 2) return \"Only one revision, nothing to compare.\";\n" +
                "var prevMinor = minors[minors.Count - 2];\n" +
                "// Open the previous revision read-only\n" +
                "DocumentId prevDocId = TopSolidHost.Documents.GetMinorRevisionDocument(prevMinor);\n" +
                "var paramsPrev = TopSolidHost.Parameters.GetParameters(prevDocId);\n" +
                "var dictPrev = new System.Collections.Generic.Dictionary<string, string>();\n" +
                "foreach (var p in paramsPrev)\n" +
                "{\n" +
                "    string n = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    var t = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string v = \"\";\n" +
                "    if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F6\");\n" +
                "    else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p);\n" +
                "    else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    else if (t == ParameterType.Boolean) v = TopSolidHost.Parameters.GetBooleanValue(p).ToString();\n" +
                "    dictPrev[n] = v;\n" +
                "}\n" +
                "// Comparer\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== REVISION COMPARISON ===\");\n" +
                "string curMinText = TopSolidHost.Pdm.GetMinorRevisionText(minors[minors.Count - 1]);\n" +
                "string prevMinText = TopSolidHost.Pdm.GetMinorRevisionText(prevMinor);\n" +
                "sb.AppendLine(\"Rev .\" + prevMinText + \" vs Rev .\" + curMinText + \" (current)\");\n" +
                "int diffs = 0;\n" +
                "foreach (var kvp in dictNow)\n" +
                "{\n" +
                "    string vPrev;\n" +
                "    if (dictPrev.TryGetValue(kvp.Key, out vPrev))\n" +
                "    {\n" +
                "        if (kvp.Value != vPrev) { sb.AppendLine(\"  MODIFIE: \" + kvp.Key + \" : \" + vPrev + \" -> \" + kvp.Value); diffs++; }\n" +
                "        dictPrev.Remove(kvp.Key);\n" +
                "    }\n" +
                "    else { sb.AppendLine(\"  AJOUTE: \" + kvp.Key + \" = \" + kvp.Value); diffs++; }\n" +
                "}\n" +
                "foreach (var kvp in dictPrev)\n" +
                "    { sb.AppendLine(\"  SUPPRIME: \" + kvp.Key + \" = \" + kvp.Value); diffs++; }\n" +
                "if (diffs == 0) sb.AppendLine(\"  No parameter differences.\");\n" +
                "else sb.AppendLine(\"\\n\" + diffs + \" difference(s)\");\n" +
                "return sb.ToString();") },

            { "export_bom_csv", R("BOM", "Exports the BOM as text (separated columns)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isBom) return \"This document is not a BOM.\";\n" +
                "int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "// Headers\n" +
                "var headers = new System.Collections.Generic.List<string>();\n" +
                "for (int i = 0; i < colCount; i++)\n" +
                "    headers.Add(TopSolidDesignHost.Boms.GetColumnTitle(docId, i));\n" +
                "sb.AppendLine(string.Join(\";\", headers));\n" +
                "// Rows\n" +
                "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n" +
                "foreach (int rowId in children)\n" +
                "{\n" +
                "    if (!TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) continue;\n" +
                "    System.Collections.Generic.List<Property> props;\n" +
                "    System.Collections.Generic.List<string> texts;\n" +
                "    TopSolidDesignHost.Boms.GetRowContents(docId, rowId, out props, out texts);\n" +
                "    sb.AppendLine(string.Join(\";\", texts));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "check_missing_materials", R("MATERIALS", "Lists project parts without assigned material",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> allDocs = new List<PdmObjectId>();\n" +
                "Action<PdmObjectId> collectDocs = null;\n" +
                "collectDocs = (parentId) => {\n" +
                "    List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "    TopSolidHost.Pdm.GetConstituents(parentId, out folders, out docs);\n" +
                "    if (docs != null) allDocs.AddRange(docs);\n" +
                "    if (folders != null) foreach (var f in folders) collectDocs(f);\n" +
                "};\n" +
                "collectDocs(projId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== VERIFICATION MATERIALS ===\");\n" +
                "int missing = 0; int total = 0;\n" +
                "foreach (var item in allDocs)\n" +
                "{\n" +
                "    string ext = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(item, out ext);\n" +
                "    if (!string.Equals(ext, \".TopPrt\", StringComparison.OrdinalIgnoreCase)) continue;\n" +
                "    string name = TopSolidHost.Pdm.GetName(item);\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(item);\n" +
                "        if (dId.IsEmpty) continue;\n" +
                "        total++;\n" +
                "        DocumentId matId = TopSolidDesignHost.Parts.GetMaterial(dId);\n" +
                "        if (matId.IsEmpty) { sb.AppendLine(\"  SANS MATERIAU: \" + name); missing++; }\n" +
                "        else { sb.AppendLine(\"  OK: \" + name + \" -> \" + TopSolidHost.Documents.GetName(matId)); }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.AppendLine(\"\\n\" + missing + \"/\" + total + \" part(s) without material.\");\n" +
                "return sb.ToString();") },

            { "assembly_mass_report", R("MEASUREMENTS", "Reads total mass, volume, surface and part count of an assembly",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Not an assembly.\"; }\n" +
                "if (!isAsm) return \"Not an assembly.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== ASSEMBLY MASS REPORT ===\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\")\n" +
                "        sb.AppendLine(\"Total mass: \" + TopSolidHost.Parameters.GetRealValue(p).ToString(\"F3\") + \" kg\");\n" +
                "    else if (name == \"Volume\")\n" +
                "        sb.AppendLine(\"Total volume: \" + (TopSolidHost.Parameters.GetRealValue(p) * 1e9).ToString(\"F2\") + \" mm3\");\n" +
                "    else if (name == \"Surface Area\")\n" +
                "        sb.AppendLine(\"Total surface: \" + (TopSolidHost.Parameters.GetRealValue(p) * 1e6).ToString(\"F2\") + \" mm2\");\n" +
                "    else if (name == \"Part Count\")\n" +
                "    {\n" +
                "        var pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "        if (pType == ParameterType.Integer) sb.AppendLine(\"Part count: \" + TopSolidHost.Parameters.GetIntegerValue(p));\n" +
                "        else sb.AppendLine(\"Part count: \" + TopSolidHost.Parameters.GetRealValue(p).ToString(\"F0\"));\n" +
                "    }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "read_material", R("MATERIALS", "Reads part material (mass and calculated density)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "double mass = 0; double vol = 0;\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\") mass = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "    else if (name == \"Volume\") vol = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "}\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "if (mass > 0)\n" +
                "{\n" +
                "    sb.AppendLine(\"Material assigned.\");\n" +
                "    sb.AppendLine(\"Mass: \" + mass.ToString(\"F3\") + \" kg\");\n" +
                "    if (vol > 0) sb.AppendLine(\"Calculated density: \" + (mass / vol).ToString(\"F0\") + \" kg/m3\");\n" +
                "}\n" +
                "else sb.AppendLine(\"No material assigned (mass = 0).\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // EXPORT — Additional formats
            // =====================================================================
            { "export_stl", RD("EXPORT", "Exports to STL (3D printing). Param: value=path",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "int idx = -1;\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName; string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    foreach (string ext in extensions)\n" +
                "        if (ext.ToLower().Contains(\"stl\")) { idx = i; break; }\n" +
                "    if (idx >= 0) break;\n" +
                "}\n" +
                "if (idx < 0) return \"STL exporter not found.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.stl\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exported to STL → \" + path;") },
            { "export_iges", RD("EXPORT", "Exports to IGES. Param: value=path",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "int idx = -1;\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName; string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    foreach (string ext in extensions)\n" +
                "        if (ext.ToLower().Contains(\"igs\") || ext.ToLower().Contains(\"iges\")) { idx = i; break; }\n" +
                "    if (idx >= 0) break;\n" +
                "}\n" +
                "if (idx < 0) return \"IGES exporter not found.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.igs\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exported to IGES → \" + path;") },

            // =====================================================================
            // ASSEMBLY — Count & Diagnostics
            // =====================================================================
            { "count_assembly_parts", R("ASSEMBLIES", "Counts parts grouped by type with quantities",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Not an assembly.\"; }\n" +
                "if (!isAsm) return \"Not an assembly.\";\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "var counts = new System.Collections.Generic.Dictionary<string, int>();\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    bool isInclusion = false;\n" +
                "    try { isInclusion = TopSolidDesignHost.Assemblies.IsInclusion(op); } catch { continue; }\n" +
                "    if (!isInclusion) continue;\n" +
                "    DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(op);\n" +
                "    string defName = \"?\";\n" +
                "    if (!defDoc.IsEmpty) { PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc); defName = TopSolidHost.Pdm.GetName(defPdm); }\n" +
                "    if (counts.ContainsKey(defName)) counts[defName]++; else counts[defName] = 1;\n" +
                "}\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== PARTS COUNT ===\");\n" +
                "int total = 0;\n" +
                "foreach (var kvp in counts)\n" +
                "{\n" +
                "    sb.AppendLine(\"  \" + kvp.Value + \"x \" + kvp.Key);\n" +
                "    total += kvp.Value;\n" +
                "}\n" +
                "sb.AppendLine(\"Total: \" + total + \" parts (\" + counts.Count + \" unique references)\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // PROJECT — Batch operations
            // =====================================================================
            { "save_all_project", RW("PROJECTS", "Saves all documents of the current project",
                // __message is declared by the modification wrapper.
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"No current project.\"; return; }\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "int saved = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    try { TopSolidHost.Pdm.Save(item, true); saved++; } catch { continue; }\n" +
                "}\n" +
                "__message = \"OK: \" + saved + \"/\" + items.Count + \" documents saved.\";") },
            { "open_document_by_name", R("DOCUMENT", "Searches and opens a document by name. Param: value=name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' not found.\";\n" +
                "DocumentId dId = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "TopSolidHost.Documents.Open(ref dId);\n" +
                "return \"OK: Document '\" + TopSolidHost.Pdm.GetName(results[0]) + \"' opened.\";") },

            // =====================================================================
            // BATCH — Recipes for library designers
            // =====================================================================
            { "list_folder_documents", R("PROJECTS", "Lists documents of a specific project folder. Param: value=folder_name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "PdmObjectId targetFolder = PdmObjectId.Empty;\n" +
                "foreach (var f in folders)\n" +
                "{\n" +
                "    if (TopSolidHost.Pdm.GetName(f).IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    { targetFolder = f; break; }\n" +
                "}\n" +
                "if (targetFolder.IsEmpty) return \"Folder '{value}' not found.\";\n" +
                "List<PdmObjectId> subFolders; List<PdmObjectId> subDocs;\n" +
                "TopSolidHost.Pdm.GetConstituents(targetFolder, out subFolders, out subDocs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Folder: \" + TopSolidHost.Pdm.GetName(targetFolder) + \" (\" + subDocs.Count + \" docs)\");\n" +
                "foreach (var d in subDocs)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(d);\n" +
                "    string desc = TopSolidHost.Pdm.GetDescription(d);\n" +
                "    string pn = TopSolidHost.Pdm.GetPartNumber(d);\n" +
                "    string type = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(d, out type);\n" +
                "    sb.AppendLine(\"  \" + name + \" | \" + desc + \" | \" + pn + \" | \" + type);\n" +
                "}\n" +
                "return sb.ToString();") },

            { "summarize_project", R("PROJECTS", "Project summary: document count by type, folders, size",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var types = new Dictionary<string, int>();\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string t = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(d, out t);\n" +
                "    if (types.ContainsKey(t)) types[t]++;\n" +
                "    else types[t] = 1;\n" +
                "}\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Project: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "sb.AppendLine(\"Folders: \" + folders.Count);\n" +
                "sb.AppendLine(\"Documents: \" + docs.Count);\n" +
                "foreach (var kv in types)\n" +
                "    sb.AppendLine(\"  \" + kv.Key + \": \" + kv.Value);\n" +
                "return sb.ToString();") },

            { "list_documents_without_reference", R("AUDIT", "Lists project documents without reference (empty part number)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int missing = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string pn = TopSolidHost.Pdm.GetPartNumber(d);\n" +
                "    if (string.IsNullOrEmpty(pn))\n" +
                "    {\n" +
                "        sb.AppendLine(\"  \" + TopSolidHost.Pdm.GetName(d));\n" +
                "        missing++;\n" +
                "    }\n" +
                "}\n" +
                "sb.Insert(0, \"Documents sans reference: \" + missing + \"/\" + docs.Count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "list_documents_without_designation", R("AUDIT", "Lists project documents without designation (empty description)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int missing = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string desc = TopSolidHost.Pdm.GetDescription(d);\n" +
                "    if (string.IsNullOrEmpty(desc))\n" +
                "    {\n" +
                "        sb.AppendLine(\"  \" + TopSolidHost.Pdm.GetName(d));\n" +
                "        missing++;\n" +
                "    }\n" +
                "}\n" +
                "sb.Insert(0, \"Documents sans designation: \" + missing + \"/\" + docs.Count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "count_documents_by_type", R("PROJECTS", "Counts project documents grouped by type (.TopPrt, .TopAsm, .TopDft...)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var types = new Dictionary<string, int>();\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string t = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(d, out t);\n" +
                "    if (types.ContainsKey(t)) types[t]++;\n" +
                "    else types[t] = 1;\n" +
                "}\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Total: \" + docs.Count + \" documents\");\n" +
                "foreach (var kv in types)\n" +
                "    sb.AppendLine(\"  \" + kv.Key + \": \" + kv.Value);\n" +
                "return sb.ToString();") },

            { "search_parts_by_material", R("MATERIALS", "Lists parts with their material (via mass > 0). Param: value=optional filter",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int count = 0;\n" +
                "foreach (var d in docs)\n" +
                "{\n" +
                "    string t = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(d, out t);\n" +
                "    if (t != \".TopPrt\") continue;\n" +
                "    string name = TopSolidHost.Pdm.GetName(d);\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(d);\n" +
                "        var pList = TopSolidHost.Parameters.GetParameters(dId);\n" +
                "        double mass = 0;\n" +
                "        foreach (var p in pList)\n" +
                "        {\n" +
                "            if (TopSolidHost.Elements.GetFriendlyName(p) == \"Mass\")\n" +
                "            { mass = TopSolidHost.Parameters.GetRealValue(p); break; }\n" +
                "        }\n" +
                "        string info = name + \" | mass=\" + mass.ToString(\"F3\") + \"kg\";\n" +
                "        if (\"{value}\" == \"\" || info.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "        { sb.AppendLine(\"  \" + info); count++; }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.Insert(0, \"Parts found: \" + count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "read_where_used", R("PROJECTS", "Finds where-used references of the current document in the project",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);\n" +
                "PdmMajorRevisionId majorRev = TopSolidHost.Pdm.GetLastMajorRevision(pdmId);\n" +
                "var backRefs = TopSolidHost.Pdm.SearchMajorRevisionBackReferences(projId, majorRev);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Where-used: \" + backRefs.Count);\n" +
                "foreach (var br in backRefs)\n" +
                "{\n" +
                "    PdmMajorRevisionId brMajor = TopSolidHost.Pdm.GetMajorRevision(br);\n" +
                "    PdmObjectId brObj = TopSolidHost.Pdm.GetPdmObject(brMajor);\n" +
                "    string name = TopSolidHost.Pdm.GetName(brObj);\n" +
                "    string type = \"\";\n" +
                "    TopSolidHost.Pdm.GetType(brObj, out type);\n" +
                "    sb.AppendLine(\"  \" + name + \" (\" + type + \")\");\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // COLORS — Reading faces
            // =====================================================================
            // --- ATTRIBUTES (colour, transparency, layer, visibility) ---
            // TopSolid: clic droit → Attributs → Color / Transparency / Layer
            { "attr_read_all", R("ATTRIBUTES", "Reads color, transparency, layer and visibility of all elements",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== ATTRIBUTES ===\");\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    sb.Append(name + \": \");\n" +
                "    if (TopSolidHost.Elements.HasColor(s))\n" +
                "    {\n" +
                "        Color c = TopSolidHost.Elements.GetColor(s);\n" +
                "        sb.Append(\"color=RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \") \");\n" +
                "    }\n" +
                "    if (TopSolidHost.Elements.HasTransparency(s))\n" +
                "        sb.Append(\"transparency=\" + TopSolidHost.Elements.GetTransparency(s).ToString(\"F2\") + \" \");\n" +
                "    sb.Append(\"visible=\" + TopSolidHost.Elements.IsVisible(s));\n" +
                "    try\n" +
                "    {\n" +
                "        ElementId layerId = TopSolidHost.Layers.GetLayer(docId, s);\n" +
                "        if (!layerId.IsEmpty) sb.Append(\" layer=\" + TopSolidHost.Elements.GetFriendlyName(layerId));\n" +
                "    } catch {}\n" +
                "    sb.AppendLine();\n" +
                "}\n" +
                "return sb.ToString();") },

            { "attr_set_color", RW("ATTRIBUTES", "Sets color. If 1 shape: direct. If multiple: asks selection. Param: value=R,G,B (e.g. 0,0,255)",
                "string[] rgb = \"{value}\".Split(',');\n" +
                "if (rgb.Length != 3) { __message = \"Format: R,G,B (e.g. 255,0,0 for red)\"; return; }\n" +
                "int r, g, b;\n" +
                "if (!int.TryParse(rgb[0].Trim(), out r) || !int.TryParse(rgb[1].Trim(), out g) || !int.TryParse(rgb[2].Trim(), out b))\n" +
                "{ __message = \"Format: R,G,B (ex: 255,0,0)\"; return; }\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "ElementId target = ElementId.Empty;\n" +
                "if (shapes.Count == 1) { target = shapes[0]; }\n" +
                "else if (shapes.Count > 1)\n" +
                "{\n" +
                "    UserQuestion q = new UserQuestion(\"Multiple elements. Select the one to color\", \"\");\n" +
                "    UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out target);\n" +
                "    if (answer != UserAnswerType.Ok || target.IsEmpty) { __message = \"Selection cancelled.\"; return; }\n" +
                "}\n" +
                "else { __message = \"No shape in the document.\"; return; }\n" +
                "TopSolidHost.Elements.SetColor(target, new Color((byte)r, (byte)g, (byte)b));\n" +
                "__message = \"OK: \" + TopSolidHost.Elements.GetFriendlyName(target) + \" → RGB(\" + r + \",\" + g + \",\" + b + \")\";") },

            { "attr_set_color_all", RW("ATTRIBUTES", "Sets color on ALL elements. Param: value=R,G,B",
                "string[] rgb = \"{value}\".Split(',');\n" +
                "if (rgb.Length != 3) { __message = \"Format: R,G,B\"; return; }\n" +
                "int r, g, b;\n" +
                "if (!int.TryParse(rgb[0].Trim(), out r) || !int.TryParse(rgb[1].Trim(), out g) || !int.TryParse(rgb[2].Trim(), out b))\n" +
                "{ __message = \"Format: R,G,B\"; return; }\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "int count = 0;\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    if (TopSolidHost.Elements.IsColorModifiable(s))\n" +
                "    { TopSolidHost.Elements.SetColor(s, new Color((byte)r, (byte)g, (byte)b)); count++; }\n" +
                "}\n" +
                "__message = \"OK: \" + count + \" element(s) → RGB(\" + r + \",\" + g + \",\" + b + \")\";") },

            { "attr_read_color", R("ATTRIBUTES", "Reads element colors",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    if (TopSolidHost.Elements.HasColor(s))\n" +
                "    {\n" +
                "        Color c = TopSolidHost.Elements.GetColor(s);\n" +
                "        sb.AppendLine(name + \": RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \")\");\n" +
                "    }\n" +
                "    else sb.AppendLine(name + \": (no colour)\");\n" +
                "}\n" +
                "return sb.ToString();") },

            { "attr_set_transparency", RW("ATTRIBUTES", "Sets transparency. If 1 shape: direct. If multiple: asks. Param: value=0.0 to 1.0",
                "double transp;\n" +
                "if (!double.TryParse(\"{value}\", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out transp))\n" +
                "{ __message = \"Format: number between 0.0 and 1.0\"; return; }\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "ElementId target = ElementId.Empty;\n" +
                "if (shapes.Count == 1) { target = shapes[0]; }\n" +
                "else if (shapes.Count > 1)\n" +
                "{\n" +
                "    UserQuestion q = new UserQuestion(\"Select the element\", \"\");\n" +
                "    UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out target);\n" +
                "    if (answer != UserAnswerType.Ok || target.IsEmpty) { __message = \"Selection cancelled.\"; return; }\n" +
                "}\n" +
                "else { __message = \"No shape.\"; return; }\n" +
                "TopSolidHost.Elements.SetTransparency(target, transp);\n" +
                "__message = \"OK: transparency \" + transp.ToString(\"F1\") + \" on \" + TopSolidHost.Elements.GetFriendlyName(target);") },

            { "attr_read_transparency", R("ATTRIBUTES", "Reads element transparency",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    if (TopSolidHost.Elements.HasTransparency(s))\n" +
                "        sb.AppendLine(name + \": \" + TopSolidHost.Elements.GetTransparency(s).ToString(\"F2\"));\n" +
                "    else sb.AppendLine(name + \": (no transparency)\");\n" +
                "}\n" +
                "return sb.ToString();") },

            { "attr_list_layers", R("ATTRIBUTES", "Lists document layers",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var layers = TopSolidHost.Layers.GetLayers(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Layers: \" + layers.Count);\n" +
                "foreach (var l in layers)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(l));\n" +
                "return sb.ToString();") },

            { "attr_assign_layer", RW("ATTRIBUTES", "Assigns an element to a layer. Param: value=element_name:layer_name",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: element_name:layer_name\"; return; }\n" +
                "string elemName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string layerName = \"{value}\".Substring(idx + 1).Trim();\n" +
                "// Find the layer\n" +
                "var layers = TopSolidHost.Layers.GetLayers(docId);\n" +
                "ElementId layerId = ElementId.Empty;\n" +
                "foreach (var l in layers)\n" +
                "{\n" +
                "    if (TopSolidHost.Elements.GetFriendlyName(l).IndexOf(layerName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    { layerId = l; break; }\n" +
                "}\n" +
                "if (layerId.IsEmpty) { __message = \"Layer '\" + layerName + \"' not found.\"; return; }\n" +
                "// Find the element\n" +
                "var elems = TopSolidHost.Elements.GetElements(docId);\n" +
                "foreach (var e in elems)\n" +
                "{\n" +
                "    if (TopSolidHost.Elements.GetFriendlyName(e).IndexOf(elemName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        TopSolidHost.Layers.SetLayer(e, layerId);\n" +
                "        __message = \"OK: \" + TopSolidHost.Elements.GetFriendlyName(e) + \" -> layer \" + layerName;\n" +
                "        return;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"Element '\" + elemName + \"' not found.\";") },

            { "attr_replace_color", RW("ATTRIBUTES", "Replaces a color with another on elements. Param: value=R1,G1,B1:R2,G2,B2 (e.g. 0,128,0:255,0,0 = green->red)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length != 2) { __message = \"Format: R1,G1,B1:R2,G2,B2 (e.g. 0,128,0:255,0,0)\"; return; }\n" +
                "string[] src = parts[0].Split(',');\n" +
                "string[] dst = parts[1].Split(',');\n" +
                "if (src.Length != 3 || dst.Length != 3) { __message = \"Format: R1,G1,B1:R2,G2,B2\"; return; }\n" +
                "int sr, sg, sb2, dr, dg, db;\n" +
                "if (!int.TryParse(src[0].Trim(), out sr) || !int.TryParse(src[1].Trim(), out sg) || !int.TryParse(src[2].Trim(), out sb2) ||\n" +
                "    !int.TryParse(dst[0].Trim(), out dr) || !int.TryParse(dst[1].Trim(), out dg) || !int.TryParse(dst[2].Trim(), out db))\n" +
                "{ __message = \"Invalid RGB values.\"; return; }\n" +
                "// Search in the document shapes\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "int changed = 0;\n" +
                "int tolerance = 50; // tolerance RGB\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    if (!TopSolidHost.Elements.HasColor(s)) continue;\n" +
                "    Color c = TopSolidHost.Elements.GetColor(s);\n" +
                "    if (System.Math.Abs(c.R - sr) < tolerance && System.Math.Abs(c.G - sg) < tolerance && System.Math.Abs(c.B - sb2) < tolerance)\n" +
                "    {\n" +
                "        if (TopSolidHost.Elements.IsColorModifiable(s))\n" +
                "        {\n" +
                "            TopSolidHost.Elements.SetColor(s, new Color((byte)dr, (byte)dg, (byte)db));\n" +
                "            changed++;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "// Search the operations too (inclusions inside an assembly)\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    if (!TopSolidHost.Elements.HasColor(op)) continue;\n" +
                "    Color c = TopSolidHost.Elements.GetColor(op);\n" +
                "    if (System.Math.Abs(c.R - sr) < tolerance && System.Math.Abs(c.G - sg) < tolerance && System.Math.Abs(c.B - sb2) < tolerance)\n" +
                "    {\n" +
                "        if (TopSolidHost.Elements.IsColorModifiable(op))\n" +
                "        {\n" +
                "            TopSolidHost.Elements.SetColor(op, new Color((byte)dr, (byte)dg, (byte)db));\n" +
                "            changed++;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "__message = changed + \" element(s) changed from RGB(\" + sr + \",\" + sg + \",\" + sb2 + \") to RGB(\" + dr + \",\" + dg + \",\" + db + \")\";") },

            // --- Selection interactive (IUser.Ask*) ---
            { "select_shape", R("GEOMETRY", "Asks the user to select a shape and returns its info",
                "ElementId selected = ElementId.Empty;\n" +
                "UserQuestion q = new UserQuestion(\"Select a shape\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out selected);\n" +
                "if (answer != UserAnswerType.Ok || selected.IsEmpty) return \"Selection cancelled.\";\n" +
                "string name = TopSolidHost.Elements.GetFriendlyName(selected);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Selected element: \" + name);\n" +
                "if (TopSolidHost.Elements.HasColor(selected))\n" +
                "{\n" +
                "    Color c = TopSolidHost.Elements.GetColor(selected);\n" +
                "    sb.AppendLine(\"Color: RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \")\");\n" +
                "}\n" +
                "if (TopSolidHost.Elements.HasTransparency(selected))\n" +
                "    sb.AppendLine(\"Transparency: \" + TopSolidHost.Elements.GetTransparency(selected).ToString(\"F2\"));\n" +
                "sb.AppendLine(\"Visible: \" + TopSolidHost.Elements.IsVisible(selected));\n" +
                "sb.AppendLine(\"Type: \" + TopSolidHost.Elements.GetTypeFullName(selected));\n" +
                "return sb.ToString();") },

            { "select_face", R("GEOMETRY", "Asks the user to select a face and returns its info",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "Color c = TopSolidHost.Shapes.GetFaceColor(selected);\n" +
                "sb.AppendLine(\"Face color: RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \")\");\n" +
                "double area = TopSolidHost.Shapes.GetFaceArea(selected);\n" +
                "sb.AppendLine(\"Area: \" + (area * 1e6).ToString(\"F2\") + \" cm2\");\n" +
                "int surfType = (int)TopSolidHost.Shapes.GetFaceSurfaceType(selected);\n" +
                "sb.AppendLine(\"Surface type: \" + surfType);\n" +
                "return sb.ToString();") },

            // =====================================================================
            // FACE GEOMETRY — New in TopSolid 7.21 (IShapes cone/torus accessors)
            // =====================================================================
            // Each recipe prompts the user to pick a face, then returns the
            // geometric property. Raw API returns SI (meters/radians); we
            // convert to user-friendly units (mm, degrees).
            { "get_face_cone_length", R("GEOMETRY", "Gets the length of a selected cone face (mm). Requires a cone face selection.",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a cone face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try {\n" +
                "    double lengthMeters = TopSolidHost.Shapes.GetFaceConeLength(selected);\n" +
                "    return \"Cone length: \" + (lengthMeters * 1000).ToString(\"F3\") + \" mm\";\n" +
                "} catch (Exception ex) {\n" +
                "    return \"Error (is the selection a cone face?): \" + ex.Message;\n" +
                "}") },

            { "get_face_cone_radius", R("GEOMETRY", "Gets the base radius of a selected cone face (mm). Requires a cone face selection.",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a cone face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try {\n" +
                "    double radiusMeters = TopSolidHost.Shapes.GetFaceConeRadius(selected);\n" +
                "    return \"Cone radius: \" + (radiusMeters * 1000).ToString(\"F3\") + \" mm\";\n" +
                "} catch (Exception ex) {\n" +
                "    return \"Error (is the selection a cone face?): \" + ex.Message;\n" +
                "}") },

            { "get_face_cone_semi_angle", R("GEOMETRY", "Gets the half-angle of a selected cone face (degrees). Requires a cone face selection.",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a cone face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try {\n" +
                "    double angleRadians = TopSolidHost.Shapes.GetFaceConeSemiAngle(selected);\n" +
                "    double angleDegrees = angleRadians * 180.0 / System.Math.PI;\n" +
                "    return \"Cone semi-angle: \" + angleDegrees.ToString(\"F2\") + \" deg (\" + angleRadians.ToString(\"F4\") + \" rad)\";\n" +
                "} catch (Exception ex) {\n" +
                "    return \"Error (is the selection a cone face?): \" + ex.Message;\n" +
                "}") },

            { "get_face_torus_major_radius", R("GEOMETRY", "Gets the major radius of a selected torus face (mm). Requires a torus face selection.",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a torus face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try {\n" +
                "    double radiusMeters = TopSolidHost.Shapes.GetFaceTorusMajorRadius(selected);\n" +
                "    return \"Torus major radius: \" + (radiusMeters * 1000).ToString(\"F3\") + \" mm\";\n" +
                "} catch (Exception ex) {\n" +
                "    return \"Error (is the selection a torus face?): \" + ex.Message;\n" +
                "}") },

            { "get_face_torus_minor_radius", R("GEOMETRY", "Gets the minor radius of a selected torus face (mm). Requires a torus face selection.",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a torus face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try {\n" +
                "    double radiusMeters = TopSolidHost.Shapes.GetFaceTorusMinorRadius(selected);\n" +
                "    return \"Torus minor radius: \" + (radiusMeters * 1000).ToString(\"F3\") + \" mm\";\n" +
                "} catch (Exception ex) {\n" +
                "    return \"Error (is the selection a torus face?): \" + ex.Message;\n" +
                "}") },

            { "get_item_last_operation_name", R("GEOMETRY", "Gets the name of the last operation that produced a selected face.",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select a face\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try {\n" +
                "    string opName = TopSolidHost.Operations.GetItemLastOperationName(selected);\n" +
                "    return string.IsNullOrEmpty(opName) ? \"Last operation: (none)\" : \"Last operation: \" + opName;\n" +
                "} catch (Exception ex) {\n" +
                "    return \"Error: \" + ex.Message;\n" +
                "}") },

            { "select_3d_point", R("GEOMETRY", "Asks the user to click a 3D point and returns coordinates",
                "SmartPoint3D selected;\n" +
                "UserQuestion q = new UserQuestion(\"Click a 3D point\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskPoint3D(q, default(SmartPoint3D), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "return \"Point: (\" + (selected.X * 1000).ToString(\"F2\") + \", \" + (selected.Y * 1000).ToString(\"F2\") + \", \" + (selected.Z * 1000).ToString(\"F2\") + \") mm\";") },

            { "list_positionings", R("ASSEMBLIES", "Lists positioning operations of the active assembly with their constraint status",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "int count = 0;\n" +
                "int underCount = 0;\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        bool under = TopSolidDesignHost.Assemblies.IsPositioningUnderconstrained(op);\n" +
                "        string name = TopSolidHost.Elements.GetFriendlyName(op);\n" +
                "        if (under) underCount++;\n" +
                "        sb.AppendLine(\"  \" + name + \" -> \" + (under ? \"UNDERCONSTRAINED\" : \"fully constrained\"));\n" +
                "        count++;\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "if (count == 0) return \"No positioning found (is the active document an assembly?).\";\n" +
                "sb.Insert(0, \"Positionings: \" + count + \" (\" + underCount + \" underconstrained)\" + System.Environment.NewLine);\n" +
                "return sb.ToString();") },
            { "is_positioning_underconstrained", R("ASSEMBLIES", "Tells if a positioning is underconstrained. Param: value=positioning_name (exact or substring)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "string target = \"{value}\".Trim();\n" +
                "if (target.Length == 0) return \"Param required: value=positioning_name (use list_positionings first).\";\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    try\n" +
                "    {\n" +
                "        bool under = TopSolidDesignHost.Assemblies.IsPositioningUnderconstrained(op);\n" +
                "        string name = TopSolidHost.Elements.GetFriendlyName(op);\n" +
                "        if (name.IndexOf(target, System.StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "            return \"Positioning '\" + name + \"' is \" + (under ? \"UNDERCONSTRAINED\" : \"fully constrained\");\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "return \"Positioning '\" + target + \"' not found (use list_positionings).\";") },
            { "get_material_brinell_hardness_factor", R("MATERIALS", "Get the Brinell hardness factor of the active material (from its standards file). Reports (also) error when no material is set",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Materials.GetBrinellHardnessFactor(docId);\n" +
                "    return \"GetBrinellHardnessFactor of the active material: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no material set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_material_vickers_hardness_factor", R("MATERIALS", "Get the Vickers hardness factor of the active material (from its standards file). Reports (also) error when no material is set",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Materials.GetVickersHardnessFactor(docId);\n" +
                "    return \"GetVickersHardnessFactor of the active material: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no material set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_material_glossiness_factor", R("MATERIALS", "Get the glossiness factor of the active material. Reports (also) error when no material is set",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Materials.GetGlossinessFactor(docId);\n" +
                "    return \"GetGlossinessFactor of the active material: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no material set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_material_bump_scale", R("MATERIALS", "Get the bump texture scale factor of the active material. Reports (also) error when no material is set",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Materials.GetBumpScale(docId);\n" +
                "    return \"GetBumpScale of the active material: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no material set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_material_bump_rotation", R("MATERIALS", "Get the bump texture rotation of the active material (radians). Reports (also) error when no material is set",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Materials.GetBumpRotation(docId);\n" +
                "    return \"GetBumpRotation of the active material: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no material set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_material_anisotropy_angle_factor", R("MATERIALS", "Get the anisotropy angle factor of the active material. Reports (also) error when no material is set",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Materials.GetAnisotropyAngleFactor(docId);\n" +
                "    return \"GetAnisotropyAngleFactor of the active material: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no material set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_coating_brinell_hardness_factor", R("MATERIALS", "Get the Brinell hardness factor of the active coating",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Coatings.GetBrinellHardnessFactor(docId);\n" +
                "    return \"GetBrinellHardnessFactor of the active coating: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_coating_vickers_hardness_factor", R("MATERIALS", "Get the Vickers hardness factor of the active coating",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Coatings.GetVickersHardnessFactor(docId);\n" +
                "    return \"GetVickersHardnessFactor of the active coating: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_coating_glossiness_factor", R("MATERIALS", "Get the glossiness factor of the active coating",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Coatings.GetGlossinessFactor(docId);\n" +
                "    return \"GetGlossinessFactor of the active coating: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_coating_bump_scale", R("MATERIALS", "Get the bump texture scale factor of the active coating",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Coatings.GetBumpScale(docId);\n" +
                "    return \"GetBumpScale of the active coating: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_coating_bump_rotation", R("MATERIALS", "Get the bump texture rotation of the active coating (radians)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Coatings.GetBumpRotation(docId);\n" +
                "    return \"GetBumpRotation of the active coating: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_coating_anisotropy_angle_factor", R("MATERIALS", "Get the anisotropy angle factor of the active coating",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    double factor = TopSolidDesignHost.Coatings.GetAnisotropyAngleFactor(docId);\n" +
                "    return \"GetAnisotropyAngleFactor of the active coating: \" + factor.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "get_profiles_folder", R("GEOMETRY", "Gets the profiles folder entity of the active document (name + id)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "try\n" +
                "{\n" +
                "    ElementId folder = TopSolidHost.Geometries3D.GetProfilesFolder(docId);\n" +
                "    if (folder.IsEmpty) return \"No profiles folder (this document type has none).\";\n" +
                "    string name = \"?\";\n" +
                "    try { name = TopSolidHost.Elements.GetFriendlyName(folder); } catch {}\n" +
                "    return \"Profiles folder: \" + name + \" [\" + folder.ToString() + \"]\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error: \" + ex.Message;\n" +
                "}") },
            { "get_operation_version", R("GEOMETRY", "Gets the algorithm version of an operation. Param: value=operation_name (use list_operations to find it)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "string target = \"{value}\".Trim();\n" +
                "if (target.Length == 0) return \"Param required: value=operation_name (use list_operations to find operations).\";\n" +
                "var ops = TopSolidHost.Operations.GetOperations(docId);\n" +
                "foreach (var op in ops)\n" +
                "{\n" +
                "    string name = \"?\";\n" +
                "    try { name = TopSolidHost.Elements.GetFriendlyName(op); } catch { continue; }\n" +
                "    if (name.IndexOf(target, System.StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        try\n" +
                "        {\n" +
                "            string version = TopSolidHost.Operations.GetOperationVersion(op);\n" +
                "            return \"Operation '\" + name + \"' algorithm version: \" + (string.IsNullOrEmpty(version) ? \"(unknown)\" : version);\n" +
                "        }\n" +
                "        catch (Exception ex)\n" +
                "        {\n" +
                "            return \"Error: \" + ex.Message;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "return \"Operation '\" + target + \"' not found (use list_operations).\";") },
            { "get_edge_faces", R("GEOMETRY", "Lists the faces bordering a selected edge (count + names)",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select an edge\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskEdge(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try\n" +
                "{\n" +
                "    var faces = TopSolidHost.Shapes.GetEdgeFaces(selected);\n" +
                "    var sb = new System.Text.StringBuilder();\n" +
                "    sb.AppendLine(\"Faces bordering the edge: \" + faces.Count);\n" +
                "    int i = 0;\n" +
                "    foreach (var f in faces)\n" +
                "    {\n" +
                "        i++;\n" +
                "        string fInfo = \"?\";\n" +
                "        try\n" +
                "        {\n" +
                "            double area = TopSolidHost.Shapes.GetFaceArea(f);\n" +
                "            fInfo = \"area=\" + (area * 1e6).ToString(\"F1\") + \" cm2\";\n" +
                "        }\n" +
                "        catch {}\n" +
                "        sb.AppendLine(\"  face \" + i + \": \" + fInfo);\n" +
                "    }\n" +
                "    return sb.ToString();\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error: \" + ex.Message;\n" +
                "}") },
            { "get_edge_ellipse_curve", R("GEOMETRY", "Gets the ellipse (plane + radii, mm) of a selected elliptical edge",
                "ElementItemId selected = default(ElementItemId);\n" +
                "UserQuestion q = new UserQuestion(\"Select an elliptical edge\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskEdge(q, default(ElementItemId), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "try\n" +
                "{\n" +
                "    CurveType ct = TopSolidHost.Shapes.GetEdgeCurveType(selected);\n" +
                "    if ((int)ct == 3)\n" +
                "    {\n" +
                "        Plane3D plane;\n" +
                "        double radiusX, radiusY;\n" +
                "        TopSolidHost.Shapes.GetEdgeEllipseCurve(selected, out plane, out radiusX, out radiusY);\n" +
                "        Point3D o = plane.Origin;\n" +
                "        Direction3D n = plane.Normal;\n" +
                "        var sb = new System.Text.StringBuilder();\n" +
                "        sb.AppendLine(\"Ellipse edge:\");\n" +
                "        sb.AppendLine(\"  RadiusX: \" + (radiusX * 1000).ToString(\"F3\") + \" mm\");\n" +
                "        sb.AppendLine(\"  RadiusY: \" + (radiusY * 1000).ToString(\"F3\") + \" mm\");\n" +
                "        sb.AppendLine(\"  Center: (\" + (o.X * 1000).ToString(\"F2\") + \", \" + (o.Y * 1000).ToString(\"F2\") + \", \" + (o.Z * 1000).ToString(\"F2\") + \") mm\");\n" +
                "        sb.AppendLine(\"  Normal: (\" + n.X.ToString(\"F3\") + \", \" + n.Y.ToString(\"F3\") + \", \" + n.Z.ToString(\"F3\") + \")\");\n" +
                "        return sb.ToString();\n" +
                "    }\n" +
                "    return \"The selected edge is not elliptical (curve type: \" + ct + \"). Select an ellipse edge.\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    return \"Error (is the selection an elliptical edge?): \" + ex.Message;\n" +
                "}") },
            { "smooth_sketch_profiles", RW("SKETCH", "Smooths the profiles of the last 2D sketch (G1 continuity). WARNING: modifies the sketch. Param: value=empty (defaults) or tolerance_mm",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "var sketches = TopSolidHost.Sketches2D.GetSketches(docId);\n" +
                "if (sketches.Count == 0) { __message = \"No 2D sketch in this document.\"; return; }\n" +
                "ElementId sketchId = sketches[sketches.Count - 1];\n" +
                "string skName = \"?\";\n" +
                "try { skName = TopSolidHost.Elements.GetFriendlyName(sketchId); } catch {}\n" +
                "string tolText = \"{value}\".Trim();\n" +
                "double linearTolerance = 1e-4;\n" +
                "if (tolText.Length > 0)\n" +
                "{\n" +
                "    double parsed;\n" +
                "    if (double.TryParse(tolText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed > 0)\n" +
                "        linearTolerance = parsed / 1000.0;\n" +
                "}\n" +
                "var profiles = TopSolidHost.Sketches2D.GetProfiles(sketchId);\n" +
                "if (profiles == null || profiles.Count == 0) { __message = \"No profile found in sketch '\" + skName + \"'.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Sketches2D.StartModification(sketchId);\n" +
                "    SmartReal zeroInfluence = new SmartReal(UnitType.Length, 0.0);\n" +
                "    SmartReal linTol = new SmartReal(UnitType.Length, linearTolerance);\n" +
                "    SmartReal angTol = new SmartReal(UnitType.Angle, 0.05);\n" +
                "    SmartInteger nbPts = new SmartInteger(0);\n" +
                "    TopSolidHost.Sketches2D.SmoothSketchProfiles(sketchId, profiles, SmoothingType.LocalWithPunctualInfluence, zeroInfluence, linTol, angTol, nbPts, true);\n" +
                "    TopSolidHost.Sketches2D.EndModification();\n" +
                "    __message = \"OK: profiles of sketch '\" + skName + \"' smoothed (linear tolerance=\" + (linearTolerance * 1000).ToString(\"F3\") + \" mm)\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    try { TopSolidHost.Sketches2D.EndModification(); } catch {}\n" +
                "    __message = \"Error (sketch already modified?): \" + ex.Message;\n" +
                "}") },

            { "attr_read_face_colors", R("ATTRIBUTES", "Reads individual face colors",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "if (shapes.Count == 0) return \"No shape.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string sName = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    var faces = TopSolidHost.Shapes.GetFaces(s);\n" +
                "    sb.AppendLine(sName + \" (\" + faces.Count + \" faces):\");\n" +
                "    foreach (var f in faces)\n" +
                "    {\n" +
                "        Color c = TopSolidHost.Shapes.GetFaceColor(f);\n" +
                "        sb.AppendLine(\"  R=\" + c.R + \" G=\" + c.G + \" B=\" + c.B);\n" +
                "    }\n" +
                "}\n" +
                "return sb.ToString();") },
            { "set_angular_scale", RW("MATERIALS", "Sets the angular scale factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetAngularScale(docId, value);\n" +
                "    __message = \"OK: angular scale factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_anisotropy_angle_factor", RW("MATERIALS", "Sets the anisotropy angle factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetAnisotropyAngleFactor(docId, value);\n" +
                "    __message = \"OK: anisotropy angle factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_brinell_hardness_factor", RW("MATERIALS", "Sets the Brinell hardness factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetBrinellHardnessFactor(docId, value);\n" +
                "    __message = \"OK: Brinell hardness factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_bulk_modulus", RW("MATERIALS", "Sets the bulk modulus of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetBulkModulus(docId, value);\n" +
                "    __message = \"OK: bulk modulus set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_bump_rotation", RW("MATERIALS", "Sets the bump texture rotation angle of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetBumpRotation(docId, value);\n" +
                "    __message = \"OK: bump texture rotation angle set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_bump_scale", RW("MATERIALS", "Sets the bump texture scale factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetBumpScale(docId, value);\n" +
                "    __message = \"OK: bump texture scale factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_cutting_force", RW("MATERIALS", "Sets the cutting force of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetCuttingForce(docId, value);\n" +
                "    __message = \"OK: cutting force set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_cutting_force_increment", RW("MATERIALS", "Sets the cutting force increment of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetCuttingForceIncrement(docId, value);\n" +
                "    __message = \"OK: cutting force increment set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_elastic_limit", RW("MATERIALS", "Sets the elastic limit of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetElasticLimit(docId, value);\n" +
                "    __message = \"OK: elastic limit set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_elongation_at_break", RW("MATERIALS", "Sets the elongation at break of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetElongationAtBreak(docId, value);\n" +
                "    __message = \"OK: elongation at break set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_glossiness_factor", RW("MATERIALS", "Sets the glossiness factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetGlossinessFactor(docId, value);\n" +
                "    __message = \"OK: glossiness factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_melting_temperature", RW("MATERIALS", "Sets the melting temperature of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetMeltingTemperature(docId, value);\n" +
                "    __message = \"OK: melting temperature set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_plastic_strain_ratio", RW("MATERIALS", "Sets the plastic strain ratio of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetPlasticStrainRatio(docId, value);\n" +
                "    __message = \"OK: plastic strain ratio set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_poisson_ratio", RW("MATERIALS", "Sets the Poisson ratio of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetPoissonRatio(docId, value);\n" +
                "    __message = \"OK: Poisson ratio set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_section_reduction_at_break", RW("MATERIALS", "Sets the section reduction at break of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetSectionReductionAtBreak(docId, value);\n" +
                "    __message = \"OK: section reduction at break set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_shear_modulus", RW("MATERIALS", "Sets the shear modulus of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetShearModulus(docId, value);\n" +
                "    __message = \"OK: shear modulus set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_shrinkage_factor", RW("MATERIALS", "Sets the shrinkage factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetShrinkageFactor(docId, value);\n" +
                "    __message = \"OK: shrinkage factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_specific_heat_capacity", RW("MATERIALS", "Sets the specific heat capacity of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetSpecificHeatCapacity(docId, value);\n" +
                "    __message = \"OK: specific heat capacity set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_thermal_conductivity", RW("MATERIALS", "Sets the thermal conductivity of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetThermalConductivity(docId, value);\n" +
                "    __message = \"OK: thermal conductivity set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_thermal_expansion_coefficient", RW("MATERIALS", "Sets the thermal expansion coefficient of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetThermalExpansionCoefficient(docId, value);\n" +
                "    __message = \"OK: thermal expansion coefficient set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_ultimate_stress", RW("MATERIALS", "Sets the ultimate stress of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetUltimateStress(docId, value);\n" +
                "    __message = \"OK: ultimate stress set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_vertical_angular_scale", RW("MATERIALS", "Sets the vertical angular scale factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetVerticalAngularScale(docId, value);\n" +
                "    __message = \"OK: vertical angular scale factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_vickers_hardness_factor", RW("MATERIALS", "Sets the Vickers hardness factor of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetVickersHardnessFactor(docId, value);\n" +
                "    __message = \"OK: Vickers hardness factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_young_modulus", RW("MATERIALS", "Sets the Young modulus of the material applied to the edited document. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Materials.SetYoungModulus(docId, value);\n" +
                "    __message = \"OK: Young modulus set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_homogeneous_parameter_value", RW("MATERIALS", "Sets the homogeneous parameter value (bool) of the material. Param: value=empty(unset)/true/false",
                "string raw = string.IsNullOrWhiteSpace(\"{value}\") ? null : \"{value}\".Trim().ToLowerInvariant();\n" +
                "bool parsed = false;\n" +
                "if (raw != null) { bool.TryParse(raw, out parsed); }\n" +
                "if (raw != null && !parsed && raw != \"true\" && raw != \"false\")\n" +
                "{\n" +
                "    __message = \"Invalid value: use empty (unset) or true/false.\"; return;\n" +
                "}\n" +
                "try\n" +
                "{\n" +
                "    bool result = TopSolidDesignHost.Materials.SetHomogeneousParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);\n" +
                "    __message = \"OK: homogeneous parameter value (bool) of the material set to \" + raw + \" (API result=\" + result + \")\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_isotropic_parameter_value", RW("MATERIALS", "Sets the isotropic parameter value (bool) of the material. Param: value=empty(unset)/true/false",
                "string raw = string.IsNullOrWhiteSpace(\"{value}\") ? null : \"{value}\".Trim().ToLowerInvariant();\n" +
                "bool parsed = false;\n" +
                "if (raw != null) { bool.TryParse(raw, out parsed); }\n" +
                "if (raw != null && !parsed && raw != \"true\" && raw != \"false\")\n" +
                "{\n" +
                "    __message = \"Invalid value: use empty (unset) or true/false.\"; return;\n" +
                "}\n" +
                "try\n" +
                "{\n" +
                "    bool result = TopSolidDesignHost.Materials.SetIsotropicParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);\n" +
                "    __message = \"OK: isotropic parameter value (bool) of the material set to \" + raw + \" (API result=\" + result + \")\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_rockwell_factors", RW("MATERIALS", "Sets the Rockwell factors (A,B,C) of the material applied to the edited document. Param: value=A,B,C (double or 'null')",
                "    string raw = \"{value}\".Trim();\n" +
                "    string[] parts = raw.Split(',');\n" +
                "    if (parts.Length != 3)\n" +
                "    {\n" +
                "        __message = \"Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset).\"; return;\n" +
                "    }\n" +
                "    double?[] vals = new double?[3];\n" +
                "    for (int i = 0; i < 3; i++)\n" +
                "    {\n" +
                "        string p = parts[i].Trim();\n" +
                "        if (p.ToLowerInvariant() == \"null\") { vals[i] = null; continue; }\n" +
                "        double d;\n" +
                "        if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))\n" +
                "        {\n" +
                "            __message = \"Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset).\"; return;\n" +
                "        }\n" +
                "        vals[i] = d;\n" +
                "    }\n" +
                "try\n" +
                "{\n" +
                "    TopSolidDesignHost.Materials.SetRockwellFactors(docId, vals[0], vals[1], vals[2]);\n" +
                "    __message = \"OK: Rockwell factors set to A=\" + vals[0] + \" B=\" + vals[1] + \" C=\" + vals[2];\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_specular_color", RW("MATERIALS", "Sets the specular color (R,G,B) of the material applied to the edited document. Param: value=R,G,B (0-255)",
                "    string raw = \"{value}\".Trim();\n" +
                "    string[] parts = raw.Split(',');\n" +
                "    if (parts.Length != 3)\n" +
                "    {\n" +
                "        __message = \"Invalid value: use R,G,B (0-255).\"; return;\n" +
                "    }\n" +
                "    int r, g, b;\n" +
                "    if (!int.TryParse(parts[0].Trim(), out r) || !int.TryParse(parts[1].Trim(), out g) || !int.TryParse(parts[2].Trim(), out b) || r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)\n" +
                "    {\n" +
                "        __message = \"Invalid value: use R,G,B (0-255).\"; return;\n" +
                "    }\n" +
                "try\n" +
                "{\n" +
                "    TopSolidDesignHost.Materials.SetSpecularColor(docId, new Color((byte)r, (byte)g, (byte)b));\n" +
                "    __message = \"OK: specular color set to R=\" + r + \" G=\" + g + \" B=\" + b;\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no material applied to this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_brinell_hardness_factor", RW("MATERIALS", "Sets the Brinell hardness factor of the active coating. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Coatings.SetBrinellHardnessFactor(docId, value);\n" +
                "    __message = \"OK: Brinell hardness factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_bump_rotation", RW("MATERIALS", "Sets the bump texture rotation angle of the active coating. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Coatings.SetBumpRotation(docId, value);\n" +
                "    __message = \"OK: bump texture rotation angle set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_bump_scale", RW("MATERIALS", "Sets the bump texture scale factor of the active coating. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Coatings.SetBumpScale(docId, value);\n" +
                "    __message = \"OK: bump texture scale factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_vickers_hardness_factor", RW("MATERIALS", "Sets the Vickers hardness factor of the active coating. Param: value=<number>",
                "try\n" +
                "{\n" +
                "    double value = double.Parse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    TopSolidDesignHost.Coatings.SetVickersHardnessFactor(docId, value);\n" +
                "    __message = \"OK: Vickers hardness factor set to \" + value.ToString(\"F4\");\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_homogeneous_parameter_value", RW("MATERIALS", "Sets the homogeneous parameter value (bool) of the active coating. Param: value=empty(unset)/true/false",
                "string raw = string.IsNullOrWhiteSpace(\"{value}\") ? null : \"{value}\".Trim().ToLowerInvariant();\n" +
                "bool parsed = false;\n" +
                "if (raw != null) { bool.TryParse(raw, out parsed); }\n" +
                "if (raw != null && !parsed && raw != \"true\" && raw != \"false\")\n" +
                "{\n" +
                "    __message = \"Invalid value: use empty (unset) or true/false.\"; return;\n" +
                "}\n" +
                "try\n" +
                "{\n" +
                "    bool result = TopSolidDesignHost.Coatings.SetHomogeneousParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);\n" +
                "    __message = \"OK: homogeneous parameter value (bool) set to \" + raw + \" (API result=\" + result + \")\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_isotropic_parameter_value", RW("MATERIALS", "Sets the isotropic parameter value (bool) of the active coating. Param: value=empty(unset)/true/false",
                "string raw = string.IsNullOrWhiteSpace(\"{value}\") ? null : \"{value}\".Trim().ToLowerInvariant();\n" +
                "bool parsed = false;\n" +
                "if (raw != null) { bool.TryParse(raw, out parsed); }\n" +
                "if (raw != null && !parsed && raw != \"true\" && raw != \"false\")\n" +
                "{\n" +
                "    __message = \"Invalid value: use empty (unset) or true/false.\"; return;\n" +
                "}\n" +
                "try\n" +
                "{\n" +
                "    bool result = TopSolidDesignHost.Coatings.SetIsotropicParameterValue(docId, raw == null ? (bool?)null : (bool?)parsed);\n" +
                "    __message = \"OK: isotropic parameter value (bool) set to \" + raw + \" (API result=\" + result + \")\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "set_coating_rockwell_factors", RW("MATERIALS", "Sets the Rockwell factors (A,B,C) of the active coating. Param: value=A,B,C (double or 'null')",
                "    string raw = \"{value}\".Trim();\n" +
                "    string[] parts = raw.Split(',');\n" +
                "    if (parts.Length != 3)\n" +
                "    {\n" +
                "        __message = \"Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset).\"; return;\n" +
                "    }\n" +
                "    double?[] vals = new double?[3];\n" +
                "    for (int i = 0; i < 3; i++)\n" +
                "    {\n" +
                "        string p = parts[i].Trim();\n" +
                "        if (p.ToLowerInvariant() == \"null\") { vals[i] = null; continue; }\n" +
                "        double d;\n" +
                "        if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))\n" +
                "        {\n" +
                "            __message = \"Invalid value: use A,B,C (e.g. 0.5,0.2,0.3 - or 'null' to leave a factor unset).\"; return;\n" +
                "        }\n" +
                "        vals[i] = d;\n" +
                "    }\n" +
                "try\n" +
                "{\n" +
                "    TopSolidDesignHost.Coatings.SetRockwellFactors(docId, vals[0], vals[1], vals[2]);\n" +
                "    __message = \"OK: Rockwell factors set to A=\" + vals[0] + \" B=\" + vals[1] + \" C=\" + vals[2];\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error (no coating set on this document?): \" + ex.Message;\n" +
                "}") },
            { "add_page_to_bundle", RW("DRAFTING", "Adds a new page to the opened drafting bundle. No value expected.",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    TopSolidDraftingHost.Draftings.AddPageToBundle(docId);\n" +
                "    int pageCount = TopSolidDraftingHost.Draftings.GetPageCount(docId);\n" +
                "    __message = \"OK: new page added, bundle now has \" + pageCount + \" pages.\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "set_current_bundle_page", RW("DRAFTING", "Sets the current page of the opened drafting bundle. Param: value=page_number (e.g. 1)",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    int page = int.Parse(\"{value}\".Trim(), System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    if (page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))\n" +
                "    {\n" +
                "        __message = \"Page number out of range (1 to \" + TopSolidDraftingHost.Draftings.GetPageCount(docId) + \"): \" + page; return;\n" +
                "    }\n" +
                "    TopSolidDraftingHost.Draftings.SetCurrentBundlePage(docId, page);\n" +
                "    __message = \"OK: current bundle page set to \" + page;\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "set_bundle_page_format_name", RW("DRAFTING", "Sets the format name of a bundle page to 'A3'. Param: value=page_number (e.g. 1)",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    int page = int.Parse(\"{value}\".Trim(), System.Globalization.CultureInfo.InvariantCulture);\n" +
                "    if (page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))\n" +
                "    {\n" +
                "        __message = \"Page number out of range (1 to \" + TopSolidDraftingHost.Draftings.GetPageCount(docId) + \"): \" + page; return;\n" +
                "    }\n" +
                "    TopSolidDraftingHost.Draftings.SetBundlePageFormatName(docId, page, \"A3\");\n" +
                "    __message = \"OK: format name A3 set on page \" + page;\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "set_bundle_page_format", RW("DRAFTING", "Sets the format of a bundle page. Param: value=page;format_name (e.g. 1;A3 landscape)",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    string raw = \"{value}\".Trim();\n" +
                "    int sep = raw.IndexOf(';');\n" +
                "    int page; string fmt;\n" +
                "    if (sep <= 0 || sep >= raw.Length - 1 || !int.TryParse(raw.Substring(0, sep).Trim(), out page) || (fmt = raw.Substring(sep + 1).Trim()).Length == 0 || page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))\n" +
                "    {\n" +
                "        __message = \"Use value=page;format (e.g. 1;A3 landscape).\"; return;\n" +
                "    }\n" +
                "    TopSolidDraftingHost.Draftings.SetBundlePageFormat(docId, page, fmt);\n" +
                "    __message = \"OK: page \" + page + \" format set to '\" + fmt + \"'.\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "set_bundle_page_dimensions", RW("DRAFTING", "Sets the dimensions of a bundle page. Param: value=page;width;height (mm, e.g. 1;420;297)",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    string raw = \"{value}\".Trim();\n" +
                "    string[] parts = raw.Split(';');\n" +
                "    double w, h; int page;\n" +
                "    if (parts.Length != 3 || !int.TryParse(parts[0].Trim(), out page) || !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w) || !double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out h) || page <= 0 || page > TopSolidDraftingHost.Draftings.GetPageCount(docId))\n" +
                "    {\n" +
                "        __message = \"Use value=page;width;height (mm, e.g. 1;420;297).\"; return;\n" +
                "    }\n" +
                "    TopSolidDraftingHost.Draftings.SetBundlePageDimensions(docId, page, new Real(UnitType.Length, \"mm\", w), new Real(UnitType.Length, \"mm\", h));\n" +
                "    __message = \"OK: page \" + page + \" dimensions set to \" + w + \" x \" + h + \" mm.\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "include_draft_in_bundle", RW("DRAFTING", "Includes a draft (all pages on the same bundle page) in the opened bundle. Param: value=draft_doc_name",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "    if (results.Count == 0)\n" +
                "    {\n" +
                "        __message = \"Draft document not found: \" + \"{value}\"; return;\n" +
                "    }\n" +
                "    DocumentId draftDoc = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "    TopSolidDraftingHost.Draftings.IncludeDraftInBundle(docId, draftDoc, true);\n" +
                "    __message = \"OK: draft '\" + \"{value}\" + \"' included in the bundle (all pages on the same bundle page).\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "include_draft_page_in_bundle_current_page", RW("DRAFTING", "Includes a specific draft page in the current bundle page with offset. Param: value=draft_doc_name;page;offset (e.g. draft;1;0)",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    string raw = \"{value}\".Trim();\n" +
                "    string[] parts = raw.Split(';');\n" +
                "    int page; double off;\n" +
                "    if (parts.Length != 3 || !int.TryParse(parts[1].Trim(), out page) || !double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out off))\n" +
                "    {\n" +
                "        __message = \"Use value=draft_doc_name;page;offset (e.g. draft;1;0).\"; return;\n" +
                "    }\n" +
                "    PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    var results = TopSolidHost.Pdm.SearchDocumentByName(projId, parts[0].Trim());\n" +
                "    if (results.Count == 0)\n" +
                "    {\n" +
                "        __message = \"Draft document not found: \" + parts[0].Trim(); return;\n" +
                "    }\n" +
                "    DocumentId draftDoc = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "    TopSolidDraftingHost.Draftings.IncludeDraftPageInBundleCurrentPage(docId, draftDoc, page, off, off);\n" +
                "    __message = \"OK: page \" + page + \" of draft included at offset \" + off + \".\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "add_draft_to_bundle", RW("DRAFTING", "Adds a draft to the opened bundle (its pages become separate bundle pages). Param: value=draft_doc_name",
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle. Open the bundle first.\"; return;\n" +
                "    }\n" +
                "    PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "    if (results.Count == 0)\n" +
                "    {\n" +
                "        __message = \"Draft document not found: \" + \"{value}\"; return;\n" +
                "    }\n" +
                "    DocumentId draftDoc = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "    TopSolidDraftingHost.Draftings.AddDraftToBundle(docId, draftDoc);\n" +
                "    __message = \"OK: draft '\" + \"{value}\" + \"' added (its pages are now bundle pages).\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },
            { "get_page_count", R("DRAFTING", "Returns the number of pages of the opened drafting bundle.",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) { __message = \"No document open. Open the bundle in TopSolid first.\"; return; }\n" +
                "try\n" +
                "{\n" +
                "    if (!TopSolidDraftingHost.Draftings.IsDraftingBundle(docId))\n" +
                "    {\n" +
                "        __message = \"The edited document is not a drafting bundle.\"; return;\n" +
                "    }\n" +
                "    __message = \"OK: \" + TopSolidDraftingHost.Draftings.GetPageCount(docId) + \" page(s).\";\n" +
                "}\n" +
                "catch (Exception ex)\n" +
                "{\n" +
                "    __message = \"Error: \" + ex.Message;\n" +
                "}") },

            // =====================================================================
            // METALWORK, CAD CREATION & ASSEMBLY RECIPES (V4)
            // =====================================================================
            { "create_project_tree", RW("PROJECTS", "Creates or reuses a PDM project and optional comma-separated folders, and sets it as current project. Param: value=project_name[;folder1,folder2,...]",
                "if (false) { TopSolidHost.Application.StartModification(\"\", false); TopSolidHost.Application.EndModification(false, false); }\n" +
                "string raw = \"{value}\".Trim();\n" +
                "if (string.IsNullOrEmpty(raw)) return \"Error: Use value=project_name[;folder1,folder2,...]\";\n" +
                "string[] parts = raw.Split(';');\n" +
                "string projName = parts[0].Trim();\n" +
                "PdmObjectId projPdm = PdmObjectId.Empty;\n" +
                "foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))\n" +
                "{\n" +
                "    if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }\n" +
                "}\n" +
                "bool createdProj = false;\n" +
                "if (projPdm.IsEmpty)\n" +
                "{\n" +
                "    projPdm = TopSolidHost.Pdm.CreateProject(projName, false);\n" +
                "    TopSolidHost.Pdm.Save(projPdm, true);\n" +
                "    createdProj = true;\n" +
                "}\n" +
                "int createdFolders = 0;\n" +
                "if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1].Trim()))\n" +
                "{\n" +
                "    List<PdmObjectId> existFolders; List<PdmObjectId> existDocs;\n" +
                "    TopSolidHost.Pdm.GetConstituents(projPdm, out existFolders, out existDocs);\n" +
                "    foreach (string fRaw in parts[1].Split(','))\n" +
                "    {\n" +
                "        string fName = fRaw.Trim();\n" +
                "        if (string.IsNullOrEmpty(fName)) continue;\n" +
                "        bool exists = false;\n" +
                "        foreach (var ef in existFolders)\n" +
                "        {\n" +
                "            if (string.Equals(TopSolidHost.Pdm.GetName(ef), fName, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }\n" +
                "        }\n" +
                "        if (!exists) { TopSolidHost.Pdm.CreateFolder(projPdm, fName); createdFolders++; }\n" +
                "    }\n" +
                "    TopSolidHost.Pdm.Save(projPdm, true);\n" +
                "}\n" +
                "return \"OK: Project '\" + projName + \"' (\" + (createdProj ? \"created\" : \"reused\") + \"), folders created: \" + createdFolders + \".\";") },

            { "create_part_document", RW("DOCUMENT", "Creates or reuses a Part document (.TopPrt) in a project/folder, sets Name/Designation/Reference, and opens it. Param: value=doc_name[;designation[;reference[;folder_name[;project_name]]]]",
                "string raw = \"{value}\".Trim();\n" +
                "if (string.IsNullOrEmpty(raw)) return \"Error: Use value=doc_name[;designation[;reference[;folder_name[;project_name]]]]\";\n" +
                "string[] parts = raw.Split(';');\n" +
                "string docName = parts[0].Trim();\n" +
                "string desig = parts.Length > 1 ? parts[1].Trim() : \"\";\n" +
                "string refer = parts.Length > 2 ? parts[2].Trim() : \"\";\n" +
                "string folderName = parts.Length > 3 ? parts[3].Trim() : \"\";\n" +
                "string projName = parts.Length > 4 ? parts[4].Trim() : \"\";\n" +
                "PdmObjectId projPdm = PdmObjectId.Empty;\n" +
                "if (!string.IsNullOrEmpty(projName))\n" +
                "{\n" +
                "    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }\n" +
                "}\n" +
                "if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projPdm.IsEmpty) return \"Error: No target project found.\";\n" +
                "PdmObjectId ownerPdm = projPdm;\n" +
                "if (!string.IsNullOrEmpty(folderName))\n" +
                "{\n" +
                "    List<PdmObjectId> fList; List<PdmObjectId> dList;\n" +
                "    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);\n" +
                "    foreach (var f in fList)\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }\n" +
                "    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);\n" +
                "}\n" +
                "List<PdmObjectId> subF; List<PdmObjectId> subD;\n" +
                "TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);\n" +
                "PdmObjectId docPdm = PdmObjectId.Empty;\n" +
                "foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }\n" +
                "if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, \".TopPrt\", true);\n" +
                "TopSolidHost.Pdm.SetName(docPdm, docName);\n" +
                "if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);\n" +
                "if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);\n" +
                "DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);\n" +
                "TopSolidHost.Documents.Open(ref dId);\n" +
                "if (false) { TopSolidHost.Application.StartModification(\"\", false); TopSolidHost.Application.EndModification(false, false); }\n" +
                "TopSolidHost.Pdm.Save(docPdm, true);\n" +
                "return \"OK: Part document '\" + docName + \"' (.TopPrt) ready and saved.\";") },

            { "create_assembly_document", RW("DOCUMENT", "Creates or reuses an Assembly document (.TopAsm) in a project/folder, sets Name/Designation/Reference, and opens it. Param: value=doc_name[;designation[;reference[;folder_name[;project_name]]]]",
                "string raw = \"{value}\".Trim();\n" +
                "if (string.IsNullOrEmpty(raw)) return \"Error: Use value=doc_name[;designation[;reference[;folder_name[;project_name]]]]\";\n" +
                "string[] parts = raw.Split(';');\n" +
                "string docName = parts[0].Trim();\n" +
                "string desig = parts.Length > 1 ? parts[1].Trim() : \"\";\n" +
                "string refer = parts.Length > 2 ? parts[2].Trim() : \"\";\n" +
                "string folderName = parts.Length > 3 ? parts[3].Trim() : \"\";\n" +
                "string projName = parts.Length > 4 ? parts[4].Trim() : \"\";\n" +
                "PdmObjectId projPdm = PdmObjectId.Empty;\n" +
                "if (!string.IsNullOrEmpty(projName))\n" +
                "{\n" +
                "    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }\n" +
                "}\n" +
                "if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projPdm.IsEmpty) return \"Error: No target project found.\";\n" +
                "PdmObjectId ownerPdm = projPdm;\n" +
                "if (!string.IsNullOrEmpty(folderName))\n" +
                "{\n" +
                "    List<PdmObjectId> fList; List<PdmObjectId> dList;\n" +
                "    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);\n" +
                "    foreach (var f in fList)\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }\n" +
                "    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);\n" +
                "}\n" +
                "List<PdmObjectId> subF; List<PdmObjectId> subD;\n" +
                "TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);\n" +
                "PdmObjectId docPdm = PdmObjectId.Empty;\n" +
                "foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }\n" +
                "if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, \".TopAsm\", true);\n" +
                "TopSolidHost.Pdm.SetName(docPdm, docName);\n" +
                "if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);\n" +
                "if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);\n" +
                "DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);\n" +
                "TopSolidHost.Documents.Open(ref dId);\n" +
                "if (false) { TopSolidHost.Application.StartModification(\"\", false); TopSolidHost.Application.EndModification(false, false); }\n" +
                "TopSolidHost.Pdm.Save(docPdm, true);\n" +
                "return \"OK: Assembly document '\" + docName + \"' (.TopAsm) ready and saved.\";") },

            { "set_material", RW("MATERIALS", "Searches a standard material (.TopMat) in material libraries (<250ms) and applies it to a part while enabling physical properties (Mass, CoG). Param: value=[doc_name;]material_keyword (e.g. S235JR or GC_Montant_50x10;S235JR)",
                "string raw = \"{value}\".Trim();\n" +
                "if (string.IsNullOrEmpty(raw)) return \"Error: Provide material name or doc_name;material_name.\";\n" +
                "string targetDocName = \"\";\n" +
                "string matQuery = raw;\n" +
                "if (raw.Contains(\";\"))\n" +
                "{\n" +
                "    string[] sp = raw.Split(';');\n" +
                "    targetDocName = sp[0].Trim();\n" +
                "    matQuery = sp[1].Trim();\n" +
                "}\n" +
                "DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "PdmObjectId targetPdm = targetDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(targetDoc);\n" +
                "if (!string.IsNullOrEmpty(targetDocName))\n" +
                "{\n" +
                "    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetDocName);\n" +
                "    if (found.Count == 0) return \"Error: Document '\" + targetDocName + \"' not found in current project.\";\n" +
                "    targetPdm = found[0];\n" +
                "    targetDoc = TopSolidHost.Documents.GetDocument(targetPdm);\n" +
                "}\n" +
                "if (targetDoc.IsEmpty) return \"Error: No active document and no doc_name specified.\";\n" +
                "PdmObjectId matPdm = PdmObjectId.Empty;\n" +
                "string pattern = matQuery.Contains(\"*\") ? matQuery : (\"*\" + matQuery + \"*\");\n" +
                "var matLibs = new List<PdmObjectId>();\n" +
                "foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))\n" +
                "{\n" +
                "    string lName = TopSolidHost.Pdm.GetName(lib);\n" +
                "    if (string.Equals(lName, \"TopSolid MetalWork - Materials and Coatings\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Matériaux\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"Bois TopSolid\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Materials\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Wood\", StringComparison.OrdinalIgnoreCase))\n" +
                "        matLibs.Add(lib);\n" +
                "}\n" +
                "foreach (var lib in matLibs)\n" +
                "{\n" +
                "    foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, matQuery))\n" +
                "    { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (string.Equals(ext, \".TopMat\", StringComparison.OrdinalIgnoreCase)) { matPdm = h; break; } }\n" +
                "    if (!matPdm.IsEmpty) break;\n" +
                "}\n" +
                "if (matPdm.IsEmpty)\n" +
                "{\n" +
                "    foreach (var lib in matLibs)\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, pattern))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (string.Equals(ext, \".TopMat\", StringComparison.OrdinalIgnoreCase)) { matPdm = h; break; } }\n" +
                "        if (!matPdm.IsEmpty) break;\n" +
                "    }\n" +
                "}\n" +
                "if (matPdm.IsEmpty) return \"Error: Material matching '\" + matQuery + \"' (.TopMat) not found in standard libraries.\";\n" +
                "DocumentId matDoc = TopSolidHost.Documents.GetDocument(matPdm);\n" +
                "string matRealName = TopSolidHost.Pdm.GetName(matPdm);\n" +
                "TopSolidHost.Application.StartModification(\"SetMaterial_\" + matRealName, false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);\n" +
                "    TopSolidDesignHost.Parts.SetMaterial(targetDoc, matDoc);\n" +
                "    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(targetDoc, true, new SmartReal(UnitType.Mass, 0)); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(targetDoc, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(targetDoc, true); } catch { }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "if (!targetPdm.IsEmpty) TopSolidHost.Pdm.Save(targetPdm, true);\n" +
                "return \"OK: Material '\" + matRealName + \"' applied to '\" + TopSolidHost.Documents.GetName(targetDoc) + \"' (Mass & CoG enabled).\";") },

            { "set_coating", RW("MATERIALS", "Searches a standard coating (.TopCot, e.g. RAL 7016) in libraries and applies it (or RGB color) to all shapes of a part. Param: value=[doc_name;]coating_keyword[;R,G,B]",
                "string raw = \"{value}\".Trim();\n" +
                "if (string.IsNullOrEmpty(raw)) return \"Error: Provide coating name or doc_name;coating_name[;R,G,B].\";\n" +
                "string[] parts = raw.Split(';');\n" +
                "string targetDocName = \"\";\n" +
                "string cotQuery = \"\";\n" +
                "string rgbSpec = \"\";\n" +
                "if (parts.Length == 1) { cotQuery = parts[0].Trim(); }\n" +
                "else if (parts.Length == 2)\n" +
                "{\n" +
                "    if (parts[1].Contains(\",\")) { cotQuery = parts[0].Trim(); rgbSpec = parts[1].Trim(); }\n" +
                "    else { targetDocName = parts[0].Trim(); cotQuery = parts[1].Trim(); }\n" +
                "}\n" +
                "else { targetDocName = parts[0].Trim(); cotQuery = parts[1].Trim(); rgbSpec = parts[2].Trim(); }\n" +
                "DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "PdmObjectId targetPdm = targetDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(targetDoc);\n" +
                "if (!string.IsNullOrEmpty(targetDocName))\n" +
                "{\n" +
                "    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetDocName);\n" +
                "    if (found.Count == 0) return \"Error: Document '\" + targetDocName + \"' not found in current project.\";\n" +
                "    targetPdm = found[0];\n" +
                "    targetDoc = TopSolidHost.Documents.GetDocument(targetPdm);\n" +
                "}\n" +
                "if (targetDoc.IsEmpty) return \"Error: No active document.\";\n" +
                "PdmObjectId cotPdm = PdmObjectId.Empty;\n" +
                "if (!string.IsNullOrEmpty(cotQuery))\n" +
                "{\n" +
                "    string pattern = cotQuery.Contains(\"*\") ? cotQuery : (\"*\" + cotQuery + \"*\");\n" +
                "    var cotLibs = new List<PdmObjectId>();\n" +
                "    foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))\n" +
                "    {\n" +
                "        string lName = TopSolidHost.Pdm.GetName(lib);\n" +
                "        if (string.Equals(lName, \"TopSolid MetalWork - Materials and Coatings\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"RAL Classics, By TopSolidBlog\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Matériaux\", StringComparison.OrdinalIgnoreCase))\n" +
                "            cotLibs.Add(lib);\n" +
                "    }\n" +
                "    foreach (var lib in cotLibs)\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, cotQuery))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (string.Equals(ext, \".TopCot\", StringComparison.OrdinalIgnoreCase)) { cotPdm = h; break; } }\n" +
                "        if (!cotPdm.IsEmpty) break;\n" +
                "    }\n" +
                "    if (cotPdm.IsEmpty)\n" +
                "    {\n" +
                "        foreach (var lib in cotLibs)\n" +
                "        {\n" +
                "            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, pattern))\n" +
                "            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (string.Equals(ext, \".TopCot\", StringComparison.OrdinalIgnoreCase)) { cotPdm = h; break; } }\n" +
                "            if (!cotPdm.IsEmpty) break;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "DocumentId cotDoc = cotPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(cotPdm);\n" +
                "TopSolidHost.Application.StartModification(\"SetCoating\", false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);\n" +
                "    string status = \"\";\n" +
                "    if (!cotDoc.IsEmpty)\n" +
                "    {\n" +
                "        try\n" +
                "        {\n" +
                "            Color alb = TopSolidDesignHost.Coatings.GetAlbedoColor(cotDoc);\n" +
                "            foreach (var s in TopSolidHost.Shapes.GetShapes(targetDoc))\n" +
                "                TopSolidHost.Elements.SetColor(s, alb);\n" +
                "        } catch { }\n" +
                "        status = \"Coating '\" + TopSolidHost.Pdm.GetName(cotPdm) + \"'\";\n" +
                "    }\n" +
                "    if (!string.IsNullOrEmpty(rgbSpec))\n" +
                "    {\n" +
                "        string[] rgb = rgbSpec.Split(',');\n" +
                "        if (rgb.Length == 3)\n" +
                "        {\n" +
                "            byte r = byte.Parse(rgb[0].Trim()); byte g = byte.Parse(rgb[1].Trim()); byte b = byte.Parse(rgb[2].Trim());\n" +
                "            foreach (var s in TopSolidHost.Shapes.GetShapes(targetDoc))\n" +
                "                try { TopSolidHost.Elements.SetColor(s, new Color(r, g, b)); } catch { }\n" +
                "            status += (status.Length > 0 ? \" + \" : \"\") + \"RGB(\" + r + \",\" + g + \",\" + b + \")\";\n" +
                "        }\n" +
                "    }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "    if (!targetPdm.IsEmpty) TopSolidHost.Pdm.Save(targetPdm, true);\n" +
                "    return \"OK: \" + (status.Length > 0 ? status : \"No coating matched\") + \" on '\" + TopSolidHost.Documents.GetName(targetDoc) + \"'.\";\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }") },

            { "provide_extruded_bar_function", RW("GEOMETRY", "Provides the TopSolid 'Extruded Bar' (Profilé) function on a part with its 3D Frame (XY section, X horizontal, Z longitudinal), Section sketch, Length parameter L, BottomLeft/TopRight points, and Minimal EnclosingBox. Param: value=[doc_name;]width_x_mm;height_y_mm;length_z_mm",
                "string raw = \"{value}\".Trim();\n" +
                "string[] parts = raw.Split(';');\n" +
                "if (parts.Length < 3) return \"Error: Use value=[doc_name;]width_x_mm;height_y_mm;length_z_mm\";\n" +
                "int idx = parts.Length >= 4 ? 1 : 0;\n" +
                "string targetDocName = idx == 1 ? parts[0].Trim() : \"\";\n" +
                "double wx = double.Parse(parts[idx].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double hy = double.Parse(parts[idx + 1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double lz = double.Parse(parts[idx + 2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "PdmObjectId targetPdm = targetDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(targetDoc);\n" +
                "if (!string.IsNullOrEmpty(targetDocName))\n" +
                "{\n" +
                "    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetDocName);\n" +
                "    if (found.Count == 0) return \"Error: Document '\" + targetDocName + \"' not found.\";\n" +
                "    targetPdm = found[0];\n" +
                "    targetDoc = TopSolidHost.Documents.GetDocument(targetPdm);\n" +
                "}\n" +
                "PdmObjectId fncPdm = PdmObjectId.Empty;\n" +
                "foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))\n" +
                "{\n" +
                "    if (!string.Equals(TopSolidHost.Pdm.GetName(lib), \"TopSolid\", StringComparison.OrdinalIgnoreCase)) continue;\n" +
                "    var hits = TopSolidHost.Pdm.SearchDocumentByName(lib, \"Extruded Bar\");\n" +
                "    foreach (var h in hits) { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopPrtFct\") { fncPdm = h; break; } }\n" +
                "    if (!fncPdm.IsEmpty) break;\n" +
                "}\n" +
                "if (fncPdm.IsEmpty) return \"Error: 'Extruded Bar.TopPrtFct' function not found in TopSolid library.\";\n" +
                "DocumentId fncDoc = TopSolidHost.Documents.GetDocument(fncPdm);\n" +
                "TopSolidHost.Application.StartModification(\"ProvideExtrudedBarFunction\", false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);\n" +
                "    ElementId skId = ElementId.Empty;\n" +
                "    foreach (var e in TopSolidHost.Elements.GetElements(targetDoc))\n" +
                "        if (TopSolidHost.Elements.GetTypeFullName(e).IndexOf(\"Sketch\", StringComparison.OrdinalIgnoreCase) >= 0) { skId = e; break; }\n" +
                "    ElementId lParam = ElementId.Empty;\n" +
                "    foreach (var p in TopSolidHost.Parameters.GetParameters(targetDoc))\n" +
                "        if (TopSolidHost.Parameters.GetParameterType(p) == ParameterType.Real && TopSolidHost.Elements.GetName(p) == \"L\") { lParam = p; break; }\n" +
                "    if (lParam.IsEmpty)\n" +
                "    {\n" +
                "        lParam = TopSolidHost.Parameters.CreateRealParameter(targetDoc, UnitType.Length, lz);\n" +
                "        TopSolidHost.Elements.SetName(lParam, \"L\");\n" +
                "    }\n" +
                "    ElementId profFrame = TopSolidHost.Geometries3D.CreateFrame(targetDoc, new Frame3D(new Point3D(0, 0, 0), Direction3D.DX, Direction3D.DY, Direction3D.DZ));\n" +
                "    try { TopSolidHost.Elements.SetName(profFrame, \"Repere_ExtrudedBar_XY_Z\"); } catch { }\n" +
                "    ElementId fnc = ElementId.Empty;\n" +
                "    foreach (var f in TopSolidHost.Entities.GetFunctions(targetDoc)) { fnc = f; break; }\n" +
                "    if (fnc.IsEmpty) fnc = TopSolidHost.Entities.ProvideFunction(targetDoc, fncDoc, \"\");\n" +
                "    foreach (var pub in TopSolidHost.Entities.GetFunctionPublishings(fnc))\n" +
                "    {\n" +
                "        string pName = TopSolidHost.Elements.GetName(pub);\n" +
                "        if (pName == \"Section\" && !skId.IsEmpty) TopSolidHost.Geometries3D.SetSectionPublishingDefinition(pub, new SmartSection3D(skId));\n" +
                "        else if (pName == \"Length\" && !lParam.IsEmpty) TopSolidHost.Parameters.SetRealPublishingDefinition(pub, new SmartReal(lParam));\n" +
                "        else if (pName == \"Frame\" && !profFrame.IsEmpty) TopSolidHost.Geometries3D.SetFramePublishingDefinition(pub, new SmartFrame3D(profFrame, false));\n" +
                "        else if (pName == \"BottomLeft\") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(-wx / 2.0, -hy / 2.0, 0)));\n" +
                "        else if (pName == \"TopRight\") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(wx / 2.0, hy / 2.0, 0)));\n" +
                "    }\n" +
                "    try { TopSolidDesignHost.Parts.SetEnclosingBoxManagement(targetDoc, EnclosingBoxManagementType.Minimal, new SmartFrame3D(profFrame, false), new SmartFrame3D(profFrame, false), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0)); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(targetDoc, true, new SmartReal(UnitType.Mass, 0)); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(targetDoc, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "if (!targetPdm.IsEmpty) TopSolidHost.Pdm.Save(targetPdm, true);\n" +
                "return \"OK: Function 'Extruded Bar' + Frame (XY section, X horizontal, Z longitudinal) + Length L wired on '\" + TopSolidHost.Documents.GetName(targetDoc) + \"'.\";") },

            { "create_extruded_bar", RW("GEOMETRY", "One-shot creation of a metalwork Extruded Bar part (.TopPrt) with XY section (X horizontal), +Z extrusion (Length parameter L), 'Extruded Bar' function + Frame (XY/X/Z), optional transverse through-holes, material, and coating. Param: value=doc_name;designation;reference;folder_name;section_type(PLAT|TUBE_RECT|ROND|TUBE_ROND|CORNIERE);dims_csv_mm;length_mm[;holes_spec[;material[;coating_or_rgb]]]",
                "string raw = \"{value}\".Trim();\n" +
                "string[] parts = raw.Split(';');\n" +
                "if (parts.Length < 7) return \"Error: Use value=doc_name;designation;reference;folder_name;section_type;dims_csv_mm;length_mm[;holes_spec[;material[;coating_or_rgb]]]\";\n" +
                "string docName = parts[0].Trim();\n" +
                "string desig = parts[1].Trim();\n" +
                "string refer = parts[2].Trim();\n" +
                "string folderName = parts[3].Trim();\n" +
                "string explicitProj = \"\";\n" +
                "if (folderName.Contains(\"/\")) { int sl = folderName.IndexOf('/'); explicitProj = folderName.Substring(0, sl).Trim(); folderName = folderName.Substring(sl + 1).Trim(); }\n" +
                "string profType = parts[4].Trim().ToUpperInvariant();\n" +
                "string[] dims = parts[5].Trim().Split(',');\n" +
                "double lengthM = double.Parse(parts[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "string holesSpec = parts.Length > 7 ? parts[7].Trim() : \"\";\n" +
                "string matQuery = parts.Length > 8 ? parts[8].Trim() : \"\";\n" +
                "string cotSpec = parts.Length > 9 ? parts[9].Trim() : \"\";\n" +
                "PdmObjectId projPdm = PdmObjectId.Empty;\n" +
                "if (!string.IsNullOrEmpty(explicitProj))\n" +
                "{\n" +
                "    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(p), explicitProj, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }\n" +
                "}\n" +
                "if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projPdm.IsEmpty) return \"Error: No current project active.\";\n" +
                "PdmObjectId ownerPdm = projPdm;\n" +
                "if (!string.IsNullOrEmpty(folderName))\n" +
                "{\n" +
                "    List<PdmObjectId> fList; List<PdmObjectId> dList;\n" +
                "    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);\n" +
                "    foreach (var f in fList) if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }\n" +
                "    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);\n" +
                "}\n" +
                "List<PdmObjectId> subF; List<PdmObjectId> subD;\n" +
                "TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);\n" +
                "PdmObjectId docPdm = PdmObjectId.Empty;\n" +
                "foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }\n" +
                "if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, \".TopPrt\", true);\n" +
                "TopSolidHost.Pdm.SetName(docPdm, docName);\n" +
                "if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);\n" +
                "if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);\n" +
                "DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);\n" +
                "TopSolidHost.Documents.Open(ref dId);\n" +
                "PdmObjectId fncPdm = PdmObjectId.Empty;\n" +
                "PdmObjectId matPdm = PdmObjectId.Empty;\n" +
                "PdmObjectId cotPdm = PdmObjectId.Empty;\n" +
                "string cotNameOnly = cotSpec.Contains(\",\") ? cotSpec.Substring(0, cotSpec.IndexOf(',')).Trim() : cotSpec;\n" +
                "var matLibs = new List<PdmObjectId>();\n" +
                "var cotLibs = new List<PdmObjectId>();\n" +
                "foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))\n" +
                "{\n" +
                "    string lName = TopSolidHost.Pdm.GetName(lib);\n" +
                "    if (fncPdm.IsEmpty && string.Equals(lName, \"TopSolid\", StringComparison.OrdinalIgnoreCase))\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, \"Extruded Bar\"))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopPrtFct\") { fncPdm = h; break; } }\n" +
                "    }\n" +
                "    if (string.Equals(lName, \"TopSolid MetalWork - Materials and Coatings\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Matériaux\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"Bois TopSolid\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Materials\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Wood\", StringComparison.OrdinalIgnoreCase))\n" +
                "        matLibs.Add(lib);\n" +
                "    if (string.Equals(lName, \"TopSolid MetalWork - Materials and Coatings\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"RAL Classics, By TopSolidBlog\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Matériaux\", StringComparison.OrdinalIgnoreCase))\n" +
                "        cotLibs.Add(lib);\n" +
                "}\n" +
                "if (!string.IsNullOrEmpty(matQuery))\n" +
                "{\n" +
                "    foreach (var lib in matLibs)\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, matQuery))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopMat\") { matPdm = h; break; } }\n" +
                "        if (!matPdm.IsEmpty) break;\n" +
                "    }\n" +
                "    if (matPdm.IsEmpty)\n" +
                "    {\n" +
                "        foreach (var lib in matLibs)\n" +
                "        {\n" +
                "            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, \"*\" + matQuery + \"*\"))\n" +
                "            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopMat\") { matPdm = h; break; } }\n" +
                "            if (!matPdm.IsEmpty) break;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "if (!string.IsNullOrEmpty(cotNameOnly))\n" +
                "{\n" +
                "    foreach (var lib in cotLibs)\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, cotNameOnly))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopCot\") { cotPdm = h; break; } }\n" +
                "        if (!cotPdm.IsEmpty) break;\n" +
                "    }\n" +
                "    if (cotPdm.IsEmpty)\n" +
                "    {\n" +
                "        foreach (var lib in cotLibs)\n" +
                "        {\n" +
                "            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, \"*\" + cotNameOnly + \"*\"))\n" +
                "            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopCot\") { cotPdm = h; break; } }\n" +
                "            if (!cotPdm.IsEmpty) break;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "DocumentId fncDoc = fncPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(fncPdm);\n" +
                "DocumentId matDoc = matPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(matPdm);\n" +
                "DocumentId cotDoc = cotPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(cotPdm);\n" +
                "TopSolidHost.Application.StartModification(\"CreateExtrudedBar_\" + docName, false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref dId);\n" +
                "    var oldOps = TopSolidHost.Operations.GetOperations(dId);\n" +
                "    for (int oi = oldOps.Count - 1; oi >= 0; oi--)\n" +
                "    {\n" +
                "        try\n" +
                "        {\n" +
                "            string tn = TopSolidHost.Elements.GetTypeFullName(oldOps[oi]);\n" +
                "            if (tn.IndexOf(\"Shape\", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf(\"Extrud\", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf(\"Drill\", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf(\"Sketch\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "                TopSolidHost.Elements.Delete(oldOps[oi]);\n" +
                "        } catch { }\n" +
                "    }\n" +
                "    Point3D orig = new Point3D(0, 0, 0);\n" +
                "    Plane3D plXY = new Plane3D(orig, Direction3D.DX, Direction3D.DY);\n" +
                "    ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(dId, new SmartPlane3D(plXY, -1.0, 1.0, -1.0, 1.0), new SmartPoint3D(orig), true, SmartDirection3D.DX);\n" +
                "    TopSolidHost.Sketches2D.StartModification(sk);\n" +
                "    double wx = 0.05, hy = 0.01;\n" +
                "    if (profType == \"PLAT\" || profType == \"FLAT\" || profType == \"TUBE_RECT\" || profType == \"RECT_TUBE\")\n" +
                "    {\n" +
                "        wx = double.Parse(dims[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "        hy = double.Parse(dims[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "        double hx = wx / 2.0, hhy = hy / 2.0;\n" +
                "        ElementItemId p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-hx, -hhy));\n" +
                "        ElementItemId p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx, -hhy));\n" +
                "        ElementItemId p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx, hhy));\n" +
                "        ElementItemId p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-hx, hhy));\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p4, p1);\n" +
                "        if ((profType == \"TUBE_RECT\" || profType == \"RECT_TUBE\") && dims.Length >= 3)\n" +
                "        {\n" +
                "            double t = double.Parse(dims[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double ix = hx - t, iy = hhy - t;\n" +
                "            ElementItemId q1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-ix, -iy));\n" +
                "            ElementItemId q2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(ix, -iy));\n" +
                "            ElementItemId q3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(ix, iy));\n" +
                "            ElementItemId q4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(-ix, iy));\n" +
                "            TopSolidHost.Sketches2D.CreateLineSegment(q1, q2);\n" +
                "            TopSolidHost.Sketches2D.CreateLineSegment(q2, q3);\n" +
                "            TopSolidHost.Sketches2D.CreateLineSegment(q3, q4);\n" +
                "            TopSolidHost.Sketches2D.CreateLineSegment(q4, q1);\n" +
                "        }\n" +
                "    }\n" +
                "    else if (profType == \"ROND\" || profType == \"ROUND_BAR\" || profType == \"TUBE_ROND\" || profType == \"ROUND_TUBE\")\n" +
                "    {\n" +
                "        double d = double.Parse(dims[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "        wx = d; hy = d;\n" +
                "        ElementItemId c0 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, 0));\n" +
                "        TopSolidHost.Sketches2D.CreateCircleSegment(c0, d / 2.0);\n" +
                "        if ((profType == \"TUBE_ROND\" || profType == \"ROUND_TUBE\") && dims.Length >= 2)\n" +
                "        {\n" +
                "            double t = double.Parse(dims[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            TopSolidHost.Sketches2D.CreateCircleSegment(c0, (d / 2.0) - t);\n" +
                "        }\n" +
                "    }\n" +
                "    else if (profType == \"CORNIERE\" || profType == \"L_ANGLE\")\n" +
                "    {\n" +
                "        wx = double.Parse(dims[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "        hy = double.Parse(dims[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "        double t = double.Parse(dims[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "        double x0 = -wx / 2.0, y0 = -hy / 2.0;\n" +
                "        ElementItemId p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0));\n" +
                "        ElementItemId p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + wx, y0));\n" +
                "        ElementItemId p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + wx, y0 + t));\n" +
                "        ElementItemId p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + t, y0 + t));\n" +
                "        ElementItemId p5 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + t, y0 + hy));\n" +
                "        ElementItemId p6 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0 + hy));\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p4, p5);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p5, p6);\n" +
                "        TopSolidHost.Sketches2D.CreateLineSegment(p6, p1);\n" +
                "    }\n" +
                "    TopSolidHost.Sketches2D.CleanSketch();\n" +
                "    TopSolidHost.Sketches2D.EndModification();\n" +
                "    ElementId lParam = ElementId.Empty;\n" +
                "    foreach (var p in TopSolidHost.Parameters.GetParameters(dId))\n" +
                "        if (TopSolidHost.Parameters.GetParameterType(p) == ParameterType.Real && TopSolidHost.Elements.GetName(p) == \"L\") { lParam = p; break; }\n" +
                "    if (lParam.IsEmpty)\n" +
                "    {\n" +
                "        lParam = TopSolidHost.Parameters.CreateRealParameter(dId, UnitType.Length, lengthM);\n" +
                "        TopSolidHost.Elements.SetName(lParam, \"L\");\n" +
                "    }\n" +
                "    else { TopSolidHost.Parameters.SetRealValue(lParam, lengthM); }\n" +
                "    ElementId body = TopSolidHost.Shapes.CreateExtrudedShape(dId, new SmartSection3D(sk), SmartDirection3D.DZ, new SmartReal(lParam), new SmartReal(UnitType.Angle, 0), false, false);\n" +
                "    int holeCount = 0;\n" +
                "    if (!string.IsNullOrEmpty(holesSpec) && holesSpec != \"-\" && holesSpec != \"none\")\n" +
                "    {\n" +
                "        double span = Math.Max(wx, hy) * 2.0 + 0.02;\n" +
                "        foreach (string hItem in holesSpec.Split('|'))\n" +
                "        {\n" +
                "            string[] hp = hItem.Trim().Split(',');\n" +
                "            if (hp.Length < 3) continue;\n" +
                "            double zPos = double.Parse(hp[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double offX = double.Parse(hp[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double diam = double.Parse(hp[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            // CreateDrillingOperation: frame Z is outward normal (+DY), drilling goes along -Z (-DY) from Y = +hy/2\n" +
                "            Frame3D drF = new Frame3D(new Point3D(offX, hy / 2.0, zPos), Direction3D.DX, Direction3D.DZ, Direction3D.DY);\n" +
                "            var prims = new List<DrillingPrimitive> { new DrillingHolePrimitive(new SmartReal(UnitType.Length, diam), new SmartReal(UnitType.Length, span), DrillingHolePrimitiveBottomType.Flat) };\n" +
                "            TopSolidDesignHost.Parts.CreateDrillingOperation(dId, new SmartShape(body), new SmartFrame3D(drF, -0.02, 0.02, -0.02, 0.02, -0.02, 0.02), prims, false);\n" +
                "            holeCount++;\n" +
                "        }\n" +
                "    }\n" +
                "    var allShapes = TopSolidHost.Shapes.GetShapes(dId);\n" +
                "    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(dId))\n" +
                "    {\n" +
                "        try {\n" +
                "            var repCons = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep);\n" +
                "            if (!repCons.Contains(body)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, body);\n" +
                "        } catch { }\n" +
                "    }\n" +
                "    ElementId profFrame = TopSolidHost.Geometries3D.CreateFrame(dId, new Frame3D(new Point3D(0, 0, 0), Direction3D.DX, Direction3D.DY, Direction3D.DZ));\n" +
                "    try { TopSolidHost.Elements.SetName(profFrame, \"Repere_ExtrudedBar_XY_Z\"); } catch { }\n" +
                "    if (!fncDoc.IsEmpty)\n" +
                "    {\n" +
                "        ElementId fnc = ElementId.Empty;\n" +
                "        foreach (var f in TopSolidHost.Entities.GetFunctions(dId)) { fnc = f; break; }\n" +
                "        if (fnc.IsEmpty) fnc = TopSolidHost.Entities.ProvideFunction(dId, fncDoc, \"\");\n" +
                "        foreach (var pub in TopSolidHost.Entities.GetFunctionPublishings(fnc))\n" +
                "        {\n" +
                "            string pName = TopSolidHost.Elements.GetName(pub);\n" +
                "            if (pName == \"Section\") TopSolidHost.Geometries3D.SetSectionPublishingDefinition(pub, new SmartSection3D(sk));\n" +
                "            else if (pName == \"Length\") TopSolidHost.Parameters.SetRealPublishingDefinition(pub, new SmartReal(lParam));\n" +
                "            else if (pName == \"Frame\") TopSolidHost.Geometries3D.SetFramePublishingDefinition(pub, new SmartFrame3D(profFrame, false));\n" +
                "            else if (pName == \"BottomLeft\") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(-wx / 2.0, -hy / 2.0, 0)));\n" +
                "            else if (pName == \"TopRight\") TopSolidHost.Geometries3D.SetPointPublishingDefinition(pub, new SmartPoint3D(new Point3D(wx / 2.0, hy / 2.0, 0)));\n" +
                "        }\n" +
                "    }\n" +
                "    try { TopSolidDesignHost.Parts.SetEnclosingBoxManagement(dId, EnclosingBoxManagementType.Minimal, new SmartFrame3D(profFrame, false), new SmartFrame3D(profFrame, false), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0), new SmartReal(UnitType.Length, 0)); } catch { }\n" +
                "    if (!matDoc.IsEmpty) TopSolidDesignHost.Parts.SetMaterial(dId, matDoc);\n" +
                "    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0)); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(dId, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(dId, true); } catch { }\n" +
                "    if (!cotDoc.IsEmpty) { try { Color alb = TopSolidDesignHost.Coatings.GetAlbedoColor(cotDoc); foreach (var s in allShapes) TopSolidHost.Elements.SetColor(s, alb); } catch { } }\n" +
                "    if (cotSpec.Contains(\",\")) {\n" +
                "        string[] cParts = cotSpec.Split(',');\n" +
                "        int startIdx = cParts.Length >= 4 ? 1 : 0;\n" +
                "        if (cParts.Length - startIdx >= 3) {\n" +
                "            byte r = byte.Parse(cParts[startIdx].Trim()); byte g = byte.Parse(cParts[startIdx+1].Trim()); byte b = byte.Parse(cParts[startIdx+2].Trim());\n" +
                "            foreach (var s in allShapes) try { TopSolidHost.Elements.SetColor(s, new Color(r, g, b)); } catch { }\n" +
                "        }\n" +
                "    }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "TopSolidHost.Pdm.Save(docPdm, true);\n" +
                "return \"OK: Extruded Bar part '\" + docName + \"' (\" + profType + \", L=\" + (lengthM * 1000.0) + \"mm, holes=\" + (string.IsNullOrEmpty(holesSpec) ? \"0\" : holesSpec) + \") created with Extruded Bar function (Frame XY/X/Z) and saved.\";") },

            { "create_revolved_part", RW("GEOMETRY", "Creates a revolved Part (.TopPrt) from an ordered radial/axial polygon profile in millimeters. The profile is closed automatically and revolved around the absolute Y axis. Param: value=doc_name;profile_points(radius_mm,axial_mm|...)[;angle_deg[;designation[;reference]]]",
                "string[] parts = \"{value}\".Trim().Split(';');\n" +
                "if (parts.Length < 2 || parts.Length > 5) return \"Error: Use doc_name;profile_points(radius_mm,axial_mm|...)[;angle_deg[;designation[;reference]]].\";\n" +
                "string docName = parts[0].Trim(), profileSpec = parts[1].Trim();\n" +
                "if (string.IsNullOrWhiteSpace(docName) || string.IsNullOrWhiteSpace(profileSpec)) return \"Error: document name and profile are required.\";\n" +
                "double angleDeg = 360.0;\n" +
                "if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) && (!double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out angleDeg) || double.IsNaN(angleDeg) || double.IsInfinity(angleDeg))) return \"Error: invalid angle_deg.\";\n" +
                "if (angleDeg <= 0.0 || angleDeg > 360.0) return \"Error: angle_deg must be greater than 0 and at most 360.\";\n" +
                "string designation = parts.Length > 3 ? parts[3].Trim() : \"\", reference = parts.Length > 4 ? parts[4].Trim() : \"\";\n" +
                "string[] points = profileSpec.Split('|');\n" +
                "if (points.Length > 1 && points[0].Trim() == points[points.Length - 1].Trim()) Array.Resize(ref points, points.Length - 1);\n" +
                "if (points.Length < 3) return \"Error: profile needs at least three points.\";\n" +
                "List<double[]> profile = new List<double[]>();\n" +
                "double maxRadiusMm = 0.0, minAxialMm = double.MaxValue, maxAxialMm = double.MinValue, twiceArea = 0.0;\n" +
                "for (int i = 0; i < points.Length; i++)\n" +
                "{\n" +
                "    string[] xy = points[i].Split(','); double radiusMm, axialMm;\n" +
                "    if (xy.Length != 2 || !double.TryParse(xy[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radiusMm) || !double.TryParse(xy[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out axialMm)) return \"Error: each profile point must be radius_mm,axial_mm.\";\n" +
                "    if (double.IsNaN(radiusMm) || double.IsInfinity(radiusMm) || double.IsNaN(axialMm) || double.IsInfinity(axialMm) || radiusMm < 0.0) return \"Error: coordinates must be finite and radius_mm cannot be negative.\";\n" +
                "    if (profile.Count > 0 && Math.Abs(profile[profile.Count - 1][0] - radiusMm) < 1e-9 && Math.Abs(profile[profile.Count - 1][1] - axialMm) < 1e-9) return \"Error: adjacent profile points must differ.\";\n" +
                "    profile.Add(new double[] { radiusMm, axialMm }); maxRadiusMm = Math.Max(maxRadiusMm, radiusMm); minAxialMm = Math.Min(minAxialMm, axialMm); maxAxialMm = Math.Max(maxAxialMm, axialMm);\n" +
                "}\n" +
                "for (int i = 0; i < profile.Count; i++) { double[] a = profile[i], b = profile[(i + 1) % profile.Count]; twiceArea += a[0] * b[1] - b[0] * a[1]; }\n" +
                "if (Math.Abs(twiceArea) < 1e-8) return \"Error: profile area must be non-zero.\";\n" +
                "PdmObjectId projectId = TopSolidHost.Pdm.GetCurrentProject(); if (projectId.IsEmpty) return \"Error: no current project.\";\n" +
                "foreach (PdmObjectId existing in TopSolidHost.Pdm.SearchDocumentByName(projectId, docName)) if (string.Equals(TopSolidHost.Pdm.GetName(existing), docName, StringComparison.OrdinalIgnoreCase)) return \"Error: a document with this exact name already exists in the current project.\";\n" +
                "PdmObjectId partPdm = TopSolidHost.Pdm.CreateDocument(projectId, \".TopPrt\", true); if (partPdm.IsEmpty) return \"Error: could not create Part document.\";\n" +
                "TopSolidHost.Pdm.SetName(partPdm, docName); if (!string.IsNullOrEmpty(designation)) TopSolidHost.Pdm.SetDescription(partPdm, designation); if (!string.IsNullOrEmpty(reference)) TopSolidHost.Pdm.SetPartNumber(partPdm, reference);\n" +
                "DocumentId docId = TopSolidHost.Documents.GetDocument(partPdm); TopSolidHost.Documents.Open(ref docId);\n" +
                "bool started = false;\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Application.StartModification(\"CreateRevolvedPart_\" + docName, false); started = true; TopSolidHost.Documents.EnsureIsDirty(ref docId);\n" +
                "    Point3D origin = new Point3D(0, 0, 0); Plane3D planeXY = new Plane3D(origin, Direction3D.DX, Direction3D.DY); double margin = Math.Max(0.05, maxRadiusMm / 5000.0);\n" +
                "    ElementId sketchId = TopSolidHost.Sketches2D.CreateSketchIn3D(docId, new SmartPlane3D(planeXY, -margin, maxRadiusMm / 1000.0 + margin, minAxialMm / 1000.0 - margin, maxAxialMm / 1000.0 + margin), new SmartPoint3D(origin), true, SmartDirection3D.DX);\n" +
                "    TopSolidHost.Elements.SetName(sketchId, \"RevolveProfile\"); TopSolidHost.Sketches2D.StartModification(sketchId); List<ElementItemId> vertices = new List<ElementItemId>();\n" +
                "    foreach (double[] point in profile) vertices.Add(TopSolidHost.Sketches2D.CreateVertex(new Point2D(point[0] / 1000.0, point[1] / 1000.0)));\n" +
                "    for (int i = 0; i < vertices.Count; i++) TopSolidHost.Sketches2D.CreateLineSegment(vertices[i], vertices[(i + 1) % vertices.Count]);\n" +
                "    TopSolidHost.Sketches2D.CleanSketch(); TopSolidHost.Sketches2D.EndModification();\n" +
                "    ElementId axisId = TopSolidHost.Geometries3D.GetAbsoluteYAxis(docId); if (axisId.IsEmpty) throw new Exception(\"Absolute Y axis not found.\");\n" +
                "    ElementId shapeId = TopSolidHost.Shapes.CreateRevolvedShape(docId, new SmartSection3D(sketchId), new SmartAxis3D(axisId, true), new SmartReal(UnitType.Angle, angleDeg * Math.PI / 180.0), false, false);\n" +
                "    if (shapeId.IsEmpty) throw new Exception(\"TopSolid did not create the revolved shape.\");\n" +
                "    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(docId)) { var constituents = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep); if (!constituents.Contains(shapeId)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, shapeId); }\n" +
                "    TopSolidHost.Application.EndModification(true, true); started = false;\n" +
                "}\n" +
                "catch (Exception ex) { if (started) try { TopSolidHost.Application.EndModification(false, false); } catch { } return \"Error creating revolved part: \" + ex.Message + \". The new PDM document may remain empty.\"; }\n" +
                "TopSolidHost.Pdm.Save(TopSolidHost.Documents.GetPdmObject(docId), true);\n" +
                "return \"OK: Revolved Part '\" + docName + \"' (.TopPrt) created with \" + profile.Count + \" profile points at \" + angleDeg + \" degrees and saved.\";") },

            { "revolve_active_part", RW("GEOMETRY", "Adds a revolved solid to the active empty Part (.TopPrt). Refuses if the active document already contains a solid. Profile points are ordered radius_mm,axial_mm pairs in the XY sketch plane and are revolved around the absolute Y axis. Param: value=profile_points(radius_mm,axial_mm|...)[;angle_deg]",
                "string[] parts = \"{value}\".Trim().Split(';');\n" +
                "if (parts.Length < 1 || parts.Length > 2 || string.IsNullOrWhiteSpace(parts[0])) return \"Error: Use profile_points(radius_mm,axial_mm|...)[;angle_deg].\";\n" +
                "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return \"Error: open an empty Part document first.\";\n" +
                "PdmObjectId partPdm = TopSolidHost.Documents.GetPdmObject(docId); if (partPdm.IsEmpty) return \"Error: active document has no PDM object.\";\n" +
                "string extension; TopSolidHost.Pdm.GetType(partPdm, out extension); if (!string.Equals(extension, \".TopPrt\", StringComparison.OrdinalIgnoreCase)) return \"Error: active document must be a Part (.TopPrt).\";\n" +
                "List<ElementId> existingShapes = TopSolidHost.Shapes.GetShapes(docId); if (existingShapes != null && existingShapes.Count > 0) return \"Error: active Part already contains a solid; no changes made.\";\n" +
                "string[] points = parts[0].Split('|'); if (points.Length > 1 && points[0].Trim() == points[points.Length - 1].Trim()) Array.Resize(ref points, points.Length - 1);\n" +
                "if (points.Length < 3) return \"Error: profile needs at least three points.\";\n" +
                "double angleDeg = 360.0; if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) && (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out angleDeg) || double.IsNaN(angleDeg) || double.IsInfinity(angleDeg))) return \"Error: invalid angle_deg.\";\n" +
                "if (angleDeg <= 0.0 || angleDeg > 360.0) return \"Error: angle_deg must be greater than 0 and at most 360.\";\n" +
                "List<double[]> profile = new List<double[]>(); double maxRadiusMm = 0.0, minAxialMm = double.MaxValue, maxAxialMm = double.MinValue, twiceArea = 0.0;\n" +
                "for (int i = 0; i < points.Length; i++)\n" +
                "{\n" +
                "    string[] xy = points[i].Split(','); double radiusMm, axialMm;\n" +
                "    if (xy.Length != 2 || !double.TryParse(xy[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radiusMm) || !double.TryParse(xy[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out axialMm)) return \"Error: each profile point must be radius_mm,axial_mm.\";\n" +
                "    if (double.IsNaN(radiusMm) || double.IsInfinity(radiusMm) || double.IsNaN(axialMm) || double.IsInfinity(axialMm) || radiusMm < 0.0) return \"Error: coordinates must be finite and radius_mm cannot be negative.\";\n" +
                "    if (profile.Count > 0 && Math.Abs(profile[profile.Count - 1][0] - radiusMm) < 1e-9 && Math.Abs(profile[profile.Count - 1][1] - axialMm) < 1e-9) return \"Error: adjacent profile points must differ.\";\n" +
                "    profile.Add(new double[] { radiusMm, axialMm }); maxRadiusMm = Math.Max(maxRadiusMm, radiusMm); minAxialMm = Math.Min(minAxialMm, axialMm); maxAxialMm = Math.Max(maxAxialMm, axialMm);\n" +
                "}\n" +
                "for (int i = 0; i < profile.Count; i++) { double[] a = profile[i], b = profile[(i + 1) % profile.Count]; twiceArea += a[0] * b[1] - b[0] * a[1]; }\n" +
                "if (Math.Abs(twiceArea) < 1e-8) return \"Error: profile area must be non-zero.\";\n" +
                "bool started = false;\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Application.StartModification(\"RevolveActivePart\", false); started = true; TopSolidHost.Documents.EnsureIsDirty(ref docId);\n" +
                "    Point3D origin = new Point3D(0, 0, 0); Plane3D planeXY = new Plane3D(origin, Direction3D.DX, Direction3D.DY); double margin = Math.Max(0.05, maxRadiusMm / 5000.0);\n" +
                "    ElementId sketchId = TopSolidHost.Sketches2D.CreateSketchIn3D(docId, new SmartPlane3D(planeXY, -margin, maxRadiusMm / 1000.0 + margin, minAxialMm / 1000.0 - margin, maxAxialMm / 1000.0 + margin), new SmartPoint3D(origin), true, SmartDirection3D.DX);\n" +
                "    TopSolidHost.Elements.SetName(sketchId, \"RevolveProfile\"); TopSolidHost.Sketches2D.StartModification(sketchId); List<ElementItemId> vertices = new List<ElementItemId>();\n" +
                "    foreach (double[] point in profile) vertices.Add(TopSolidHost.Sketches2D.CreateVertex(new Point2D(point[0] / 1000.0, point[1] / 1000.0)));\n" +
                "    for (int i = 0; i < vertices.Count; i++) TopSolidHost.Sketches2D.CreateLineSegment(vertices[i], vertices[(i + 1) % vertices.Count]);\n" +
                "    TopSolidHost.Sketches2D.CleanSketch(); TopSolidHost.Sketches2D.EndModification();\n" +
                "    ElementId axisId = TopSolidHost.Geometries3D.GetAbsoluteYAxis(docId); if (axisId.IsEmpty) throw new Exception(\"Absolute Y axis not found.\");\n" +
                "    ElementId shapeId = TopSolidHost.Shapes.CreateRevolvedShape(docId, new SmartSection3D(sketchId), new SmartAxis3D(axisId, true), new SmartReal(UnitType.Angle, angleDeg * Math.PI / 180.0), false, false);\n" +
                "    if (shapeId.IsEmpty) throw new Exception(\"TopSolid did not create the revolved shape.\");\n" +
                "    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(docId)) { var constituents = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep); if (!constituents.Contains(shapeId)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, shapeId); }\n" +
                "    TopSolidHost.Application.EndModification(true, true); started = false;\n" +
                "}\n" +
                "catch (Exception ex) { if (started) try { TopSolidHost.Application.EndModification(false, false); } catch { } return \"Error revolving active Part: \" + ex.Message; }\n" +
                "TopSolidHost.Pdm.Save(TopSolidHost.Documents.GetPdmObject(docId), true);\n" +
                "return \"OK: Revolved solid created in active Part with \" + profile.Count + \" profile points at \" + angleDeg + \" degrees and saved.\";") },

            { "replace_revolved_profile", RW("GEOMETRY", "Replaces the profile of the single revolved solid in the active Part. Refuses Parts with additional solid features. The profile uses XY-plane coordinates in millimeters and the absolute Y axis. Param: value=old_sketch_name;segments(P,r,z|L,r,z|A,r,z,cx,cz,CW_or_CCW|...)[;angle_deg]",
                @"string[] parts = ""{value}"".Trim().Split(';');
if (parts.Length < 2 || parts.Length > 3) return ""Error: Use old_sketch_name;segments(P,r,z|L,r,z|A,r,z,cx,cz,CW_or_CCW|...)[;angle_deg]."";
string oldSketchName = parts[0].Trim();
string segmentSpec = parts[1].Trim();
if (string.IsNullOrWhiteSpace(oldSketchName) || string.IsNullOrWhiteSpace(segmentSpec)) return ""Error: sketch name and profile segments are required."";
double angleDeg = 360.0;
if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) && (!double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out angleDeg) || double.IsNaN(angleDeg) || double.IsInfinity(angleDeg))) return ""Error: invalid angle_deg."";
if (angleDeg <= 0.0 || angleDeg > 360.0) return ""Error: angle_deg must be greater than 0 and at most 360."";
string[] encodedSegments = segmentSpec.Split('|');
if (encodedSegments.Length < 4) return ""Error: provide P plus at least three line/arc segments."";
string[] start = encodedSegments[0].Split(',');
double startRadiusMm, startAxialMm;
if (start.Length != 3 || !string.Equals(start[0].Trim(), ""P"", StringComparison.OrdinalIgnoreCase) || !double.TryParse(start[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out startRadiusMm) || !double.TryParse(start[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out startAxialMm)) return ""Error: first token must be P,radius_mm,axial_mm."";
if (startRadiusMm < 0.0 || double.IsNaN(startRadiusMm) || double.IsInfinity(startRadiusMm) || double.IsNaN(startAxialMm) || double.IsInfinity(startAxialMm)) return ""Error: profile coordinates must be finite and radius cannot be negative."";
List<string[]> segments = new List<string[]>();
double currentRadiusMm = startRadiusMm, currentAxialMm = startAxialMm;
double minX = startRadiusMm, maxX = startRadiusMm, minY = startAxialMm, maxY = startAxialMm;
for (int i = 1; i < encodedSegments.Length; i++)
{
    string[] item = encodedSegments[i].Split(',');
    string kind = item[0].Trim().ToUpperInvariant();
    double endRadiusMm, endAxialMm;
    if (item.Length < 3 || (kind != ""L"" && kind != ""A"") || !double.TryParse(item[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out endRadiusMm) || !double.TryParse(item[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out endAxialMm)) return ""Error: each segment must start with L or A and include an endpoint radius_mm,axial_mm."";
    if (endRadiusMm < 0.0 || double.IsNaN(endRadiusMm) || double.IsInfinity(endRadiusMm) || double.IsNaN(endAxialMm) || double.IsInfinity(endAxialMm)) return ""Error: profile coordinates must be finite and radius cannot be negative."";
    if (kind == ""L"" && item.Length != 3) return ""Error: line segment syntax is L,radius_mm,axial_mm."";
    if (kind == ""A"")
    {
        double centerRadiusMm, centerAxialMm;
        if (item.Length != 6 || !double.TryParse(item[3].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out centerRadiusMm) || !double.TryParse(item[4].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out centerAxialMm) || (item[5].Trim().ToUpperInvariant() != ""CW"" && item[5].Trim().ToUpperInvariant() != ""CCW"")) return ""Error: arc syntax is A,radius_mm,axial_mm,center_radius_mm,center_axial_mm,CW|CCW."";
        double startArcRadius = Math.Sqrt((currentRadiusMm-centerRadiusMm)*(currentRadiusMm-centerRadiusMm) + (currentAxialMm-centerAxialMm)*(currentAxialMm-centerAxialMm));
        double endArcRadius = Math.Sqrt((endRadiusMm-centerRadiusMm)*(endRadiusMm-centerRadiusMm) + (endAxialMm-centerAxialMm)*(endAxialMm-centerAxialMm));
        if (Math.Abs(startArcRadius-endArcRadius) > 0.05) return ""Error: arc start and end must lie on the same circle (tolerance 0.05 mm)."";
        minX = Math.Min(minX, centerRadiusMm-startArcRadius); maxX = Math.Max(maxX, centerRadiusMm+startArcRadius);
        minY = Math.Min(minY, centerAxialMm-startArcRadius); maxY = Math.Max(maxY, centerAxialMm+startArcRadius);
    }
    minX = Math.Min(minX, endRadiusMm); maxX = Math.Max(maxX, endRadiusMm); minY = Math.Min(minY, endAxialMm); maxY = Math.Max(maxY, endAxialMm);
    segments.Add(item); currentRadiusMm = endRadiusMm; currentAxialMm = endAxialMm;
}
if (Math.Abs(currentRadiusMm-startRadiusMm) > 0.01 || Math.Abs(currentAxialMm-startAxialMm) > 0.01) return ""Error: final segment must close back to the starting point (tolerance 0.01 mm)."";
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return ""Error: no active document."";
PdmObjectId activePdm = TopSolidHost.Documents.GetPdmObject(docId);
string extension;
TopSolidHost.Pdm.GetType(activePdm, out extension);
if (!string.Equals(extension, "".TopPrt"", StringComparison.OrdinalIgnoreCase)) return ""Error: active document must be a Part (.TopPrt)."";
List<ElementId> shapes = TopSolidHost.Shapes.GetShapes(docId);
if (shapes == null || shapes.Count != 1) return ""Error: active Part must contain exactly one solid."";
List<ElementId> sketches = TopSolidHost.Sketches2D.GetSketches(docId);
ElementId oldSketch = ElementId.Empty;
foreach (ElementId sketch in sketches) if (string.Equals(TopSolidHost.Elements.GetFriendlyName(sketch), oldSketchName, StringComparison.OrdinalIgnoreCase)) oldSketch = sketch;
if (oldSketch.IsEmpty) return ""Error: named profile sketch not found."";
List<ElementId> operations = TopSolidHost.Operations.GetOperations(docId);
int revolveCount = 0;
int extraSolidFeatureCount = 0;
foreach (ElementId operation in operations)
{
    string typeName = TopSolidHost.Elements.GetTypeFullName(operation);
    if (typeName.IndexOf(""RevolvedCreation"", StringComparison.Ordinal) >= 0) revolveCount++;
    else if (typeName.IndexOf(""ExtrudedCreation"", StringComparison.Ordinal) >= 0 || typeName.IndexOf(""PatternCreation"", StringComparison.Ordinal) >= 0 || typeName.IndexOf(""Drilling"", StringComparison.Ordinal) >= 0 || typeName.IndexOf(""Fillet"", StringComparison.Ordinal) >= 0 || typeName.IndexOf(""Chamfer"", StringComparison.Ordinal) >= 0 || typeName.IndexOf(""Boolean"", StringComparison.Ordinal) >= 0) extraSolidFeatureCount++;
}
if (revolveCount != 1 || extraSolidFeatureCount != 0) return ""Error: active Part must have one revolve and no additional solid features."";
bool started = false;
try
{
    TopSolidHost.Application.StartModification(""Replace revolved profile"", false); started = true;
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    operations = TopSolidHost.Operations.GetOperations(docId);
    ElementId oldRevolve = ElementId.Empty;
    foreach (ElementId operation in operations) if (TopSolidHost.Elements.GetTypeFullName(operation).IndexOf(""RevolvedCreation"", StringComparison.Ordinal) >= 0) oldRevolve = operation;
    sketches = TopSolidHost.Sketches2D.GetSketches(docId); oldSketch = ElementId.Empty;
    foreach (ElementId sketch in sketches) if (string.Equals(TopSolidHost.Elements.GetFriendlyName(sketch), oldSketchName, StringComparison.OrdinalIgnoreCase)) oldSketch = sketch;
    if (oldRevolve.IsEmpty || oldSketch.IsEmpty) throw new Exception(""Target revolve or profile changed after transaction start."");
    TopSolidHost.Elements.Delete(oldRevolve);
    TopSolidHost.Elements.Delete(oldSketch);
    Point3D origin = new Point3D(0, 0, 0); Plane3D planeXY = new Plane3D(origin, Direction3D.DX, Direction3D.DY);
    double margin = 0.02;
    ElementId sketchId = TopSolidHost.Sketches2D.CreateSketchIn3D(docId, new SmartPlane3D(planeXY, minX/1000.0-margin, maxX/1000.0+margin, minY/1000.0-margin, maxY/1000.0+margin), new SmartPoint3D(origin), true, SmartDirection3D.DX);
    TopSolidHost.Elements.SetName(sketchId, oldSketchName);
    TopSolidHost.Sketches2D.StartModification(sketchId);
    ElementItemId firstVertex = TopSolidHost.Sketches2D.CreateVertex(new Point2D(startRadiusMm/1000.0, startAxialMm/1000.0));
    ElementItemId currentVertex = firstVertex; currentRadiusMm = startRadiusMm; currentAxialMm = startAxialMm;
    for (int i = 0; i < segments.Count; i++)
    {
        string[] item = segments[i]; string kind = item[0].Trim().ToUpperInvariant();
        double endRadiusMm = double.Parse(item[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        double endAxialMm = double.Parse(item[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        bool closes = i == segments.Count-1;
        ElementItemId endVertex = closes ? firstVertex : TopSolidHost.Sketches2D.CreateVertex(new Point2D(endRadiusMm/1000.0, endAxialMm/1000.0));
        if (kind == ""L"") TopSolidHost.Sketches2D.CreateLineSegment(currentVertex, endVertex);
        else
        {
            double centerRadiusMm = double.Parse(item[3].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            double centerAxialMm = double.Parse(item[4].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            bool clockwise = string.Equals(item[5].Trim(), ""CW"", StringComparison.OrdinalIgnoreCase);
            TopSolidHost.Sketches2D.CreateArcSegment(currentVertex, endVertex, new Point2D(centerRadiusMm/1000.0, centerAxialMm/1000.0), clockwise);
        }
        currentVertex = endVertex; currentRadiusMm = endRadiusMm; currentAxialMm = endAxialMm;
    }
    TopSolidHost.Sketches2D.CleanSketch(); TopSolidHost.Sketches2D.EndModification();
    ElementId axisId = TopSolidHost.Geometries3D.GetAbsoluteYAxis(docId);
    ElementId newShape = TopSolidHost.Shapes.CreateRevolvedShape(docId, new SmartSection3D(sketchId), new SmartAxis3D(axisId, true), new SmartReal(UnitType.Angle, angleDeg*Math.PI/180.0), false, false);
    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(docId)) { var constituents = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep); if (!constituents.Contains(newShape)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, newShape); }
    TopSolidHost.Application.EndModification(true, true); started = false;
}
catch (Exception ex) { if (started) try { TopSolidHost.Application.EndModification(false, false); } catch { } return ""Error replacing revolved profile: "" + ex.Message; }
TopSolidHost.Pdm.Save(TopSolidHost.Documents.GetPdmObject(docId), true);
return ""Replaced the active Part's single revolve profile and saved."";") },

            { "create_sheet_metal_plate", RW("GEOMETRY", "One-shot creation of a metalwork sheet/baseplate (.TopPrt) in XY extruded along +Z, with optional circular through-holes in the same sketch, named parameter Epaisseur, Minimal EnclosingBox, material, and coating. Param: value=doc_name;designation;reference;folder_name;length_x_mm;width_y_mm;thickness_z_mm[;holes_x_y_diam_mm[;material[;coating_or_rgb[;ORIGIN|CENTER]]]]",
                "string raw = \"{value}\".Trim();\n" +
                "string[] parts = raw.Split(';');\n" +
                "if (parts.Length < 7) return \"Error: Use value=doc_name;designation;reference;folder_name;length_x_mm;width_y_mm;thickness_z_mm[;holes_x_y_diam_mm[;material[;coating_or_rgb[;ORIGIN|CENTER]]]]\";\n" +
                "string docName = parts[0].Trim();\n" +
                "string desig = parts.Length > 1 ? parts[1].Trim() : \"\";\n" +
                "string refer = parts.Length > 2 ? parts[2].Trim() : \"\";\n" +
                "string folderName = parts[3].Trim();\n" +
                "string explicitProj = \"\";\n" +
                "if (folderName.Contains(\"/\")) { int sl = folderName.IndexOf('/'); explicitProj = folderName.Substring(0, sl).Trim(); folderName = folderName.Substring(sl + 1).Trim(); }\n" +
                "double lx = double.Parse(parts[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double wy = double.Parse(parts[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double tz = double.Parse(parts[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "string holesSpec = parts.Length > 7 ? parts[7].Trim() : \"\";\n" +
                "string matQuery = parts.Length > 8 ? parts[8].Trim() : \"\";\n" +
                "string cotSpec = parts.Length > 9 ? parts[9].Trim() : \"\";\n" +
                "string alignMode = parts.Length > 10 ? parts[10].Trim().ToUpperInvariant() : \"CENTER\";\n" +
                "PdmObjectId projPdm = PdmObjectId.Empty;\n" +
                "if (!string.IsNullOrEmpty(explicitProj))\n" +
                "{\n" +
                "    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(p), explicitProj, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }\n" +
                "}\n" +
                "if (projPdm.IsEmpty) projPdm = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projPdm.IsEmpty) return \"Error: No current project active.\";\n" +
                "PdmObjectId ownerPdm = projPdm;\n" +
                "if (!string.IsNullOrEmpty(folderName))\n" +
                "{\n" +
                "    List<PdmObjectId> fList; List<PdmObjectId> dList;\n" +
                "    TopSolidHost.Pdm.GetConstituents(projPdm, out fList, out dList);\n" +
                "    foreach (var f in fList) if (string.Equals(TopSolidHost.Pdm.GetName(f), folderName, StringComparison.OrdinalIgnoreCase)) { ownerPdm = f; break; }\n" +
                "    if (ownerPdm.Equals(projPdm)) ownerPdm = TopSolidHost.Pdm.CreateFolder(projPdm, folderName);\n" +
                "}\n" +
                "List<PdmObjectId> subF; List<PdmObjectId> subD;\n" +
                "TopSolidHost.Pdm.GetConstituents(ownerPdm, out subF, out subD);\n" +
                "PdmObjectId docPdm = PdmObjectId.Empty;\n" +
                "foreach (var d in subD) if (string.Equals(TopSolidHost.Pdm.GetName(d), docName, StringComparison.OrdinalIgnoreCase)) { docPdm = d; break; }\n" +
                "if (docPdm.IsEmpty) docPdm = TopSolidHost.Pdm.CreateDocument(ownerPdm, \".TopPrt\", true);\n" +
                "TopSolidHost.Pdm.SetName(docPdm, docName);\n" +
                "if (!string.IsNullOrEmpty(desig)) TopSolidHost.Pdm.SetDescription(docPdm, desig);\n" +
                "if (!string.IsNullOrEmpty(refer)) TopSolidHost.Pdm.SetPartNumber(docPdm, refer);\n" +
                "DocumentId dId = TopSolidHost.Documents.GetDocument(docPdm);\n" +
                "TopSolidHost.Documents.Open(ref dId);\n" +
                "PdmObjectId matPdm = PdmObjectId.Empty;\n" +
                "PdmObjectId cotPdm = PdmObjectId.Empty;\n" +
                "string cotNameOnly = cotSpec.Contains(\",\") ? cotSpec.Substring(0, cotSpec.IndexOf(',')).Trim() : cotSpec;\n" +
                "var matLibs = new List<PdmObjectId>();\n" +
                "var cotLibs = new List<PdmObjectId>();\n" +
                "foreach (var lib in TopSolidHost.Pdm.GetProjects(false, true))\n" +
                "{\n" +
                "    string lName = TopSolidHost.Pdm.GetName(lib);\n" +
                "    if (string.Equals(lName, \"TopSolid MetalWork - Materials and Coatings\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Matériaux\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"Bois TopSolid\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Materials\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Wood\", StringComparison.OrdinalIgnoreCase))\n" +
                "        matLibs.Add(lib);\n" +
                "    if (string.Equals(lName, \"TopSolid MetalWork - Materials and Coatings\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"RAL Classics, By TopSolidBlog\", StringComparison.OrdinalIgnoreCase) || string.Equals(lName, \"TopSolid Matériaux\", StringComparison.OrdinalIgnoreCase))\n" +
                "        cotLibs.Add(lib);\n" +
                "}\n" +
                "if (!string.IsNullOrEmpty(matQuery))\n" +
                "{\n" +
                "    foreach (var lib in matLibs)\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, matQuery))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopMat\") { matPdm = h; break; } }\n" +
                "        if (!matPdm.IsEmpty) break;\n" +
                "    }\n" +
                "    if (matPdm.IsEmpty)\n" +
                "    {\n" +
                "        foreach (var lib in matLibs)\n" +
                "        {\n" +
                "            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, \"*\" + matQuery + \"*\"))\n" +
                "            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopMat\") { matPdm = h; break; } }\n" +
                "            if (!matPdm.IsEmpty) break;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "if (!string.IsNullOrEmpty(cotNameOnly))\n" +
                "{\n" +
                "    foreach (var lib in cotLibs)\n" +
                "    {\n" +
                "        foreach (var h in TopSolidHost.Pdm.SearchDocumentByName(lib, cotNameOnly))\n" +
                "        { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopCot\") { cotPdm = h; break; } }\n" +
                "        if (!cotPdm.IsEmpty) break;\n" +
                "    }\n" +
                "    if (cotPdm.IsEmpty)\n" +
                "    {\n" +
                "        foreach (var lib in cotLibs)\n" +
                "        {\n" +
                "            foreach (var h in TopSolidHost.Pdm.SearchDocumentByNameWithWildcards(lib, \"*\" + cotNameOnly + \"*\"))\n" +
                "            { string ext; TopSolidHost.Pdm.GetType(h, out ext); if (ext == \".TopCot\") { cotPdm = h; break; } }\n" +
                "            if (!cotPdm.IsEmpty) break;\n" +
                "        }\n" +
                "    }\n" +
                "}\n" +
                "DocumentId matDoc = matPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(matPdm);\n" +
                "DocumentId cotDoc = cotPdm.IsEmpty ? DocumentId.Empty : TopSolidHost.Documents.GetDocument(cotPdm);\n" +
                "TopSolidHost.Application.StartModification(\"CreateSheetPlate_\" + docName, false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref dId);\n" +
                "    var oldOps = TopSolidHost.Operations.GetOperations(dId);\n" +
                "    for (int oi = oldOps.Count - 1; oi >= 0; oi--)\n" +
                "    {\n" +
                "        try\n" +
                "        {\n" +
                "            string tn = TopSolidHost.Elements.GetTypeFullName(oldOps[oi]);\n" +
                "            if (tn.IndexOf(\"Shape\", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf(\"Extrud\", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf(\"Drill\", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf(\"Sketch\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "                TopSolidHost.Elements.Delete(oldOps[oi]);\n" +
                "        } catch { }\n" +
                "    }\n" +
                "    Point3D orig = new Point3D(0, 0, 0);\n" +
                "    Plane3D plXY = new Plane3D(orig, Direction3D.DX, Direction3D.DY);\n" +
                "    ElementId sk = TopSolidHost.Sketches2D.CreateSketchIn3D(dId, new SmartPlane3D(plXY, -1.0, 1.0, -1.0, 1.0), new SmartPoint3D(orig), true, SmartDirection3D.DX);\n" +
                "    TopSolidHost.Sketches2D.StartModification(sk);\n" +
                "    double x0 = alignMode == \"ORIGIN\" ? 0.0 : -lx / 2.0;\n" +
                "    double y0 = alignMode == \"ORIGIN\" ? 0.0 : -wy / 2.0;\n" +
                "    ElementItemId p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0));\n" +
                "    ElementItemId p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + lx, y0));\n" +
                "    ElementItemId p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0 + lx, y0 + wy));\n" +
                "    ElementItemId p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(x0, y0 + wy));\n" +
                "    TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);\n" +
                "    TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);\n" +
                "    TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);\n" +
                "    TopSolidHost.Sketches2D.CreateLineSegment(p4, p1);\n" +
                "    if (!string.IsNullOrEmpty(holesSpec) && holesSpec != \"-\" && holesSpec != \"none\")\n" +
                "    {\n" +
                "        foreach (string hItem in holesSpec.Split('|'))\n" +
                "        {\n" +
                "            string[] hp = hItem.Trim().Split(',');\n" +
                "            if (hp.Length < 3) continue;\n" +
                "            double hx = double.Parse(hp[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double hy = double.Parse(hp[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double hd = double.Parse(hp[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            ElementItemId hc = TopSolidHost.Sketches2D.CreateVertex(new Point2D(hx, hy));\n" +
                "            TopSolidHost.Sketches2D.CreateCircleSegment(hc, hd / 2.0);\n" +
                "        }\n" +
                "    }\n" +
                "    TopSolidHost.Sketches2D.CleanSketch();\n" +
                "    TopSolidHost.Sketches2D.EndModification();\n" +
                "    ElementId pEp = ElementId.Empty;\n" +
                "    foreach (var p in TopSolidHost.Parameters.GetParameters(dId))\n" +
                "        if (TopSolidHost.Parameters.GetParameterType(p) == ParameterType.Real && TopSolidHost.Elements.GetName(p) == \"Epaisseur\") { pEp = p; break; }\n" +
                "    if (pEp.IsEmpty) { pEp = TopSolidHost.Parameters.CreateRealParameter(dId, UnitType.Length, tz); TopSolidHost.Elements.SetName(pEp, \"Epaisseur\"); }\n" +
                "    else { TopSolidHost.Parameters.SetRealValue(pEp, tz); }\n" +
                "    ElementId body = TopSolidHost.Shapes.CreateExtrudedShape(dId, new SmartSection3D(sk), SmartDirection3D.DZ, new SmartReal(pEp), new SmartReal(UnitType.Angle, 0), false, false);\n" +
                "    var allShapes = TopSolidHost.Shapes.GetShapes(dId);\n" +
                "    foreach (ElementId rep in TopSolidDesignHost.Representations.GetRepresentations(dId))\n" +
                "    {\n" +
                "        try {\n" +
                "            var repCons = TopSolidDesignHost.Representations.GetRepresentationConstituents(rep);\n" +
                "            if (!repCons.Contains(body)) TopSolidDesignHost.Representations.AddRepresentationConstituent(rep, body);\n" +
                "        } catch { }\n" +
                "    }\n" +
                "    if (!matDoc.IsEmpty) TopSolidDesignHost.Parts.SetMaterial(dId, matDoc);\n" +
                "    try { TopSolidDesignHost.Parts.SetMassPropertyManagement(dId, true, new SmartReal(UnitType.Mass, 0)); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetCenterOfMassPropertyManagement(dId, true, new SmartPoint3D(new Point3D(0, 0, 0))); } catch { }\n" +
                "    try { TopSolidDesignHost.Parts.SetPhysicalPropertiesManagementRefreshAuto(dId, true); } catch { }\n" +
                "    if (!cotDoc.IsEmpty) { try { Color alb = TopSolidDesignHost.Coatings.GetAlbedoColor(cotDoc); foreach (var s in allShapes) TopSolidHost.Elements.SetColor(s, alb); } catch { } }\n" +
                "    if (cotSpec.Contains(\",\")) {\n" +
                "        string[] cParts = cotSpec.Split(',');\n" +
                "        int startIdx = cParts.Length >= 4 ? 1 : 0;\n" +
                "        if (cParts.Length - startIdx >= 3) {\n" +
                "            byte r = byte.Parse(cParts[startIdx].Trim()); byte g = byte.Parse(cParts[startIdx+1].Trim()); byte b = byte.Parse(cParts[startIdx+2].Trim());\n" +
                "            foreach (var s in allShapes) try { TopSolidHost.Elements.SetColor(s, new Color(r, g, b)); } catch { }\n" +
                "        }\n" +
                "    }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "TopSolidHost.Pdm.Save(docPdm, true);\n" +
                "return \"OK: Sheet/Plate part '\" + docName + \"' (\" + (lx*1000) + \"x\" + (wy*1000) + \"x\" + (tz*1000) + \"mm) created and saved.\";") },

            { "create_drilling", RW("GEOMETRY", "Drills a cylindrical hole on the active part at (X,Y,Z) (mm) with outward face normal (NX,NY,NZ) and updates all representations. Param: value=diam_mm:depth_mm:X_mm:Y_mm:Z_mm:NX:NY:NZ",
                "if (docId.IsEmpty) { __message = \"No active document.\"; return; }\n" +
                "string[] p = \"{value}\".Split(':');\n" +
                "if (p.Length < 8) { __message = \"Error: Use value=diam_mm:depth_mm:X_mm:Y_mm:Z_mm:NX:NY:NZ\"; return; }\n" +
                "double diam = double.Parse(p[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double depth = double.Parse(p[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double x = double.Parse(p[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double y = double.Parse(p[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double z = double.Parse(p[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "double nx = double.Parse(p[5].Trim(), System.Globalization.CultureInfo.InvariantCulture);\n" +
                "double ny = double.Parse(p[6].Trim(), System.Globalization.CultureInfo.InvariantCulture);\n" +
                "double nz = double.Parse(p[7].Trim(), System.Globalization.CultureInfo.InvariantCulture);\n" +
                "List<ElementId> shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "if (shapes.Count == 0) { __message = \"Error: No solid shape in document.\"; return; }\n" +
                "ElementId body = shapes[0];\n" +
                "Direction3D norm = new Direction3D(nx, ny, nz);\n" +
                "Direction3D xDir = Math.Abs(nx) < 0.9 ? Direction3D.DX : Direction3D.DY;\n" +
                "Direction3D yDir = Math.Abs(nx) < 0.9 ? Direction3D.DZ : Direction3D.DZ;\n" +
                "Frame3D drF = new Frame3D(new Point3D(x, y, z), xDir, yDir, norm);\n" +
                "var prims = new List<DrillingPrimitive> { new DrillingHolePrimitive(new SmartReal(UnitType.Length, diam), new SmartReal(UnitType.Length, depth), DrillingHolePrimitiveBottomType.Flat) };\n" +
                "TopSolidDesignHost.Parts.CreateDrillingOperation(docId, new SmartShape(body), new SmartFrame3D(drF, -0.02, 0.02, -0.02, 0.02, -0.02, 0.02), prims, false);\n" +
                "__message = \"OK: Drilling d=\" + (diam*1000) + \"mm depth=\" + (depth*1000) + \"mm applied.\";") },

            { "add_inclusion_positioned", RW("ASSEMBLIES", "Includes a part or sub-assembly in an assembly using either (tx,ty,tz[,rx,ry,rz]) or BETWEEN(x1,y1,z1,x2,y2,z2[,ux,uy,uz]) (aligns Extruded Bar +Z from P1 to P2 and section X along uX). Param: value=[target_asm|]child_doc;tx,ty,tz[,rx,ry,rz] OR [target_asm|]child_doc;BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]",
                "string raw = \"{value}\".Trim();\n" +
                "string targetAsmName = \"\";\n" +
                "if (raw.Contains(\"|\")) { int bar = raw.IndexOf('|'); targetAsmName = raw.Substring(0, bar).Trim(); raw = raw.Substring(bar + 1).Trim(); }\n" +
                "string[] parts = raw.Split(';');\n" +
                "if (parts.Length < 2) return \"Error: Use value=[target_asm|]child_doc;tx,ty,tz[,rx,ry,rz] or child_doc;BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz]\";\n" +
                "string childName = parts[0].Trim();\n" +
                "string[] coords = parts[1].Trim().Split(',');\n" +
                "PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "Func<string, PdmObjectId> findDoc = (string dName) => {\n" +
                "    List<PdmObjectId> rf; List<PdmObjectId> rd;\n" +
                "    TopSolidHost.Pdm.GetConstituents(proj, out rf, out rd);\n" +
                "    for (int fi = rf.Count - 1; fi >= 0; fi--)\n" +
                "    {\n" +
                "        List<PdmObjectId> sf; List<PdmObjectId> sd;\n" +
                "        TopSolidHost.Pdm.GetConstituents(rf[fi], out sf, out sd);\n" +
                "        foreach (var d in sd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;\n" +
                "    }\n" +
                "    foreach (var d in rd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;\n" +
                "    var s = TopSolidHost.Pdm.SearchDocumentByName(proj, dName);\n" +
                "    return s.Count > 0 ? s[0] : PdmObjectId.Empty;\n" +
                "};\n" +
                "DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "PdmObjectId asmPdm = asmDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(asmDoc);\n" +
                "if (!string.IsNullOrEmpty(targetAsmName))\n" +
                "{\n" +
                "    asmPdm = findDoc(targetAsmName);\n" +
                "    if (asmPdm.IsEmpty) return \"Error: Target assembly '\" + targetAsmName + \"' not found.\";\n" +
                "    asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);\n" +
                "}\n" +
                "PdmObjectId childPdm = findDoc(childName);\n" +
                "if (childPdm.IsEmpty) return \"Error: Child document '\" + childName + \"' not found.\";\n" +
                "DocumentId childDoc = TopSolidHost.Documents.GetDocument(childPdm);\n" +
                "Transform3D tf = Transform3D.Identity;\n" +
                "if (coords[0].Trim().ToUpperInvariant() == \"BETWEEN\")\n" +
                "{\n" +
                "    double x1 = double.Parse(coords[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double y1 = double.Parse(coords[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double z1 = double.Parse(coords[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double x2 = double.Parse(coords[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double y2 = double.Parse(coords[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double z2 = double.Parse(coords[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double dzx = x2 - x1, dzy = y2 - y1, dzz = z2 - z1;\n" +
                "    double len = Math.Sqrt(dzx*dzx + dzy*dzy + dzz*dzz);\n" +
                "    dzx /= len; dzy /= len; dzz /= len;\n" +
                "    double rxx = coords.Length >= 10 ? double.Parse(coords[7].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 1.0 : 0.0);\n" +
                "    double rxy = coords.Length >= 10 ? double.Parse(coords[8].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 0.0 : 1.0);\n" +
                "    double rxz = coords.Length >= 10 ? double.Parse(coords[9].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0.0;\n" +
                "    double dot = rxx*dzx + rxy*dzy + rxz*dzz;\n" +
                "    double uxx = rxx - dot*dzx, uxy = rxy - dot*dzy, uxz = rxz - dot*dzz;\n" +
                "    double nX = Math.Sqrt(uxx*uxx + uxy*uxy + uxz*uxz);\n" +
                "    uxx /= nX; uxy /= nX; uxz /= nX;\n" +
                "    double uyx = dzy*uxz - dzz*uxy, uyy = dzz*uxx - dzx*uxz, uyz = dzx*uxy - dzy*uxx;\n" +
                "    tf = new Transform3D(uxx, uyx, dzx, x1, uxy, uyy, dzy, y1, uxz, uyz, dzz, z1, 0, 0, 0, 1);\n" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    double tx = double.Parse(coords[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double ty = double.Parse(coords[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double tz = double.Parse(coords[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "    double rx = coords.Length > 3 ? double.Parse(coords[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;\n" +
                "    double ry = coords.Length > 4 ? double.Parse(coords[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;\n" +
                "    double rz = coords.Length > 5 ? double.Parse(coords[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;\n" +
                "    double cx = Math.Cos(rx), sx = Math.Sin(rx), cy = Math.Cos(ry), sy = Math.Sin(ry), cz = Math.Cos(rz), sz = Math.Sin(rz);\n" +
                "    Direction3D vx = new Direction3D(cy*cz, cy*sz, -sy);\n" +
                "    Direction3D vy = new Direction3D(sx*sy*cz - cx*sz, sx*sy*sz + cx*cz, sx*cy);\n" +
                "    Direction3D vz = new Direction3D(cx*sy*cz + sx*sz, cx*sy*sz - sx*cz, cx*cy);\n" +
                "    tf = new Transform3D(vx.X, vy.X, vz.X, tx, vx.Y, vy.Y, vz.Y, ty, vx.Z, vy.Z, vz.Z, tz, 0, 0, 0, 1);\n" +
                "}\n" +
                "TopSolidHost.Application.StartModification(\"AddInclusion_\" + childName, false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);\n" +
                "    ElementId posId = TopSolidDesignHost.Assemblies.CreatePositioning(asmDoc);\n" +
                "    TopSolidDesignHost.Assemblies.CreateInclusion(asmDoc, posId, childName, childDoc, \"\", new List<string>(), new List<SmartObject>(), true, ElementId.Empty, ElementId.Empty, false, true, false, false, tf, true);\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "if (!asmPdm.IsEmpty) TopSolidHost.Pdm.Save(asmPdm, true);\n" +
                "return \"OK: Included '\" + childName + \"' in '\" + TopSolidHost.Documents.GetName(asmDoc) + \"'.\";") },

            { "batch_add_inclusions", RW("ASSEMBLIES", "Batch-includes multiple parts or sub-assemblies into an assembly in a single transaction. Optional 'CLEAR|' prefix deletes existing occurrences first. Each item separated by ';' is either 'docName,tx,ty,tz[,rx,ry,rz[,occName]]' or 'docName,BETWEEN,x1,y1,z1,x2,y2,z2[,ux,uy,uz[,occName]]'. Param: value=[target_asm|][CLEAR|]item1;item2;...",
                "string raw = \"{value}\".Trim();\n" +
                "string targetAsmName = \"\";\n" +
                "bool clearExisting = false;\n" +
                "while (raw.Contains(\"|\"))\n" +
                "{\n" +
                "    int bar = raw.IndexOf('|');\n" +
                "    string prefix = raw.Substring(0, bar).Trim();\n" +
                "    raw = raw.Substring(bar + 1).Trim();\n" +
                "    if (string.Equals(prefix, \"CLEAR\", StringComparison.OrdinalIgnoreCase)) clearExisting = true;\n" +
                "    else targetAsmName = prefix;\n" +
                "}\n" +
                "PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "Func<string, PdmObjectId> findDoc = (string dName) => {\n" +
                "    List<PdmObjectId> rf; List<PdmObjectId> rd;\n" +
                "    TopSolidHost.Pdm.GetConstituents(proj, out rf, out rd);\n" +
                "    for (int fi = rf.Count - 1; fi >= 0; fi--)\n" +
                "    {\n" +
                "        List<PdmObjectId> sf; List<PdmObjectId> sd;\n" +
                "        TopSolidHost.Pdm.GetConstituents(rf[fi], out sf, out sd);\n" +
                "        foreach (var d in sd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;\n" +
                "    }\n" +
                "    foreach (var d in rd) if (string.Equals(TopSolidHost.Pdm.GetName(d), dName, StringComparison.OrdinalIgnoreCase)) return d;\n" +
                "    var s = TopSolidHost.Pdm.SearchDocumentByName(proj, dName);\n" +
                "    return s.Count > 0 ? s[0] : PdmObjectId.Empty;\n" +
                "};\n" +
                "DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "PdmObjectId asmPdm = asmDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(asmDoc);\n" +
                "if (!string.IsNullOrEmpty(targetAsmName))\n" +
                "{\n" +
                "    asmPdm = findDoc(targetAsmName);\n" +
                "    if (asmPdm.IsEmpty) return \"Error: Target assembly '\" + targetAsmName + \"' not found.\";\n" +
                "    asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);\n" +
                "}\n" +
                "if (asmDoc.IsEmpty) return \"Error: No active assembly.\";\n" +
                "TopSolidHost.Documents.Open(ref asmDoc);\n" +
                "string[] items = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);\n" +
                "var docCache = new Dictionary<string, DocumentId>(StringComparer.OrdinalIgnoreCase);\n" +
                "foreach (string item in items)\n" +
                "{\n" +
                "    string cName = item.Split(',')[0].Trim();\n" +
                "    if (!docCache.ContainsKey(cName))\n" +
                "    {\n" +
                "        PdmObjectId fc = findDoc(cName);\n" +
                "        if (fc.IsEmpty) return \"Error: Child document '\" + cName + \"' not found in project.\";\n" +
                "        docCache[cName] = TopSolidHost.Documents.GetDocument(fc);\n" +
                "    }\n" +
                "}\n" +
                "TopSolidHost.Application.StartModification(\"BatchAddInclusions_\" + TopSolidHost.Documents.GetName(asmDoc), false);\n" +
                "int count = 0;\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);\n" +
                "    ElementId modStage = TopSolidHost.Operations.GetModelingStage(asmDoc);\n" +
                "    if (!modStage.IsEmpty) { try { TopSolidHost.Operations.SetWorkingStage(modStage); TopSolidHost.Operations.ResetInsertionOperation(asmDoc); } catch { } }\n" +
                "    if (clearExisting)\n" +
                "    {\n" +
                "        foreach (var op in TopSolidHost.Operations.GetOperations(asmDoc))\n" +
                "            try { if (TopSolidDesignHost.Assemblies.IsInclusion(op)) TopSolidHost.Elements.Delete(op); } catch { }\n" +
                "    }\n" +
                "    foreach (string item in items)\n" +
                "    {\n" +
                "        string[] c = item.Trim().Split(',');\n" +
                "        if (c.Length < 4) continue;\n" +
                "        string cName = c[0].Trim();\n" +
                "        DocumentId childDoc = docCache[cName];\n" +
                "        Transform3D tf = Transform3D.Identity;\n" +
                "        string occName = cName + \"_\" + (count + 1);\n" +
                "        if (c[1].Trim().ToUpperInvariant() == \"BETWEEN\")\n" +
                "        {\n" +
                "            double x1 = double.Parse(c[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double y1 = double.Parse(c[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double z1 = double.Parse(c[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double x2 = double.Parse(c[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double y2 = double.Parse(c[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double z2 = double.Parse(c[7].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double dzx = x2 - x1, dzy = y2 - y1, dzz = z2 - z1;\n" +
                "            double len = Math.Sqrt(dzx*dzx + dzy*dzy + dzz*dzz);\n" +
                "            dzx /= len; dzy /= len; dzz /= len;\n" +
                "            double rxx = c.Length >= 11 ? double.Parse(c[8].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 1.0 : 0.0);\n" +
                "            double rxy = c.Length >= 11 ? double.Parse(c[9].Trim(), System.Globalization.CultureInfo.InvariantCulture) : (Math.Abs(dzx) < 0.9 ? 0.0 : 1.0);\n" +
                "            double rxz = c.Length >= 11 ? double.Parse(c[10].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0.0;\n" +
                "            if (c.Length >= 12) occName = c[11].Trim();\n" +
                "            double dot = rxx*dzx + rxy*dzy + rxz*dzz;\n" +
                "            double uxx = rxx - dot*dzx, uxy = rxy - dot*dzy, uxz = rxz - dot*dzz;\n" +
                "            double nX = Math.Sqrt(uxx*uxx + uxy*uxy + uxz*uxz);\n" +
                "            uxx /= nX; uxy /= nX; uxz /= nX;\n" +
                "            double uyx = dzy*uxz - dzz*uxy, uyy = dzz*uxx - dzx*uxz, uyz = dzx*uxy - dzy*uxx;\n" +
                "            tf = new Transform3D(uxx, uyx, dzx, x1, uxy, uyy, dzy, y1, uxz, uyz, dzz, z1, 0, 0, 0, 1);\n" +
                "        }\n" +
                "        else\n" +
                "        {\n" +
                "            double tx = double.Parse(c[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double ty = double.Parse(c[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double tz = double.Parse(c[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1000.0;\n" +
                "            double rx = c.Length > 4 ? double.Parse(c[4].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;\n" +
                "            double ry = c.Length > 5 ? double.Parse(c[5].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;\n" +
                "            double rz = c.Length > 6 ? double.Parse(c[6].Trim(), System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0 : 0.0;\n" +
                "            if (c.Length > 7) occName = c[7].Trim();\n" +
                "            double cx = Math.Cos(rx), sx = Math.Sin(rx), cy = Math.Cos(ry), sy = Math.Sin(ry), cz = Math.Cos(rz), sz = Math.Sin(rz);\n" +
                "            Direction3D vx = new Direction3D(cy*cz, cy*sz, -sy);\n" +
                "            Direction3D vy = new Direction3D(sx*sy*cz - cx*sz, sx*sy*sz + cx*cz, sx*cy);\n" +
                "            Direction3D vz = new Direction3D(cx*sy*cz + sx*sz, cx*sy*sz - sx*cz, cx*cy);\n" +
                "            tf = new Transform3D(vx.X, vy.X, vz.X, tx, vx.Y, vy.Y, vz.Y, ty, vx.Z, vy.Z, vz.Z, tz, 0, 0, 0, 1);\n" +
                "        }\n" +
                "        ElementId posId = TopSolidDesignHost.Assemblies.CreatePositioning(asmDoc);\n" +
                "        TopSolidDesignHost.Assemblies.CreateInclusion(asmDoc, posId, occName, childDoc, \"\", new List<string>(), new List<SmartObject>(), true, ElementId.Empty, ElementId.Empty, false, true, false, false, tf, true);\n" +
                "        count++;\n" +
                "    }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "if (!asmPdm.IsEmpty) TopSolidHost.Pdm.Save(asmPdm, true);\n" +
                "return \"OK: Batch-included \" + count + \" occurrence(s) into '\" + TopSolidHost.Documents.GetName(asmDoc) + \"' and saved.\";") },

            { "check_collisions", RW("AUDIT", "Activates TopSolid native B-Rep 3D collision analysis (SetCollisionsManagement + ShapeShapeCollisionEntity) on an assembly, reports any colliding pairs, and restores Modeling stage. Param: value=[asm_doc_name]",
                "string targetAsmName = \"{value}\".Trim();\n" +
                "DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "PdmObjectId asmPdm = asmDoc.IsEmpty ? PdmObjectId.Empty : TopSolidHost.Documents.GetPdmObject(asmDoc);\n" +
                "if (!string.IsNullOrEmpty(targetAsmName))\n" +
                "{\n" +
                "    PdmObjectId proj = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "    PdmObjectId foundPdm = PdmObjectId.Empty;\n" +
                "    List<PdmObjectId> rf; List<PdmObjectId> rd;\n" +
                "    TopSolidHost.Pdm.GetConstituents(proj, out rf, out rd);\n" +
                "    for (int fi = rf.Count - 1; fi >= 0 && foundPdm.IsEmpty; fi--)\n" +
                "    {\n" +
                "        List<PdmObjectId> sf; List<PdmObjectId> sd;\n" +
                "        TopSolidHost.Pdm.GetConstituents(rf[fi], out sf, out sd);\n" +
                "        foreach (var d in sd) if (string.Equals(TopSolidHost.Pdm.GetName(d), targetAsmName, StringComparison.OrdinalIgnoreCase)) { foundPdm = d; break; }\n" +
                "    }\n" +
                "    if (foundPdm.IsEmpty)\n" +
                "    {\n" +
                "        var found = TopSolidHost.Pdm.SearchDocumentByName(proj, targetAsmName);\n" +
                "        if (found.Count > 0) foundPdm = found[0];\n" +
                "    }\n" +
                "    if (foundPdm.IsEmpty) return \"Error: Assembly '\" + targetAsmName + \"' not found.\";\n" +
                "    asmPdm = foundPdm; asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);\n" +
                "}\n" +
                "if (asmDoc.IsEmpty) return \"Error: No assembly open or specified.\";\n" +
                "TopSolidHost.Documents.Open(ref asmDoc);\n" +
                "TopSolidHost.Application.StartModification(\"CheckCollisions_\" + TopSolidHost.Documents.GetName(asmDoc), false);\n" +
                "try\n" +
                "{\n" +
                "    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);\n" +
                "    ElementId designRep = TopSolidDesignHost.Representations.GetDesignRepresentation(asmDoc);\n" +
                "    TopSolidDesignHost.Assemblies.SetCollisionsManagement(asmDoc, designRep, true, true, true);\n" +
                "    TopSolidDesignHost.Assemblies.SetCollisionsManagementCheckingMechanisms(asmDoc, false);\n" +
                "    TopSolidDesignHost.Assemblies.SetCollisionsManagementInvalidWhenColliding(asmDoc, false);\n" +
                "    ElementId modStage = TopSolidHost.Operations.GetModelingStage(asmDoc);\n" +
                "    if (!modStage.IsEmpty)\n" +
                "    {\n" +
                "        TopSolidHost.Operations.SetWorkingStage(modStage);\n" +
                "        TopSolidHost.Operations.ResetInsertionOperation(asmDoc);\n" +
                "    }\n" +
                "    TopSolidHost.Application.EndModification(true, true);\n" +
                "}\n" +
                "catch (Exception ex) { TopSolidHost.Application.EndModification(false, false); return \"Error: \" + ex.Message; }\n" +
                "int collCount = 0;\n" +
                "var sb = new StringBuilder();\n" +
                "foreach (var el in TopSolidHost.Elements.GetElements(asmDoc))\n" +
                "{\n" +
                "    if (TopSolidHost.Elements.GetTypeFullName(el) == \"TopSolid.Cad.Design.DB.Collisions.ShapeShapeCollisionEntity\")\n" +
                "    {\n" +
                "        collCount++;\n" +
                "        var chs = TopSolidHost.Elements.GetConstituents(el);\n" +
                "        var pair = new List<string>();\n" +
                "        foreach (var ch in chs)\n" +
                "        {\n" +
                "            ElementId target = TopSolidHost.Entities.IsShortcut(ch) ? TopSolidHost.Entities.GetShortcutTarget(ch) : ElementId.Empty;\n" +
                "            ElementId occ = target.IsEmpty ? ElementId.Empty : TopSolidHost.Elements.GetParent(target);\n" +
                "            pair.Add(TopSolidHost.Elements.GetFriendlyName(ch) + \" (\" + (occ.IsEmpty ? \"\" : TopSolidHost.Elements.GetFriendlyName(occ)) + \")\");\n" +
                "        }\n" +
                "        sb.AppendLine(\"  COLLISION: \" + string.Join(\" <-> \", pair));\n" +
                "    }\n" +
                "}\n" +
                "if (!asmPdm.IsEmpty) TopSolidHost.Pdm.Save(asmPdm, true);\n" +
                "return \"OK: B-Rep Collision Check on '\" + TopSolidHost.Documents.GetName(asmDoc) + \"' — Collisions: \" + collCount + (collCount > 0 ? \"\\n\" + sb.ToString() : \" (0 collision)\");") },

            { "update_where_used_and_save", RW("PROJECTS", "Updates (rebuilds) and saves all documents of the current project in bottom-up dependency order (Parts .TopPrt -> Sub-Assemblies .TopAsm -> Root Assembly .TopAsm) so all Where-Used links stay up to date. Param: value=[project_name]",
                "if (false) { TopSolidHost.Application.StartModification(\"\", false); TopSolidHost.Application.EndModification(false, false); }\n" +
                "string projName = \"{value}\".Trim();\n" +
                "PdmObjectId projPdm = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (!string.IsNullOrEmpty(projName))\n" +
                "{\n" +
                "    foreach (var p in TopSolidHost.Pdm.GetProjects(true, false))\n" +
                "        if (string.Equals(TopSolidHost.Pdm.GetName(p), projName, StringComparison.OrdinalIgnoreCase)) { projPdm = p; break; }\n" +
                "}\n" +
                "if (projPdm.IsEmpty) return \"Error: No current project.\";\n" +
                "var allDocs = new List<PdmObjectId>();\n" +
                "var fStack = new Stack<PdmObjectId>();\n" +
                "fStack.Push(projPdm);\n" +
                "while (fStack.Count > 0)\n" +
                "{\n" +
                "    PdmObjectId cur = fStack.Pop();\n" +
                "    List<PdmObjectId> subF; List<PdmObjectId> subD;\n" +
                "    TopSolidHost.Pdm.GetConstituents(cur, out subF, out subD);\n" +
                "    foreach (var d in subD) allDocs.Add(d);\n" +
                "    foreach (var f in subF) fStack.Push(f);\n" +
                "}\n" +
                "var partsList = new List<PdmObjectId>();\n" +
                "var asmsList = new List<PdmObjectId>();\n" +
                "foreach (var d in allDocs)\n" +
                "{\n" +
                "    string ext; TopSolidHost.Pdm.GetType(d, out ext);\n" +
                "    if (string.Equals(ext, \".TopPrt\", StringComparison.OrdinalIgnoreCase)) partsList.Add(d);\n" +
                "    else if (string.Equals(ext, \".TopAsm\", StringComparison.OrdinalIgnoreCase)) asmsList.Add(d);\n" +
                "}\n" +
                "int updatedParts = 0, updatedAsms = 0;\n" +
                "foreach (var p in partsList)\n" +
                "{\n" +
                "    try { TopSolidHost.Pdm.Save(p, true); updatedParts++; } catch { }\n" +
                "}\n" +
                "foreach (var a in asmsList)\n" +
                "{\n" +
                "    try { DocumentId dId = TopSolidHost.Documents.GetDocument(a); TopSolidHost.Documents.Open(ref dId); try { TopSolidHost.Documents.Update(dId, false); } catch { } TopSolidHost.Pdm.Save(a, true); updatedAsms++; } catch { }\n" +
                "}\n" +
                "foreach (var a in asmsList)\n" +
                "{\n" +
                "    try { DocumentId dId = TopSolidHost.Documents.GetDocument(a); TopSolidHost.Documents.Open(ref dId); try { TopSolidHost.Documents.Update(dId, false); } catch { } TopSolidHost.Pdm.Save(a, true); } catch { }\n" +
                "}\n" +
                "TopSolidHost.Pdm.Save(projPdm, true);\n" +
                "return \"OK: Where-Used bottom-up update & save complete on '\" + TopSolidHost.Pdm.GetName(projPdm) + \"' (Parts: \" + updatedParts + \", Assemblies: \" + updatedAsms + \").\";") },
        };

        // Shortcut factory methods for readability.
        // R  = read-only, RW = writes to the PDM (transactional), RD = writes to disk only.
        private static RecipeEntry R(string category, string description, string code)
        {
            return new RecipeEntry(category, description, code, RecipeEntry.RecipeMode.Read);
        }

        private static RecipeEntry RW(string category, string description, string code)
        {
            return new RecipeEntry(category, description, code, RecipeEntry.RecipeMode.WritePdm);
        }

        private static RecipeEntry RD(string category, string description, string code)
        {
            return new RecipeEntry(category, description, code, RecipeEntry.RecipeMode.WriteDisk);
        }

        /// <summary>
        /// Creates the tool.
        /// </summary>
        /// <param name="connectorProvider">Supplies the current TopSolid connector.</param>
        /// <param name="readOnly">When true, only Read recipes are allowed.</param>
        public RecipeTool(Func<TopSolidConnector> connectorProvider, bool readOnly = false)
        {
            _connectorProvider = connectorProvider;
            _readOnly = readOnly;
        }

        public void Register(McpToolRegistry registry)
        {
            // The recipe catalogue is deliberately NOT inlined here: it used to be
            // duplicated in both descriptions (~1300 tokens on every session start).
            // topsolid_list_recipes serves the catalogue on demand instead.
            string categories = string.Join(", ", GetAllCategories());

            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_run_recipe",
                Description = "Executes one of the " + Recipes.Count + " pre-built TopSolid recipes. " +
                    "Do NOT write code - pick a recipe name. " +
                    "Categories: " + categories + ". " +
                    "Use topsolid_list_recipes to find the exact name of a recipe.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["recipe"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Exact recipe name (e.g. 'read_designation'). " +
                                "Call topsolid_list_recipes to search the catalogue by category or keyword."
                        },
                        ["value"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Value for parameterized recipes (e.g. name, path, name:value)"
                        }
                    },
                    ["required"] = new JArray { "recipe" }
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            string recipeName = arguments?["recipe"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(recipeName))
                return "Error: 'recipe' is required. Use topsolid_list_recipes to browse the " +
                    Recipes.Count + " available recipes.";

            RecipeEntry recipe;
            if (!Recipes.TryGetValue(recipeName, out recipe))
                return "Unknown recipe: '" + recipeName + "'. " +
                    "Use topsolid_list_recipes to search the available recipes.";

            if (_readOnly && recipe.Mode != RecipeEntry.RecipeMode.Read)
                return "Refused: recipe '" + recipeName + "' is " + GetModeLabel(recipe.Mode) +
                    " and this server runs in read-only mode. " +
                    "Only " + GetModeLabel(RecipeEntry.RecipeMode.Read) + " recipes are allowed.";

            // Substitute the {value} placeholder, escaped for a C# string literal.
            string value = arguments["value"]?.ToString() ?? "";
            string code = recipe.Code.Replace("{value}", EscapeForLiteral(value));

            // Ensure connector is initialized and connected (auto-reconnect if needed)
            var connector = _connectorProvider();
            if (connector == null)
                return "Error: TopSolid connector not initialized.";
            if (!connector.EnsureConnected())
                return "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled (port 8090). Use the tray icon to reconnect.";

            // The declared mode wins: auto-detection must never promote a recipe.
            // forceModification: true  -> transactional wrapper (Pattern D).
            // blockReturn:       false -> recipe bodies legitimately use a bare 'return;' as an
            //                             early exit; the wrapper rewrites it into 'goto __commit'.
            //                             ScriptExecutor.ExecuteModification() cannot be used here
            //                             because it sets blockReturn = true, which is the guard
            //                             meant for topsolid_modify_script, not for recipes.
            // autoDetect:        false -> the declared mode is final.
            if (recipe.Mode == RecipeEntry.RecipeMode.WritePdm)
                return ScriptExecutor.Execute(code, true, false, false);

            // Read and WriteDisk: no PDM transaction, and auto-detection disabled.
            return ScriptExecutor.Execute(code, false, false, false);
        }

        /// <summary>
        /// Escapes a user-supplied value so it can be embedded in a C# string literal.
        /// Backslash first, then the double quote, then the control characters that would
        /// otherwise terminate the literal (CS1010: newline in constant).
        /// </summary>
        private static string EscapeForLiteral(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Returns the human-readable label of an execution mode.
        /// </summary>
        public static string GetModeLabel(RecipeEntry.RecipeMode mode)
        {
            switch (mode)
            {
                case RecipeEntry.RecipeMode.WritePdm: return "[WRITE-PDM]";
                case RecipeEntry.RecipeMode.WriteDisk: return "[WRITE-DISK]";
                default: return "[READ]";
            }
        }

        /// <summary>
        /// Returns the recipe definition for a given name, or null if not found.
        /// Used by GetRecipeTool to expose recipe code as API reference for
        /// developers writing standalone C# TopSolid apps.
        /// </summary>
        public static RecipeEntry GetRecipe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            RecipeEntry entry;
            Recipes.TryGetValue(name.Trim(), out entry);
            return entry;
        }

        /// <summary>
        /// Returns all available recipe names (sorted).
        /// </summary>
        public static List<string> GetAllRecipeNames()
        {
            var names = new List<string>(Recipes.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Returns the distinct recipe categories (sorted).
        /// </summary>
        public static List<string> GetAllCategories()
        {
            var categories = new List<string>();
            foreach (var entry in Recipes.Values)
            {
                if (string.IsNullOrEmpty(entry.Category)) continue;
                if (!categories.Contains(entry.Category)) categories.Add(entry.Category);
            }
            categories.Sort(StringComparer.OrdinalIgnoreCase);
            return categories;
        }

        /// <summary>
        /// A recipe definition: category, human description, C# body and execution mode.
        /// </summary>
        public class RecipeEntry
        {
            /// <summary>
            /// How a recipe affects the system.
            /// </summary>
            public enum RecipeMode
            {
                /// <summary>Reads only. Nothing is written.</summary>
                Read,

                /// <summary>Modifies the document or the PDM. Needs a modification transaction.</summary>
                WritePdm,

                /// <summary>Writes outside TopSolid (file on disk, print job). No PDM transaction.</summary>
                WriteDisk
            }

            public string Category { get; }
            public string Description { get; }
            public string Code { get; }
            public RecipeMode Mode { get; }

            /// <summary>
            /// True when the recipe modifies the document or the PDM and therefore needs a
            /// modification transaction. Kept so existing callers keep working.
            /// </summary>
            public bool IsModification
            {
                get { return Mode == RecipeMode.WritePdm; }
            }

            public RecipeEntry(string category, string description, string code, RecipeMode mode)
            {
                Category = category;
                Description = description;
                Code = code;
                Mode = mode;
            }
        }
    }
}
