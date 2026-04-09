using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to compile and execute a C# script in modification mode inside TopSolid,
    /// wrapping user code in StartModification/EndModification automatically.
    /// </summary>
    public class ModifyScriptTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public ModifyScriptTool(Func<TopSolidConnector> connectorProvider)
        {
            _connectorProvider = connectorProvider;
        }

        /// <summary>
        /// Registers the tool in the provided registry.
        /// </summary>
        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_modify_script",
                Description = "IMPORTANT: Appeler d'abord topsolid_api_help pour trouver les signatures correctes. " +
                    "Compile et execute un script C# 5 en mode MODIFICATION contre TopSolid. " +
                    "Le code est automatiquement wrappe dans StartModification/EndModification. " +
                    "Variables pre-declarees : docId (DocumentId du document edite), pdmId (PdmObjectId associe), " +
                    "__message (string, message de retour par defaut). " +
                    "TopSolidHost.Documents.EnsureIsDirty(ref docId) est appele AUTOMATIQUEMENT avant votre code. " +
                    "TopSolidHost.Pdm.Save(pdmId, true) est appele AUTOMATIQUEMENT apres votre code. " +
                    "IMPORTANT : NE PAS UTILISER 'return'. Pour personnaliser le message de succes, assignez __message. " +
                    "Exemple : __message = \"Operation OK\"; " +
                    "INTERDIT : $\"\", TopSolidHost.Instance, Documents.ActiveDocument, reflexion.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["code"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Corps du script C# 5. " +
                                "PAS de using/namespace/class/accolades. " +
                                "NE PAS utiliser 'return'. " +
                                "Variables dispos : docId, pdmId, __message. " +
                                "Exemple renommer param : " +
                                "var paramId = TopSolidHost.Parameters.GetParameter(docId, \"MonParam\"); " +
                                "TopSolidHost.Elements.SetName(paramId, \"NouveauNom\"); " +
                                "__message = \"Parametre renomme avec succes.\";"
                        }
                    },
                    ["required"] = new JArray { "code" }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes a dynamic C# script in modification mode against TopSolid Automation API.
        /// </summary>
        public string Execute(JObject arguments)
        {
            try
            {
                string code = arguments["code"]?.ToString();

                if (string.IsNullOrWhiteSpace(code))
                    return "Erreur : le paramètre 'code' est requis.";

                var connector = _connectorProvider();

                if (!connector.IsConnected && !connector.Connect())
                    return "TopSolid n'est pas connecté. Impossible d'exécuter le script.";

                return ScriptExecutor.ExecuteModification(code);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ModifyScriptTool] Unexpected error: " + ex.Message);
                return "Erreur lors de l'exécution du script de modification : " + ex.Message;
            }
        }
    }
}
