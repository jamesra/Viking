#region references
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using ConnectomeViz.Helpers;
#endregion

namespace ConnectomeViz.Controllers
{
    [Authorize]
    [RequiresSSL]

    public class AdminController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
