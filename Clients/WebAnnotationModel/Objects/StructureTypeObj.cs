using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces; 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel.Objects;


namespace WebAnnotationModel.Objects
{
    public class StructureTypeObj : AnnotationModelObjBaseWithParent<long, IStructureType, StructureTypeObj>, IEquatable<StructureTypeObj>, IStructureTypeReadOnly
    {
        private readonly long _ID;
        public override long ID => _ID;

        private long? _ParentID;
        public override long? ParentID
        {
            get => _ParentID; 
            set
            {
                if (_ParentID != value)
                {
                    OnPropertyChanging(nameof(ParentID));

                    _ParentID = value;
                    Parent = null;

                    SetDBActionForChange();
                    OnPropertyChanged(nameof(ParentID));
                }
            }
        } 

        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.
        /// </summary>
        /// <returns></returns>
        protected override int GenerateHashCode()
        {
            return (int)(ID % int.MaxValue);
        }

        public override string ToString()
        {
            return this.Name;
        }

        private string _Name;
        public string Name
        {
            get => _Name;
            set
            {
                OnPropertyChanging(nameof(Name));
                _Name = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _Notes;
        public string Notes
        {
            get => _Notes;
            set
            {
                OnPropertyChanging(nameof(Notes));
                _Notes = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(Notes));
            }
        }

        private uint _Color = 0;
        public uint Color
        {
            get => (uint)_Color;
            set
            {
                if (_Color == value)
                    return;

                OnPropertyChanging(nameof(Color));
                _Color = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(Color));
            }
        }

        private string _Code;
        public string Code
        {
            get => _Code;
            set
            {
                OnPropertyChanging(nameof(Code));
                _Code = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(Code));
            }
        }

        private bool _Abstract;
        public bool Abstract
        {
            get => _Abstract;
            set
            {
                OnPropertyChanging(nameof(Abstract));
                _Abstract = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(Abstract));
            }
        }

        private int _AllowedShapes;
        public int AllowedShapes
        {
            get => AllowedShapes;
            set
            {
                OnPropertyChanging(nameof(AllowedShapes));
                _AllowedShapes = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(AllowedShapes));
            }
        }

        private readonly ConcurrentObservableAttributeSet _Attributes = new ConcurrentObservableAttributeSet();
        public ReadOnlyObservableCollection<ObjAttribute> Attributes => _Attributes.ReadOnlyObservable;
        public Task<ObjAttribute[]> CopyAttributesAsync()
        {
            return _Attributes.CreateCopyAsync();
        }

        public Task SetAttributes(IEnumerable<ObjAttribute> attribs)
        {
            return _Attributes.SetAttributes(attribs);
        }

        public StructureTypeObj(long id)
        {
            //this.MarkupType = "Point";
            this._ID = id;
        }

        public StructureTypeObj()
        {
            this.DBAction = DBACTION.INSERT;
            this.Name = "New Structure Type";
            //this.MarkupType = "Point";
            //this._ID = Store.StructureTypes.NextKey(); 
            this.Code = "NoCode";
        }

        /*
        public StructureTypeObj(IStructureType newdata)
        {
            newdata.Code = newdata.Code.Trim();

            if (newdata.PermittedLinks != null)
            {
                foreach (var link in newdata.PermittedLinks)
                {
                    Store.PermittedStructureLinks.GetOrAdd(new PermittedStructureLinkKey(link),
                                                  new Func<PermittedStructureLinkKey, PermittedStructureLinkObj>(l => { return new PermittedStructureLinkObj(link); }),
                                                  out bool added);
                }
            }
        }
        */

        public StructureTypeObj(StructureTypeObj parent) : this()
        {  
            if (parent != null)
            {
                this.ParentID = parent.ID;
            }
        }
         

        private ConcurrentObservablePermittedStructureLinkSet _PermittedLinks;
        private SemaphoreSlim LinkLock = new SemaphoreSlim(1);

        public ReadOnlyObservableCollection<PermittedStructureLinkObj> PermittedLinks =>
            _PermittedLinks.ReadOnlyObservable;
        /*
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
        */

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

        ulong IStructureTypeReadOnly.ID => (ulong)ID;

        ulong? IStructureTypeReadOnly.ParentID => (ulong?)ParentID ?? new ulong?();

        IReadOnlyDictionary<string, string> IStructureTypeReadOnly.Attributes =>
            Attributes.ToDictionary(o => o.Name, o => o.Value);

        bool IStructureTypeReadOnly.Abstract => Abstract;

        int IStructureTypeReadOnly.AllowedShapes => AllowedShapes;

        /*
        private void OnPermittedLinksChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            lock (LinksLock)
            {
                //UpdateFromServer the underlying object we will send to the server]
                Data.PermittedLinks = _PermittedLinks.Select(l => l.GetData()).ToArray();
            }

            SetDBActionForChange();

            OnPropertyChanged("PermittedLinkSourceTypes");
            OnPropertyChanged("PermittedLinkTargetTypes");
            OnPropertyChanged("PermittedLinkBidirectionalTypes");
        }
        */

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created.
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal async Task<bool> TryAddPermittedLink(PermittedStructureLinkObj ID)
        {
            try
            {
                await LinkLock.WaitAsync();
                if (PermittedLinks.Contains(ID))
                    return false;

                await _PermittedLinks.AddAsync(ID);
                return false;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal Task<bool> TryRemovePermittedLink(PermittedStructureLinkObj link)
        {
            return TryRemovePermittedLink(link.ID);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        internal async Task<bool> TryRemovePermittedLink(PermittedStructureLinkKey key)
        {
            try
            {
                await LinkLock.WaitAsync();
                PermittedStructureLinkObj LinkToRemove = PermittedLinks.FirstOrDefault(link =>
                    link.SourceTypeID == key.SourceTypeID && link.TargetTypeID == key.TargetTypeID);
                if (LinkToRemove == null)
                    return false;

                await _PermittedLinks.RemoveAsync(LinkToRemove);
                return true;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        public bool Equals(IStructureTypeReadOnly other)
        {
            if (other is null)
                return false;

            return other.ID == (ulong)this.ID;
        }

        public bool Equals(StructureTypeObj other)
        {
            if (other is StructureTypeObj o)
            {
                return o.ID.Equals(ID);
            }

            return false;
        }
    }
}
