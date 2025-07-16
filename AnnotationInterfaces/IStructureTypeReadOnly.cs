using System;
using System.Collections.Generic;

namespace Viking.AnnotationServiceTypes.Interfaces
{ 

    public interface IStructureTypeReadOnly : IEquatable<IStructureTypeReadOnly>
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