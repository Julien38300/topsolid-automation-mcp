using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Reports the dialog boxes open in TopSolid (title, text, buttons). It reads the windows
    /// through Win32 and never calls the Automation API, so it works while TopSolid is blocked.
    /// </summary>
    public class DetectDialogTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public DetectDialogTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_detect_dialog",
                Description = "Detect a dialog box open in TopSolid (error, warning, 'nothing to export', confirmation...) " +
                    "and return its title, text and buttons. Read-only, and it works even when TopSolid is blocked: " +
                    "call it as soon as a TopSolid call times out or TopSolid stops answering.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject()
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            try
            {
                return DialogDetector.Describe(ResolveProcessId());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[DetectDialogTool] Unexpected error: " + ex.Message);
                return "Error: the dialog detection failed (" + ex.Message + ").";
            }
        }

        /// <summary>
        /// The TopSolid process listening on the connector port; 0 (every TopSolid process)
        /// when it cannot be told apart.
        /// </summary>
        private int ResolveProcessId()
        {
            try
            {
                var connector = _connectorProvider();
                if (connector == null) return 0;
                var instance = TopSolidInstanceList.FindByPort(TopSolidInstanceList.Scan(), connector.Port);
                return instance != null ? instance.Pid : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
