using System;
using System.Collections.Generic;
using System.Linq;

namespace GraphLib
{
    /// <summary>
    /// Helper class to access path algorithms
    /// </summary>
    public static class GraphPathExtensions
    {
        /// <summary>
        /// Build the set of nodes connected to the root node through any number of hops
        /// </summary>
        /// <typeparam name="KEY"></typeparam>
        /// <typeparam name="NODETYPE"></typeparam>
        /// <typeparam name="EDGETYPE"></typeparam>
        /// <param name="graph"></param>
        /// <param name="subgraphNodes"></param>
        /// <param name="rootNode"></param>
        public static void ConnectedNodes<KEY, NODETYPE, EDGETYPE>(this Graph<KEY, NODETYPE, EDGETYPE> graph, ref SortedSet<KEY> subgraphNodes, NODETYPE rootNode)
            where KEY : IComparable<KEY>, IEquatable<KEY>
            where NODETYPE : Node<KEY, EDGETYPE>
            where EDGETYPE : Edge<KEY>
        {
            Graph<KEY, NODETYPE, EDGETYPE>.ConnectedNodes(ref subgraphNodes, graph, rootNode);
        }

        /// <summary>
        /// Return the shortest path from Origin to a node matching the predicate
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IList<KEY> ShortestPath<KEY, NODETYPE, EDGETYPE>(this Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, Func<NODETYPE, bool> IsMatch)
            where KEY : IComparable<KEY>, IEquatable<KEY>
            where NODETYPE : Node<KEY, EDGETYPE>
            where EDGETYPE : Edge<KEY>
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            return Graph<KEY, NODETYPE, EDGETYPE>.RecursePath(ref testedNodes, graph, Origin, IsMatch);
        }

