using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;

namespace MorphologyMesh
{

    public static class SmoothMeshGenerator
    {

        static public int NumPointsAroundCircle = 16;
        static public int NumPointsAroundCircleAdjacentToPolygon = 64;

        /// <summary>
        /// Generate a mesh for a cell
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static Mesh3D<IVertex3D<ulong>> Generate(MorphologyGraph graph)
        {
            MeshGraph mGraph = MeshGraphBuilder.ConvertToMeshGraph(graph);
            return Generate(mGraph);
        }

        public static Mesh3D<IVertex3D<ulong>> Generate(MeshGraph meshGraph )
        {
            return Generate(meshGraph, out List<GridLineSegment> newMeshLines);
        }

        /// <summary>
        /// Generate a mesh for a cell
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static Mesh3D<IVertex3D<ulong>> Generate(MeshGraph meshGraph, out List<GridLineSegment> newMeshEdges)
        {
            //Adjust the verticies so the models are centered on zero
            //GridVector3 translate = -meshGraph.BoundingBox.CenterPoint;

            //Cap the terminal ports so they are not lost when we merge branches.  Since nodes can only have one upper and lower port we lose one branches port if we don't cap in advance.

            List<Mesh3D<IVertex3D<ulong>>> listMeshes = new List<Mesh3D<IVertex3D<ulong>>>();

#if DEBUG
            foreach (var Node in meshGraph.Nodes.Values)
            {
                CapTerminalPorts(Node);
            }
#else
            meshGraph.Nodes.Values.AsParallel().ForAll((node) => { CapTerminalPorts(node); });
#endif

#if DEBUG
            /*
            foreach (MeshNode node in meshGraph.Nodes.Values)
            {
                
                GridPolygon poly = PolygonForPort(node.Mesh, node.CapPort);
                GridVector2 convexHullCentroid;
                long FirstIndex = SmoothMeshGenerator.FirstIndex(poly.ExteriorRing, out convexHullCentroid);
                long SecondIndex = FirstIndex + 1 > poly.ExteriorRing.Length ? 0 : FirstIndex + 1; 

                DynamicRenderMesh<ulong> centroidMesh = ShapeMeshGenerator<ulong>.CreateMeshForCircle(new GridCircle(poly.Centroid, 16), node.Z, 16, node.Key, GridVector3.Zero);
                listMeshes.Add(centroidMesh);

                DynamicRenderMesh<ulong> convexHullCentroidMesh = ShapeMeshGenerator<ulong>.CreateMeshForBox(new GridBox(new GridRectangle(convexHullCentroid, 8), node.Z - 1, node.Z + 1), node.Key, GridVector3.Zero);
                listMeshes.Add(convexHullCentroidMesh);

                DynamicRenderMesh<ulong> firstIndexMesh = ShapeMeshGenerator<ulong>.CreateMeshForCircle(new GridCircle(poly.ExteriorRing[FirstIndex], 8), node.Z, 16, node.Key, GridVector3.Zero);
                listMeshes.Add(firstIndexMesh);

                DynamicRenderMesh<ulong> secondIndexMesh = ShapeMeshGenerator<ulong>.CreateMeshForBox(new GridBox(new GridRectangle(poly.ExteriorRing[SecondIndex], 8), node.Z-1, node.Z + 1), node.Key, GridVector3.Zero);
                listMeshes.Add(secondIndexMesh);
            }
            */
#endif

#if DEBUG
            newMeshEdges = new List<GridLineSegment>();
#else
            newMeshEdges = null; 
#endif

            int EdgesProcessed = 1;

            while (EdgesProcessed > 0)
            {
                EdgesProcessed = 0;

                IList<MeshEdge> EdgesToProcess = meshGraph.Edges.Values.ToList();

                foreach (MeshEdge edge in EdgesToProcess)
                {
                    if (!(meshGraph.Nodes.ContainsKey(edge.SourceNodeKey) &&
                       meshGraph.Nodes.ContainsKey(edge.TargetNodeKey)))
                        continue;

                    /*
                    MeshNode A = meshGraph.Nodes[edge.SourceNodeKey];
                    MeshNode B = meshGraph.Nodes[edge.TargetNodeKey];

                    MeshNode UpperNode;
                    MeshNode LowerNode;

                    if (A.Z > B.Z)
                    {
                        UpperNode = A;
                        LowerNode = B;
                    }
                    else
                    {
                        UpperNode = B;
                        LowerNode = A;
                    }

                    ulong[] EdgesToLowerNodes = UpperNode.GetEdgesBelow(meshGraph);
                    ulong[] EdgesToUpperNodes = LowerNode.GetEdgesAbove(meshGraph);

                    //The simplest case, not a branch or a terminal
                    if (EdgesToLowerNodes.Length <= 1 && EdgesToUpperNodes.Length <= 1)
                    {
                        MergeMeshNodes(meshGraph, A, B);
                        EdgesProcessed++;
                    }
                    else
                    {

                    }
                    */
#if DEBUG 
                    newMeshEdges.AddRange(MergeMeshEdge(meshGraph, edge));
#else
                    MergeMeshEdge(meshGraph, edge);
#endif
                    EdgesProcessed++;

                    //TODO: Merge the nodes in the graph 
                }
            }
             

            /*
            List<MeshNode> nodesToMerge = meshGraph.Nodes.Values.Where(n => IsValidBranchToMerge(n)).ToList();
            while (nodesToMerge.Count > 0)
            {
                foreach (MeshNode node in nodesToMerge)
                {
                    if (node.GetEdgesAbove(meshGraph).Length > 1)
                    {
                        //SimpleMergeBranchNode(node, true);
                        UglyMergeBranchNode(node, true);
                    }

                    if (node.GetEdgesBelow(meshGraph).Length > 1)
                    {
                        //SimpleMergeBranchNode(node, false);
                        UglyMergeBranchNode(node, false);
                    }
                }

                nodesToMerge = meshGraph.Nodes.Values.Where(n => IsValidBranchToMerge(n)).ToList();
            }
            */

            meshGraph.Nodes.Values.AsParallel().ForAll((node) => { node.Mesh.RecalculateNormals(); });

            /*
#if DEBUG

            //OK, the remaining nodes need to have caps put on thier faces
            foreach (MeshNode node in meshGraph.Nodes.Values)
            {
                node.Mesh.RecalculateNormals();
            }
#else

            meshGraph.Nodes.Values.AsParallel().ForAll((node) => { node.Mesh.RecalculateNormals(); });
#endif
*/

            //Todo: Not all nodes may be merged.  For these nodes just merge the meshes so we return a single mesh.

            MeshNode[] nodes = meshGraph.Nodes.Values.OrderByDescending(n => n.Mesh.Verticies.Count).ToArray();
            if (nodes.Length == 0)
                return null;

            MeshNode keepNode = nodes[0];
            for(int i = 1; i < nodes.Length; i++)  
            {
                MergeMeshes(keepNode, nodes[i]);
                meshGraph.RemoveNode(nodes[i].Key);
                nodes[i] = null; 
            }


            //listMeshes.AddRange(meshGraph.Nodes.Select(n => n.Value.Mesh).ToArray());
            //return listMeshes.ToArray();

            return keepNode.Mesh;
        }

