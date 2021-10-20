using Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebAnnotationModel.ServerInterface
{
    public interface IServerSpatialAnnotations<OBJECT>
    {
        /// <summary>
        /// Fetches objects from the server in the specified regionf
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="ScreenPixelSizeInVolume"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        Task<IList<OBJECT>> GetAsync(IRectangle bounds,
                                     double ScreenPixelSizeInVolume,
                                     int Z);
    }
}
