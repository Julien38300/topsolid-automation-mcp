using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// MCP server using stdio for transport.
    /// </summary>
    public class McpStdioServer
    {
        private readonly McpRouter _router;
        private readonly StreamWriter _stdout;

        public McpStdioServer(McpRouter router)
        {
            _router = router;
            // Fix M-46: Force stdin to UTF-8 to handle accented characters from OpenClaw
            Console.InputEncoding = new UTF8Encoding(false);
            // Force stdout to UTF-8 with no BOM and AutoFlush
            // This prevents .NET Framework from buffering when stdout is a pipe
            var stdoutStream = Console.OpenStandardOutput();
            _stdout = new StreamWriter(stdoutStream, new UTF8Encoding(false));
            _stdout.AutoFlush = true;
            Console.SetOut(_stdout);
        }

        /// <summary>
        /// Starts the server loop.
        /// </summary>
        public void Start()
        {
            try
            {
                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    ProcessRequest(line);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-SERVER] Critical error in main loop: {ex.Message}");
            }
        }

        private void ProcessRequest(string line)
        {
            JsonRpcResponse response;
            JsonRpcRequest request = null;

            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(line);
                if (request == null)
                {
                    response = BuildBasicErrorResponse(null, -32600, "Invalid Request");
                }
                else
                {
                    // Notifications have no Id — do NOT send a response
                    if (request.Id == null)
                    {
                        Console.Error.WriteLine($"[MCP-SERVER] Notification received: {request.Method}");
                        return;
                    }
                    response = _router.Route(request);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-SERVER] Deserialization error: {ex.Message}");
                response = BuildBasicErrorResponse(null, -32700, "Parse error");
            }

            SendResponse(response);
        }

        private void SendResponse(JsonRpcResponse response)
        {
            try
            {
                var json = JsonConvert.SerializeObject(response, Formatting.None);
                _stdout.WriteLine(json);
                _stdout.Flush();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP-SERVER] Serialization error: {ex.Message}");
            }
        }

        private JsonRpcResponse BuildBasicErrorResponse(object id, int code, string message)
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
