using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// MCP tool annotations (GH #7): hints that let an MCP client auto-approve the tools that only
    /// read and ask the user before the ones that write. They are hints, not a security boundary: the
    /// server-side guards (read-only mode, transaction rules) stay in force.
    /// Every tool must be listed here; ToolAnnotationsTests fails when one is missing.
    /// </summary>
    public static class McpToolAnnotations
    {
        private sealed class Hint
        {
            public string Title;
            public bool ReadOnly;
            public bool Destructive;
            public bool Idempotent;
        }

        private static readonly Dictionary<string, Hint> Hints = new Dictionary<string, Hint>
        {
            // Read only: TopSolid or the bundled data.
            { "topsolid_api_help", new Hint { Title = "Search the TopSolid API", ReadOnly = true } },
            { "topsolid_find_path", new Hint { Title = "Find an API call chain", ReadOnly = true } },
            { "topsolid_explore_paths", new Hint { Title = "Explore API call chains", ReadOnly = true } },
            { "topsolid_list_recipes", new Hint { Title = "List recipes", ReadOnly = true } },
            { "topsolid_get_recipe", new Hint { Title = "Show a recipe's code", ReadOnly = true } },
            { "topsolid_compile", new Hint { Title = "Compile a script (no run)", ReadOnly = true } },
            { "topsolid_search_examples", new Hint { Title = "Search code examples", ReadOnly = true } },
            { "topsolid_search_help", new Hint { Title = "Search TopSolid help", ReadOnly = true } },
            { "topsolid_search_commands", new Hint { Title = "Search TopSolid commands", ReadOnly = true } },
            { "topsolid_whats_new", new Hint { Title = "API changes between versions", ReadOnly = true } },
            { "topsolid_get_state", new Hint { Title = "TopSolid state", ReadOnly = true } },
            { "topsolid_detect_dialog", new Hint { Title = "Detect a blocking dialog", ReadOnly = true } },

            // Writes, but only adds or sets values on the target (never deletes).
            { "topsolid_compare_documents", new Hint { Title = "Compare documents and carry changes over", Idempotent = true } },

            // Can delete, overwrite or export: a client should ask first.
            { "topsolid_run_recipe", new Hint { Title = "Run a recipe", Destructive = true } },
            { "topsolid_execute_script", new Hint { Title = "Run a C# script", Destructive = true } },
            { "topsolid_modify_script", new Hint { Title = "Run a C# script in a transaction", Destructive = true } },
        };

        /// <summary>The "annotations" object of a tool, or null for a tool that is not classified.</summary>
        public static JObject For(string toolName)
        {
            Hint h;
            if (toolName == null || !Hints.TryGetValue(toolName, out h)) return null;
            var o = new JObject
            {
                ["title"] = h.Title,
                ["readOnlyHint"] = h.ReadOnly,
                // TopSolid is a local application: no tool reaches an open world.
                ["openWorldHint"] = false
            };
            if (!h.ReadOnly)
            {
                o["destructiveHint"] = h.Destructive;
                o["idempotentHint"] = h.Idempotent;
            }
            return o;
        }
    }
}
