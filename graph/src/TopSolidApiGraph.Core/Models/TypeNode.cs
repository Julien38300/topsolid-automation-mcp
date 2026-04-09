using System;

namespace TopSolidApiGraph.Core.Models
{
    /// <summary>
    /// Represents a unique type node in the API graph.
    /// </summary>
    public class TypeNode
    {
        /// <summary>
        /// Gets or sets the full name of the type.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the namespace of the type.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets whether the type is a primitive or basic system type.
        /// </summary>
        public bool IsPrimitive { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeNode"/> class.
        /// </summary>
        public TypeNode() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeNode"/> class with a type name.
        /// </summary>
        /// <param name="typeName">The full name of the type.</param>
        public TypeNode(string typeName)
        {
            this.TypeName = typeName;
        }
        
        /// <summary>
        /// Overrides Equals to compare type names.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is TypeNode node)) return false;
            return string.Equals(TypeName, node.TypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Overrides GetHashCode to hash based on the type name.
        /// </summary>
        public override int GetHashCode()
        {
            return TypeName != null ? TypeName.GetHashCode() : 0;
        }

        /// <summary>
        /// Returns a string representation of the type node.
        /// </summary>
        public override string ToString()
        {
            return TypeName;
        }
    }
}
