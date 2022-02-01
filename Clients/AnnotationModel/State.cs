using System;
using System.ServiceModel;

namespace WebAnnotationModel
{
    public class State
    {
        public static System.Net.NetworkCredential UserCredentials = new System.Net.NetworkCredential("anonymous", "connectome");

        public static bool UseAsynchEvents = true;

        public static Uri Endpoint
        {
            get => EndpointAddress?.Uri;
            set => EndpointAddress = new EndpointAddress(value);
        }

        internal static EndpointAddress EndpointAddress
        {
            get; private set;
        }

        /// <summary>
        /// Record the program start time so we do not send queries that request every update since the start of time,
        /// only every update since our first possible query.
        /// </summary>
        public readonly static DateTime ProgramStartTime = DateTime.UtcNow;
    }
}
