using System;
using Newtonsoft.Json.Linq;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tools
{
    /// <summary>
    /// Tool to compile and execute a C# script inside TopSolid via the WCF Bridge (Roslyn SafeWrapper).
    /// </summary>
    public class ExecuteScriptTool
    {
        private readonly Func<TopSolidConnector> _connectorProvider;

        public ExecuteScriptTool(Func<TopSolidConnector> connectorProvider)
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
                Name = "topsolid_execute_script",
                Description = "IMPORTANT: Appeler d'abord topsolid_api_help pour trouver les signatures correctes. " +
                    "Compile et execute un script C# 5 contre TopSolid vivant. " +
                    "API : utiliser TopSolidHost.* (classe statique). " +
                    "Document actif : TopSolidHost.Documents.EditedDocument (propriete, pas methode). " +
                    "Nom : TopSolidHost.Documents.GetName(docId). " +
                    "Esquisses : TopSolidHost.Sketches2D.GetSketches(docId). " +
                    "Parametres : TopSolidHost.Parameters.GetParameters(docId) → List<ElementId>. " +
                    "Type param : TopSolidHost.Parameters.GetParameterType(p) → ParameterType. " +
                    "Valeur selon type : GetRealValue(p), GetTextValue(p), GetIntegerValue(p), GetBooleanValue(p) — PAS de GetValue() generique. " +
                    "Operations : TopSolidHost.Operations.GetOperations(docId). " +
                    "Nom element : TopSolidHost.Elements.GetName(elementId) ou GetFriendlyName(elementId). " +
                    "Projet courant : TopSolidHost.Pdm.GetCurrentProject(). " +
                    "Projets ouverts : TopSolidHost.Pdm.GetOpenProjects(true, true). " +
                    "Nom PDM : TopSolidHost.Pdm.GetName(pdmId). " +
                    "Contenu dossier : TopSolidHost.Pdm.GetConstituents(pdmId, out folders, out docs). " +
                    "INTERDIT : $\"\", TopSolidHost.Instance, Documents.ActiveDocument, GetActiveDocument(), GetValue(), GetExpression(), reflexion.",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["code"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Corps de la methode Run() en C# 5 UNIQUEMENT. " +
                                "PAS de using/namespace/class/accolades. " +
                                "Usings deja inclus : System, System.Collections.Generic, System.Linq, System.Text, System.IO, TopSolid.Kernel.Automating, TopSolid.Cad.Design.Automating. " +
                                "Utiliser + ou string.Format() (pas $\"\"). " +
                                "Terminer par return \"resultat\". " +
                                "Exemple esquisses : " +
                                "var docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return \"Aucun document.\"; " +
                                "var sketches = TopSolidHost.Sketches2D.GetSketches(docId); var sb = new StringBuilder(); " +
                                "foreach (var s in sketches) sb.AppendLine(TopSolidHost.Elements.GetName(s)); return sb.ToString(); " +
                                "Exemple parametres avec valeurs : " +
                                "var docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return \"Aucun document.\"; " +
                                "var pars = TopSolidHost.Parameters.GetParameters(docId); var sb = new StringBuilder(); " +
                                "foreach (var p in pars) { var t = TopSolidHost.Parameters.GetParameterType(p); string v = \"\"; " +
                                "if (t == ParameterType.Real) v = TopSolidHost.Parameters.GetRealValue(p).ToString(); " +
                                "else if (t == ParameterType.Text) v = TopSolidHost.Parameters.GetTextValue(p); " +
                                "else if (t == ParameterType.Integer) v = TopSolidHost.Parameters.GetIntegerValue(p).ToString(); " +
                                "else if (t == ParameterType.Boolean) v = TopSolidHost.Parameters.GetBooleanValue(p).ToString(); " +
                                "else v = t.ToString(); " +
                                "sb.AppendLine(TopSolidHost.Elements.GetFriendlyName(p) + \" = \" + v); } return sb.ToString();"
                        }
                    },
                    ["required"] = new JArray { "code" }
                }
            }, Execute);
        }

        /// <summary>
        /// Executes a dynamic C# script against TopSolid Automation API.
        /// </summary>
        public string Execute(JObject arguments)
        {
            try
            {
                string code = arguments["code"]?.ToString();

                if (string.IsNullOrWhiteSpace(code))
                    return "Erreur : le paramètre 'code' est requis.";

                var connector = _connectorProvider();

                if (!connector.EnsureConnected())
                    return "Error: TopSolid not connected. Please check that TopSolid is running with Automation enabled.";

                // Appel au compilateur et exécuteur dynamique
                return ScriptExecutor.Execute(code);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ExecuteScriptTool] Unexpected error: {ex.Message}");
                return $"Erreur lors de l'exécution du script : {ex.Message}";
            }
        }
    }
}
