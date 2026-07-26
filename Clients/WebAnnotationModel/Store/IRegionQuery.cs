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
        /// Return only objects already known on the client
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <param name="bounds"></param>
        /// <param name="MinRadius"></param>
        /// <param name="LastQueryUtc"></param>
        /// <returns></returns>
        Task<ICollection<OBJECT>> GetLocalObjectsInRegion(long SectionNumber, Geometry.GridRectangle bounds, double MinRadius);

        /// <summary>
        /// Return objects from the server
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <param name="bounds"></param>
        /// <param name="MinRadius"></param>
        /// <param name="LastQueryUtc"></param>
        /// <returns></returns>
        Task<ICollection<OBJECT>> GetServerObjectsInRegion(long SectionNumber, Geometry.GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc, out DateTime queryCompletedTime); 
    }
}
