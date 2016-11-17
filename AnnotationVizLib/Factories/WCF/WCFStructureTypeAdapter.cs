using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib.AnnotationService;

namespace AnnotationVizLib
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

        public ulong ParentID
        {
            get
            {
                return (ulong)type.ParentID;
            }
        }

        public string[] Tags
        {
            get
            {
                return type.Tags;
            }
        }
    }
}
