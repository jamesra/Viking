using System;
using System.Collections.Generic;

namespace WebAnnotationModel
{
    /// <summary>
    /// Returned type used by queries which return a local cache immediately and a handle on the server request for updates
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    /// <typeparam name="OBJECT"></typeparam>
    public class MixedLocalAndRemoteQueryResults<KEY, OBJECT>
    {
        public readonly IAsyncResult ServerRequestResult = null;
        public readonly ICollection<OBJECT> KnownObjects = null;

        public MixedLocalAndRemoteQueryResults(IAsyncResult result, ICollection<OBJECT> known_objects)
        {
            this.ServerRequestResult = result;
            this.KnownObjects = known_objects;
        }
    }
}
