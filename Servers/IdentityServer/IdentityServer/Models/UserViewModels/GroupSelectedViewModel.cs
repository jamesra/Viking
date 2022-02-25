using System.Collections.Generic;
using System.Linq;

namespace Viking.Identity.Models.UserViewModels
{
    public class GroupSelectedViewModel : NamedItemSelectedViewModel<long> { }

    /// <summary>
    /// Update the organization assignments to match the UserSelectedViewModel
    /// </summary>
    public static class GroupSelectedViewModelExtensions
    {
        public static void UpdateGroups(this ApplicationUser user, IEnumerable<GroupSelectedViewModel> Organizations)
        {
            foreach (GroupSelectedViewModel org in Organizations)
            {
                user.UpdateGroupMembership(org);
            }
        }

        /// <summary>
        /// Add or remove the user from the group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="org"></param>
        public static void UpdateGroupMembership(this ApplicationUser user, GroupSelectedViewModel org)
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

        public static void UpdateGroupMembership(this Group group, IEnumerable<GroupSelectedViewModel> Organizations)
        {
            foreach (GroupSelectedViewModel org in Organizations)
            {
                group.UpdateGroupMembership(org);
            }
        }

        /// <summary>
        /// Add or remove a member group to a group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="org"></param>
        public static void UpdateGroupMembership(this Group group, GroupSelectedViewModel org)
        {
            var ExistingMapping = group.MemberGroups.FirstOrDefault(o => o.MemberGroupId == org.Id);

            //We cannot be a member of our own group, but add other groups if they are selected
            if (org.Selected && group.Id != org.Id)
            {
                if (ExistingMapping == null)
                {
                    //Create the mapping
                    GroupToGroupAssignment oa = new GroupToGroupAssignment() {  ContainerGroupId = group.Id,  MemberGroupId = org.Id};
                    group.MemberGroups.Add(oa);
                }
            }
            else
            {
                if (ExistingMapping != null)
                {
                    //Remove the mapping
                    group.MemberGroups.Remove(ExistingMapping);
                }
            }
        }
    }
}