using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAnnotationModel
{
    
    public interface IRegionQuery<KEY, OBJECT> 
        where KEY : struct 
        where OBJECT : class
    {
        ConcurrentDictionary<KEY, OBJECT> GetObjectsInRegion(long SectionNumber, Geometry.GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc);

        MixedLocalAndRemoteQueryResults<KEY, OBJECT> GetObjectsInRegionAsync(long SectionNumber, Geometry.GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc, Action<ICollection<OBJECT>> OnLoadedCallback);
    }

    public interface ISectionQuery<KEY, OBJECT>
        where KEY : struct
        where OBJECT : class
    {
        ConcurrentDictionary<KEY, OBJECT> GetLocalObjectsForSection(long SectionNumber);

        ConcurrentDictionary<KEY, OBJECT> GetObjectsForSection(long SectionNumber);

        MixedLocalAndRemoteQueryResults<KEY, OBJECT> GetObjectsForSectionAsynch(long SectionNumber, Action<ICollection<OBJECT>> OnLoadedCallback);
    }
}
