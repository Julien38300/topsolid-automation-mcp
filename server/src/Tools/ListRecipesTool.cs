using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Searchable catalogue of the built-in recipes.
    ///
    /// The recipe list used to be inlined twice in the <c>topsolid_run_recipe</c>
    /// descriptor, which sent ~1300 tokens of duplicated names on every session
    /// start. This tool serves the catalogue on demand instead: one line per
    /// recipe (name, mode and description), filtered by category or keyword.
    ///
    /// Knowledge-base tool: no TopSolid connection required.
    /// </summary>
    public class ListRecipesTool
    {
        private const int DefaultMaxResults = 30;
        private const int HardMaxResults = 100;

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_list_recipes",
                Description = "Lists the built-in TopSolid recipes (name, mode, description), " +
                    "filtered by category and/or keyword. " +
                    "Use it to find the exact recipe name to pass to topsolid_run_recipe. " +
                    "Categories: " + string.Join(", ", RecipeTool.GetAllCategories()) + ". " +
                    "No TopSolid connection required.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["category"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional category filter (e.g. 'EXPORT', 'DRAFTING'). " +
                                "Omit to search every category."
                        },
                        ["search"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional keyword, matched against the recipe name and description."
                        },
                        ["max_results"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of recipes returned. Default 30, maximum 100."
                        }
                    }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes the catalogue lookup and returns one line per matching recipe.
        /// </summary>
        public string Execute(JObject arguments)
        {
            string category = arguments?["category"]?.ToString()?.Trim();
            string search = arguments?["search"]?.ToString()?.Trim();
            int maxResults = ReadMaxResults(arguments);

            var matches = new List<string>();
            foreach (string name in RecipeTool.GetAllRecipeNames())
            {
                var entry = RecipeTool.GetRecipe(name);
                if (entry == null) continue;
                if (!MatchesCategory(entry, category)) continue;
                if (!MatchesSearch(name, entry, search)) continue;
                matches.Add(name);
            }

            var sb = new StringBuilder();

            if (matches.Count == 0)
            {
                sb.AppendLine("No recipe matches this filter.");
                sb.AppendLine("Available categories: " + string.Join(", ", RecipeTool.GetAllCategories()));
                sb.AppendLine("Call topsolid_list_recipes without arguments to see the whole catalogue.");
                return sb.ToString();
            }

            int shown = matches.Count < maxResults ? matches.Count : maxResults;

            sb.AppendLine(matches.Count + " recipe(s) found" + DescribeFilter(category, search) +
                (shown < matches.Count ? ", showing the first " + shown : "") + ":");
            sb.AppendLine();

            for (int i = 0; i < shown; i++)
            {
                var entry = RecipeTool.GetRecipe(matches[i]);
                sb.AppendLine(RecipeTool.GetModeLabel(entry.Mode) + " " + entry.Category + " | " +
                    matches[i] + " - " + entry.Description);
            }

            sb.AppendLine();
            if (shown < matches.Count)
            {
                sb.AppendLine("Narrow the search with 'category' or 'search', or raise 'max_results' (maximum " +
                    HardMaxResults + ").");
            }
            sb.AppendLine("[READ] = no write. [WRITE-PDM] = modifies the document or the PDM. " +
                "[WRITE-DISK] = writes a file or sends a print job.");
            sb.AppendLine("Run one with topsolid_run_recipe, or read its C# body with topsolid_get_recipe.");
            return sb.ToString();
        }

        /// <summary>
        /// Reads the max_results argument, applying the default and the hard cap.
        /// </summary>
        private static int ReadMaxResults(JObject arguments)
        {
            var token = arguments?["max_results"];
            if (token == null) return DefaultMaxResults;

            int parsed;
            if (!int.TryParse(token.ToString().Trim(), out parsed)) return DefaultMaxResults;
            if (parsed < 1) return DefaultMaxResults;
            return parsed > HardMaxResults ? HardMaxResults : parsed;
        }

        private static bool MatchesCategory(RecipeTool.RecipeEntry entry, string category)
        {
            if (string.IsNullOrEmpty(category)) return true;
            if (string.IsNullOrEmpty(entry.Category)) return false;
            if (string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase)) return true;
            return entry.Category.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesSearch(string name, RecipeTool.RecipeEntry entry, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(entry.Description) &&
                entry.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeFilter(string category, string search)
        {
            bool hasCategory = !string.IsNullOrEmpty(category);
            bool hasSearch = !string.IsNullOrEmpty(search);
            if (!hasCategory && !hasSearch) return "";
            if (hasCategory && hasSearch) return " in category '" + category + "' matching '" + search + "'";
            if (hasCategory) return " in category '" + category + "'";
            return " matching '" + search + "'";
        }
    }
}
