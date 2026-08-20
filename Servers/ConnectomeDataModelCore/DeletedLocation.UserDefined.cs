using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace Viking.DataModel.Annotation
{
    public partial class DeletedLocation
    {
        /// <summary>
        /// Section the location occupied when deleted. Null on rows written before this column existed;
        /// those still match every section query so older watermarks stay complete.
        /// </summary>
        [Column("Z")]
        public long? Z { get; set; }
    }
}

