using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Linq;

namespace AnnotationVizLib.OData
{
    class ODataStructureTypeAdapter(StructureType t) : IStructureTypeReadOnly
    {
        private readonly StructureType type = t ?? throw new ArgumentNullException();

        public ulong ID => (ulong)type.ID;

        public string Name => type.Name;

        public string Code => type.Code;

        public ulong? ParentID => (ulong?)type.ParentID;

        public string[] Tags => [.. ObjAttribute.Parse(type.Tags).Select(a => a.ToString())];

        public bool Equals(IStructureTypeReadOnly other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(StructureType other) => this.Equals((IStructureTypeReadOnly)other);
    }
}
