using System.Collections.Generic;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Nodes are organizational units, Volumes are the leaves of the tree
    /// </summary>
    public class VolumeTreeNode
    {
        public long Id { get; set; }
        public string Name { get; set; }

        public long? ParentId { get; set; }
        /// <summary>
        /// The resource's type, e.g., "Volume", "SegmentationService", etc.
        /// </summary>
        public string ResourceType { get; set; }

        public List<UserResourcePermissions> Volumes { get; set; } = new List<UserResourcePermissions>();

        public List<VolumeTreeNode> Children { get; set; } = new List<VolumeTreeNode>();
    }
}


