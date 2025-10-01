using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.OData
{
    /// <summary>
    /// Represents a read-only adapter around an OData Structure object
    /// </summary>
    class ODataStructureAdapter : IStructureReadOnly
    {
        private readonly Structure structure;

        public ODataStructureAdapter(Structure s)
        {
            this.structure = s ?? throw new ArgumentNullException();
        }

        public ulong ID => (ulong)structure.ID;

        public string Label => structure.Label;

        public ICollection<IStructureLink> Links
        {
            get
            {
                List<StructureLink> links = structure.SourceOfLinks.ToList();
                links.AddRange(structure.TargetOfLinks);

                return links.Select(l => new ODataStructureLinkAdapter(l)).ToArray();
            }
        }

        public ulong? ParentID
        {
            get
            {
                if (structure.ParentID.HasValue)
                    return (ulong)structure.ParentID;
                else
                    return new ulong?();
            }
        }

        public string TagsXML => structure.Tags;

        public IStructureTypeReadOnly Type => new ODataStructureTypeAdapter(structure.Type);

        public ulong TypeID => (ulong)structure.TypeID;

        public bool Equals(IStructureReadOnly other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(Structure other)
        {
            return this.Equals((IStructureReadOnly)other);
        }
    }
}
