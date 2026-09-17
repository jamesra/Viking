using System;

namespace WebAnnotationModel.ServerInterface
{
    /// <summary>Owns the gRPC/WCF client lifetime. GetOrCreate reuses; do not dispose the returned instance.</summary>
    public interface IServerAnnotationsClientFactory<out INTERFACE>
    {
        /// <summary>Reuse the existing client when one exists; do not assume a new instance each call.</summary>
        INTERFACE GetOrCreate();
    }
}