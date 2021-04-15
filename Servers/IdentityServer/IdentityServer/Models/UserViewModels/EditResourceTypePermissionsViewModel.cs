using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer.Models.UserViewModels
{
    public class EditResourceTypePermissionsViewModel
    {
        public string ResourceTypeId { get; set; }

        public IList<ResourceTypePermission> Permissions { get; set; }
    }
}
