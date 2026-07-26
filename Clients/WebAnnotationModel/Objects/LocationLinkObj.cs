using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System;
using System.Diagnostics;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{

    public class LocationLinkObj : WCFObjBaseWithKey<LocationLinkKey, LocationLink>, ILocationLink
    {
        public override LocationLinkKey ID => new(this);

        public override string ToString() => ID.ToString();

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (!this.GetType().IsInstanceOfType(obj))
                return false;

            LocationLinkObj other = (LocationLinkObj)obj;

            return (A == other.A) && (B == other.B);
        }

        protected override int GenerateHashCode() => ID.GetHashCode();

        public override int GetHashCode() => ID.GetHashCode();

        public static bool operator ==(LocationLinkObj A, LocationLinkObj B) => object.Equals(A, B);/*if (object.Equals(A, B))
                return true;
            if (object.Equals(A, null) || object.Equals(B, null))
                return false;  

            return (A.ID == B.ID);
            */

        public static bool operator !=(LocationLinkObj A, LocationLinkObj B) => !object.Equals(A, B);/*
            if (object.Equals(A, B))
                return false;
            if(object.Equals(A,null) || object.Equals(B,null))                
                return true; 

            return !((A.ID == B.ID));
            */

        public int CompareTo(LocationLinkKey other)
        {
            if (A != other.A)
                return (int)(other.A - A);
            else
                return (int)(other.B - B);
        }

        bool IEquatable<ILocationLink>.Equals(ILocationLink other) => throw new NotImplementedException();

        public long A => Data.SourceID < Data.TargetID ? Data.SourceID : Data.TargetID;

        public long B => Data.SourceID > Data.TargetID ? Data.SourceID : Data.TargetID;

        ulong ILocationLink.A => (ulong)A;

        ulong ILocationLink.B => (ulong)B;

        public LocationLinkObj()
        {
            LocationLink link = new();
            this.Data = link;
        }

        public LocationLinkObj(long IDA,
                               long IDB)
        {
            Debug.Assert(IDA != IDB);
            LocationLink link = new()
            {
                SourceID = IDA < IDB ? IDA : IDB,
                TargetID = IDA < IDB ? IDB : IDA
            };
            this.Data = link;
        }

        public LocationLinkObj(LocationLink link)
        {
            this.Data = link;
        }
    }
}
