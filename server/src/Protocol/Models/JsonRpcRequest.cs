using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Protocol.Models
{
    /// <summary>
    /// Represents a JSON-RPC 2.0 request.
    /// </summary>
    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JObject Params { get; set; }
    }
}
