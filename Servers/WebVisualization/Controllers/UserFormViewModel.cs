using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using ConnectomeViz.Helpers;

namespace ConnectomeViz.Controllers
{
    [RequiresSSL]
    public class UserFormViewModel
    {
        public string[] AllRoles { get; private set; }
        public string[] UserRoles { get; private set; }
        public MembershipUser User { get; private set; }
        public ViewMode Mode { get; private set; }

        #region constructors 

        public UserFormViewModel(string[] allRoles, string[] userRoles, MembershipUser user, ViewMode mode)
        {
            AllRoles = allRoles;
            UserRoles = userRoles;
            User = user;
            Mode = mode;
        }

        #endregion
    }

    public enum ViewMode
    {
        List, Create, Edit, Update, Delete, Details
    }
}
