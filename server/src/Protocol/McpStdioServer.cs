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
        private readonly StreamReader _stdin;

        public McpStdioServer(McpRouter router)
        {
            _router = router;

            // Force stdin to UTF-8 to handle accented characters.
            // The setter throws IOException when the process has no console attached
            // (started as a child process with redirected handles), which is the normal case here.
            try
            {
                Console.InputEncoding = new UTF8Encoding(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MCP-SERVER] Could not set stdin encoding to UTF-8: " + ex.Message);
            }

            // Frames are read through a private UTF-8 reader rather than Console.In: when the
            // setter above fails (no console attached), Console.In would decode stdin with the
            // ANSI code page and mangle the accented characters of the JSON-RPC frames.
            _stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));

            // Force stdout to UTF-8 with no BOM and AutoFlush.
            // This prevents .NET Framework from buffering when stdout is a pipe.
            var stdoutStream = Console.OpenStandardOutput();
            _stdout = new StreamWriter(stdoutStream, new UTF8Encoding(false));
            _stdout.AutoFlush = true;

            // stdout carries JSON-RPC frames only: they are written through the private _stdout
            // writer. Console.Out is redirected to stderr so that a stray Console.WriteLine
            // anywhere in the process cannot corrupt the protocol stream.
            Console.SetOut(Console.Error);
        }

        /// <summary>
        /// Starts the server loop.
        /// </summary>
        public void Start()
        {
            try
            {
                string line;
                while ((line = _stdin.ReadLine()) != null)
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
                    // Notifications carry no Id — never answer them, whatever the method is
                    // (notifications/initialized, notifications/cancelled, and any future one).
                    // Unknown *requests* still get a -32601 from the router, as the specification requires.
                    if (IsNotification(request))
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

        /// <summary>
        /// Tells whether an incoming message is a notification, which must never be answered.
        /// A message with no Id is a notification per JSON-RPC 2.0; a message whose method is in
        /// the "notifications/" namespace is treated as one too, even if a client wrongly sends an Id.
        /// </summary>
        /// <param name="request">Decoded incoming message.</param>
        /// <returns>True when no response must be sent.</returns>
        private static bool IsNotification(JsonRpcRequest request)
        {
            if (request.Id == null)
                return true;

            return request.Method != null
                   && request.Method.StartsWith("notifications/", StringComparison.Ordinal);
        }

        private void SendResponse(JsonRpcResponse response)
        {
            if (response == null)
                return;

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
