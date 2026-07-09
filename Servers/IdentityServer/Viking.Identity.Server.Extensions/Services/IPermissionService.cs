using System.Collections.Generic;
using System.Threading.Tasks;

namespace Viking.Identity.Server.Extensions.Services
{
    public interface IPermissionService
    {
        Task<Dictionary<long, UserResourcePermissions>> GetUserPermissionsByTypeAsync(string userId, string resourceTypeId);
        Task<List<UserResourcePermissions>> GetUserPermissionsAsync(string userId);
        Task<List<string>> GetUserResourcePermissionsAsync(string userId, string resourceId);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleVolumesAsync(string userId);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleSegmentationServicesAsync(string userId);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleSegmentationServicesByUsernameAsync(string username);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleVolumesByUsernameAsync(string username);
        Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeAsync(string userId);
        Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeForAnonymousAsync();
    }
}

