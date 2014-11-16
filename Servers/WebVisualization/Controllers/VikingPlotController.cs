using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using AnnotationVizLib.AnnotationService;
using System.IO;
using System.Diagnostics;
using System.Web.Security;
using ConnectomeViz.Models;
using System.Net;
using ConnectomeViz.ObjectFileService;
using System.Xml.Linq;
using System.Xml;
using System.Security.Principal;
using System.Security;
using System.Text.RegularExpressions;

namespace ConnectomeViz.Controllers
{
    //[Authorize]
    public class VikingPlotController : Controller
    {
        //
        // GET: /VikingPlot/

        [Authorize]
        public ActionResult Index()
        {
            MembershipUser user = Membership.GetUser(HttpContext.User.Identity.Name);

            ViewData["className"] = State.className;

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;

            State.ReadServices();

            if (!user.IsApproved)
                return RedirectToAction("Index", "Default");
            return View();
        }

        public void servicesUpdate()
        {
            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;

            State.ReadServices();

        }

        public ActionResult Plot()
        {
            MembershipUser user = Membership.GetUser(HttpContext.User.Identity.Name);

            ViewData["className"] = State.className;

            servicesUpdate();

            if (!user.IsApproved)
                return RedirectToAction("Index", "Default");
            return View();
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Plot3D(string query)
        {

            MembershipUser user = Membership.GetUser(HttpContext.User.Identity.Name);
            if (!user.IsApproved)
                return RedirectToAction("Index", "Default");

            State.redirected = true;

            State.redirectedQuery = query;

            return RedirectToAction("Index", "VikingPlot");
            //string structureids;


            //if (!String.IsNullOrEmpty(Request["ctl00$MainContent$structureIDs"]) && Int32.Parse(Request["ctl00$MainContent$structureIDs"]) != 0)
            //{
            //    structureids = Request["ctl00$MainContent$structureIDs"];
            //    structureids = structureids.Replace(" ", string.Empty);
            //}
            //else
            //{
            //    structureids = Request["cellids"];
            //    structureids.TrimStart();
            //}

            //ViewData["className"] = State.className;

            ////if (State.className.Equals("Mouse.class"))
            ////{
            ////    State.className = "Mouse1.class";

            ////    ViewData["className"] = State.className;

            ////}
            ////else
            ////{
            ////    State.className = "Mouse.class";

            ////    ViewData["className"] = State.className;

            ////}

            //ViewData["structureids"] = structureids;

            //char[] delimiters = { ',', '-', ' ', '.', '|' };
            //string[] structureIds = structureids.Split(delimiters);


            //string applicationPath = HttpContext.Request.ApplicationPath;
            //if (applicationPath == "/")
            //    applicationPath = "";

            //string workDirectory = Server.MapPath("~");
            //ViewData["workDirectory"] = workDirectory;

            //string virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;
            //ViewData["virtualRoot"] = virtualRoot;


            //string fileLocation = workDirectory + "\\Files\\asp.flex.communication.obj";

            //string webLocation = virtualRoot + "/Files/teapot.obj";

            //string userName = null;

            //if (String.IsNullOrEmpty(HttpContext.User.Identity.Name) || HttpContext.User.Identity.Name == "")
            //    userName = "Anonymous";
            //else
            //    userName = HttpContext.User.Identity.Name;

            //string fileDir = workDirectory + "\\Files\\" + userName;

            //if (!System.IO.Directory.Exists(fileDir))
            //    System.IO.Directory.CreateDirectory(fileDir);

            //string filepath = fileDir + "\\" + State.VikingPlotFileName;

            //if (System.IO.File.Exists(filepath))
            //{
            //    System.IO.File.Delete(filepath);
            //}

            //FileStream fs = new FileStream(filepath, FileMode.Create, FileAccess.Write);
            //StreamWriter sw = new StreamWriter(fs);
            //sw.Write("<StructureID>");

            //sw.Write("http://" + HttpContext.Request.Url.Authority + "/ObjectFiles/" + String.Join("_", structureIds).ToString() + ".obj");
            //sw.Write("</StructureID>");
            //sw.Flush();
            //sw.Close();
            //fs.Close();


            //// This part is where I communicate with the webservice and send a message to flex
            //List<long> LocationIds = new List<long>(Array.ConvertAll<string, long>(structureIds, delegate(string s) { return Convert.ToInt32(s); }));


            //AnnotateLocationsClient client = new AnnotateLocationsClient();

            //Process p = new Process();
            //p.StartInfo.FileName = "matlab.exe";
            //p.StartInfo.Arguments = "VikingPlot";
            //p.StartInfo.UseShellExecute = false;
            //p.Start();
            //p.WaitForExit();         


            //AnnotationService.Location[] results = client.GetLocationsByID(LocationIds.ToArray());


            //try
            //{
            //    FileStream fr = new FileStream(workDirectory + "\\Files\\teapot.obj", FileMode.Open, FileAccess.Read);
            //    StreamReader sr = new StreamReader(fr);
            //    var buffer = sr.ReadToEnd();

            //    FileStream fs = new FileStream(fileLocation, FileMode.Create, FileAccess.ReadWrite);
            //    StreamWriter sw = new StreamWriter(fs);
            //    sw.Write(buffer);
            //    sw.Close();
            //    fs.Close();
            //}
            //catch (Exception e)
            //{
            //    Debug.Write(e);
            //    return Content("fail" + webLocation);
            //}

            //return Content("success," + webLocation);
        }

        [Authorize]
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult GetObjectFile(string query)
        {

            servicesUpdate();

            ViewData["sentQuery"] = query;
            

            ServicePointManager.ServerCertificateValidationCallback +=
                             (sender, cert, chain, sslPolicyErrors) => true;

            if (State.redirected)
            {
                
                query =State.redirectedQuery.ToString();
                State.redirected = false;
                State.redirectedQuery = "";

            }

            string[] queryParams = query.Split(',');

            ViewData["sentQueryDatabase"] = queryParams[1];

            ViewData["sentQueryServer"] = queryParams[0];

            //return Content(GetObject(queryParams[0], queryParams[1], queryParams[2], queryParams[3], queryParams[4] == "true" ? true : false).ToString());
            ObjectFileService.ObjServiceClient client = new ObjectFileService.ObjServiceClient();

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            string virtualUserRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath + "/Files/" + HttpContext.User.Identity.Name + "/";

            //Map working directory
            string workingDirectory = Server.MapPath("~");
            string outputPath = workingDirectory + "\\Files\\" + HttpContext.User.Identity.Name + "\\";

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            string globalPath = workingDirectory + "\\Files\\Global" + "\\";
            State.globalPath = globalPath;

            if (String.IsNullOrEmpty(queryParams[2]))
            {
                return Content("sorry");
            }

            string idString = queryParams[2];

            ViewData["sentQueryIDs"] = queryParams[2];

            if (idString.Contains(' ') || idString.Contains(','))
            {
                string[] nos = idString.Split(' ');
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach (string cell in nos)
                {
                    if (!String.IsNullOrEmpty(cell.Replace(" ", "")))
                        sb.Append(cell + ",");
                }
                idString = sb.ToString().Substring(0, sb.Length - 1);
            }

            else if (queryParams[4] != "0")
            {
                idString = getCellHop(Convert.ToInt32(idString.Split(' ')[0].Trim()), Convert.ToInt32(queryParams[4]));
            }


            string response = "problem"; /* client.GetObject(State.serverDictionaryREMOVELATER[queryParams[0]], State.databaseDictionary[queryParams[1]], idString,
                 queryParams[3], queryParams[5] == "true" ? true : false, virtualUserRoot, globalPath, outputPath).ToString();
            */
            if (response.Equals("problem"))
                return Content("problem");
            else
            {
                return Content(response);
            }

            //client.Close();

            // //Map the virtual path of application

            // //MvcApplication1.Controllers.AccountController temp = new MvcApplication1.Controllers.AccountController();

            // //var result = temp.Authenticate("Anonymous", "connectome");

            // var str = HttpContext.Request.UserAgent;

            // var st2r = HttpContext.Request.LogonUserIdentity.Groups;





            // string[] cells = number.Trim().Split(' ');
            // List<string> missingCells = new List<string>();

            // string virtualRoot = "http://" + HttpContext.Request.Url.Authority + "/ObjectFiles/";




            // foreach (string cell in cells)
            // {
            //     string filepath = virtualRoot + cell + ".obj";
            //     HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(new Uri(filepath));
            //     try
            //     {
            //         HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
            //         if (webResponse.StatusCode != HttpStatusCode.NotFound)
            //             continue;
            //     }
            //     catch (WebException e)
            //     {
            //         missingCells.Add(cell);
            //     }

            // }

            // if (missingCells.Count == 0)
            // {

            //     System.Text.StringBuilder sb = new System.Text.StringBuilder();

            //     foreach (string cell in cells)
            //     {
            //         sb.Append(virtualRoot + cell + ".obj" + ",");
            //     }

            //     string ans = sb.ToString();

            //     return Content(ans.Substring(0, ans.Length - 1));

            // }

            // // There are some missing cells then, create the appropriate files

            // List<ProcessStartInfo> startInfos = new List<ProcessStartInfo>();

            // foreach (string cell in missingCells)
            // {

            //     ProcessStartInfo startInfo = new ProcessStartInfo(@"E:\src\VikingPlot\VikingPlot.exe");

            //     startInfo.RedirectStandardInput = true;

            //     startInfo.UseShellExecute = false;

            //     startInfo.WorkingDirectory = @"E:\src\VikingPlot\";

            //     startInfo.CreateNoWindow = true;

            //     startInfo.RedirectStandardOutput = true;

            //     startInfo.RedirectStandardError = true;

            //     startInfo.Arguments = " " + cell + " -RenderMode 0 -ObjPath "+ Server.MapPath("~") + "\\Files\\ObjectFiles\\";

            //     startInfo.RedirectStandardOutput = true;

            //     startInfos.Add(startInfo);

            //}


            // List<Process> processes = new List<Process>();

            // foreach (ProcessStartInfo startinfo in startInfos)
            // {

            //     Process p = new Process();

            //     p = Process.Start(startinfo);


            //     p.WaitForExit();

            //     processes.Add(p);

            //     p.Close();

            // }

            // missingCells.RemoveRange(0, missingCells.Count);

            // foreach (string cell in cells)
            // {
            //     string filepath = virtualRoot + cell + ".obj";
            //     HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(new Uri(filepath));
            //     try
            //     {
            //         HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
            //         if (webResponse.StatusCode != HttpStatusCode.NotFound)
            //             continue;
            //     }
            //     catch (WebException e)
            //     {
            //         missingCells.Add(cell);
            //     }

            // }

            // if (missingCells.Count == 0)
            // {

            //     System.Text.StringBuilder sb = new System.Text.StringBuilder();

            //     foreach (string cell in cells)
            //     {
            //         sb.Append(virtualRoot + cell + ".obj" + ",");
            //     }

            //     string ans = sb.ToString();

            //     return Content(ans.Substring(0, ans.Length - 1));

            // }

            // return Content("problem");
        }

        public string GetObject(string server, string database, string cell, string type, Boolean update = false) // type could be.obj or .dae
        {

            type = "." + type;

            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex(@"[ ]{2,}", options);
            cell = regex.Replace(cell, @" ");



            string[] cells = cell.Trim().Split(' ');

            
            List<string> missingCells = new List<string>();

            string objPath = @"E:\ObjectFiles\";
            string colladaPath = @"E:\ColladaFiles\";

            string colladaLink = "http://connectomes.utah.edu/ColladaFiles/";
            string objLink = "http://connectomes.utah.edu/ObjectFiles/";



            string writePath = colladaPath;
            string returnPath = colladaLink;

            if (type == ".obj")
            {
                writePath = objPath;
                returnPath = objLink;
            }



            foreach (string id in cells)
            {
                string filepath = writePath + "\\" + id + "\\" + id + type;
                if (System.IO.File.Exists(filepath) && update == false)
                {
                    continue;
                }
                missingCells.Add(id);
            }

            if (missingCells.Count == 0)
            {

                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach (string id in cells)
                {
                    sb.Append(returnPath + "/id/" + id + type + ",");
                }

                string ans = sb.ToString();

                return (ans.Substring(0, ans.Length - 1));

            }

            // There are some missing cells then, create the appropriate files

            Dictionary<string, ProcessStartInfo> startInfos = new Dictionary<string, ProcessStartInfo>();

            foreach (string id in missingCells) // for each cell that needs an obj created
            {

                Process proc = new Process();

                //WindowsIdentity identity = new WindowsIdentity("shoeb");
                //WindowsImpersonationContext context = identity.Impersonate();


                proc.StartInfo.FileName = @"E:\src\VikingPlotNew\VikingPlot.exe";
                var passwd = new SecureString();
                passwd.AppendChar('r');
                passwd.AppendChar('e');
                passwd.AppendChar('s');
                passwd.AppendChar('e');
                passwd.AppendChar('t');
                passwd.AppendChar('1');
                passwd.AppendChar('2');
                passwd.AppendChar('3');

                proc.StartInfo.UserName = "shoeb";
                proc.StartInfo.Password = passwd;
                proc.StartInfo.Verb = "runas";

                //// set up output redirection
                //proc.StartInfo.RedirectStandardOutput = true;
                //proc.StartInfo.RedirectStandardError = true;
                //proc.StartInfo.UseShellExecute = false;
                //proc.EnableRaisingEvents = true;
                //proc.StartInfo.CreateNoWindow = true;
                //proc.StartInfo.RedirectStandardOutput = true;
                //proc.StartInfo.RedirectStandardError = true;
                //proc.StartInfo.Verb = "runas";
                //// see below for output handler
                //proc.ErrorDataReceived += proc_DataReceived;
                //proc.OutputDataReceived += proc_DataReceived;           




                string outputType = "-ColladaPath";
                if (type == ".obj")
                    outputType = "-ObjPath";

                proc.StartInfo.Arguments = " " + id + " -Server " + server + " -Database " + database + " -RenderMode 0 " + outputType + " " + writePath + id + "\\";

                DeleteDirectory(writePath + id + "\\");


                Directory.CreateDirectory(writePath + id);

                proc.StartInfo.UseShellExecute = false;
                proc.Start();
                proc.WaitForExit();

                //string user = "shoeb";
                //char[] myPassword = "reset123".ToCharArray();
                //SecureString blah = new SecureString();
                //for (int i = 0; i < myPassword.Length; i++)
                //{
                //    blah.AppendChar(myPassword[i]);
                //}

                //startInfo.UserName = user;

                //startInfo.Password = blah;







                //Process p = new Process();


                ////p.EnableRaisingEvents = true;

                ////p.OutputDataReceived += build_ErrorDataReceived;
                ////p.ErrorDataReceived += build_ErrorDataReceived;



                //p = Process.Start(startInfo);


                ////p.BeginOutputReadLine();
                ////p.BeginErrorReadLine();

                //Console.WriteLine(p.StandardOutput.ReadToEnd());       

                //p.WaitForExit();

                //if (type == ".dae")
                //    createFinalCollada(writePath, process.Key);

            }

            missingCells.RemoveRange(0, missingCells.Count);

            foreach (string id in cells)
            {
                string filepath = writePath + id + "\\" + id + type;
                if (System.IO.File.Exists(filepath))
                {
                    continue;
                }
                missingCells.Add(id);
            }

            if (missingCells.Count == 0)
            {

                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach (string id in cells)
                {
                    sb.Append(returnPath + "/id" + id + type + ",");
                }

                string ans = sb.ToString();

                return (ans.Substring(0, ans.Length - 1));

            }

            return "problem";
        }

        public EventHandler process_Exited { get; set; }

        static void build_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            string strMessage = e.Data;

            Console.WriteLine(e.Data);
        }


