using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Protocol.Models
{
    /// <summary>
    /// Describes a tool available in the MCP server.
    /// </summary>
    public class McpToolDescriptor
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public JObject InputSchema { get; set; }

        /// <summary>MCP annotations (readOnlyHint, destructiveHint...), see McpToolAnnotations.</summary>
        [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Annotations { get; set; }
    }
}
