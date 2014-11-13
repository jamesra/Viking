#region references

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

#endregion

namespace ConnectomeViz.Helpers
{
    public class AuthorizeControlPoint : AuthorizeAttribute
    {
        public string ControlPoint { get; set; }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            
        }
    }
}
