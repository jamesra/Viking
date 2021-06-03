using System.Collections.Generic;

namespace IdentityServer.Models.UserViewModels
{
    public class EditResourceTypePermissionsViewModel
    {
        public string ResourceTypeId { get; set; }

        public IList<ResourceTypePermission> Permissions { get; set; }
    }
}
