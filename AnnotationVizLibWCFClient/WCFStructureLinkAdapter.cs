using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationService.Types;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureLinkAdapter : IStructureLink
    {
        private StructureLink structureLink;

        public WCFStructureLinkAdapter(StructureLink sl)
        {
            this.structureLink = sl;
        }

        public bool Directional
        {
            get
            {
                return !structureLink.Bidirectional;
            }
        }

        public ulong SourceID
        {
            get
            {
                return (ulong)structureLink.SourceID;
            }
        }

        public ulong TargetID
        {
            get
            {
                return (ulong)structureLink.TargetID;
            }
        }

    }
}
