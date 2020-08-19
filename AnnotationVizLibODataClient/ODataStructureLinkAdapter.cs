using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODataClient.ConnectomeDataModel;
using Annotation.Interfaces;

namespace AnnotationVizLib.OData
{
    class ODataStructureLinkAdapter : IStructureLink
    {
        private StructureLink structureLink;

        public ODataStructureLinkAdapter(StructureLink sl)
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

        public bool Equals(IStructureLink other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.SourceID == this.SourceID &&
                other.TargetID == this.TargetID &&
                other.Directional == this.Directional)
                return true;

            return false;
        }

        public bool Equals(StructureLink other)
        {
            return this.Equals((IStructureLink)other);
        }
    }
}
