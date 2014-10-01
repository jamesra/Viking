using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using ConnectomeViz.Helpers;
using ConnectomeViz.Models;
namespace ConnectomeViz.Controllers
{
    //[RequiresSSL]
    public class DefaultController : Controller
    {
        //
        // GET: /Default/

        public ActionResult Index()
        {
            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;

            State.ReadServices();

            return View();
        }

        public ActionResult CleanUp()
        {
           
            return new EmptyResult();
        }
    }
}
