using EntityFrameworkExtras.EFCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.DataModel.Annotation.UDT
{
    [UserDefinedTableType("udtParentChildIDMap")]
    [Index(nameof(ID), Name = nameof(ID))]
    [Index(nameof(ParentID), Name = nameof(ParentID))]
    public class udtParentChildIDMap
    {
        [UserDefinedTableTypeColumn(1, nameof(ID))]
        public long ID { get; set; }

        [UserDefinedTableTypeColumn(2, nameof(ParentID))]
        public long ParentID { get; set; }
    }
}
