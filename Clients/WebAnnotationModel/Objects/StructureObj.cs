using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using WebAnnotationModel.Objects; 

using WebAnnotationModel.Service;

namespace WebAnnotationModel
{

    public class StructureObj : WCFObjBaseWithParent<long, Structure, StructureObj>
    {
        public override long ID
        {
            get { return Data.ID; } 
        }

        public override long? ParentID
        {
            get { return Data.ParentID; }
            set
            {
                if (Data.ParentID != value)
                {
                    OnPropertyChanging("ParentID");

                    Data.ParentID = value;

                    SetDBActionForChange();
                    OnPropertyChanged("ParentID");
                }
            }
        }

        public long TypeID
        {
            get { return Data.TypeID; }
        }

        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.  As a result this is called for each object only once.
        /// </summary>
        /// <returns></returns>
        protected override int GenerateHashCode()
        {
            return (int)(ID % int.MaxValue);
        }

        public string Notes
        {
            get { return Data.Notes; }
            set
            {
                if (Data.Notes != value)
                {
                    OnPropertyChanging("Notes");

                    Data.Notes = value;

                    SetDBActionForChange();
                    OnPropertyChanged("Notes");
                }
            }
        }

        public bool Verified
        {
            get { return Data.Verified; }
            set
            {
                if (Data.Verified != value)
                {
                    OnPropertyChanging("Verified");
                    Data.Verified = value;
                    SetDBActionForChange();
                    OnPropertyChanged("Verified");
                }
            }
        }

        public double Confidence
        {
            get { return Data.Confidence; }
            set
            {
                if (Confidence != value)
                {
                    OnPropertyChanging("Confidence");
                    Data.Confidence = value;
                    SetDBActionForChange();
                    OnPropertyChanged("Confidence");
                }
            }
        }

        public IEnumerable<ObjAttribute> Attributes
        {
            get { return ObjAttribute.Parse(Data.AttributesXml); }
            set
            { 
                if (Data.AttributesXml == null && value == null)
                    return;

                string xmlstring = ObjAttribute.ToXml(value);

                if (Data.AttributesXml != xmlstring)
                {
                    OnPropertyChanging("Attributes");

                    Data.AttributesXml = xmlstring;

                    //Refresh the tags
                    SetDBActionForChange();
                    OnPropertyChanged("Attributes");
                }
            }
        }

        /// <summary>
        /// Add the specified name to the attributes if it does not exists, removes it 
        /// </summary>
        /// <param name="tag"></param>
        public void ToggleAttribute(string tag)
        {
            ObjAttribute attrib = new ObjAttribute(tag, null);
            List<ObjAttribute> listAttributes = this.Attributes.ToList();
            if (listAttributes.Contains(attrib))
            {
                listAttributes.Remove(attrib);
            }
            else
            {
                listAttributes.Add(attrib);
            }

            this.Attributes = listAttributes;
        }

        public string Label
        {
            get { return Data.Label; }
            set
            {
                if (Label != value)
                {
                    OnPropertyChanging("Label");
                    Data.Label = value;
                    //Refresh the tags
                    SetDBActionForChange();
                    OnPropertyChanged("Label");
                }
            }
        }

        public string Username
        {
            get { return Data.Username; }
        }

        private object LinksLock = new object();
        private ObservableCollection<StructureLinkObj> _Links = null;
        internal ObservableCollection<StructureLinkObj> Links
        {
            get
            {
                lock (LinksLock)
                { 
                    if (_Links == null)
                    { 
                        if (Data.Links != null)
                        {
                            StructureLinkKey[] keys = Data.Links.Select(l => new StructureLinkKey(l)).ToArray();

                            List<StructureLinkObj> linkArray = new List<StructureLinkObj>(Data.Links.Length);
                            //Initialize from the Data object
                            foreach (StructureLink link in Data.Links)
                            {
                                Debug.Assert(link != null);
                                bool added;
                                //Add it if it doesn't exist, otherwise get the official version
                                StructureLinkObj linkObj = Store.StructureLinks.GetOrAdd(new StructureLinkKey(link),
                                                                                         new Func<StructureLinkKey, StructureLinkObj>( key => { return new StructureLinkObj(link); }),
                                                                                         out added); //This call will fire events that add the link to this.Links if it is new to the local store
                                Debug.Assert(linkObj != null, "If structureObj has the value the store should have the value.   Does it link to itself?");
                                linkArray.Add(linkObj);
                            }

                            _Links = new ObservableCollection<StructureLinkObj>(linkArray); 
                        }
                        else
                        {
                            _Links = new ObservableCollection<StructureLinkObj>(); 
                        }
                         
                        _Links.CollectionChanged += this.OnLinksChanged;
                    }

                    return _Links;
                }
            }
        }

