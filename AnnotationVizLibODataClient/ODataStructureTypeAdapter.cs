using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
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

        public string Notes => type.Notes;

        public bool Abstract => type.Abstract;

        public uint Color => (uint)type.Color;

        /// <summary>
        /// The OData model does not expose a permitted-shapes bitmask, so this is unconstrained (0 = no restriction).
        /// </summary>
        public int AllowedShapes => 0;

        public IReadOnlyDictionary<string, string> Attributes =>
            ObjAttribute.Parse(type.Tags).ToDictionary(a => a.Name, a => a.Value);

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
