using Geometry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotationModel.ServerInterface
{

    public interface IServerSpatialAnnotationsClient<KEY, SERVER_OBJECT>
        where KEY : struct, IEquatable<KEY>
    {
        /// <summary>
        /// Fetches objects from the server in the specified region
        /// </summary>
        /// <param name="Z">Section number.</param>
        /// <param name="bounds">The region in mosaic space.</param>
        /// <param name="ScreenPixelSizeInVolume">Minimum location radius to return.</param>
        /// <param name="modifiedAfter">Only objects changed after this time; null for all.</param>
        /// <param name="token">Cancels the request.</param>
        /// <param name="onChunk">Optional, awaited for each streamed batch.</param>
        Task<ServerUpdate<KEY, SERVER_OBJECT[]>> GetAsync(long Z, 
                                     Rectangle bounds,
                                     double ScreenPixelSizeInVolume,
                                     DateTime? modifiedAfter,
                                     CancellationToken token,
                                     Func<ServerUpdate<KEY, SERVER_OBJECT[]>, Task> onChunk);
    }
}
