using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.OData
{
    class ODataStructureAdapter : IStructureReadOnly
    {
        private Structure structure;

        public ODataStructureAdapter(Structure s)
        {
            if (s == null)
                throw new ArgumentNullException();

            this.structure = s;
        }

        public ulong ID
        {
            get
            {
                return (ulong)structure.ID;
            }
        }

        public string Label
        {
            get
            {
                return structure.Label;
            }
        }

        public ICollection<IStructureLinkKey> Links
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

        public string TagsXML
        {
            get
            {
                return structure.Tags;
            }
        }

        public IStructureTypeReadOnly Type
        {
            get
            {
                return new ODataStructureTypeAdapter(structure.Type);
            }
        }

        public ulong TypeID
        {
            get
            {
                return (ulong)structure.TypeID;
            }
        }

        public IReadOnlyDictionary<string, string> Attributes =>
            ObjAttribute.Parse(structure.Tags).ToDictionary(a => a.Name, a => a.Value);

        public double Confidence => structure.Confidence;

        public string Notes => structure.Notes;

        public bool Equals(IStructureReadOnly other)
        {
            if (object.ReferenceEquals(other, null))
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
