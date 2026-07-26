using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkExtras;
using EntityFrameworkExtras.EFCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.DataModel.Annotation.UDT
{
    [UserDefinedTableType("integer_list")]
    
    public class integer_list
    {
        [UserDefinedTableTypeColumn(1, nameof(ID))]
        [Key]
        public long ID { get; set; }
    }
}
