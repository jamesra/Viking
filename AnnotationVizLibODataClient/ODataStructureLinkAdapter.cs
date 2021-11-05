using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib.OData
{
    class ODataStructureLinkAdapter : IStructureLinkKey
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

        public int CompareTo(IStructureLinkKey other)
        {
            var result = SourceID.CompareTo(other.SourceID);
            if (result == 0)
            {
                result = TargetID.CompareTo(other.TargetID);
                if (result == 0)
                    result = Directional.CompareTo(other.Directional);
            }

            return result;
        }

        public bool Equals(IStructureLinkKey other)
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
            return this.Equals((IStructureLinkKey)other);
        }
    }
}
