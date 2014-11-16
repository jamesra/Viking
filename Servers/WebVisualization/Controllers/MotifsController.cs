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
using AnnotationVizLib.AnnotationService;
using System.Xml;
using System.Web.Script.Serialization;

using ConnectomeViz.Helpers;
using ConnectomeViz;
using AnnotationVizLib;


namespace ConnectomeViz.Controllers
{
    public class MotifGraphRequestData
    { 
        public bool RefreshGraph = false;
        public bool ReduceEdges = true;
        public long[] CellIDs;

        public string Server;
        public string Volume;

        private static bool IsCheckboxSet(string val)
        {
            return !String.IsNullOrEmpty(val);
        }

        private static long[] GetRequestCellIDs(HttpRequestBase Request)
        {
            try
            {
                if (!String.IsNullOrEmpty(Request["ctl00$MainContent$NetworkInterface$structureID"]) && Int32.Parse(Request["ctl00$MainContent$NetworkInterface$NetworkInterface$structureID"]) != 0)
                {
                    return new long[] { Convert.ToInt64(Request["ctl00$MainContent$structureID"]) };
                }
                else
                {
                    return new long[] { Convert.ToInt64(Request["cellID"]) };
                }
            }
            catch (Exception e)
            {
                return new long[0]; 
            }
        }

        public static MotifGraphRequestData Create(HttpRequestBase Request)
        {
            MotifGraphRequestData data = new MotifGraphRequestData();
            
            data.Volume = Request["ctl00$MainContent$EndpointSelector$volumeList"];
            data.Server = Request["ctl00$MainContent$EndpointSelector$serverList"];

            data.CellIDs = GetRequestCellIDs(Request);

            data.RefreshGraph = IsCheckboxSet(Request["freshQuery"]);
            data.ReduceEdges = IsCheckboxSet(Request["reduceEdges"]);

            return data;
        }

        public string FileName
        {
            get
            {
                string FileName = Server + "_" + Volume + "_Motifs";

                if (CellIDs != null)
                {
                    if (CellIDs.Length > 0)
                    {
                        FileName += "_" + CellIDs[0];
                    }
                }

                if (ReduceEdges)
                {
                    FileName += "_EdgeMerge";
                }
                 
                return FileName;
            }
        }
    }

    [HandleError]
    [Authorize]
    public class MotifsController : Controller
    {
        public SortedDictionary<long, StructureType> StructureTypesDictionary = new SortedDictionary<long, StructureType>();

        SortedList<string, List<Structure>> LabelToStructures = new SortedList<string, List<Structure>>(); 
        
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
            State.ReadServices();
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
        [AcceptVerbs(HttpVerbs.Post), ActionName("OnGetDOTRequest")]
        public ActionResult OnGetDOTRequest()
        {
            MotifGraphRequestData graph_data = MotifGraphRequestData.Create(Request);
            GraphRequestPathData path_data = GraphRequestPathData.Create(this.HttpContext, Server);
            string userDotFileFullPath = path_data.UserFileFullPath(graph_data.FileName, ".dot"); 

            if (!System.IO.File.Exists(userDotFileFullPath) || graph_data.RefreshGraph)
            {
                State.ReadServices();
                MotifGraph motifGraph = MotifGraph.BuildGraph(State.SelectedEndpoint, State.userCredentials);
                MotifDOTView DotGraph = MotifDOTView.ToDOT(motifGraph);
                DotGraph.SaveDOT(userDotFileFullPath);  
            }

            //System.Net.Mime.ContentDisposition cd = new System.Net.Mime.ContentDisposition();

            return File(userDotFileFullPath, "text/plain", graph_data.FileName + ".dot");
             
        }


        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Trace()
        {
            //Render all labels matching the specified pattern

            //Map working directory
            string workingDirectory = Server.MapPath("~");
            ViewData["workDirectory"] = workingDirectory;
            State.filesPath = workingDirectory;
            State.ReadServices(); 

            if (Request["OutputType"] == "dot")
            {
                return OnGetDOTRequest();
            }

            MotifGraphRequestData graph_data = MotifGraphRequestData.Create(Request);
            GraphRequestPathData path_data = GraphRequestPathData.Create(HttpContext, Server); 

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

            //Map working directory
             
            
             
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
