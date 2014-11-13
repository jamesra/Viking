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
using ConnectomeViz.AnnotationService;
using System.Xml;
using System.Web.Script.Serialization;


namespace ConnectomeViz.Controllers
{
    [HandleError]
    [Authorize]
    public class CircuitController : Controller
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

            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Circuit");

            //int cellid = Int32.Parse(id);
            //Call draw graph
            CreateCircuitDiagram(Int32.Parse(id), 3, true);

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
            //Create structureTypes dictionary

            int cellid;
            
            if ( !String.IsNullOrEmpty(Request["ctl00$MainContent$structureID"]) && Int32.Parse(Request["ctl00$MainContent$structureID"]) != 0)
            {
                cellid = Convert.ToInt32(Request["ctl00$MainContent$structureID"]);
            }
            else 
            {
                cellid = Convert.ToInt32(Request["cellID"]);
            }


            State.selectedService = Request["ctl00$MainContent$dataSource"];

            State.selectedLab = Request["ctl00$MainContent$labName"];

            State.graphType = Request["group1"];

            String pin = Request["pinNodes"];

            if (String.IsNullOrEmpty(pin))
                fixedNodes = false;
            else
                fixedNodes = true;

            ViewData["dataSource"] = State.selectedService;

            ViewData["lab"] = State.selectedLab;

           

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

            

            CreateCircuitDiagram(cellid, hops, reduceEdges);

            //Map working directory
            string workingDirectory = Server.MapPath("~");
            ViewData["workDirectory"] = workingDirectory;


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

        public string CreateZLevelTooltip(Graph graph, long TargetID, long SourceID)
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

        Graph graph;     

        /*************************************************************
         * create the main circuit diagram depending on number of hops
        ******************************************************************/
        public void CreateCircuitDiagram(int cellID, int hops, bool reduceEdges)
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
            State.circuitID = cellID;

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

            string modifiedFileName= State.selectedLab+"_"+State.selectedService+"_"+cellID+"_"+hops+"_"+reduce+"_"+position;
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


