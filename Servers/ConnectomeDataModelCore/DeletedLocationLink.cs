using System;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace Viking.DataModel.Annotation
{
    public partial class DeletedLocationLink
    {
        [Column("A")]
        public long A { get; set; }

        [Column("B")]
        public long B { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime DeletedOn { get; set; }
    }
}
