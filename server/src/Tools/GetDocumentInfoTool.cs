using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Returns detailed PDM information about the active document:
    /// name, type, project, folder, description, partNumber, manufacturer, and user properties.
    /// </summary>
    public class GetDocumentInfoTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public GetDocumentInfoTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_get_document_info",
                Description = "Returns detailed PDM information about the active TopSolid document: name, type, project, folder path, description (Désignation), part number (Référence), manufacturer, and text user properties.",
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
                var connector = _connectorProvider();
                if (!connector.EnsureConnected())
                    return "Error: TopSolid not connected. Please ensure TopSolid is running with Automation enabled.";

                var docId = TopSolidHost.Documents.EditedDocument;
                if (docId.IsEmpty)
                    return "Error: No active document in TopSolid. Open a document first.";

                var sb = new StringBuilder();

                // ── Basic document info ──
                string docName = TopSolidHost.Documents.GetName(docId);
                string docExt = Path.GetExtension(docName);
                sb.AppendLine("Name       : " + Path.GetFileNameWithoutExtension(docName));
                sb.AppendLine("Extension  : " + docExt);

                // ── Document type (part / assembly / drafting) ──
                string docType = "Unknown";
                try
                {
                    bool isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId);
                    docType = isAsm ? "Assembly" : "Part";
                }
                catch { /* not a design doc — try other types */ }

                if (docType == "Unknown")
                    docType = docExt.Replace(".", "");
                sb.AppendLine("Type       : " + docType);

                // ── PDM info ──
                var pdmId = TopSolidHost.Documents.GetPdmObject(docId);
                if (pdmId.IsEmpty)
                {
                    sb.AppendLine("\n(Document has no PDM object — may not be in a project.)");
                    return sb.ToString();
                }

                // Project
                var projId = TopSolidHost.Pdm.GetProject(pdmId);
                if (!projId.IsEmpty)
                    sb.AppendLine("Project    : " + TopSolidHost.Pdm.GetName(projId));

                // ── Standard PDM properties ──
                sb.AppendLine("\n── PDM Properties ──");

                string desc = TopSolidHost.Pdm.GetDescription(pdmId);
                sb.AppendLine("Description: " + (string.IsNullOrEmpty(desc) ? "(empty)" : desc));

                string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);
                sb.AppendLine("PartNumber : " + (string.IsNullOrEmpty(pn) ? "(empty)" : pn));

                string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);
                sb.AppendLine("Manufacturer: " + (string.IsNullOrEmpty(mfr) ? "(empty)" : mfr));

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[GetDocumentInfoTool] Unexpected error: " + ex.Message);
                return "Error retrieving document info: " + ex.Message;
            }
        }
    }
}
