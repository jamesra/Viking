using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using System.Text;
using System.Threading.Tasks;
using Geometry.Meshing;

namespace MorphologyMesh
{
    public class MedialAxisEdge : GraphLib.Edge<GridVector2>
    {
        public MedialAxisEdge(GridVector2 SourceNode, GridVector2 TargetNode) : base(SourceNode, TargetNode, false)
        {
        }

        public GridLineSegment Line
        {
            get
            {
                return new GridLineSegment(this.SourceNodeKey, this.TargetNodeKey);
            }
        }
    }

    public class MedialAxisVertex : GraphLib.Node<GridVector2, MedialAxisEdge>
    {
        public MedialAxisVertex(GridVector2 k) : base(k)
        {
        }

        public override string ToString()
        {
            return Key.ToString();
        }
    }

    public class MedialAxisGraph : GraphLib.Graph<GridVector2, MedialAxisVertex, MedialAxisEdge>
    {
        public GridVector2 FindStartForBoundarySearch(GridPolygon[] shapes)
        {
            return Nodes.First(v => shapes.All(shape => !shape.Contains(v.Key))).Key;
        }

        public GridLineSegment[] Segments
        {
            get
            {
                return this.Edges.Select(edge => edge.Value.Line).ToArray();
            }
        }

        public GridVector2[] Points
        {
            get
            {
                return this.Nodes.Select(n => n.Key).ToArray();
            }
        }

    }

    public static class MedialAxisFinder
    {
        /// <summary>
        /// Approximate the boundary that is equidistant from all shapes
        /// </summary>
        /// <param name="shapes"></param>
        /// <returns></returns>
        static public MedialAxisGraph ApproximateMedialAxis(GridPolygon shape)
        {
            TriangleNet.Meshing.IMesh triangulationMesh = null;
            TriangulationMesh<IVertex2D<PointIndex>> mesh;
            try
            {
                mesh = shape.Triangulate();
                //triangulationMesh = TriangleExtensions.Triangulate(shape);// shape.Triangulate();
            }
            catch (ArgumentException)
            {
                return new MedialAxisGraph();
            }

            //List<GridLineSegment> LinesBetweenShapes = SelectLinesBetweenShapes(triangulationMesh, shapes);

            //List<GridTriangle> triangles = triangulationMesh.ToTriangles();

            MedialAxisGraph graph = BuildGraphFromMesh2D(mesh, shape);
            return graph;
        }

        private static MedialAxisGraph BuildGraphFromTriangles(GridTriangle[] triangles, GridPolygon boundary)
        {
           
            //Create an index map of points 
            //Dictionary<GridVector2, SortedSet<int>> PointToTrianglesIndex = CreatePointToConnectedTrianglesIndexLookup(triangles);

            Mesh2D mesh = triangles.ToDynamicRenderMesh();
            return BuildGraphFromMesh2D(mesh, boundary);
        }

