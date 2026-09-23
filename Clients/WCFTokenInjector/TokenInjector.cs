using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Diagnostics;
using System.Threading;

namespace Viking.Tokens
{

    public class TokenInjector : System.ServiceModel.Dispatcher.IClientMessageInspector
    {
        public static string BearerTokenAuthority = null;
        public static Duende.IdentityModel.Client.TokenResponse BearerToken = null;

        /// <summary>
        /// First successful attach is logged once per process. Later calls stay quiet
        /// so a DBWIN monitor can see whether the launch token was sent without a token value.
        /// </summary>
        private static int _attachLogged;

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            return;
        }

        /// <summary>
        /// True when a non-empty access token is published. Authority is optional for
        /// attaching Authorization; launch-exchange can leave settings.IdentityServerURL blank.
        /// </summary>
        public static bool TryGetBearerAuthorizationValue(out string authorizationHeaderValue)
        {
            authorizationHeaderValue = null;
            if (BearerToken == null || string.IsNullOrEmpty(BearerToken.AccessToken))
                return false;

            authorizationHeaderValue = $"Bearer {BearerToken.AccessToken}";
            return true;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            if (!TryGetBearerAuthorizationValue(out string authorization))
            {
                Trace.WriteLine(
                    "[TokenInjector] Skipping Authorization: BearerToken is unset or AccessToken is empty. " +
                    $"Authority={(BearerTokenAuthority ?? "(null)")}");
                return null;
            }

            if (string.IsNullOrEmpty(BearerTokenAuthority))
            {
                Trace.WriteLine("[TokenInjector] BearerTokenAuthority is unset; still attaching Authorization header.");
            }

            if (Interlocked.Exchange(ref _attachLogged, 1) == 0)
            {
                Trace.WriteLine(
                    "[TokenInjector] Attached Authorization Bearer. " +
                    $"AccessTokenLength={BearerToken.AccessToken.Length} Authority={(BearerTokenAuthority ?? "(null)")}");
            }

            // Add bearer token to HTTP Authorization header
            HttpRequestMessageProperty httpRequestMessage;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out object httpRequestMessageObject))
            {
                httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
                if (httpRequestMessage != null && string.IsNullOrEmpty(httpRequestMessage.Headers["Authorization"]))
                {
                    httpRequestMessage.Headers["Authorization"] = authorization;
                }
            }
            else
            {
                httpRequestMessage = new HttpRequestMessageProperty();
                httpRequestMessage.Headers["Authorization"] = authorization;
                request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
            }

            return null;
        }
    }
}
