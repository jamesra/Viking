using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace Viking.DataModel.Annotation
{
    public partial class DeletedStructure
    {
        [Key]
        [Column("ID")]
        public long Id { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime DeletedOn { get; set; }
    }
}
