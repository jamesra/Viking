using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

            foreach (var node in graph.Nodes.Values)
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
              
            /*
            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesAbove().Length > 0).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesAbove().SelectMany(e => node.Edges[e]).ToArray());
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesBelow().Length > 0).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesBelow().SelectMany(e => node.Edges[e]).ToArray());
            }
            */

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

        private static void AddIndexSetToMeshIndexMap(SortedList<GridVector3, long> map, Mesh3D<IVertex3D<ulong>> mesh, Geometry.IIndexSet set)
        {
            IVertex3D[] verts = mesh[set].ToArray();
            long[] mesh_indicies = set.ToArray();

            for (int iVert = 0; iVert < mesh_indicies.Length; iVert++)
            {
                map.Add(verts[iVert].Position, mesh_indicies[iVert]);
            }
        }

        /// <summary>
        /// Build a map so we can navigate from a vertex back to a mesh index from a port
        /// </summary>
        /// <param name="mesh">The mesh all ports in Nodes should index into</param>
        /// <param name="Nodes">All nodes containing cap ports that index into the mesh</param>
        /// <returns></returns>
        private static SortedList<GridVector3, long> CreateVertexToMeshIndexMap(Mesh3D<IVertex3D<ulong>> mesh, IEnumerable<ConnectionVerticies> ports)
        {
            SortedList<GridVector3, long> map = new SortedList<GridVector3, long>();

            foreach (ConnectionVerticies port in ports)
            {
                AddIndexSetToMeshIndexMap(map, mesh, port.ExternalBorder);

                foreach (var innerBorder in port.InternalBorders)
                {
                    AddIndexSetToMeshIndexMap(map, mesh, innerBorder);
                }
            }

            return map;
        }

        private static void CreatePortsForBranch(MeshNode node, MeshEdge[] edges)
        {
            MeshGraph graph = node.MeshGraph;

            //OK, Voronoi diagram the shapes.  Create new ports.
            MeshNode[] other_nodes;
            other_nodes = edges.Select(e => graph.Nodes[e.SourceNodeKey == node.Key ? e.TargetNodeKey : e.SourceNodeKey]).ToArray();


            //Build a set of all polygons in the nodes
            GridPolygon[] Polygons;
            {
                List<GridPolygon> Polylist = new List<GridPolygon>();
                Polylist.Add(node.CapPort.ToPolygon(node.Mesh.Verticies));

                //Need a map of verticies to Polygon/Index number
                foreach (MeshNode branchNode in other_nodes)
                {
                    Polylist.Add(branchNode.CapPort.ToPolygon(branchNode.Mesh.Verticies));
                }

                Polygons = Polylist.ToArray();
            }

            //Build a single mesh with all components of the branch
            foreach (MeshNode other_node in other_nodes)
            {
                SmoothMeshGenerator.MergeMeshes(node, other_node);
            }

            List<MeshNode> AllNodes = new List<MorphologyMesh.MeshNode>();
            AllNodes.Add(node);
            AllNodes.AddRange(other_nodes);

            SortedList<GridVector3, long> VertexToMeshIndex = CreateVertexToMeshIndexMap(node.Mesh, AllNodes.Select(n => node.IDToCrossSection[n.Key]));

            //Create a map so we can record where every vertex came from in the set of points
            //TODO: Overlapping verticies are not going to be handled...
            Dictionary<GridVector2, List<PolygonIndex>> pointToPoly = GridPolygon.CreatePointToPolyMap(Polygons.ToArray());

            //Create a map from the index into our array of polygons to the actual node ID the polygon came from.
            ulong[] PolygonToNodeKey = new ulong[other_nodes.Length + 1];
            PolygonToNodeKey[0] = node.Key;

            for (int iNode = 0; iNode < other_nodes.Length; iNode++)
            {
                PolygonToNodeKey[iNode + 1] = other_nodes[iNode].Key;
            }

            //Triangulate the ports together
            //IMesh mesh = pointToPoly.Keys.Triangulate();
            IMesh mesh = Polygons.ToArray().Triangulate();

            //OK, here there be dragons.  What to do with the mesh...
            GridTriangle[] triangles = mesh.ToTriangles().ToArray();

            GridLineSegment[] lines = mesh.ToLines().ToArray();

            //First remove any triangles that have a line segment in empty space.  i.e. across an internal polygon or outside the external boundary
            /*
            for (int iTri = 0; iTri < triangles.Length; iTri++)
            {
                GridTriangle triangle = triangles[iTri];

                //OK, determine if the triangle should recieve a face in the final mesh
                if (!(pointToPoly.ContainsKey(triangle.p1) && pointToPoly.ContainsKey(triangle.p2) && pointToPoly.ContainsKey(triangle.p3)))
                    continue;

                PointIndex A = pointToPoly[triangle.p1].First();
                PointIndex B = pointToPoly[triangle.p2].First();
                PointIndex C = pointToPoly[triangle.p3].First();

                PointIndex[] P = new PointIndex[] { A, B, C };

                double[] Z = P.Select(p => -AllNodes[p.iPoly].CapPortZ).ToArray();
                double AZ = -AllNodes[A.iPoly].CapPortZ;
                double BZ = -AllNodes[B.iPoly].CapPortZ;
                double CZ = -AllNodes[C.iPoly].CapPortZ;

                //Determine the mesh index for the verticies
                GridVector3[] v3 = triangle.Points.Select((v, i) => v.ToGridVector3(Z[i])).ToArray();
                long[] iMesh = v3.Select(v => VertexToMeshIndex[v]).ToArray();// triangle.Points.Select((v, i) => VertexToMeshIndex[v.ToGridVector3(Z[i])]).ToArray();
                long iMeshA = VertexToMeshIndex[triangle.p1.ToGridVector3(AZ)];
                long iMeshB = VertexToMeshIndex[triangle.p2.ToGridVector3(BZ)];
                long iMeshC = VertexToMeshIndex[triangle.p3.ToGridVector3(CZ)];

                //OK, march through the cases for our triangles
               
            }
            */

            //GridLineSegment[] lines = mesh.ToLines().ToArray();

            Dictionary<GridVector2, SortedSet<PolygonIndex>> pointToConnectedPolys = new Dictionary<GridVector2, SortedSet<PolygonIndex>>();


            GridVector2[] midpoints = lines.Select(l => l.PointAlongLine(0.5)).AsParallel().ToArray();

            //Figure out which verticies are included in the port
            //Verticies of a line between two shapes are included 
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                GridLineSegment l = lines[i];

                if (!pointToPoly.ContainsKey(l.A) || !pointToPoly.ContainsKey(l.B))
                    continue;

                PolygonIndex APolyIndex = pointToPoly[l.A].First();
                PolygonIndex BPolyIndex = pointToPoly[l.B].First();

                int APoly = APolyIndex.iPoly;
                int BPoly = APolyIndex.iPoly;

                //IsLineOnSurface(APolyIndex, BPolyIndex, Polygons, midpoints[i]);

                //Line between the same polygon.  We can ignore but add it to the list for that vertex (Right thing to do?)
                if (APoly == BPoly)
                {
                    if (APolyIndex.IsInner ^ BPolyIndex.IsInner)
                    {
                        CreateOrAddToSet(pointToConnectedPolys, l.A, BPolyIndex);
                        CreateOrAddToSet(pointToConnectedPolys, l.B, APolyIndex);
                    }

                    CreateOrAddToSet(pointToConnectedPolys, l.A, APolyIndex);
                    CreateOrAddToSet(pointToConnectedPolys, l.B, BPolyIndex);
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

                CreateOrAddToSet(pointToConnectedPolys, l.A, APolyIndex);
                CreateOrAddToSet(pointToConnectedPolys, l.A, BPolyIndex);
                CreateOrAddToSet(pointToConnectedPolys, l.B, APolyIndex);
                CreateOrAddToSet(pointToConnectedPolys, l.B, BPolyIndex);
            }


            for (int iEdge = 0; iEdge < edges.Length; iEdge++)
            {
                MeshEdge edge = edges[iEdge];

                GeneratePortFromTriangulation(node, edge, pointToConnectedPolys, PolygonToNodeKey);
            }
        }

        //private static void BuildMeshForBranch(MeshNode node, )

        /// <summary>
        /// Replace the external port for the node connected to the edge using data provided by triangulating the exterior and interior polygons of shapes on both sides of the edge
        /// </summary>
        /// <param name="node"></param>
        /// <param name="edge"></param>
        /// <param name="pointToConnectedPolys">A map indicating which polygon verticies each vertex is connected to in the triangulation</param>
        /// <returns></returns>
        private static void Triangulation(MeshNode node, MeshEdge edge, Dictionary<GridVector2, SortedSet<PolygonIndex>> pointToConnectedPolys, ulong[] PolygonToNodeKey)
        {
            ConnectionVerticies port = edge.GetPortForNode(node.Key);
            ConnectionVerticies other_port = edge.GetOppositePortForNode(node.Key);

            bool[] VertexInPort = new bool[port.ExternalBorder.Count];
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                GridVector2 v = node.Mesh.Verticies[(int)port.ExternalBorder[iVertex]].Position.XY();
                if (!pointToConnectedPolys.ContainsKey(v))
                {
                    VertexInPort[iVertex] = iVertex > 0 ? VertexInPort[iVertex - 1] : false;
                    continue;
                }

                SortedSet<PolygonIndex> Connected = pointToConnectedPolys[v];

                SortedSet<ulong> ConnectedKeys = new SortedSet<ulong>(Connected.Select(i => PolygonToNodeKey[i.iPoly]));

                VertexInPort[iVertex] = ConnectedKeys.Contains(edge.SourceNodeKey) && ConnectedKeys.Contains(edge.TargetNodeKey);
            }

            long[] PortIndices = new long[VertexInPort.Sum(b => b ? 1 : 0)];
            int iAdded = 0;
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                if (!VertexInPort[iVertex])
                    continue;

                PortIndices[iAdded] = port.ExternalBorder[iVertex];
                iAdded += 1;
            }

            PortIndices = PortIndices.Distinct().ToArray();
            if (PortIndices.Length >= 3)
            {
                port.ExternalBorder = new Geometry.IndexSet(PortIndices);
            }
        }


        /// <summary>
        /// Replace the external port for the node connected to the edge using data provided by triangulating the exterior and interior polygons of shapes on both sides of the edge
        /// </summary>
        /// <param name="node"></param>
        /// <param name="edge"></param>
        /// <param name="pointToConnectedPolys">A map indicating which polygon verticies each vertex is connected to in the triangulation</param>
        /// <returns></returns>
        private static void GeneratePortFromTriangulation(MeshNode node, MeshEdge edge, Dictionary<GridVector2, SortedSet<PolygonIndex>> pointToConnectedPolys, ulong[] PolygonToNodeKey)
        {
            ConnectionVerticies port = edge.GetPortForNode(node.Key);
            ConnectionVerticies other_port = edge.GetOppositePortForNode(node.Key);

            bool[] VertexInPort = new bool[port.ExternalBorder.Count];
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                int iMeshVertex = (int)port.ExternalBorder[iVertex];

                //A hack, this shouldn't occur. 
                if (iMeshVertex >= node.Mesh.Verticies.Count)
                    continue;

                GridVector2 v = node.Mesh.Verticies[iMeshVertex].Position.XY();
                if (!pointToConnectedPolys.ContainsKey(v))
                {
                    VertexInPort[iVertex] = iVertex > 0 ? VertexInPort[iVertex - 1] : false;
                    continue;
                }

                SortedSet<PolygonIndex> Connected = pointToConnectedPolys[v];

                SortedSet<ulong> ConnectedKeys = new SortedSet<ulong>(Connected.Select(i => PolygonToNodeKey[i.iPoly]));

                VertexInPort[iVertex] = ConnectedKeys.Contains(edge.SourceNodeKey) && ConnectedKeys.Contains(edge.TargetNodeKey);
            }

            long[] PortIndices = new long[VertexInPort.Sum(b => b ? 1 : 0)];
            int iAdded = 0;
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                if (!VertexInPort[iVertex])
                    continue;

                PortIndices[iAdded] = port.ExternalBorder[iVertex];
                iAdded += 1;
            }

            PortIndices = PortIndices.Distinct().ToArray();
            if (PortIndices.Length >= 3)
            {
                port.ExternalBorder = new Geometry.IndexSet(PortIndices);
            }
        }
        /*
        private static IIndexSet IdentifyOppositePortFromTriangulation(IIndexSet source, IMesh triangulation, DynamicRenderMesh<ulong> Mesh, Dictionary<GridVector2, List<PointIndex>> pointToPoly, GridPolygon[] Polygons)
        {
            GridLineSegment[] lines = triangulation.ToLines().ToArray();

            List<GridTriangle> tri = triangulation.ToTriangles();

            List<long> OppositePort = new List<long>();
            List<long> SourcePort = new List<long>();

            GridVector3[] portPositionsXYZ = source.Select(i => Mesh[i].Position).ToArray();
            GridVector2[] portPositions = portPositionsXYZ.Select(p => p.XY()).ToArray();

            SortedSet<GridVector2> sortedPortPositions = new SortedSet<GridVector2>(portPositions);

            GridLineSegment[] portLines = lines.Where(l => sortedPortPositions.Contains(l.A) || sortedPortPositions.Contains(l.B)).ToArray();

            //Find the first vertex in the mesh that connects ports
            int[] portMeshIndicies = triangulation.IndiciesForPointsXY(portPositions.Select(p => p.XY()));

            SortedSet<GridVector2> oppositePointsInPort = new SortedSet<GridVector2>();

            foreach (long index in source)
            {
                GridVector3 p = Mesh[index].Position;

                GridLineSegment[] attachedLines = portLines.Where(l => l.IsEndpoint(p.XY())).ToArray();
                //GridVector2[] attachedPoints = attachedLines.Select(l => l.OppositeEndpoint(p.XY())).ToArray();

                //oppositePointsInPort.UnionWith(attachedPoints);

                foreach (GridLineSegment line in attachedLines)
                {
                    GridVector2 attachedPoint = line.OppositeEndpoint(p.XY());

                    PointIndex APoly = pointToPoly[line.A].First();
                    PointIndex BPoly = pointToPoly[line.B].First();

                    if (!IsLineOnSurface(APoly, BPoly, Polygons, line.PointAlongLine(0.5)))
                        continue;

                    oppositePointsInPort.Add(attachedPoint);
                }


                                
            }
            
        }
        */

        private static GridTriangle[] RemoveTrianglesOutsidePolygons(GridTriangle[] triangles, GridLineSegment[] lines, GridPolygon[] polygons, Dictionary<GridVector2, PolygonIndex> pointToPoly)
        {
            GridVector2[] midpoints = lines.Select(l => l.PointAlongLine(0.5)).ToArray();

            bool[] KeepTriangle = triangles.Select(t => true).ToArray();

            Dictionary<GridLineSegment, SortedSet<int>> lineToTriangles = new Dictionary<GridLineSegment, SortedSet<int>>(); //List all triangles a line segment is part of
            for (int iTri = 0; iTri < triangles.Length; iTri++)
            {
                GridTriangle tri = triangles[iTri];
                foreach (GridLineSegment l in tri.Segments)
                {
                    if (!lineToTriangles.ContainsKey(l))
                    {
                        lineToTriangles.Add(l, new SortedSet<int>());
                    }

                    lineToTriangles[l].Add(iTri);
                }
            }
            bool[] KeepLine = lines.Select(l => true).ToArray();

            for (int iLine = 0; iLine < lines.Length; iLine++)
            {
                GridLineSegment l = lines[iLine];
                PolygonIndex APolyIndex = pointToPoly[l.A];
                PolygonIndex BPolyIndex = pointToPoly[l.B];

                //Same polygon, check if it is inside
                if (APolyIndex.iPoly == BPolyIndex.iPoly)
                {
                    GridPolygon poly = polygons[APolyIndex.iPoly];
                    //Check that the indicies are not adjacent.  If they are part of a border retain the line
                    if (PolygonIndex.IsBorderLine(APolyIndex, BPolyIndex, poly))
                        continue;

                    //Check if the line falls outside our polygon, in which case we don't want it;
                    KeepLine[iLine] = poly.Contains(midpoints[iLine]);
                }
                else
                {
                    GridPolygon[] connectedPolys = new GridPolygon[] { polygons[APolyIndex.iPoly], polygons[BPolyIndex.iPoly] };

                }
            }

            return triangles.Where((t, i) => KeepTriangle[i]).ToArray();
        }

        /// <summary>
        /// Add an integer to the dictionary for the key.  Creates the SortedSet if needed
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="iPoly"></param>
        private static void CreateOrAddToSet(Dictionary<GridVector2, SortedSet<PolygonIndex>> dict, GridVector2 key, PolygonIndex iPoly)
        {
            if (!dict.ContainsKey(key))
            {
                dict[key] = new SortedSet<PolygonIndex>();
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

        public static bool IsLineOnSurface(PolygonIndex APoly, PolygonIndex BPoly, GridPolygon[] Polygons, GridVector2 midpoint)
        {
            GridPolygon A = Polygons[APoly.iPoly];
            GridPolygon B = Polygons[BPoly.iPoly];

            if (APoly.iPoly != BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    return false; //Exclude from port.  Line covers empty space
                }
                else //Midpoint in one or the other polygon, but not both
                {
                    if (APoly.IsInner ^ BPoly.IsInner) //One or the other is an interior polygon, but not both
                    {
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Include in port.
                            //Line runs from exterior ring to the near side of an overlapping interior hole
                            return true;
                        }
                        else //Find out if the midpoint is contained by the same polygon with the inner polygon
                        {
                            if ((midInA && APoly.IsInner) || (midInB && BPoly.IsInner))
                            {
                                return true;// lineViews[i].Color = Color.Gold;
                            }
                            else
                            {
                                return false; //Not sure if this is correct.  Never saw it in testing. //lineViews[i].Color = Color.Pink;
                            }
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else if (APoly.iPoly == BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = midInA;

                if (PolygonIndex.IsBorderLine(APoly, BPoly, Polygons[APoly.iPoly]))
                {
                    //Line is part of the border, either internal or external
                    return true;
                }

                if (!midInA)
                {
                    //Line does not pass through solid space
                    return false;
                }
                else
                {
                    //Two options, the line is outside other shapes or inside other shapes.
                    //If outside other shapes we want to keep this edge, otherwise it is discarded
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                    if (APoly.IsInner ^ BPoly.IsInner)
                    {
                        return !LineIntersectsAnyOtherPoly;
                    }
                    else
                    {
                        return !LineIntersectsAnyOtherPoly;
                    }
                }
            }

            throw new ArgumentException("Unhandled case in IsLineOnSurface");
        }
    }
}
