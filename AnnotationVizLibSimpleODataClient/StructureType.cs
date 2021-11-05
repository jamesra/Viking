using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.Generic;

namespace AnnotationVizLib.SimpleOData
{
    class StructureType : IStructureTypeReadOnly, IEquatable<StructureType>
    {
        public StructureType()
        {

        }

        public ulong ID
        {
            get; private set;
        }

        public string Name
        {
            get; private set;
        }

        public string Code
        {
            get; private set;
        }

        public ulong? ParentID
        {
            get; private set;
        }

        public string[] Tags
        {
            get; private set;
        }

        public string Notes
        {
            get; private set;
        }

        public IReadOnlyDictionary<string, string> Attributes
        {
            get; private set;
        }

        public bool Abstract
        {
            get; private set;
        }

        public uint Color
        {
            get; private set;
        }

        public int AllowedShapes
        {
            get; private set;
        }

        public bool Equals(IStructureTypeReadOnly other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(StructureType other)
        {
            return this.Equals((IStructureTypeReadOnly)other);
        }
    }
}
