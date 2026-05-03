using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    public class ListDocumentsTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public ListDocumentsTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_list_documents",
                Description = "Lists documents in the current TopSolid PDM project. Optionally filter by folder name, file extension (.TopPrt, .TopAsm, .TopDrf), and enable recursive traversal of subfolders.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["folder"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Folder name to list (optional, defaults to project root)"
                        },
                        ["extension"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Filter by file extension, e.g. .TopPrt, .TopAsm, .TopDrf"
                        },
                        ["recursive"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Include subfolders (default: false)"
                        }
                    }
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

                string folderFilter = arguments["folder"]?.ToString();
                string extFilter = arguments["extension"]?.ToString()?.ToLowerInvariant();
                bool recursive = arguments["recursive"]?.Value<bool>() ?? false;

                var projId = TopSolidHost.Pdm.GetCurrentProject();
                if (projId.IsEmpty)
                    return "Error: No current project in TopSolid. Open a project first.";

                string projectName = TopSolidHost.Pdm.GetName(projId);
                var rootId = projId;

                if (!string.IsNullOrEmpty(folderFilter))
                {
                    var matches = TopSolidHost.Pdm.SearchFolderByName(projId, folderFilter);
                    if (matches == null || matches.Count == 0)
                        return $"Error: Folder '{folderFilter}' not found in project '{projectName}'.";
                    rootId = matches[0];
                }

                var sb = new StringBuilder();
                sb.AppendLine("Project: " + projectName);
                int count = 0;
                ListDocs(rootId, extFilter, recursive, sb, "", ref count);
                sb.AppendLine("\nTotal: " + count + " document(s)");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ListDocumentsTool] Error: " + ex.Message);
                return "Error listing documents: " + ex.Message;
            }
        }

        private void ListDocs(PdmObjectId parentId, string extFilter, bool recursive, StringBuilder sb, string indent, ref int count)
        {
            List<PdmObjectId> folders;
            List<PdmObjectId> docs;
            TopSolidHost.Pdm.GetConstituents(parentId, out folders, out docs);

            if (docs != null)
            {
                foreach (var doc in docs)
                {
                    string name = TopSolidHost.Pdm.GetName(doc);
                    if (string.IsNullOrEmpty(extFilter) || name.ToLowerInvariant().EndsWith(extFilter))
                    {
                        sb.AppendLine(indent + name);
                        count++;
                    }
                }
            }

            if (recursive && folders != null)
            {
                foreach (var folder in folders)
                {
                    string folderName = TopSolidHost.Pdm.GetName(folder);
                    sb.AppendLine(indent + "[" + folderName + "/]");
                    ListDocs(folder, extFilter, recursive, sb, indent + "  ", ref count);
                }
            }
        }
    }
}
