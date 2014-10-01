using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;

using Annotation.Database;

namespace Annotation
{
    
    [DataContract]
    public class StructureLink : DataObject
    {
        public override string ToString()
        {
            return _SourceID.ToString() + " -> " + _TargetID.ToString(); 
        }

        long _SourceID;
        long _TargetID;
        bool _Bidirectional;
        string _Tags;
        string _Username;

        [DataMember]
        public long SourceID
        {
         get{return _SourceID;}
         set{_SourceID = value;}
        }

        [DataMember]
        public long TargetID
        {
            get { return _TargetID; }
            set { _TargetID = value; }
        }

        [DataMember]
        public bool Bidirectional
        {
            get { return _Bidirectional; }
            set { _Bidirectional = value; }
        }

        [DataMember]
        public string Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }

        [DataMember]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        public StructureLink()
        {
        }

        public StructureLink(DBStructureLink obj)
        {
            DBStructureLink db = obj;

            this.SourceID = db.SourceID;
            this.TargetID = db.TargetID;
            this.Bidirectional = db.Bidirectional;
            this.Tags = db.Tags;
            this.Username = db.Username; 
        }

        public void Sync(DBStructureLink db)
        {
            db.SourceID = this.SourceID;
            db.TargetID = this.TargetID;
            db.Bidirectional = this.Bidirectional;
            db.Tags = this.Tags;
            db.Username = ServiceModelUtil.GetUserForCall();
        }
    }
     
}
