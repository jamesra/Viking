using System;
using System.Collections.Generic;

namespace Viking.Identity.Server.WebApi.Models
{
    /// <summary>
    /// Row data for an organizational unit when building the volume tree.
    /// </summary>
    public class OuRow
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long? ParentID { get; set; }
    }

    /// <summary>
    /// Row data for a volume when building the volume tree.
    /// </summary>
    public class VolumeRow
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Uri Endpoint { get; set; }
        public long? ParentID { get; set; }
    }

    /// <summary>
    /// DTO for volume tree nodes returned by UserAccessibleVolumeTree endpoint.
    /// Matches client ApiVolumeTreeNode: OUs are branches, Volumes are leaves.
    /// </summary>
    public class VolumeTreeNodeDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long? ParentId { get; set; }
        public string ResourceType { get; set; }
        public List<UserResourcePermissionsDto> Volumes { get; set; } = new List<UserResourcePermissionsDto>();
        public List<VolumeTreeNodeDto> Children { get; set; } = new List<VolumeTreeNodeDto>();
    }

    /// <summary>
    /// DTO for a resource (e.g. Volume) with user permissions. Matches client UserResourcePermissions.
    /// </summary>
    public class UserResourcePermissionsDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ResourceType { get; set; }
        public IEnumerable<string> Permissions { get; set; } = System.Array.Empty<string>();
        public long? ParentId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
