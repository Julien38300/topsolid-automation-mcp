using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Registry for MCP tools.
    /// </summary>
    public class McpToolRegistry
    {
        private readonly Dictionary<string, McpToolDescriptor> _descriptors = new Dictionary<string, McpToolDescriptor>();
        private readonly Dictionary<string, Func<JObject, string>> _handlers = new Dictionary<string, Func<JObject, string>>();

        /// <summary>
        /// Registers a new tool.
        /// </summary>
        /// <param name="descriptor">Tool descriptor.</param>
        /// <param name="handler">Tool execution handler.</param>
        public void RegisterTool(McpToolDescriptor descriptor, Func<JObject, string> handler)
        {
            _descriptors[descriptor.Name] = descriptor;
            _handlers[descriptor.Name] = handler;
        }

        /// <summary>
        /// Lists all registered tools.
        /// </summary>
        /// <returns>List of tool descriptors.</returns>
        public List<McpToolDescriptor> ListTools()
        {
            return _descriptors.Values.ToList();
        }

        /// <summary>
        /// Tells whether a tool is registered under the given name.
        /// Lets the router answer an unknown tool name with a protocol error, while a
        /// failure thrown by a registered handler is reported as a tool error instead.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>True when the tool exists.</returns>
        public bool HasTool(string name)
        {
            return name != null && _handlers.ContainsKey(name);
        }

        /// <summary>
        /// Invokes a tool by name.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>Execution result string.</returns>
        public string InvokeTool(string name, JObject arguments)
        {
            if (!_handlers.TryGetValue(name, out var handler))
            {
                throw new KeyNotFoundException($"Tool '{name}' not found.");
            }

            return handler(arguments);
        }
    }
}
