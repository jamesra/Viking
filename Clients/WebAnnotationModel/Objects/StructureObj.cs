using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utils;
using WebAnnotationModel;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel.Objects
{
    public class StructureObj : AnnotationModelObjBaseWithParent<long, IStructure, StructureObj>, IDataObjectLinks<StructureLinkKey, StructureLinkObj>, IEquatable<StructureObj>, IStructureReadOnly
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

        public long TypeID
        {
            get;
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

        private bool _Verified;
        public bool Verified
        {
            get => _Verified;
            set
            {
                if (_Verified != value)
                {
                    OnPropertyChanging(nameof(Verified));
                    _Verified = value;
                    SetDBActionForChange();
                    OnPropertyChanged(nameof(Verified));
                }
            }
        }

        private double _Confidence;
        public double Confidence
        {
            get => _Confidence;
            set
            {
                if (_Confidence != value)
                {
                    OnPropertyChanging(nameof(Confidence));
                    _Confidence = value;
                    SetDBActionForChange();
                    OnPropertyChanged(nameof(Confidence));
                }
            }
        }

        public ConcurrentObservableAttributeSet _Attributes { get; private set; }

        public Task<ObjAttribute[]> CopyAttributesAsync()
        {
            return _Attributes.CreateCopyAsync();
        }

        public ReadOnlyObservableCollection<ObjAttribute> Attributes => _Attributes.ReadOnlyObservable;

        public Task SetAttributes(IEnumerable<ObjAttribute> attribs)
        {
            return _Attributes.SetAttributes(attribs);
        }

        /// <summary>
        /// Add the specified name to the attributes if it does not exists, removes it 
        /// </summary>
        /// <param name="tag"></param>
        public Task<bool> ToggleAttribute(string tag, string value = null)
        {
            return _Attributes.ToggleAttribute(tag, value);
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public Task AddAttributeAsync(ObjAttribute attribute)
        {
            return _Attributes.AddAsync(attribute);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public Task RemoveAttributeAsync(ObjAttribute attribute)
        {
            return _Attributes.RemoveAsync(attribute);
        }

        private string _Label;
        public string Label
        {
            get => _Label;
            set
            {
                if (_Label != value)
                {
                    OnPropertyChanging(nameof(Label));
                    _Label = value;
                    //Refresh the tags
                    SetDBActionForChange();
                    OnPropertyChanged(nameof(Label));
                }
            }
        }

        private string _Username;
        public string Username
        {
            get => _Username;
            internal set
            {
                if (_Username == value) 
                    return; 

                OnPropertyChanging(nameof(Username));
                _Username = value;
                //Refresh the tags
                SetDBActionForChange();
                OnPropertyChanged(nameof(Username));
            }
        }

        private DateTime _LastModified;
        public DateTime LastModified
        {
            get => _LastModified;
            internal set
            {
                if (value == _LastModified)
                    return;

                OnPropertyChanging(nameof(LastModified));
                _LastModified = value; 
                OnPropertyChanged(nameof(LastModified));
            }
        }

        private DateTime _Created;
        public DateTime Created
        {
            get => _Created;
            internal set
            {
                if (value == _Created)
                    return;
                
                OnPropertyChanging(nameof(Created));
                _Created = value; 
                OnPropertyChanged(nameof(Created));
            }
        }

        private readonly ConcurrentObservableStructureLinkSet _Links = new ConcurrentObservableStructureLinkSet();

        private readonly SemaphoreSlim LinkLock = new SemaphoreSlim(1); 

        internal ReadOnlyObservableCollection<StructureLinkObj> Links => _Links.ReadOnlyObservable;

        public int NumLinks => Links.Count;

        /// <summary>
        /// This provides a copy of the links collection so there is no danger of another thread changing the collection while it is enumerated.
        /// </summary>
        public Task<StructureLinkObj[]> CopyLinksAsync()
        {
            return _Links.CreateCopyAsync();
        }


        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created.
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public Task<bool> AddLinkAsync(StructureLinkObj ID)
        {
            return _Links.AddAsync(ID);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public Task<bool> RemoveLinkAsync(StructureLinkObj link)
        {
            return _Links.RemoveAsync(link.ID);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public Task<bool> RemoveLinkAsync(StructureLinkKey key)
        {
            return _Links.RemoveAsync(key);
        }

        public StructureObj()
        {
            //_ID = Store.Structures.NextKey();
        }

        internal StructureObj(long typeid)
        {
            //_ID = Store.Structures.NextKey();
            TypeID = typeid;
        }

        public StructureObj(long id, long typeid)
        {
            _ID = id;
            TypeID = typeid;
        }

        public StructureObj(long id, StructureTypeObj type) : this(id, type.ID)
        {
            _Type = type;
        }

        public StructureObj(IStructure data) : this(data.ID)
        {

            /*
            if (data.Links != null)
            {
                foreach (IStructureLink link in data.Links)
                {
                    Store.StructureLinks.GetOrAdd(new StructureLinkKey(link),
                                                  new Func<StructureLinkKey, StructureLinkObj>(l => { return new StructureLinkObj(link); }),
                                                  out bool added);
                }
            }
            */
        }

        private int Initialized = 0;

        public async Task InitializeAsync(IAnnotationStores stores, CancellationToken token)
        {
            if (Interlocked.Exchange(ref Initialized, 1) == 0)
            {
                _Type = await stores.StructureTypes.GetObjectByID(TypeID, true, false, token);

                List<Task> tasks = new List<Task>();
                if (ParentID.HasValue)
                    this.Parent = await stores.Structures.GetObjectByID(ParentID.Value, true, false, token); 
            }
        }
          
        /*
        protected static Task<StructureObj> CreateForType(IStructureType type)
        { 
            this.Data.ID = Store.Structures.GetTempKey();
            this.Data.TypeID = type.ID;
            Debug.Assert(type.ID >= 0);
            this.Data.Notes = "";
            this.Data.Confidence = 0.5;
            this.Data.ParentID = new long?();
            this.Data.Links = null;
        }
        */

        internal static Task<StructureObj> Create(IStructure newdata)
        {
            return Task.FromResult(new StructureObj(newdata.ID, newdata.TypeID)
            {
                Notes = newdata.Notes,
                Label = newdata.Label,
                Confidence = newdata.Confidence,
                ParentID = newdata.ParentID,
                _Username = newdata.Username,
                _Verified = newdata.Verified,
                _Attributes = new ConcurrentObservableAttributeSet(ObjAttributeParser.ParseAttributes(newdata.Attributes))
            });
        }

        internal override Task Update(IStructure newdata)
        {
            Notes = newdata.Notes;
            Label = newdata.Label;
            Confidence = newdata.Confidence;
            Username = newdata.Username;
            return Task.CompletedTask;
        }

        private StructureTypeObj _Type = null;
        public StructureTypeObj Type
        {
            get => _Type;
            /*
            set
            {
                Debug.Assert(value != null);
                if (value.ID == TypeID)
                    return;

                if (value != null)
                {
                    OnPropertyChanging("Type");
                    TypeID = value.ID;
                    _Type = value;

                    SetDBActionForChange();

                    OnPropertyChanged("Type");
                }
            }
            */
        }

        public string TagsXML => this.TagsXML;

        ulong IStructureReadOnly.ID => (ulong)ID;

        ulong? IStructureReadOnly.ParentID => (ulong?)ParentID ?? new ulong?();

        ulong IStructureReadOnly.TypeID => (ulong)TypeID;

        ICollection<IStructureLinkKey> IStructureReadOnly.Links => Links.Cast<IStructureLinkKey>().ToArray();

        IStructureTypeReadOnly IStructureReadOnly.Type => Type;

        IReadOnlyDictionary<string, string> IStructureReadOnly.Attributes => _Attributes.ReadOnlyObservable.ToDictionary(o => o.Name, o => o.Value);

        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            //TODO, create notification
            //Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
            OnCreate?.Invoke(this, null);
        }

        public new bool Equals(object obj)
        {
            if (obj is StructureObj other)
            {
                return Equals(other);
            }
            else if (obj is IStructureReadOnly Iother)
            {
                return Equals(Iother);
            }

            return base.Equals(obj);
        }

        public bool Equals(IStructureReadOnly other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return (long)other.ID == this.ID;
        }

        public bool Equals(StructureObj other)
        {
            if (other is null)
                return false;

            return other.ID == this.ID;
        }
    }
}
