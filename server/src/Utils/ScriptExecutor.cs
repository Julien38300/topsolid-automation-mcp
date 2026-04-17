using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using TopSolid.Kernel.Automating;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Compile et exécute du code C# dynamiquement contre l'API TopSolid Automation.
    /// </summary>
    public static class ScriptExecutor
    {
        private const string TopSolidBinPath = @"C:\Program Files\TOPSOLID\TopSolid 7.21\bin\";
        private const string TopSolidDllName = "TopSolid.Kernel.Automating.dll";
        private const string TopSolidDesignDllName = "TopSolid.Cad.Design.Automating.dll";
        private const string TopSolidDraftingDllName = "TopSolid.Cad.Drafting.Automating.dll";

        /// <summary>
        /// Exécute un script C# dynamique en forçant le mode modification.
        /// </summary>
        public static string ExecuteModification(string userCode)
        {
            return Execute(userCode, true, true);
        }

        /// <summary>
        /// Exécute un script C# dynamique.
        /// </summary>
        /// <param name="userCode">Le fragment de code C# (corps de la méthode Run).</param>
        /// <param name="forceModification">Force le wrapping dans un bloc de modification.</param>
        /// <returns>Le résultat string du script ou l'erreur.</returns>
        public static string Execute(string userCode, bool forceModification = false, bool blockReturn = false)
        {
            try
            {
                // Code will be validated by the compiler and diagnosed in DiagnoseCompilationErrors

                string preprocessed = PreprocessCode(userCode);
                bool isModification = forceModification || DetectModification(preprocessed);
                string wrappedCode = WrapCode(preprocessed, isModification, blockReturn);

                using (var provider = new CSharpCodeProvider())
                {
                    var parameters = new CompilerParameters
                    {
                        GenerateInMemory = true,
                        GenerateExecutable = false,
                        TreatWarningsAsErrors = false
                    };

                    // Références standards
                    parameters.ReferencedAssemblies.Add("System.dll");
                    parameters.ReferencedAssemblies.Add("System.Core.dll");
                    parameters.ReferencedAssemblies.Add("System.Data.dll");
                    parameters.ReferencedAssemblies.Add("System.Xml.dll");
                    parameters.ReferencedAssemblies.Add("System.Linq.dll");

                    // Référence TopSolid (doit pointer vers le chemin absolu car Private=false)
                    string topSolidDllPath = Path.Combine(TopSolidBinPath, TopSolidDllName);
                    if (!File.Exists(topSolidDllPath))
                    {
                        return $"Erreur : DLL TopSolid introuvable à l'emplacement : {topSolidDllPath}";
                    }
                    parameters.ReferencedAssemblies.Add(topSolidDllPath);

                    string designDllPath = Path.Combine(TopSolidBinPath, TopSolidDesignDllName);
                    if (File.Exists(designDllPath))
                    {
                        parameters.ReferencedAssemblies.Add(designDllPath);
                    }

                    string draftingDllPath = Path.Combine(TopSolidBinPath, TopSolidDraftingDllName);
                    if (File.Exists(draftingDllPath))
                    {
                        parameters.ReferencedAssemblies.Add(draftingDllPath);
                    }

                    var results = provider.CompileAssemblyFromSource(parameters, wrappedCode);

                    if (results.Errors.HasErrors)
                    {
                        var errors = new List<string>();
                        foreach (CompilerError err in results.Errors)
                        {
                            errors.Add(string.Format("Ligne {0} : {1} - {2}", err.Line - 14, err.ErrorNumber, err.ErrorText));
                        }
                        return DiagnoseCompilationErrors(errors, userCode, wrappedCode);
                    }

                    // Exécution
                    var assembly = results.CompiledAssembly;
                    var type = assembly.GetType("TopSolidMcpServer.Dynamic.DynamicScript");
                    var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

                    return (string)method.Invoke(null, null);
                }
            }
            catch (TargetInvocationException tied)
            {
                var inner = tied.InnerException ?? tied;
                return $"Erreur d'exécution : {inner.GetType().Name} - {inner.Message}\n{inner.StackTrace}";
            }
            catch (Exception ex)
            {
                return $"Erreur système : {ex.Message}";
            }
        }

        /// <summary>
        /// Strips common LLM code wrappers to extract a bare method body.
        /// Handles 4 patterns: namespace/class wrapper, using+Run(), using-only, raw body.
        /// </summary>
        private static string PreprocessCode(string code)
        {
            // Pattern 1 & 2: namespace or public class present → extract Run() body
            if (code.Contains("namespace ") || code.Contains("public class "))
            {
                string extracted = ExtractRunBody(code);
                if (extracted != null) return extracted;
            }

            // Pattern 3: static string Run() without namespace/class (e.g. LLM adds using + Run())
            if (code.Contains("static string Run()"))
            {
                string extracted = ExtractRunBody(code);
                if (extracted != null) return extracted;
            }

            // Pattern 4: code starts with using statements but no Run() or namespace
            string trimmed = code.Trim();
            if (trimmed.StartsWith("using "))
            {
                var lines = trimmed.Split(new[] { '\n' }, StringSplitOptions.None);
                var codeLines = new List<string>();
                bool pastUsings = false;
                foreach (var line in lines)
                {
                    string lt = line.Trim();
                    if (!pastUsings && lt.StartsWith("using ") && lt.EndsWith(";"))
                        continue;
                    if (!pastUsings && lt == string.Empty)
                        continue;
                    pastUsings = true;
                    codeLines.Add(line);
                }
                return string.Join("\n", codeLines).Trim();
            }

            return trimmed;
        }

        /// <summary>
        /// Extracts the body of the Run() method from a code block.
        /// Returns null if Run() cannot be found or its braces cannot be matched.
        /// </summary>
        private static string ExtractRunBody(string code)
        {
            int runStart = code.IndexOf("public static string Run()", StringComparison.Ordinal);
            if (runStart < 0)
                runStart = code.IndexOf("static string Run()", StringComparison.Ordinal);

            if (runStart < 0)
                return null;

            int braceOpen = code.IndexOf('{', runStart);
            if (braceOpen < 0)
                return null;

            int depth = 0;
            int braceClose = -1;
            for (int i = braceOpen; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}')
                {
                    depth--;
                    if (depth == 0) { braceClose = i; break; }
                }
            }

            if (braceClose > braceOpen)
                return code.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();

            return null;
        }

        /// <summary>
        /// Détecte si le script contient des mots-clés suggérant une modification de TopSolid.
        /// </summary>
        private static bool DetectModification(string code)
        {
            // If the user already handles StartModification themselves, do NOT auto-wrap
            // (would cause double declaration of docId/pdmId and double StartModification)
            if (Regex.IsMatch(code, @"\bStartModification\s*\("))
                return false;

            // Match method calls only (word boundary + parenthesis) to avoid false positives
            // e.g. "Set" must not match "SETUP" or "GetSettings"
            string[] modPatterns = {
                @"\bSetName\s*\(",
                @"\bSetValue\s*\(",
                @"\bCreate\w*\s*\(",
                @"\bDelete\w*\s*\(",
                @"\bAdd\w+\s*\(",
                @"\bModify\w*\s*\(",
                @"\bEnsureIsDirty\s*\(",
                @"\bRename\w*\s*\(",
                @"\bRemove\w*\s*\("
            };
            return modPatterns.Any(p => Regex.IsMatch(code, p));
        }

        /// <summary>
        /// Wraps user code in a full modification block with StartModification,
        /// EnsureIsDirty, EndModification, and Save.
        /// User code MUST declare and assign `docId` (DocumentId) before modifications.
        /// </summary>
        public static string WrapModificationCode(string userCode)
        {
            string preprocessed = PreprocessCode(userCode);
            return WrapCode(preprocessed, true, true);
        }

        /// <summary>
        /// Diagnose compilation errors and provide suggestions.
        /// </summary>
        private static string DiagnoseCompilationErrors(List<string> errors, string userCode, string wrappedCode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Erreur de compilation :");
            foreach (var err in errors)
            {
                sb.AppendLine(err);
            }
            sb.AppendLine();

            // Detect common patterns
            string errorBlock = string.Join(" ", errors);

            // Pattern 1 : Braces mismatch (CS1513 / CS1022)
            if (errorBlock.Contains("CS1513") || errorBlock.Contains("CS1022"))
            {
                sb.AppendLine("--- Diagnostic ---");
                if (userCode.Contains("namespace ") || userCode.Contains("public class "))
                {
                    sb.AppendLine("Cause probable : Le code contient des declarations namespace/class qui n'ont pas ete correctement nettoyees.");
                    sb.AppendLine("Solution : Envoyer UNIQUEMENT le corps de la methode, sans using, namespace, class, ni accolades englobantes.");
                }
                else if (userCode.TrimStart().StartsWith("using "))
                {
                    sb.AppendLine("Cause probable : Le code commence par des directives 'using' qui creent des accolades parasites apres le preprocessing.");
                    sb.AppendLine("Solution : Ne pas inclure de 'using'. Les namespaces System, System.Collections.Generic, System.Linq, TopSolid.Kernel.Automating et TopSolid.Cad.Design.Automating sont deja importes.");
                }
                else
                {
                    sb.AppendLine("Cause probable : Accolades { } desequilibrees dans le code.");
                    sb.AppendLine("Solution : Verifier que chaque { a un } correspondant.");
                }
                sb.AppendLine();
                sb.AppendLine("Rappel format attendu :");
                sb.AppendLine("  var docId = TopSolidHost.Documents.EditedDocument;");
                sb.AppendLine("  if (docId.IsEmpty) return \"Aucun document.\";");
                sb.AppendLine("  return TopSolidHost.Documents.GetName(docId);");
            }

            // Pattern 2 : String interpolation (CS1056)
            if (errorBlock.Contains("CS1056") || errorBlock.Contains("CS1009"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Cause probable : Utilisation de string interpolation $\"...\" (non supporte en C# 5).");
                sb.AppendLine("Solution : Utiliser string.Format() ou concatenation avec +.");
                sb.AppendLine("  Exemple : string.Format(\"Valeur: {0}\", val)  OU  \"Valeur: \" + val");
            }

            // Pattern 3 : Unknown type/method (CS0246, CS0103, CS0117)
            if (errorBlock.Contains("CS0246") || errorBlock.Contains("CS0103") || errorBlock.Contains("CS0117"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Cause probable : Type ou methode inconnu(e). Le namespace n'est peut-etre pas importe ou la methode n'existe pas.");
                sb.AppendLine("Solution : Utiliser topsolid_api_help pour verifier les signatures exactes.");
                sb.AppendLine("Namespaces disponibles : System, System.Collections.Generic, System.Linq, System.Text, System.IO, TopSolid.Kernel.Automating, TopSolid.Cad.Design.Automating.");

                // Try to extract the unknown name
                foreach (var err in errors)
                {
                    if (err.Contains("CS0246") || err.Contains("CS0103"))
                    {
                        // Extract the name between quotes
                        int q1 = err.IndexOf('\'');
                        int q2 = err.IndexOf('\'', q1 + 1);
                        if (q1 >= 0 && q2 > q1)
                        {
                            string unknownName = err.Substring(q1 + 1, q2 - q1 - 1);
                            sb.AppendLine("  Type/methode inconnu(e) : " + unknownName);
                            sb.AppendLine("  Essayer : topsolid_api_help(\"" + unknownName + "\")");
                        }
                    }
                }
            }

            // Pattern 4 : ref keyword missing (CS1620)
            if (errorBlock.Contains("CS1620"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Cause probable : Argument 'ref' manquant. Certaines methodes TopSolid utilisent 'ref'.");
                sb.AppendLine("Rappel : EnsureIsDirty(ref docId) — le 'ref' est OBLIGATOIRE.");
                sb.AppendLine("Rappel : Documents.Open(ref docId) — le 'ref' est OBLIGATOIRE.");
            }

            // Pattern 5 : Cannot convert type (CS0029, CS1503)
            if (errorBlock.Contains("CS0029") || errorBlock.Contains("CS1503"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Cause probable : Type incorrect passe a une methode.");
                sb.AppendLine("Rappel : PdmObjectId ≠ DocumentId ≠ ElementId. Convertir avec :");
                sb.AppendLine("  PdmObjectId → DocumentId : TopSolidHost.Documents.GetDocument(pdmId)");
                sb.AppendLine("  DocumentId → PdmObjectId : TopSolidHost.Documents.GetPdmObject(docId)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Enveloppe le code utilisateur dans une structure de classe compatible.
        /// </summary>
        private static string WrapCode(string userCode, bool isModification, bool blockReturn = false)
        {
            if (blockReturn && Regex.IsMatch(userCode, @"(?m)^\s*return\b"))
            {
                throw new InvalidOperationException("Le code pour modify_script ne doit pas contenir 'return'. " +
                    "Utilisez la variable __message pour personnaliser le message de retour.");
            }

            var header = new List<string>
            {
                "using System;",
                "using System.Collections.Generic;",
                "using System.Linq;",
                "using System.Text;",
                "using System.IO;",
                "using TopSolid.Kernel.Automating;",
                "using TopSolid.Cad.Design.Automating;",
                "using TopSolid.Cad.Drafting.Automating;",
                "",
                "namespace TopSolidMcpServer.Dynamic",
                "{",
                "    public class DynamicScript",
                "    {",
                "        public static string Run()",
                "        {"
            };

            if (isModification)
            {
                header.Add("            TopSolidHost.Application.StartModification(\"Cortana IA Script\", false);");
                header.Add("            try");
                header.Add("            {");
                header.Add("                DocumentId docId = TopSolidHost.Documents.EditedDocument;");
                header.Add("                PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);");
                header.Add("                string __message = \"Modification effectuée avec succès.\";");
                header.Add("                TopSolidHost.Documents.EnsureIsDirty(ref docId);");
            }

            // Replace bare 'return;' with 'goto __done;' in modification code
            // so that early exits properly go through EndModification/Save
            if (isModification)
            {
                userCode = Regex.Replace(userCode, @"\breturn\s*;", "goto __done;");
            }

            var footer = new List<string>();
            if (isModification)
            {
                footer.Add("                __done:");
                footer.Add("                TopSolidHost.Application.EndModification(true, true);");
                footer.Add("                TopSolidHost.Pdm.Save(pdmId, true);");
                footer.Add("                return __message;");
                footer.Add("            }");
                footer.Add("            catch (Exception __ex)");
                footer.Add("            {");
                footer.Add("                try { TopSolidHost.Application.EndModification(false, false); } catch { }");
                footer.Add("                return \"ERREUR: \" + __ex.Message;");
                footer.Add("            }");
            }
            else
            {
                footer.Add("            return \"Script exécuté avec succès (lecture seule).\";");
            }

            footer.Add("        }");
            footer.Add("    }");
            footer.Add("}");

            return string.Join("\n", header) + "\n" + userCode + "\n" + string.Join("\n", footer);
        }
    }
}
