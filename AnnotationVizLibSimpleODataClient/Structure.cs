using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.SimpleOData
{
    class Structure : IStructureReadOnly, IEquatable<Structure>
    {
        public static Structure FromDictionary(IDictionary<string, object> dict)
        {
            Structure s = new() { ID = System.Convert.ToUInt64(dict["ID"]) };

            if (dict.ContainsKey("ParentID"))
            {
                s.ParentID = dict["ParentID"] is null ? new ulong?() : System.Convert.ToUInt64(dict["ParentID"]);
            }


            if (dict.TryGetValue("Label", out var label))
                s.Label = (string)label;

            if (dict.TryGetValue("Tags", out var tags))
                s.Tags = (string)tags;

            if (dict.TryGetValue("TypeID", out var typeid))
                s.TypeID = System.Convert.ToUInt64(typeid);

            if (dict.TryGetValue("SourceOfLinks", out var sourceoflinks))
                s.SourceOfLinks = (ICollection<StructureLink>)sourceoflinks;

            if (dict.TryGetValue("TargetOfLinks", out var targetoflinks))
                s.TargetOfLinks = (ICollection<StructureLink>)targetoflinks;

            return s;
        }


        public Structure()
        {
        }

        public ulong ID
        {
            get; private set;
        }

        public string Label
        {
            get; private set;
        }

        public ICollection<StructureLink> SourceOfLinks
        {
            get; internal set;
        }

        public ICollection<StructureLink> TargetOfLinks
        {
            get; internal set;
        }

        public ICollection<IStructureLink> Links
        {
            get
            {
                List<StructureLink> links = [];
                if (this.SourceOfLinks != null)
                    links.AddRange(SourceOfLinks);

                if (TargetOfLinks != null)
                    links.AddRange(TargetOfLinks);

                return [.. links.Select(ll => ll as IStructureLink)];
            }
        }

        public ulong? ParentID
        {
            get; private set;
        }

        public string TagsXML => this.Tags;

        private string Tags { get; set; }

        IStructureTypeReadOnly IStructureReadOnly.Type => this.Type;

        public StructureType Type
        {
            get; private set;
        }

        public ulong TypeID
        {
            get; private set;
        }

        public ICollection<Structure> Children
        {
            get; internal set;
        }

        public ICollection<Location> Locations
        {
            get; private set;
        }

        public ICollection<LocationLink> LocationLinks
        {
            get; internal set;
        }

        public override string ToString() => ID.ToString();

        public bool Equals(IStructureReadOnly other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(Structure other) => this.Equals((IStructureReadOnly)other);
    }
}
