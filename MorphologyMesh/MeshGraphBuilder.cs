using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using AnnotationVizLib;
using TriangleNet;
using TriangleNet.Meshing;

namespace MorphologyMesh
{
    interface IMeshGenerator
    {
        MeshNode CreateNode(MorphologyNode node);

        MeshEdge CreateEdge(MorphologyEdge edge);

        /// <summary>
        /// Create the connection port for the verticies we generated for the node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        ConnectionVerticies CreatePort(MorphologyNode source);

        //DynamicRenderMesh<T> CreateMesh(MorphologyNode node);
    }

    static class MeshGraphBuilder
    {
        /// <summary>
        /// Convert a morphology graph to an unprocessed mesh graph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static MeshGraph ConvertToMeshGraph(this MorphologyGraph graph)
        {
            MeshGraph meshGraph = new MeshGraph();

            meshGraph.SectionThickness = graph.SectionThickness;

            //Create a graph where each node is a set of verticies.
            ConcurrentBag<MeshNode> nodes = new ConcurrentBag<MeshNode>();

#if !DEBUG
            graph.Nodes.Values.AsParallel().ForAll(node =>
            {
                MeshNode newNode = SmoothMeshGraphGenerator.CreateNode(node);
                newNode.MeshGraph = meshGraph;
                nodes.Add(newNode);
            });
#else
            
            foreach(var node in graph.Nodes.Values)
            {
                MeshNode newNode = SmoothMeshGraphGenerator.CreateNode(node);
                newNode.MeshGraph = meshGraph;
                nodes.Add(newNode);
            }

#endif

            MeshNode meshNode = null;
            while (nodes.TryTake(out meshNode))
            {
                meshGraph.AddNode(meshNode);
            }

            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                MeshEdge mEdge = SmoothMeshGraphGenerator.CreateEdge(graph.Nodes[edge.SourceNodeKey], graph.Nodes[edge.TargetNodeKey]);
                meshGraph.AddEdge(mEdge);
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesAbove().Length > 0).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesAbove().SelectMany(e => node.Edges[e]).ToArray());
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesBelow().Length > 0).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesBelow().SelectMany(e => node.Edges[e]).ToArray());
            }

            /*
            //Create multiple ports for branches
            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesAbove().Length > 1).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesAbove().SelectMany(e => node.Edges[e]).ToArray());
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesBelow().Length > 1).ToArray())
            { 
                CreatePortsForBranch(node, node.GetEdgesBelow().SelectMany(e => node.Edges[e]).ToArray());
            }
             */

            return meshGraph;
        }

        private static void CreatePortsForBranch(MeshNode node, MeshEdge[] edges)
        {
            MeshGraph graph = node.MeshGraph;

            //OK, Voronoi diagram the shapes.  Create new ports.
            MeshNode[] other_nodes;
            other_nodes = edges.Select(e => graph.Nodes[e.SourceNodeKey == node.Key ? e.TargetNodeKey : e.SourceNodeKey]).ToArray();

            ulong[] nodeKeys = other_nodes.Select(n => n.Key).ToArray();
            /*
            //Temp
            foreach (MeshNode other in other_nodes)
            {
                node.MeshGraph.RemoveEdge(node.Edges[other.Key].First());
            }
            */
                        
            //Build a set of all points in the polygons
            List<GridPolygon> Polygons = new List<GridPolygon>();
             
            Polygons.Add(node.CapPort.ToPolygon(node.Mesh));

            //Need a map of verticies to Polygon/Index number
            foreach (MeshNode branchNode in other_nodes)
            {
                Polygons.Add(branchNode.CapPort.ToPolygon(branchNode.Mesh));
            }

            ulong[] PolygonToNodeKey = new ulong[other_nodes.Length + 1];
            PolygonToNodeKey[0] = node.Key;

            for(int iNode = 0; iNode < other_nodes.Length; iNode++)
            {
                PolygonToNodeKey[iNode + 1] = other_nodes[iNode].Key;
            }


            List <GridVector2> points = new List<GridVector2>(Polygons.Select(p => p.ExteriorRing.Length).Sum());
            foreach (GridPolygon p in Polygons)
            {
                points.AddRange(p.ExteriorRing);
            }

            IMesh mesh = points.Triangulate();

            GridLineSegment[] lines = mesh.ToLines().ToArray();

            Dictionary<GridVector2, int> pointToPoly = CreatePointToPolyMap(Polygons);

            Dictionary<GridVector2, SortedSet<int>> pointToConnectedPolys = new Dictionary<GridVector2, SortedSet<int>>();

            GridVector2[] midpoints = lines.Select(l => l.PointAlongLine(0.5)).AsParallel().ToArray();
            
            //Figure out which verticies are included in the port
            //Verticies of a line between two shapes are included 
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                GridLineSegment l = lines[i];

                if (!pointToPoly.ContainsKey(l.A) || !pointToPoly.ContainsKey(l.B))
                    continue;

                int APoly = pointToPoly[l.A];
                int BPoly = pointToPoly[l.B];

                //Line between the same polygon.  We can ignore but add it to the list for that vertex (Right thing to do?)
                if (APoly == BPoly)
                {
                    CreateOrAddToSet(pointToConnectedPolys, l.A, APoly); 
                    CreateOrAddToSet(pointToConnectedPolys, l.B, APoly);
                    continue; 
                }

                GridPolygon A = Polygons[APoly];
                GridPolygon B = Polygons[BPoly];

                GridVector2 midpoint = midpoints[i];//l.PointAlongLine(0.5);
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                //Line midpoint should be in one poly but not both
                if (!(midInA ^ midInB))
                {
                    continue;
                }
                
                CreateOrAddToSet(pointToConnectedPolys, l.A, APoly);
                CreateOrAddToSet(pointToConnectedPolys, l.A, BPoly);
                CreateOrAddToSet(pointToConnectedPolys, l.B, APoly);
                CreateOrAddToSet(pointToConnectedPolys, l.B, BPoly);
            }
            

            for (int iEdge = 0; iEdge < edges.Length; iEdge++)
            {
                MeshEdge edge = edges[iEdge];
                ConnectionVerticies port = edge.GetPortForNode(node.Key);
                
                bool[] VertexInPort = new bool[port.ExternalBorder.Count];
                for(int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
                { 
                    GridVector2 v = node.Mesh.Verticies[(int)port.ExternalBorder[iVertex]].Position.XY();
                    if (!pointToConnectedPolys.ContainsKey(v))
                    {
                        VertexInPort[iVertex] = iVertex > 0 ? VertexInPort[iVertex - 1] : false;
                        continue;
                    }

                    SortedSet<int> Connected = pointToConnectedPolys[v];

                    SortedSet<ulong> ConnectedKeys = new SortedSet<ulong>(Connected.Select(i => PolygonToNodeKey[i]));

                    VertexInPort[iVertex] = ConnectedKeys.Contains(edge.SourceNodeKey) && ConnectedKeys.Contains(edge.TargetNodeKey);
                }

                long[] PortIndices = new long[VertexInPort.Sum(b => b ? 1 : 0)];
                int iAdded = 0; 
                for(int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
                {
                    if (!VertexInPort[iVertex])
                        continue;

                    PortIndices[iAdded] = port.ExternalBorder[iVertex];
                    iAdded += 1; 
                }

                PortIndices = PortIndices.Distinct().ToArray();
                if (PortIndices.Length >= 3)
                {
                    port.ExternalBorder = new Geometry.Meshing.IndexSet(PortIndices);
                }
            }
        }

        /// <summary>
        /// Add an integer to the dictionary for the key.  Creates the SortedSet if needed
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="iPoly"></param>
        private static void CreateOrAddToSet(Dictionary<GridVector2, SortedSet<int>> dict, GridVector2 key, int iPoly)
        {
            if(!dict.ContainsKey(key))
            {
                dict[key] = new SortedSet<int>();
            }

            dict[key].Add(iPoly);
        }

        internal static Dictionary<GridVector2, int> CreatePointToPolyMap(IReadOnlyList<GridPolygon> Polygons)
        {
            Dictionary<GridVector2, int> pointToPoly = new Dictionary<GridVector2, int>();
            for (int iPoly = 0; iPoly < Polygons.Count; iPoly++)
            {
                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;
                foreach (GridVector2 p in poly.ExteriorRing)
                {
                    if (!pointToPoly.ContainsKey(p))
                        pointToPoly.Add(p, iPoly);
                }
            }

            return pointToPoly;
        }
    }
}
