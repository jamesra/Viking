using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Viking.Tokens
{
    public class TokenInjector : System.ServiceModel.Dispatcher.IClientMessageInspector
    {
        public static string BearerTokenAuthority
        {
            get => TokenStore.BearerTokenAuthority;
            set => TokenStore.BearerTokenAuthority = value;
        }

        public static Duende.IdentityModel.Client.TokenResponse BearerToken
        {
            get => TokenStore.BearerToken;
            set => TokenStore.BearerToken = value;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            if (BearerTokenAuthority != null && BearerToken != null)
            {
                HttpRequestMessageProperty httpRequestMessage;
                if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out object httpRequestMessageObject))
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
