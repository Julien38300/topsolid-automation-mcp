using System;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    public class ListElementsTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public ListElementsTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_list_elements",
                Description = "Lists elements in the active TopSolid document. Filter by type: Parameter, Sketch, Shape, Part (assembly inclusions). Omit typeFilter to list all types.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["typeFilter"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Element type to list: Parameter, Sketch, Shape, Part. Omit to list all types."
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

                var docId = TopSolidHost.Documents.EditedDocument;
                if (docId.IsEmpty)
                    return "Error: No active document in TopSolid. Open a document first.";

                string typeFilter = (arguments["typeFilter"]?.ToString() ?? "").ToLowerInvariant().Trim();
                bool listAll = string.IsNullOrEmpty(typeFilter);

                string docName = TopSolidHost.Documents.GetName(docId);
                var sb = new StringBuilder();
                sb.AppendLine("Document: " + System.IO.Path.GetFileNameWithoutExtension(docName));

                if (listAll || typeFilter == "parameter")
                {
                    try
                    {
                        var pList = TopSolidHost.Parameters.GetParameters(docId);
                        if (pList != null && pList.Count > 0)
                        {
                            sb.AppendLine("\nParameters (" + pList.Count + "):");
                            foreach (var p in pList)
                                sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(p));
                        }
                        else if (!listAll)
                        {
                            sb.AppendLine("\nNo parameters found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[ListElementsTool] Parameters error: " + ex.Message);
                    }
                }

                if (listAll || typeFilter == "sketch")
                {
                    try
                    {
                        var sList = TopSolidHost.Sketches2D.GetSketches(docId);
                        if (sList != null && sList.Count > 0)
                        {
                            sb.AppendLine("\nSketches (" + sList.Count + "):");
                            foreach (var s in sList)
                                sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(s));
                        }
                        else if (!listAll)
                        {
                            sb.AppendLine("\nNo sketches found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[ListElementsTool] Sketches error: " + ex.Message);
                    }
                }

                if (listAll || typeFilter == "shape")
                {
                    try
                    {
                        var shList = TopSolidHost.Shapes.GetShapes(docId);
                        if (shList != null && shList.Count > 0)
                        {
                            sb.AppendLine("\nShapes (" + shList.Count + "):");
                            foreach (var s in shList)
                            {
                                string name = TopSolidHost.Elements.GetFriendlyName(s);
                                string typeName = TopSolidHost.Elements.GetTypeFullName(s);
                                sb.AppendLine("  " + name + " (" + typeName + ")");
                            }
                        }
                        else if (!listAll)
                        {
                            sb.AppendLine("\nNo shapes found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[ListElementsTool] Shapes error: " + ex.Message);
                    }
                }

                if (listAll || typeFilter == "part")
                {
                    try
                    {
                        bool isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId);
                        if (isAsm)
                        {
                            var parts = TopSolidDesignHost.Assemblies.GetParts(docId);
                            if (parts != null && parts.Count > 0)
                            {
                                sb.AppendLine("\nParts (" + parts.Count + "):");
                                foreach (var p in parts)
                                    sb.AppendLine("  " + TopSolidHost.Elements.GetFriendlyName(p));
                            }
                            else
                            {
                                sb.AppendLine("\nNo parts found (empty assembly).");
                            }
                        }
                        else if (typeFilter == "part")
                        {
                            sb.AppendLine("\nNote: Active document is not an assembly.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[ListElementsTool] Parts error: " + ex.Message);
                        if (typeFilter == "part")
                            sb.AppendLine("\nCould not list parts (document may not be a design document).");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ListElementsTool] Unexpected error: " + ex.Message);
                return "Error listing elements: " + ex.Message;
            }
        }
    }
}
