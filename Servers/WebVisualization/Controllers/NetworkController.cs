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
using System.Xml;
using System.Web.Script.Serialization;
using AnnotationUtils;
using AnnotationUtils.AnnotationService;
using ConnectomeViz;


namespace ConnectomeViz.Controllers
{
    public class NetworkGraphRequestData
    {
        public int NumHops = 1;
        public bool RefreshGraph = false;
        public bool ReduceEdges = true;
        public bool ShowExtraHop = false;
        public bool PinNodePosition = false;
        public long[] CellIDs;

        public string Server;
        public string Volume;

        private static bool IsCheckboxSet(string val)
        {
            return !String.IsNullOrEmpty(val);
        }

        private static long[] GetRequestCellIDs(HttpRequestBase Request)
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

        public static NetworkGraphRequestData Create(HttpRequestBase Request)
        {
            NetworkGraphRequestData data = new NetworkGraphRequestData();
            data.NumHops = Convert.ToInt32(Request["hops"]);
            data.RefreshGraph = IsCheckboxSet(Request["freshQuery"]);
            data.ReduceEdges = IsCheckboxSet(Request["reduceEdges"]);
            data.ShowExtraHop = IsCheckboxSet(Request["showExtraHop"]);
            data.PinNodePosition = IsCheckboxSet(Request["pinNodes"]);
            data.CellIDs = GetRequestCellIDs(Request);

            data.Volume = Request["ctl00$MainContent$NetworkInterface$EndpointSelector$volumeList"];
            data.Server = Request["ctl00$MainContent$NetworkInterface$EndpointSelector$ServerList"];

            return data;
        }

        public string FileName
        {
            get
            {
                string FileName = Server + "_" + Volume + "_" + CellIDs[0] + "_Hops" + NumHops.ToString();

                if (ReduceEdges)
                {
                    FileName += "_EdgeMerge";
                }

                if (PinNodePosition)
                {
                    FileName += "_WithPos";
                }

                if (ShowExtraHop)
                {
                    FileName += "_ShowGhost";
                }

                return FileName;
            }
        }
    }

    [HandleError]
    [Authorize]
    public class NetworkController : Controller
    {
        public SortedDictionary<long, StructureType> StructureTypesDictionary = new SortedDictionary<long, StructureType>();

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
            State.ReadServices();
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Network");

            //int cellid = Int32.Parse(id);
            //Call draw graph
            CreateNetworkDiagram(Int32.Parse(id), 3, true);

            ViewData["cellID"] = Int32.Parse(id);

            State.graphType = "generate";

            return View("Trace"); 
        }

        public bool IsCheckboxSet(string val)
        {
            return !String.IsNullOrEmpty(val);
        }
         
        [AcceptVerbs(HttpVerbs.Post), ActionName("OnGetDOTRequest")]
        public ActionResult OnGetDOTRequest()
        {
            NetworkGraphRequestData graph_data = NetworkGraphRequestData.Create(Request);
            GraphRequestPathData path_data = GraphRequestPathData.Create(this.HttpContext, Server);
            string userDotFileFullPath = path_data.UserFileFullPath(graph_data.FileName, ".dot"); 

            if(!System.IO.File.Exists(userDotFileFullPath) || graph_data.RefreshGraph)
            {
                State.ReadServices();
                NeuronGraph graph = AnnotationUtils.NeuronGraph.BuildGraph(graph_data.CellIDs, graph_data.NumHops, State.SelectedEndpoint, State.userCredentials);
                  
                NeuronDOTView dotGraph = NeuronDOTView.ToDOT(graph, graph_data.ShowExtraHop); 
                dotGraph.SaveDOT(userDotFileFullPath);
            }

            //System.Net.Mime.ContentDisposition cd = new System.Net.Mime.ContentDisposition();

            return File(userDotFileFullPath, "text/plain", graph_data.FileName + ".dot");
            
            /*
            FileInfo file = new FileInfo(userDotFileFullPath); 
            Response.Clear();
            Response.AddHeader("Content-Disposition", "attachment; filename=" + file.Name + ".dot");
            Response.AddHeader("Content-Length", file.Length.ToString());
            Response.ContentType = "plain/txt";
            Response.Flush();
            Response.TransmitFile(file.FullName);
            Response.End(); 
             */
        }
      
