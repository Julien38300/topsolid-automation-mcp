using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Compiles a C# script against the TopSolid API without executing it.
    /// Lets LLMs and developers validate generated code cheaply before running.
    /// No TopSolid connection required — only DLL references.
    /// </summary>
    public class CompileTool
    {
        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_compile",
                Description = "Dry-run compile a C# script against the TopSolid Automation API. " +
                    "Returns 'OK' or the list of compile errors with line numbers. " +
                    "Does NOT execute the code; no TopSolid connection required. " +
                    "Use this to validate LLM-generated C# before running it with topsolid_execute_script.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["code"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The C# method body (same format as topsolid_execute_script)."
                        },
                        ["force_modification"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Force the transactional wrapper (StartModification/EndModification). Optional."
                        }
                    },
                    ["required"] = new JArray { "code" }
                }
            }, Execute);
        }

        /// <summary>
        /// Execute the compile-only validation.
        /// </summary>
        public string Execute(JObject arguments)
        {
            try
            {
                string code = arguments?["code"]?.ToString();
                if (string.IsNullOrWhiteSpace(code))
                {
                    return "Error: 'code' argument is required.";
                }

                bool forceModification = arguments?["force_modification"]?.Value<bool>() ?? false;
                return ScriptExecutor.CompileOnly(code, forceModification);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[CompileTool] Unexpected error: " + ex.Message);
                return "Error: " + ex.Message;
            }
        }
    }
}
