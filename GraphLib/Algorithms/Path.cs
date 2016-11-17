using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GraphLib
{
    public partial class Graph<KEY, NODETYPE, EDGETYPE>
    {
        public static void ConnectedNodes(ref SortedSet<KEY> subgraphNodes, Graph<KEY, NODETYPE, EDGETYPE> graph, NODETYPE rootNode)
        {
            if (subgraphNodes.Contains(rootNode.Key))
                return;

            subgraphNodes.Add(rootNode.Key);

            foreach (KEY linkedNode in rootNode.Edges.Keys)
            {
                if (subgraphNodes.Contains(linkedNode))
                    continue;

                ConnectedNodes(ref subgraphNodes, graph, graph.Nodes[linkedNode]);
            }
        }

        public static IList<KEY> Path(Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, KEY Destination)
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            return RecursePath(ref testedNodes, graph, Origin, Destination);
        }

        private static IList<KEY> RecursePath(ref SortedSet<KEY> testedNodes, Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, KEY Destination)
        {
            testedNodes.Add(Origin);

            List<KEY> path = new List<KEY>();
            path.Add(Origin);
            if (Origin.Equals(Destination))
                return path;

            NODETYPE origin_node = graph.Nodes[Origin];

            //If there are no nodes, then the destination cannot be reached from here.
            if (origin_node.Edges.Keys.Count == 0)
                return null;

            //Remove the nodes we've already checked
            SortedSet<KEY> linked_Keys = new SortedSet<KEY>(origin_node.Edges.Keys);
            linked_Keys.ExceptWith(testedNodes);

            //If no linked nodes left, there is no path here
            if (linked_Keys.Count == 0)
                return null;
            else if (linked_Keys.Count == 1)
            {
                //Is the edge directional?
                if (false == CanTravelPath(Origin, origin_node.Edges[linked_Keys.First()]))
                    return null;

                //Optimization, avoids copying the testedNodes set if there is only one path
                IList<KEY> result =  RecursePath(ref testedNodes, graph, linked_Keys.First(), Destination);
                if (result == null)
                    return null;

                path.AddRange(result);
                return path;
            }
            else
            {
                List<IList<KEY>> listPotentialPaths = new List<IList<KEY>>(linked_Keys.Count);
                foreach (KEY linked_Key in linked_Keys)
                {
                    // Is the edge directional ?
                    if (false == CanTravelPath(Origin, origin_node.Edges[linked_Key]))
                        continue;

                    SortedSet<KEY> testedNodesCopy = new SortedSet<KEY>(testedNodes);
                    IList<KEY> result = RecursePath(ref testedNodesCopy, graph, linked_Key, Destination);
                    if (result == null)
                        continue;

                    listPotentialPaths.Add(result);
                }

                //If no paths lead to destination, return null. 
                if (listPotentialPaths.Count == 0)
                    return null;

                //Otherwise, select the shortest path
                int MinDistance = listPotentialPaths.Select(L => L.Count).Min();
                IList<KEY> shortestPath = listPotentialPaths.Where(L => L.Count == MinDistance).First();
                path.AddRange(shortestPath);
                return path;
            } 
        }

        /// <summary>
        /// Some edges are direction.  This test checks if we can travel an edge going from the node key
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Destination"></param>
        /// <param name="edge"></param>
        protected static bool CanTravelPath(KEY Source, EDGETYPE edge)
        {
            if(edge.Directional)
            {
                return edge.SourceNodeKey.Equals(Source);
            }
            else
            {
                return edge.SourceNodeKey.Equals(Source) || edge.TargetNodeKey.Equals(Source);
            }
        }

        /// <summary>
        /// Some edges are direction.  This test checks if we can travel an edge going from the node key
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Destination"></param>
        /// <param name="edge"></param>
        protected static bool CanTravelPath(KEY Source, ICollection<EDGETYPE> edges)
        {
            foreach (EDGETYPE edge in edges)
            {
                if (CanTravelPath(Source, edge))
                    return true;
            }

            return false;
        }
    }
}