        /// <summary>
        /// When we merge branches we remove the merged nodes.  However if there are merges that occur on the deleted nodes we do not get the updates.
        /// As a result we need to only merge branches whose branches are terminals or have already had all downstream branches merges.
        /// 
        /// Ex:
        /// E   F   G
        /// |    \ /
        /// C     D
        ///  \   /
        ///   \ /
        ///    B
        ///    |
        ///    A
        ///    
        /// If we merge BCD first then C&D have incomplete models.  When DFG is merged that model update is not incorporated into the ABCD mesh.
        /// Instead we should merge DFG, then merge BCD.
        /// This function returns true if all branches are terminal
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private static bool IsValidBranchToMerge(MeshNode node)
        {
            ulong[] branchesAbove = node.GetEdgesAbove(node.MeshGraph);
            ulong[] branchesBelow = node.GetEdgesBelow(node.MeshGraph);

            //Return false if there is no branch
            if (branchesAbove.Length <= 1 && branchesBelow.Length <= 1)
                return false; 

            for (int i = 0; i < branchesAbove.Length; i++)
            {
                ulong ID = branchesAbove[i];
                MeshNode branchNode = node.MeshGraph.Nodes[ID];                
                if (branchNode.GetEdgesAbove().Length > 1)
                    return false;  
            }

            for (int i = 0; i < branchesBelow.Length; i++)
            {
                ulong ID = branchesBelow[i];
                MeshNode branchNode = node.MeshGraph.Nodes[ID];
                if (branchNode.GetEdgesBelow().Length > 1)
                    return false;
            }

            return true;
        }

        private static GridVector3[] AllPointsInConnection(Mesh3D mesh, ConnectionVerticies port)
        {
            List<GridVector3> listPoints = new List<GridVector3>(port.TotalVerticies);

            listPoints.AddRange(port.ExternalBorder.Select(i => mesh.Verticies[(int)i].Position));
            listPoints.AddRange(port.InternalBorders.SelectMany(ib => ib.Select(i => mesh.Verticies[(int)i].Position)));
            listPoints.AddRange(port.InternalVerticies.Select(i => mesh.Verticies[(int)i].Position));

            return listPoints.ToArray();
        }

        /*
        private static void SimpleMergeBranchNode(MeshNode branchNode, bool BranchAbove)
        {
            //We have a node with two edges above or below the node. 
            //This function handles the simple solution where we create a 3D slab from the 2D outline of the annotation.

            double ZAdjustment = branchNode.MeshGraph.SectionThickness / 2.0;
            if (!BranchAbove)
                ZAdjustment = -ZAdjustment;

            ulong[] ConnectedNodes;
            ConnectionVerticies branch_node_port; 
            if(BranchAbove)
            {
                ConnectedNodes = branchNode.GetEdgesAbove(branchNode.MeshGraph);
                branch_node_port = branchNode.UpperPort;
            }
            else
            {
                ConnectedNodes = branchNode.GetEdgesBelow(branchNode.MeshGraph);
                branch_node_port = branchNode.LowerPort;
            }

            //Create a copy of the verticies at the border where the sections meet.
            GridVector3[] points = AllPointsInConnection(branchNode.Mesh, branch_node_port);

            Geometry.Meshing.Vertex[] verts = points.Select(p => new Geometry.Meshing.Vertex( new GridVector3(p.X, p.Y, p.Z + ZAdjustment), GridVector3.Zero)).ToArray();
            long iOriginalFirstIndex = branch_node_port.ExternalBorder.Min();
            long iFirstIndex = branchNode.Mesh.AddVertex(verts);

            ConnectionVerticies new_port = branch_node_port.IncrementStartingIndex((int)(-iOriginalFirstIndex + iFirstIndex));

            ConnectionVerticies upper_node_port = BranchAbove ? new_port : branch_node_port;
            ConnectionVerticies lower_node_port = BranchAbove ? branch_node_port : new_port;

            AttachPorts(branchNode.Mesh, upper_node_port, lower_node_port);

            CapPort(branchNode.Mesh, new_port, BranchAbove);

            if (BranchAbove)
                branchNode.UpperPortCapped = true;
            else
                branchNode.LowerPortCapped = true; 
        }
        */
        /*
        /// <summary>
        /// Create a branch by creating faces that intersect
        /// </summary>
        /// <param name="branchNode"></param>
        /// <param name="BranchAbove"></param>
        private static void UglyMergeBranchNode(MeshNode branchNode, bool BranchAbove)
        {
            //We have a node with two edges above or below the node. 
            //This function handles the simple solution where we create a 3D slab from the 2D outline of the annotation.
            ulong[] ConnectedNodeIDs;
            MeshNode[] ConnectedNodes;
            ConnectionVerticies source_node_port;
            if (BranchAbove)
            {
                ConnectedNodeIDs = branchNode.GetEdgesAbove(branchNode.MeshGraph);
                ConnectedNodes = ConnectedNodeIDs.Select(node_id => branchNode.MeshGraph.Nodes[node_id]).ToArray();
                source_node_port = branchNode.UpperPort;

                for(int i = 0; i < ConnectedNodes.Length; i++)
                { 
                    MergeMeshes(branchNode, ConnectedNodes[i]);
                    AttachPorts(branchNode.Mesh, ConnectedNodes[i].LowerPort, branchNode.UpperPort);
                }

                branchNode.UpperPortCapped = true;
            }
            else
            {
                ConnectedNodeIDs = branchNode.GetEdgesBelow(branchNode.MeshGraph);
                ConnectedNodes = ConnectedNodeIDs.Select(node_id => branchNode.MeshGraph.Nodes[node_id]).ToArray();
                source_node_port = branchNode.LowerPort;

                for (int i = 0; i < ConnectedNodes.Length; i++)
                {
                    MergeMeshes(branchNode, ConnectedNodes[i]);
                    AttachPorts(branchNode.Mesh, branchNode.LowerPort, ConnectedNodes[i].UpperPort);
                }

                branchNode.LowerPortCapped = true;
            }

            
            foreach(MeshNode removeNode in ConnectedNodes)
            {
                branchNode.MeshGraph.RemoveNode(removeNode.Key);
            }

        }
        */

