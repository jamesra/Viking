using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.Objects
{

    public class StructureLinkObj : AnnotationModelObjBaseWithKey<StructureLinkKey, IStructureLink>, IEquatable<StructureLinkObj>, IDataObjectWithKey<StructureLinkKey>, IStructureLinkKey
    {
        public override StructureLinkKey ID => new StructureLinkKey(SourceID, TargetID, Bidirectional);

        protected override int GenerateHashCode()
        {
            return (int)(SourceID % int.MaxValue);
        }

        private long _SourceID;
        public long SourceID => _SourceID;
        
        private long _TargetID;
        public long TargetID => _TargetID;

        private bool _Bidirectional;
        public bool Bidirectional
        {
            get => _Bidirectional;
        }

        ulong IStructureLinkKey.SourceID => (ulong)SourceID;

        ulong IStructureLinkKey.TargetID => (ulong)TargetID;

        bool IStructureLinkKey.Directional => !Bidirectional;

        public override string ToString()
        {
            if (Bidirectional)
                return $"{SourceID} <-> {TargetID}";
            else
                return $"{SourceID} -> {TargetID}";
        }

        protected Task<StructureLinkObj> CreateFromServer(IStructureLink data)
        {
            return Task.FromResult(new StructureLinkObj
            {
                _SourceID = (long) data.SourceID,
                _TargetID = (long) data.TargetID,
                _Bidirectional = !data.Directional
            });
        }

        internal override Task Update(IStructureLink data)
        {
            this._SourceID = (long)data.SourceID;
            this._TargetID = (long)data.TargetID;
            this._Bidirectional = !data.Directional;
            return Task.CompletedTask;
        }

        public StructureLinkObj(long sourceID, long targetID,
                                bool Bidirectional) : base()
        {
            this._SourceID = sourceID;
            this._TargetID = targetID;
            this._Bidirectional = Bidirectional;
            this.DBAction = DBACTION.INSERT;
        }

        public StructureLinkObj(IStructureLink data)
        {
            this._SourceID = (long)data.SourceID;
            this._TargetID = (long)data.TargetID;
            this._Bidirectional = !data.Directional; 
        }

        public StructureLinkObj()
        {
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null))
                return false;

            if(obj is StructureLinkObj sl)
                return this.ID.Equals(sl.ID);
            if (obj is StructureLinkKey sk)
                return this.ID.Equals(sk);

            return base.Equals(obj);
        }

        public bool Equals(StructureLinkObj obj)
        {
            return this.ID.Equals(obj?.ID);
        }

        public bool Equals(StructureLinkKey obj)
        {
            return this.ID.Equals(obj);
        }

        bool IEquatable<IStructureLinkKey>.Equals(IStructureLinkKey other)
        {
            throw new NotImplementedException();
        }

        int IComparable<IStructureLinkKey>.CompareTo(IStructureLinkKey other)
        {
            throw new NotImplementedException();
        }
    }
}
