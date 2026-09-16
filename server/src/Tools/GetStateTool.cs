using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to retrieve the current state of TopSolid via the WCF Bridge.
    /// </summary>
    public class GetStateTool
    {
        /// <summary>Maximum size of the returned text, to keep a single call from flooding the caller's context.</summary>
        private const int MaxOutputChars = 8000;

        private readonly Func<TopSolidConnector> _connectorProvider;

        public GetStateTool(Func<TopSolidConnector> connectorProvider)
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
                Name = "topsolid_get_state",
                Description = "Return the current TopSolid state: edited document, document type, " +
                    "associated project. Requires a running TopSolid session.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject()
                }
            }, Execute);
        }

        /// <summary>
        /// Executes the get-state query against TopSolid.
        /// </summary>
        public string Execute(JObject arguments)
        {
            return Truncate(ExecuteCore(arguments));
        }

        private string ExecuteCore(JObject arguments)
        {
            try
            {
                var connector = _connectorProvider();

                if (!connector.EnsureConnected())
                {
                    return "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled.";
                }

                return connector.GetState();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetStateTool] Unexpected error: {ex.Message}");
                return "Error: an unexpected error occurred while retrieving the TopSolid state.";
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
