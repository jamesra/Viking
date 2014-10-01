using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebAnnotationModel.Service;
using WebAnnotationModel.Objects;
using System.Diagnostics; 

namespace WebAnnotationModel
{
    public struct LocationLinkKey
    {
        public readonly long A;
        public readonly long B;

        public LocationLinkKey(long a, long b)
        {
            Debug.Assert(a != b); 
            A = a < b ? a : b;
            B = b < a ? a : b;
        }

        public LocationLinkKey(LocationLinkObj obj)
        {
            this.A = obj.A;
            this.B = obj.B;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (!this.GetType().IsInstanceOfType(obj))
                return false;

            LocationLinkKey other = (LocationLinkKey)obj;

            return this == other; 
        }

        public override int GetHashCode()
        {
            return (int)(A % int.MaxValue); 
        }

        public static bool operator ==(LocationLinkKey A, LocationLinkKey B)
        {
            return (A.A == B.A) && (A.B == B.B);
        }

        public static bool operator !=(LocationLinkKey A, LocationLinkKey B)
        {
            return !((A.A == B.A) && (A.B == B.B));
        }
    }

    public class LocationLinkObj : WCFObjBaseWithKey<LocationLinkKey, LocationLink>
    {
        public override LocationLinkKey ID
        {
            get { return new LocationLinkKey(this); }
        }

        protected override int GenerateHashCode()
        {
            return (int)(Data.SourceID % int.MaxValue);
        }

        public long A
        {
            get { return Data.SourceID < Data.TargetID ? Data.SourceID : Data.TargetID; }
        }

        public long B
        {
            get { return Data.SourceID > Data.TargetID ? Data.SourceID : Data.TargetID; } 
        }

        public LocationLinkObj()
        {
            LocationLink link = new LocationLink();
            this.Data = link; 
        }

        public LocationLinkObj(long IDA,
                               long IDB)
        {
            LocationLink link = new LocationLink();
            link.SourceID = IDA;
            link.TargetID = IDB;
            this.Data = link; 
        }

        public LocationLinkObj(LocationLink link)
        {
            this.Data = link;
        }
    }
}
