using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebAnnotationModel.Service;
using WebAnnotationModel.Objects; 

namespace WebAnnotationModel
{
    public struct StructureLinkKey
    {
        StructureLinkObj link;

        long _SourceID;
        long _TargetID; 

        //public long SourceID { get { return link != null ? link.SourceID : _SourceID;  } }
        //public long TargetID { get { return link != null ? link.TargetID : _TargetID; } }

        public long SourceID { get { return _SourceID;  } }
        public long TargetID { get { return _TargetID; } }

        public StructureLinkKey(StructureLink obj)
        {
            _SourceID = obj.SourceID;
            _TargetID = obj.TargetID;
            link = null;
        }

        public StructureLinkKey(StructureLinkObj obj)
        {
            link = obj;
            _SourceID = link.SourceID;
            _TargetID = link.TargetID; 
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if(!this.GetType().IsInstanceOfType(obj))
                return false; 

            StructureLinkKey other = (StructureLinkKey)obj;
            
            return this == other;           
        }

        public override int GetHashCode()
        {
            return (int)(SourceID % int.MaxValue); 
        }

        public static bool operator ==(StructureLinkKey A, StructureLinkKey B)
        {
            return (A.SourceID == B.SourceID) && (A.TargetID == B.TargetID);
        }

        public static bool operator !=(StructureLinkKey A, StructureLinkKey B)
        {
            return !((A.SourceID == B.SourceID) && (A.TargetID == B.TargetID));
        }
    }

    public class StructureLinkObj : WCFObjBaseWithKey<StructureLinkKey, StructureLink>
    {
        public override StructureLinkKey ID
        {
            get { return new StructureLinkKey(this);  }
            internal set { Data.SourceID = value.SourceID;
                           Data.TargetID = value.TargetID;
            }
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
            set {
                if (Data.Bidirectional != value)
                {
                    OnPropertyChanging("Bidirectional");
                    Data.Bidirectional = value;
                    OnPropertyChanged("Bidirectional");
                    SetDBActionForChange();
                }
            }
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

        /*
        public override void Delete()
        {
            this.DBAction = DBACTION.DELETE;

            Store.Structures.SaveLinks(new StructureLinkObj[] {this});
        }
        */
    }
}
