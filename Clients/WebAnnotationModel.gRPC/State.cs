using System; 

namespace WebAnnotationModel
{
    public class State
    {
        /// <summary>
        /// Legacy WCF credentials. gRPC auth uses IAnnotationAccessTokenProvider, not this field.
        /// </summary>
        public static System.Net.NetworkCredential UserCredentials = new System.Net.NetworkCredential("anonymous", "connectome");

        /// <summary>
        /// When true, store CollectionChanged runs on Task.Run. Views that touch WPF
        /// (RootObjects, trees) must Dispatcher.BeginInvoke. Default is true.
        /// </summary>
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
