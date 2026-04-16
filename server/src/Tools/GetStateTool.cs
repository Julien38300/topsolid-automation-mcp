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
                Description = "Retourne l'état courant de TopSolid : document actif, type de document, projet associé. Nécessite TopSolid ouvert.",
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
                return "Une erreur inattendue est survenue lors de la récupération de l'état.";
            }
        }
    }
}
