﻿using Viking.AnnotationServiceTypes.Interfaces;
using System;

namespace AnnotationVizLib.SimpleOData
{
    class StructureType : IStructureType, IEquatable<StructureType>
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

        public bool Equals(IStructureType other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(StructureType other)
        {
            return this.Equals((IStructureType)other);
        }
    }
}