        private static MedialAxisGraph BuildGraphFromMesh2D(IReadOnlyMesh2D<IVertex2D> mesh, GridPolygon boundary)
        {
            MedialAxisGraph graph = new MedialAxisGraph();

            foreach (var edge in mesh.Edges.Values)
            {
                //Create a vertex at the edge midpoint
                GridLineSegment line = mesh.ToGridLineSegment(edge);

                //If the line is between two different shapes we add a node to the graph
                if (false == boundary.IsExteriorOrInteriorSegment(line) && boundary.ContainsExt(line.Bisect()) == OverlapType.CONTAINED)
                {
                    MedialAxisVertex node = GetOrAddLineBisectorVertex(graph, line);
                    System.Diagnostics.Debug.Assert(boundary.ContainsExt(node.Key) == OverlapType.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");

                    foreach (IFace AdjacentFace in edge.Faces)
                    {
                        MedialAxisVertex otherNode = null;

                        var edgeCandidates = AdjacentFace.Edges.Where(e => e.Equals(edge) == false && boundary.IsExteriorOrInteriorSegment(mesh.ToGridLineSegment(e)) == false).ToList();
                        if (edgeCandidates.Count == 1)
                        {
                            GridLineSegment ConnectedLine = mesh.ToGridLineSegment(edgeCandidates.First());
                            GridVector2 midpoint = ConnectedLine.Bisect();
                            GridLineSegment ProposedMedialLine = new GridLineSegment(node.Key, midpoint);
                            if (boundary.Intersects(ProposedMedialLine) == false && boundary.ContainsExt(midpoint) == OverlapType.CONTAINED) //Checking for containment handles a rare edge case
                            {
                                otherNode = GetOrAddLineBisectorVertex(graph, ConnectedLine);
                                System.Diagnostics.Debug.Assert(boundary.ContainsExt(otherNode.Key) == OverlapType.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");
                            }
                            else
                            {
                                GridTriangle tri = new GridTriangle(mesh[AdjacentFace.iVerts].Select(v => v.Position).ToArray());
                                //GridVector2 face_centroid = mesh.GetCentroid(AdjacentFace);
                                GridVector2 face_centroid = tri.Centroid;
                                otherNode = GetOrAddVertex(graph, face_centroid);
                                System.Diagnostics.Debug.Assert(boundary.ContainsExt(face_centroid) == OverlapType.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");
                            }
                        }
                        else if (edgeCandidates.Count == 2 || edgeCandidates.Count == 0) ////All edges of the face are part of the medial axis.  Add a vertex at the centroid and connect them all to the centroid
                        {
                            GridTriangle tri = new GridTriangle(mesh[AdjacentFace.iVerts].Select(v => v.Position).ToArray());
                            //GridVector2 face_centroid = mesh.GetCentroid(AdjacentFace);
                            GridVector2 face_centroid = tri.Centroid;
                            //GridVector2 face_centroid = mesh.GetCentroid(AdjacentFace);
                            otherNode = GetOrAddVertex(graph, face_centroid);
                            System.Diagnostics.Debug.Assert(boundary.ContainsExt(face_centroid) == OverlapType.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");
                        }

                        if (otherNode != null)
                        {
                            MedialAxisEdge e = new MedialAxisEdge(node.Key, otherNode.Key);
                            if (!graph.Edges.ContainsKey(e))
                                graph.AddEdge(e);
                        }
                    }

                    /*
                    //Check the faces of this edge for lines to connect to.
                    foreach (var AdjacentEdge in edge.Faces.SelectMany(f => f.Edges.Where(e => e != edge && boundary.IsExteriorOrInteriorSegment(mesh.ToSegment(e)) == false)))
                    {
                        GridLineSegment ConnectedLine = mesh.ToSegment(AdjacentEdge);
                        BorderVertex otherNode = GetOrAddLineBisectorVertex(graph, ConnectedLine);

                        BorderEdge borderEdge = new BorderEdge(node.Key, otherNode.Key);
                        if (!graph.Edges.ContainsKey(borderEdge))
                            graph.AddEdge(borderEdge);
                    }*/
                }
            }

            return graph;
        }

        private static MedialAxisVertex GetOrAddVertex(MedialAxisGraph graph, GridVector2 p)
        {
            if (!graph.Nodes.ContainsKey(p))
            {
                MedialAxisVertex node = new MedialAxisVertex(p);
                graph.AddNode(node);
            }

            return graph.Nodes[p];
        }

        private static MedialAxisVertex GetOrAddLineBisectorVertex(MedialAxisGraph graph, GridLineSegment line)
        {
            GridVector2 midpoint = line.Bisect();
            if (!graph.Nodes.ContainsKey(midpoint))
            {
                MedialAxisVertex node = new MedialAxisVertex(midpoint);
                graph.AddNode(node);
            }

            return graph.Nodes[midpoint];
        }
    }

}
