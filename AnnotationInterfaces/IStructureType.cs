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
          
        string Notes { get; }
        IReadOnlyDictionary<string, string> Attributes { get; }

        bool Abstract { get; }

        uint Color { get; }

        int AllowedShapes { get; }
    }

    public interface IStructureType : IDataObjectWithParent<long>, IEquatable<IStructureType>
    {  
        string Name { get; set; }
        /// <summary>
        /// Shorthand name 
        /// </summary>
        string Code { get; set; }

        string Notes { get; set; }
          
        string Attributes { get; set; }

        //string[] StructureTags { get; set; }

        /// <summary>
        /// True if the type cannot be instantiated as an annotation directly (another type must be a child of this type)
        /// </summary>
        bool Abstract { get; set; }

        uint Color { get; set; }

        int AllowedShapes { get; set; }

        IPermittedStructureLink[] PermittedLinks { get; set; }
    }
}
