using Viking.Tokens;
using WebAnnotationModel.gRPC;

namespace WebAnnotation.Services
{
    /// <summary>
    /// Reads the signed-in Viking user's bearer token for annotation gRPC calls.
    /// Login writes both <see cref="Viking.UI.State.UserBearerToken"/> and
    /// <see cref="TokenStore.BearerToken"/>; prefer whichever is populated.
    /// </summary>
    internal sealed class VikingAnnotationAccessTokenProvider : IAnnotationAccessTokenProvider
    {
        public string GetAccessToken() =>
            Viking.UI.State.UserBearerToken?.AccessToken
            ?? TokenStore.BearerToken?.AccessToken;
    }
}