            /*
        /// <summary>
        /// Create a branch by dividing the port into multiple parts according to which branch is closest and merge the ports
        /// </summary>
        /// <param name="branchNode"></param>
        /// <param name="BranchAbove"></param>
        private static void SmoothMergeBranchNode(MeshNode branchNode, bool BranchAbove)
        {
            //We have a node with two edges above or below the node. 
            
            ulong[] ConnectedNodeIDs;
            MeshNode[] ConnectedNodes;
            ConnectionVerticies source_node_port;
            ConnectionVerticies[] target_ports;
            if (BranchAbove)
            {
                ConnectedNodeIDs = branchNode.GetEdgesAbove(branchNode.MeshGraph);
                ConnectedNodes = ConnectedNodeIDs.Select(node_id => branchNode.MeshGraph.Nodes[node_id]).ToArray();
                source_node_port = branchNode.UpperPort;
                target_ports = ConnectedNodes.Select(node => node.LowerPort).ToArray();
            }
            else
            {
                ConnectedNodeIDs = branchNode.GetEdgesBelow(branchNode.MeshGraph);
                ConnectedNodes = ConnectedNodeIDs.Select(node_id => branchNode.MeshGraph.Nodes[node_id]).ToArray();
                source_node_port = branchNode.LowerPort;
                target_ports = ConnectedNodes.Select(node => node.UpperPort).ToArray();
            }

            //Create a copy of the verticies at the border where the sections meet.
            GridVector3[] points = AllPointsInConnection(branchNode.Mesh, source_node_port);

            GridPolygon[] target_port_polys = new GridPolygon[target_ports.Length];
            //Create a Voronoi domain using centers of the targets
            for (int i = 0; i < target_ports.Length; i++)
            {
                target_port_polys[i] = target_ports[i].ToPolygon(ConnectedNodes[i].Mesh);
            }

            GridVector2[] Target_Centroids = target_port_polys.Select(tpp => tpp.Centroid).ToArray();

            //An alternate implementation is to simply create the faces for each branch as overlapping, and then remove the intersecting triangles.
            if(target_ports.Length == 2)
            {
                //We need to cut the connection port in half.

            }
            else
            {
                TriangleNet.Voronoi.VoronoiBase voronoi = Target_Centroids.Voronoi();

                //Find the verticies of the branch connection ports.
                voronoi.ResolveBoundaryEdges();

                GridPolygon source_port_polys = source_node_port.ToPolygon(branchNode.Mesh);
            }
             
            //Create a single mesh from every node involved in the branch
            /*foreach(MeshNode RemoveNode in ConnectedNodes)
            {
                MergeMeshes(branchNode, RemoveNode);
            }*/

            //Divide the branch node's port into sub-ports for each branch.
            //How????  
       // }

        /// <summary>
        /// Place faces over the ports of a node. Done when no further joins are expected
        /// </summary>
        /// <param name="node"></param>
        private static void CapPorts(MeshNode node)
        { 
            CapUpperPort(node);  
            CapLowerPort(node); 
        }

        /// <summary>
        /// Cap ports with no edges.
        /// </summary>
        /// <param name="node"></param>
        private static void CapTerminalPorts(MeshNode node)
        {
            //The dead-end nodes need to be capped before they are merged into branches
             
            if (node.GetEdgesAbove(node.MeshGraph).Length == 0)
            {
                CapUpperPort(node);
            }

            if (node.GetEdgesBelow(node.MeshGraph).Length == 0)
            {
                CapLowerPort(node);
            } 
        }

        /// <summary>
        /// Place faces over the ports of a node. Done when no further joins are expected
        /// </summary>
        /// <param name="node"></param>
        private static void CapUpperPort(MeshNode node)
        {
            if (node.UpperPortCapped == false)
            {
                CapPort(node.Mesh, node.CapPort, true);
                node.UpperPortCapped = true;
            } 
        }

        /// <summary>
        /// Place faces over the ports of a node. Done when no further joins are expected
        /// </summary>
        /// <param name="node"></param>
        private static void CapLowerPort(MeshNode node)
        {
            if (node.LowerPortCapped == false)
            {
                CapPort(node.Mesh, node.CapPort, false);
                node.LowerPortCapped = true;
            }
        }

        private static void CapPort(Mesh3D<IVertex3D<ulong>> mesh, ConnectionVerticies Port, bool UpperFace)
        {
            //Cannot cap an open port
            if (Port.Type == ConnectionPortType.OPEN)
                return;

            GridPolygon UpperPoly = PolygonForPort(mesh, Port);
            IPoint2D[] internal_points = Port.InternalVerticies.Select(iv => mesh.Verticies[(int)iv].Position.Convert() as IPoint2D).ToArray();
            IMesh triangulate = UpperPoly.Triangulate(internal_points);

            double Z = mesh[Port.ExternalBorder.First()].Position.Z; 

            //Triangulation could add new verticies, and when I tested attributes in triangle I could not store the original index in the triangulation.  So go back and figure it out...
            Dictionary<GridVector2, long> VertToMeshIndex = PointToMeshIndex(mesh, Port);

            //Create a map of triangle index to mesh index
            Dictionary<int, long> Tri_to_Mesh = new Dictionary<int, long>();
            for (int iTri = 0; iTri < triangulate.Vertices.Count; iTri++)
            {
                TriangleNet.Geometry.Vertex v = triangulate.Vertices.ElementAt(iTri);
                GridVector2 tri_vert = new GridVector2(v.X, v.Y);
                if (VertToMeshIndex.ContainsKey(tri_vert))
                {
                    Tri_to_Mesh[iTri] = VertToMeshIndex[tri_vert];
                }
                else
                {
                    //Create a new vertex
                    Tri_to_Mesh[iTri] = mesh.AddVertex(new Geometry.Meshing.Vertex3D<ulong>(new GridVector3(v.X, v.Y, Z), GridVector3.UnitZ));
                    VertToMeshIndex.Add(new GridVector2(v.X, v.Y), iTri);
                }
            }
             
            foreach (var tri in triangulate.Triangles)
            {
                TriangleNet.Geometry.Vertex v1 = tri.GetVertex(0);
                TriangleNet.Geometry.Vertex v2 = tri.GetVertex(1);
                TriangleNet.Geometry.Vertex v3 = tri.GetVertex(2);

                int iA = (int)VertToMeshIndex[new GridVector2(v1.X, v1.Y)];
                int iB = (int)VertToMeshIndex[new GridVector2(v2.X, v2.Y)];
                int iC = (int)VertToMeshIndex[new GridVector2(v3.X, v3.Y)];

                Face f;
                if (!UpperFace)
                    f = new Face(iA, iB, iC);
                else
                    f = new Face(iC, iB, iA);

                mesh.AddFace(f);
            }

            return;
        }

