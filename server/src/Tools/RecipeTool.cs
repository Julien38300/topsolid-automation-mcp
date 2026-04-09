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
            // --- Proprietes PDM ---
            { "lire_designation", new RecipeEntry(
                "Lit la designation (description) du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetDescription(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Designation: (vide)\" : \"Designation: \" + val;",
                false) },
            { "lire_nom", new RecipeEntry(
                "Lit le nom du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "return \"Nom: \" + TopSolidHost.Pdm.GetName(pdmId);",
                false) },
            { "lire_reference", new RecipeEntry(
                "Lit la reference (part number) du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetPartNumber(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Reference: (vide)\" : \"Reference: \" + val;",
                false) },
            { "lire_fabricant", new RecipeEntry(
                "Lit le fabricant du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "string val = TopSolidHost.Pdm.GetManufacturer(pdmId);\n" +
                "return string.IsNullOrEmpty(val) ? \"Fabricant: (vide)\" : \"Fabricant: \" + val;",
                false) },
            { "lire_proprietes_pdm", new RecipeEntry(
                "Lit toutes les proprietes PDM (nom, designation, reference, fabricant)",
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
                "return sb.ToString();",
                false) },

            // --- Modification proprietes PDM (pas besoin de StartModification) ---
            { "modifier_designation", new RecipeEntry(
                "Modifie la designation du document actif. Parametre: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetDescription(pdmId, \"{value}\");\n" +
                "TopSolidHost.Pdm.Save(pdmId, true);\n" +
                "return \"OK: Designation modifiee et sauvegardee → {value}\";",
                false) },
            { "modifier_nom", new RecipeEntry(
                "Modifie le nom du document actif. Parametre: value",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);\n" +
                "TopSolidHost.Pdm.SetName(pdmId, \"{value}\");\n" +
                "return \"OK: Nom modifie → {value}\";",
                false) },

            // --- Parametres ---
            { "lire_parametres", new RecipeEntry(
                "Liste tous les parametres du document actif",
                "DocumentId docId = TopSolidHost.Documents.EditedDocument;\n" +
                "if (docId.IsEmpty) return \"Aucun document ouvert.\";\n" +
                "var pList = TopSolidHost.Parameters.GetParameters(docId);\n" +
                "var sb = new System.Text.StringBuilder();\n" +
                "sb.AppendLine(\"Parametres: \" + pList.Count);\n" +
                "foreach (var p in pList)\n" +
                "{\n" +
                "    string name = TopSolidHost.Elements.GetFriendlyName(p);\n" +
                "    int pType = TopSolidHost.Parameters.GetParameterType(p);\n" +
                "    sb.AppendLine(\"  \" + name + \" (type=\" + pType + \")\");\n" +
                "}\n" +
                "return sb.ToString();",
                false) },

            // --- Export ---
            { "lister_exporteurs", new RecipeEntry(
                "Liste tous les exporteurs disponibles",
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
                "return sb.ToString();",
                false) },

            // --- Document type ---
            { "type_document", new RecipeEntry(
                "Detecte le type du document actif (piece, assemblage, mise en plan, nomenclature, mise a plat)",
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
                "return sb.ToString();",
                false) },
        };

        public RecipeTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            // Build enum description from recipe names
            var recipeList = new List<string>();
            foreach (var kvp in Recipes)
            {
                recipeList.Add(kvp.Key + " — " + kvp.Value.Description);
            }

            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_run_recipe",
                Description = "Execute une recette TopSolid pre-programmee. " +
                    "NE PAS ecrire de code — choisir le nom de recette parmi : " +
                    string.Join(", ", Recipes.Keys) + ". " +
                    "Parametre optionnel 'value' pour les recettes de modification.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["recipe"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Nom de la recette : " + string.Join(", ", Recipes.Keys)
                        },
                        ["value"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Valeur pour les recettes de modification (ex: nouvelle designation)"
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
                return "Erreur : le parametre 'recipe' est requis. Recettes disponibles : " + string.Join(", ", Recipes.Keys);

            if (!Recipes.TryGetValue(recipeName, out var recipe))
                return "Recette inconnue : '" + recipeName + "'. Recettes disponibles : " + string.Join(", ", Recipes.Keys);

            string code = recipe.Code;

            // Substitute {value} placeholder if present
            string value = arguments["value"]?.ToString() ?? "";
            code = code.Replace("{value}", value.Replace("\"", "\\\""));

            // Ensure connector is initialized (triggers lazy init)
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
