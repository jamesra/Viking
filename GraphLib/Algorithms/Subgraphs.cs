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
        /// <summary>
        /// Returns a list of sorted sets containing the node keys that exist in each subgraph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static List<SortedSet<KEY>> IsolatedSubgraphs(Graph<KEY, NODETYPE, EDGETYPE> graph)
        {
            List<SortedSet<KEY>> subgraphs = new List<SortedSet<KEY>>();

            SortedSet<KEY> mappedNodes = new SortedSet<KEY>();

            foreach (NODETYPE node in graph.Nodes.Values)
            {
                if (mappedNodes.Contains(node.Key))
                    continue;

                //Record all of the nodes linked to our new root node
                SortedSet<KEY> subgraph = new SortedSet<KEY>();
                ConnectedNodes(ref subgraph, graph, node);

                //Record all of the nodes in the subgraph so we don't re-record
                mappedNodes.UnionWith(subgraph);

                //Add to our list of subgraphs
                subgraphs.Add(subgraph);
            }

            Debug.Assert(mappedNodes.Count == graph.Nodes.Count, "Not all nodes in the graph were included in the IsolatedSubgraph analysis");

            return subgraphs;
        }        
    }
}
