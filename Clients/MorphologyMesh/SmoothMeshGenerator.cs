using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using TriangleNet;
using TriangleNet.Meshing;
using TriangleNet.Geometry;
using System.Collections;

namespace MorphologyMesh
{
    /// <summary>
    /// Describes the verticies available to connect two meshes together.
    /// Verticies should be listed in Counter-clockwise order
    /// </summary>
    public class ConnectionVerticies
    {
        public IIndexSet ExternalBorder;

        public IIndexSet[] InternalBorders;
        
        public ConnectionVerticies(long[] exteriorRing, ICollection<long[]> interiorRings)
        {
            ExternalBorder = new IndexSet(exteriorRing);

            if(InternalBorders != null)
                InternalBorders = interiorRings.Select(ir => new IndexSet(ir)).ToArray();
            else
                InternalBorders = new IIndexSet[0];
        }

        public ConnectionVerticies(IIndexSet exteriorRing, IIndexSet[] interiorRings)
        {
            ExternalBorder = exteriorRing;

            if (interiorRings != null)
                InternalBorders = interiorRings;
            else
                InternalBorders = new IIndexSet[0];
        }

        /// <summary>
        /// Add a constant to all index values
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ConnectionVerticies IncrementStartingIndex(int value)
        {
            IIndexSet external = ExternalBorder.IncrementStartingIndex(value);
            IIndexSet[] internalSets = InternalBorders.Select(ib => ib.IncrementStartingIndex(value)).ToArray();

            return new ConnectionVerticies(external, internalSets);
        } 
    }
  
    
    public static class SmoothMeshGenerator
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
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                MeshNode newNode = SmoothMeshGenerator.CreateNode(node);
                newNode.MeshGraph = meshGraph;
                meshGraph.AddNode(newNode);

            }

            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                meshGraph.AddEdge(new MeshEdge(edge.SourceNodeKey, edge.TargetNodeKey));
            }

            return meshGraph;
        }

        /// <summary>
        /// Generate a mesh for a cell
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static ICollection<DynamicRenderMesh<ulong>> Generate(MorphologyGraph graph)
        {
            MeshGraph mGraph = ConvertToMeshGraph(graph);
            return Generate(mGraph);
        }

        /// <summary>
        /// Generate a mesh for a cell
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static ICollection<DynamicRenderMesh<ulong>> Generate(MeshGraph meshGraph)
        {
            //Adjust the verticies so the models are centered on zero
            //GridVector3 translate = -meshGraph.BoundingBox.CenterPoint;

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

                    MeshNode A = meshGraph.Nodes[edge.SourceNodeKey];
                    MeshNode B = meshGraph.Nodes[edge.TargetNodeKey];

                    MeshNode UpperNode;
                    MeshNode LowerNode; 

                    if(A.Z > B.Z)
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

                    //TODO: Merge the nodes in the graph 
                }
            }

            //OK, the remaining nodes need to have caps put on thier faces
            foreach(MeshNode node in meshGraph.Nodes.Values)
            {
                CapPorts(node);
            }

            //Todo: Not all nodes may be merged.  For these nodes just merge the meshes so we return a single mesh.

            return meshGraph.Nodes.Select(n => n.Value.Mesh).ToArray();
        }

        /// <summary>
        /// Place faces over the ports of a node. Done when no further joins are expected
        /// </summary>
        /// <param name="node"></param>
        private static void CapPorts(MeshNode node)
        {
            CapPort(node.Mesh, node.UpperPort, true);
            CapPort(node.Mesh, node.LowerPort, false);
        }

        private static void CapPort(DynamicRenderMesh mesh, ConnectionVerticies Port, bool UpperFace)
        {
            GridPolygon UpperPoly = PolygonForPort(mesh, Port);
            IMesh triangulate = UpperPoly.Triangulate();

            //double HalfSectionThickness = SectionThickness / 2.0; 

            double Z = mesh[Port.ExternalBorder.First()].Position.Z;
            /*
            Z += UpperFace ? HalfSectionThickness : -HalfSectionThickness;

            foreach(int i in Port.ExternalBorder)
            {
                GridVector3 p = mesh[i].Position; 
                mesh[i].Position = new GridVector3(p.X, p.Y, p.Z + (UpperFace ? HalfSectionThickness : -HalfSectionThickness));
            }
            */

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
                    Tri_to_Mesh[iTri] = mesh.AddVertex(new Geometry.Meshing.Vertex(new GridVector3(v.X, v.Y, Z), GridVector3.UnitZ));
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
                if (UpperFace)
                    f = new Face(iA, iB, iC);
                else
                    f = new Face(iC, iB, iA);

                mesh.AddFace(f);
            }

            return;
        }

        private static Dictionary<GridVector2, long> PointToMeshIndex(DynamicRenderMesh mesh, ConnectionVerticies port)
        {
            Dictionary<GridVector2, long> VertToMeshIndex = new Dictionary<GridVector2, long>(port.ExternalBorder.Count + port.InternalBorders.Sum(ib=>ib.Count));
            
            foreach(long index in port.ExternalBorder)
            {
                GridVector2 XY = new GridVector2(mesh[index].Position.X, mesh[index].Position.Y);
                VertToMeshIndex.Add(XY, index); 
            }
             
            foreach (IIndexSet internalRing in port.InternalBorders)
            {
                foreach (long index in port.ExternalBorder)
                {
                    GridVector2 XY = new GridVector2(mesh[index].Position.X, mesh[index].Position.Y);
                    VertToMeshIndex.Add(XY, index);
                }
            }

            return VertToMeshIndex;
        }

        private static GridPolygon PolygonForPort(DynamicRenderMesh mesh, ConnectionVerticies port)
        {
            GridVector2[] ExternalVerts = port.ExternalBorder.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray();

            List<GridVector2[]> listInternalRings = new List<GridVector2[]>(port.InternalBorders.Length);
            foreach(IIndexSet internalRing in port.InternalBorders)
            {
                GridVector2[] InternalVerts = internalRing.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray();
                listInternalRings.Add(InternalVerts);
            }

            return new GridPolygon(ExternalVerts, listInternalRings);
        }

        private static void MergeMeshNodes(MeshGraph graph, MeshNode A, MeshNode B)
        {
            MeshNode UpperNode = A.Z > B.Z ? A : B;
            MeshNode LowerNode = A.Z > B.Z ? B : A;

            //TODO: Merge the meshes and adjust all of the indicies and connection verticies
            int NewStartingIndex = UpperNode.Mesh.Append(LowerNode.Mesh); //Quiting here on friday.  Adjust the port indicies next...
            LowerNode.UpperPort = LowerNode.UpperPort.IncrementStartingIndex(NewStartingIndex);
            LowerNode.LowerPort = LowerNode.LowerPort.IncrementStartingIndex(NewStartingIndex);

            DynamicRenderMesh<ulong> CompositeMesh = UpperNode.Mesh; 

            //We keep the upper node and discard the lower node
            ConnectionVerticies UpperPort = UpperNode.LowerPort;
            ConnectionVerticies LowerPort = LowerNode.UpperPort;
                        
            //OK, find the nearest two verticies between the ports, and walk counter-clockwise (incrementing the index) around the shapes.  Creating faces until we are finished.
            //Find the verticies on the exterior ring
            GridVector2[] ExternalVerticiesUpper = UpperPort.ExternalBorder.Select(i => new GridVector2(CompositeMesh.Verticies[(int)i].Position.X, CompositeMesh.Verticies[(int)i].Position.Y)).ToArray();
            GridVector2[] ExternalVerticiesLower = LowerPort.ExternalBorder.Select(i => new GridVector2(CompositeMesh.Verticies[(int)i].Position.X, CompositeMesh.Verticies[(int)i].Position.Y)).ToArray();
               
            long UpperStart = FirstIndex(UpperPort, ExternalVerticiesUpper);
            long LowerStart = FirstIndex(LowerPort, ExternalVerticiesLower);

            //Create faces for the rim.
             
            //Determine the normalized distance along the perimeter for each point
            double[] PerimeterDistanceA = CalculateNormalizedPerimeterDistance(ExternalVerticiesUpper, UpperStart);
            double[] PerimeterDistanceB = CalculateNormalizedPerimeterDistance(ExternalVerticiesLower, LowerStart);

            //The next vertex that we will be adding a face for.  We have to determine if the third vertex is pulled from the upper or lower port.
            int iUpper = (int)UpperStart;
            int iLower = (int)LowerStart;

            int UpperAddedCount = 0;
            int LowerAddedCount = 0; 

            IIndexSet UpperIndexArray = UpperPort.ExternalBorder;
            IIndexSet LowerIndexArray = LowerPort.ExternalBorder;

            while (true)
            {
                double UpperVertex = PerimeterDistanceA[iUpper];
                double LowerVertex = PerimeterDistanceB[iLower];

                int iNextUpper = iUpper + 1;
                int iNextLower = iLower + 1; 

                if(iNextUpper >= ExternalVerticiesUpper.Length)
                {
                    iNextUpper = 0;
                }

                if (iNextLower >= ExternalVerticiesLower.Length)
                {
                    iNextLower = 0;
                }

                //If the current opposite side vertex (*) is behind the current vertex (*) and the next opposite side vertex (+) is in front, we always create that triangle.
                // A ---*----o--
                //     /  \
                // B -*----+--o-

                double NextUpperVertex = PerimeterDistanceA[iNextUpper];
                double NextLowerVertex = PerimeterDistanceB[iNextLower];

                GridVector2 UV1 = ExternalVerticiesUpper[iUpper];
                GridVector2 LV1 = ExternalVerticiesLower[iLower];

                GridVector2 UV2 = ExternalVerticiesUpper[iNextUpper];
                GridVector2 LV2 = ExternalVerticiesLower[iNextLower];

                double UpperToLower = NextLowerVertex - UpperVertex;
                double LowerToUpper = NextUpperVertex - LowerVertex;
                //bool LinkToUpper = GridVector2.Distance(LV1, UV2) < GridVector2.Distance(UV1, LV2);
                bool LinkToUpper = LowerToUpper < UpperToLower;

                int iUpperIndex = (int)UpperIndexArray[iUpper];
                int iMiddleIndex;
                int iLowerIndex = (int)LowerIndexArray[iLower];

                if ((LinkToUpper && !(UpperAddedCount == UpperIndexArray.Count)) ||
                    LowerAddedCount == LowerIndexArray.Count) 
                {
                    iMiddleIndex = (int)UpperIndexArray[iNextUpper];
                    iUpper = iNextUpper;
                    UpperAddedCount++;
                }
                else
                {
                    iMiddleIndex = (int)LowerIndexArray[iNextLower];
                    iLower = iNextLower;
                    LowerAddedCount++;
                }

                //Face f = new Face(iUpper, iUpper + 1, iLower);
                //Face f = new Face(iUpper, iLower + 1, iLower);
                Face f = new Face(iLowerIndex, iMiddleIndex, iUpperIndex);
                CompositeMesh.AddFace(f);

                if (UpperAddedCount == UpperIndexArray.Count && LowerAddedCount == LowerIndexArray.Count)
                    break;
            }
             
            //Remove the nodes and replace with the new nodes and edges
            MeshNode newNode = new MeshNode(UpperNode.Key);

            newNode.Mesh = CompositeMesh;
            MeshGraph meshGraph = UpperNode.MeshGraph;

            UpperNode.Mesh = CompositeMesh; 
            UpperNode.LowerPort = LowerNode.LowerPort;

            MeshEdge removedEdge = new MeshEdge(UpperNode.Key, LowerNode.Key);
            graph.RemoveEdge(removedEdge);
            
            foreach(var Edge in LowerNode.Edges.Keys)
            {
                MeshEdge newEdge = new MeshEdge(UpperNode.Key, Edge);
                //Do not add the edge if it exists, this can happen if the graph has a cycle
                if(!graph.Edges.ContainsKey(newEdge))
                    graph.AddEdge(newEdge);
            }
             
            graph.RemoveNode(LowerNode.Key); 
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

        private static MeshNode CreateNode(MorphologyNode node)
        {
            IShape2D shape = node.Geometry.ToShape2D();
            Vertex<ulong>[] v;
            MeshNode mNode = new MorphologyMesh.MeshNode(node.Key);
            mNode.PopulateNode(shape, -node.Z, node.ID);
            return mNode;
        }

        /// <summary>
        /// Create a mesh of verticies only for the specified shape
        /// </summary>
        /// <param name="mNode"></param>
        /// <param name="shape"></param>
        /// <param name="Z"></param>
        public static void PopulateNode(this MeshNode mNode, IShape2D shape, double Z, ulong NodeData)
        {
            mNode.Mesh = new DynamicRenderMesh<ulong>();

            switch (shape.ShapeType)
            {
                case ShapeType2D.CIRCLE:
                    ICircle2D circle = shape as ICircle2D;
                    mNode.Mesh.AddVertex(ShapeMeshGenerator<ulong>.CreateVerticiesForCircle(circle, Z, TopologyMeshGenerator.NumPointsAroundCircle, NodeData, GridVector3.Zero));
                    mNode.UpperPort = CreatePort(circle);
                    mNode.LowerPort = CreatePort(circle); 
                    break;
                case ShapeType2D.POLYGON:
                    IPolygon2D poly = shape as IPolygon2D;
                    mNode.Mesh.AddVertex(ShapeMeshGenerator<ulong>.CreateVerticiesForPolygon(poly, Z, NodeData, GridVector3.Zero));
                    mNode.UpperPort = CreatePort(poly);
                    mNode.LowerPort = CreatePort(poly); 
                    break;
                default:
                    throw new ArgumentException("Unexpected shape type");
            }
        }

        private static ConnectionVerticies CreatePort(ICircle2D shape)
        {
            ContinuousIndexSet ExternalBorder = new ContinuousIndexSet(0, TopologyMeshGenerator.NumPointsAroundCircle);
            return new ConnectionVerticies(ExternalBorder, null);
        }

        private static ConnectionVerticies CreatePort(IPolygon2D shape)
        {
            ContinuousIndexSet ExternalBorder = new ContinuousIndexSet(0, shape.ExteriorRing.Count-1);

            ContinuousIndexSet[] InternalBorders = new ContinuousIndexSet[shape.InteriorRings.Count];

            int iStartVertex = shape.ExteriorRing.Count;
            for(int i = 0; i < shape.InteriorRings.Count; i++)
            {
                InternalBorders[i] = new ContinuousIndexSet(iStartVertex, shape.InteriorRings.ElementAt(i).Length-1);
            }

            return new ConnectionVerticies(ExternalBorder, InternalBorders);
        }

        /// <summary>
        /// Find the first point on the convex hull whose angle is positive to the X axis.
        /// Written for 3 component vector, but expects input to be in X/Y plane.
        /// </summary>
        /// <param name="verticies"></param>
        /// <param name="Centroid"></param>
        /// <param name="Positions"></param>
        /// <returns></returns>
        private static long FirstIndex(ConnectionVerticies verticies, GridVector3[] Positions)
        { 
            GridVector2[] Positions2D = Positions.Select(p => new GridVector2(p.X, p.Y)).ToArray();

            return FirstIndex(verticies, Positions2D);
        }

        /// <summary>
        /// Find the first point on the convex hull whose angle is positive to the X axis.
        /// Written for 3 component vector, but expects input to be in X/Y plane.
        /// </summary>
        /// <param name="verticies"></param>
        /// <param name="Centroid"></param>
        /// <param name="ConvexHullPoints"></param>
        /// <returns></returns>
        private static long FirstIndex(ConnectionVerticies verticies, GridVector2[] Positions2D)
        {
            int[] original_idx; 
            GridVector2[] ConvexHullPoints = Positions2D.ConvexHull(out original_idx);

            GridVector2 center = GridVector2Extensions.Centroid(ConvexHullPoints);
            GridVector2[] PositionRelativeToCenter2D = ConvexHullPoints.Select(p => p - center).ToArray();
            GridVector2[] AngleAndDistance = new GridVector2[ConvexHullPoints.Length];
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

            //Convert the convex hull index to the original index in the array
            return original_idx[iBestVertex];
        }
    }
}
