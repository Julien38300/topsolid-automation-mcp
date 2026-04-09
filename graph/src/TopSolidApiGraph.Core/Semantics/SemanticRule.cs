using Newtonsoft.Json;

namespace TopSolidApiGraph.Core.Semantics
{
    /// <summary>
    /// Defines a semantic rule that modifies graph behavior for a specific node or method.
    /// </summary>
    public class SemanticRule
    {
        /// <summary>
        /// Target name to match. Can be a type name (e.g. "ISketches2D"),
        /// a method name (e.g. "GetElements"), a regex pattern, or a namespace prefix.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Match mode:
        /// "method" - match exact method name.
        /// "type" - match exact short or full type name.
        /// "type_regex" - match regex against short type name.
        /// "namespace" - match against full type name (namespace prefix).
        /// </summary>
        public string MatchOn { get; set; } = "type";

        /// <summary>
        /// Gets a value indicating whether this rule uses regex matching.
        /// </summary>
        [JsonIgnore]
        public bool IsRegex
        {
            get { return MatchOn == "type_regex"; }
        }

        /// <summary>
        /// Override weight for Dijkstra. null = use default GetWeight().
        /// Low = preferred, high = penalized.
        /// </summary>
        public int? WeightOverride { get; set; }

        /// <summary>
        /// If true, edges matching this rule are excluded from the graph entirely.
        /// </summary>
        public bool Exclude { get; set; }

        /// <summary>
        /// Textual instruction injected into the edge for AI consumption.
        /// Exported in graph.json and shown in pathfinding results.
        /// </summary>
        public string Instruction { get; set; }
    }
}
