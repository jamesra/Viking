using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib.OData
{
    class ODataStructureLinkAdapter : IStructureLink
    {
        private readonly StructureLink structureLink;

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
            if (other is null)
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
