using System;

namespace Viking.AnnotationServiceTypes.Interfaces
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
