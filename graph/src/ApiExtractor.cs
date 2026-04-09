using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TopSolidApiGraph.Core.Models;
using TopSolidApiGraph.Core.Utils;

namespace TopSolidApiGraph
{
    /// <summary>
    /// Statistics for the extraction process.
    /// </summary>
    public class ExtractionStats
    {
        public int TotalMethodsFound;
        public int ExcludedSpecialName;
        public int ExcludedSystemObject;
        public int RetainedMethods;
    }

    /// <summary>
    /// Service for extracting method signatures from .NET assemblies using reflection.
    /// </summary>
    public class ApiExtractor
    {
        private readonly List<ExtractedMethod> _methods = new List<ExtractedMethod>();
        public ExtractionStats Stats { get; private set; } = new ExtractionStats();

        /// <summary>
        /// Extracts public methods from the specified assembly DLL.
        /// </summary>
        /// <param name="dllPath">The absolute path to the .NET assembly.</param>
        /// <returns>A list of extracted methods.</returns>
        public List<ExtractedMethod> Extract(string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                Console.WriteLine($"[ApiExtractor] Error: DLL path '{dllPath}' is invalid or not found.");
                return new List<ExtractedMethod>();
            }

            // Register resolve handler for dependencies in the same folder
            string assemblyDir = Path.GetDirectoryName(dllPath);
            ResolveEventHandler resolveHandler = (sender, args) =>
            {
                string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                string dependencyPath = Path.Combine(assemblyDir, assemblyName);
                
                if (File.Exists(dependencyPath))
                {
                    return Assembly.ReflectionOnlyLoadFrom(dependencyPath);
                }
                return null;
            };

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolveHandler;

            try
            {
                Console.WriteLine($"[ApiExtractor] Reflection-only loading: {dllPath}");
                Assembly assembly = Assembly.ReflectionOnlyLoadFrom(dllPath);
                
                _methods.Clear();
                Stats = new ExtractionStats();

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.WriteLine("[ApiExtractor] Warning: Some types could not be loaded due to missing dependencies. Skipping as requested.");
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    try
                    {
                        if (!type.IsPublic && !type.IsNestedPublic) continue;

                        // Get public instance and static methods
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

                        foreach (var method in methods)
                        {
                            Stats.TotalMethodsFound++;

                            if (method.IsSpecialName)
                            {
                                if (!method.Name.StartsWith("get_") || method.Name == "get_Arguments")
                                {
                                    Stats.ExcludedSpecialName++;
                                    continue;
                                }
                            }

                            if (method.DeclaringType.FullName == "System.Object")
                            {
                                Stats.ExcludedSystemObject++;
                                continue;
                            }

                            var extracted = new ExtractedMethod
                            {
                                Name = method.Name,
                                ReturnType = CleanTypeName(method.ReturnType.FullName ?? method.ReturnType.Name),
                                DeclaringType = CleanTypeName(type.FullName ?? type.Name),
                                IsStatic = method.IsStatic
                            };

                            foreach (var param in method.GetParameters())
                            {
                                extracted.Parameters.Add(CleanTypeName(param.ParameterType.FullName ?? param.ParameterType.Name));
                            }

                            _methods.Add(extracted);
                        }
                    }
                    catch (Exception)
                    {
                        // Some types might fail even in a loop if they inherit from missing types
                        continue;
                    }
                }

                Stats.RetainedMethods = _methods.Count;
                Console.WriteLine($"[ApiExtractor] Done: {Stats.RetainedMethods} methods retained (Total: {Stats.TotalMethodsFound})");
                return _methods;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApiExtractor] Critical error during extraction: {ex.Message}");
                return new List<ExtractedMethod>();
            }
            finally
            {
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolveHandler;
            }
        }

        /// <summary>
        /// Saves the extracted methods to a JSON file.
        /// </summary>
        /// <param name="outputFilePath">The path to the output JSON file.</param>
        public void SaveToJson(string outputFilePath)
        {
            JsonHelper.Save(_methods, outputFilePath);
        }

        /// <summary>
        /// Cleans the type name to handle generic types and long names.
        /// </summary>
        /// <param name="typeName">The full type name.</param>
        /// <returns>A cleaner type name.</returns>
        private string CleanTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "void";
            
            // Basic cleaning for simpler graph nodes
            if (typeName.Contains("`"))
            {
                // Handle generics: Example<T>
                int backtickIndex = typeName.IndexOf('`');
                return typeName.Substring(0, backtickIndex);
            }
            
            return typeName;
        }
    }
}
