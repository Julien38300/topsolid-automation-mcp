using System;
using System.Collections.Generic;
using System.Linq;
using TopSolidApiGraph.Core;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Result of a type name resolution.
    /// </summary>
    public class ResolveResult
    {
        public bool Found { get; set; }
        public string FullName { get; set; }
        public List<string> Alternatives { get; set; }
    }

    /// <summary>
    /// Resolves short type names to full type names based on graph data.
    /// </summary>
    public class TypeNameResolver
    {
        private readonly Dictionary<string, List<string>> _shortToFull = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _allFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public TypeNameResolver(TypeGraph graph)
        {
            // Register common primitives
            Register("void", "System.Void");
            Register("string", "System.String");
            Register("int", "System.Int32");
            Register("bool", "System.Boolean");
            Register("double", "System.Double");
            Register("object", "System.Object");

            // Extract all types from graph
            foreach (var edge in graph.GetEdges())
            {
                RegisterFromFullName(edge.Source.TypeName);
                RegisterFromFullName(edge.Target.TypeName);
            }
        }

        private void RegisterFromFullName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return;
            if (_allFullNames.Contains(fullName)) return;

            _allFullNames.Add(fullName);

            // Register as itself
            Register(fullName, fullName);

            // Register short name (last component)
            var parts = fullName.Split('.');
            var shortName = parts[parts.Length - 1];
            Register(shortName, fullName);
        }

        private void Register(string key, string value)
        {
            if (!_shortToFull.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _shortToFull[key] = list;
            }
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        /// <summary>
        /// Resolves an input string to a full type name.
        /// </summary>
        public ResolveResult Resolve(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new ResolveResult { Found = false };

            input = input.Trim();
            if (_shortToFull.TryGetValue(input, out var matches))
            {
                if (matches.Count == 1)
                {
                    return new ResolveResult { Found = true, FullName = matches[0] };
                }
                return new ResolveResult { Found = false, Alternatives = matches };
            }

            return new ResolveResult { Found = false };
        }

        /// <summary>
        /// Gets suggestions for a type name by partial matching.
        /// </summary>
        public List<string> GetSuggestions(string input, int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(input)) return _allFullNames.Take(limit).ToList();

            return _allFullNames
                .Where(n => n.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(n => n.Length)
                .Take(limit)
                .ToList();
        }
    }
}
