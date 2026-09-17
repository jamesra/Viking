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

        /// <summary>
        /// Section of endpoint A at delete time. Null on rows written before this column existed.
        /// </summary>
        [Column("AZ")]
        public long? Az { get; set; }

        /// <summary>
        /// Section of endpoint B at delete time. Null on rows written before this column existed.
        /// </summary>
        [Column("BZ")]
        public long? Bz { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime DeletedOn { get; set; }
    }
}
