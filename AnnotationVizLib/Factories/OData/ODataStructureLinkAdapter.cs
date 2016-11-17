using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib
{
    class ODataStructureLinkAdapter : IStructureLink
    {
        private StructureLink structureLink;

        public ODataStructureLinkAdapter(StructureLink sl)
        {
            this.structureLink = sl;
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
