using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ConnectomeViz.Models;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.Security;
using System.ServiceModel;
using System.Reflection;
using AnnotationUtils.AnnotationService;
using System.Xml;
using System.Web.Script.Serialization;

using ConnectomeViz.Helpers;
using AnnotationUtils;


namespace ConnectomeViz.Controllers
{
    [HandleError]
    [Authorize]
    public class MotifsController : Controller
    {
        public SortedDictionary<long, StructureType> StructureTypesDictionary = new SortedDictionary<long, StructureType>();

        SortedList<string, List<Structure>> LabelToStructures = new SortedList<string, List<Structure>>(); 

        Boolean fixedNodes = false;

        Boolean freshQuery = false;

        //Display the Index page of the website
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Index()
        {
            MembershipUser user = Membership.GetUser(HttpContext.User.Identity.Name);

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;

            if (!user.IsApproved)
                return RedirectToAction("Index", "Default");

            State.ReadServices();

            return View();
        }          
      
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Trace(string id)
        {

            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Motif");

            //int cellid = Int32.Parse(id);
            //Call draw graph
            //CreateMotifDiagram(id, );

            ViewData["cellID"] = Int32.Parse(id);

            State.graphType = "generate";

            return View("Trace");

        }

      
        /*******************
        * For POST Requests
        ********************/     
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Trace()
        {
            //Render all labels matching the specified pattern

            string labelPattern;

            if (!String.IsNullOrEmpty(Request["ctl00$MainContent$labelPattern"]))
            {
                labelPattern = Request["ctl00$MainContent$labelPattern"];
            }
            else 
            {
                labelPattern = Request["labelPatternList"];
            }


            State.selectedVolume = Request["ctl00$MainContent$EndpointSelector$volumeList"];
            State.selectedServer = Request["ctl00$MainContent$EndpointSelector$serverList"];
            ViewData["dataSource"] = State.selectedVolume;
            ViewData["lab"] = State.selectedServer;


            State.graphType = Request["group1"];
              
            ViewData["dataSource"] = State.selectedVolume;

            ViewData["lab"] = State.selectedServer;

            int hops = Convert.ToInt32(Request["hops"]);

            ViewData["numHops"] = hops;

            string selection = Request["reduceEdges"];

            String fresh = Request["freshQuery"];

            if (String.IsNullOrEmpty(fresh))
                freshQuery = false;
            else
                freshQuery = true;

            bool reduceEdges = true;

            try
            {
                if (selection.Length > 0)
                    reduceEdges = true;
            }
            catch
            {
                reduceEdges = false;
            }

            //Map working directory
            string workingDirectory = Server.MapPath("~");
            ViewData["workDirectory"] = workingDirectory;

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            string virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;
            ViewData["virtualRoot"] = virtualRoot; 
            ViewData["labelPattern"] = labelPattern;

            String[] fileTypes = { "svg" };

            string userPath = workingDirectory + "\\Files\\" + HttpContext.User.Identity.Name + "\\";
            ViewData["outputPath"] = userPath;
            ViewData["username"] = HttpContext.User.Identity.Name;

            string globalPath = workingDirectory + "\\Files\\Global" + "\\";
            ViewData["globalPath"] = globalPath; 

            State.globalPath = globalPath;

            string reduce = "reduced"; 

            if (!reduceEdges)
                reduce = "unreduced";

            string Layout = Request["layout"]; 

            string DotFileName = "AllMotifs"; 

            ViewData["modifiedFileName"] = DotFileName;
            State.userFileName = DotFileName;

            string userFileFullPath = userPath + DotFileName;
            State.userFile = userFileFullPath;
            ViewData["userfile"] = userFileFullPath;

            string userDotFileFullPath = userFileFullPath + ".dot"; 

            string userURL = virtualRoot + "/Files/" + HttpContext.User.Identity.Name + "/" + DotFileName;
            ViewData["SVGFile"] = DotFileName + "_" + Layout;
            ViewData["userURL"] = virtualRoot + "/Files/" + HttpContext.User.Identity.Name + "/" + DotFileName + "_" + Layout;
            State.userURL = userURL;
             
            string GlobalFileFullPath = globalPath + DotFileName;

            if (!Directory.Exists(globalPath))
                Directory.CreateDirectory(globalPath);

            if (!System.IO.Directory.Exists(userPath))
                System.IO.Directory.CreateDirectory(userPath);

            string DotFileFullPath = userFileFullPath + ".dot"; 
            string JSONFileFullPath = userFileFullPath + ".json";

            if (!System.IO.File.Exists(DotFileFullPath) ||
                !System.IO.File.Exists(JSONFileFullPath) ||
                freshQuery)
            {
                MotifGraph motifGraph = MotifGraph.BuildGraph(State.SelectedEndpoint, State.userCredentials);
                MotifDOTView DotGraph = MotifDOTView.ToDOT(motifGraph);
                DotGraph.SaveDOT(DotFileFullPath);  

                MotifJSONView JSONView = MotifJSONView.ToJSON(motifGraph);
                JSONView.SaveJSON(JSONFileFullPath); 
            }
              
            State.networkFreshQuery = true; 
           
            IList<string> OutputFileList = MotifDOTView.Convert(Layout + ".exe", userDotFileFullPath, fileTypes); 
              
            foreach( string outputFile in  OutputFileList)
            {
                string ext = System.IO.Path.GetExtension(outputFile);
                if (ext.ToLower() != "svg")
                {
                    string userSvgFile = outputFile;
                    if (System.IO.File.Exists(userSvgFile))
                    {
                        SVG.InjectSVGViewer(userSvgFile, virtualRoot); 
                  
                        string globalSvgFile = globalPath + System.IO.Path.GetFileName(outputFile);

                        if (System.IO.File.Exists(globalSvgFile))
                        {
                            if(System.IO.File.GetLastWriteTime(globalSvgFile) < System.IO.File.GetLastWriteTime(userDotFileFullPath))
                                System.IO.File.Delete(globalSvgFile);
                        }

                        if (!System.IO.File.Exists(globalSvgFile))
                            System.IO.File.Copy(userSvgFile, globalSvgFile);
                    }
                }
            }

            return View("Trace"); 
        }  

        void BuildSingleLabelMotif(string label)
        {
            List<long> structureIDList = new List<long>();
            foreach (Structure s in this.LabelToStructures[label])
            {
                structureIDList.Add(s.ID); 
            }
        }

        //Contains list of all structureIDs of the last hop
        List<long> ActualNodes = new List<long>();
    }
}