            State.circuitFreshQuery = true;

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
                        State.circuitFreshQuery = false;
                    }
                    else
                        State.circuitFreshQuery = true;

                                                

                }
                catch (Exception e)
                { }

            }

         

            float additionFactor;

            float mulFactor;

            float threshold = 100.0f;

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

            CircuitClient circuitClient = State.CreateCircuitClient();
            circuitClient.Open();

            //string s = circuitClient.GetWorkingResponse(10);
            

            Graphx graphx = circuitClient.getGraph(cellID, hops + 1);

            
            List<Edge> tmpEdgeList = new List<Edge>();

            foreach (Edgex edgex in graphx.EdgeList)
            {
                Edge tmp = new Edge(edgex.SourceID, edgex.TargetID, edgex.SourceParentID, edgex.TargetParentID, edgex.Link, edgex.SourceTypeName,additionFactor);
                tmpEdgeList.Add(tmp);
            }

            graph = new Graph(tmpEdgeList, graphx.NodeList, graphx.InvolvedCells.ToList<long>(), graphx._FrontierNodes.ToList<long>(),
                            graphx.FrontierNodes.ToList<long>(), graphx.ReducedEdges.ToList<long>(), graphx.locationInfo, graphx.zLocationForSynapses);


            // Call graphviz Engine to create graph
            GraphVizEngine connectionsGraph = new GraphVizEngine();

            // Name the graph
            connectionsGraph.createDirectedGraph(cellID.ToString());

            string layout = "dot";

            //set graph attributes
            connectionsGraph.graphAttribites.Add("maxiter", "1000");

            //Scale max number of iterations according to the size of the graph
            connectionsGraph.graphAttribites.Add("nslimit", Math.Ceiling(Math.Sqrt(graph.NodeList.Count)).ToString());
            connectionsGraph.graphAttribites.Add("mclimit", Math.Ceiling(Math.Sqrt(graph.NodeList.Count)).ToString());

            connectionsGraph.graphAttribites.Add("splines", "true");

            connectionsGraph.graphAttribites.Add("size", "9,6.5");
            connectionsGraph.graphAttribites.Add("page", "11,8.5");
            connectionsGraph.graphAttribites.Add("dpi", "300"); 

            connectionsGraph.graphAttribites.Add("ratio", "compress");
            connectionsGraph.graphAttribites.Add("regular", "true"); 

            //connectionsGraph.graphAttribites.Add("packMode", "node");

            connectionsGraph.graphAttribites.Add("center", "true");
            //connectionsGraph.graphAttribites.Add("landscape", "true");

            connectionsGraph.graphAttribites.Add("minlen", "0"); 


            connectionsGraph.graphAttribites.Add("overlap", fixedNodes ? "true" : "false");

            if (!fixedNodes)
            {
                connectionsGraph.graphAttribites.Add("sep", "0.1");
                connectionsGraph.graphAttribites.Add("ranksep", "0.1");

                connectionsGraph.graphAttribites.Add("nodesep", "0.1");
                //connectionsGraph.graphAttribites.Add("overlap_scaling", "1.5");
                connectionsGraph.graphAttribites.Add("mode", "major");
                connectionsGraph.graphAttribites.Add("Model", "subset");

                //connectionsGraph.graphAttribites.Add("size", "6.5,9");
                //connectionsGraph.graphAttribites.Add("page", "8.5,11");
                //connectionsGraph.graphAttribites.Add("ratio", "compress");
                //connectionsGraph.graphAttribites.Add("mode", "major");
                //connectionsGraph.graphAttribites.Add("Model", "circuit");
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
                Edges tempEdge = new Edges();
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
                    tempEdge.edgeAttributes.Add("len", length.ToString());

                tempEdge.edgeAttributes.Add("tailclip", "true"); 
                tempEdge.edgeAttributes.Add("color", color);
                tempEdge.edgeAttributes.Add("URL", edgeSections.Substring(0,edgeSections.Length-1));
                tempEdge.edgeAttributes.Add("dir", dir);
                //tempEdge.edgeAttributes.Add("samehead", TypeName); 
                tempEdge.edgeAttributes.Add("arrowhead", arrowhead);
                tempEdge.edgeAttributes.Add("arrowtail", arrowtail);
                tempEdge.edgeAttributes.Add("arrowsize", arrowsize.ToString());
                tempEdge.edgeAttributes.Add("w", edge.Strength.ToString());
                //tempEdge.edgeAttributes.Add("weight", edge.Strength.ToString());
                tempEdge.edgeAttributes.Add("penwidth", pensize.ToString());
                tempEdge.edgeAttributes.Add("tooltip", tooltip.Length > 250 ? tooltip.Substring(0,250) : tooltip);

                //If the edge is bidirectional clone it, reverse the direction, and make it invisible to help directional layout algorithms.
                if (dir == "both")
                {
                    Edges reverseTempEdge = tempEdge.Clone() as Edges;
                    reverseTempEdge.to = tempEdge.from;
                    reverseTempEdge.from = tempEdge.to;
                    reverseTempEdge.edgeAttributes.Add("style", "invis"); //invisible
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
                Nodes tempNode = connectionsGraph.addNode(nodeID);

                try
                {
                    tempNode.nodeAttributes.Add("fillcolor", nodeColorTable[nodeID]);
                }
                catch
                {
                    tempNode.nodeAttributes.Add("fillcolor", "grey");
                }

                string shape = "ellipse";
                if (nodeShapeTable.ContainsKey(nodeID))
                    shape = nodeShapeTable[nodeID];

                if (nodeID == Convert.ToInt32(cellID))
                {
                    tempNode.nodeAttributes.Add("shape", shape);

          //          if(fixedNodes)
                     //  tempNode.nodeAttributes["width"]= "80";
          //          else
          //              tempNode.nodeAttributes.Add("width", "0.8");
                }
                else
                {


                    tempNode.nodeAttributes.Add("shape", shape);

                    if (fixedNodes)
                    {
                    //    tempNode.nodeAttributes.Add("width", "60");
                     //   tempNode.nodeAttributes.Add("height", "30");
                    }
                    else
                    {
                        tempNode.nodeAttributes.Add("width", "0.6");
                        tempNode.nodeAttributes.Add("height", "0.3");
                    }
                }
                tempNode.nodeAttributes.Add("peripheries", "3");
                tempNode.nodeAttributes.Add("fontcolor", "white");
                tempNode.nodeAttributes.Add("label", graph.NodeList[nodeID].ID + "\\n" + graph.NodeList[nodeID].Label);
                tempNode.nodeAttributes.Add("style", "filled");
                //tempNode.nodeAttributes.Add("target", "_top");
                tempNode.nodeAttributes.Add("penwidth", "0.0");
                tempNode.nodeAttributes.Add("fontname", "Arial");
                tempNode.nodeAttributes.Add("tooltip", graph.NodeList[nodeID].ID + ", " + graph.NodeList[nodeID].Label);
                //tempNode.nodeAttributes.Add("URL", virtualRoot + "/Circuit/Trace/" + graph.NodeList[nodeID].ID);
                tempNode.nodeAttributes.Add("URL", "#");
                if (fixedNodes)
                {
                    //tempNode.nodeAttributes.Add("fontsize", "8");
                   
                    
                    try
                    {
                        tempNode.nodeAttributes.Add("fontsize", (graph.LocationInfo[nodeID].Radius * bounds.XScale * 72).ToString());
                       // tempNode.nodeAttributes.Add("width", "1"); tempNode.nodeAttributes.Add("fontsize", (graph.LocationInfo[nodeID].Radius * bounds.XScale * 72).ToString());
                        tempNode.nodeAttributes.Add("width", ((int)graph.LocationInfo[nodeID].Radius * bounds.XScale * 2).ToString());
                       // tempNode.nodeAttributes.Add("height", ".5");
                        tempNode.nodeAttributes.Add("height", ((int)graph.LocationInfo[nodeID].Radius * bounds.YScale).ToString());
                        tempNode.nodeAttributes.Add("pos", ((float)(bounds.AdjustX(graph.LocationInfo[nodeID].X))).ToString() + "," +
                                                                        ((float)(bounds.AdjustY(graph.LocationInfo[nodeID].Y))).ToString());
                        tempNode.nodeAttributes.Remove("tooltip");
                        tempNode.nodeAttributes.Add("tooltip", graph.NodeList[nodeID].ID + ", " + graph.NodeList[nodeID].Label);
                        tempNode.nodeAttributes.Add("pin", "true");

                    }
                    catch (Exception e)
                    {

                        connectionsGraph.removeNode(nodeID);

                    }
                }
                else
                {
                    tempNode.nodeAttributes.Add("fontsize", "8");
                }
                    
            }

            //Create nodes for ghosts
            foreach (long ID in graph.FrontierNodes)
            {
                if (connectionsGraph.nodes.ContainsKey(ID))
                    continue;

                Nodes tempNode = connectionsGraph.addNode(ID);


                try
                {
                    tempNode.nodeAttributes.Add("fillcolor", nodeColorTable[ID]);
                }
                catch
                {
                    tempNode.nodeAttributes.Add("fillcolor", "white");
                }

                tempNode.nodeAttributes.Add("fontcolor", "black");
                tempNode.nodeAttributes.Add("label", ID.ToString());
                tempNode.nodeAttributes.Add("style", "filled");
                //tempNode.nodeAttributes.Add("target", "_top");
                tempNode.nodeAttributes.Add("penwidth", "0.0");
                tempNode.nodeAttributes.Add("fontname", "Helvetica");
                //tempNode.nodeAttributes.Add("URL", virtualRoot + "/Circuit/Trace/" + ID.ToString());
                tempNode.nodeAttributes.Add("URL", "#");
                if (fixedNodes)
                {


                    
                    
                    try
                    {
                        tempNode.nodeAttributes.Add("fontsize", (graph.LocationInfo[ID].Radius * bounds.XScale * 72).ToString());
                        //tempNode.nodeAttributes.Add("width", "1");
                        tempNode.nodeAttributes.Add("width", (graph.LocationInfo[ID].Radius*bounds.XScale*2).ToString());
                        //tempNode.nodeAttributes.Add("height", ".5");
                        tempNode.nodeAttributes.Add("height", (graph.LocationInfo[ID].Radius*bounds.YScale).ToString());

                        tempNode.nodeAttributes.Add("pos", ((float)(bounds.AdjustX(graph.LocationInfo[ID].X))).ToString() + "," +
                                                                        ((float)(bounds.AdjustY(graph.LocationInfo[ID].Y))).ToString());
                        tempNode.nodeAttributes.Remove("tooltip");
                        tempNode.nodeAttributes.Add("tooltip", graph.NodeList[ID].ID + ", " + graph.NodeList[ID].Label);
                        tempNode.nodeAttributes.Add("pin", "true");
                    }
                    catch (Exception e)
                    {

                        connectionsGraph.removeNode(ID);

                    }
                    
                }
                else
                {
                    tempNode.nodeAttributes.Add("fontsize", "8");
                    tempNode.nodeAttributes.Add("width", "0.6");
                    tempNode.nodeAttributes.Add("height", "0.3");
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



            
            connectionsGraph.completePath_local = userFile; // contains user file location local
            connectionsGraph.completePath_URL = outputPath; // contains filename and the whole path, just needs appending of formats
            connectionsGraph.virtualRoot = virtualRoot;

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


            connectionsGraph.Output();


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