        private void createFinalCollada(string writePath, string id)
        {
            string pathInQuestion = writePath + id + "\\";



            var xmlSource = XDocument.Parse(pathInQuestion + id + ".dae");

            XDocument finalXML = null;

            foreach (string file in Directory.GetFiles(pathInQuestion))
            {
                finalXML = (XDocument)xmlSource.Descendants("Collada").Union(XDocument.Parse(file).Descendants("Collada"));
                System.IO.File.Delete(file);
            }

            XmlWriter writeXML = XmlWriter.Create(pathInQuestion + id + ".xml");
            finalXML.WriteTo(writeXML);
            writeXML.Flush();
            writeXML.Close();

            System.IO.File.Move(pathInQuestion + id + ".xml", pathInQuestion + id + ".dae");

            //Combine and remove duplicates

        }

        void RunProcessWithRedirect(ProcessStartInfo startInfo)
        {
            Process proc = new Process();

            proc.StartInfo = startInfo;

            proc.EnableRaisingEvents = true;

            proc.ErrorDataReceived += proc_DataReceived;
            proc.OutputDataReceived += proc_DataReceived;

            proc.Start();

            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            proc.WaitForExit();
        }

        void proc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data.ToString());
        }


        public static bool DeleteDirectory(string target_dir)
        {
            if (!Directory.Exists(target_dir))
                return true;

            bool result = false;

            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                System.IO.File.SetAttributes(file, FileAttributes.Normal);
                System.IO.File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);

            return result;
        }

        public ActionResult GetID()
        {

            //Map the virtual path of application
            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";

            string workDirectory = Server.MapPath("~");

            string virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string userName = HttpContext.User.Identity.Name;

            userName = "shoeb";

            string filepath = virtualRoot + "/Files/" + userName + "/" + State.VikingPlotFileName;

            return Content(filepath);

        }

        public string getCellHop(int cellID, int hops)
        {



            CircuitClient networkClient = State.CreateNetworkClient();
            networkClient.Open();

            //string s = networkClient.GetWorkingResponse(10);


            Graphx graphx = networkClient.getGraph(cellID, hops+1);


            List<Edge> tmpEdgeList = new List<Edge>();

            foreach (Edgex edgex in graphx.EdgeList)
            {
                Edge tmp = new Edge(edgex.SourceID, edgex.TargetID, edgex.SourceParentID, edgex.TargetParentID, edgex.Link, edgex.SourceTypeName, 0.0f);
                tmpEdgeList.Add(tmp);
            }

            NetworkGraph graph = new NetworkGraph(tmpEdgeList, graphx.NodeList, graphx.InvolvedCells.ToList<long>(), graphx._FrontierNodes.ToList<long>(),
                            graphx.FrontierNodes.ToList<long>(), graphx.ReducedEdges.ToList<long>(), graphx.locationInfo, graphx.zLocationForSynapses);


            Dictionary<string, string> colorTable = new Dictionary<string, string>();
            colorTable.Add("ribbon synapse", "chartreuse4");
            colorTable.Add("conventional", "red3");
            colorTable.Add("bc conventional", "chartreuse4");
            colorTable.Add("gap junction", "goldenrod4");
            colorTable.Add("unknown", "gray50");
            colorTable.Add("frontier", "white");

           
        
            List<object> edgesJson = new List<object>();  

            foreach (Edge edge in graph.EdgeList)
            {

                if (false == graph.NodeList.ContainsKey(edge.SourceParentID) ||
                    false == graph.NodeList.ContainsKey(edge.TargetParentID))
                {

                }
                else
                {
                    if (false == graph.InvolvedCells.Contains(edge.SourceParentID))
                    {
                        graph.InvolvedCells.Add(edge.SourceParentID);
                    }

                    if (false == graph.InvolvedCells.Contains(edge.TargetParentID))
                    {
                        graph.InvolvedCells.Add(edge.TargetParentID);
                    }


                }
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (long nodeID in graph.InvolvedCells)
            {

                sb.Append(nodeID + ",");

            }

            return sb.ToString().Substring(0, sb.Length - 1);
           
        }

    }
}
