using System;

namespace TopSolidApiGraph.Core.Models
{
    /// <summary>
    /// Represents a directed edge in the API graph, corresponding to a method call.
    /// </summary>
    public class GraphEdge
    {
        /// <summary>
        /// Gets or sets the source type node.
        /// </summary>
        public TypeNode Source { get; set; }

        /// <summary>
        /// Gets or sets the target type node.
        /// </summary>
        public TypeNode Target { get; set; }

        /// <summary>
        /// Gets or sets the underlying method name for this transition.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Gets or sets the full signature of the method.
        /// </summary>
        public string MethodSignature { get; set; }

        /// <summary>
        /// Gets or sets the weight/cost of this transition.
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether the method is static.
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// Interface that owns this method (e.g. "IParameters", "ISketches2D").
        /// </summary>
        public string Interface { get; set; }

        /// <summary>
        /// Human-readable description from official documentation.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Minimum TopSolid version (e.g. "v7.6"). Null if unknown.
        /// </summary>
        public string Since { get; set; }

        /// <summary>
        /// Semantic instruction for AI guidance. Null if no rule applies.
        /// </summary>
        public string SemanticHint { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphEdge"/> class.
        /// </summary>
        public GraphEdge() { }

        /// <summary>
        /// Returns a string representation of the edge.
        /// </summary>
        public override string ToString()
        {
            return $"{Source} -> {MethodName} -> {Target}";
        }
    }
}
