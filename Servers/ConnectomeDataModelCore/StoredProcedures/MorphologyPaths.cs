using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkExtras.EFCore;
using EntityFrameworkExtras;
using Viking.DataModel.Annotation.UDT;

namespace Viking.DataModel.Annotation
{
    [StoredProcedure(nameof(MorphologyPaths))]
    public class MorphologyPaths
    {
        [StoredProcedureParameter(SqlDbType.BigInt, Direction = ParameterDirection.Input)]
        public long SourceID { get; set; }

        [StoredProcedureParameter(SqlDbType.Udt, Direction = ParameterDirection.Input)]
        public List<integer_list> TargetIDs { get; set; }

        [StoredProcedureParameter(SqlDbType.Udt, Direction = ParameterDirection.ReturnValue)]
        public List<MorphologyPathsResult> Result { get; set; }
    }
}
