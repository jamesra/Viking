using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;

namespace Viking.Tokens
{
    /// <summary>
    /// Builds a Duende <see cref="TokenResponse"/> from a raw JWT already in hand (launch-code exchange).
    /// Duende 7 exposes <see cref="TokenResponse.AccessToken"/> as TryGet("access_token") from parsed JSON,
    /// so assigning a backing field via reflection is a no-op and WCF then calls the annotation service unauthenticated.
    /// </summary>
    public static class TokenResponseFactory
    {
        /// <summary>
        /// Returns a TokenResponse whose AccessToken getter yields <paramref name="accessToken"/>, or null if empty/unreadable.
        /// Called on the UI thread from LoginWindow.OnLoaded; uses GetResult because that path is synchronous.
        /// </summary>
        public static TokenResponse FromAccessToken(string accessToken)
        {
            return FromAccessTokenAsync(accessToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async form of <see cref="FromAccessToken"/>. Returns null when the token is empty or JSON parse fails.
        /// </summary>
        public static async Task<TokenResponse> FromAccessTokenAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return null;

            string json = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["access_token"] = accessToken
            });

            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            TokenResponse response = await ProtocolResponse.FromHttpResponseAsync<TokenResponse>(httpResponse).ConfigureAwait(false);
            if (response is null || string.IsNullOrEmpty(response.AccessToken))
                return null;

            return response;
        }
    }
}
