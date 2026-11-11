using System.Collections.Generic;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>One permission a resource type offers, as stored in the ResourceTypePermission table.</summary>
    public class PermissionInfo
    {
        public string PermissionId { get; set; }
        public string Description { get; set; }
    }

    /// <summary>A resource type with every permission that can be granted on its resources.</summary>
    public class ResourceTypeInfo
    {
        public string ResourceTypeId { get; set; }
        public string Description { get; set; }
        public List<PermissionInfo> Permissions { get; set; } = new List<PermissionInfo>();
    }
}
