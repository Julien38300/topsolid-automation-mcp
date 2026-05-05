using System;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Batch-modifies PDM properties on one or more documents without opening them.
    /// Supported actions: set_description, set_partNumber, set_manufacturer, set_user_property.
    /// </summary>
    public class ModifyDocumentsTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public ModifyDocumentsTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_modify_documents",
                Description = "Batch-modifies standard PDM properties on one or more TopSolid documents. Actions: set_description, set_partNumber, set_manufacturer. Documents are identified by name within the current project. For user properties, use topsolid_run_recipe with the set_user_property recipe.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["required"] = new JArray { "documents", "action", "value" },
                    ["properties"] = new JObject
                    {
                        ["documents"] = new JObject
                        {
                            ["type"] = "array",
                            ["items"] = new JObject { ["type"] = "string" },
                            ["description"] = "List of document names to modify (e.g. [\"Piece1.TopPrt\", \"Piece2.TopPrt\"])"
                        },
                        ["action"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Action to perform: set_description | set_partNumber | set_manufacturer"
                        },
                        ["value"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Value to set"
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

                var docsArray = arguments["documents"] as JArray;
                if (docsArray == null || docsArray.Count == 0)
                    return "Error: 'documents' must be a non-empty array of document names.";

                string action = arguments["action"]?.ToString()?.ToLowerInvariant();
                string value = arguments["value"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(action))
                    return "Error: 'action' is required. Use set_description, set_partNumber, or set_manufacturer.";

                var projId = TopSolidHost.Pdm.GetCurrentProject();
                if (projId.IsEmpty)
                    return "Error: No current project in TopSolid.";

                if (action != "set_description" && action != "set_partnumber" && action != "set_manufacturer")
                    return "Error: unknown action '" + action + "'. Use set_description, set_partNumber, or set_manufacturer.";

                var sb = new StringBuilder();
                int successCount = 0;
                int failCount = 0;

                foreach (var docToken in docsArray)
                {
                    string docName = docToken.ToString();
                    try
                    {
                        var matches = TopSolidHost.Pdm.SearchDocumentByName(projId, docName);
                        if (matches == null || matches.Count == 0)
                        {
                            sb.AppendLine("  SKIP " + docName + ": not found in project.");
                            failCount++;
                            continue;
                        }

                        // Use the first match (most projects have unique doc names)
                        var pdmId = matches[0];

                        switch (action)
                        {
                            case "set_description":
                                TopSolidHost.Pdm.SetDescription(pdmId, value);
                                break;
                            case "set_partnumber":
                                TopSolidHost.Pdm.SetPartNumber(pdmId, value);
                                break;
                            case "set_manufacturer":
                                TopSolidHost.Pdm.SetManufacturer(pdmId, value);
                                break;
                            default:
                                sb.AppendLine("  ERROR " + docName + ": unknown action '" + action + "'.");
                                failCount++;
                                continue;
                        }

                        TopSolidHost.Pdm.Save(pdmId, true);
                        sb.AppendLine("  OK    " + docName);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[ModifyDocumentsTool] Error on " + docName + ": " + ex.Message);
                        sb.AppendLine("  FAIL  " + docName + ": " + ex.Message);
                        failCount++;
                    }
                }

                sb.Insert(0, "Action: " + action + " = \"" + value + "\"\n");
                sb.AppendLine("\nResult: " + successCount + " succeeded, " + failCount + " failed.");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ModifyDocumentsTool] Unexpected error: " + ex.Message);
                return "Error modifying documents: " + ex.Message;
            }
        }
    }
}