        private static Dictionary<GridVector2, long> PointToMeshIndex(Mesh3D<IVertex3D<ulong>> mesh, ConnectionVerticies port)
        {
            Dictionary<GridVector2, long> VertToMeshIndex = new Dictionary<GridVector2, long>(port.ExternalBorder.Count + port.InternalBorders.Sum(ib=>ib.Count));
            
            foreach(long index in port.ExternalBorder)
            {
                GridVector2 XY = new GridVector2(mesh[index].Position.X, mesh[index].Position.Y);
                VertToMeshIndex.Add(XY, index); 
            }

            foreach (long index in port.InternalVerticies)
            {
                GridVector2 XY = new GridVector2(mesh[index].Position.X, mesh[index].Position.Y);
                VertToMeshIndex.Add(XY, index);
            }

            foreach (IIndexSet internalRing in port.InternalBorders)
            {
                foreach (long index in internalRing)
                {
                    GridVector2 XY = new GridVector2(mesh[index].Position.X, mesh[index].Position.Y);
                    VertToMeshIndex.Add(XY, index);
                }
            }

            return VertToMeshIndex;
        }

        private static GridPolygon PolygonForPort(Mesh3D<IVertex3D<ulong>> mesh, ConnectionVerticies port)
        {
            GridVector2[] ExternalVerts = port.ExternalBorder.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray();
            ExternalVerts = ExternalVerts.EnsureClosedRing();

            List<GridVector2[]> listInternalRings = new List<GridVector2[]>(port.InternalBorders.Length);
            foreach(IIndexSet internalRing in port.InternalBorders)
            {
                GridVector2[] InternalVerts = internalRing.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray().EnsureClosedRing();
                listInternalRings.Add(InternalVerts);
            }

            return new GridPolygon(ExternalVerts, listInternalRings);
        }

        
        internal static Mesh3D<IVertex3D<ulong>> MergeMeshes(MeshNode KeepNode, MeshNode RemoveNode)
        {
            Mesh3D<IVertex3D<ulong>> CompositeMesh;
             
            int NewStartingIndex = KeepNode.Mesh.Append(RemoveNode.Mesh);

            foreach(var OtherNodeKey in RemoveNode.Edges.Keys)
            {
                foreach(MeshEdge edge in RemoveNode.Edges[OtherNodeKey])
                {
                    bool RemoveNodeIsSource = RemoveNode.Key == edge.SourceNodeKey;
                    if(RemoveNodeIsSource)
                    {
                        edge.SourcePort = edge.SourcePort.IncrementStartingIndex(NewStartingIndex);
                    }
                    else
                    {
                        edge.TargetPort = edge.TargetPort.IncrementStartingIndex(NewStartingIndex);
                    }
                }
            }

            CompositeMesh = KeepNode.Mesh;
            KeepNode.IDToCrossSection[RemoveNode.Key] = RemoveNode.CapPort.IncrementStartingIndex(NewStartingIndex);
            return CompositeMesh;
        }

        private static List<GridLineSegment> MergeMeshEdge(MeshGraph graph, MeshEdge edge)
        {
            Mesh3D<IVertex3D<ulong>> CompositeMesh = null;
            MeshNode KeepNode = null;
            MeshNode RemoveNode = null;
            ConnectionVerticies KeepPort;
            ConnectionVerticies RemovePort;

            bool KeepTarget = graph.Nodes[edge.SourceNodeKey].Mesh.Verticies.Count < graph.Nodes[edge.TargetNodeKey].Mesh.Verticies.Count;

            if (KeepTarget)
            {
                KeepNode = graph.Nodes[edge.TargetNodeKey];
                RemoveNode = graph.Nodes[edge.SourceNodeKey];
            }
            else
            {
                KeepNode = graph.Nodes[edge.SourceNodeKey];
                RemoveNode = graph.Nodes[edge.TargetNodeKey];
            }

#if DEBUG
            System.Diagnostics.Trace.WriteLine(string.Format("Merge {0} - {1}", KeepNode.Key, RemoveNode.Key));
#endif

            CompositeMesh = MergeMeshes(KeepNode, RemoveNode);

            if (KeepTarget)
            {
                KeepPort = edge.TargetPort;
                RemovePort = edge.SourcePort;
            }
            else
            {
                KeepPort = edge.SourcePort;
                RemovePort = edge.TargetPort;
            }

            bool KeepIsUpper = KeepNode.IsNodeBelow(RemoveNode);

            List<GridLineSegment> newMeshLines = AttachPorts(CompositeMesh, 
                        KeepIsUpper ? KeepPort : RemovePort,
                        KeepIsUpper ? RemovePort : KeepPort);

            RemoveMergedEdge(KeepNode, RemoveNode);

            return newMeshLines;
        }

