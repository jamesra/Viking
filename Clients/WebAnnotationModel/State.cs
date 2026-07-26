using System;
using System.ServiceModel;

namespace WebAnnotationModel
{
    public class State
    {
        /// <summary>
        /// Default credentials used when no login has been performed (e.g. anonymous).
        /// Do not return null to avoid SecurityAccessDeniedException from WCF when credentials are missing.
        /// </summary>
        private static readonly System.Net.NetworkCredential DefaultCredentials = new("anonymous", "connectome");

        /// <summary>
        /// User credentials for WCF AnnotationService calls. Never null; falls back to DefaultCredentials.
        /// </summary>
        public static System.Net.NetworkCredential UserCredentials
        {
            get => _userCredentials ?? DefaultCredentials;
            set => _userCredentials = value;
        }
        private static System.Net.NetworkCredential _userCredentials;

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
        public static readonly DateTime ProgramStartTime = DateTime.UtcNow;
    }
}
