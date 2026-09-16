using System;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Returns the C# code body of a given recipe — for developers and LLMs
    /// who want to learn from validated TopSolid patterns, or adapt a recipe
    /// into a standalone C# application.
    ///
    /// Unlike <c>topsolid_run_recipe</c> (which executes the recipe against
    /// TopSolid), <c>topsolid_get_recipe</c> is a pure knowledge-base lookup:
    /// no TopSolid connection required.
    /// </summary>
    public class GetRecipeTool
    {
        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_get_recipe",
                Description = "Returns the C# code body of a TopSolid recipe by name. " +
                    "Use this to learn validated TopSolid patterns, or to adapt a recipe " +
                    "into a standalone C# application. No TopSolid connection required. " +
                    "If no name is given, returns the recipe names and their modes; " +
                    "use topsolid_list_recipes to search them with descriptions.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["recipe"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Recipe name (e.g. 'read_designation'). " +
                                "Omit to list all available recipes."
                        }
                    }
                }
            }, Execute);
        }

        /// <summary>
        /// Execute the get-recipe lookup. Returns either the full list or one recipe.
        /// </summary>
        public string Execute(JObject arguments)
        {
            string name = arguments?["recipe"]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                return FormatAllRecipes();
            }

            var entry = RecipeTool.GetRecipe(name);
            if (entry == null)
            {
                return "Unknown recipe: '" + name + "'. " +
                    "Use topsolid_list_recipes to search the available recipes.";
            }

            return FormatOneRecipe(name, entry);
        }

        private static string FormatAllRecipes()
        {
            var names = RecipeTool.GetAllRecipeNames();
            var sb = new StringBuilder();
            sb.AppendLine("Available recipes (" + names.Count + " total) — name and mode only:");
            sb.AppendLine();
            foreach (var n in names)
            {
                var e = RecipeTool.GetRecipe(n);
                if (e == null) continue;
                sb.AppendLine("  " + RecipeTool.GetModeLabel(e.Mode) + " " + n);
            }
            sb.AppendLine();
            sb.AppendLine("Use topsolid_list_recipes (category / search filters) to get descriptions.");
            sb.AppendLine("Call topsolid_get_recipe with a specific name to see its C# body.");
            return sb.ToString();
        }

        /// <summary>
        /// Returns a one-line explanation of an execution mode.
        /// </summary>
        private static string DescribeMode(RecipeTool.RecipeEntry.RecipeMode mode)
        {
            switch (mode)
            {
                case RecipeTool.RecipeEntry.RecipeMode.WritePdm:
                    return "modifies the document or the PDM (transactional — Pattern D applied by runtime)";
                case RecipeTool.RecipeEntry.RecipeMode.WriteDisk:
                    return "writes outside TopSolid (file on disk or print job), no PDM transaction";
                default:
                    return "reads only, nothing is written";
            }
        }

        private static string FormatOneRecipe(string name, RecipeTool.RecipeEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Recipe: " + name);
            sb.AppendLine("Category: " + entry.Category);
            sb.AppendLine("Mode:   " + RecipeTool.GetModeLabel(entry.Mode) + " " + DescribeMode(entry.Mode));
            sb.AppendLine("Description: " + entry.Description);
            sb.AppendLine();
            sb.AppendLine("C# code (placeholders like {value} are substituted at runtime):");
            sb.AppendLine("```csharp");
            sb.AppendLine(entry.Code);
            sb.AppendLine("```");
            if (entry.Mode == RecipeTool.RecipeEntry.RecipeMode.WritePdm)
            {
                sb.AppendLine();
                sb.AppendLine("Note: when called via topsolid_run_recipe, the runtime wraps this in:");
                sb.AppendLine("  TopSolidHost.Application.StartModification(\"TopSolid MCP\", false);");
                sb.AppendLine("  bool committed = false;");
                sb.AppendLine("  try { ...code... ; TopSolidHost.Application.EndModification(true, true); committed = true; }");
                sb.AppendLine("  catch (Exception ex) { /* report ex */ }");
                sb.AppendLine("  finally { if (!committed) TopSolidHost.Application.EndModification(false, false); }");
                sb.AppendLine("The wrapper also declares docId, pdmId and __message: a recipe body sets");
                sb.AppendLine("__message instead of returning a string, and uses a bare 'return;' to exit early.");
                sb.AppendLine("For standalone apps, add the Pattern D wrapper yourself.");
            }
            return sb.ToString();
        }
    }
}
