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
                "return \"OK: Reference → {value}\";") },
            { "modifier_fabricant", R("Modifie le fabricant. Param: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetManufacturer(pdmId, \"{value}\");\n" +
                "return \"OK: Fabricant → {value}\";") },

            // =====================================================================
            // PROJETS & NAVIGATION PDM
            // =====================================================================
            { "lire_projet_courant", R("Retourne le projet courant",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "return \"Projet: \" + TopSolidHost.Pdm.GetName(projId);") },
            { "lire_contenu_projet", R("Liste dossiers et documents du projet courant",
                "PdmObjectId projId = TopSolidHost.Pdm.GetCurrentProject();\n" +
                "if (projId.IsEmpty) return \"Aucun projet courant.\";\n" +
                "var items = TopSolidHost.Pdm.GetConstituents(projId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Projet: \" + TopSolidHost.Pdm.GetName(projId) + \" (\" + items.Count + \" elements)\");\n" +
                "foreach (var item in items)\n" +
                "{\n" +
                "    string name = TopSolidHost.Pdm.GetName(item);\n" +
                "    sb.AppendLine(\"  \" + name);\n" +
                "}\n" +
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
                "try { isDrafting = TopSolidDesignHost.Draftings.IsDrafting(docId); } catch {}\n" +
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
            { "reconstruire_document", R("Reconstruit le document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "TopSolidHost.Documents.Rebuild(docId);\n" +
                "return \"OK: Document reconstruit.\";") },

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
                "    int pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    string val = \"\";\n" +
                "    if (pType == 0) val = TopSolidHost.Parameters.GetRealValue(p).ToString(\"F6\");\n" +
                "    else if (pType == 1) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();\n" +
                "    else if (pType == 3) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();\n" +
                "    else if (pType == 4) val = TopSolidHost.Parameters.GetTextValue(p);\n" +
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
            { "detecter_mise_en_plan", R("Detecte si le document est une mise en plan et donne les infos",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDesignHost.Draftings.IsDrafting(docId); } catch { return \"Impossible de verifier.\"; }\n" +
                "if (!isDrafting) return \"Ce document n'est PAS une mise en plan.\";\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Mise en plan detectee.\");\n" +
                "int pages = TopSolidDesignHost.Draftings.GetPageCount(docId);\n" +
                "sb.AppendLine(\"Pages: \" + pages);\n" +
                "string format = TopSolidDesignHost.Draftings.GetDraftingFormatName(docId);\n" +
                "sb.AppendLine(\"Format: \" + format);\n" +
                "return sb.ToString();") },
            { "lister_vues_mise_en_plan", R("Liste les vues d'une mise en plan",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "bool isDrafting = false;\n" +
                "try { isDrafting = TopSolidDesignHost.Draftings.IsDrafting(docId); } catch { return \"Pas une mise en plan.\"; }\n" +
                "if (!isDrafting) return \"Pas une mise en plan.\";\n" +
                "var views = TopSolidDesignHost.Draftings.GetDraftingViews(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Vues: \" + views.Count);\n" +
                "foreach (var v in views)\n" +
                "{\n" +
                "    string title = TopSolidDesignHost.Draftings.GetViewTitle(v);\n" +
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

            // Substitute {value} placeholder
            string value = arguments["value"]?.ToString() ?? "";
            code = code.Replace("{value}", value.Replace("\"", "\\\""));

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
