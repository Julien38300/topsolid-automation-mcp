using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TopSolidApiGraph.Core.Utils;

namespace TopSolidApiGraph.Core.Semantics
{
    /// <summary>
    /// Loads and queries semantic rules from a JSON configuration file.
    /// </summary>
    public class SemanticRuleSet
    {
        private readonly List<SemanticRule> _rules;
        private readonly Dictionary<string, Regex> _compiledRegex = new Dictionary<string, Regex>();

        public SemanticRuleSet(List<SemanticRule> rules)
        {
            _rules = rules ?? new List<SemanticRule>();

            foreach (var rule in _rules)
            {
                if (rule.IsRegex && !string.IsNullOrEmpty(rule.Target))
                {
                    try
                    {
                        if (!_compiledRegex.ContainsKey(rule.Target))
                        {
                            _compiledRegex[rule.Target] = new Regex(rule.Target, RegexOptions.Compiled);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[SemanticRuleSet] Error compiling regex '{rule.Target}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Loads rules from a JSON file. Returns empty set if file not found.
        /// </summary>
        public static SemanticRuleSet LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"[SemanticRuleSet] No rules file at {filePath}, using empty rules.");
                return new SemanticRuleSet(new List<SemanticRule>());
            }

            var rules = JsonHelper.Load<List<SemanticRule>>(filePath);
            Console.Error.WriteLine($"[SemanticRuleSet] Loaded {rules.Count} semantic rules.");
            return new SemanticRuleSet(rules);
        }

        /// <summary>
        /// Finds the first matching rule for a given method name and target type name.
        /// </summary>
        public SemanticRule FindMatch(string methodName, string targetTypeName)
        {
            if (string.IsNullOrEmpty(targetTypeName)) return null;

            // Extract short name from full qualified name
            string shortTarget = targetTypeName;
            int lastDot = targetTypeName.LastIndexOf('.');
            if (lastDot >= 0) shortTarget = targetTypeName.Substring(lastDot + 1);

            return _rules.FirstOrDefault(r =>
            {
                switch (r.MatchOn)
                {
                    case "method":
                        return r.Target == methodName;
                    case "type":
                        return r.Target == targetTypeName || r.Target == shortTarget;
                    case "type_regex":
                        return _compiledRegex.TryGetValue(r.Target, out var regex) && regex.IsMatch(shortTarget);
                    case "namespace":
                        // Match either as a direct namespace prefix or the full type name
                        return targetTypeName.StartsWith(r.Target + ".") || targetTypeName == r.Target;
                    default:
                        // Fallback to exact type match for legacy compatibility
                        return r.Target == targetTypeName || r.Target == shortTarget;
                }
            });
        }

        public int Count => _rules.Count;
    }
}
