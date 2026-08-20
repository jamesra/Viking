using Duende.IdentityModel.Client;

namespace Viking.Tokens
{
    /// <summary>
    /// Process-wide bearer token used by HTTP/gRPC clients.
    /// </summary>
    public static class TokenStore
    {
        public static string BearerTokenAuthority { get; set; }

        public static TokenResponse BearerToken { get; set; }
    }
}
