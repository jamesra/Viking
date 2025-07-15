using Viking.AnnotationServiceTypes.Interfaces;
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

        public ulong ID => (ulong)type.ID;

        public string Name => type.Name;

        public string Code => type.Code;

        public ulong? ParentID => (ulong?)type.ParentID;

        public string[] Tags => type.Tags;

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
