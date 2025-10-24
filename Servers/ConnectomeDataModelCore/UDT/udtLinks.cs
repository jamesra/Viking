using EntityFrameworkExtras.EFCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.DataModel.Annotation.UDT
{
    [UserDefinedTableType("udtLinks")] 
    [Index(nameof(SourceID), Name = nameof(SourceID))]
    [Index(nameof(TargetID), Name = nameof(TargetID))]
    public class udtLinks
    {
        [UserDefinedTableTypeColumn(1, nameof(SourceID))]
        public long SourceID { get; set; }

        [UserDefinedTableTypeColumn(1, nameof(TargetID))]
        public long TargetID { get; set; }
    }
}
