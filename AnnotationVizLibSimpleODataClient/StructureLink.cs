using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Annotation.Interfaces;

namespace AnnotationVizLib.SimpleOData
{
    class StructureLink : IStructureLink, IEquatable<StructureLink>
    {
        public static StructureLink FromDictionary(IDictionary<string, object> dict)
        {
            StructureLink s = new StructureLink { SourceID = System.Convert.ToUInt64(dict["SourceID"]),
                                                  TargetID = System.Convert.ToUInt64(dict["TargetID"]),
                                                  Bidirectional = System.Convert.ToBoolean(dict["Bidirectional"])};
            
            return s;
        }

        public StructureLink()
        {
        }

        public bool Directional
        {
            get
            {
                return !Bidirectional;
            } 
            set { Bidirectional = !value; }
        }

        private bool Bidirectional {get;set;}

        public ulong SourceID
        {
            get; private set;
        }

        public ulong TargetID
        {
            get; private set;
        }

        public override string ToString()
        {
            if(Bidirectional)
                return string.Format("{0} <-> {1}", SourceID, TargetID);
            else
                return string.Format("{0}  -> {1}", SourceID, TargetID);
        }

        public bool Equals(IStructureLink other)
        { 
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.SourceID == this.SourceID && 
                other.TargetID == this.TargetID &&
                other.Directional == this.Directional)
                return true;

            return false; 
        }

        public bool Equals(StructureLink other)
        {
            return this.Equals((IStructureLink)other);
        }
    }
}
