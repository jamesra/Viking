﻿using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureTypeAdapter : IStructureType
    {
        private readonly StructureType type;
        public WCFStructureTypeAdapter(StructureType t)
        {
            type = t;
        }

        public ulong ID
        {
            get
            {
                return (ulong)type.ID;
            }
        }

        public string Name
        {
            get
            {
                return type.Name;
            }
        }

        public string Code
        {
            get
            {
                return type.Code;
            }
        }

        public ulong? ParentID
        {
            get
            {
                return (ulong?)type.ParentID;
            }
        }

        public string[] Tags
        {
            get
            {
                return type.Tags;
            }
        }

        public bool Equals(IStructureType other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }
    }
}
