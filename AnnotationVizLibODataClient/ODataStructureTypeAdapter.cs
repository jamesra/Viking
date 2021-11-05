using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AnnotationVizLib.OData
{
    class ODataStructureTypeAdapter : IStructureTypeReadOnly
    {
        private StructureType type;
        public ODataStructureTypeAdapter(StructureType t)
        {
            if (t == null)
                throw new ArgumentNullException();
            type = t;
        }

        public ulong ID
        {
            get
            {
                return (ulong)type.ID;
            }
        }

        public string Name
        {
            get
            {
                return type.Name;
            }
        }

        public string Code
        {
            get
            {
                return type.Code;
            }
        }

        public ulong? ParentID
        {
            get
            {
                return (ulong?)type.ParentID;
            }
        }

        public string[] Tags
        {
            get
            {
                return ObjAttribute.Parse(type.Tags).Select(a => a.ToString()).ToArray();
            }
        }

        public string Notes => type.Notes;

        public IReadOnlyDictionary<string, string> Attributes =>
            ObjAttribute.Parse(type.Tags).ToDictionary(a => a.Name, a => a.Value);

        public bool Abstract => type.Abstract;

        public uint Color => (uint)type.Color;

        public int AllowedShapes => throw new NotImplementedException();

        public bool Equals(IStructureTypeReadOnly other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(StructureType other)
        {
            return this.Equals((IStructureTypeReadOnly)other);
        }
    }
}
