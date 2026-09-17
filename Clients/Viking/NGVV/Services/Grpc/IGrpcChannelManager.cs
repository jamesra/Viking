using Grpc.Core;

namespace Viking.Services.Grpc
{
    /// <summary>
    /// Interface for managing gRPC channel lifecycle and connections.
    /// </summary>
    public interface IGrpcChannelManager
    {
        /// <summary>
        /// Gets or creates a shared gRPC channel for the segmentation service.
        /// The channel is reused across all calls to avoid connection overhead.
        /// </summary>
        /// <returns>A gRPC channel, or null if the service is not available.</returns>
        Channel GetOrCreateChannel();

        /// <summary>
        /// Checks if the channel is in a healthy state for making calls.
        /// </summary>
        /// <returns>True if the channel is healthy, false otherwise.</returns>
        bool IsChannelHealthy();

        /// <summary>
        /// Forces recreation of the channel on next access. 
        /// Call this if you know the service endpoint has changed.
        /// </summary>
        void ResetChannel();

        /// <summary>
        /// Shutdown the shared channel. Should only be called during application shutdown.
        /// </summary>
        void Shutdown();
    }
}

