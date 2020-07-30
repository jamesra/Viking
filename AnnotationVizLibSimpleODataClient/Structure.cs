using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib.SimpleOData
{
    class Structure : IStructure, IEquatable<Structure>
    {  
        public static Structure FromDictionary(IDictionary<string, object> dict)
        {
            Structure s = new Structure { ID = System.Convert.ToUInt64(dict["ID"]) };

            if (dict.ContainsKey("ParentID"))
            {
                if (dict["ParentID"] == null)
                    s.ParentID = new ulong?();
                else
                    s.ParentID = System.Convert.ToUInt64(dict["ParentID"]);
            }

            if (dict.ContainsKey("Label"))
                s.Label = (string)dict["Label"];

            if (dict.ContainsKey("Tags"))
                s.Tags = (string)dict["Tags"];

            if (dict.ContainsKey("TypeID"))
                s.TypeID = System.Convert.ToUInt64(dict["TypeID"]);

            if (dict.ContainsKey("SourceOfLinks"))
                s.SourceOfLinks = (ICollection<StructureLink>)dict["SourceOfLinks"];

            if (dict.ContainsKey("TargetOfLinks"))
                s.TargetOfLinks = (ICollection<StructureLink>)dict["TargetOfLinks"];

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
                List<StructureLink> links = new List<StructureLink>();
                if (this.SourceOfLinks != null)
                    links.AddRange(SourceOfLinks);

                if (TargetOfLinks != null)
                    links.AddRange(TargetOfLinks);

                return links.Select(ll => ll as IStructureLink).ToList();
            }
        }

        public ulong? ParentID
        {
            get; private set;
        }

        public string TagsXML
        {
            get
            {
                return this.Tags;
            }
        }

        private string Tags { get; set; }

        IStructureType IStructure.Type
        {
            get { return this.Type; }
        }

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

        public override string ToString()
        {
            return ID.ToString();
        }

        public bool Equals(IStructure other)
        { 
            if (object.ReferenceEquals(other, null))
                return false;
            
            if (other.ID == this.ID)
                return true;
            
            return false; 
        }

        public bool Equals(Structure other)
        {
            return this.Equals((IStructure)other);
        }
    }
}
