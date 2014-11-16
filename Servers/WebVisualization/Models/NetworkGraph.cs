using System;
using System.Collections.Generic; 
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using AnnotationVizLib;
using AnnotationVizLib.AnnotationService;

namespace ConnectomeViz.Models
{
    public class NetworkGraph
    {
        public NetworkGraph()
        { }

        public NetworkGraph(List<Edge> EdgeList, Dictionary<long, Structure> NodeList,List<long> InvolvedCells,
                        List<long> _FrontierNodes, List<long> FrontierNodes, List<long> ReducedEdges, Dictionary<long,LocationInfo> LocationInfo, Dictionary<long, long> zLocationInfoForSynapses)
        {

            this.EdgeList = EdgeList;
            this.NodeList = NodeList;
            this.InvolvedCells = InvolvedCells;
            this._FrontierNodes = _FrontierNodes;
            this.FrontierNodes = FrontierNodes;
            this.ReducedEdges = ReducedEdges;
            this.LocationInfo = LocationInfo;
            this.zlocationForSynapses = zLocationInfoForSynapses;
        }

        // Contains all edges
        public List<Edge> EdgeList = new List<Edge>();

        public Dictionary<long, LocationInfo> LocationInfo = new Dictionary<long, LocationInfo>();
        //Contains all cells among nodes
        public List<long> InvolvedCells = new List<long>();

        public Dictionary<long, long> zlocationForSynapses = new Dictionary<long, long>(); 

        private List<long> _FrontierNodes; 
        
        /// <summary>
        /// This is a list of node IDs which may have additional edges not included in the graph.  If the graph
        /// needs to be expanded another hop these are the nodes to query against
        /// </summary>
        public List<long> FrontierNodes
        {
            get { return _FrontierNodes; }
            set { _FrontierNodes = value; }
        }

        public List<long> ReducedEdges = new List<long>();

        //Contains all nodes
        public Dictionary<long, Structure> NodeList = new Dictionary<long, Structure>();

    }
}