        private static void RemoveMergedEdge(MeshNode KeepNode, MeshNode RemoveNode)
        {
            MeshGraph graph = KeepNode.MeshGraph;
            MeshEdge removedEdge = new MeshEdge(KeepNode.Key, RemoveNode.Key);
            graph.RemoveEdge(removedEdge);

            foreach (var OtherNodeID in RemoveNode.Edges.Keys)
            {
                //Don't keep the edges that we just merged
                if (OtherNodeID == KeepNode.Key)
                {
                    continue;
                }

                foreach (MeshEdge EdgeToNode in RemoveNode.Edges[OtherNodeID])
                {
                    bool RemovedNodeIsSource = EdgeToNode.SourceNodeKey == RemoveNode.Key;
                    ulong SourceKey = RemovedNodeIsSource ? KeepNode.Key : OtherNodeID;
                    ulong TargetKey = RemovedNodeIsSource ? OtherNodeID : KeepNode.Key;
                    MeshEdge newEdge = new MeshEdge(SourceKey, TargetKey, EdgeToNode.SourcePort, EdgeToNode.TargetPort);

                    //Do not add the edge if it exists, this can happen if the graph has a cycle
                    if (!graph.Edges.ContainsKey(newEdge))
                        graph.AddEdge(newEdge);
                }
            }

            graph.RemoveNode(RemoveNode.Key);
        }
        /*
        private static void MergeMeshNodes(MeshGraph graph, MeshNode A, MeshNode B)
        {
            MeshNode UpperNode = A.IsNodeAbove(B) ? B : A;
            MeshNode LowerNode = A.IsNodeAbove(B) ? A : B;
            DynamicRenderMesh<ulong> CompositeMesh = null;
            MeshNode MergedNode = null;
            MeshNode RemoveNode = null;

            //Optimization: Merging meshes copies all verticies, edges, and faces into the mesh calling append.  Saves a lot of time to append the smaller mesh onto the larger.
            if (LowerNode.Mesh.Verticies.Count < UpperNode.Mesh.Verticies.Count)
            {
                CompositeMesh = MergeMeshes(UpperNode, LowerNode);
                MergedNode = UpperNode;
                RemoveNode = LowerNode;
            }
            else
            {
                CompositeMesh = MergeMeshes(LowerNode, UpperNode);
                MergedNode = LowerNode;
                RemoveNode = UpperNode;
            }
            
            AttachPorts(CompositeMesh, UpperNode.LowerPort, LowerNode.UpperPort);

            //Remove the nodes and replace with the new nodes and edges
            MeshGraph meshGraph = MergedNode.MeshGraph;

            MergedNode.Mesh = CompositeMesh;
            UpperNode.LowerPort = LowerNode.LowerPort;
            LowerNode.UpperPort = UpperNode.UpperPort;

            RemoveMergedEdge(MergedNode, RemoveNode);
            */
            /*
            MeshEdge removedEdge = new MeshEdge(MergedNode.Key, RemoveNode.Key);
            graph.RemoveEdge(removedEdge);

            foreach (var OtherNodeID in RemoveNode.Edges.Keys)
            {
                //Don't keep the edges that we just merged
                if(OtherNodeID == MergedNode.Key)
                {
                    continue;
                }

                foreach(MeshEdge EdgeToNode in RemoveNode.Edges[OtherNodeID])
                {
                    bool RemovedNodeIsSource = EdgeToNode.SourceNodeKey == RemoveNode.Key;
                    ulong SourceKey = RemovedNodeIsSource ? MergedNode.Key : OtherNodeID;
                    ulong TargetKey = RemovedNodeIsSource ? OtherNodeID : MergedNode.Key;
                    MeshEdge newEdge = new MeshEdge(SourceKey, TargetKey, EdgeToNode.SourcePort, EdgeToNode.TargetPort);

                    //Do not add the edge if it exists, this can happen if the graph has a cycle
                    if (!graph.Edges.ContainsKey(newEdge))
                        graph.AddEdge(newEdge);
                }
            }

            graph.RemoveNode(RemoveNode.Key);

            */
    //    }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="CompositeMesh">A mesh containing the verticies from both halves that need to be joined</param>
        /// <param name="UpperPort">Indicies that describe which verticies are part of the upper connection port</param>
        /// <param name="LowerPort">Indicies that describe which verticies are part of the lower connection port</param>
        internal static List<GridLineSegment> AttachPorts(Mesh3D<IVertex3D<ulong>> CompositeMesh, ConnectionVerticies UpperPort, ConnectionVerticies LowerPort)
        {
            //We need to center the verticies so both ports have the same centroid.  If we do not do this the GridVector2 distance measurement latches onto a single vertex on the convex hull when the shapes do not overlap.
            //TODO: Only works on the XY axis, not for truly 3D ports

            if (UpperPort.Type == ConnectionPortType.CLOSED && LowerPort.Type == ConnectionPortType.CLOSED)
                return AttachClosedPorts(CompositeMesh, UpperPort, LowerPort);
            else if (UpperPort.Type == ConnectionPortType.OPEN && LowerPort.Type == ConnectionPortType.OPEN)
                return AttachOpenPorts(CompositeMesh, UpperPort.ExternalBorder, LowerPort.ExternalBorder);

            //throw new ArgumentException("Unsupported port types");
            return new List<GridLineSegment>();
        }

        private static List<GridLineSegment> AttachClosedPorts(Mesh3D<IVertex3D<ulong>> CompositeMesh, ConnectionVerticies UpperPort, ConnectionVerticies LowerPort)
        {
            return AttachClosedPorts(CompositeMesh, UpperPort.ExternalBorder, LowerPort.ExternalBorder);

            //OK, if there are interior borders we need to figure out which ones overlap and connect the ports
            //GridPolygon[] UpperInteriorPolys = UpperPort.InternalBorders.Select(index_set => new GridPolygon(index_set.Select(i => CompositeMesh.Verticies[(int)i].Position.XY()).ToArray())).ToArray();
            //GridPolygon[] LowerInteriorPolys = LowerPort.InternalBorders.Select(index_set => new GridPolygon(index_set.Select(i => CompositeMesh.Verticies[(int)i].Position.XY()).ToArray())).ToArray();

            //Interior Polys that do not have an overlapping interior poly on the other annotation need to be capped off 
        }
        