        public int NumLinks
        {
            get
            {
                lock (LinksLock)
                {
                    return Data.Links == null ? 0 : Data.Links.Length;
                }
            }
        }

        /// <summary>
        /// This provides a copy of the links collection so there is no danger of another thread changing the collection while it is enumerated.
        /// </summary>
        public StructureLinkObj[] LinksCopy
        {
            get
            {
                lock (LinksLock)
                {
                    if(NumLinks == 0)
                        return new StructureLinkObj[0];

                    StructureLinkObj[] copy = new StructureLinkObj[Links.Count];
                    
                    Links.CopyTo(copy, 0);
                    return copy; 
                }
            }
        }
        
        private void OnLinksChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            lock (LinksLock)
            {
                //Update the underlying object we will send to the server]
                Data.Links = _Links.Select(l => l.GetData()).ToArray();
            }

            SetDBActionForChange();
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created.
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal void AddLink(StructureLinkObj ID)
        {
            lock (LinksLock)
            {
                if (Links.Contains(ID))
                    return;
                
                Links.Add(ID);
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal void RemoveLink(StructureLinkObj link)
        {
            RemoveLink(link.ID); 
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal void RemoveLink(StructureLinkKey key)
        {
            lock (LinksLock)
            {
                StructureLinkObj LinkToRemove = Links.FirstOrDefault(link => link.SourceID == key.SourceID && link.TargetID == key.TargetID);
                if (LinkToRemove == null)
                    return;
                
                Links.Remove(LinkToRemove);
            }
        }

        /*
        public StructureLink[] Links
        {
            get { return Data.Links; }
        }

        /// <summary>
        /// Adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public void AddLink(StructureLink link)
        {
            List<StructureLink> listLinks = Data.Links.ToList<StructureLink>();
            listLinks.Add(link);
            Data.Links = listLinks.ToArray();
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public void RemoveLink(StructureLink link)
        {
            List<StructureLink> listLinks = Data.Links.ToList<StructureLink>();
            for(int i = 0; i < listLinks.Count; i++)
            {
                StructureLink item = listLinks[i]; 
                if(item.SourceID == link.SourceID && item.TargetID == link.TargetID)
                {
                    listLinks.RemoveAt(i);
                    i--; 
                }
            }
            Data.Links = listLinks.ToArray();
        }*/

        public StructureObj()
        {

        }

        public StructureObj(StructureTypeObj type)
        {
            this.Data = new Structure();
            InitNewData(type);
        }

        public StructureObj(Structure data)
        {
            this.Data = data;
            bool added;

            if (data.Links != null)
            {
                foreach (StructureLink link in data.Links)
                {
                    Store.StructureLinks.GetOrAdd(new StructureLinkKey(link),
                                                  new Func<StructureLinkKey, StructureLinkObj>(l => { return new StructureLinkObj(link); }),
                                                  out added);
                }
            }
        }

        

        protected void InitNewData(StructureTypeObj type)
        {
            this.Data.DBAction = DBACTION.INSERT;
            
            this.Data.ID = Store.Structures.GetTempKey();
            this.Data.TypeID = type.ID;
            Debug.Assert(type.ID >= 0);
            this.Data.Notes = ""; 
            this.Data.Confidence = 0.5;
            this.Data.ParentID = new long?();
            this.Data.Links = null; 
        }

        private StructureTypeObj _Type = null;
        public StructureTypeObj Type
        {
            get
            {
                if (_Type == null)
                {
                    _Type = Store.StructureTypes.GetObjectByID(Data.TypeID);
                }
                return _Type; 
            }
            set
            {
                Debug.Assert(value != null);
                if (value.ID == Data.TypeID)
                    return; 

                if (value != null)
                {
                    OnPropertyChanging("Type");
                    Data.TypeID = value.ID;
                    _Type = value;

                    SetDBActionForChange();

                    OnPropertyChanged("Type");
                }
            }
        }

        protected override StructureObj OnMissingParent()
        {
            return Store.Structures.GetObjectByID(ParentID.Value, true);
        }
        
        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            if (OnCreate != null)
            {
                //TODO, create notification
                //Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
                OnCreate(this, null); 
            }
        }
        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }

        
    }
}
