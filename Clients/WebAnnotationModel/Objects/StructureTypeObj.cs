using Annotation.Interfaces;
using AnnotationService.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public class StructureTypeObj : WCFObjBaseWithParent<long, StructureType, StructureTypeObj>, IStructureType
    {
        public override long ID
        {
            get { return Data.ID; }
        }

        ulong IStructureType.ID => (ulong)this.ID;
        ulong? IStructureType.ParentID => this.ParentID.HasValue ? new ulong?((ulong)ParentID.Value) : new ulong?();

        string[] IStructureType.Tags => this.Data.Tags;

        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.
        /// </summary>
        /// <returns></returns>
        protected override int GenerateHashCode()
        {
            return (int)(ID % int.MaxValue);
        }

        public override long? ParentID
        {
            get { return Data.ParentID; }
            set { Data.ParentID = value; }
        }

        public override string ToString()
        {
            return this.Name;
        }

        public string Name
        {
            get { return Data.Name; }
            set
            {
                OnPropertyChanging("Name");
                Data.Name = value;
                SetDBActionForChange();
                OnPropertyChanged("Name");
            }
        }

        public string Notes
        {
            get { return Data.Notes; }
            set
            {
                OnPropertyChanging("Notes");
                Data.Notes = value;
                SetDBActionForChange();
                OnPropertyChanged("Notes");
            }
        }

        public uint Color
        {
            get { return (uint)Data.Color; }
            set
            {
                if (Data.Color == value)
                    return;
                OnPropertyChanging("Color");
                Data.Color = (int)value;
                SetDBActionForChange();
                OnPropertyChanged("Color");
            }
        }

        public string Code
        {
            get { return Data.Code; }
            set
            {
                OnPropertyChanging("Code");
                Data.Code = value;
                SetDBActionForChange();
                OnPropertyChanged("Code");
            }
        }

        public StructureTypeObj()
        {
            if (this.Data == null)
                this.Data = new StructureType();

            this.Data.DBAction = DBACTION.INSERT;
            this.Data.Name = "New Structure Type";
            this.Data.MarkupType = "Point";
            this.Data.ID = Store.StructureTypes.GetTempKey();
            this.Data.Tags = new String[0];
            this.Data.StructureTags = new String[0];
            this.Data.Code = "NoCode";
        }

        public StructureTypeObj(StructureType data)
        {
            this.Data = data;
            this.Data.Code = this.Data.Code.Trim();

            if (data.PermittedLinks != null)
            {
                foreach (PermittedStructureLink link in data.PermittedLinks)
                {
                    Store.PermittedStructureLinks.GetOrAdd(new PermittedStructureLinkKey(link),
                                                  new Func<PermittedStructureLinkKey, PermittedStructureLinkObj>(l => { return new PermittedStructureLinkObj(link); }),
                                                  out bool added);
                }
            }
        }

        public StructureTypeObj(StructureTypeObj parent) : this()
        {
            if (this.Data == null)
                this.Data = new StructureType();

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }
        }

        protected override StructureTypeObj OnMissingParent()
        {
            return Store.StructureTypes.GetObjectByID(this.ParentID.Value, true);
        }

        private object LinksLock = new object();
        private ObservableCollection<PermittedStructureLinkObj> _PermittedLinks = null;
        public ObservableCollection<PermittedStructureLinkObj> PermittedLinks
        {
            get
            {
                lock (LinksLock)
                {
                    if (_PermittedLinks == null)
                    {
                        if (Data.PermittedLinks != null)
                        {
                            PermittedStructureLinkKey[] keys = Data.PermittedLinks.Select(l => new PermittedStructureLinkKey(l)).ToArray();

                            List<PermittedStructureLinkObj> linkArray = new List<PermittedStructureLinkObj>(Data.PermittedLinks.Length);
                            //Initialize from the Data object
                            foreach (var link in Data.PermittedLinks)
                            {
                                Debug.Assert(link != null);
                                bool added;
                                //Add it if it doesn't exist, otherwise get the official version
                                PermittedStructureLinkObj linkObj = Store.PermittedStructureLinks.GetOrAdd(new PermittedStructureLinkKey(link),
                                                                                         new Func<PermittedStructureLinkKey, PermittedStructureLinkObj>(key => { return new PermittedStructureLinkObj(link); }),
                                                                                         out added); //This call will fire events that add the link to this.Links if it is new to the local store
                                Debug.Assert(linkObj != null, "If structureObj has the value the store should have the value.   Does it link to itself?");
                                linkArray.Add(linkObj);
                            }

                            _PermittedLinks = new ObservableCollection<PermittedStructureLinkObj>(linkArray);
                        }
                        else
                        {
                            _PermittedLinks = new ObservableCollection<PermittedStructureLinkObj>();
                        }

                        _PermittedLinks.CollectionChanged += this.OnPermittedLinksChanged;
                    }

                    return _PermittedLinks;
                }
            }
        }

        public long[] PermittedLinkSourceTypes
        {
            get
            {
                return PermittedLinks.Where(pl => pl.TargetTypeID == this.ID && pl.Bidirectional == false).Select(pl => pl.SourceTypeID).ToArray();
            }
        }

        public long[] PermittedLinkTargetTypes
        {
            get
            {
                return PermittedLinks.Where(pl => pl.SourceTypeID == this.ID && pl.Bidirectional == false).Select(pl => pl.TargetTypeID).ToArray();
            }
        }

        public long[] PermittedLinkBidirectionalTypes
        {
            get
            {
                return PermittedLinks.Where(pl => (pl.SourceTypeID == this.ID || pl.TargetTypeID == this.ID) && pl.Bidirectional == true).Select(pl => pl.SourceTypeID == this.ID ? pl.TargetTypeID : pl.SourceTypeID).ToArray();
            }
        }


        private void OnPermittedLinksChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            lock (LinksLock)
            {
                //Update the underlying object we will send to the server]
                Data.PermittedLinks = _PermittedLinks.Select(l => l.GetData()).ToArray();
            }

            SetDBActionForChange();

            OnPropertyChanged("PermittedLinkSourceTypes");
            OnPropertyChanged("PermittedLinkTargetTypes");
            OnPropertyChanged("PermittedLinkBidirectionalTypes");
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created.
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal bool TryAddPermittedLink(PermittedStructureLinkObj ID)
        {
            lock (LinksLock)
            {
                if (PermittedLinks.Contains(ID))
                    return false;

                PermittedLinks.Add(ID);
                return false;
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal bool TryRemovePermittedLink(PermittedStructureLinkObj link)
        {
            return TryRemovePermittedLink(link.ID);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal bool TryRemovePermittedLink(PermittedStructureLinkKey key)
        {
            lock (LinksLock)
            {
                PermittedStructureLinkObj LinkToRemove = PermittedLinks.FirstOrDefault(link => link.SourceTypeID == key.SourceTypeID && link.TargetTypeID == key.TargetTypeID);
                if (LinkToRemove == null)
                    return false;

                PermittedLinks.Remove(LinkToRemove);
                return true;
            }
        }

        public bool Equals(IStructureType other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return other.ID == (ulong)this.ID;
        }

        /*
        public override void Delete()
        {
            StructureTypeObj OriginalParent = this.Parent;
            this.Parent = null;

            DBACTION originalAction = this.DBAction;
            this.DBAction = DBACTION.DELETE;

            bool success = Store.StructureTypes.Save();
            if (!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
                this.Parent = OriginalParent;
            }
            
            Viking.UI.State.SelectedObject = null;             
        }
         */

    }
}
