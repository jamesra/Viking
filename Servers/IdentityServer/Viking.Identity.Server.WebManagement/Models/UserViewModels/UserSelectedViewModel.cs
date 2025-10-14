using System.Collections.Generic;
using System.Linq;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class UserSelectedViewModel : NamedItemSelectedViewModel<string> { }


    /// <summary>
    /// Update the organization assignments to match the UserSelectedViewModel
    /// </summary>
    public static class UserSelectedViewModelExtensions
    {
        public static void UpdateUserMembership(this Group group, IEnumerable<UserSelectedViewModel> Users)
        {
            foreach (UserSelectedViewModel user in Users)
            {
                group.UpdateUserMembership(user);
            }
        }

        public static void UpdateUserMembership(this Group group, UserSelectedViewModel user)
        {
            var ExistingMapping = group.MemberUsers.FirstOrDefault(u => u.UserId == user.Id);

            if (user.Selected)
            {
                if (ExistingMapping == null)
                {
                    //Create the mapping
                    UserToGroupAssignment oa = new UserToGroupAssignment() { GroupId = group.Id, UserId = user.Id };
                    group.MemberUsers.Add(oa);
                }
            }
            else
            {
                if (ExistingMapping != null)
                {
                    //Remove the mapping
                    group.MemberUsers.Remove(ExistingMapping);
                }
            }
        }
         
    }
}
