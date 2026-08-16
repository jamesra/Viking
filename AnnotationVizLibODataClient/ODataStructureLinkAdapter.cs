using System;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib.OData
{
    class ODataStructureLinkAdapter(StructureLink sl) : IStructureLink
    {
        private readonly StructureLink structureLink = sl;

        public bool Directional
        {
            get => !structureLink.Bidirectional;
            set => structureLink.Bidirectional = !value;
        }

        public ulong SourceID
        {
            get => (ulong)structureLink.SourceID;
            set => structureLink.SourceID = (long)value;
        }

        public ulong TargetID
        {
            get => (ulong)structureLink.TargetID;
            set => structureLink.TargetID = (long)value;
        }

        IStructureLinkKey IDataObjectWithKey<IStructureLinkKey>.ID
        {
            get => new StructureLinkKey(this);
            set => throw new NotSupportedException("ODataStructureLinkAdapter is a read-only view over an OData StructureLink.");
        }

        StructureLinkKey IDataObjectWithKey<StructureLinkKey>.ID
        {
            get => new StructureLinkKey(this);
            set => throw new NotSupportedException("ODataStructureLinkAdapter is a read-only view over an OData StructureLink.");
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

        public bool Equals(IStructureLinkKey other)
        {
            if (other is null)
                return false;

            return other.SourceID == this.SourceID &&
                   other.TargetID == this.TargetID &&
                   other.Directional == this.Directional;
        }

        public bool Equals(StructureLink other) => this.Equals((IStructureLink)other);
    }
}
