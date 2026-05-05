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

        private static readonly Dictionary<string, RecipeEntry> Recipes = new Dictionary<string, RecipeEntry>(StringComparer.OrdinalIgnoreCase)
        {
            // =====================================================================
            // PDM PROPERTIES — Read
            // =====================================================================
            { "read_designation", R("Reads the designation of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Designation: (empty)\" : \"Designation: \" + val;") },
            { "read_name", R("Reads the name of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "return \"Name: \" + TopSolidHost.Pdm.GetName(pdmId);") },
            { "read_reference", R("Reads the reference (part number) of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Reference: (empty)\" : \"Reference: \" + val;") },
            { "read_manufacturer", R("Reads the manufacturer of the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Manufacturer: (empty)\" : \"Manufacturer: \" + val;") },
            { "read_pdm_properties", R("Reads all PDM properties (name, designation, reference, manufacturer)",
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
            { "set_designation", R("Sets the designation. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetDescription(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Designation → {value}\";") },
            { "set_name", R("Sets the name. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetName(pdmId, \"{value}\");\n" +
                "return \"OK: Name → {value}\";") },
            { "set_reference", R("Sets the reference. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetPartNumber(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Reference → {value}\";") },
            { "set_manufacturer", R("Sets the manufacturer. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetManufacturer(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Manufacturer → {value}\";") },

            // =====================================================================
            // PROJECTS & PDM NAVIGATION
            // =====================================================================
            { "read_current_project", R("Returns the current project",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "return \"Project: \" + TopSolidHost.Pdm.GetName(projId);") },
            { "read_project_contents", R("Lists folders, subfolders and documents of the current project (full tree)",
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
            { "search_document", R("Searches for a document by name (CONTAINS). Param: value",
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
            { "search_folder", R("Searches for a folder by name (CONTAINS). Param: value",
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
            { "document_type", R("Detects the type of the active document",
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
            { "save_document", R("Saves the active document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Document saved.\";") },
            { "rebuild_document", RW("Rebuilds the active document",
                "TopSolidHost.Documents.Rebuild(docId);\n" +
                "__message = \"OK: Document rebuilt.\";") },

            // =====================================================================
            // PARAMETERS — Read
            // =====================================================================
            { "read_parameters", R("Lists all parameters of the active document",
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
            { "read_real_parameter", R("Reads a real parameter by name. Param: value=name",
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
            { "read_text_parameter", R("Reads a text parameter by name. Param: value=name",
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
            { "set_real_parameter", RW("Sets a real parameter. Param: value=name:SIvalue (e.g. Length:0.15)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length != 2) return \"Format: name:SIvalue (e.g. Length:0.15)\";\n" +
                "string pName = parts[0].Trim();\n" +
                "double newVal;\n" +
                "if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out newVal))\n" +
                "    return \"Invalid value: \" + parts[1];\n" +
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
            { "set_text_parameter", RW("Sets a text parameter. Param: value=name:value",
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

            { "creer_parametre_reel", RW("Creates a new real parameter in the active document. " +
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

            { "creer_parametre_formule", RW("Creates a new formula-driven real parameter in the active document. " +
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

            { "creer_esquisse_rectangle", RW("Cree une esquisse 2D rectangulaire dans le document actif. Param: value=largeur:hauteur (mm)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length < 2) { __message = \"ERROR: format attendu largeur:hauteur (mm)\"; return; }\n" +
                "double widthMm, heightMm;\n" +
                "if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out widthMm) ||\n" +
                "    !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heightMm))\n" +
                "    { __message = \"ERROR: valeurs numeriques invalides\"; return; }\n" +
                "double w = widthMm * 0.001;\n" +
                "double h = heightMm * 0.001;\n" +
                "// On utilise docId mis a jour par le wrapper (EnsureIsDirty)\n" +
                "ElementId sketchId = TopSolidHost.Sketches2D.CreateSketchIn2D(docId, new SmartPoint2D(Point2D.O), new SmartDirection2D(Direction2D.DX), false);\n" +
                "TopSolidHost.Elements.SetName(sketchId, \"Esquisse_Rectangle\");\n" +
                "TopSolidHost.Sketches2D.StartModification(sketchId);\n" +
                "ElementItemId pt1 = TopSolidHost.Sketches2D.CreatePoint(new Point2D(0, 0));\n" +
                "ElementItemId pt2 = TopSolidHost.Sketches2D.CreatePoint(new Point2D(w, 0));\n" +
                "ElementItemId pt3 = TopSolidHost.Sketches2D.CreatePoint(new Point2D(w, h));\n" +
                "ElementItemId pt4 = TopSolidHost.Sketches2D.CreatePoint(new Point2D(0, h));\n" +
                "var segs = new List<ElementItemId>();\n" +
                "segs.Add(TopSolidHost.Sketches2D.CreateLineSegment(pt1, pt2));\n" +
                "segs.Add(TopSolidHost.Sketches2D.CreateLineSegment(pt2, pt3));\n" +
                "segs.Add(TopSolidHost.Sketches2D.CreateLineSegment(pt3, pt4));\n" +
                "segs.Add(TopSolidHost.Sketches2D.CreateLineSegment(pt4, pt1));\n" +
                "TopSolidHost.Sketches2D.CreateProfile(segs);\n" +
                "TopSolidHost.Sketches2D.EndModification();\n" +
                "__message = \"OK: esquisse Esquisse_Rectangle creee (\" + widthMm + \"x\" + heightMm + \" mm)\";") },

            { "extruder_esquisse", RW("Extrude la derniere esquisse 2D du document actif en solide 3D. Param: value=hauteur_mm",
                "double heightMm;\n" +
                "if (!double.TryParse(\"{value}\".Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heightMm))\n" +
                "    { __message = \"ERROR: hauteur numerique invalide\"; return; }\n" +
                "double heightM = heightMm * 0.001;\n" +
                "var sketches = TopSolidHost.Sketches2D.GetSketches(docId);\n" +
                "if (sketches == null || sketches.Count == 0)\n" +
                "    { __message = \"ERROR: aucune esquisse dans ce document\"; return; }\n" +
                "ElementId lastSketch = sketches[sketches.Count - 1];\n" +
                "TopSolidHost.Shapes.CreateExtrudedShape(docId, new SmartSection3D(lastSketch), SmartDirection3D.DZ, new SmartReal(UnitType.Length, heightM), new SmartReal(UnitType.Angle, 0), false, false);\n" +
                "__message = \"OK: extrusion de \" + heightMm + \"mm creee\";") },

            { "ajouter_inclusion", RW("Ajoute une inclusion d'un document piece dans le document assemblage actif. Param: value=nom_document_piece",
                "if (!TopSolidDesignHost.Assemblies.IsAssembly(docId))\n" +
                "    { __message = \"ERROR: le document actif n'est pas un assemblage\"; return; }\n" +
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
                "    { __message = \"ERROR: document '\" + targetName + \"' introuvable (ouvrir le document d'abord)\"; return; }\n" +
                "ElementId positioningId = TopSolidDesignHost.Assemblies.CreatePositioning(docId);\n" +
                "TopSolidDesignHost.Assemblies.CreateInclusion(docId, positioningId, null, partDoc, null, null, null, true, ElementId.Empty, ElementId.Empty, false, false, false, false, Transform3D.Identity, false);\n" +
                "__message = \"OK: document '\" + targetName + \"' inclus dans l'assemblage\";") },

            // =====================================================================
            // GEOMETRY — Read
            // =====================================================================
            { "read_3d_points", R("Lists 3D points of the document",
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
            { "read_3d_frames", R("Lists 3D frames of the document",
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
            { "list_sketches", R("Lists sketches of the document",
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
            { "read_shapes", R("Lists shapes of the document",
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
            { "read_operations", R("Lists operations (feature tree)",
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
            { "detect_assembly", R("Detects if the document is an assembly and lists parts",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Unable to verify (not a Design document).\"; }\n" +
                "if (!isAsm) return \"This document is NOT an assembly.\";\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Assembly: \" + parts.Count + \" pieces\");\n" +
                "foreach (var p in parts)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(p));\n" +
                "return sb.ToString();") },
            { "list_inclusions", R("Lists inclusions of an assembly",
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
            { "detect_family", R("Detects if the document is a family",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isFamily = TopSolidHost.Families.IsFamily(docId);\n" +
                "if (!isFamily) return \"This document is NOT a family.\";\n" +
                "bool isExplicit = TopSolidHost.Families.IsExplicit(docId);\n" +
                "return \"Family detected (\" + (isExplicit ? \"explicit\" : \"implicit\") + \").\";") },
            { "read_family_codes", R("Reads family codes",
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
            { "open_drafting", R("Finds and opens the drafting associated with the current part/assembly via PDM back-references",
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

            { "detect_drafting", R("Detects if the document is a drafting and provides info",
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
            { "list_drafting_views", R("Lists views of a drafting",
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
            { "detect_bom", R("Detects if the document is a BOM",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isBom) return \"This document is NOT a BOM.\";\n" +
                "int cols = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "return \"BOM detected (\" + cols + \" columns).\";") },
            { "read_bom_columns", R("Reads BOM columns",
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
            { "read_drafting_scale", R("Reads global and per-view scale of a drafting",
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

            { "read_drafting_format", R("Reads the format (size, margins) of a drafting",
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

            { "read_main_projection", R("Reads the main projection of a drafting",
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
            { "read_bom_contents", R("Reads the full BOM contents (rows and cells)",
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

            { "count_bom_rows", R("Counts active BOM rows",
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
            { "detect_unfolding", R("Detects if the document is an unfolding (sheet metal)",
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

            { "read_bend_features", R("Lists bends of an unfolding (angles, radii, lengths)",
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

            { "read_unfolding_dimensions", R("Reads unfolding dimensions from system properties (sheet metal)",
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
            { "set_drafting_scale", RW("Sets the global scale of a drafting. Param: value=denominator (e.g. '10' for 1:10)",
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

            { "set_drafting_format", RW("Sets the drafting format (paper size). Param: value=format_name (e.g. 'A3', 'A4')",
                "if (docId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { __message = \"Not a drafting.\"; return; }\n" +
                "if (!isDrafting) { __message = \"Not a drafting.\"; return; }\n" +
                "string fmt = \"{value}\";\n" +
                "if (string.IsNullOrWhiteSpace(fmt)) { __message = \"ERROR: value required (e.g. 'A3').\"; return; }\n" +
                "TopSolidDraftingHost.Draftings.SetDraftingFormatName(docId, fmt);\n" +
                "__message = \"OK: drafting format set to \" + fmt;") },

            { "set_projection_quality", RW("Sets the drafting projection quality. Param: value='exact' (precise) or 'fast' (quick)",
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

            { "print_drafting", R("Prints the current drafting (all pages, black & white, 300 DPI, printed to scale)",
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

            { "activate_bom_row", RW("Activates a BOM row by its index (0-based, among root-children). Param: value=row_index",
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

            { "deactivate_bom_row", RW("Deactivates a BOM row by its index (0-based, among root-children). Param: value=row_index",
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
            { "list_exporters", R("Lists all available exporters",
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
            { "export_step", R("Exporte en STEP. Param: value=chemin (ex: C:\\temp\\piece.stp)",
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
            { "export_dxf", R("Exports to DXF. Param: value=path",
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
            { "export_pdf", R("Exports to PDF. Param: value=path",
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
            { "read_user_property", R("Reads a text user property. Param: value=property_name",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetTextUserProperty(pdmId, \"{value}\");\n" +
                "return string.IsNullOrEmpty(val) ? \"Propriete '{value}': (empty)\" : \"Propriete '{value}': \" + val;") },

            // =====================================================================
            // PART AUDIT — High-value composite scenarios
            // =====================================================================
            { "audit_part", R("Full part audit: properties, parameters, shapes, mass, volume",
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

            { "audit_assembly", R("Full assembly audit: parts, inclusions, mass",
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
                "sb.Insert(sb.ToString().IndexOf(\"\\nPieces\"), \"Inclusions: \" + inclCount + \"\\n\");\n" +
                "return sb.ToString();") },

            { "check_part", R("Quality check: designation, reference, material filled?",
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
            { "read_mass_volume", R("Reads mass, volume, surface from document system properties",
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

            { "read_material_density", R("Calculates density from document mass/volume",
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
            { "invoke_command", R("Executes a TopSolid menu command by name. Param: value=command_name",
                "bool result = TopSolidHost.Application.InvokeCommand(\"{value}\");\n" +
                "return result ? \"OK: Command '{value}' executee.\" : \"ERREUR: Commande '{value}' non trouvee ou echec.\";") },

            // =====================================================================
            // OCCURRENCES — Occurrence properties in assemblies
            // =====================================================================
            { "read_occurrences", R("Lists occurrences of an assembly with their definition",
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
                "    sb.AppendLine(\"  \" + name + (isOcc ? \" [occ]\" : \"\") + (!string.IsNullOrEmpty(occName) ? \" nom=\" + occName : \"\") + (!string.IsNullOrEmpty(defName) ? \" -> \" + defName : \"\"));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "rename_occurrence", RW("Renames an occurrence. Param: value=old_name:new_name",
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
            // USER PROPERTIES — Ecriture
            // =====================================================================
            { "set_user_property", R("Sets a text user property. Param: value=property_name:value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) return \"Format: property_name:value\";\n" +
                "string propName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string propVal = \"{value}\".Substring(idx + 1).Trim();\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetTextUserProperty(pdmId, propName, propVal);\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Property '\" + propName + \"' = '\" + propVal + \"'\";") },

            // =====================================================================
            // BOUNDING BOX / STOCK
            // =====================================================================
            { "read_bounding_box", R("Reads bounding box dimensions from system properties",
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

            { "read_part_dimensions", R("Reads dimensions (Height, Width, Length, Box Size) from system properties",
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

            { "read_inertia_moments", R("Reads principal inertia moments from system properties",
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
            { "list_project_documents", R("Lists ALL project documents with designation and reference",
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

            { "check_project", R("Full project quality check: parts without designation/reference",
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
            { "compare_parameters", R("Compares parameters of the active document with another. Param: value=other_document_name",
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

            { "compare_document_operations", R("Compares operations (feature tree) between the current and another document. Param: value=other_doc_name",
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

            { "compare_document_entities", R("Compares entities (shapes, sketches, points, frames) between the current and another document. Param: value=other_doc_name",
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
                "// Esquisses\n" +
                "int skA = TopSolidHost.Sketches2D.GetSketches(docIdA).Count;\n" +
                "int skB = TopSolidHost.Sketches2D.GetSketches(docIdB).Count;\n" +
                "sb.AppendLine((skA==skB?\"  =\":\"  *\") + \" Esquisses: \" + skA + \" vs \" + skB);\n" +
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

            { "copy_parameters_to", RW("Copies parameter values from the current document to another. Param: value=target_doc_name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (srcDocId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) { __message = \"Document '{value}' not found.\"; return; }\n" +
                "// Lire les params source\n" +
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
                "// Appliquer sur le doc cible\n" +
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

            { "copy_pdm_properties_to", RW("Copies designation/reference/manufacturer from current to another document. Param: value=target_doc_name",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (srcDocId.IsEmpty) { __message = \"No document open.\"; return; }\n" +
                "PdmObjectId srcPdmId = TopSolidHost.Documents.GetPdmObject(srcDocId);\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) { __message = \"Document '{value}' not found.\"; return; }\n" +
                "PdmObjectId tgtPdmId = results[0];\n" +
                "// Lire source\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(srcPdmId);\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(srcPdmId);\n" +
                "string mfr = TopSolidHost.Pdm.GetManufacturer(srcPdmId);\n" +
                "string mfrPn = TopSolidHost.Pdm.GetManufacturerPartNumber(srcPdmId);\n" +
                "// Ecrire cible\n" +
                "TopSolidHost.Pdm.SetDescription(tgtPdmId, desc);\n" +
                "TopSolidHost.Pdm.SetPartNumber(tgtPdmId, pn);\n" +
                "TopSolidHost.Pdm.SetManufacturer(tgtPdmId, mfr);\n" +
                "TopSolidHost.Pdm.SetManufacturerPartNumber(tgtPdmId, mfrPn);\n" +
                "string tgtName = TopSolidHost.Pdm.GetName(tgtPdmId);\n" +
                "__message = \"OK: PDM properties copied to '\" + tgtName + \"' (designation=\" + desc + \", ref=\" + pn + \", manufacturer=\" + mfr + \").\";") },

            { "batch_export_step", R("Exports ALL project parts to STEP in a folder. Param: value=folder_path",
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

            { "batch_read_property", R("Reads a specific property across all project documents. Param: value=property_name",
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

            { "find_modified_documents", R("Lists unsaved (dirty) documents of the project",
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

            { "batch_clear_author", RW("Clears the Author field on all project documents",
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

            { "batch_set_designation", RW("Sets Designation (Description) on ALL documents of the current project. Param: value=new_designation",
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

            { "batch_set_reference", RW("Sets Reference (PartNumber) on ALL documents of the current project. Param: value=new_reference",
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

            { "batch_set_manufacturer", RW("Sets Manufacturer on ALL documents of the current project. Param: value=new_manufacturer",
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

            { "clear_document_author", RW("Clears the Author field on the current document",
                "TopSolidHost.Pdm.SetAuthor(pdmId, \"\");\n" +
                "__message = \"OK: Author cleared on current document.\";") },

            { "batch_check_virtual", R("Checks the virtual property (IsVirtualDocument) on all project documents",
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

            { "batch_enable_virtual", RW("Enables virtual mode on ALL non-virtual project documents",
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

            { "enable_virtual_document", RW("Enables virtual mode on the current document",
                "TopSolidHost.Documents.SetVirtualDocumentMode(docId, true);\n" +
                "__message = \"OK: Virtual mode enabled on current document.\";") },

            { "check_family_drivers", R("Checks that family drivers have a designation. Lists those without.",
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

            { "fix_family_drivers", RW("Assigns a designation to family drivers that lack one (inferred from parameter name)",
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
                "        // Deduire designation du nom: CamelCase split + espaces\n" +
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

            { "batch_check_family_drivers", R("Checks drivers of all families in the project",
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

            { "audit_parameter_names", R("Audits parameter name syntax: detects convention inconsistencies and near-duplicates",
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
                "// Liste tous les noms pour inspection visuelle par le LLM\n" +
                "sb.AppendLine(\"\\nListe complete:\");\n" +
                "foreach (var n in names) sb.AppendLine(\"  \" + n);\n" +
                "return sb.ToString();") },

            { "batch_audit_parameter_names", R("Audits parameter name syntax across all project documents",
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
                "// Trouver les variantes (meme nom, casse differente)\n" +
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
                "// Lister tous les noms pour inspection par le LLM (fautes de frappe)\n" +
                "sb.AppendLine(\"\\nTous les noms de parametres du projet:\");\n" +
                "foreach (var kvp in allNames)\n" +
                "    sb.AppendLine(\"  \" + kvp.Value[0]);\n" +
                "return sb.ToString();") },

            { "batch_audit_driver_designations", R("Lists driver designations of all project families for inspection (typos, inconsistencies)",
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

            { "read_revision_history", R("Lists all major/minor revisions of the current document",
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

            { "compare_revisions", R("Compares parameters of the current revision with the previous one",
                "DocumentId curDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDocId.IsEmpty) return \"No document open.\";\n" +
                "PdmObjectId curPdmId = TopSolidHost.Documents.GetPdmObject(curDocId);\n" +
                "// Lire les parametres du document courant\n" +
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
                "// Trouver la revision precedente\n" +
                "var majors = TopSolidHost.Pdm.GetMajorRevisions(curPdmId);\n" +
                "if (majors.Count == 0) return \"No revision found.\";\n" +
                "var lastMajor = majors[majors.Count - 1];\n" +
                "var minors = TopSolidHost.Pdm.GetMinorRevisions(lastMajor);\n" +
                "if (minors.Count < 2) return \"Only one revision, nothing to compare.\";\n" +
                "var prevMinor = minors[minors.Count - 2];\n" +
                "// Ouvrir la revision precedente en lecture seule\n" +
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

            { "export_bom_csv", R("Exports the BOM as text (separated columns)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Unable to verify.\"; }\n" +
                "if (!isBom) return \"This document is not a BOM.\";\n" +
                "int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "// En-tetes\n" +
                "var headers = new System.Collections.Generic.List<string>();\n" +
                "for (int i = 0; i < colCount; i++)\n" +
                "    headers.Add(TopSolidDesignHost.Boms.GetColumnTitle(docId, i));\n" +
                "sb.AppendLine(string.Join(\";\", headers));\n" +
                "// Lignes\n" +
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

            { "check_missing_materials", R("Lists project parts without assigned material",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== VERIFICATION MATERIALS ===\");\n" +
                "int missing = 0; int total = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(item);\n" +
                "    try\n" +
                "    {\n" +
                "        DocumentId dId = TopSolidHost.Documents.GetDocument(item);\n" +
                "        if (dId.IsEmpty) continue;\n" +
                "        total++;\n" +
                "        var pList = TopSolidHost.Parameters.GetParameters(dId);\n" +
                "        double mass = 0;\n" +
                "        foreach (var p in pList)\n" +
                "        {\n" +
                "            if (TopSolidHost.Elements.GetFriendlyName(p) == \"Mass\")\n" +
                "            { mass = TopSolidHost.Parameters.GetRealValue(p); break; }\n" +
                "        }\n" +
                "        if (mass <= 0) { sb.AppendLine(\"  SANS MATERIAU: \" + name); missing++; }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.AppendLine(\"\\n\" + missing + \"/\" + total + \" part(s) without material.\");\n" +
                "return sb.ToString();") },

            { "assembly_mass_report", R("Reads total mass, volume, surface and part count of an assembly",
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

            { "read_material", R("Reads part material (mass and calculated density)",
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
            { "export_stl", R("Exports to STL (3D printing). Param: value=path",
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
            { "export_iges", R("Exports to IGES. Param: value=path",
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
            { "count_assembly_parts", R("Counts parts grouped by type with quantities",
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
            { "save_all_project", R("Saves all documents of the current project",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"No current project.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "int saved = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    try { TopSolidHost.Pdm.Save(item, true); saved++; } catch { continue; }\n" +
                "}\n" +
                "return \"OK: \" + saved + \"/\" + items.Count + \" documents saved.\";") },
            { "open_document_by_name", R("Searches and opens a document by name. Param: value=name",
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
            { "list_folder_documents", R("Lists documents of a specific project folder. Param: value=folder_name",
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

            { "summarize_project", R("Project summary: document count by type, folders, size",
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

            { "list_documents_without_reference", R("Lists project documents without reference (empty part number)",
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

            { "list_documents_without_designation", R("Lists project documents without designation (empty description)",
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

            { "count_documents_by_type", R("Counts project documents grouped by type (.TopPrt, .TopAsm, .TopDft...)",
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

            { "search_parts_by_material", R("Lists parts with their material (via mass > 0). Param: value=optional filter",
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

            { "read_where_used", R("Finds where-used references of the current document in the project",
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
            // --- ATTRIBUTS (couleur, transparence, calque, visibilite) ---
            // TopSolid: clic droit → Attributs → Color / Transparency / Layer
            { "attr_read_all", R("Reads color, transparency, layer and visibility of all elements",
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

            { "attr_set_color", RW("Sets color. If 1 shape: direct. If multiple: asks selection. Param: value=R,G,B (e.g. 0,0,255)",
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

            { "attr_set_color_all", RW("Sets color on ALL elements. Param: value=R,G,B",
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

            { "attr_read_color", R("Reads element colors",
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
                "    else sb.AppendLine(name + \": (pas de couleur)\");\n" +
                "}\n" +
                "return sb.ToString();") },

            { "attr_set_transparency", RW("Sets transparency. If 1 shape: direct. If multiple: asks. Param: value=0.0 to 1.0",
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
                "__message = \"OK: transparence \" + transp.ToString(\"F1\") + \" sur \" + TopSolidHost.Elements.GetFriendlyName(target);") },

            { "attr_read_transparency", R("Reads element transparency",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    if (TopSolidHost.Elements.HasTransparency(s))\n" +
                "        sb.AppendLine(name + \": \" + TopSolidHost.Elements.GetTransparency(s).ToString(\"F2\"));\n" +
                "    else sb.AppendLine(name + \": (pas de transparence)\");\n" +
                "}\n" +
                "return sb.ToString();") },

            { "attr_list_layers", R("Lists document layers",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"No document open.\";\n" +
                "var layers = TopSolidHost.Layers.GetLayers(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Layers: \" + layers.Count);\n" +
                "foreach (var l in layers)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(l));\n" +
                "return sb.ToString();") },

            { "attr_assign_layer", RW("Assigns an element to a layer. Param: value=element_name:layer_name",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: element_name:layer_name\"; return; }\n" +
                "string elemName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string layerName = \"{value}\".Substring(idx + 1).Trim();\n" +
                "// Trouver le calque\n" +
                "var layers = TopSolidHost.Layers.GetLayers(docId);\n" +
                "ElementId layerId = ElementId.Empty;\n" +
                "foreach (var l in layers)\n" +
                "{\n" +
                "    if (TopSolidHost.Elements.GetFriendlyName(l).IndexOf(layerName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    { layerId = l; break; }\n" +
                "}\n" +
                "if (layerId.IsEmpty) { __message = \"Layer '\" + layerName + \"' not found.\"; return; }\n" +
                "// Trouver l'element\n" +
                "var elems = TopSolidHost.Elements.GetElements(docId);\n" +
                "foreach (var e in elems)\n" +
                "{\n" +
                "    if (TopSolidHost.Elements.GetFriendlyName(e).IndexOf(elemName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        TopSolidHost.Layers.SetLayer(e, layerId);\n" +
                "        __message = \"OK: \" + TopSolidHost.Elements.GetFriendlyName(e) + \" -> calque \" + layerName;\n" +
                "        return;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"Element '\" + elemName + \"' not found.\";") },

            { "attr_replace_color", RW("Replaces a color with another on elements. Param: value=R1,G1,B1:R2,G2,B2 (e.g. 0,128,0:255,0,0 = green->red)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length != 2) { __message = \"Format: R1,G1,B1:R2,G2,B2 (e.g. 0,128,0:255,0,0)\"; return; }\n" +
                "string[] src = parts[0].Split(',');\n" +
                "string[] dst = parts[1].Split(',');\n" +
                "if (src.Length != 3 || dst.Length != 3) { __message = \"Format: R1,G1,B1:R2,G2,B2\"; return; }\n" +
                "int sr, sg, sb2, dr, dg, db;\n" +
                "if (!int.TryParse(src[0].Trim(), out sr) || !int.TryParse(src[1].Trim(), out sg) || !int.TryParse(src[2].Trim(), out sb2) ||\n" +
                "    !int.TryParse(dst[0].Trim(), out dr) || !int.TryParse(dst[1].Trim(), out dg) || !int.TryParse(dst[2].Trim(), out db))\n" +
                "{ __message = \"Invalid RGB values.\"; return; }\n" +
                "// Chercher dans les shapes du document\n" +
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
                "// Chercher aussi dans les operations (inclusions dans un assemblage)\n" +
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
            { "select_shape", R("Asks the user to select a shape and returns its info",
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

            { "select_face", R("Asks the user to select a face and returns its info",
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
            { "get_face_cone_length", R("Gets the length of a selected cone face (mm). Requires a cone face selection.",
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

            { "get_face_cone_radius", R("Gets the base radius of a selected cone face (mm). Requires a cone face selection.",
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

            { "get_face_cone_semi_angle", R("Gets the half-angle of a selected cone face (degrees). Requires a cone face selection.",
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

            { "get_face_torus_major_radius", R("Gets the major radius of a selected torus face (mm). Requires a torus face selection.",
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

            { "get_face_torus_minor_radius", R("Gets the minor radius of a selected torus face (mm). Requires a torus face selection.",
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

            { "get_item_last_operation_name", R("Gets the name of the last operation that produced a selected face.",
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

            { "select_3d_point", R("Asks the user to click a 3D point and returns coordinates",
                "SmartPoint3D selected;\n" +
                "UserQuestion q = new UserQuestion(\"Click a 3D point\", \"\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskPoint3D(q, default(SmartPoint3D), out selected);\n" +
                "if (answer != UserAnswerType.Ok) return \"Selection cancelled.\";\n" +
                "return \"Point: (\" + (selected.X * 1000).ToString(\"F2\") + \", \" + (selected.Y * 1000).ToString(\"F2\") + \", \" + (selected.Z * 1000).ToString(\"F2\") + \") mm\";") },

            { "attr_read_face_colors", R("Reads individual face colors",
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
        };

        // Shortcut factory methods for readability
        private static RecipeEntry R(string desc, string code) { return new RecipeEntry(desc, code, false); }
        private static RecipeEntry RW(string desc, string code) { return new RecipeEntry(desc, code, true); }

        public RecipeTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            var recipeNames = new List<string>(Recipes.Keys);

            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_run_recipe",
                Description = "Executes a pre-built TopSolid recipe. " +
                    "Do NOT write code — pick a recipe name. " +
                    "Recipes: " + string.Join(", ", recipeNames) + ". " +
                    "Parameter 'value' for parameterized recipes.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["recipe"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Recipe name: " + string.Join(", ", recipeNames)
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
            string recipeName = arguments["recipe"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(recipeName))
                return "Error: 'recipe' required. Available: " + string.Join(", ", Recipes.Keys);

            if (!Recipes.TryGetValue(recipeName, out var recipe))
                return "Unknown recipe: '" + recipeName + "'. Available: " + string.Join(", ", Recipes.Keys);

            string code = recipe.Code;

            // Substitute {value} placeholder — escape for C# string literal
            string value = arguments["value"]?.ToString() ?? "";
            code = code.Replace("{value}", value.Replace("\\", "\\\\").Replace("\"", "\\\""));

            // Ensure connector is initialized and connected (auto-reconnect if needed)
            var connector = _connectorProvider();
            if (connector == null)
                return "Error: TopSolid connector not initialized.";
            if (!connector.EnsureConnected())
                return "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled (port 8090). Use the tray icon to reconnect.";

            if (recipe.IsModification)
                return ScriptExecutor.ExecuteModification(code);
            else
                return ScriptExecutor.Execute(code);
        }

        /// <summary>
        /// Returns the recipe definition for a given name, or null if not found.
        /// Used by GetRecipeTool to expose recipe code as API reference for
        /// developers writing standalone C# TopSolid apps.
        /// </summary>
        public static RecipeEntry GetRecipe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            Recipes.TryGetValue(name.Trim(), out var entry);
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
        /// A recipe definition: human description + C# body + transactional mode flag.
        /// </summary>
        public class RecipeEntry
        {
            public string Description { get; }
            public string Code { get; }
            public bool IsModification { get; }

            public RecipeEntry(string description, string code, bool isModification)
            {
                Description = description;
                Code = code;
                IsModification = isModification;
            }
        }
    }
}