        private static List<GridLineSegment> AttachClosedPorts(Mesh3D<IVertex3D<ulong>> CompositeMesh, IIndexSet UpperIndexArray, IIndexSet LowerIndexArray)
        {

            //System.Diagnostics.Debug.Assert(CompositeMesh[UpperIndexArray.First()].Position.Z >= CompositeMesh[LowerIndexArray.First()].Position.Z,
            //    string.Format("Upper and lower ports have incorrect Z relationship Upper: {0} Lower: {1}", CompositeMesh[UpperIndexArray.First()].Position.Z, CompositeMesh[LowerIndexArray.First()].Position.Z));

            //OK, find the nearest two verticies between the ports, and walk counter-clockwise (incrementing the index) around the shapes.  Creating faces until we are finished.
            //Find the verticies on the exterior ring
            GridVector2[] UpperVerticies;
            GridVector2[] LowerVerticies;
            //try
            //{
                UpperVerticies = UpperIndexArray.Select(i => CompositeMesh.Verticies[(int)i].Position.XY()).ToArray();
                LowerVerticies = LowerIndexArray.Select(i => CompositeMesh.Verticies[(int)i].Position.XY()).ToArray();
            /*}
            catch(ArgumentException)
            {
                return new List<GridLineSegment>();
            }*/

            List<GridVector2> UpperPolyVertList = UpperVerticies.ToList();
            UpperPolyVertList.Add(UpperPolyVertList.First());

            List<GridVector2> LowerPolyVertList = LowerVerticies.ToList();
            LowerPolyVertList.Add(LowerPolyVertList.First());

            
            GridPolygon upperPoly = new GridPolygon(UpperPolyVertList.ToArray());
            GridPolygon lowerPoly = new GridPolygon(LowerPolyVertList.ToArray());
            /*
            GridVector2 LowerPortCentroid = lowerPoly.Centroid;
            GridVector2 UpperPortCentroid = upperPoly.Centroid;
            */

            //Debug.Assert(lowerPoly.Contains(LowerPortCentroid));
            //Debug.Assert(upperPoly.Contains(UpperPortCentroid));


            long UpperStart = FirstIndex(UpperVerticies, out GridVector2 UpperPortConvexHullCentroid);
            long LowerStart = FirstIndex(LowerVerticies, out GridVector2 LowerPortConvexHullCentroid);

            //LowerVerticies = LowerVerticies.Translate(-LowerPortConvexHullCentroid);
            //UpperVerticies = UpperVerticies.Translate(-UpperPortConvexHullCentroid);

            //    long UpperStart;
            //    long LowerStart;

            //  FirstIndexByDistance(LowerVerticies, UpperVerticies, out LowerStart, out UpperStart);

            //Create faces for the rim.

            //Determine the normalized distance along the perimeter for each point
            double[] PerimeterDistanceA = CalculateNormalizedPerimeterDistance(UpperVerticies, UpperStart);
            double[] PerimeterDistanceB = CalculateNormalizedPerimeterDistance(LowerVerticies, LowerStart);

            //The next vertex that we will be adding a face for.  We have to determine if the third vertex is pulled from the upper or lower port.
            int iUpper = (int)UpperStart;
            int iLower = (int)LowerStart;

            int UpperAddedCount = 0;
            int LowerAddedCount = 0;

            //We make sure the normal of the face we create is facing away from the shape.  
            //This value is the same for all faces so it is only calculated once.
            bool? FlipFaces = new bool?();

            List<GridLineSegment> CreatedLines = new List<GridLineSegment>(UpperVerticies.Length + 1);

            bool HeadsOrTails = false; 
              
            while (true)
            {
                double UpperVertex = PerimeterDistanceA[iUpper];
                double LowerVertex = PerimeterDistanceB[iLower];

                int iNextUpper = iUpper + 1;
                int iNextLower = iLower + 1; 

                if(iNextUpper >= UpperVerticies.Length)
                {
                    iNextUpper = 0;
                }

                if (iNextLower >= LowerVerticies.Length)
                {
                    iNextLower = 0;
                }

                //If the current opposite side vertex (*) is behind the current vertex (*) and the next opposite side vertex (+) is in front, we always create that triangle.
                // A ---*----o--
                //     /  \
                // B -*----+--o-

                double NextUpperVertex = PerimeterDistanceA[iNextUpper];
                double NextLowerVertex = PerimeterDistanceB[iNextLower];

                GridVector2 UV1 = UpperVerticies[iUpper];
                GridVector2 LV1 = LowerVerticies[iLower];

                GridVector2 UV2 = UpperVerticies[iNextUpper];
                GridVector2 LV2 = LowerVerticies[iNextLower];

                //double UpperToNextLower = Math.Abs(NextLowerVertex - UpperVertex);
                //double LowerToNextUpper = Math.Abs(NextUpperVertex - LowerVertex);
                //double distLowerToNextUpper = GridVector2.Distance(LV1, UV2);
                //double distUpperToNextLower = GridVector2.Distance(UV1, LV2);
                //double distNextUpperToNextLower = GridVector2.Distance(UV2, LV2);

                //GridTriangle TwoUpper = new GridTriangle(UV1, UV2, LV1);
                //GridTriangle TwoLower = new GridTriangle(UV1, LV1, LV2);

                //We can't decide which triangle is more ideal, so consume a vertex from the shape with the most verticies unassigned
                //LinkToUpper = TwoUpper.Angles.Min() >= TwoLower.Angles.Min();

                //TODO, if upper and lower verticies are equal they always link
                bool LinkToUpper;

                if (UV1 == LV2)
                    LinkToUpper = false;
                else if (LV1 == UV2)
                    LinkToUpper = true;
                else
                {
                    GridLineSegment UV1LV2 = new GridLineSegment(UV1, LV2);
                    GridLineSegment LV1UV2 = new GridLineSegment(LV1, UV2);

                    GridVector2 UV1LV2_mid = UV1LV2.PointAlongLine(0.5);
                    GridVector2 LV1UV2_mid = LV1UV2.PointAlongLine(0.5);

                    EdgeType UV1LV2Type = UV1LV2.GetEdgeType(upperPoly, lowerPoly);
                    EdgeType LV1UV2Type = LV1UV2.GetEdgeType(upperPoly, lowerPoly);

                    if (!(UV1LV2Type == LV1UV2Type))
                    {
                        if (UV1LV2Type.IsValid() && !LV1UV2Type.IsValid())
                        {
                            LinkToUpper = false;
                        }
                        else if (!UV1LV2Type.IsValid() && LV1UV2Type.IsValid())
                        {
                            LinkToUpper = true;
                        }
                        else if (UV1LV2Type.CouldBeSliceChord() && !LV1UV2Type.CouldBeSliceChord())
                        {
                            LinkToUpper = false;
                        }
                        else if (!UV1LV2Type.CouldBeSliceChord() && LV1UV2Type.CouldBeSliceChord())
                        {
                            LinkToUpper = true;
                        }
                        else
                        {
                            throw new ArgumentException("Unhandled case for line type");
                        }
                    }
                    else if (UV1LV2Type.IsValid() && LV1UV2Type.IsValid())
                    {
                        LinkToUpper = LV1UV2.Length < UV1LV2.Length;
                    }
                    else if (UV1LV2Type.CouldBeSliceChord() && LV1UV2Type.CouldBeSliceChord())
                    {
                        LinkToUpper = LV1UV2.Length < UV1LV2.Length;
                    }
                    else
                    {
                        try
                        {
                            GridCircle UpperCircle = GridCircle.CircleFromThreePoints(UV1, UV2, LV1);
                            GridCircle LowerCircle = GridCircle.CircleFromThreePoints(UV1, LV1, LV2);

                            if (UpperCircle.Contains(LV2) && !LowerCircle.Contains(UV2))
                            {
                                LinkToUpper = false;
                            }
                            else if (!UpperCircle.Contains(LV2) && LowerCircle.Contains(UV2))
                            {
                                LinkToUpper = true;
                            }
                            else
                            {
                                //We can't decide which triangle is more ideal, so consume a vertex from the shape with the most verticies unassigned
                                //LinkToUpper = TwoUpper.Angles.Min() >= TwoLower.Angles.Min();

                                LinkToUpper = HeadsOrTails; //Alternate which line to add
                                HeadsOrTails = !HeadsOrTails;

                            }
                        }
                        catch (ArgumentException e)
                        {
                            //double distLowerToNextUpper = GridVector2.Distance(LV1, UV2);
                            //double distUpperToNextLower = GridVector2.Distance(UV1, LV2);

                            //This can occur when all three verticies are in a perfect line.  In this case choose the nearest.
                            //LinkToUpper = distLowerToNextUpper < distUpperToNextLower;
                            LinkToUpper = HeadsOrTails; //Alternate which line to add
                            HeadsOrTails = !HeadsOrTails;
                        }
                    }
                }
                 
                //We want to choose the triangle with the largest internal angles
                //bool LinkToUpper = TwoUpper.Angles.Min() >= TwoLower.Angles.Min();
                //bool LinkToUpper = TwoUpper.Area < TwoLower.Area;
                //bool LinkToUpper = distLowerToNextUpper < distUpperToNextLower;
                //bool LinkToUpper = LowerToNextUpper < UpperToNextLower;

                //If the next vertex would be closer to the opposite side then merge the opposite side
                int iUpperIndex = (int)UpperIndexArray[iUpper];
                int iMiddleIndex;
                int iLowerIndex = (int)LowerIndexArray[iLower];

                Face f;

                if ((LinkToUpper && !(UpperAddedCount == UpperIndexArray.Count)) ||
                    LowerAddedCount == LowerIndexArray.Count)
                {
                    iMiddleIndex = (int)UpperIndexArray[iNextUpper];

                    try
                    {
                        CreatedLines.Add(new GridLineSegment(UV2, LV1));
                    }
                    catch(Exception)
                    { }

                    f = new Face(iLowerIndex, iMiddleIndex, iUpperIndex);

                    if (!FlipFaces.HasValue)
                    {
                        FlipFaces = IsNormalCorrect(CompositeMesh.Normal(f),
                                        new GridLineSegment(UpperVerticies[iUpper],
                                                            UpperVerticies[iNextUpper]));
                    }
                    UpperAddedCount++;
                    iUpper = iNextUpper;
                }
                else
                {
                    iMiddleIndex = (int)LowerIndexArray[iNextLower];

                    try
                    {
                        CreatedLines.Add(new GridLineSegment(LV2, UV1));
                    }
                    catch (Exception)
                    { }

                    f = new Face(iLowerIndex, iMiddleIndex, iUpperIndex);

                    if (!FlipFaces.HasValue)
                    {
                        FlipFaces = IsNormalCorrect(CompositeMesh.Normal(f),
                                    new GridLineSegment(LowerVerticies[iLower],
                                                        LowerVerticies[iNextLower]));
                    }

                    LowerAddedCount++;
                    iLower = iNextLower;
                }
                
                if (FlipFaces.HasValue && FlipFaces.Value)
                    f = f.Flip();

                //Debug.Assert(FaceCorrect);

                //Face f = new Face(iLowerIndex, iMiddleIndex, iUpperIndex);
                CompositeMesh.AddFace(f);
                
                if (UpperAddedCount == UpperIndexArray.Count && LowerAddedCount == LowerIndexArray.Count)
                    break;
            }

            return CreatedLines;
        }

