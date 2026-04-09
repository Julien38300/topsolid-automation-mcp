using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Routes MCP requests to the appropriate handlers.
    /// </summary>
    public class McpRouter
    {
        private readonly McpToolRegistry _registry;

        public McpRouter(McpToolRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Routes a JSON-RPC request and returns a response.
        /// </summary>
        /// <param name="request">Request to route.</param>
        /// <returns>Response message.</returns>
        public JsonRpcResponse Route(JsonRpcRequest request)
        {
            try
            {
                switch (request.Method)
                {
                    case "initialize":
                        return BuildInitializeResponse(request.Id);
                    case "tools/list":
                        return BuildToolsListResponse(request.Id);
                    case "tools/call":
                        return BuildToolsCallResponse(request, request.Id);
                    case "ping":
                        return new JsonRpcResponse { Id = request.Id, Result = new JObject() };
                    default:
                        Console.Error.WriteLine($"[MCP-ROUTER] Unknown method: {request.Method}");
                        return BuildErrorResponse(request.Id, -32601, $"Method '{request.Method}' not found.");
                }
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(request.Id, -32603, ex.Message);
            }
        }

        private JsonRpcResponse BuildInitializeResponse(object id)
        {
            return new JsonRpcResponse
            {
                Id = id,
                Result = new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JObject
                    {
                        ["tools"] = new JObject { ["listChanged"] = false }
                    },
                    ["serverInfo"] = new JObject
                    {
                        ["name"] = "TopSolid-MCP-Server",
                        ["version"] = "1.0.0"
                    }
                }
            };
        }

        private JsonRpcResponse BuildToolsListResponse(object id)
        {
            var tools = _registry.ListTools();
            return new JsonRpcResponse
            {
                Id = id,
                Result = new JObject
                {
                    ["tools"] = JArray.FromObject(tools)
                }
            };
        }

        private JsonRpcResponse BuildToolsCallResponse(JsonRpcRequest request, object id)
        {
            if (request.Params == null)
                return BuildErrorResponse(id, -32602, "Invalid parameters: params is missing.");

            var name = request.Params["name"]?.ToString();
            var args = request.Params["arguments"] as JObject ?? new JObject();

            if (string.IsNullOrEmpty(name))
                return BuildErrorResponse(id, -32602, "Invalid parameters: name is required.");

            try
            {
                var resultText = _registry.InvokeTool(name, args);
                return new JsonRpcResponse
                {
                    Id = id,
                    Result = new JObject
                    {
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = resultText
                            }
                        }
                    }
                };
            }
            catch (KeyNotFoundException)
            {
                return BuildErrorResponse(id, -32602, $"Tool '{name}' not found.");
            }
        }

        private JsonRpcResponse BuildErrorResponse(object id, int code, string message)
        {
            return new JsonRpcResponse
            {
                Id = id,
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message
                }
            };
        }
    }
}
