using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using AnnotationService.Types;
using Annotation.Interfaces;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureTypeAdapter : IStructureType
    {
        private StructureType type;
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
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }
    }
}
