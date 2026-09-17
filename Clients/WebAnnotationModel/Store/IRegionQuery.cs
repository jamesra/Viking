using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebAnnotationModel
{
    public interface IRegionQuery<KEY, OBJECT>
        where KEY : struct
        where OBJECT : class
    {  
        /// <summary>
        /// Cache-only region query. Does not contact the server.
        /// Use <see cref="GetServerObjectsInRegion"/> to load missing objects from the server.
        /// </summary>
        Task<ICollection<OBJECT>> GetLocalObjectsInRegion(long SectionNumber, Geometry.Rectangle bounds, double MinRadius);

        /// <summary>
        /// Loads objects in the region from the server and adds them to the local store.
        /// </summary>
        /// <returns>The objects now known for the region and the UTC time the server query completed.</returns>
        Task<(ICollection<OBJECT> Objects, DateTime QueryCompletedTime)> GetServerObjectsInRegion(long SectionNumber, Geometry.Rectangle bounds, double MinRadius, DateTime? LastQueryUtc); 
    }
}
