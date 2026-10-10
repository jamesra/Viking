using System;

namespace ConnectomeDataModel
{
    /// <summary>
    /// One row of batched structure freshness for mesh/cache consumers (e.g. sbfsem-tools).
    /// Aggregates the structure and its direct children (<c>ParentID = StructureID</c>).
    /// </summary>
    public sealed class StructureFreshness
    {
        /// <summary>
        /// Requested structure id (root of the aggregation).
        /// </summary>
        public long StructureID { get; set; }

        /// <summary>
        /// Newest <see cref="Structure.LastModified"/> or <see cref="Location.LastModified"/>
        /// among the structure and its direct children (UTC wall time in SQL datetime).
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Count of locations owned by the structure and its direct children.
        /// </summary>
        public long AnnotationCount { get; set; }
    }
}
