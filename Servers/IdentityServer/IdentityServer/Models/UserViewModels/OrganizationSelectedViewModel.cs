using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IdentityServer.Models;

namespace IdentityServer.Models.UserViewModels
{
    public class OrganizationSelectedViewModel
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
        public static void UpdateOrganizationUsers(this ApplicationUser user, IEnumerable<OrganizationSelectedViewModel> Organizations)
        {
            foreach (OrganizationSelectedViewModel org in Organizations)
            {
                var ExistingMapping = user.OrganizationAssignments.FirstOrDefault(o => o.OrganizationId == org.Id);

                if (org.Selected)
                {
                    if (ExistingMapping == null)
                    {
                        //Create the mapping
                        OrganizationAssignment oa = new OrganizationAssignment() { OrganizationId = org.Id, UserId = user.Id };
                        user.OrganizationAssignments.Add(oa);
                    }
                }
                else
                {
                    if (ExistingMapping != null)
                    {
                        //Remove the mapping
                        user.OrganizationAssignments.Remove(ExistingMapping);
                    }
                }
            }
        }
    }
}