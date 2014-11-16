using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using ConnectomeViz.Models;
using System.Web.Security;
using AnnotationVizLib.AnnotationService;

namespace ConnectomeViz.Controllers
{


    [Authorize]
    public class StatsController : Controller
    {
        //
        // GET: /Stats/

        public ActionResult Index()
        {
            MembershipUser user = Membership.GetUser(HttpContext.User.Identity.Name);

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;
            ViewData["virtualRoot"] = State.virtualRoot;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;


            if (!user.IsApproved)
                return RedirectToAction("Index", "Default");

            //State.ReadServices();
            State.ReadServices();         

            return View();
        }

        public ActionResult Visualize()
        {
            return View();
        }

        public ActionResult StatsJSON(string lab, string dataSource)
        {
            State.selectedServer = lab.ToString().Trim();
            State.selectedVolume = dataSource.ToString().Trim();

            CircuitClient client = State.CreateNetworkClient();
            client.Open();
            SynapseObject retObj = client.getSynapseStats();

            List<SendObject> send = new List<SendObject>();
           
            foreach (var row in retObj.objList)
            {
                SendObject temp = new SendObject();
                temp.id = row.id;
                temp.synapses = row.synapses;
                send.Add(temp);
            }

            client.Close();

            return Json(send, JsonRequestBehavior.AllowGet);

        }
       

    }

    class SendObject
    {
        public string id {get;set;}
        public string[] synapses { get; set; }
    }
}
