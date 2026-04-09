using System;
using System.Collections.Generic;

namespace TopSolidApiGraph.Core.Models
{
    /// <summary>
    /// Represents a method extracted from a .NET assembly via reflection.
    /// </summary>
    public class ExtractedMethod
    {
        /// <summary>
        /// Gets or sets the name of the method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the full name of the return type.
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// Gets or sets the list of parameter types in order.
        /// </summary>
        public List<string> Parameters { get; set; }

        /// <summary>
        /// Gets or sets the full name of the declaring type (parent class).
        /// </summary>
        public string DeclaringType { get; set; }

        /// <summary>
        /// Gets or sets whether the method is static.
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractedMethod"/> class.
        /// </summary>
        public ExtractedMethod()
        {
            Parameters = new List<string>();
        }

        /// <summary>
        /// Returns a string representation of the method signature.
        /// </summary>
        public override string ToString()
        {
            string parameters = string.Join(", ", Parameters);
            return $"{DeclaringType}.{Name}({parameters}) -> {ReturnType}";
        }
    }
}