        /*******************
        * For POST Requests
        ********************/
        [AcceptVerbs(HttpVerbs.Post), ActionName("Trace")]
        public ActionResult Trace()
        {
            //Create structureTypes dictionary
            State.graphType = Request["OutputType"];

            if (Request["OutputType"] == "dot")
            {
                return OnGetDOTRequest(); 
            }

            NetworkGraphRequestData graph_data = NetworkGraphRequestData.Create(Request);
            GraphRequestPathData path_data = GraphRequestPathData.Create(HttpContext, Server); 
             
            State.selectedVolume = Request["ctl00$MainContent$NetworkInterface$EndpointSelector$volumeList"];
            State.selectedServer = Request["ctl00$MainContent$NetworkInterface$EndpointSelector$ServerList"];
            ViewData["dataSource"] = State.selectedVolume;
            ViewData["lab"] = State.selectedServer;

            
               
            int hops = Convert.ToInt32(Request["hops"]);

            ViewData["numHops"] = hops;
            
            freshQuery = IsCheckboxSet(Request["freshQuery"]);
            bool reduceEdges = IsCheckboxSet(Request["reduceEdges"]);
            bool showExtraHop = IsCheckboxSet(Request["showExtraHop"]);

            //Map working directory
            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;
            State.ReadServices();
            ViewData["workDirectory"] = workingDirectory;

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            string virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;
            ViewData["virtualRoot"] = virtualRoot;

            ViewData["cellid"] = graph_data.CellIDs[0];
            State.networkID = (int)graph_data.CellIDs[0];

            String[] fileTypes = {"svg"};

            string outputPath = workingDirectory + "\\Files\\" + HttpContext.User.Identity.Name + "\\";
            ViewData["outputPath"] = outputPath;
            ViewData["username"] = HttpContext.User.Identity.Name;

            string globalPath = workingDirectory + "\\Files\\Global" + "\\";
            ViewData["globalPath"] = globalPath;
            State.globalPath = globalPath; 

            string Layout = "dot";

            string OutputFilenameBase = graph_data.FileName;  

            ViewData["modifiedFileName"] = OutputFilenameBase;
            State.userFileName = OutputFilenameBase;

            string userFileFullPath = outputPath + OutputFilenameBase;
            State.userFile = userFileFullPath;
            ViewData["userfile"] = userFileFullPath;

            string userDotFileFullPath = userFileFullPath + ".dot";

            string svgFilenameBase = OutputFilenameBase + "_" + Layout;
            string svgFileFullPath = outputPath + svgFilenameBase + ".svg";
            string svgUrlbase = OutputFilenameBase + "_" + Layout;
            string userURL = virtualRoot + "/Files/" + HttpContext.User.Identity.Name + "/" + svgFilenameBase;

            ViewData["SVGFile"] = svgUrlbase;
            ViewData["userURL"] = userURL;
            State.userURL = userURL;
              

            if(!System.IO.File.Exists(userDotFileFullPath) || freshQuery)
            {
                NeuronGraph graph = AnnotationUtils.NeuronGraph.BuildGraph(graph_data.CellIDs, hops, State.SelectedEndpoint, State.userCredentials);
                  
                NeuronDOTView dotGraph = NeuronDOTView.ToDOT(graph, showExtraHop); 
                dotGraph.SaveDOT(userDotFileFullPath);

                string JSONFullPath = userFileFullPath + ".json";
                NeuronJSONView JSON = NeuronJSONView.ToJSON(graph);
                JSON.SaveJSON(JSONFullPath);

                if(System.IO.File.Exists(svgFileFullPath))
                    System.IO.File.Delete(svgFileFullPath);
            }

            if (!System.IO.File.Exists(svgFileFullPath))
            {
                NeuronDOTView.Convert("dot", userDotFileFullPath, new string[] { "svg" });

                SVG.InjectSVGViewer(svgFileFullPath, virtualRoot); 
            }

            //if (Request["group1"] == "generate")
            //    return View("Trace");

            //else
            //{
            //    Response.ClearHeaders();
            //    Response.ClearContent();
            //    Response.Clear();
            //    Response.AddHeader("content-disposition", "inline;filename=" + cellid + ".svg");
            //    Response.ContentType = "image/svg+xml";
            //    Response.WriteFile(workingDirectory+ "\\Files\\"+ HttpContext.User.Identity.Name+"\\"+cellid + ".svg");
            //    Response.Flush();
            //    Response.End();

            //}

            return View("Trace");

        }

        public string CreateZLevelTooltip(NetworkGraph graph, long TargetID, long SourceID)
        {

            string newToolTipString = "";

            long? TargetZ = new long?();
            if (graph.zlocationForSynapses.ContainsKey(TargetID))
                TargetZ = new long?(graph.zlocationForSynapses[TargetID]);

            long? SourceZ = new int?();
            if (graph.zlocationForSynapses.ContainsKey(SourceID))
                SourceZ = new long?(graph.zlocationForSynapses[SourceID]);


            if (SourceZ.HasValue && TargetZ.HasValue)
            {
                if (SourceZ.Value != TargetZ.Value)
                    newToolTipString += " (z:" + TargetZ.Value + "-" + SourceZ.Value + ") ";
                else
                    newToolTipString += " (z:" + TargetZ.Value + ") ";
            }
            else if (SourceZ.HasValue)
                newToolTipString += " (z:" + SourceZ.HasValue + ") ";
            else if (TargetZ.HasValue)
                newToolTipString += " (z:" + TargetZ.Value + ") ";

            return newToolTipString; 
        }

        Graphx graphx = new Graphx();

        NetworkGraph graph;     