        /// <summary>
        /// Return the shortest path from Origin to Destination
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IList<KEY> ShortestPath<KEY, NODETYPE, EDGETYPE>(this Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, KEY Destination)
            where KEY : IComparable<KEY>, IEquatable<KEY>
            where NODETYPE : Node<KEY, EDGETYPE>
            where EDGETYPE : Edge<KEY>
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            return Graph<KEY, NODETYPE, EDGETYPE>.RecursePath(ref testedNodes, graph, Origin, (node) => node.Key.Equals(Destination));
        }

        /// <summary>
        /// Return the set of nodes we can reach that match the condition without passing over a node that matches.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="IsMatch"></param>
        /// <returns></returns>
        public static SortedSet<KEY> FindReachableMatches<KEY, NODETYPE, EDGETYPE>(this Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, Func<NODETYPE, bool> IsMatch)
            where KEY : IComparable<KEY>, IEquatable<KEY>
            where NODETYPE : Node<KEY, EDGETYPE>
            where EDGETYPE : Edge<KEY>
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            SortedSet<KEY> matchingNodes = new SortedSet<KEY>();
            Graph<KEY, NODETYPE, EDGETYPE>.RecurseReachableNodes(ref testedNodes, ref matchingNodes, graph, Origin, IsMatch);
            return matchingNodes;
        }

        public static IList<KEY> FindCycle<KEY, NODETYPE, EDGETYPE>(this Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin)
            where KEY : IComparable<KEY>, IEquatable<KEY>
            where NODETYPE : Node<KEY, EDGETYPE>
            where EDGETYPE : Edge<KEY>
        {
            return Graph<KEY, NODETYPE, EDGETYPE>.FindCycle(graph, Origin);
        }
    }

    public partial class Graph<KEY, NODETYPE, EDGETYPE>
    {
        /// <summary>
        /// Build the set of all nodes connected by any number of edges to the rootNode
        /// </summary>
        /// <param name="subgraphNodes"></param>
        /// <param name="graph"></param>
        /// <param name="rootNode"></param>
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

        public static IList<KEY> FindCycle(Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin)
        {

            List<KEY> path = new List<KEY>();

            NODETYPE origin_node = graph.Nodes[Origin];
            //If there is zero or one edges a cycle cannot exist.
            var Candidates = origin_node.Edges;
            if (Candidates.Count <= 1)
                return null;

            foreach (var connected_node_edges in Candidates)
            {
                SortedSet<KEY> testedNodes = new SortedSet<KEY>();
                Edge<KEY> forbiddenDirectionalEdge = new Edge<KEY>(connected_node_edges.Key, Origin, true); //Do not allow us to travel back the way we came.  The only way to return to the origin is a cycle
                Edge<KEY> forbiddenBidirectionalEdge = new Edge<KEY>(connected_node_edges.Key, Origin, false); //Do not allow us to travel back the way we came.  The only way to return to the origin is a cycle

                var result = RecursePath(ref testedNodes, graph, connected_node_edges.Key,
                   (node) => { return node.Key.Equals(Origin); },
                   (source, edge) => { return CanTravelPath(source, edge) && edge != forbiddenDirectionalEdge && edge != forbiddenBidirectionalEdge; });

                if (result != null)
                {
                    result.Add(connected_node_edges.Key);
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Return the shortest path from Origin to a node matching the predicate
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IList<KEY> ShortestPath(Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, Func<NODETYPE, bool> IsMatch)
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            return RecursePath(ref testedNodes, graph, Origin, IsMatch);
        }

        /// <summary>
        /// Return the shortest path from Origin to Destination
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IList<KEY> ShortestPath(Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, KEY Destination)
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            return RecursePath(ref testedNodes, graph, Origin, (node) => node.Key.Equals(Destination));
        }

        /// <summary>
        /// Return the path to the nearest node matching the predicate
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        internal static IList<KEY> RecursePath(ref SortedSet<KEY> testedNodes, Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, Func<NODETYPE, bool> IsMatch, Func<KEY, EDGETYPE, bool> CanTravelEdge = null)
        {
            if (CanTravelEdge == null)
                CanTravelEdge = CanTravelPath;

            testedNodes.Add(Origin);

            List<KEY> path = new List<KEY>
            {
                Origin
            };
            if (IsMatch(graph.Nodes[Origin]))
                return path;

            NODETYPE origin_node = graph.Nodes[Origin];

            //If there are no nodes, then the destination cannot be reached from here.
            if (!origin_node.Edges.Keys.Any())
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
                if (false == CanTravelPath(Origin, origin_node.Edges[linked_Keys.First()], CanTravelEdge))
                    return null;

                //Optimization, avoids copying the testedNodes set if there is only one path
                IList<KEY> result = RecursePath(ref testedNodes, graph, linked_Keys.First(), IsMatch);
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
                    if (false == CanTravelPath(Origin, origin_node.Edges[linked_Key], CanTravelEdge))
                        continue;

                    SortedSet<KEY> testedNodesCopy = new SortedSet<KEY>(testedNodes);
                    IList<KEY> result = RecursePath(ref testedNodesCopy, graph, linked_Key, IsMatch);
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
        /// The default edge travel test. 
        /// Some edges are direction.  This test checks if we can travel an edge going from the node key
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Destination"></param>
        /// <param name="edge"></param>
        protected static bool CanTravelPath(KEY Source, EDGETYPE edge)
        {
            if (edge.Directional)
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
        protected static bool CanTravelPath(KEY Source, ICollection<EDGETYPE> edges, Func<KEY, EDGETYPE, bool> CanTravelEdge)
        {
            return edges.Any(e => CanTravelEdge(Source, e));
            /*foreach (EDGETYPE edge in edges)
            {
                if (CanTravelPath(Source, edge))
                    return true;
            }

            return false;*/
        }

        /// <summary>
        /// Return the set of nodes we can reach that match the condition without passing over a node that matches.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="IsMatch"></param>
        /// <returns></returns>
        public static SortedSet<KEY> FindReachableMatches(Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, Func<NODETYPE, bool> IsMatch)
        {
            SortedSet<KEY> testedNodes = new SortedSet<KEY>();
            SortedSet<KEY> matchingNodes = new SortedSet<KEY>();
            RecurseReachableNodes(ref testedNodes, ref matchingNodes, graph, Origin, IsMatch);
            return matchingNodes;
        }

        /// <summary>
        /// Return the path to the nearest node matching the predicate
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="Origin"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        internal static void RecurseReachableNodes(ref SortedSet<KEY> testedNodes, ref SortedSet<KEY> matchingNodes, Graph<KEY, NODETYPE, EDGETYPE> graph, KEY Origin, Func<NODETYPE, bool> IsMatch)
        {
            testedNodes.Add(Origin);

            if (IsMatch(graph.Nodes[Origin]))
            {
                matchingNodes.Add(Origin);
                return;
            }

            NODETYPE origin_node = graph.Nodes[Origin];

            //If there are no nodes, then the destination cannot be reached from here.
            if (!origin_node.Edges.Keys.Any())
                return;

            //Remove the nodes we've already checked
            SortedSet<KEY> linked_Keys = new SortedSet<KEY>(origin_node.Edges.Keys);
            linked_Keys.ExceptWith(testedNodes);

            //If no linked nodes left, there is no path here
            if (linked_Keys.Count == 0)
            {
                return;
            }
            else
            {
                foreach (KEY linked_node in linked_Keys)
                {
                    //Is the edge directional?
                    if (false == CanTravelPath(Origin, origin_node.Edges[linked_Keys.First()], CanTravelPath))
                        return;

                    RecurseReachableNodes(ref testedNodes, ref matchingNodes, graph, linked_node, IsMatch);
                }

                return;
            }
        }
    }
}