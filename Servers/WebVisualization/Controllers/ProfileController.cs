using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using ConnectomeViz.Helpers;
namespace ConnectomeViz.Controllers
{
    [RequiresSSL]
    public class ProfileController : Controller
    {
        //
        // GET: /Profile/

        public ActionResult Index()
        {
            return RedirectToAction("Manage");
        }

        public ActionResult Manage()
        {
            return View();
        }

    }
}
