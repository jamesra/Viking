using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IdentityServer.Models;

namespace IdentityServer.Models.UserViewModels
{
    public class GroupSelectedViewModel
    {
        [Required]
        public long Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public bool Selected { get; set; }
    }

    /// <summary>
    /// Update the organization assignments to match the UserSelectedViewModel
    /// </summary>
    public static class OrganizationSelectedViewModelExtensions
    {
        public static void UpdateOrganizations(this ApplicationUser user, IEnumerable<GroupSelectedViewModel> Organizations)
        {
            foreach (GroupSelectedViewModel org in Organizations)
            {
                user.UpdateOrganization(org);
            }
        }

        public static void UpdateOrganization(this ApplicationUser user, GroupSelectedViewModel org)
        {
            var ExistingMapping = user.GroupAssignments.FirstOrDefault(o => o.GroupId == org.Id);

            if (org.Selected)
            {
                if (ExistingMapping == null)
                {
                    //Create the mapping
                    UserToGroupAssignment oa = new UserToGroupAssignment() { GroupId = org.Id, UserId = user.Id };
                    user.GroupAssignments.Add(oa);
                }
            }
            else
            {
                if (ExistingMapping != null)
                {
                    //Remove the mapping
                    user.GroupAssignments.Remove(ExistingMapping);
                }
            } 
        }
    }
}