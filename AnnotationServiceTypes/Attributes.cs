
using System;

namespace Annotation
{
    /// <summary>
    /// Determines which types of objects are valid targets for the command
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute(string name) : System.Attribute
    {
        /// <summary>
        /// Object types that the command is active for
        /// </summary>
        public string Name = name;
    }
}