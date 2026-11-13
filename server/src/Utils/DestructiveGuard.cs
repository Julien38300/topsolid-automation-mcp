using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// GH #7: an explicit confirm=true before what cannot be undone. A recipe that overwrites files or
    /// changes a whole project, or a script that deletes TopSolid data, is refused until the agent has
    /// asked the user and calls again with confirm=true. Automation contexts turn this off with the
    /// --no-confirm flag or TOPSOLID_MCP_SKIP_CONFIRM=1.
    /// </summary>
    public static class DestructiveGuard
    {
        public const string EnvVarName = "TOPSOLID_MCP_SKIP_CONFIRM";

        /// <summary>Set at start from --no-confirm or the environment variable.</summary>
        public static bool Disabled { get; set; }

        /// <summary>
        /// Recipes that change many documents at once or remove data, beyond the WriteDisk ones
        /// (exports and printing, which overwrite files). Deliberately a list: several creation
        /// recipes delete their own temporary elements, which is not a destructive act.
        /// </summary>
        private static readonly HashSet<string> DestructiveRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "batch_set_designation", "batch_set_reference", "batch_set_manufacturer",
            "batch_clear_author", "batch_enable_virtual", "clear_document_author",
            "replace_revolved_profile", "import_project_xliff",
        };

        private static readonly Regex DeletingCall = new Regex(
            @"\bTopSolid\w*Host\s*\.\s*\w+\s*\.\s*((?:Delete|Purge|Remove|Erase)\w*)\s*\(",
            RegexOptions.Compiled);

        public static bool IsDestructiveRecipe(string name, bool writesDisk)
        {
            return writesDisk || (name != null && DestructiveRecipes.Contains(name));
        }

        /// <summary>The TopSolid methods of the script that delete or remove something, without duplicates.</summary>
        public static List<string> DeletingCalls(string code)
        {
            return DeletingCall.Matches(code ?? "").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        }

        /// <summary>True when the arguments carry confirm=true (boolean or the string "true").</summary>
        public static bool IsConfirmed(Newtonsoft.Json.Linq.JObject arguments)
        {
            var t = arguments == null ? null : arguments["confirm"];
            if (t == null) return false;
            if (t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) return (bool)t;
            return string.Equals(t.ToString().Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Null when the call may run; else the refusal returned to the agent.</summary>
        public static string CheckRecipe(string name, bool writesDisk, Newtonsoft.Json.Linq.JObject arguments)
        {
            if (Disabled || !IsDestructiveRecipe(name, writesDisk) || IsConfirmed(arguments)) return null;
            return "Error: confirmation required. Recipe '" + name + "' " +
                (writesDisk ? "writes files on disk and may overwrite existing ones" : "changes many documents or removes data, and cannot be undone from here") +
                ". Tell the user what it will do, and call again with confirm=true only after they agree.";
        }

        /// <summary>Null when the script may run; else the refusal returned to the agent.</summary>
        public static string CheckScript(string code, Newtonsoft.Json.Linq.JObject arguments)
        {
            if (Disabled || IsConfirmed(arguments)) return null;
            var calls = DeletingCalls(code);
            if (calls.Count == 0) return null;
            return "Error: confirmation required. The script deletes TopSolid data (" + string.Join(", ", calls) +
                "). Tell the user what will be deleted, and call again with confirm=true only after they agree.";
        }
    }
}
