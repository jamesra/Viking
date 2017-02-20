
using System;

namespace Annotation
{
    /// <summary>
    /// Determines which types of objects are valid targets for the command
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : System.Attribute
    {
        /// <summary>
        /// Object types that the command is active for
        /// </summary>
        public string Name;

        public ColumnAttribute(string name)
        {
            this.Name = name;
        }

    } 
}