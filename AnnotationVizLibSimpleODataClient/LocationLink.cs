using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.Generic;

namespace AnnotationVizLib.SimpleOData
{
    public class LocationLink : ILocationLink, IEquatable<LocationLink>
    {
        public static LocationLink FromDictionary(IDictionary<string, object> dict)
        {
            LocationLink ll = new LocationLink { A = System.Convert.ToUInt64(dict["A"]), B = System.Convert.ToUInt64(dict["B"]) };
            return ll;
        }



        public ulong A { get; private set; }
        public ulong B { get; private set; }

        public bool Equals(ILocationLink other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.A == this.A && other.B == this.B)
                return true;

            if (other.A == this.B && other.B == this.A)
                return true;

            return false;
        }

        public bool Equals(LocationLink other)
        {
            return this.Equals((ILocationLink)other);
        }
    }
}
