using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureLinkAdapter : IStructureLink
    {
        private readonly StructureLink structureLink;

        public WCFStructureLinkAdapter(StructureLink sl)
        {
            this.structureLink = sl;
        }

        public bool Directional => !structureLink.Bidirectional;

        public ulong SourceID => (ulong)structureLink.SourceID;

        public ulong TargetID => (ulong)structureLink.TargetID;

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
    }
}
