using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Geometry;

namespace WebAnnotationModel
{
    public interface IRegionLoader<OBJECT>
    {
        /// <summary>
        /// Divides the requested region into a grid and requests intersecting grid cells.
        /// The callbacks are invoked multiple times as local and server objects are identified
        /// within each grid
        /// </summary>
        /// <param name="VolumeBounds"></param>
        /// <param name="ScreenPixelSizeInVolume"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="queryTargets"></param>
        /// <param name="OnServerObjectsLoadedCallback"></param>
        /// <param name="FoundCachedLocalObjectsCallback"></param>
        /// <returns></returns>
        Task<List<OBJECT>> GetObjectsInRegionAsync(GridRectangle VolumeBounds,
            double ScreenPixelSizeInVolume,
            int SectionNumber,
            QueryTargets queryTargets,
            CancellationToken token,
            Action<ICollection<OBJECT>> foundObjectCallback);
    }
}