        public static bool IsNormalCorrect(Mesh3D<IVertex3D<ulong>> CompositeMesh, Face face, GridPolygon p, int iStartSegment)
        {
            GridVector3 normal = CompositeMesh.Normal(face);
            int iEndSegment = iStartSegment + 1; 
            if(iEndSegment >= p.ExteriorRing.Length)
            {
                iEndSegment = 0; 
            }

            //Get the line segment our face is part of.  
            GridLineSegment l = new GridLineSegment(p.ExteriorRing[iStartSegment], p.ExteriorRing[iEndSegment]);
            return false;
        }

        ///
        public static bool IsNormalCorrect(GridVector3 normal, GridLineSegment ExteriorLineSegment)
        {
            normal.Normalize();
            //We use counter - clockwise winding.  So if the normal points to the 
            //left side of the line the normal is pointing inside the polygon. 
            GridVector2 l = ExteriorLineSegment.B - ExteriorLineSegment.A;
            l.Normalize();
            
            double side = Math.Sin((l.X * (normal.Y - 0)) - (l.Y * (normal.X - 0)));
            return side < 0;
        }


        private static List<GridLineSegment> AttachOpenPorts(Mesh3D<IVertex3D<ulong>> CompositeMesh, IIndexSet UpperIndexArray, IIndexSet LowerIndexArray)
        {
            //Used to combine two lines.  Find the starting point by locating which endpoints are closest to each other.  
            GridVector2[] UpperVerticies = UpperIndexArray.Select(i => new GridVector2(CompositeMesh.Verticies[(int)i].Position.X, CompositeMesh.Verticies[(int)i].Position.Y)).ToArray();
            GridVector2[] LowerVerticies = LowerIndexArray.Select(i => new GridVector2(CompositeMesh.Verticies[(int)i].Position.X, CompositeMesh.Verticies[(int)i].Position.Y)).ToArray();

            GridVector2 LowerPortCentroid = GridRectangle.GetBoundingBox(LowerVerticies).Center;
            GridVector2 UpperPortCentroid = GridRectangle.GetBoundingBox(UpperVerticies).Center;

            LowerVerticies = LowerVerticies.Translate(-LowerPortCentroid);
            UpperVerticies = UpperVerticies.Translate(-UpperPortCentroid);

            //Figure out which vertex is closest to the first upper vertex
            bool ReverseLowerIndicies = GridVector2.Distance(LowerVerticies.First(), UpperVerticies.First()) > GridVector2.Distance(LowerVerticies.Last(), UpperVerticies.First());

            long[] lowerIndicies;
            if (ReverseLowerIndicies)
            {
                lowerIndicies = LowerIndexArray.Reverse().ToArray();
                LowerVerticies = LowerVerticies.Reverse().ToArray();
            }
            else
            {
                lowerIndicies = LowerIndexArray.ToArray();
            }

            //The next vertex that we will be adding a face for.  We have to determine if the third vertex is pulled from the upper or lower port.
            int iUpper = 0;
            int iLower = 0;

            int UpperAddedCount = 0;
            int LowerAddedCount = 0;

            List<GridLineSegment> CreatedLines = new List<GridLineSegment>(UpperVerticies.Length + 1);

            //OK, create faces between indicies until we reach the end of both lists of verticies
            while (true)
            {
                bool LinkToUpper;
                int iNextUpper = iUpper + 1;
                int iNextLower = iLower + 1;

                int iUpperIndex = (int)UpperIndexArray[iUpper];
                int iMiddleIndex;
                int iLowerIndex = (int)lowerIndicies[iLower];

                if (iNextLower >= LowerVerticies.Length)
                {
                    LinkToUpper = true;
                    iMiddleIndex = iNextUpper;
                }
                else if(iNextUpper >= UpperVerticies.Length)
                {
                    LinkToUpper = false;
                    iMiddleIndex = iNextLower;
                }
                else
                { 
                    GridVector2 UV1 = UpperVerticies[iUpper];
                    GridVector2 LV1 = LowerVerticies[iLower];

                    GridVector2 UV2 = UpperVerticies[iNextUpper];
                    GridVector2 LV2 = LowerVerticies[iNextLower];

                    LinkToUpper = GridVector2.Distance(LV1, UV2) < GridVector2.Distance(UV1, LV2);
                } 
                 
                if ((LinkToUpper && !(UpperAddedCount == UpperIndexArray.Count)) ||
                    LowerAddedCount == LowerIndexArray.Count)
                {
                    iMiddleIndex = (int)UpperIndexArray[iNextUpper];

                    if (UpperVerticies[iNextUpper] != LowerVerticies[iLower])
                    {
                        CreatedLines.Add(new GridLineSegment(UpperVerticies[iNextUpper], LowerVerticies[iLower]));
                    }

                    iUpper = iNextUpper;
                    UpperAddedCount++;
                }
                else
                {
                    iMiddleIndex = (int)lowerIndicies[iNextLower];

                    if (UpperVerticies[iUpper] != LowerVerticies[iNextLower])
                    {
                        CreatedLines.Add(new GridLineSegment(UpperVerticies[iUpper], LowerVerticies[iNextLower]));
                    }

                    iLower = iNextLower;
                    LowerAddedCount++;
                }

                Face f = new Face(iLowerIndex, iMiddleIndex, iUpperIndex);
                CompositeMesh.AddFace(f);

                Face oppositeFace = new Face(iUpperIndex, iMiddleIndex, iLowerIndex);
                CompositeMesh.AddFace(oppositeFace);

                if (UpperAddedCount == UpperIndexArray.Count - 1 && LowerAddedCount == LowerIndexArray.Count - 1)
                    break;
            }

            return CreatedLines;
        }

