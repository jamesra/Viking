using System;
using System.Collections.Generic;

namespace Viking.Identity.Server.Extensions.Services
{
    public class UserResourcePermissions
    {
        /// <summary>
        /// The resource ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The resource's name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The resource's type, e.g., "Volume", "SegmentationService", etc.
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// The permissions the user has on this resource
        /// </summary>
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional parent resource ID (for hierarchical objects)
        /// </summary>
        public long? ParentId { get; set; }

        /// <summary>
        /// Additional resource metadata - set as needed (optional)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}


