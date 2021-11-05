using System; 

namespace WebAnnotationModel
{
    public class State
    {
        public static System.Net.NetworkCredential UserCredentials = new System.Net.NetworkCredential("anonymous", "connectome");

        public static bool UseAsynchEvents = true;

        public static Uri Endpoint
        {
            get => EndpointAddress != null ? new Uri(EndpointAddress) : null;
            set => EndpointAddress = value.ToString();
        }

        internal static string EndpointAddress
        {
            get; private set;
        }
    }
}
