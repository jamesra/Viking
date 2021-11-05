using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkExtras.EFCore;
using Microsoft.EntityFrameworkCore;
using Viking.DataModel.Annotation.UDT;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// Hand written implementations for udt parameter accepting or returning stored procedures or functions
    /// </summary>
    public partial class AnnotationContext
    {
        public List<MorphologyPathsResult> MorphologyPaths(long SourceID, IEnumerable<long> TargetIDs)
        {
            var udf = new MorphologyPaths { SourceID = SourceID, 
                TargetIDs = TargetIDs.Select(id => new integer_list() { ID = id }).ToList()};

            this.Database.ExecuteStoredProcedureAsync(udf);
            return udf.Result;
        }
    }
}