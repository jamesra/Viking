using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Linq;

namespace AnnotationVizLib.OData
{
    class ODataStructureTypeAdapter : IStructureType
    {
        private readonly StructureType type;
        public ODataStructureTypeAdapter(StructureType t)
        {
            type = t ?? throw new ArgumentNullException();
        }

        public ulong ID => (ulong)type.ID;

        public string Name => type.Name;

        public string Code => type.Code;

        public ulong? ParentID => (ulong?)type.ParentID;

        public string[] Tags
        {
            get
            {
                return ObjAttribute.Parse(type.Tags).Select(a => a.ToString()).ToArray();
            }
        }

        public bool Equals(IStructureType other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(StructureType other)
        {
            return this.Equals((IStructureType)other);
        }
    }
}
