using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CSharp;
using TopSolid.Kernel.Automating;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Compiles and runs C# code dynamically against the TopSolid Automation API.
    /// Generated scripts are compiled by <see cref="CSharpCodeProvider"/>, i.e. in C# 5:
    /// no string interpolation is allowed in the emitted source.
    /// </summary>
    public static class ScriptExecutor
    {
        private const string TopSolidDllName = "TopSolid.Kernel.Automating.dll";
        private const string TopSolidDesignDllName = "TopSolid.Cad.Design.Automating.dll";
        private const string TopSolidDraftingDllName = "TopSolid.Cad.Drafting.Automating.dll";

        /// <summary>Default execution timeout, in seconds (overridable with TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC).</summary>
        private const int DefaultTimeoutSeconds = 60;

        /// <summary>Maximum number of compiled scripts kept in the cache.</summary>
        private const int MaxCacheEntries = 200;

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, MethodInfo> CompiledCache = new Dictionary<string, MethodInfo>();
        private static readonly List<string> CacheOrder = new List<string>();

        /// <summary>
        /// Symbols rejected before compilation. The tool is exposed to LLM clients, so scripts
        /// must not spawn processes, open sockets, use reflection or delete files.
        /// System.IO itself stays available (export recipes need it): only the destructive
        /// members are listed here.
        /// </summary>
        private static readonly string[] BlockedSymbols =
        {
            "System.Diagnostics.Process",
            "System.Net",
            "System.Reflection",
            "Assembly.",
            "DllImport",
            "File.Delete",
            "Directory.Delete",
            "Microsoft.Win32.Registry",
            "Environment.Exit"
        };

        private static string TopSolidBinPath
        {
            get { return TopSolidPathResolver.Resolve(); }
        }

        /// <summary>
        /// Executes a dynamic C# script, forcing transactional (modification) mode.
        /// </summary>
        public static string ExecuteModification(string userCode)
        {
            return Execute(userCode, true, true);
        }

        /// <summary>
        /// Compiles a user C# script WITHOUT executing it. Used by
        /// <c>topsolid_compile</c> for dry-run validation by LLMs generating code.
        /// Returns "OK" on success (with a diagnostic summary), or the error list.
        /// No TopSolid connection required (only the DLLs need to load).
        /// </summary>
        /// <param name="userCode">C# fragment (body of the generated Run method).</param>
        /// <param name="forceModification">Compile as a modification (transactional) script.</param>
        /// <param name="autoDetect">
        /// When false, <see cref="DetectModification"/> is not called and the mode is exactly
        /// the one given by <paramref name="forceModification"/>.
        /// </param>
        public static string CompileOnly(string userCode, bool forceModification = false, bool autoDetect = true)
        {
            try
            {
                string preprocessed = PreprocessCode(userCode);

                string blocked = CheckForBlockedApis(preprocessed);
                if (blocked != null) return blocked;

                bool isModification = autoDetect
                    ? (forceModification || DetectModification(preprocessed))
                    : forceModification;

                int headerLineCount;
                string wrappedCode = WrapCode(preprocessed, isModification, false, out headerLineCount);

                using (var provider = new CSharpCodeProvider())
                {
                    string parameterError;
                    CompilerParameters parameters = BuildCompilerParameters(out parameterError);
                    if (parameters == null) return parameterError;

                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, wrappedCode);

                    if (results.Errors.HasErrors)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("COMPILE ERRORS (" + results.Errors.Count + ")");
                        foreach (CompilerError err in results.Errors)
                        {
                            if (err.IsWarning) continue;
                            sb.AppendLine(string.Format("  Line {0}: {1} - {2}",
                                FormatUserLine(err.Line, headerLineCount), err.ErrorNumber, err.ErrorText));
                        }
                        return sb.ToString();
                    }

                    // Success: report warnings if any
                    int warnCount = 0;
                    foreach (CompilerError err in results.Errors)
                    {
                        if (err.IsWarning) warnCount++;
                    }

                    var okSb = new StringBuilder();
                    okSb.AppendLine("OK: code compiles successfully.");
                    okSb.AppendLine("Mode: " + (isModification ? "WRITE (transactional)" : "READ"));
                    if (warnCount > 0)
                    {
                        okSb.AppendLine("Warnings: " + warnCount);
                        foreach (CompilerError err in results.Errors)
                        {
                            if (!err.IsWarning) continue;
                            okSb.AppendLine("  Line " + FormatUserLine(err.Line, headerLineCount) + ": " + err.ErrorText);
                        }
                    }
                    return okSb.ToString();
                }
            }
            catch (InvalidOperationException ex)
            {
                // Raised by the "no return allowed" guard of the modification wrapper.
                return "Error: " + ex.Message;
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Executes a dynamic C# script.
        /// </summary>
        /// <param name="userCode">C# fragment (body of the generated Run method).</param>
        /// <param name="forceModification">Wrap the code in a modification transaction.</param>
        /// <param name="blockReturn">
        /// Reject code that returns a value (modify_script and WRITE recipes must set __message
        /// instead). A bare 'return;' stays allowed: it is rewritten into a jump to the commit path.
        /// </param>
        /// <param name="autoDetect">
        /// When false, <see cref="DetectModification"/> is not called and the mode is exactly
        /// the one given by <paramref name="forceModification"/>.
        /// </param>
        /// <returns>The string returned by the script, or an error description.</returns>
        public static string Execute(string userCode, bool forceModification = false, bool blockReturn = false, bool autoDetect = true)
        {
            try
            {
                string preprocessed = PreprocessCode(userCode);

                string blocked = CheckForBlockedApis(preprocessed);
                if (blocked != null) return blocked;

                bool isModification = autoDetect
                    ? (forceModification || DetectModification(preprocessed))
                    : forceModification;

                int headerLineCount;
                string wrappedCode = WrapCode(preprocessed, isModification, blockReturn, out headerLineCount);

                // Compiled scripts are cached: recipes have a frozen body and are compiled once.
                string cacheKey = ComputeSourceHash(wrappedCode);
                MethodInfo cachedMethod = TryGetCachedMethod(cacheKey);
                if (cachedMethod != null) return InvokeWithTimeout(cachedMethod);

                using (var provider = new CSharpCodeProvider())
                {
                    string parameterError;
                    CompilerParameters parameters = BuildCompilerParameters(out parameterError);
                    if (parameters == null) return parameterError;

                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, wrappedCode);

                    if (results.Errors.HasErrors)
                    {
                        var errors = new List<string>();
                        foreach (CompilerError err in results.Errors)
                        {
                            if (err.IsWarning) continue;
                            errors.Add(string.Format("Line {0}: {1} - {2}",
                                FormatUserLine(err.Line, headerLineCount), err.ErrorNumber, err.ErrorText));
                        }
                        return DiagnoseCompilationErrors(errors, userCode, wrappedCode);
                    }

                    Assembly assembly = results.CompiledAssembly;
                    Type type = assembly.GetType("TopSolidMcpServer.Dynamic.DynamicScript");
                    MethodInfo method = (type == null)
                        ? null
                        : type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

                    // Defensive: the wrapper always emits DynamicScript.Run(), so this only happens
                    // if WrapCode is changed without updating these names.
                    if (method == null)
                        return "System error: the generated assembly does not expose " +
                            "TopSolidMcpServer.Dynamic.DynamicScript.Run().";

                    StoreCachedMethod(cacheKey, method);
                    return InvokeWithTimeout(method);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Raised by the "no return allowed" guard of the modification wrapper.
                return "Error: " + ex.Message;
            }
            catch (TargetInvocationException tiex)
            {
                Exception inner = tiex.InnerException ?? tiex;
                return "Execution error: " + inner.GetType().Name + " - " + inner.Message + "\n" + inner.StackTrace;
            }
            catch (Exception ex)
            {
                return "System error: " + ex.Message;
            }
        }

        /// <summary>
        /// Runs the compiled Run() method on a dedicated thread and gives up after the timeout
        /// configured by TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC (default 60 seconds), so that an endless
        /// loop in a user script cannot freeze the single-threaded stdio loop.
        /// KNOWN LIMITATION: a script that overruns the timeout is NOT killed. Thread.Abort is
        /// unsupported and unsafe, so the thread is only marked as background (it cannot keep the
        /// process alive) and keeps running — possibly still holding a TopSolid transaction —
        /// until it finishes on its own.
        /// </summary>
        private static string InvokeWithTimeout(MethodInfo method)
        {
            int timeoutSeconds = GetTimeoutSeconds();
            string result = null;
            Exception failure = null;

            var worker = new Thread(delegate()
            {
                try
                {
                    result = (string)method.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            worker.IsBackground = true;
            worker.Name = "TopSolidMcpScript";
            worker.Start();

            if (!worker.Join(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                Console.Error.WriteLine("[MCP-ERROR] Script execution timed out after " + timeoutSeconds +
                    " s. The script thread is left running in the background.");
                return "Error: script execution timed out after " + timeoutSeconds + " seconds. " +
                    "The script thread is still running in the background and cannot be stopped safely; " +
                    "check TopSolid before running another script. " +
                    "Set TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC to raise the limit.";
            }

            if (failure != null)
            {
                var invocationFailure = failure as TargetInvocationException;
                Exception inner = (invocationFailure != null && invocationFailure.InnerException != null)
                    ? invocationFailure.InnerException
                    : failure;
                return "Execution error: " + inner.GetType().Name + " - " + inner.Message + "\n" + inner.StackTrace;
            }

            return result ?? string.Empty;
        }

        /// <summary>
        /// Reads the execution timeout from TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC, falling back to
        /// <see cref="DefaultTimeoutSeconds"/> when unset or invalid.
        /// </summary>
        private static int GetTimeoutSeconds()
        {
            string raw = Environment.GetEnvironmentVariable("TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC");
            int seconds;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw.Trim(), out seconds) && seconds > 0)
                return seconds;
            return DefaultTimeoutSeconds;
        }

        /// <summary>
        /// Builds the compiler parameters. Returns null and sets <paramref name="error"/> when the
        /// TopSolid kernel assembly cannot be found. Design and Drafting assemblies are referenced
        /// only when present, mirroring <see cref="BuildUsingNamespaces"/>.
        /// </summary>
        private static CompilerParameters BuildCompilerParameters(out string error)
        {
            error = null;

            var parameters = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                TreatWarningsAsErrors = false
            };

            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("System.Data.dll");
            parameters.ReferencedAssemblies.Add("System.Xml.dll");
            parameters.ReferencedAssemblies.Add("System.Linq.dll");

            // TopSolid references must be absolute paths (the assemblies are not copied locally).
            string kernelDllPath = Path.Combine(TopSolidBinPath, TopSolidDllName);
            if (!File.Exists(kernelDllPath))
            {
                error = "Error: TopSolid assembly not found at " + kernelDllPath +
                    ". Set TOPSOLID_BIN_PATH to the TopSolid bin directory.";
                return null;
            }
            parameters.ReferencedAssemblies.Add(kernelDllPath);

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

            return parameters;
        }

        /// <summary>
        /// Builds the list of namespaces imported by generated scripts. Design and Drafting are
        /// added ONLY when the matching assembly exists in the TopSolid bin directory: on an
        /// installation without those modules, emitting the using directive unconditionally makes
        /// every script and every recipe fail to compile with CS0246.
        /// </summary>
        private static List<string> BuildUsingNamespaces()
        {
            var namespaces = new List<string>
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.IO",
                "TopSolid.Kernel.Automating"
            };

            if (HasAssembly(TopSolidDesignDllName))
                namespaces.Add("TopSolid.Cad.Design.Automating");

            if (HasAssembly(TopSolidDraftingDllName))
                namespaces.Add("TopSolid.Cad.Drafting.Automating");

            return namespaces;
        }

        /// <summary>
        /// True when the given assembly file exists in the resolved TopSolid bin directory.
        /// </summary>
        private static bool HasAssembly(string dllName)
        {
            try
            {
                return File.Exists(Path.Combine(TopSolidBinPath, dllName));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Rejects scripts referencing dangerous APIs. Returns null when the code is allowed,
        /// otherwise the error message naming the blocked symbol. Set TOPSOLID_MCP_ALLOW_UNSAFE=1
        /// to bypass the check.
        /// </summary>
        private static string CheckForBlockedApis(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            string allowUnsafe = Environment.GetEnvironmentVariable("TOPSOLID_MCP_ALLOW_UNSAFE");
            if (allowUnsafe != null && allowUnsafe.Trim() == "1") return null;

            // Literals and comments are neutralized first so that a symbol quoted in a message
            // or in a comment is not mistaken for a real call.
            string sanitized = StripLiteralsAndComments(code);

            foreach (string symbol in BlockedSymbols)
            {
                if (sanitized.IndexOf(symbol, StringComparison.Ordinal) >= 0)
                {
                    return "Error: the script references a blocked API: '" + symbol + "'. " +
                        "Process launching, networking, reflection, registry access and file/directory " +
                        "deletion are refused. Set TOPSOLID_MCP_ALLOW_UNSAFE=1 to allow them.";
                }
            }

            return null;
        }

        /// <summary>
        /// Strips common LLM code wrappers to extract a bare method body.
        /// Handles 4 patterns: namespace/class wrapper, using+Run(), using-only, raw body.
        /// </summary>
        private static string PreprocessCode(string code)
        {
            if (code == null) return string.Empty;

            // Pattern 1 & 2: namespace or public class present -> extract Run() body
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
        /// Detects whether the script modifies the TopSolid model, so that it can be wrapped in a
        /// transaction. Every pattern is anchored on a TopSolid host receiver (TopSolidHost,
        /// TopSolidDesignHost or TopSolidDraftingHost followed by a member chain): BCL calls such
        /// as dict.Remove(key), list.AddRange(x), sb.Remove(0, 2) or
        /// System.IO.Directory.CreateDirectory(path) must NOT be treated as model modifications.
        /// </summary>
        private static bool DetectModification(string code)
        {
            string sanitized = StripLiteralsAndComments(code);

            // If the user already handles StartModification themselves, do NOT auto-wrap
            // (would cause double declaration of docId/pdmId and a nested StartModification).
            if (Regex.IsMatch(sanitized, @"\bStartModification\s*\("))
                return false;

            // TopSolidHost.Pdm. / TopSolidDesignHost.Assemblies. / TopSolidHost.Documents. ...
            const string hostReceiver =
                @"\b(?:TopSolidHost|TopSolidDesignHost|TopSolidDraftingHost)\b(?:\s*\.\s*[A-Za-z_]\w*)*\s*\.\s*";

            string[] mutatingMembers =
            {
                @"Set\w*\s*\(",
                @"Create\w*\s*\(",
                @"Delete\w*\s*\(",
                @"Add\w*\s*\(",
                @"Modify\w*\s*\(",
                @"EnsureIsDirty\s*\(",
                @"Rename\w*\s*\(",
                @"Remove\w*\s*\("
            };

            return mutatingMembers.Any(member => Regex.IsMatch(sanitized, hostReceiver + member));
        }

        /// <summary>
        /// True when the code contains a real 'return' statement. String literals, character
        /// literals and comments are neutralized first, so that a return hidden in the middle of a
        /// line — for instance: if (docId.IsEmpty) return "no document"; — is caught as well.
        /// Missing it used to leave the modification transaction open and freeze the TopSolid session.
        /// </summary>
        private static bool ContainsReturnStatement(string code)
        {
            return Regex.IsMatch(StripLiteralsAndComments(code), @"\breturn\b");
        }

        /// <summary>
        /// Replaces every real 'return;' statement by a jump to the commit label of the generated
        /// modification wrapper. Literals and comments are masked first; the mask produced by
        /// <see cref="StripLiteralsAndComments"/> has exactly the same length as the input, so the
        /// match offsets are valid in the original text and a 'return;' quoted inside a string is
        /// left untouched. <paramref name="replaced"/> reports whether at least one jump was emitted.
        /// </summary>
        private static string RewriteBareReturns(string code, out bool replaced)
        {
            replaced = false;
            if (string.IsNullOrEmpty(code)) return code ?? string.Empty;

            string masked = StripLiteralsAndComments(code);
            MatchCollection matches = Regex.Matches(masked, @"\breturn\s*;");
            if (matches.Count == 0) return code;

            var sb = new StringBuilder(code.Length + matches.Count * 8);
            int last = 0;
            bool any = false;
            foreach (Match match in matches)
            {
                // The mask blanks literal contents to spaces, so 'return "x";' also matches the
                // pattern in the masked text. Only rewrite when the ORIGINAL slice really is a
                // bare 'return;' — a value-returning statement must stay untouched so that the
                // blockReturn guard can still see and refuse it.
                if (!Regex.IsMatch(code.Substring(match.Index, match.Length), @"^return\s*;$"))
                    continue;

                sb.Append(code, last, match.Index - last);
                sb.Append("goto __commit;");
                last = match.Index + match.Length;
                any = true;
            }

            if (!any) return code;

            sb.Append(code, last, code.Length - last);
            replaced = true;
            return sb.ToString();
        }

        /// <summary>
        /// Maps a compiler line number back onto the user's own code by removing the generated
        /// header. Diagnostics that land inside the generated header or footer cannot be attributed
        /// to a user line, so they are reported with their generated position instead of the
        /// meaningless zero or negative number a plain subtraction would produce.
        /// </summary>
        private static string FormatUserLine(int compilerLine, int headerLineCount)
        {
            int userLine = compilerLine - headerLineCount;
            if (userLine >= 1) return userLine.ToString();
            return "(generated " + compilerLine + ")";
        }

        /// <summary>
        /// Returns a copy of the code where string literals, character literals, line comments and
        /// block comments are replaced by spaces. Newlines are preserved so that line numbers stay
        /// valid. Used by the return guard, the blocked-API check and the modification detection.
        /// The result ALWAYS has the same length as the input (every branch emits one character per
        /// consumed character): <see cref="RewriteBareReturns"/> relies on that to map match offsets
        /// back onto the original text, so keep that property if this method is ever changed.
        /// </summary>
        private static string StripLiteralsAndComments(string code)
        {
            if (string.IsNullOrEmpty(code)) return code ?? string.Empty;

            var sb = new StringBuilder(code.Length);
            int i = 0;
            int n = code.Length;

            while (i < n)
            {
                char c = code[i];

                // Line comment
                if (c == '/' && i + 1 < n && code[i + 1] == '/')
                {
                    while (i < n && code[i] != '\n')
                    {
                        sb.Append(' ');
                        i++;
                    }
                    continue;
                }

                // Block comment
                if (c == '/' && i + 1 < n && code[i + 1] == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n)
                    {
                        if (code[i] == '*' && i + 1 < n && code[i + 1] == '/')
                        {
                            sb.Append("  ");
                            i += 2;
                            break;
                        }
                        sb.Append(code[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                // Verbatim string literal: @"..." where "" is an escaped quote
                if (c == '@' && i + 1 < n && code[i + 1] == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n)
                    {
                        if (code[i] == '"')
                        {
                            if (i + 1 < n && code[i + 1] == '"')
                            {
                                sb.Append("  ");
                                i += 2;
                                continue;
                            }
                            sb.Append(' ');
                            i++;
                            break;
                        }
                        sb.Append(code[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                // Regular string literal
                if (c == '"')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (code[i] == '\\' && i + 1 < n)
                        {
                            sb.Append("  ");
                            i += 2;
                            continue;
                        }
                        if (code[i] == '"')
                        {
                            sb.Append(' ');
                            i++;
                            break;
                        }
                        sb.Append(code[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                // Character literal
                if (c == '\'')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (code[i] == '\\' && i + 1 < n)
                        {
                            sb.Append("  ");
                            i += 2;
                            continue;
                        }
                        if (code[i] == '\'')
                        {
                            sb.Append(' ');
                            i++;
                            break;
                        }
                        sb.Append(code[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Computes the SHA-256 hash (lowercase hex) of the wrapped source; used as the cache key.
        /// </summary>
        private static string ComputeSourceHash(string source)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Returns the cached Run() method for this source, or null when not cached.
        /// </summary>
        private static MethodInfo TryGetCachedMethod(string cacheKey)
        {
            lock (CacheLock)
            {
                MethodInfo method;
                if (CompiledCache.TryGetValue(cacheKey, out method)) return method;
                return null;
            }
        }

        /// <summary>
        /// Caches the compiled Run() method, evicting the oldest entry beyond
        /// <see cref="MaxCacheEntries"/>. Note: eviction only bounds the dictionary — the loaded
        /// assembly itself cannot be unloaded from the current AppDomain. The cache exists exactly
        /// to stop loading a new assembly on every call.
        /// </summary>
        private static void StoreCachedMethod(string cacheKey, MethodInfo method)
        {
            if (method == null) return;

            lock (CacheLock)
            {
                if (CompiledCache.ContainsKey(cacheKey)) return;

                while (CacheOrder.Count >= MaxCacheEntries)
                {
                    string oldest = CacheOrder[0];
                    CacheOrder.RemoveAt(0);
                    CompiledCache.Remove(oldest);
                }

                CompiledCache[cacheKey] = method;
                CacheOrder.Add(cacheKey);
            }
        }

        /// <summary>
        /// Wraps user code in a full modification block with StartModification, EnsureIsDirty,
        /// EndModification and Save, and returns the generated source without compiling it.
        /// The wrapper already declares <c>docId</c>, <c>pdmId</c> and <c>__message</c>: user code
        /// must USE them, never redeclare them (that would be a CS0128 duplicate local).
        /// </summary>
        public static string WrapModificationCode(string userCode)
        {
            string preprocessed = PreprocessCode(userCode);
            int headerLineCount;
            return WrapCode(preprocessed, true, true, out headerLineCount);
        }

        /// <summary>
        /// Diagnoses compilation errors and suggests fixes.
        /// </summary>
        private static string DiagnoseCompilationErrors(List<string> errors, string userCode, string wrappedCode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Compilation error:");
            foreach (var err in errors)
            {
                sb.AppendLine(err);
            }
            sb.AppendLine();

            // Detect common patterns
            string errorBlock = string.Join(" ", errors);

            // Pattern 1: braces mismatch (CS1513 / CS1022)
            if (errorBlock.Contains("CS1513") || errorBlock.Contains("CS1022"))
            {
                sb.AppendLine("--- Diagnostic ---");
                if (userCode.Contains("namespace ") || userCode.Contains("public class "))
                {
                    sb.AppendLine("Likely cause: the code contains namespace/class declarations that were not stripped correctly.");
                    sb.AppendLine("Fix: send ONLY the method body, without using directives, namespace, class or enclosing braces.");
                }
                else if (userCode.TrimStart().StartsWith("using "))
                {
                    sb.AppendLine("Likely cause: the code starts with 'using' directives, which leave stray braces after preprocessing.");
                    sb.AppendLine("Fix: do not include any 'using'. These namespaces are already imported: " +
                        string.Join(", ", BuildUsingNamespaces()) + ".");
                }
                else
                {
                    sb.AppendLine("Likely cause: unbalanced braces { } in the code.");
                    sb.AppendLine("Fix: check that every { has a matching }.");
                }
                sb.AppendLine();
                sb.AppendLine("Expected format:");
                sb.AppendLine("  var docId = TopSolidHost.Documents.EditedDocument;");
                sb.AppendLine("  if (docId.IsEmpty) return \"No document.\";");
                sb.AppendLine("  return TopSolidHost.Documents.GetName(docId);");
            }

            // Pattern 2: string interpolation (CS1056)
            if (errorBlock.Contains("CS1056") || errorBlock.Contains("CS1009"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Likely cause: string interpolation $\"...\" is used (not supported in C# 5).");
                sb.AppendLine("Fix: use string.Format() or concatenation with +.");
                sb.AppendLine("  Example: string.Format(\"Value: {0}\", val)  OR  \"Value: \" + val");
            }

            // Pattern 3: unknown type/method (CS0246, CS0103, CS0117)
            if (errorBlock.Contains("CS0246") || errorBlock.Contains("CS0103") || errorBlock.Contains("CS0117"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Likely cause: unknown type or method. The namespace may not be imported, or the method does not exist.");
                sb.AppendLine("Fix: use topsolid_api_help to check the exact signatures.");
                sb.AppendLine("Imported namespaces: " + string.Join(", ", BuildUsingNamespaces()) + ".");

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
                            sb.AppendLine("  Unknown type/method: " + unknownName);
                            sb.AppendLine("  Try: topsolid_api_help(\"" + unknownName + "\")");
                        }
                    }
                }
            }

            // Pattern 4: missing 'ref' keyword (CS1620)
            if (errorBlock.Contains("CS1620"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Likely cause: missing 'ref' argument. Some TopSolid methods take 'ref' parameters.");
                sb.AppendLine("Reminder: EnsureIsDirty(ref docId) — 'ref' is MANDATORY.");
                sb.AppendLine("Reminder: Documents.Open(ref docId) — 'ref' is MANDATORY.");
            }

            // Pattern 5: cannot convert type (CS0029, CS1503)
            if (errorBlock.Contains("CS0029") || errorBlock.Contains("CS1503"))
            {
                sb.AppendLine("--- Diagnostic ---");
                sb.AppendLine("Likely cause: wrong type passed to a method.");
                sb.AppendLine("Reminder: PdmObjectId != DocumentId != ElementId. Convert with:");
                sb.AppendLine("  PdmObjectId -> DocumentId: TopSolidHost.Documents.GetDocument(pdmId)");
                sb.AppendLine("  DocumentId -> PdmObjectId: TopSolidHost.Documents.GetPdmObject(docId)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Wraps user code into a compilable class. <paramref name="headerLineCount"/> receives the
        /// exact number of generated lines placed before the user code, so that compiler line
        /// numbers can be mapped back to the user's own lines.
        /// </summary>
        private static string WrapCode(string userCode, bool isModification, bool blockReturn, out int headerLineCount)
        {
            // A bare 'return;' cannot compile inside a string-returning method and must still reach
            // the commit path, so it is rewritten as a jump to the commit label. This runs BEFORE
            // the blockReturn guard on purpose: 'return;' is the documented way of leaving a
            // modification script early (the text is carried by __message), it cannot leave the
            // transaction open once rewritten, and rejecting it would break every recipe and every
            // modify_script body using that idiom. Only a value-returning 'return' is refused.
            bool hasCommitJump = false;
            if (isModification)
            {
                userCode = RewriteBareReturns(userCode, out hasCommitJump);
            }

            if (blockReturn && ContainsReturnStatement(userCode))
            {
                throw new InvalidOperationException("Code for a modification script must not return a value. " +
                    "Assign the __message variable to customize the returned message; a bare 'return;' " +
                    "may be used to exit early.");
            }

            var header = new List<string>();
            foreach (string ns in BuildUsingNamespaces())
            {
                header.Add("using " + ns + ";");
            }

            header.Add("");
            header.Add("namespace TopSolidMcpServer.Dynamic");
            header.Add("{");
            header.Add("    public class DynamicScript");
            header.Add("    {");
            header.Add("        public static string Run()");
            header.Add("        {");

            if (isModification)
            {
                header.Add("            TopSolidHost.Application.StartModification(\"TopSolid MCP\", false);");
                header.Add("            bool __committed = false;");
                header.Add("            try");
                header.Add("            {");
                header.Add("                DocumentId docId = TopSolidHost.Documents.EditedDocument;");
                header.Add("                PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);");
                header.Add("                string __message = \"Modification completed successfully.\";");
                header.Add("                TopSolidHost.Documents.EnsureIsDirty(ref docId);");
            }

            var footer = new List<string>();
            if (isModification)
            {
                // The label is emitted only when a 'return;' was actually rewritten into a jump:
                // an unreferenced label makes csc report CS0164 on every modification script.
                if (hasCommitJump)
                    footer.Add("                __commit:");
                footer.Add("                TopSolidHost.Application.EndModification(true, true);");
                footer.Add("                __committed = true;");
                footer.Add("                TopSolidHost.Pdm.Save(pdmId, true);");
                footer.Add("                return __message;");
                footer.Add("            }");
                footer.Add("            catch (Exception __ex)");
                footer.Add("            {");
                footer.Add("                return \"ERROR: \" + __ex.Message;");
                footer.Add("            }");
                footer.Add("            finally");
                footer.Add("            {");
                footer.Add("                // Closes the transaction on every exit path, including an early");
                footer.Add("                // 'return' or an exception inside the user code. A script that");
                footer.Add("                // returns a value before the commit point is therefore ABORTED:");
                footer.Add("                // say so on stderr, since its own return value hides the rollback.");
                footer.Add("                if (!__committed)");
                footer.Add("                {");
                footer.Add("                    try { TopSolidHost.Application.EndModification(false, false); } catch { }");
                footer.Add("                    try { System.Console.Error.WriteLine(\"[MCP-WARN] Modification transaction aborted: the script left the wrapper before the commit point.\"); } catch { }");
                footer.Add("                }");
                footer.Add("            }");
            }
            else
            {
                footer.Add("            return \"Script executed successfully (no value returned).\";");
            }

            footer.Add("        }");
            footer.Add("    }");
            footer.Add("}");

            // The header is joined with "\n" and followed by "\n" + userCode, so the first user
            // line lands exactly at compiler line header.Count + 1.
            headerLineCount = header.Count;

            return string.Join("\n", header) + "\n" + userCode + "\n" + string.Join("\n", footer);
        }
    }
}
