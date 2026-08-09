using WebAnnotationModel.gRPC;

namespace WebAnnotation.Services
{
    /// <summary>
    /// Reads the signed-in Viking user's bearer token for annotation gRPC calls.
    /// </summary>
    internal sealed class VikingAnnotationAccessTokenProvider : IAnnotationAccessTokenProvider
    {
        public string GetAccessToken() => Viking.UI.State.UserBearerToken?.AccessToken;
    }
}
