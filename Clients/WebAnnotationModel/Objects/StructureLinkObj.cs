using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using WebAnnotationModel.Objects;
using AnnotationService.Types;

namespace WebAnnotationModel
{
    public struct StructureLinkKey : IEquatable<StructureLinkKey>, IComparable<StructureLinkKey>
    {
        readonly long _SourceID;
        readonly long _TargetID;
        readonly bool _Bidirectional;

        //public long SourceID { get { return link != null ? link.SourceID : _SourceID;  } }
        //public long TargetID { get { return link != null ? link.TargetID : _TargetID; } }

        public long SourceID { get { return _SourceID;  } }
        public long TargetID { get { return _TargetID; } }

        public bool Bidirectional { get { return _Bidirectional; } }

        public StructureLinkKey(StructureLink obj)
        {
            _SourceID = obj.SourceID;
            _TargetID = obj.TargetID;
            _Bidirectional = obj.Bidirectional;
            
        }

        public StructureLinkKey(StructureLinkObj obj)
        {
            _SourceID = obj.SourceID;
            _TargetID = obj.TargetID;
            _Bidirectional = obj.Bidirectional;
        }

        public StructureLinkKey(long SourceID, long TargetID, bool Bidirectional)
        {
            _SourceID = SourceID;
            _TargetID = TargetID;
            _Bidirectional = Bidirectional;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if(!this.GetType().IsInstanceOfType(obj))
                return false; 

            StructureLinkKey other = (StructureLinkKey)obj;

            return this.SourceID == other.SourceID && this.TargetID == other.TargetID && this.Bidirectional == other.Bidirectional;
        }

        public override int GetHashCode()
        {
            return (int)(SourceID % int.MaxValue); 
        }

        public bool Equals(StructureLinkKey other)
        {
            if ((object)other == null)
                return false;

            return this.SourceID == other.SourceID && this.TargetID == other.TargetID && this.Bidirectional == other.Bidirectional;
        }

        public int CompareTo(StructureLinkKey other)
        {
            if ((object)other == null)
                return -1;

            int SourceCompare = this.SourceID.CompareTo(other.SourceID);
            if (SourceCompare != 0)
                return SourceCompare;

            int TargetCompare = this.TargetID.CompareTo(other.TargetID);
            if (TargetCompare != 0)
                return TargetCompare;

            return this.Bidirectional.CompareTo(other.Bidirectional);
        }

        public static bool operator ==(StructureLinkKey A, StructureLinkKey B)
        {
            return (A.SourceID == B.SourceID) && (A.TargetID == B.TargetID) && (A.Bidirectional == B.Bidirectional);
        }

        public static bool operator !=(StructureLinkKey A, StructureLinkKey B)
        {
            return !((A.SourceID == B.SourceID) && (A.TargetID == B.TargetID) && (A.Bidirectional == B.Bidirectional));
        }
    }

    public class StructureLinkObj : WCFObjBaseWithKey<StructureLinkKey, StructureLink>
    {
        public override StructureLinkKey ID
        {
            get { return new StructureLinkKey(this);  }  
        }

        protected override int GenerateHashCode()
        {
            return (int)(SourceID % int.MaxValue); 
        }

        public long SourceID
        {
            get
            {
                return Data.SourceID;
            }
        }

        public long TargetID
        {
            get
            {
                return Data.TargetID;
            }   
        }
                
        public bool Bidirectional
        {
            get { return Data.Bidirectional; }
        }

        public override string ToString()
        {
            if (Bidirectional)
                return string.Format("{0} <-> {1}", SourceID, TargetID);
            else
                return string.Format("{0} -> {1}", SourceID, TargetID);
        }

        public StructureLinkObj(long sourceID, long targetID, 
                                bool Bidirectional) : base()
        {
            this.Data = new StructureLink(); 
            this.Data.SourceID = sourceID;
            this.Data.TargetID = targetID;
            this.Data.Bidirectional = Bidirectional; 
            this.DBAction = DBACTION.INSERT; 
        }

        public StructureLinkObj(StructureLink data)
        {
            this.Data = data; 
        }

        public StructureLinkObj()
        {
        }
    }
}
