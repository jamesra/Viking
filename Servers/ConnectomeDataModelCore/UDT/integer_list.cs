using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkExtras;
using EntityFrameworkExtras.EFCore;

namespace Viking.DataModel.Annotation.UDT
{
    [UserDefinedTableType("integer_list")]
    public class integer_list
    {
        [UserDefinedTableTypeColumn(1, nameof(ID))]
        public long ID { get; set; }
    }
}
