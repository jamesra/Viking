using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;


namespace AnnotationService.Types
{
    [DataContract]
    public class LocationInfo
    {
        [DataMember]
        public double X;
        [DataMember]
        public double Y;
        [DataMember]
        public double Z;
        [DataMember]
        public double Radius;

        public LocationInfo(double a, double b, double c, double rad)
        {
            this.X = a;

            this.Y = b;

            this.Z = c;

            this.Radius = rad;
        }
    }


}