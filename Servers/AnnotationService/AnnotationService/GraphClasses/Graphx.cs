using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{
    [DataContract]
    public class Graphx
    {
        // Contains all edges
        [DataMember]
        public List<Edgex> EdgeList { get; set; }


        //Contains all cells among nodes
        [DataMember]
        public List<long> InvolvedCells { get; set; }
        [DataMember]
        private List<long> _FrontierNodes { get; set; }

        /// <summary>
        /// This is a list of node IDs which may have additional edges not included in the graph.  If the graph
        /// needs to be expanded another hop these are the nodes to query against
        /// </summary>
        [DataMember]
        public List<long> FrontierNodes
        {
            get { return _FrontierNodes; }
            set { _FrontierNodes = value; }
        }
        [DataMember]
        public List<long> ReducedEdges { get; set; }

        //Contains all nodes
        [DataMember]
        public SortedDictionary<long, Structure> NodeList { get; set; }

        [DataMember]
        public SortedDictionary<long, LocationInfo> locationInfo;


        [DataMember]
        public SortedDictionary<long, long> zLocationForSynapses;

        public Graphx()
        {
            EdgeList = new List<Edgex>();

            InvolvedCells = new List<long>();

            ReducedEdges = new List<long>();

            NodeList = new SortedDictionary<long, Structure>();

            locationInfo = new SortedDictionary<long, LocationInfo>();

            zLocationForSynapses = new SortedDictionary<long, long>();
        }

    }
}

