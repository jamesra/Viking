using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Viking.Tokens
{

    public class TokenInjector : System.ServiceModel.Dispatcher.IClientMessageInspector
    {
        public static string BearerTokenAuthority = null;
        public static Duende.IdentityModel.Client.TokenResponse BearerToken = null;

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            return;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        { 
            if (BearerTokenAuthority != null && BearerToken != null)
            {
                // Add bearer token to HTTP Authorization header
                HttpRequestMessageProperty httpRequestMessage;
                object httpRequestMessageObject;
                if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
                {
                    httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
                    if (httpRequestMessage != null && string.IsNullOrEmpty(httpRequestMessage.Headers["Authorization"]))
                    {
                        httpRequestMessage.Headers["Authorization"] = $"Bearer {BearerToken.AccessToken}";
                    }
                }
                else
                {
                    httpRequestMessage = new HttpRequestMessageProperty();
                    httpRequestMessage.Headers["Authorization"] = $"Bearer {BearerToken.AccessToken}";
                    request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
                }
            }
            return null;
        }
    }
}
