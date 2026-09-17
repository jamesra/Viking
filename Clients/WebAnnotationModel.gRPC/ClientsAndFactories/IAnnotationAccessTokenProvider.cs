namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Supplies a bearer access token for authenticated gRPC calls.
    /// </summary>
    public interface IAnnotationAccessTokenProvider
    {
        /// <summary>
        /// Invoked per HTTP request so a refreshed token is used without recreating the channel.
        /// </summary>
        string GetAccessToken();
    }
}