        /// <summary>
        /// Distance of points around the perimeter, normalized from 0 to 1
        /// </summary>
        /// <param name="Positions"></param>
        /// <param name="iStartingPoint"></param>
        /// <returns></returns>
        private static double[] CalculateNormalizedPerimeterDistance(GridVector2[] Positions, long iStartingPoint)
        {
            double PerimeterLength = Positions.PerimeterLength();

            double[] PerimeterDistance = new double[Positions.Length];

            double distance_accumulator = 0;

            long iPoint = iStartingPoint;
            long iNextPoint = iStartingPoint + 1;
            long PointCount = 0; 

            while(PointCount < Positions.Length)
            {
                if(iNextPoint >= Positions.Length)
                {
                    iNextPoint = 0; 
                }

                double Distance = GridVector2.Distance(Positions[iPoint], Positions[iNextPoint]);
                PerimeterDistance[iPoint] = distance_accumulator;
                distance_accumulator += Distance; 
                PointCount += 1;
                iPoint = iNextPoint;
                iNextPoint++; 
            }
              
            return PerimeterDistance.Select(d => d / distance_accumulator).ToArray();
        }
          
        /*
        //TODO: Check if adjacent mesh nodes are polygons and add more points in a circle if they are.
        public static bool IsNodeAdjacentToPolygon(MeshNode mNode)
        {
            ulong[] LinkedNodes = mNode.Edges.Keys.ToArray();
            LinkedNodes.Any(ln => mNode.MeshGraph.Nodes[ln].)
        }
        */

        private static void FirstIndexByDistance(GridVector2[] LowerVerticies, GridVector2[] UpperVerticies, out long LowerStart, out long UpperStart)
        {
            LowerStart = -1;
            UpperStart = -1; 

            double minDistance = double.MaxValue;
            for(int i = 0; i < LowerVerticies.Length; i++)
            {
                for(int j = i+1; j < UpperVerticies.Length; j++)
                {
                    double distance = GridVector2.Distance(LowerVerticies[i], UpperVerticies[j]);
                    if(distance < minDistance)
                    {
                        minDistance = distance; 
                        LowerStart = i;
                        UpperStart = j;
                    }
                }
            }

            return;
        }
 
        /// <summary>
        /// Find the first point on the convex hull whose angle is positive to the X axis.
        /// Written for 3 component vector, but expects input to be in X/Y plane.
        /// </summary>
        /// <param name="verticies"></param>
        /// <param name="Centroid"></param>
        /// <param name="Positions"></param>
        /// <returns></returns>
        private static long FirstIndex(ConnectionVerticies verticies, GridVector3[] Positions, out GridVector2 convexHullCentroid)
        { 
            GridVector2[] Positions2D = Positions.Select(p => new GridVector2(p.X, p.Y)).ToArray();

            return FirstIndex(Positions2D, out convexHullCentroid);
        }

        /// <summary>
        /// Find the first point on the convex hull whose angle is positive to the X axis.
        /// Written for 3 component vector, but expects input to be in X/Y plane.
        /// </summary>
        /// <param name="verticies"></param>
        /// <param name="Centroid"></param>
        /// <param name="ConvexHullPoints"></param>
        /// <returns></returns>
        public static long FirstIndex( GridVector2[] Positions2D, out GridVector2 convexHullCentroid)
        {

            GridVector2 center = GridPolygon.CalculateCentroid(Positions2D);
            GridVector2[] adjustedPositions = Positions2D.Translate(-center);
            GridVector2[] ConvexHullPoints = adjustedPositions.ConvexHull(out int[] original_idx);

            //GridVector2[] ConvexHullPoints = Positions2D.ConvexHull(out original_idx);

           // GridVector2 convexHullCenter = GridPolygon.CalculateCentroid(ConvexHullPoints);
            //GridVector2[]  PositionRelativeToCenter2D = adjustedPositions;
            GridVector2[] PositionRelativeToCenter2D = Positions2D.Translate(-center);
            GridVector2[] AngleAndDistance = new GridVector2[PositionRelativeToCenter2D.Length];
            GridVector3 Axis = GridVector3.UnitX;

            //TODO: Optimization, look for verticies where the X axis is positive
            //GridVector2[] CandidatePoints = PositionRelativeToCenter2D.Where(p => p.X > 0).ToArray();

            for(int i = 0; i < PositionRelativeToCenter2D.Length; i++)
            { 
                double Distance = GridVector2.Distance(GridVector2.Zero, PositionRelativeToCenter2D[i]);
                AngleAndDistance[i] = new GridVector2(GridVector2.Angle(GridVector2.UnitX, PositionRelativeToCenter2D[i]), Distance);
                //AngleAndDistance[i] = new GridVector2(GridVector3.Angle(Axis, PositionRelativeToCenter[i]), Distance);

                //if (PositionRelativeToCenter[i].Y < 0)
                    //AngleAndDistance[i].X = -AngleAndDistance[i].X;
            }

            //Find the first vertex where the angle is positive

            double BestAngle = AngleAndDistance.Max(p => p.X);
            int iBestVertex = 0;
            for(int i= 0; i < AngleAndDistance.Length; i++)
            {
                double Angle = AngleAndDistance[i].X;
                if(Angle >= 0 && Angle < BestAngle)
                {
                    BestAngle = Angle;
                    iBestVertex = i;
                }
            }

            convexHullCentroid = center;
            //Convert the convex hull index to the original index in the array
            return iBestVertex; //original_idx[iBestVertex];
        }
    }
}
