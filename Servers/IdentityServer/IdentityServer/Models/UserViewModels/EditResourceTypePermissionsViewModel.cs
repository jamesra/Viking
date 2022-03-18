using System.Collections.Generic;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class EditResourceTypePermissionsViewModel
    {
        public string ResourceTypeId { get; set; }

        public IList<ResourceTypePermission> Permissions { get; set; }
    }
}
