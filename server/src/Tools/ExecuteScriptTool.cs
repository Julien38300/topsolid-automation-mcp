using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to compile and execute a C# script inside TopSolid via the WCF Bridge.
    /// </summary>
    public class ExecuteScriptTool
    {
        /// <summary>Maximum size of the returned text, to keep a single call from flooding the caller's context.</summary>
        private const int MaxOutputChars = 8000;

        private readonly Func<TopSolidConnector> _connectorProvider;

        public ExecuteScriptTool(Func<TopSolidConnector> connectorProvider)
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
                Name = "topsolid_execute_script",
                Description = "Compile and run a C# script in the live TopSolid session. " +
                    "NOT sandboxed and NOT read-only: it runs fully trusted and is auto-wrapped in a " +
                    "write transaction when it mutates. Do not auto-approve. " +
                    "Method body ONLY (no using/namespace/class), C# 5 - no string interpolation " +
                    "($\"\"), use string.Format; end with return \"...\". " +
                    "Call topsolid_api_help first for the exact signatures.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["code"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "C# 5 method body. Usings already available: System, " +
                                "System.Collections.Generic, System.Linq, System.Text, System.IO, " +
                                "TopSolid.Kernel.Automating, TopSolid.Cad.Design.Automating. Example: " +
                                "var docId = TopSolidHost.Documents.EditedDocument; " +
                                "return TopSolidHost.Documents.GetName(docId);"
                        }
                    },
                    ["required"] = new JArray { "code" }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes a dynamic C# script against the TopSolid Automation API.
        /// </summary>
        public string Execute(JObject arguments)
        {
            return Truncate(ExecuteCore(arguments));
        }

        private string ExecuteCore(JObject arguments)
        {
            try
            {
                string code = arguments?["code"]?.ToString();

                if (string.IsNullOrWhiteSpace(code))
                    return "Error: the 'code' argument is required.";

                var connector = _connectorProvider();

                if (!connector.EnsureConnected())
                    return "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled.";

                // Hand the code over to the dynamic compiler/executor.
                return ScriptExecutor.Execute(code);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ExecuteScriptTool] Unexpected error: {ex.Message}");
                return "Error while executing the script: " + ex.Message;
            }
        }

        /// <summary>
        /// Caps the output length and appends an explicit marker when text was cut.
        /// </summary>
        private static string Truncate(string output)
        {
            if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputChars)
                return output;

            return output.Substring(0, MaxOutputChars) + "\n... [output truncated - refine your query]";
        }
    }
}
