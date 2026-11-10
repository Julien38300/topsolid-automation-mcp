using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// topsolid_compare_documents: lists what changed between two revisions of a part or assembly
    /// (or between two documents), sorted by material, parameters, operations and geometry, says for
    /// each change whether a target document already has it, and applies the automatic part.
    /// </summary>
    public class CompareDocumentsTool
    {
        private const int MaxOutputChars = 20000;

        private readonly Func<TopSolidConnector> _connectorProvider;
        private readonly DocumentCompareRunner _runner;

        public CompareDocumentsTool(Func<TopSolidConnector> connectorProvider, bool readOnly)
        {
            _connectorProvider = connectorProvider;
            _runner = new DocumentCompareRunner(code => ScriptExecutor.Execute(code), readOnly);
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_compare_documents",
                Description = "Compare two revisions of a TopSolid part or assembly (default: the previous " +
                    "revision with the current one) and list every change, sorted: material, parameters, " +
                    "operations (added, removed, moved, activated, invalid) and geometry (holes and faces " +
                    "that changed). With 'target', tells for each change whether that other document already " +
                    "has it and whether it can be carried over automatically or by hand. With apply=true, " +
                    "applies the automatic changes to the target in one saved transaction (material, plain " +
                    "parameters, operation activation), then lists what is left to do by hand. The content " +
                    "of an operation (a drilling diameter, a sketch) cannot be read by the API: it shows up " +
                    "as a geometry change. mode='documents' compares 'document' with 'target' directly.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["document"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Exact name of the document that evolved, in the current project. Default: the edited document."
                        },
                        ["from_revision"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Base revision: 'A.0', 'A' (last minor revision of A) or 'previous' (default)."
                        },
                        ["to_revision"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Revision to compare with the base. Default: the current one."
                        },
                        ["target"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Exact name of a document to carry the changes over to (same project)."
                        },
                        ["mode"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JArray { "revisions", "documents" },
                            ["description"] = "'revisions' (default): changes of 'document' between two of its revisions. " +
                                "'documents': everything that differs between 'target' and 'document' (what to change in target to match document)."
                        },
                        ["apply"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Apply the AUTO changes to 'target' (needs the current revision as to_revision). Default false: report only."
                        }
                    }
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            string output;
            try
            {
                arguments = arguments ?? new JObject();
                output = _runner.Validate(arguments);
                if (output == null)
                {
                    var connector = _connectorProvider();
                    output = connector == null || !connector.EnsureConnected()
                        ? "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled."
                        : _runner.Run(arguments);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[CompareDocumentsTool] " + ex);
                output = "Error while comparing: " + ex.Message;
            }
            if (output.Length > MaxOutputChars)
                output = output.Substring(0, MaxOutputChars) + "\n... [output truncated]";
            return output;
        }
    }
}