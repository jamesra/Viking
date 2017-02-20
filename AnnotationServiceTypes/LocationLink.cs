using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;  
using ProtoBuf;

namespace Annotation
{
    
    [ProtoContract]
    [DataContract]
    public class LocationLink : DataObject
    {
        long _SourceID;
        long _TargetID;
        string _Username; 

        [ProtoMember(1)]
        [DataMember]
        public long SourceID
        {
            get { return _SourceID; }
            set { _SourceID = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        public long TargetID
        {
            get { return _TargetID; }
            set { _TargetID = value; }
        }

        /*
        [DataMember]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }
        */

    }
     
}
