using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.UserViewModels
{

    public class ResourcePermissionsViewModel<KEY>
    {
        [Display(Name = "Grantee", Description = "Entity being granted permission")]
        public virtual KEY GranteeId { get; set; }

        [Display(Name = "Name", Description = "Name of entity being granted permission")]
        public virtual string Name { get; set; }
        
        //public List<string> AvailablePermissions { get; set; }

        public virtual List<ItemSelectedViewModel<string>> Permissions { get; set; }

        /// <summary>
        /// True if the given permission is granted
        /// </summary>
        //public string[] Permissions { get; set; }
    }

    public class UserResourcePermissionsViewModel : ResourcePermissionsViewModel<string> 
    { 
    }

    public class GroupResourcePermissionsViewModel : ResourcePermissionsViewModel<long> { }
}
