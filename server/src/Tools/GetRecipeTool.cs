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
                    "If no name is given, returns the list of available recipes.",
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
                    "Use topsolid_get_recipe without arguments to list available recipes.";
            }

            return FormatOneRecipe(name, entry);
        }

        private static string FormatAllRecipes()
        {
            var names = RecipeTool.GetAllRecipeNames();
            var sb = new StringBuilder();
            sb.AppendLine("Available recipes (" + names.Count + " total):");
            sb.AppendLine();
            foreach (var n in names)
            {
                var e = RecipeTool.GetRecipe(n);
                if (e == null) continue;
                string mode = e.IsModification ? "[WRITE]" : "[READ] ";
                sb.AppendLine("  " + mode + "  " + n + " — " + e.Description);
            }
            sb.AppendLine();
            sb.AppendLine("Call topsolid_get_recipe with a specific name to see its C# body.");
            return sb.ToString();
        }

        private static string FormatOneRecipe(string name, RecipeTool.RecipeEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Recipe: " + name);
            sb.AppendLine("Mode:   " + (entry.IsModification ? "WRITE (transactional — Pattern D applied by runtime)" : "READ"));
            sb.AppendLine("Description: " + entry.Description);
            sb.AppendLine();
            sb.AppendLine("C# code (placeholders like {value} are substituted at runtime):");
            sb.AppendLine("```csharp");
            sb.AppendLine(entry.Code);
            sb.AppendLine("```");
            if (entry.IsModification)
            {
                sb.AppendLine();
                sb.AppendLine("Note: when called via topsolid_run_recipe, the runtime wraps this in:");
                sb.AppendLine("  TopSolidHost.Application.StartModification(\"Cortana IA Script\", false);");
                sb.AppendLine("  try { ...code... ; TopSolidHost.Application.EndModification(true, true); ... }");
                sb.AppendLine("  catch { TopSolidHost.Application.EndModification(false, false); }");
                sb.AppendLine("For standalone apps, add the Pattern D wrapper yourself.");
            }
            return sb.ToString();
        }
    }
}
