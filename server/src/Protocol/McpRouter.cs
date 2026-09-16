using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Routes MCP requests to the appropriate handlers.
    /// </summary>
    public class McpRouter
    {
        /// <summary>
        /// MCP protocol revisions this server understands, most recent first.
        /// Only the protocol revision is negotiated: the transport stays stdio
        /// (newline-delimited JSON-RPC on stdin/stdout) whichever revision the client asks for.
        /// </summary>
        private static readonly string[] SupportedProtocolVersions =
        {
            "2025-06-18",
            "2025-03-26",
            "2024-11-05"
        };

        /// <summary>
        /// Server version reported in initialize, read from the running assembly
        /// so it can never drift from the build number.
        /// </summary>
        private static readonly string ServerVersion = ReadAssemblyVersion();

        private readonly McpToolRegistry _registry;

        public McpRouter(McpToolRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Reads the executing assembly version and formats it as major.minor.build.
        /// </summary>
        /// <returns>Version string, or "0.0.0" when the version cannot be read.</returns>
        private static string ReadAssemblyVersion()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version == null)
                    return "0.0.0";

                int build = version.Build < 0 ? 0 : version.Build;
                return version.Major + "." + version.Minor + "." + build;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-ROUTER] Unable to read assembly version: " + ex.Message);
                return "0.0.0";
            }
        }

        /// <summary>
        /// Negotiates the protocol revision: echoes the client revision when it is supported,
        /// otherwise answers with the most recent supported revision.
        /// </summary>
        /// <param name="parameters">The initialize request parameters (may be null).</param>
        /// <returns>The protocol revision to announce.</returns>
        private static string NegotiateProtocolVersion(JObject parameters)
        {
            string requested = parameters == null ? null : parameters["protocolVersion"]?.ToString();

            if (!string.IsNullOrEmpty(requested))
            {
                for (int i = 0; i < SupportedProtocolVersions.Length; i++)
                {
                    if (string.Equals(requested, SupportedProtocolVersions[i], StringComparison.Ordinal))
                        return requested;
                }

                Console.Error.WriteLine("[MCP-ROUTER] Unsupported protocolVersion '" + requested +
                                        "' requested, answering with '" + SupportedProtocolVersions[0] + "'.");
            }

            return SupportedProtocolVersions[0];
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
                        return BuildInitializeResponse(request);
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

        /// <summary>
        /// Builds the initialize result: negotiated protocol revision, capabilities and server identity.
        /// </summary>
        /// <param name="request">The initialize request.</param>
        /// <returns>Initialize response.</returns>
        private JsonRpcResponse BuildInitializeResponse(JsonRpcRequest request)
        {
            string protocolVersion = NegotiateProtocolVersion(request.Params);

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new JObject
                {
                    ["protocolVersion"] = protocolVersion,
                    ["capabilities"] = new JObject
                    {
                        ["tools"] = new JObject { ["listChanged"] = false }
                    },
                    ["serverInfo"] = new JObject
                    {
                        ["name"] = "TopSolid-MCP-Server",
                        ["version"] = ServerVersion
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

        /// <summary>
        /// Executes a tool and wraps its output in a tools/call result.
        /// </summary>
        /// <remarks>
        /// Tool failures are reported inside the result with isError set to true, not as JSON-RPC
        /// errors: the -32603 code is reserved for protocol-level failures, and the model must be
        /// able to read the failure text. isError is set when the handler throws, and also by
        /// convention when the returned text starts with "Error:" or "ERROR:" (case-insensitive),
        /// which is how the tools in this server report a failure they handled themselves.
        /// An unknown tool name stays a JSON-RPC -32602 error, since that is a caller mistake.
        /// </remarks>
        /// <param name="request">The tools/call request.</param>
        /// <param name="id">Request identifier echoed in the response.</param>
        /// <returns>Response message.</returns>
        private JsonRpcResponse BuildToolsCallResponse(JsonRpcRequest request, object id)
        {
            if (request.Params == null)
                return BuildErrorResponse(id, -32602, "Invalid parameters: params is missing.");

            var name = request.Params["name"]?.ToString();
            var args = request.Params["arguments"] as JObject ?? new JObject();

            if (string.IsNullOrEmpty(name))
                return BuildErrorResponse(id, -32602, "Invalid parameters: name is required.");

            if (!_registry.HasTool(name))
                return BuildErrorResponse(id, -32602, $"Tool '{name}' not found.");

            string resultText;
            bool isError;

            try
            {
                resultText = _registry.InvokeTool(name, args);
                isError = IsErrorText(resultText);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-ROUTER] Tool '{name}' failed: {ex}");
                resultText = $"Error: tool '{name}' failed: {ex.Message}";
                isError = true;
            }

            return BuildToolResult(id, resultText, isError);
        }

        /// <summary>
        /// Builds a tools/call result payload with the isError flag.
        /// </summary>
        /// <param name="id">Request identifier echoed in the response.</param>
        /// <param name="text">Text content returned to the client.</param>
        /// <param name="isError">True when the call must be reported as a tool failure.</param>
        /// <returns>Response message.</returns>
        private JsonRpcResponse BuildToolResult(object id, string text, bool isError)
        {
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
                            ["text"] = text ?? string.Empty
                        }
                    },
                    ["isError"] = isError
                }
            };
        }

        /// <summary>
        /// Applies the "Error:" prefix convention used by the tools of this server.
        /// </summary>
        /// <param name="text">Text returned by a tool handler.</param>
        /// <returns>True when the text reports a failure.</returns>
        private static bool IsErrorText(string text)
        {
            return !string.IsNullOrEmpty(text)
                   && text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
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
