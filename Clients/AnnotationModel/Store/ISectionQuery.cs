using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WebAnnotationModel
{

    public interface ISectionQuery<KEY, OBJECT>
        where KEY : struct
        where OBJECT : class
    {
        ConcurrentDictionary<KEY, OBJECT> GetLocalObjectsForSection(long SectionNumber);

        ConcurrentDictionary<KEY, OBJECT> GetObjectsForSection(long SectionNumber);

        MixedLocalAndRemoteQueryResults<KEY, OBJECT> GetObjectsForSectionAsynch(long SectionNumber, Action<ICollection<OBJECT>> OnLoadedCallback);
    }
}
