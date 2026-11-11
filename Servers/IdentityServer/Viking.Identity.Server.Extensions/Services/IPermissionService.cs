using System.Collections.Generic;
using System.Threading.Tasks;

namespace Viking.Identity.Server.Extensions.Services
{
    public interface IPermissionService
    {
        /// <summary>
        /// Resources of one type the user holds grants on. <paramref name="requiredPermissions"/>, when given, keeps only
        /// resources on which the user holds all of them.
        /// </summary>
        Task<Dictionary<long, UserResourcePermissions>> GetUserPermissionsByTypeAsync(
            string userId, string resourceTypeId, IReadOnlyCollection<string> requiredPermissions = null);
        /// <summary>
        /// Name, database name and service URLs of the annotation servers among <paramref name="ids"/> on which the user
        /// holds Read (implied by Annotate/Review). Ids that do not exist or are not readable are left out.
        /// </summary>
        Task<Dictionary<long, Dictionary<string, object>>> GetReadableAnnotationServerDetailsAsync(
            string userId, IReadOnlyCollection<long> ids);
        /// <summary>
        /// The permission catalog (the ResourceTypePermission table): every resource type with the permissions it defines,
        /// or just <paramref name="resourceTypeId"/> (empty list when the type does not exist). No per-user data.
        /// </summary>
        Task<List<ResourceTypeInfo>> GetResourceTypeCatalogAsync(string resourceTypeId = null);
        Task<List<UserResourcePermissions>> GetUserPermissionsAsync(string userId);
        Task<List<string>> GetUserResourcePermissionsAsync(string userId, string resourceId);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleAnnotationContextsAsync(string userId);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleSegmentationServicesAsync(string userId);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleSegmentationServicesByUsernameAsync(string username);
        Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleAnnotationContextsByUsernameAsync(string username);
        Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeAsync(string userId);
        Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeForAnonymousAsync();
    }
}

