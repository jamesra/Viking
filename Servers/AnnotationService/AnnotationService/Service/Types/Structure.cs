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
    public class Structure : DataObjectWithParent<long>
    {
        private long _Type;
        private string _Notes;
        private bool _Verified;
        private string[] _Tags = new string[0];
        private double _Confidence;
        private StructureLink[] _Links;
        private long[] _ChildIDs;
        private string _Label;
        private string _Username;
        private string _Xml;
        
        [DataMember]
        public long TypeID
        {
            get { return _Type; }
            set { _Type = value; }
        }

        [DataMember]
        public string Notes
        {
            get { return _Notes; }
            set { _Notes = value; }
        }

        [DataMember]
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

        [DataMember]
        public string AttributesXml
        {
            get { return _Xml; }
            set { _Xml = value; }
        }

        [DataMember]
        public double Confidence
        {
            get { return _Confidence; }
            set { _Confidence = value; }
        }

        [DataMember]
        public StructureLink[] Links
        {
            get { return _Links; }
            set { _Links = value; }
        }

        [DataMember]
        public long[] ChildIDs
        {
            get { return _ChildIDs; }
            set { _ChildIDs = value; }
        }

        [DataMember]
        public string Label
        {
            get { return _Label; }
            set { _Label = value; }
        }

        [DataMember]
        [Column("Username")]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        public Structure()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="IncludeChildren">Include children is set to false to save space when children aren't needed</param>
        public Structure(DBStructure obj, bool IncludeChildren)
        {
            DBStructure db = obj as DBStructure;

            this.ID = db.ID;
            this.TypeID = db.TypeID;
            this.Notes = db.Notes;
            this.Verified = db.Verified;

            if (db.Tags == null)
            {
                //_Tags = new string[0];
                _Xml = "";
            }
            else
            {
            //    _Tags = db.Tags.Split(';');
                _Xml = db.Tags;
            }
                

            this.Confidence = db.Confidence;
            this.ParentID = db.ParentID;

            this._Links = new StructureLink[db.IsSourceOf.Count + db.IsTargetOf.Count];

            if (IncludeChildren)
            {
                List<long> childIDs = new List<long>(db.ChildStructures.Count);
                foreach (DBStructure Child in db.ChildStructures)
                {
                    childIDs.Add(Child.ID);
                }
                this._ChildIDs = childIDs.ToArray();
            }
            else
            {
                this._ChildIDs = new long[0]; 
            }

            int i = 0;
            foreach (DBStructureLink link in db.IsSourceOf)
            {
                _Links[i] = new StructureLink(link);
                i++;
            }

            foreach (DBStructureLink link in db.IsTargetOf)
            {
                _Links[i] = new StructureLink(link);
                i++;
            }

            this._Label = db.Label;
            this._Username = db.Username; 
        }

        public void Sync(DBStructure db)
        {
            db.TypeID = this.TypeID;
            db.Notes = this.Notes;
            db.Verified = this.Verified;        
            /*
            string tags = "";
            foreach (string s in _Tags)
            {
                if (tags.Length > 0)
                    tags = tags + ';' + s;
                else
                    tags = s; 
            }
            */
            db.Tags = this.AttributesXml;
            db.Confidence = this.Confidence;
            db.ParentID = this.ParentID;
            db.Label = this.Label;
            db.Username = ServiceModelUtil.GetUserForCall();
        }
    }
}



