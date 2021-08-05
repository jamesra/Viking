using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAnnotationModel.ServerInterface
{
    public interface IServerAnnotations<KEY, OBJECT>
        where KEY : struct, IEquatable<KEY>
    {
        /// <summary>
        /// Get the specified object from the server
        /// </summary>
        /// <param name="key"></param>
        Task<OBJECT> GetAsync(KEY key);

        Task<IList<OBJECT>> GetAsync(IEnumerable<KEY> keys);

        Task<OBJECT> Create(OBJECT obj);

        Task<OBJECT> UpdateAsync(OBJECT obj);

        Task<IList<OBJECT>> UpdateAsync(IEnumerable<OBJECT> objs);

        Task<KEY?> Delete(KEY key);
    }
}
