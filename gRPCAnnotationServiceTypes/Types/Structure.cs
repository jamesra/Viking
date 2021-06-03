using ProtoBuf;
using System;

namespace Viking.gRPC.AnnotationTypes
{

    [ProtoContract]
    /* Recoded [DataContract] */
    public partial class Structure : DataObjectWithParentOfLong
    {
        private Int64 _Type;
        private string _Notes;
        private bool _Verified;
        private double _Confidence;
        private StructureLink[] _Links;
        private Int64[] _ChildIDs;
        private string _Label;
        private string _Username;
        private string _Xml;

        /* Recoded [DataMember] */
        [ProtoMember(1)]
        public Int64 TypeID
        {
            get { return _Type; }
            set { _Type = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(2)]
        public string Notes
        {
            get { return _Notes; }
            set { _Notes = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(3)]
        public bool Verified
        {
            get { return _Verified; }
            set { _Verified = value; }
        }

        /*
        [DataMember]
        public string[] Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }
        */

        /* Recoded [DataMember] */
        [ProtoMember(4)]
        public string AttributesXml
        {
            get { return _Xml; }
            set { _Xml = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(5)]
        public double Confidence
        {
            get { return _Confidence; }
            set { _Confidence = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(6)]
        public StructureLink[] Links
        {
            get { return _Links; }
            set { _Links = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(7)]
        public Int64[] ChildIDs
        {
            get { return _ChildIDs; }
            set { _ChildIDs = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(8)]
        public string Label
        {
            get { return _Label; }
            set { _Label = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(9)] 
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }


        public Structure()
        {
        }

    }
}




