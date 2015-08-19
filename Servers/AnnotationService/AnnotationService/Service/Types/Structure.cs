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
    public class CreateStructureRetval
    {
        private Structure _structure;
        private Location _location;

        [DataMember]
        public Structure structure { get { return _structure; } set { _structure = value; } }

        [DataMember]
        public Location location { get { return _location; } set { _location = value; }  }

        public CreateStructureRetval(Structure s, Location l)
        {
            _structure = s;
            _location = l;
        }

        public CreateStructureRetval()
        {
        }
    }

    [DataContract]
    public class Structure : DataObjectWithParent<long>
    {
        protected long _Type;
        protected string _Notes;
        protected bool _Verified; 
        protected double _Confidence;
        protected StructureLink[] _Links;
        protected long[] _ChildIDs;
        protected string _Label;
        protected string _Username;
        protected string _Xml;
        
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

        private static StructureLink[] PopulateLinks(ConnectomeDataModel.Structure structObj)
        {
            if (!(db.IsSourceOf.Any() || db.IsTargetOf.Any()))
                return null;

            StructureLink[] _Links = new StructureLink[db.IsSourceOf.Count + db.IsTargetOf.Count];

            int i = 0;
            foreach (ConnectomeDataModel.StructureLink link in structObj.SourceOfLinks)
            {
                _Links[i] = new StructureLink(link);
                i++;
            }

            foreach (ConnectomeDataModel.StructureLink link in structObj.TargetOfLinks)
            {
                _Links[i] = new StructureLink(link);
                i++;
            }

            return _Links;
        }

        public Structure()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="IncludeChildren">Include children is set to false to save space when children aren't needed</param>
        public Structure(ConnectomeDataModel.Structure obj, bool IncludeChildren)
        {
            ConnectomeDataModel.Structure dbStructObj = obj as ConnectomeDataModel.Structure;

            this.ID = dbStructObj.ID;
            this.TypeID = dbStructObj.TypeID;
            this.Notes = dbStructObj.Notes;
            this.Verified = dbStructObj.Verified;


            if (dbStructObj.Tags == null)
            {
                //_Tags = new string[0];
                _Xml = ""; 
            }
            else
            {
            //    _Tags = dbStructObj.Tags.Split(';');
                _Xml = dbStructObj.Tags;
            }
                

            this.Confidence = dbStructObj.Confidence;
            this.ParentID = dbStructObj.ParentID;

            this._Links = PopulateLinks(dbStructObj);

            if (IncludeChildren)
            {
                List<long> childIDs = new List<long>(dbStructObj.Children.Count);
                foreach (ConnectomeDataModel.Structure Child in dbStructObj.Children)
                {
                    childIDs.Add(Child.ID);
                }
                this._ChildIDs = childIDs.ToArray();
            }
            else
            {
                this._ChildIDs = null; 
            }
             
            this._Label = dbStructObj.Label;
            this._Username = dbStructObj.Username; 
        }

        public void Sync(ConnectomeDataModel.Structure dbStructObj)
        {
            dbStructObj.TypeID = this.TypeID;
            dbStructObj.Notes = this.Notes;
            dbStructObj.Verified = this.Verified;        
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
            dbStructObj.Tags = this.AttributesXml;
            dbStructObj.Confidence = this.Confidence;
            dbStructObj.ParentID = this.ParentID;
            dbStructObj.Label = this.Label;
            dbStructObj.Username = ServiceModelUtil.GetUserForCall();
        }
    }

    
    [DataContract]
    public class StructureHistory : Structure
    {

        public StructureHistory(SelectStructureChangeLog_Result dbStructObj)
        {

            this.ID = dbStructObj.ID.Value;
            this.TypeID = dbStructObj.TypeID.Value;
            this.Notes = dbStructObj.Notes;
            this.Verified = dbStructObj.Verified.Value;

            /*
            if (dbStructObj.Tags == null)
            { 
                _Xml = "";
            }
            else
            { 
                _Xml = dbStructObj.Tags.ToString();
            }
            */

            this.Confidence = dbStructObj.Confidence.Value;
            this.ParentID = dbStructObj.ParentID.Value;
            this._Label = dbStructObj.Label;
            this._Username = dbStructObj.Username; 
        }

    }
}



