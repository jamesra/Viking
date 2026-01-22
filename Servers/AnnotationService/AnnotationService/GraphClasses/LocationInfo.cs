using System.Runtime.Serialization;


namespace AnnotationService.Types
{
    [DataContract]
    public class LocationInfo(double a, double b, double c, double rad)
    {
        [DataMember]
        public double X = a;
        [DataMember]
        public double Y = b;
        [DataMember]
        public double Z = c;
        [DataMember]
        public double Radius = rad;
    }


}