        /*************************************************************
         * create the main network diagram depending on number of hops
        ******************************************************************/
        public void CreateNetworkDiagram(int cellID, int hops, bool reduceEdges)
        {
            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            string virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;
            ViewData["virtualRoot"] = virtualRoot;

            //Map working directory
            string workingDirectory = Server.MapPath("~");
            ViewData["workDirectory"] = workingDirectory;
             
            ViewData["cellid"] = cellID;
            State.networkID = cellID;

            String[] fileTypes = {"svg"};

            string outputPath = workingDirectory + "\\Files\\" + HttpContext.User.Identity.Name + "\\";
            ViewData["outputPath"] = outputPath;
            ViewData["username"] = HttpContext.User.Identity.Name;

            string globalPath = workingDirectory + "\\Files\\Global" + "\\";
            ViewData["globalPath"] = globalPath;
            State.globalPath = globalPath;

            string reduce= "reduced";
            string position = "positioned";

            if(!reduceEdges)
                reduce="unreduced";
            if(!fixedNodes)
                position="nonpositioned";

            string modifiedFileName= State.selectedServer+"_"+State.selectedVolume+"_"+cellID+"_"+hops+"_"+reduce+"_"+position;
            ViewData["modifiedFileName"] = modifiedFileName;
            State.userFileName = modifiedFileName;

            string userFile = outputPath + modifiedFileName;
            State.userFile = userFile;
            ViewData["userfile"] = userFile;

            string userURL = virtualRoot + "/Files/" + HttpContext.User.Identity.Name+"/" + modifiedFileName;           
            ViewData["userURL"] = userURL;
            State.userURL = userURL;

            string storedFile = globalPath + modifiedFileName;

            if (!Directory.Exists(globalPath))
                Directory.CreateDirectory(globalPath);

            if (!System.IO.Directory.Exists(outputPath))
                System.IO.Directory.CreateDirectory(outputPath);


            State.networkFreshQuery = true;

            if (!freshQuery)
            {

                try
                {
                    if (System.IO.File.Exists(storedFile + ".svg"))
                    {


                        foreach (string type in fileTypes)
                        {
                            string destfile = userFile + "." + type;
                            if (System.IO.File.Exists(destfile))
                                System.IO.File.Delete(destfile);
                            System.IO.File.Copy(storedFile + "." + type, destfile);

                        }
                        return;
                    }

                    if (System.IO.File.Exists(storedFile + ".json"))
                    {
                        State.networkFreshQuery = false;
                    }
                    else
                        State.networkFreshQuery = true; 
                }
                catch (Exception e)
                { }

            }

            float additionFactor;
            float mulFactor;
             

            if (fixedNodes)
            {
                additionFactor = 1f;
                mulFactor = 0.5f;
            }
            else
            {
                additionFactor = 1f;
                mulFactor = 0.5f;
            }

            //Create dictionary of structure types
            webService_GetStructureTypes();

            CircuitClient networkClient = State.CreateNetworkClient();
            networkClient.Open();
                     

            Graphx graphx = networkClient.getGraph(cellID, hops + 1);

            
            List<Edge> tmpEdgeList = new List<Edge>();

            foreach (Edgex edgex in graphx.EdgeList)
            {
                Edge tmp = new Edge(edgex.SourceID, edgex.TargetID, edgex.SourceParentID, edgex.TargetParentID, edgex.Link, edgex.SourceTypeName,additionFactor);
                tmpEdgeList.Add(tmp);
            }

            graph = new NetworkGraph(tmpEdgeList, graphx.NodeList, graphx.InvolvedCells.ToList<long>(), graphx._FrontierNodes.ToList<long>(),
                            graphx.FrontierNodes.ToList<long>(), graphx.ReducedEdges.ToList<long>(), graphx.locationInfo, graphx.zLocationForSynapses);


            // Call graphviz Engine to create graph
            GraphVizEngine<long> connectionsGraph = new GraphVizEngine<long>();

            // Name the graph
            connectionsGraph.createDirectedGraph(cellID.ToString());

            string layout = "dot";

            //set graph attributes
            connectionsGraph.Attributes.Add("maxiter", "1000");

            //Scale max number of iterations according to the size of the graph
            connectionsGraph.Attributes.Add("nslimit", Math.Ceiling(Math.Sqrt(graph.NodeList.Count)).ToString());
            connectionsGraph.Attributes.Add("mclimit", Math.Ceiling(Math.Sqrt(graph.NodeList.Count)).ToString());

            connectionsGraph.Attributes.Add("splines", "true");

            connectionsGraph.Attributes.Add("size", "9,6.5");
            connectionsGraph.Attributes.Add("page", "11,8.5");
            connectionsGraph.Attributes.Add("dpi", "300"); 

            connectionsGraph.Attributes.Add("ratio", "compress");
            connectionsGraph.Attributes.Add("regular", "true"); 

            //connectionsGraph.graphAttribites.Add("packMode", "node");

            connectionsGraph.Attributes.Add("center", "true");
            //connectionsGraph.graphAttribites.Add("landscape", "true");

            connectionsGraph.Attributes.Add("minlen", "0"); 


            connectionsGraph.Attributes.Add("overlap", fixedNodes ? "true" : "false");

            if (!fixedNodes)
            {
                connectionsGraph.Attributes.Add("sep", "0.1");
                connectionsGraph.Attributes.Add("ranksep", "0.1");

                connectionsGraph.Attributes.Add("nodesep", "0.1");
                //connectionsGraph.graphAttribites.Add("overlap_scaling", "1.5");
                connectionsGraph.Attributes.Add("mode", "major");
                connectionsGraph.Attributes.Add("Model", "subset");

                //connectionsGraph.graphAttribites.Add("size", "6.5,9");
                //connectionsGraph.graphAttribites.Add("page", "8.5,11");
                //connectionsGraph.graphAttribites.Add("ratio", "compress");
                //connectionsGraph.graphAttribites.Add("mode", "major");
                //connectionsGraph.graphAttribites.Add("Model", "network");
            }
            

            //create edges in graph
            List<string> done = new List<string>();

            //If it is required to reduce edges, then remove redundant edges from edgelist, else leave it alone.
            Dictionary<string, Edge> tempEdgeList = new Dictionary<string, Edge>();

            //Key is a string describing SourceParentID->TargetParentID,Type
            //Value is the tooltip containing structureID's of the connections
            Dictionary<string, string> toolTipDictionary = new Dictionary<string, string>();
            Dictionary<string, HashSet<long>> sectionsDictionary = new Dictionary<string, HashSet<long>>();
            Dictionary<string, List<long>> subgraphDictionary = new Dictionary<string, List<long>>(); 

            if (reduceEdges)
            {
                foreach (Edge edge in graph.EdgeList)
                {
                    string keyString = edge.KeyString;
                    string reversedKeyString = "";

                    if (edge.Link.Bidirectional)
                    {
                        reversedKeyString = edge.TargetParentID +  "-" + edge.SourceParentID + "," + edge.SourceTypeName;
                    }

                    bool exists = false;
                    Edge tempEdge = null; 
                    if (tempEdgeList.ContainsKey(keyString))
                    {
                        tempEdge = tempEdgeList[keyString]; 
                        exists = true;
                    }
                    else if (edge.Link.Bidirectional && tempEdgeList.ContainsKey(reversedKeyString))
                    {
                        exists = true;
                        tempEdge = tempEdgeList[reversedKeyString]; 
                        keyString = reversedKeyString;
                    }

                    //HACK: Make sure edge is not a desmosome before adding it
                    if (graph.NodeList.ContainsKey(edge.SourceID))
                    {
                        Structure SourceStructure = graph.NodeList[edge.SourceID];
                        StructureType type = StructureTypesDictionary[SourceStructure.TypeID];
                        if (type.Name.ToLower() == "desmosome")
                            continue; 
                    }

                    string newToolTipString = edge.ConnectionString + CreateZLevelTooltip(graph, edge.TargetID, edge.SourceID); 


                    if (tempEdge == null)
                    {
                        tempEdgeList.Add(keyString, edge);
                        toolTipDictionary.Add(keyString, newToolTipString);
                        sectionsDictionary[keyString] = new HashSet<long>();
                        
                    }
                    else
                    {
                        string OldToolTipString = toolTipDictionary[keyString];
                        newToolTipString = OldToolTipString + " " + newToolTipString;
                        toolTipDictionary[keyString] = newToolTipString;
                        tempEdge.Strength = tempEdge.Strength + additionFactor;
                    }

                    if (graph.zlocationForSynapses.ContainsKey(edge.TargetID))
                        sectionsDictionary[keyString].Add(graph.zlocationForSynapses[edge.TargetID]);

                    if (graph.zlocationForSynapses.ContainsKey(edge.SourceID))
                        sectionsDictionary[keyString].Add(graph.zlocationForSynapses[edge.SourceID]);
                }

                graph.EdgeList.Clear();

                foreach (KeyValuePair<string, Edge> temp in tempEdgeList)
                    graph.EdgeList.Add(temp.Value);
            }


            Dictionary<string, string> colorTable = new Dictionary<string, string>();
            colorTable.Add("ribbon synapse", "chartreuse4");
            colorTable.Add("conventional", "red3");
            colorTable.Add("bc conventional", "chartreuse4");
            colorTable.Add("gap junction", "goldenrod4");
            colorTable.Add("unknown", "gray50");
            colorTable.Add("frontier", "white");

            //Ranks:
            // 1 = OFF BC
            // 2 = ON BC
            // 3 = AC
            // 4 = GC

            Dictionary<long, string> nodeColorTable = new Dictionary<long, string>();
            Dictionary<long, string> nodeShapeTable = new Dictionary<long, string>();
            foreach (Structure node in graph.NodeList.Values)
            {
                string label;
                if (node.Label == null || node.Label.Length == 0)
                {
                    if (StructureTypesDictionary.ContainsKey(node.TypeID))
                    {
                        StructureType type = StructureTypesDictionary[node.TypeID];
                        label = type.Name.ToUpper();
                    }
                    else
                    {
                        label = "";
                    }

                }
                else
                {
                    label = node.Label.ToUpper();
                    label = label.Trim(); 
                }
                
                //Assign nodes with the same labels (minus brackets) to the same subgraph
                int firstBracket = label.IndexOf('[');
                if (firstBracket >= 0)
                {
                    label = label.Remove(firstBracket); 
                    label = label.Trim(); 
                }

                connectionsGraph.AssignNodeToSubgraph(label, node.ID); 
                
                //Determine node color
                if (label.Contains("AXON"))
                {
                    nodeColorTable.Add(node.ID, "Red3");
                    nodeShapeTable.Add(node.ID, "hexagon");
                }
                else if (label.Contains("DENDRITE"))
                {
                    nodeColorTable.Add(node.ID, "green3");
                }
                else if (label.IndexOf("CBab", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "green4");
                }
                else if (label.Contains("GBC") || 
                         label.IndexOf("CBb", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "cadetblue");
                }
                else if (label.IndexOf("Aii", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "yellow3");
                    nodeShapeTable.Add(node.ID, "hexagon");
                }
                else if (label.IndexOf("S1", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "palevioletred1");
                    nodeShapeTable.Add(node.ID, "diamond");
                }
                else if (label.IndexOf("S2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "palevioletred4");
                    nodeShapeTable.Add(node.ID, "diamond");
                }
                else if (label.IndexOf("Ai", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "orchid");
                    nodeShapeTable.Add(node.ID, "diamond");
                }
                else if (label.IndexOf("STARBURST", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "hotpink");
                    nodeShapeTable.Add(node.ID, "diamond");
                }
                else if (label.IndexOf("IAC", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "brown1");
                    nodeShapeTable.Add(node.ID, "invtrapezium");
                }
                else if (label.IndexOf("ROD BC", StringComparison.OrdinalIgnoreCase)>=0)
                {
                    nodeColorTable.Add(node.ID, "purple");
                }
                else if (label.IndexOf("OFF", StringComparison.OrdinalIgnoreCase) >= 0 
                           || label.IndexOf("CBa", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "blue");
                }
                else if (label.Contains("BC"))
                {
                    nodeColorTable.Add(node.ID, "grey");
                }
                else if (label.IndexOf("AxC", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nodeColorTable.Add(node.ID, "orange");
                    nodeShapeTable.Add(node.ID, "doubleoctagon"); 
                }
                else if (label.Contains("YAC") ||
                         label.Contains("GABA"))
                {
                    nodeColorTable.Add(node.ID, "Red3");
                    nodeShapeTable.Add(node.ID, "triangle");
                }
                else if (label.Contains("GLY") ||
                         label.Contains("GAC"))
                {
                    nodeColorTable.Add(node.ID, "green3");
                    nodeShapeTable.Add(node.ID, "invtriangle");
                }
                else if (label.Contains("AC"))
                {
                    nodeColorTable.Add(node.ID, "darkkhaki");
                    nodeShapeTable.Add(node.ID, "ellipse");
                }
                else if (label.Contains("GC"))
                {
                    nodeColorTable.Add(node.ID, "saddlebrown");
                }
                else
                {
                    nodeColorTable.Add(node.ID, "grey");
                }
            }

            //These are the ghost nodes.  We want to allude to thier existence, but we may not 
            //have all the connections so they need to be drawn with a distinct style
            foreach (long ID in graph.FrontierNodes)
            {
                if (nodeColorTable.ContainsKey(ID))
                    continue;

                nodeColorTable.Add(ID, "white");
            }

            List<object> edgesJson = new List<object>();
            int edgeCount = 0;

            foreach (Edge edge in graph.EdgeList)
            {
                double length = 0; 

                //Create an edge for GraphViz
                GraphVizEdge<long> tempEdge = new GraphVizEdge<long>();
                connectionsGraph.addEdge(tempEdge);
                tempEdge.from = edge.SourceParentID;
                tempEdge.to = edge.TargetParentID;

                string TypeName = "unknown";

                if (false == graph.NodeList.ContainsKey(edge.SourceParentID) ||
                    false == graph.NodeList.ContainsKey(edge.TargetParentID))
                {
                    //This is a link to a ghost node
                    if (graph.NodeList.ContainsKey(edge.SourceID))
                    {
                        Structure SourceStructure = graph.NodeList[edge.SourceID];
                        StructureType type = StructureTypesDictionary[SourceStructure.TypeID];
                        TypeName = type.Name.ToLower();
                    }
                    else
                    {
                        TypeName = "unknown";
                    }

                }
                else
                {
                    Boolean activeEdge = true;

                    if (false == graph.InvolvedCells.Contains(edge.SourceParentID))
                    {
                        graph.InvolvedCells.Add(edge.SourceParentID);
                        activeEdge = false;

                    }

                    if (false == graph.InvolvedCells.Contains(edge.TargetParentID))
                    {
                        graph.InvolvedCells.Add(edge.TargetParentID);
                        activeEdge = false;

                    }

                    if (activeEdge)// add edge and cell information to json
                    {

                        edgeCount++;                       
                 
                        edgesJson.Add(new { id= edgeCount, node1= graph.NodeList[edge.SourceParentID].ID + "(" + graph.NodeList[edge.SourceParentID].Label+")",
                                            node2 = graph.NodeList[edge.TargetParentID].ID + "( " + graph.NodeList[edge.TargetParentID].Label + " )",
                                            label = edge.KeyString,
                                            type = StructureTypesDictionary[graph.NodeList[edge.SourceID].TypeID].Name
                        });


                       
                    }

                    Structure SourceStructure = graph.NodeList[edge.SourceID];                   

                    StructureType type = StructureTypesDictionary[SourceStructure.TypeID];

                    TypeName = type.Name.ToLower();
                    //Update the node color if we need to
                    string NodeColor = "unknown";
                    if (colorTable.ContainsKey(TypeName))
                        NodeColor = colorTable[TypeName];

                    if (nodeColorTable.ContainsKey(edge.SourceParentID))
                    {
                        if (nodeColorTable[edge.SourceParentID] == colorTable["unknown"])
                            nodeColorTable[edge.SourceParentID] = NodeColor;
                    }
                    else
                    {
                        nodeColorTable.Add(edge.SourceParentID, NodeColor);

                    }

                    //Don't change target color unless there is no entry
                    if (false == nodeColorTable.ContainsKey(edge.TargetParentID))
                    {
                        nodeColorTable.Add(edge.TargetParentID, colorTable["unknown"]);
                    }

                    /*
                    //Figure out how far apart the somas are and use that for a preferred edge length
                    LocationInfo sourceLoc = graph.LocationInfo[edge.SourceID]; 
                    LocationInfo targetLoc = graph.LocationInfo[edge.TargetID]; 

                    if(sourceLoc != null &&
                        targetLoc != null)
                    {
                        length = (int)Math.Sqrt(((sourceLoc.X - targetLoc.X) * (sourceLoc.X - targetLoc.X)) +
                                 ((sourceLoc.Y - targetLoc.Y) * (sourceLoc.Y - targetLoc.Y)) +
                                 ((sourceLoc.Y - targetLoc.Z) * (sourceLoc.Z - targetLoc.Z)))o;
                    }
                     */

                }


                //Set the arrow properties
                string color = "unknown";
                string arrowhead = "";
                string arrowtail = "";
                string tooltip = "";
                string dir = "";
                float arrowsize = additionFactor;
                float pensize = additionFactor; 

                dir = "";
                string StoredToolTip = "";
                string keyString = edge.KeyString;

                if (toolTipDictionary.ContainsKey(keyString))
                {
                    tooltip = toolTipDictionary[keyString];
                }
                else
                {
                    string temp = edge.ConnectionString;

                    string[] cells = temp.Replace(">","").Replace("<","").Split('-');

                    tooltip = edge.ConnectionString + CreateZLevelTooltip(graph, Convert.ToInt32(cells[0]), Convert.ToInt32(cells[1]));

                    sectionsDictionary[keyString] = new HashSet<long>();

                    sectionsDictionary[keyString].Add(graph.zlocationForSynapses[Convert.ToInt32(cells[0])]);
                    sectionsDictionary[keyString].Add(graph.zlocationForSynapses[Convert.ToInt32(cells[1])]);

                }

                if (colorTable.ContainsKey(TypeName))
                {
                    color = colorTable[TypeName];
                }


                tooltip = "" + tooltip + "";

                switch (TypeName)
                {
                    case "ribbon synapse":
                        {

                            dir = "forward";
                            arrowtail = "none";
                            arrowhead = "normal";
                            //arrowsize = "1.25";
                            break;
                        }

                    case "conventional":
                        {

                            dir = "forward";
                            arrowhead = "tee";
                            arrowtail = "none";
                            //arrowsize = "1.25";
                            break;
                        }
                    case "gap junction":
                        {

                            dir = "both";
                            arrowhead = "open";
                            arrowtail = "open";

                            //arrowsize = "1";
                        }
                        break;
                    case "bc conventional":
                        {
                            dir = "forward";
                            arrowtail = "none";
                            arrowhead = "onormal";
                            //arrowsize = "1.5";
                            break;
                        }
                    case "unknown":
                        {
                            dir = "forward";
                            arrowhead = "normal";
                            arrowtail = "none";
                            //arrowsize = "1.5";
                            break;    
                        }
                    case "desmosome":
                        {
                            dir = ""; 
                        }
                        break;

                }

                if (dir == "") // dont' consider drawing desmosome
                    continue;


                arrowsize = arrowsize * (float)(Math.Sqrt(edge.Strength) * mulFactor);
                if (arrowsize < 1)
                    arrowsize = 1;

                pensize = pensize * (float)Math.Sqrt(edge.Strength);

                //pensize = 1;

                //arrowsize = 1;

                string edgeSections = "#";
                foreach(long id in sectionsDictionary[keyString])
                {
                    edgeSections += id.ToString() + ",";
                }

                if (length > 0)
                    tempEdge.Attributes.Add("len", length.ToString());

                tempEdge.Attributes.Add("tailclip", "true"); 
                tempEdge.Attributes.Add("color", color);
                tempEdge.Attributes.Add("URL", edgeSections.Substring(0,edgeSections.Length-1));
                tempEdge.Attributes.Add("dir", dir);
                //tempEdge.edgeAttributes.Add("samehead", TypeName); 
                tempEdge.Attributes.Add("arrowhead", arrowhead);
                tempEdge.Attributes.Add("arrowtail", arrowtail);
                tempEdge.Attributes.Add("arrowsize", arrowsize.ToString());
                tempEdge.Attributes.Add("w", edge.Strength.ToString());
                //tempEdge.edgeAttributes.Add("weight", edge.Strength.ToString());
                tempEdge.Attributes.Add("penwidth", pensize.ToString());
                tempEdge.Attributes.Add("tooltip", tooltip.Length > 250 ? tooltip.Substring(0,250) : tooltip);

                //If the edge is bidirectional clone it, reverse the direction, and make it invisible to help directional layout algorithms.
                if (dir == "both")
                {
                    GraphVizEdge<long> reverseTempEdge = tempEdge.Clone() as GraphVizEdge<long>;
                    reverseTempEdge.to = tempEdge.from;
                    reverseTempEdge.from = tempEdge.to;
                    reverseTempEdge.Attributes.Add("style", "invis"); //invisible
                   // reverseTempEdge.edgeAttributes.Add("constraint", "false"); //don't adjust ranking of nodes, the first link does that
                    //reverseTempEdge.edgeAttributes.Add("layer", ""); //invisible
                  //  reverseTempEdge.edgeAttributes["weight"] = (edge.Strength/2).ToString();
                  //  tempEdge.edgeAttributes["weight"] = (edge.Strength/2).ToString();
                    connectionsGraph.addEdge(reverseTempEdge); 
                }

            }


            if (System.IO.File.Exists(userFile+".json"))
                System.IO.File.Delete(userFile + ".json");

            FileStream fs = new FileStream(userFile + ".json", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);
            JavaScriptSerializer oSerializer = new JavaScriptSerializer();
            sw.Write(oSerializer.Serialize(new { page = "1", total = (edgesJson.Count % 10 + 1), records = edgesJson.Count.ToString(), rows = edgesJson }));
            //sw.Write(oSerializer.Serialize(edgesJson));

            sw.Close();
            fs.Close();

            if (System.IO.File.Exists(storedFile + ".json"))
                System.IO.File.Delete(storedFile + ".json");
            System.IO.File.Copy(userFile + ".json", storedFile +".json");


            //Find the boundaries of node positions so we can scale positions to fit in the graph
            ConnectomeViz.Utils.BoundsAdjustment bounds = Utils.BoundsAdjustment.CalculateBounds(graph.LocationInfo.Values.ToArray(), 50, 50);

            foreach (long nodeID in graph.InvolvedCells)
            {
                GraphVizNode<long> tempNode = connectionsGraph.addNode(nodeID);

                try
                {
                    tempNode.Attributes.Add("fillcolor", nodeColorTable[nodeID]);
                }
                catch
                {
                    tempNode.Attributes.Add("fillcolor", "grey");
                }

                string shape = "ellipse";
                if (nodeShapeTable.ContainsKey(nodeID))
                    shape = nodeShapeTable[nodeID];

                if (nodeID == Convert.ToInt32(cellID))
                {
                    tempNode.Attributes.Add("shape", shape);

          //          if(fixedNodes)
                     //  tempNode.nodeAttributes["width"]= "80";
          //          else
          //              tempNode.nodeAttributes.Add("width", "0.8");
                }
                else
                {


                    tempNode.Attributes.Add("shape", shape);

                    if (fixedNodes)
                    {
                    //    tempNode.nodeAttributes.Add("width", "60");
                     //   tempNode.nodeAttributes.Add("height", "30");
                    }
                    else
                    {
                        tempNode.Attributes.Add("width", "0.6");
                        tempNode.Attributes.Add("height", "0.3");
                    }
                }
                tempNode.Attributes.Add("peripheries", "3");
                tempNode.Attributes.Add("fontcolor", "white");
                tempNode.Attributes.Add("label", graph.NodeList[nodeID].ID + "\\n" + graph.NodeList[nodeID].Label);
                tempNode.Attributes.Add("style", "filled");
                //tempNode.nodeAttributes.Add("target", "_top");
                tempNode.Attributes.Add("penwidth", "0.0");
                tempNode.Attributes.Add("fontname", "Arial");
                tempNode.Attributes.Add("tooltip", graph.NodeList[nodeID].ID + ", " + graph.NodeList[nodeID].Label);
                //tempNode.nodeAttributes.Add("URL", virtualRoot + "/Network/Trace/" + graph.NodeList[nodeID].ID);
                tempNode.Attributes.Add("URL", "#");
                if (fixedNodes)
                {
                    //tempNode.nodeAttributes.Add("fontsize", "8");
                   
                    
                    try
                    {
                        tempNode.Attributes.Add("fontsize", (graph.LocationInfo[nodeID].Radius * bounds.XScale * 72).ToString());
                       // tempNode.nodeAttributes.Add("width", "1"); tempNode.nodeAttributes.Add("fontsize", (graph.LocationInfo[nodeID].Radius * bounds.XScale * 72).ToString());
                        tempNode.Attributes.Add("width", ((int)graph.LocationInfo[nodeID].Radius * bounds.XScale * 2).ToString());
                       // tempNode.nodeAttributes.Add("height", ".5");
                        tempNode.Attributes.Add("height", ((int)graph.LocationInfo[nodeID].Radius * bounds.YScale).ToString());
                        tempNode.Attributes.Add("pos", ((float)(bounds.AdjustX(graph.LocationInfo[nodeID].X))).ToString() + "," +
                                                                        ((float)(bounds.AdjustY(graph.LocationInfo[nodeID].Y))).ToString());
                        tempNode.Attributes.Remove("tooltip");
                        tempNode.Attributes.Add("tooltip", graph.NodeList[nodeID].ID + ", " + graph.NodeList[nodeID].Label);
                        tempNode.Attributes.Add("pin", "true");

                    }
                    catch (Exception e)
                    {

                        connectionsGraph.removeNode(nodeID);

                    }
                }
                else
                {
                    tempNode.Attributes.Add("fontsize", "8");
                }
                    
            }

            //Create nodes for ghosts
            foreach (long ID in graph.FrontierNodes)
            {
                if (connectionsGraph.nodes.ContainsKey(ID))
                    continue;

                GraphVizNode<long> tempNode = connectionsGraph.addNode(ID);


                try
                {
                    tempNode.Attributes.Add("fillcolor", nodeColorTable[ID]);
                }
                catch
                {
                    tempNode.Attributes.Add("fillcolor", "white");
                }

                tempNode.Attributes.Add("fontcolor", "black");
                tempNode.Attributes.Add("label", ID.ToString());
                tempNode.Attributes.Add("style", "filled");
                //tempNode.nodeAttributes.Add("target", "_top");
                tempNode.Attributes.Add("penwidth", "0.0");
                tempNode.Attributes.Add("fontname", "Helvetica");
                //tempNode.nodeAttributes.Add("URL", virtualRoot + "/Network/Trace/" + ID.ToString());
                tempNode.Attributes.Add("URL", "#");
                if (fixedNodes)
                {
                    try
                    {
                        tempNode.Attributes.Add("fontsize", (graph.LocationInfo[ID].Radius * bounds.XScale * 72).ToString());
                        //tempNode.nodeAttributes.Add("width", "1");
                        tempNode.Attributes.Add("width", (graph.LocationInfo[ID].Radius*bounds.XScale*2).ToString());
                        //tempNode.nodeAttributes.Add("height", ".5");
                        tempNode.Attributes.Add("height", (graph.LocationInfo[ID].Radius*bounds.YScale).ToString());

                        tempNode.Attributes.Add("pos", ((float)(bounds.AdjustX(graph.LocationInfo[ID].X))).ToString() + "," +
                                                                        ((float)(bounds.AdjustY(graph.LocationInfo[ID].Y))).ToString());
                        tempNode.Attributes.Remove("tooltip");
                        tempNode.Attributes.Add("tooltip", graph.NodeList[ID].ID + ", " + graph.NodeList[ID].Label);
                        tempNode.Attributes.Add("pin", "true");
                    }
                    catch (Exception e)
                    {

                        connectionsGraph.removeNode(ID);

                    }
                    
                }
                else
                {
                    tempNode.Attributes.Add("fontsize", "8");
                    tempNode.Attributes.Add("width", "0.6");
                    tempNode.Attributes.Add("height", "0.3");
                }
                    

            }

            for(int i=0;i<connectionsGraph.edges.Count;i++)
            {
                if (!connectionsGraph.nodes.ContainsKey(connectionsGraph.edges[i].from) || !connectionsGraph.nodes.ContainsKey(connectionsGraph.edges[i].to))
                {
                    connectionsGraph.removeEdge(connectionsGraph.edges[i]);
                    i = 0;
                }
            }



            
            //connectionsGraph.completePath_local = userFile; // contains user file location local
            //connectionsGraph.completePath_URL = outputPath; // contains filename and the whole path, just needs appending of formats
            //connectionsGraph.virtualRoot = virtualRoot;

            if (!System.IO.Directory.Exists(outputPath))
                System.IO.Directory.CreateDirectory(outputPath);


            //specify layout
            connectionsGraph.layout = layout;

            //specify types of layout outputs required
            foreach( string type in fileTypes)
             connectionsGraph.outputFormats.Add(type);
          

            /*if(graph.InvolvedCells.Count() > 8)
                  connectionsGraph.graphAttribites.Add("size", "12,10!");
            else
                  connectionsGraph.graphAttribites.Add("size", "5,5!");
             */
            //Create graph and write to file
            connectionsGraph.minimize = true;

            //specify output path



            GraphVizEngine<long>.Convert(layout + ".exe", userFile, fileTypes);

            string svgfile = userFile + ".svg";
            

            //Add scripts to svg
            //if (System.IO.File.Exists(svgfile))
            //{
            //    FileStream file = new FileStream(svgfile, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            //    StreamReader sr = new StreamReader(file);
            //    StringBuilder contents = new StringBuilder(sr.ReadToEnd());
            //    sr.Close();
            //    file.Close();
            //    System.IO.File.Delete(svgfile);


            //    string searchfor = "http://www.w3.org/1999/xlink\">";
            //    contents.Replace(searchfor, searchfor +
            //        "\n<script xlink:href=\"" + virtualRoot + "/Scripts/SVGzoom.js\"/>\n<script xlink:href=\"" + virtualRoot + "/Scripts/effect.js\"/>");

            //    FileStream fl = new FileStream(svgfile, FileMode.Create);
            //    StreamWriter write = new StreamWriter(fl);
            //    write.Write(contents.ToString());
            //    write.Close();
            //    fl.Close();

            //    //FileStream f2 = new FileStream(outputPath + "changed.txt", FileMode.Create);
            //    //StreamWriter wrr = new StreamWriter(f2);
            //    //wrr.Write(contents.ToString());
            //    //wrr.Close();
            //    //f2.Close();
            //}

            try
            {
                foreach (string type in fileTypes)
                {
                    string destfile = storedFile + "." + type;
                    if (System.IO.File.Exists(destfile))
                        System.IO.File.Delete(destfile);
                    System.IO.File.Copy(userFile + "." + type, destfile);
                }
            }
            catch (Exception e)
            {
            }
             
        }


        public string returnFirstTypeOf(long cellID)
        {
            foreach (Edge edge in graph.EdgeList)
            {
                if (edge.SourceParentID == cellID || edge.TargetParentID == cellID)
                {
                    return StructureTypesDictionary[graph.NodeList[edge.SourceID].TypeID].Name;
                }

            }
            return null;
        }

        /***************************************
        * Create structure types dictionary
        * *************************************/


        public void webService_GetStructureTypes()
        {
            AnnotateStructureTypesClient proxy = State.CreateStructureTypeClient();
            
            proxy.Open();
            StructureType[] StructureTypes = proxy.GetStructureTypes();
            proxy.Close(); 

            foreach (StructureType type in StructureTypes)
            {
                StructureTypesDictionary.Add(type.ID, type);
            }
           
        }



        //Contains list of all structureIDs of the last hop
        List<long> ActualNodes = new List<long>();

     
 

    }
}
