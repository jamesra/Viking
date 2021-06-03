using System.Collections.Generic;
using System.Linq;

namespace IdentityServer.Models.UserViewModels
{
    public static class PermissionsExtensions
    {  
        
        /// <summary>
        /// Will only add selected users to permissions, but not remove unselected users
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="Permissions"></param>
        /// <param name="Users"></param>
        public static void AddGrantedUserPermissions(this Resource resource, IEnumerable<NamedItemSelectedViewModel<string>> Permissions, IEnumerable<NamedItemSelectedViewModel<string>> Users)
        {
            foreach (var permission in Permissions.Where(p => p.Selected))
            {
                foreach (var item in Users.Where(item => item.Selected))
                {
                    if( false == resource.UsersWithPermissions.Any(uwp => uwp.PermissionId == permission.Id && uwp.UserId == item.Id && uwp.ResourceId == resource.Id))
                    {
                        GrantedUserPermission row = new GrantedUserPermission() { UserId = item.Id, PermissionId = permission.Id, ResourceId = resource.Id };
                        resource.UsersWithPermissions.Add(row);
                    }
                }
            }
        }

        /*
        /// <summary>
        /// Adds selected items from the permissions and remove unselected items
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="Permissions"></param>
        /// <param name="Users"></param>
        public static void UpdateGrantedUserPermissions(this Resource resource, IEnumerable<NamedItemSelectedViewModel<string>> Permissions, IEnumerable<NamedItemSelectedViewModel<string>> Users)
        {
            foreach (var permission in Permissions.Where(p => p.Selected))
            {
                foreach (var item in Users)
                {
                    var ExistingMapping = resource.UsersWithPermissions.FirstOrDefault(uwp => uwp.PermissionId == permission.Id && uwp.UserId == item.Id && uwp.ResourceId == resource.Id);

                    if (item.Selected)
                    {
                        if (ExistingMapping == null)
                        {
                            GrantedUserPermission row = new GrantedUserPermission() { UserId = item.Id, PermissionId = permission.Id, ResourceId = resource.Id };
                            resource.UsersWithPermissions.Add(row);
                        }
                    }
                    else
                    {
                        if(ExistingMapping != null)
                        {
                            //Remove the mapping
                            resource.UsersWithPermissions.Remove(ExistingMapping);
                        } 
                    }
                }
            }
        }
        */

        /// <summary>
        /// Will only add selected users to permissions, but not remove unselected users
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="Permissions"></param>
        /// <param name="Groups"></param>
        public static void AddGrantedGroupPermissions(this Resource resource, IEnumerable<NamedItemSelectedViewModel<string>> Permissions, IEnumerable<NamedItemSelectedViewModel<long>> Groups)
        {
            foreach (var permission in Permissions.Where(p => p.Selected))
            {
                foreach (var item in Groups.Where(item => item.Selected))
                {  
                    if (false == resource.GroupsWithPermissions.Any(uwp => uwp.PermissionId == permission.Id && uwp.GroupId == item.Id && uwp.ResourceId == resource.Id))
                    {
                        GrantedGroupPermission row = new GrantedGroupPermission() { GroupId = item.Id, PermissionId = permission.Id, ResourceId = resource.Id };
                        resource.GroupsWithPermissions.Add(row);
                    }
                }
            }
        }

        /*
        /// <summary>
        /// Adds selected items from the permissions and remove unselected items
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="Permissions"></param>
        /// <param name="Users"></param>
        public static void UpdateGrantedGroupPermissions(this Resource resource, IEnumerable<NamedItemSelectedViewModel<string>> Permissions, IEnumerable<NamedItemSelectedViewModel<long>> Groups)
        {
            foreach (var permission in Permissions.Where(p => p.Selected))
            {
                foreach (var item in Groups)
                { 
                    var ExistingMapping = resource.GroupsWithPermissions.FirstOrDefault(uwp => uwp.PermissionId == permission.Id && uwp.GroupId == item.Id && uwp.ResourceId == resource.Id);

                    if (item.Selected)
                    {
                        if (ExistingMapping == null)
                        {
                            GrantedGroupPermission row = new GrantedGroupPermission() { GroupId = item.Id, PermissionId = permission.Id, ResourceId = resource.Id };
                            resource.GroupsWithPermissions.Add(row);
                        }
                    }
                    else
                    {
                        if (ExistingMapping != null)
                        {
                            //Remove the mapping
                            resource.GroupsWithPermissions.Remove(ExistingMapping);
                        }
                    }
                }
            }
        }
        */
    }
}
