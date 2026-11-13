using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to compile and execute a C# script in modification mode inside TopSolid,
    /// wrapping user code in StartModification/EndModification automatically.
    /// </summary>
    public class ModifyScriptTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public ModifyScriptTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        /// <summary>
        /// Registers the tool in the provided registry.
        /// </summary>
        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_modify_script",
                Description = "Compile and run a C# script against TopSolid in MODIFICATION mode. The code is " +
                    "wrapped automatically: EnsureIsDirty, StartModification/EndModification, then Pdm.Save. " +
                    "Do NOT use return - assign __message to customise the success text. Pre-declared variables: " +
                    "docId, pdmId, __message. C# 5, no string interpolation ($\"\"). " +
                    "Call topsolid_api_help first for signatures.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["confirm"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Set to true ONLY after the user explicitly approved. Required for actions that cannot be undone (see the refusal message)."
                        },
                        ["code"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "C# 5 method body: no using/namespace/class, no 'return'. " +
                                "Available variables: docId, pdmId, __message. Example: " +
                                "var p = TopSolidHost.Parameters.GetParameter(docId, \"MyParam\"); " +
                                "TopSolidHost.Elements.SetName(p, \"NewName\"); __message = \"Parameter renamed.\";"
                        }
                    },
                    ["required"] = new JArray { "code" }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes a dynamic C# script in modification mode against the TopSolid Automation API.
        /// </summary>
        public string Execute(JObject arguments)
        {
            return ScriptOutputLimit.Apply(ExecuteCore(arguments), ScriptOutputLimit.CurrentMaxChars());
        }

        private string ExecuteCore(JObject arguments)
        {
            try
            {
                string code = arguments?["code"]?.ToString();

                if (string.IsNullOrWhiteSpace(code))
                    return "Error: the 'code' argument is required.";

                // GH #7: a script that deletes TopSolid data waits for confirm=true.
                string refusal = DestructiveGuard.CheckScript(code, arguments);
                if (refusal != null) return refusal;

                var connector = _connectorProvider();

                if (!connector.EnsureConnected())
                    return "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled.";

                return ScriptExecutor.ExecuteModification(code);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ModifyScriptTool] Unexpected error: " + ex.Message);
                return "Error while executing the modification script: " + ex.Message;
            }
        }
    }
}
