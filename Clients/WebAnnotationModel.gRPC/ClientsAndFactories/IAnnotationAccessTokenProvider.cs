namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Supplies a bearer access token for authenticated gRPC calls.
    /// Invoked per-call so refreshed tokens are picked up without recreating channels.
    /// </summary>
    public interface IAnnotationAccessTokenProvider
    {
        string GetAccessToken();
    }
}
