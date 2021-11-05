using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotationModel.ServerInterface
{
    public interface IServerAnnotationsBySectionClient<KEY, SERVER_OBJECT>
        where KEY : struct, IEquatable<KEY>
    {
        /// <summary>
        /// Fetches objects from the server in the specified regionf
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="ScreenPixelSizeInVolume"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        Task<ServerUpdate<KEY, SERVER_OBJECT>> GetAsync(long Z,
            DateTime? modifiedAfter,
            CancellationToken token);
    }
}
