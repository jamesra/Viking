using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotationModel.ServerInterface
{
    public interface IServerAnnotationsClient<KEY, OBJECT, in CREATION_INPUT, CREATION_OUTPUT>
        where KEY : struct, IEquatable<KEY>
    {
        /// <summary>
        /// Get the specified object from the server
        /// </summary>
        /// <param name="key"></param>
        Task<OBJECT> GetAsync(KEY key, CancellationToken token);

        Task<IList<OBJECT>> GetAsync(IEnumerable<KEY> keys, CancellationToken token);

        Task<CREATION_OUTPUT> Create(CREATION_INPUT obj, CancellationToken token);

        Task<UpdateResults<KEY, OBJECT>> UpdateAsync(OBJECT obj, CancellationToken token);

        Task<UpdateResults<KEY, OBJECT>> UpdateAsync(IEnumerable<OBJECT> objs, CancellationToken token);

        Task<KEY?> Delete(KEY key, CancellationToken token);
    }
}
