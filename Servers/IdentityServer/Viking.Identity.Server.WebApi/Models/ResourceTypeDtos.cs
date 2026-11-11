namespace Viking.Identity.Server.WebApi.Models
{
    /// <summary>A resource type and the permissions it defines, from the permission catalog.</summary>
    public class ResourceTypeDto
    {
        public string ResourceTypeId { get; set; }
        public string Description { get; set; }
        public System.Collections.Generic.List<PermissionDto> Permissions { get; set; } = new System.Collections.Generic.List<PermissionDto>();
    }

    /// <summary>One permission of a resource type.</summary>
    public class PermissionDto
    {
        public string PermissionId { get; set; }
        public string Description { get; set; }
    }
}
