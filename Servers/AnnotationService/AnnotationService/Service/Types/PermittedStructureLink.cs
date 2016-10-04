using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;

using ConnectomeDataModel;

namespace Annotation
{

    [DataContract]
    public class PermittedStructureLink : DataObject
    {
        public override string ToString()
        {
            string result = _SourceTypeID.ToString();
            result += _Bidirectional ? " <-> " : " -> ";
            result += _TargetTypeID.ToString();
            return result;
        }

        long _SourceTypeID;
        long _TargetTypeID;
        bool _Bidirectional;

        [DataMember]
        public long SourceTypeID
        {
            get { return _SourceTypeID; }
            set { _SourceTypeID = value; }
        }

        [DataMember]
        public long TargetTypeID
        {
            get { return _TargetTypeID; }
            set { _TargetTypeID = value; }
        }

        [DataMember]
        public bool Bidirectional
        {
            get { return _Bidirectional; }
            set { _Bidirectional = value; }
        }

        public PermittedStructureLink()
        {
        }

        public PermittedStructureLink(ConnectomeDataModel.PermittedStructureLink obj)
        {
            ConnectomeDataModel.PermittedStructureLink db = obj;

            this.SourceTypeID = db.SourceTypeID;
            this.TargetTypeID = db.TargetTypeID;
            this.Bidirectional = db.Bidirectional;
        }

        public void Sync(ConnectomeDataModel.PermittedStructureLink db)
        {
            db.SourceTypeID = this.SourceTypeID;
            db.TargetTypeID = this.TargetTypeID;
            db.Bidirectional = this.Bidirectional;
        }
    }
}
