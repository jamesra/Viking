using System;

namespace Annotation.Interfaces
{
    public interface IStructureType : IEquatable<IStructureType>
    {
        ulong ID { get; }
        ulong? ParentID { get; }
        string Name { get; }
        /// <summary>
        /// Shorthand name 
        /// </summary>
        string Code { get; }
        string[] Tags { get; }
    }
}
