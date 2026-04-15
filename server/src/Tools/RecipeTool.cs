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
            // PROPRIETES PDM — Lecture
            // =====================================================================
            { "lire_designation", R("Lit la designation du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Designation: (vide)\" : \"Designation: \" + val;") },
            { "lire_nom", R("Lit le nom du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "return \"Nom: \" + TopSolidHost.Pdm.GetName(pdmId);") },
            { "lire_reference", R("Lit la reference (part number) du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Reference: (vide)\" : \"Reference: \" + val;") },
            { "lire_fabricant", R("Lit le fabricant du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Fabricant: (vide)\" : \"Fabricant: \" + val;") },
            { "lire_proprietes_pdm", R("Lit toutes les proprietes PDM (nom, designation, reference, fabricant)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Nom: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "sb.AppendLine(\"Designation: \" + (string.IsNullOrEmpty(desc) ? \"(vide)\" : desc));\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "sb.AppendLine(\"Reference: \" + (string.IsNullOrEmpty(pn) ? \"(vide)\" : pn));\n" +
                "string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "sb.AppendLine(\"Fabricant: \" + (string.IsNullOrEmpty(mfr) ? \"(vide)\" : mfr));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // PROPRIETES PDM — Modification
            // =====================================================================
            { "modifier_designation", R("Modifie la designation. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetDescription(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Designation → {value}\";") },
            { "modifier_nom", R("Modifie le nom. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetName(pdmId, \"{value}\");\n" +
                "return \"OK: Nom → {value}\";") },
            { "modifier_reference", R("Modifie la reference. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetPartNumber(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Reference → {value}\";") },
            { "modifier_fabricant", R("Modifie le fabricant. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetManufacturer(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Fabricant → {value}\";") },

            // =====================================================================
            // PROJETS & NAVIGATION PDM
            // =====================================================================
            { "lire_projet_courant", R("Retourne le projet courant",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "return \"Projet: \" + TopSolidHost.Pdm.GetName(projId);") },
            { "lire_contenu_projet", R("Liste dossiers, sous-dossiers et documents du projet courant (arborescence complete)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Projet: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "int totalDocs = 0; int totalFolders = 0;\n" +
                "Action<PdmObjectId, string> listRecursive = null;\n" +
                "listRecursive = (parentId, indent) => {\n" +
                "    List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "    TopSolidHost.Pdm.GetConstituents(parentId, out folders, out docs);\n" +
                "    foreach (var f in folders) {\n" +
                "        totalFolders++;\n" +
                "        sb.AppendLine(indent + \"[Dossier] \" + TopSolidHost.Pdm.GetName(f));\n" +
                "        listRecursive(f, indent + \"  \");\n" +
                "    }\n" +
                "    foreach (var d in docs) {\n" +
                "        totalDocs++;\n" +
                "        sb.AppendLine(indent + TopSolidHost.Pdm.GetName(d));\n" +
                "    }\n" +
                "};\n" +
                "listRecursive(projId, \"  \");\n" +
                "sb.Insert(sb.ToString().IndexOf('\\n') + 1, \"(\" + totalFolders + \" dossiers, \" + totalDocs + \" documents)\\n\");\n" +
                "return sb.ToString();") },
            { "chercher_document", R("Cherche un document par nom (CONTAINS). Param: value",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Recherche '\" + \"{value}\" + \"': \" + results.Count + \" resultats\");\n" +
                "foreach (var r in results)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(r);\n" +
                "    sb.AppendLine(\"  \" + name);\n" +
                "}\n" +
                "return sb.ToString();") },
            { "chercher_dossier", R("Cherche un dossier par nom (CONTAINS). Param: value",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "var results = TopSolidHost.Pdm.SearchFolderByName(projId, \"{value}\");\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Recherche dossier '\" + \"{value}\" + \"': \" + results.Count + \" resultats\");\n" +
                "foreach (var r in results)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Pdm.GetName(r));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // DOCUMENT — Etat et operations
            // =====================================================================
            { "type_document", R("Detecte le type du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "sb.AppendLine(\"Nom: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string typeName = TopSolidHost.Documents.GetTypeFullName(docId);\n" +
                "sb.AppendLine(\"Type: \" + typeName);\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch {}\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch {}\n" +
                "bool isAssembly = false;\n" +
                "try { isAssembly = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch {}\n" +
                "if (isDrafting) sb.AppendLine(\"→ Mise en plan\");\n" +
                "else if (isBom) sb.AppendLine(\"→ Nomenclature\");\n" +
                "else if (isAssembly) sb.AppendLine(\"→ Assemblage\");\n" +
                "else sb.AppendLine(\"→ Piece\");\n" +
                "return sb.ToString();") },
            { "sauvegarder_document", R("Sauvegarde le document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Document sauvegarde.\";") },
            { "reconstruire_document", RW("Reconstruit le document actif",
                "TopSolidHost.Documents.Rebuild(docId);\n" +
                "__message = \"OK: Document reconstruit.\";") },

            // =====================================================================
            // PARAMETRES — Lecture
            // =====================================================================
            { "lire_parametres", R("Liste tous les parametres du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Parametres: \" + pList.Count);\n" +
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
            { "lire_parametre_reel", R("Lit un parametre reel par nom. Param: value=nom",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "return \"Parametre '{value}' non trouve.\";") },
            { "lire_parametre_texte", R("Lit un parametre texte par nom. Param: value=nom",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "        return name + \" = \" + TopSolidHost.Parameters.GetTextValue(p);\n" +
                "}\n" +
                "return \"Parametre '{value}' non trouve.\";") },

            // =====================================================================
            // PARAMETRES — Modification
            // =====================================================================
            { "modifier_parametre_reel", RW("Modifie un parametre reel. Param: value=nom:valeurSI (ex: Longueur:0.15)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length != 2) return \"Format: nom:valeurSI (ex: Longueur:0.15)\";\n" +
                "string pName = parts[0].Trim();\n" +
                "double newVal;\n" +
                "if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out newVal))\n" +
                "    return \"Valeur invalide: \" + parts[1];\n" +
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
                "__message = \"Parametre '\" + pName + \"' non trouve.\";") },
            { "modifier_parametre_texte", RW("Modifie un parametre texte. Param: value=nom:valeur",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: nom:valeur\"; return; }\n" +
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
                "__message = \"Parametre '\" + pName + \"' non trouve.\";") },

            // =====================================================================
            // GEOMETRIE — Lecture
            // =====================================================================
            { "lire_points_3d", R("Liste les points 3D du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var points = TopSolidHost.Geometries3D.GetPoints(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Points 3D: \" + points.Count);\n" +
                "foreach (var pt in points)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(pt);\n" +
                "    Point3D geom = TopSolidHost.Geometries3D.GetPointGeometry(pt);\n" +
                "    sb.AppendLine(\"  \" + name + \" (\" + (geom.X*1000).ToString(\"F1\") + \", \" + (geom.Y*1000).ToString(\"F1\") + \", \" + (geom.Z*1000).ToString(\"F1\") + \") mm\");\n" +
                "}\n" +
                "return sb.ToString();") },
            { "lire_reperes_3d", R("Liste les reperes 3D du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var frames = TopSolidHost.Geometries3D.GetFrames(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Reperes 3D: \" + frames.Count);\n" +
                "foreach (var f in frames)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(f));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // ESQUISSES — Lecture
            // =====================================================================
            { "lister_esquisses", R("Liste les esquisses du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var sketches = TopSolidHost.Sketches2D.GetSketches(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Esquisses: \" + sketches.Count);\n" +
                "foreach (var s in sketches)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(s));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // SHAPES — Lecture
            // =====================================================================
            { "lire_shapes", R("Liste les shapes du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
            { "lire_operations", R("Liste les operations (arbre de construction)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
            // ASSEMBLAGES — Lecture
            // =====================================================================
            { "detecter_assemblage", R("Detecte si le document est un assemblage et liste les pieces",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Impossible de verifier (pas un document Design).\"; }\n" +
                "if (!isAsm) return \"Ce document n'est PAS un assemblage.\";\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Assemblage: \" + parts.Count + \" pieces\");\n" +
                "foreach (var p in parts)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(p));\n" +
                "return sb.ToString();") },
            { "lister_inclusions", R("Liste les inclusions d'un assemblage",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
            // FAMILLES — Lecture
            // =====================================================================
            { "detecter_famille", R("Detecte si le document est une famille",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isFamily = TopSolidHost.Families.IsFamily(docId);\n" +
                "if (!isFamily) return \"Ce document n'est PAS une famille.\";\n" +
                "bool isExplicit = TopSolidHost.Families.IsExplicit(docId);\n" +
                "return \"Famille detectee (\" + (isExplicit ? \"explicite\" : \"implicite\") + \").\";") },
            { "lire_codes_famille", R("Lit les codes d'une famille",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "if (!TopSolidHost.Families.IsFamily(docId)) return \"Pas une famille.\";\n" +
                "var codes = TopSolidHost.Families.GetCodes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Codes: \" + codes.Count);\n" +
                "foreach (var c in codes)\n" +
                "    sb.AppendLine(\"  \" + c);\n" +
                "return sb.ToString();") },

            // =====================================================================
            // MISE EN PLAN — Lecture
            // =====================================================================
            { "ouvrir_mise_en_plan", R("Cherche et ouvre la mise en plan associee a la piece/assemblage courant via back-references PDM",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "        return \"Mise en plan ouverte: \" + name;\n" +
                "    }\n" +
                "}\n" +
                "return \"Aucune mise en plan trouvee pour ce document.\";") },

            { "detecter_mise_en_plan", R("Detecte si le document est une mise en plan et donne les infos",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isDrafting) return \"Ce document n'est PAS une mise en plan.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Mise en plan detectee.\");\n" +
                "int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);\n" +
                "sb.AppendLine(\"Pages: \" + pages);\n" +
                "string format = TopSolidDraftingHost.Draftings.GetDraftingFormatName(docId);\n" +
                "sb.AppendLine(\"Format: \" + format);\n" +
                "return sb.ToString();") },
            { "lister_vues_mise_en_plan", R("Liste les vues d'une mise en plan",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Pas une mise en plan.\"; }\n" +
                "if (!isDrafting) return \"Pas une mise en plan.\";\n" +
                "var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Vues: \" + views.Count);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string title = TopSolidDraftingHost.Draftings.GetViewTitle(v);\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(v);\n" +
                "    sb.AppendLine(\"  \" + name + \" - \" + title);\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // NOMENCLATURE — Lecture
            // =====================================================================
            { "detecter_nomenclature", R("Detecte si le document est une nomenclature",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isBom) return \"Ce document n'est PAS une nomenclature.\";\n" +
                "int cols = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "return \"Nomenclature detectee (\" + cols + \" colonnes).\";") },
            { "lire_colonnes_nomenclature", R("Lit les colonnes d'une nomenclature",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Pas une nomenclature.\"; }\n" +
                "if (!isBom) return \"Pas une nomenclature.\";\n" +
                "int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Colonnes: \" + colCount);\n" +
                "for (int i = 0; i < colCount; i++)\n" +
                "{\n" +
                "    string title = TopSolidDesignHost.Boms.GetColumnTitle(docId, i);\n" +
                "    bool visible = TopSolidDesignHost.Boms.IsColumnVisible(docId, i);\n" +
                "    sb.AppendLine(\"  [\" + i + \"] \" + title + (visible ? \"\" : \" (masquee)\"));\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // MISE EN PLAN — Recettes avancees (M-58)
            // =====================================================================
            { "lire_echelle_mise_en_plan", R("Lit l'echelle globale et par vue d'une mise en plan",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Pas une mise en plan.\"; }\n" +
                "if (!isDrafting) return \"Pas une mise en plan.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "double globalScale = TopSolidDraftingHost.Draftings.GetScaleFactorParameterValue(docId);\n" +
                "sb.AppendLine(\"Echelle globale: 1:\" + (1.0/globalScale).ToString(\"F0\"));\n" +
                "var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(v);\n" +
                "    bool isRel; double rel; double abs; double refVal;\n" +
                "    TopSolidDraftingHost.Draftings.GetViewScaleFactor(v, out isRel, out rel, out abs, out refVal);\n" +
                "    sb.AppendLine(\"  \" + name + \": 1:\" + (1.0/abs).ToString(\"F0\") + (isRel ? \" (relative)\" : \"\"));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "lire_format_mise_en_plan", R("Lit le format (taille, marges) d'une mise en plan",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Pas une mise en plan.\"; }\n" +
                "if (!isDrafting) return \"Pas une mise en plan.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "string format = TopSolidDraftingHost.Draftings.GetDraftingFormatName(docId);\n" +
                "sb.AppendLine(\"Format: \" + format);\n" +
                "double w; double h;\n" +
                "TopSolidDraftingHost.Draftings.GetDraftingFormatDimensions(docId, out w, out h);\n" +
                "sb.AppendLine(\"Dimensions: \" + (w*1000).ToString(\"F0\") + \" x \" + (h*1000).ToString(\"F0\") + \" mm\");\n" +
                "int pages = TopSolidDraftingHost.Draftings.GetPageCount(docId);\n" +
                "sb.AppendLine(\"Pages: \" + pages);\n" +
                "var mode = TopSolidDraftingHost.Draftings.GetProjectionMode(docId);\n" +
                "sb.AppendLine(\"Mode projection: \" + mode);\n" +
                "return sb.ToString();") },

            { "lire_projection_principale", R("Lit la projection principale d'une mise en plan",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); } catch { return \"Pas une mise en plan.\"; }\n" +
                "if (!isDrafting) return \"Pas une mise en plan.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "DocumentId mainDocId; ElementId repId;\n" +
                "TopSolidDraftingHost.Draftings.GetMainProjectionSet(docId, out mainDocId, out repId);\n" +
                "if (!mainDocId.IsEmpty)\n" +
                "{\n" +
                "    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(mainDocId);\n" +
                "    string srcName = TopSolidHost.Pdm.GetName(pdm);\n" +
                "    sb.AppendLine(\"Piece source: \" + srcName);\n" +
                "}\n" +
                "var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);\n" +
                "sb.AppendLine(\"Vues: \" + views.Count);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string title = TopSolidDraftingHost.Draftings.GetViewTitle(v);\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(v);\n" +
                "    ElementId mainView = TopSolidDraftingHost.Draftings.GetMainView(v);\n" +
                "    bool isMain = (mainView.Equals(v));\n" +
                "    sb.AppendLine(\"  \" + name + (isMain ? \" [PRINCIPALE]\" : \" [auxiliaire]\") + \" - \" + title);\n" +
                "}\n" +
                "return sb.ToString();") },

            // =====================================================================
            // NOMENCLATURE — Recettes avancees (M-58)
            // =====================================================================
            { "lire_contenu_nomenclature", R("Lit le contenu complet d'une nomenclature (lignes et cellules)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Pas une nomenclature.\"; }\n" +
                "if (!isBom) return \"Pas une nomenclature.\";\n" +
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

            { "compter_lignes_nomenclature", R("Compte les lignes actives d'une nomenclature",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Pas une nomenclature.\"; }\n" +
                "if (!isBom) return \"Pas une nomenclature.\";\n" +
                "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n" +
                "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n" +
                "int active = 0; int inactive = 0;\n" +
                "foreach (int rowId in children)\n" +
                "{\n" +
                "    if (TopSolidDesignHost.Boms.IsRowActive(docId, rowId)) active++;\n" +
                "    else inactive++;\n" +
                "}\n" +
                "return \"Lignes actives: \" + active + \", inactives: \" + inactive + \", total: \" + (active+inactive);") },

            // =====================================================================
            // MISE A PLAT — Depliage tolerie (M-58)
            // =====================================================================
            { "detecter_mise_a_plat", R("Detecte si le document est une mise a plat (depliage tolerie)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isUnfolding = false;\n" +
                "try { isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isUnfolding) return \"Ce document n'est PAS une mise a plat.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Mise a plat (depliage) detectee.\");\n" +
                "DocumentId partDoc; ElementId repId; ElementId shapeId;\n" +
                "TopSolidDesignHost.Unfoldings.GetPartToUnfold(docId, out partDoc, out repId, out shapeId);\n" +
                "if (!partDoc.IsEmpty)\n" +
                "{\n" +
                "    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(partDoc);\n" +
                "    sb.AppendLine(\"Piece source: \" + TopSolidHost.Pdm.GetName(pdm));\n" +
                "}\n" +
                "return sb.ToString();") },

            { "lire_plis_depliage", R("Liste les plis d'un depliage (angles, rayons, longueurs)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isUnfolding = false;\n" +
                "try { isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); } catch { return \"Pas une mise a plat.\"; }\n" +
                "if (!isUnfolding) return \"Pas une mise a plat.\";\n" +
                "List<TopSolid.Cad.Design.DB.Documents.BendFeature> bends;\n" +
                "TopSolidDesignHost.Unfoldings.GetBendFeatures(docId, out bends);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Plis: \" + bends.Count);\n" +
                "foreach (var b in bends)\n" +
                "    sb.AppendLine(\"  Pli: angle=\" + (b.Angle*180/3.14159).ToString(\"F1\") + \"deg, rayon=\" + (b.Radius*1000).ToString(\"F2\") + \"mm, longueur=\" + (b.Length*1000).ToString(\"F2\") + \"mm\");\n" +
                "return sb.ToString();") },

            { "lire_dimensions_depliage", R("Lit les dimensions de depliage depuis les proprietes systeme (tolerie)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== DEPLIAGE ===\");\n" +
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
                "if (sb.Length <= 18) return \"Pas de proprietes depliage trouvees.\";\n" +
                "return sb.ToString();") },

            // =====================================================================
            // EXPORT
            // =====================================================================
            { "lister_exporteurs", R("Liste tous les exporteurs disponibles",
                "var sb = new System.Text.StringBuilder();\n" +
                "int count = TopSolidHost.Application.ExporterCount;\n" +
                "sb.AppendLine(\"Exporteurs: \" + count);\n" +
                "for (int i = 0; i < count; i++)\n" +
                "{\n" +
                "    string typeName;\n" +
                "    string[] extensions;\n" +
                "    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);\n" +
                "    sb.AppendLine(i + \": \" + typeName + \" [\" + string.Join(\", \", extensions) + \"]\");\n" +
                "}\n" +
                "return sb.ToString();") },
            { "exporter_step", R("Exporte en STEP. Param: value=chemin (ex: C:\\temp\\piece.stp)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (idx < 0) return \"Exporteur STEP non trouve.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.stp\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exporte en STEP → \" + path;") },
            { "exporter_dxf", R("Exporte en DXF. Param: value=chemin",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (idx < 0) return \"Exporteur DXF non trouve.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.dxf\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exporte en DXF → \" + path;") },
            { "exporter_pdf", R("Exporte en PDF. Param: value=chemin",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (idx < 0) return \"Exporteur PDF non trouve.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.pdf\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exporte en PDF → \" + path;") },

            // =====================================================================
            // PROPRIETES UTILISATEUR
            // =====================================================================
            { "lire_propriete_utilisateur", R("Lit une propriete utilisateur texte. Param: value=nom_propriete",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetTextUserProperty(pdmId, \"{value}\");\n" +
                "return string.IsNullOrEmpty(val) ? \"Propriete '{value}': (vide)\" : \"Propriete '{value}': \" + val;") },

            // =====================================================================
            // AUDIT PIECE — Scenarios composites haute valeur
            // =====================================================================
            { "audit_piece", R("Audit complet de la piece : proprietes, parametres, shapes, masse, volume",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== AUDIT PIECE ===\");\n" +
                "sb.AppendLine(\"Nom: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "sb.AppendLine(\"Designation: \" + (string.IsNullOrEmpty(desc) ? \"(VIDE!)\" : desc));\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "sb.AppendLine(\"Reference: \" + (string.IsNullOrEmpty(pn) ? \"(VIDE!)\" : pn));\n" +
                "sb.AppendLine(\"Type: \" + TopSolidHost.Documents.GetTypeFullName(docId));\n" +
                "// Parametres\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "sb.AppendLine(\"\\nParametres: \" + pList.Count);\n" +
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

            { "audit_assemblage", R("Audit complet de l'assemblage : pieces, inclusions, masse",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isAsm) return \"Ce document n'est PAS un assemblage.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== AUDIT ASSEMBLAGE ===\");\n" +
                "sb.AppendLine(\"Nom: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "sb.AppendLine(\"Designation: \" + (string.IsNullOrEmpty(desc) ? \"(VIDE!)\" : desc));\n" +
                "// Pieces\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "sb.AppendLine(\"\\nPieces: \" + parts.Count);\n" +
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

            { "verifier_piece", R("Verification qualite : designation, reference, materiau remplis ?",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== VERIFICATION QUALITE ===\");\n" +
                "int warnings = 0;\n" +
                "string desc = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "if (string.IsNullOrEmpty(desc)) { sb.AppendLine(\"ALERTE: Designation VIDE\"); warnings++; } else sb.AppendLine(\"OK: Designation = \" + desc);\n" +
                "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "if (string.IsNullOrEmpty(pn)) { sb.AppendLine(\"ALERTE: Reference VIDE\"); warnings++; } else sb.AppendLine(\"OK: Reference = \" + pn);\n" +
                "string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "if (string.IsNullOrEmpty(mfr)) sb.AppendLine(\"INFO: Fabricant non renseigne\");\n" +
                "// Parametres\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "sb.AppendLine(\"Parametres: \" + pList.Count);\n" +
                "if (pList.Count == 0) { sb.AppendLine(\"ALERTE: Aucun parametre\"); warnings++; }\n" +
                "// Shapes\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "if (shapes.Count == 0) { sb.AppendLine(\"ALERTE: Aucun shape (piece vide?)\"); warnings++; } else sb.AppendLine(\"OK: \" + shapes.Count + \" shape(s)\");\n" +
                "sb.AppendLine(\"\\n\" + (warnings == 0 ? \"RESULTAT: Piece OK\" : \"RESULTAT: \" + warnings + \" alerte(s)\"));\n" +
                "return sb.ToString();") },

            // =====================================================================
            // PERFORMANCE — Masse, volume, surface
            // =====================================================================
            { "lire_masse_volume", R("Lit masse, volume, surface depuis les proprietes systeme du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\" || name == \"Volume\" || name == \"Surface Area\")\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        if (name == \"Mass\") sb.AppendLine(\"Masse: \" + val.ToString(\"F3\") + \" kg\");\n" +
                "        else if (name == \"Volume\") sb.AppendLine(\"Volume: \" + (val * 1e9).ToString(\"F2\") + \" mm3\");\n" +
                "        else sb.AppendLine(\"Surface: \" + (val * 1e6).ToString(\"F2\") + \" mm2\");\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length == 0) return \"Aucune propriete physique trouvee.\";\n" +
                "return sb.ToString();") },

            { "lire_densite_materiau", R("Calcule la densite a partir de masse/volume du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "double mass = 0; double vol = 0;\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\") mass = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "    else if (name == \"Volume\") vol = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "}\n" +
                "if (vol > 0 && mass > 0) return \"Densite: \" + (mass / vol).ToString(\"F0\") + \" kg/m3 (masse=\" + mass.ToString(\"F3\") + \"kg, vol=\" + (vol*1e6).ToString(\"F2\") + \"cm3)\";\n" +
                "return \"Masse ou volume non disponible.\";") },

            // =====================================================================
            // INVOKE COMMAND — Appel de commandes menu TopSolid
            // =====================================================================
            { "invoquer_commande", R("Execute une commande menu TopSolid par nom. Param: value=nom_commande",
                "bool result = TopSolidHost.Application.InvokeCommand(\"{value}\");\n" +
                "return result ? \"OK: Commande '{value}' executee.\" : \"ERREUR: Commande '{value}' non trouvee ou echec.\";") },

            // =====================================================================
            // OCCURRENCES — Proprietes d'occurrence dans assemblages
            // =====================================================================
            { "lire_occurrences", R("Liste les occurrences d'un assemblage avec leur definition",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Pas un assemblage.\"; }\n" +
                "if (!isAsm) return \"Pas un assemblage.\";\n" +
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

            { "renommer_occurrence", RW("Renomme une occurrence. Param: value=ancien_nom:nouveau_nom",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: ancien_nom:nouveau_nom\"; return; }\n" +
                "string oldName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string newName = \"{value}\".Substring(idx + 1).Trim();\n" +
                "var parts = TopSolidDesignHost.Assemblies.GetParts(docId);\n" +
                "foreach (var p in parts)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    {\n" +
                "        TopSolidHost.Entities.SetFunctionOccurrenceName(p, newName);\n" +
                "        __message = \"OK: Occurrence '\" + name + \"' renommee en '\" + newName + \"'\";\n" +
                "        return;\n" +
                "    }\n" +
                "}\n" +
                "__message = \"Occurrence '\" + oldName + \"' non trouvee.\";") },

            // =====================================================================
            // PROPRIETES UTILISATEUR — Ecriture
            // =====================================================================
            { "modifier_propriete_utilisateur", R("Modifie une propriete utilisateur texte. Param: value=nom_propriete:valeur",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) return \"Format: nom_propriete:valeur\";\n" +
                "string propName = \"{value}\".Substring(0, idx).Trim();\n" +
                "string propVal = \"{value}\".Substring(idx + 1).Trim();\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetTextUserProperty(pdmId, propName, propVal);\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Propriete '\" + propName + \"' = '\" + propVal + \"'\";") },

            // =====================================================================
            // BOITE ENGLOBANTE / STOCK
            // =====================================================================
            { "lire_boite_englobante", R("Lit les dimensions de la boite englobante depuis les proprietes systeme",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Boite englobante:\");\n" +
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
                "if (sb.Length <= 22) return \"Aucune dimension de boite trouvee.\";\n" +
                "return sb.ToString();") },

            { "lire_dimensions_piece", R("Lit les dimensions (Height, Width, Length, Box Size) depuis les proprietes systeme",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (sb.Length <= 22) return \"Aucune dimension trouvee (pas une piece?)\";\n" +
                "return sb.ToString();") },

            { "lire_moments_inertie", R("Lit les moments principaux d'inertie depuis les proprietes systeme",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== MOMENTS D'INERTIE ===\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Principal X Moment\" || name == \"Principal Y Moment\" || name == \"Principal Z Moment\")\n" +
                "    {\n" +
                "        double val = TopSolidHost.Parameters.GetRealValue(p);\n" +
                "        sb.AppendLine(\"  \" + name + \": \" + val.ToString(\"F6\") + \" kg.mm2\");\n" +
                "    }\n" +
                "}\n" +
                "if (sb.Length <= 28) return \"Aucun moment d'inertie trouve.\";\n" +
                "return sb.ToString();") },

            // =====================================================================
            // BATCH — Operations sur le projet
            // =====================================================================
            { "lister_documents_projet", R("Liste TOUS les documents du projet avec designation et reference",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Projet: \" + TopSolidHost.Pdm.GetName(projId));\n" +
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

            { "verifier_projet", R("Verification qualite du projet entier : pieces sans designation/reference",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== VERIFICATION PROJET ===\");\n" +
                "sb.AppendLine(\"Projet: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "int total = 0; int alertes = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(item);\n" +
                "    string desc = TopSolidHost.Pdm.GetDescription(item);\n" +
                "    string pn = TopSolidHost.Pdm.GetPartNumber(item);\n" +
                "    total++;\n" +
                "    if (string.IsNullOrEmpty(desc) || string.IsNullOrEmpty(pn))\n" +
                "    {\n" +
                "        sb.AppendLine(\"  ALERTE: \" + name + (string.IsNullOrEmpty(desc) ? \" [designation vide]\" : \"\") + (string.IsNullOrEmpty(pn) ? \" [reference vide]\" : \"\"));\n" +
                "        alertes++;\n" +
                "    }\n" +
                "}\n" +
                "sb.AppendLine(\"\\nTotal: \" + total + \" documents, \" + alertes + \" alerte(s)\");\n" +
                "sb.AppendLine(alertes == 0 ? \"RESULTAT: Projet OK\" : \"RESULTAT: \" + alertes + \" document(s) a completer\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // MATERIAUX
            // =====================================================================
            { "comparer_parametres", R("Compare les parametres du document actif avec un autre. Param: value=nom_autre_document",
                "DocumentId curDoc = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDoc.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' non trouve.\";\n" +
                "DocumentId otherDocId = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "var paramsA = TopSolidHost.Parameters.GetParameters(curDoc);\n" +
                "var paramsB = TopSolidHost.Parameters.GetParameters(otherDocId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(curDoc));\n" +
                "string nameB = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "sb.AppendLine(\"=== COMPARAISON ===\");\n" +
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

            { "comparer_operations_documents", R("Compare les operations (arbre de construction) entre le doc courant et un autre. Param: value=nom_autre_doc",
                "DocumentId docIdA = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docIdA.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' non trouve.\";\n" +
                "DocumentId docIdB = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "var opsA = TopSolidHost.Operations.GetOperations(docIdA);\n" +
                "var opsB = TopSolidHost.Operations.GetOperations(docIdB);\n" +
                "string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docIdA));\n" +
                "string nameB = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== COMPARAISON OPERATIONS ===\");\n" +
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

            { "comparer_entites_documents", R("Compare les entites (shapes, esquisses, points, reperes) entre le doc courant et un autre. Param: value=nom_autre_doc",
                "DocumentId docIdA = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docIdA.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' non trouve.\";\n" +
                "DocumentId docIdB = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "string nameA = TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(docIdA));\n" +
                "string nameB = TopSolidHost.Pdm.GetName(results[0]);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== COMPARAISON ENTITES ===\");\n" +
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
                "sb.AppendLine((paA==paB?\"  =\":\"  *\") + \" Parametres: \" + paA + \" vs \" + paB);\n" +
                "return sb.ToString();") },

            { "reporter_parametres", RW("Copie les valeurs de parametres du doc courant vers un autre document. Param: value=nom_doc_cible",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (srcDocId.IsEmpty) { __message = \"Aucun document ouvert.\"; return; }\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) { __message = \"Document '{value}' non trouve.\"; return; }\n" +
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
                "__message = \"OK: \" + applied + \" parametres reportes sur '\" + tgtName + \"' (\" + skipped + \" ignores).\";") },

            { "reporter_proprietes_pdm", RW("Copie designation/reference/fabricant du doc courant vers un autre. Param: value=nom_doc_cible",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "DocumentId srcDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (srcDocId.IsEmpty) { __message = \"Aucun document ouvert.\"; return; }\n" +
                "PdmObjectId srcPdmId = TopSolidHost.Documents.GetPdmObject(srcDocId);\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) { __message = \"Document '{value}' non trouve.\"; return; }\n" +
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
                "__message = \"OK: Proprietes PDM reportees sur '\" + tgtName + \"' (designation=\" + desc + \", ref=\" + pn + \", fabricant=\" + mfr + \").\";") },

            { "exporter_batch_step", R("Exporte TOUTES les pieces du projet en STEP dans un dossier. Param: value=chemin_dossier",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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
                "if (stepIdx < 0) return \"Exporteur STEP non trouve.\";\n" +
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
                "return \"Export batch STEP: \" + exported + \" exportes, \" + skipped + \" ignores. Dossier: \" + outputDir;") },

            { "lire_propriete_batch", R("Lit une propriete specifique sur tous les documents du projet. Param: value=nom_propriete",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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
                "    catch { sb.AppendLine(\"  \" + name + \": (erreur)\"); }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "chercher_documents_modifies", R("Liste les documents non sauvegardes (dirty) du projet",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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
                "sb.Insert(0, \"Documents modifies (non sauvegardes): \" + dirty + \"/\" + docs.Count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "vider_auteur_batch", RW("Vide le champ Auteur de tous les documents du projet",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"Aucun projet courant.\"; return; }\n" +
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
                "__message = \"OK: Auteur vide sur \" + cleared + \"/\" + docs.Count + \" documents.\";") },

            { "vider_auteur_document", RW("Vide le champ Auteur du document courant",
                "TopSolidHost.Pdm.SetAuthor(pdmId, \"\");\n" +
                "__message = \"OK: Auteur vide sur le document courant.\";") },

            { "verifier_virtuel_batch", R("Verifie la propriete virtuel (IsVirtualDocument) sur tous les documents du projet",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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
                "sb.Insert(0, \"Virtuel: \" + virtuel + \", Non-virtuel: \" + nonVirtuel + \"/\" + docs.Count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "activer_virtuel_batch", RW("Active le mode virtuel sur TOUS les documents non-virtuels du projet",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) { __message = \"Aucun projet courant.\"; return; }\n" +
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
                "__message = \"OK: Mode virtuel active sur \" + activated + \" document(s).\";") },

            { "activer_virtuel_document", RW("Active le mode virtuel sur le document courant",
                "TopSolidHost.Documents.SetVirtualDocumentMode(docId, true);\n" +
                "__message = \"OK: Mode virtuel active sur le document courant.\";") },

            { "verifier_drivers_famille", R("Verifie que les drivers d'une famille ont une designation. Liste ceux sans designation.",
                "DocumentId famDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (famDocId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isFam = false;\n" +
                "try { isFam = TopSolidHost.Families.IsFamily(famDocId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isFam) return \"Ce document n'est PAS une famille.\";\n" +
                "var drivers = TopSolidHost.Families.GetCatalogColumnParameters(famDocId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== DRIVERS FAMILLE ===\");\n" +
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
                "sb.AppendLine(\"\\n\" + withDesc + \" avec designation, \" + withoutDesc + \" sans.\");\n" +
                "return sb.ToString();") },

            { "corriger_drivers_famille", RW("Affecte une designation aux drivers de famille qui n'en ont pas (deduit du nom du parametre)",
                "DocumentId famDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (famDocId.IsEmpty) { __message = \"Aucun document ouvert.\"; return; }\n" +
                "bool isFam = false;\n" +
                "try { isFam = TopSolidHost.Families.IsFamily(famDocId); } catch { __message = \"Impossible de verifier.\"; return; }\n" +
                "if (!isFam) { __message = \"Ce document n'est PAS une famille.\"; return; }\n" +
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
                "__message = \"OK: \" + fixed_ + \" drivers corriges avec designation deduite du nom.\";") },

            { "verifier_drivers_famille_batch", R("Verifie les drivers de toutes les familles du projet",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== AUDIT DRIVERS FAMILLES ===\");\n" +
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
                "            sb.AppendLine(\"  \" + famName + \": \" + missing + \"/\" + drivers.Count + \" sans designation\");\n" +
                "            totalMissing += missing;\n" +
                "        }\n" +
                "        else sb.AppendLine(\"  \" + famName + \": OK (\" + drivers.Count + \" drivers)\");\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.Insert(0, \"Familles: \" + famCount + \", Drivers sans designation: \" + totalMissing + \"\\n\");\n" +
                "return sb.ToString();") },

            { "auditer_noms_parametres", R("Audit la syntaxe des noms de parametres : detecte les incoherences de convention et les doublons proches",
                "DocumentId curDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDocId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(curDocId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== AUDIT NOMS PARAMETRES ===\");\n" +
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
                "sb.AppendLine(\"Parametres utilisateur: \" + names.Count);\n" +
                "sb.AppendLine(\"Conventions detectees:\");\n" +
                "if (camel > 0) sb.AppendLine(\"  CamelCase: \" + camel);\n" +
                "if (under > 0) sb.AppendLine(\"  underscore: \" + under);\n" +
                "if (space > 0) sb.AppendLine(\"  espaces: \" + space);\n" +
                "if (upper > 0) sb.AppendLine(\"  MAJUSCULES: \" + upper);\n" +
                "if (lower > 0) sb.AppendLine(\"  minuscules: \" + lower);\n" +
                "int conventions = (camel>0?1:0) + (under>0?1:0) + (space>0?1:0) + (upper>0?1:0) + (lower>0?1:0);\n" +
                "if (conventions > 1) sb.AppendLine(\"  *** ATTENTION: \" + conventions + \" conventions melangees ! ***\");\n" +
                "else sb.AppendLine(\"  Convention unique: OK\");\n" +
                "// Doublons proches (Levenshtein simple)\n" +
                "sb.AppendLine(\"\\nDoublons potentiels (casse differente):\");\n" +
                "int dupes = 0;\n" +
                "for (int i = 0; i < names.Count; i++)\n" +
                "    for (int j = i + 1; j < names.Count; j++)\n" +
                "        if (names[i].ToLower() == names[j].ToLower())\n" +
                "        { sb.AppendLine(\"  '\" + names[i] + \"' vs '\" + names[j] + \"'\"); dupes++; }\n" +
                "if (dupes == 0) sb.AppendLine(\"  Aucun doublon.\");\n" +
                "// Liste tous les noms pour inspection visuelle par le LLM\n" +
                "sb.AppendLine(\"\\nListe complete:\");\n" +
                "foreach (var n in names) sb.AppendLine(\"  \" + n);\n" +
                "return sb.ToString();") },

            { "auditer_noms_parametres_batch", R("Audit la syntaxe des noms de parametres sur tous les documents du projet",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== AUDIT NOMS PARAMETRES PROJET ===\");\n" +
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
                "sb.AppendLine(\"Noms uniques (hors systeme): \" + allNames.Count);\n" +
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
                "if (variants == 0) sb.AppendLine(\"  Aucune variante.\");\n" +
                "// Lister tous les noms pour inspection par le LLM (fautes de frappe)\n" +
                "sb.AppendLine(\"\\nTous les noms de parametres du projet:\");\n" +
                "foreach (var kvp in allNames)\n" +
                "    sb.AppendLine(\"  \" + kvp.Value[0]);\n" +
                "return sb.ToString();") },

            { "auditer_designations_drivers_batch", R("Liste les designations de drivers de toutes les familles du projet pour inspection (fautes, incoherences)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== AUDIT DESIGNATIONS DRIVERS ===\");\n" +
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
                "            sb.AppendLine(\"  \" + name + \" -> \" + (string.IsNullOrEmpty(desc) ? \"(VIDE)\" : desc));\n" +
                "        }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "lire_historique_revisions", R("Liste toutes les revisions majeures/mineures du document courant",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "var majors = TopSolidHost.Pdm.GetMajorRevisions(pdmId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== HISTORIQUE REVISIONS ===\");\n" +
                "sb.AppendLine(\"Document: \" + TopSolidHost.Pdm.GetName(pdmId));\n" +
                "sb.AppendLine(\"Revisions majeures: \" + majors.Count);\n" +
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

            { "comparer_revisions", R("Compare les parametres de la revision courante avec la precedente",
                "DocumentId curDocId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (curDocId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (majors.Count == 0) return \"Aucune revision trouvee.\";\n" +
                "var lastMajor = majors[majors.Count - 1];\n" +
                "var minors = TopSolidHost.Pdm.GetMinorRevisions(lastMajor);\n" +
                "if (minors.Count < 2) return \"Une seule revision, rien a comparer.\";\n" +
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
                "sb.AppendLine(\"=== COMPARAISON REVISIONS ===\");\n" +
                "string curMinText = TopSolidHost.Pdm.GetMinorRevisionText(minors[minors.Count - 1]);\n" +
                "string prevMinText = TopSolidHost.Pdm.GetMinorRevisionText(prevMinor);\n" +
                "sb.AppendLine(\"Rev .\" + prevMinText + \" vs Rev .\" + curMinText + \" (courante)\");\n" +
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
                "if (diffs == 0) sb.AppendLine(\"  Aucune difference de parametres.\");\n" +
                "else sb.AppendLine(\"\\n\" + diffs + \" difference(s)\");\n" +
                "return sb.ToString();") },

            { "exporter_nomenclature_csv", R("Exporte la nomenclature en format texte (colonnes separees)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isBom = false;\n" +
                "try { isBom = TopSolidDesignHost.Boms.IsBom(docId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isBom) return \"Ce document n'est pas une nomenclature.\";\n" +
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

            { "verifier_materiaux_manquants", R("Liste les pieces du projet sans materiau affecte",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== VERIFICATION MATERIAUX ===\");\n" +
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
                "sb.AppendLine(\"\\n\" + missing + \"/\" + total + \" piece(s) sans materiau.\");\n" +
                "return sb.ToString();") },

            { "rapport_masse_assemblage", R("Lit masse totale, volume, surface et nombre de pieces d'un assemblage",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Pas un assemblage.\"; }\n" +
                "if (!isAsm) return \"Pas un assemblage.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== RAPPORT MASSE ASSEMBLAGE ===\");\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    if (name == \"Mass\")\n" +
                "        sb.AppendLine(\"Masse totale: \" + TopSolidHost.Parameters.GetRealValue(p).ToString(\"F3\") + \" kg\");\n" +
                "    else if (name == \"Volume\")\n" +
                "        sb.AppendLine(\"Volume total: \" + (TopSolidHost.Parameters.GetRealValue(p) * 1e9).ToString(\"F2\") + \" mm3\");\n" +
                "    else if (name == \"Surface Area\")\n" +
                "        sb.AppendLine(\"Surface totale: \" + (TopSolidHost.Parameters.GetRealValue(p) * 1e6).ToString(\"F2\") + \" mm2\");\n" +
                "    else if (name == \"Part Count\")\n" +
                "    {\n" +
                "        var pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "        if (pType == ParameterType.Integer) sb.AppendLine(\"Nombre de pieces: \" + TopSolidHost.Parameters.GetIntegerValue(p));\n" +
                "        else sb.AppendLine(\"Nombre de pieces: \" + TopSolidHost.Parameters.GetRealValue(p).ToString(\"F0\"));\n" +
                "    }\n" +
                "}\n" +
                "return sb.ToString();") },

            { "lire_materiau", R("Lit le materiau de la piece (masse et densite calculee)",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "    sb.AppendLine(\"Materiau affecte.\");\n" +
                "    sb.AppendLine(\"Masse: \" + mass.ToString(\"F3\") + \" kg\");\n" +
                "    if (vol > 0) sb.AppendLine(\"Densite calculee: \" + (mass / vol).ToString(\"F0\") + \" kg/m3\");\n" +
                "}\n" +
                "else sb.AppendLine(\"Aucun materiau affecte (masse = 0).\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // EXPORT — Formats supplementaires
            // =====================================================================
            { "exporter_stl", R("Exporte en STL (impression 3D). Param: value=chemin",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (idx < 0) return \"Exporteur STL non trouve.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.stl\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exporte en STL → \" + path;") },
            { "exporter_iges", R("Exporte en IGES. Param: value=chemin",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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
                "if (idx < 0) return \"Exporteur IGES non trouve.\";\n" +
                "string path = \"{value}\";\n" +
                "if (string.IsNullOrEmpty(path)) path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"export.igs\");\n" +
                "TopSolidHost.Documents.Export(idx, docId, path);\n" +
                "return \"OK: Exporte en IGES → \" + path;") },

            // =====================================================================
            // ASSEMBLAGE — Comptage et diagnostic
            // =====================================================================
            { "compter_pieces_assemblage", R("Compte les pieces groupees par type avec quantites",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isAsm = false;\n" +
                "try { isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId); } catch { return \"Pas un assemblage.\"; }\n" +
                "if (!isAsm) return \"Pas un assemblage.\";\n" +
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
                "sb.AppendLine(\"=== COMPTAGE PIECES ===\");\n" +
                "int total = 0;\n" +
                "foreach (var kvp in counts)\n" +
                "{\n" +
                "    sb.AppendLine(\"  \" + kvp.Value + \"x \" + kvp.Key);\n" +
                "    total += kvp.Value;\n" +
                "}\n" +
                "sb.AppendLine(\"Total: \" + total + \" pieces (\" + counts.Count + \" references uniques)\");\n" +
                "return sb.ToString();") },

            // =====================================================================
            // PROJET — Operations batch
            // =====================================================================
            { "sauvegarder_tout_projet", R("Sauvegarde tous les documents du projet courant",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\nTopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\nvar items = docs;\n" +
                "int saved = 0;\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    try { TopSolidHost.Pdm.Save(item, true); saved++; } catch { continue; }\n" +
                "}\n" +
                "return \"OK: \" + saved + \"/\" + items.Count + \" documents sauvegardes.\";") },
            { "ouvrir_document_par_nom", R("Cherche et ouvre un document par nom. Param: value=nom",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "var results = TopSolidHost.Pdm.SearchDocumentByName(projId, \"{value}\");\n" +
                "if (results.Count == 0) return \"Document '{value}' non trouve.\";\n" +
                "DocumentId dId = TopSolidHost.Documents.GetDocument(results[0]);\n" +
                "TopSolidHost.Documents.Open(ref dId);\n" +
                "return \"OK: Document '\" + TopSolidHost.Pdm.GetName(results[0]) + \"' ouvert.\";") },

            // =====================================================================
            // BATCH — Recettes pour concepteurs de bibliotheque
            // =====================================================================
            { "lister_documents_dossier", R("Liste les documents d'un dossier specifique du projet. Param: value=nom_dossier",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "List<PdmObjectId> folders; List<PdmObjectId> docs;\n" +
                "TopSolidHost.Pdm.GetConstituents(projId, out folders, out docs);\n" +
                "PdmObjectId targetFolder = PdmObjectId.Empty;\n" +
                "foreach (var f in folders)\n" +
                "{\n" +
                "    if (TopSolidHost.Pdm.GetName(f).IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "    { targetFolder = f; break; }\n" +
                "}\n" +
                "if (targetFolder.IsEmpty) return \"Dossier '{value}' non trouve.\";\n" +
                "List<PdmObjectId> subFolders; List<PdmObjectId> subDocs;\n" +
                "TopSolidHost.Pdm.GetConstituents(targetFolder, out subFolders, out subDocs);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Dossier: \" + TopSolidHost.Pdm.GetName(targetFolder) + \" (\" + subDocs.Count + \" docs)\");\n" +
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

            { "resumer_projet", R("Resume du projet: nombre de documents par type, dossiers, taille",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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
                "sb.AppendLine(\"Projet: \" + TopSolidHost.Pdm.GetName(projId));\n" +
                "sb.AppendLine(\"Dossiers: \" + folders.Count);\n" +
                "sb.AppendLine(\"Documents: \" + docs.Count);\n" +
                "foreach (var kv in types)\n" +
                "    sb.AppendLine(\"  \" + kv.Key + \": \" + kv.Value);\n" +
                "return sb.ToString();") },

            { "lister_documents_sans_reference", R("Liste les documents du projet sans reference (part number vide)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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

            { "lister_documents_sans_designation", R("Liste les documents du projet sans designation (description vide)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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

            { "compter_documents_par_type", R("Compte les documents du projet groupes par type (.TopPrt, .TopAsm, .TopDft...)",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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

            { "chercher_pieces_par_materiau", R("Liste les pieces avec leur materiau (via masse > 0). Param: value=filtre optionnel",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
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
                "        string info = name + \" | masse=\" + mass.ToString(\"F3\") + \"kg\";\n" +
                "        if (\"{value}\" == \"\" || info.IndexOf(\"{value}\", StringComparison.OrdinalIgnoreCase) >= 0)\n" +
                "        { sb.AppendLine(\"  \" + info); count++; }\n" +
                "    }\n" +
                "    catch { continue; }\n" +
                "}\n" +
                "sb.Insert(0, \"Pieces trouvees: \" + count + \"\\n\");\n" +
                "return sb.ToString();") },

            { "lire_cas_emploi", R("Cherche les cas d'emploi (where-used) du document courant dans le projet",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);\n" +
                "PdmMajorRevisionId majorRev = TopSolidHost.Pdm.GetLastMajorRevision(pdmId);\n" +
                "var backRefs = TopSolidHost.Pdm.SearchMajorRevisionBackReferences(projId, majorRev);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Cas d'emploi: \" + backRefs.Count);\n" +
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
            // COULEURS — Lecture des faces
            // =====================================================================
            // --- ATTRIBUTS (couleur, transparence, calque, visibilite) ---
            // TopSolid: clic droit → Attributs → Color / Transparency / Layer
            { "attribut_lire_tout", R("Attribut: lit couleur, transparence, calque et visibilite de tous les elements",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"=== ATTRIBUTS ===\");\n" +
                "foreach (var s in shapes)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(s);\n" +
                "    sb.Append(name + \": \");\n" +
                "    if (TopSolidHost.Elements.HasColor(s))\n" +
                "    {\n" +
                "        Color c = TopSolidHost.Elements.GetColor(s);\n" +
                "        sb.Append(\"couleur=RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \") \");\n" +
                "    }\n" +
                "    if (TopSolidHost.Elements.HasTransparency(s))\n" +
                "        sb.Append(\"transparence=\" + TopSolidHost.Elements.GetTransparency(s).ToString(\"F2\") + \" \");\n" +
                "    sb.Append(\"visible=\" + TopSolidHost.Elements.IsVisible(s));\n" +
                "    try\n" +
                "    {\n" +
                "        ElementId layerId = TopSolidHost.Layers.GetLayer(docId, s);\n" +
                "        if (!layerId.IsEmpty) sb.Append(\" calque=\" + TopSolidHost.Elements.GetFriendlyName(layerId));\n" +
                "    } catch {}\n" +
                "    sb.AppendLine();\n" +
                "}\n" +
                "return sb.ToString();") },

            { "attribut_modifier_couleur", RW("Attribut: change la couleur. Si 1 shape → direct. Si plusieurs → demande selection. Param: value=R,G,B (ex: 0,0,255)",
                "string[] rgb = \"{value}\".Split(',');\n" +
                "if (rgb.Length != 3) { __message = \"Format: R,G,B (ex: 255,0,0 pour rouge)\"; return; }\n" +
                "int r, g, b;\n" +
                "if (!int.TryParse(rgb[0].Trim(), out r) || !int.TryParse(rgb[1].Trim(), out g) || !int.TryParse(rgb[2].Trim(), out b))\n" +
                "{ __message = \"Format: R,G,B (ex: 255,0,0)\"; return; }\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "ElementId target = ElementId.Empty;\n" +
                "if (shapes.Count == 1) { target = shapes[0]; }\n" +
                "else if (shapes.Count > 1)\n" +
                "{\n" +
                "    UserQuestion q = new UserQuestion(\"Plusieurs elements. Selectionnez celui a colorer\");\n" +
                "    UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out target);\n" +
                "    if (answer != UserAnswerType.Validated || target.IsEmpty) { __message = \"Selection annulee.\"; return; }\n" +
                "}\n" +
                "else { __message = \"Aucun shape dans le document.\"; return; }\n" +
                "TopSolidHost.Elements.SetColor(target, new Color((byte)r, (byte)g, (byte)b));\n" +
                "__message = \"OK: \" + TopSolidHost.Elements.GetFriendlyName(target) + \" → RGB(\" + r + \",\" + g + \",\" + b + \")\";") },

            { "attribut_modifier_couleur_tout", RW("Attribut: change la couleur de TOUS les elements. Param: value=R,G,B",
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

            { "attribut_lire_couleur", R("Attribut: lit la couleur des elements",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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

            { "attribut_modifier_transparence", RW("Attribut: change la transparence. Si 1 shape → direct. Si plusieurs → demande. Param: value=0.0 a 1.0",
                "double transp;\n" +
                "if (!double.TryParse(\"{value}\", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out transp))\n" +
                "{ __message = \"Format: nombre entre 0.0 et 1.0\"; return; }\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "ElementId target = ElementId.Empty;\n" +
                "if (shapes.Count == 1) { target = shapes[0]; }\n" +
                "else if (shapes.Count > 1)\n" +
                "{\n" +
                "    UserQuestion q = new UserQuestion(\"Selectionnez l'element\");\n" +
                "    UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out target);\n" +
                "    if (answer != UserAnswerType.Validated || target.IsEmpty) { __message = \"Selection annulee.\"; return; }\n" +
                "}\n" +
                "else { __message = \"Aucun shape.\"; return; }\n" +
                "TopSolidHost.Elements.SetTransparency(target, transp);\n" +
                "__message = \"OK: transparence \" + transp.ToString(\"F1\") + \" sur \" + TopSolidHost.Elements.GetFriendlyName(target);") },

            { "attribut_lire_transparence", R("Attribut: lit la transparence des elements",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
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

            { "attribut_lister_calques", R("Attribut: liste les calques (layers) du document",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var layers = TopSolidHost.Layers.GetLayers(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Calques: \" + layers.Count);\n" +
                "foreach (var l in layers)\n" +
                "    sb.AppendLine(\"  \" + TopSolidHost.Elements.GetFriendlyName(l));\n" +
                "return sb.ToString();") },

            { "attribut_affecter_calque", RW("Attribut: affecte un element a un calque. Param: value=nom_element:nom_calque",
                "int idx = \"{value}\".IndexOf(':');\n" +
                "if (idx < 0) { __message = \"Format: nom_element:nom_calque\"; return; }\n" +
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
                "if (layerId.IsEmpty) { __message = \"Calque '\" + layerName + \"' non trouve.\"; return; }\n" +
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
                "__message = \"Element '\" + elemName + \"' non trouve.\";") },

            { "attribut_remplacer_couleur", RW("Attribut: remplace une couleur par une autre sur les elements. Param: value=R1,G1,B1:R2,G2,B2 (ex: 0,128,0:255,0,0 = vert→rouge)",
                "string[] parts = \"{value}\".Split(':');\n" +
                "if (parts.Length != 2) { __message = \"Format: R1,G1,B1:R2,G2,B2 (ex: 0,128,0:255,0,0)\"; return; }\n" +
                "string[] src = parts[0].Split(',');\n" +
                "string[] dst = parts[1].Split(',');\n" +
                "if (src.Length != 3 || dst.Length != 3) { __message = \"Format: R1,G1,B1:R2,G2,B2\"; return; }\n" +
                "int sr, sg, sb2, dr, dg, db;\n" +
                "if (!int.TryParse(src[0].Trim(), out sr) || !int.TryParse(src[1].Trim(), out sg) || !int.TryParse(src[2].Trim(), out sb2) ||\n" +
                "    !int.TryParse(dst[0].Trim(), out dr) || !int.TryParse(dst[1].Trim(), out dg) || !int.TryParse(dst[2].Trim(), out db))\n" +
                "{ __message = \"Valeurs RGB invalides.\"; return; }\n" +
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
                "__message = changed + \" element(s) passe(s) de RGB(\" + sr + \",\" + sg + \",\" + sb2 + \") a RGB(\" + dr + \",\" + dg + \",\" + db + \")\";") },

            // --- Selection interactive (IUser.Ask*) ---
            { "selectionner_shape", R("Demande a l'utilisateur de selectionner un shape et retourne ses infos",
                "ElementId selected = ElementId.Empty;\n" +
                "UserQuestion q = new UserQuestion(\"Selectionnez un shape\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskShape(q, ElementId.Empty, out selected);\n" +
                "if (answer != UserAnswerType.Validated || selected.IsEmpty) return \"Selection annulee.\";\n" +
                "string name = TopSolidHost.Elements.GetFriendlyName(selected);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Element selectionne: \" + name);\n" +
                "if (TopSolidHost.Elements.HasColor(selected))\n" +
                "{\n" +
                "    Color c = TopSolidHost.Elements.GetColor(selected);\n" +
                "    sb.AppendLine(\"Couleur: RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \")\");\n" +
                "}\n" +
                "if (TopSolidHost.Elements.HasTransparency(selected))\n" +
                "    sb.AppendLine(\"Transparence: \" + TopSolidHost.Elements.GetTransparency(selected).ToString(\"F2\"));\n" +
                "sb.AppendLine(\"Visible: \" + TopSolidHost.Elements.IsVisible(selected));\n" +
                "sb.AppendLine(\"Type: \" + TopSolidHost.Elements.GetTypeFullName(selected));\n" +
                "return sb.ToString();") },

            { "selectionner_face", R("Demande a l'utilisateur de selectionner une face et retourne ses infos",
                "ElementItemId selected = default;\n" +
                "UserQuestion q = new UserQuestion(\"Selectionnez une face\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskFace(q, default, out selected);\n" +
                "if (answer != UserAnswerType.Validated) return \"Selection annulee.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "Color c = TopSolidHost.Shapes.GetFaceColor(selected);\n" +
                "sb.AppendLine(\"Couleur face: RGB(\" + c.R + \",\" + c.G + \",\" + c.B + \")\");\n" +
                "double area = TopSolidHost.Shapes.GetFaceArea(selected);\n" +
                "sb.AppendLine(\"Aire: \" + (area * 1e6).ToString(\"F2\") + \" cm2\");\n" +
                "int surfType = (int)TopSolidHost.Shapes.GetFaceSurfaceType(selected);\n" +
                "sb.AppendLine(\"Type surface: \" + surfType);\n" +
                "return sb.ToString();") },

            { "selectionner_point_3d", R("Demande a l'utilisateur de cliquer un point 3D et retourne les coordonnees",
                "SmartPoint3D selected;\n" +
                "UserQuestion q = new UserQuestion(\"Cliquez un point 3D\");\n" +
                "UserAnswerType answer = TopSolidHost.User.AskPoint3D(q, default, out selected);\n" +
                "if (answer != UserAnswerType.Validated) return \"Selection annulee.\";\n" +
                "return \"Point: (\" + (selected.X * 1000).ToString(\"F2\") + \", \" + (selected.Y * 1000).ToString(\"F2\") + \", \" + (selected.Z * 1000).ToString(\"F2\") + \") mm\";") },

            { "attribut_lire_couleurs_faces", R("Attribut: lit les couleurs de chaque face individuellement",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var shapes = TopSolidHost.Shapes.GetShapes(docId);\n" +
                "if (shapes.Count == 0) return \"Aucun shape.\";\n" +
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
                Description = "Execute une recette TopSolid pre-programmee. " +
                    "NE PAS ecrire de code — choisir le nom de recette. " +
                    "Recettes: " + string.Join(", ", recipeNames) + ". " +
                    "Parametre 'value' pour les recettes avec parametres.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["recipe"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Nom de la recette: " + string.Join(", ", recipeNames)
                        },
                        ["value"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Valeur pour les recettes parametrees (ex: nom, chemin, nom:valeur)"
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
                return "Erreur : 'recipe' requis. Disponibles: " + string.Join(", ", Recipes.Keys);

            if (!Recipes.TryGetValue(recipeName, out var recipe))
                return "Recette inconnue: '" + recipeName + "'. Disponibles: " + string.Join(", ", Recipes.Keys);

            string code = recipe.Code;

            // Substitute {value} placeholder — escape for C# string literal
            string value = arguments["value"]?.ToString() ?? "";
            code = code.Replace("{value}", value.Replace("\\", "\\\\").Replace("\"", "\\\""));

            // Ensure connector is initialized
            var connector = _connectorProvider();
            if (connector == null)
                return "Erreur : TopSolid non connecte.";

            if (recipe.IsModification)
                return ScriptExecutor.ExecuteModification(code);
            else
                return ScriptExecutor.Execute(code);
        }

        private class RecipeEntry
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
