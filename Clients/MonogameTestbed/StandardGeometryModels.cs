using Geometry;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonogameTestbed
{
    public static class StandardGeometryModels
    {
        public static GridPolygon CreateBoxPolygon(GridRectangle rect)
        {
            GridVector2[] points = new GridVector2[6];

            Array.Copy(rect.Corners, points, 4);
            points[4] = rect.Center;
            points[5] = points[0];

            return new GridPolygon(points);
        }

        public static GridVector2[] CreateTestPolygonExteriorVerticies(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = new GridVector2[] {new GridVector2(10,10),
                                      new GridVector2(5, 20),
                                      new GridVector2(15, 30),
                                      new GridVector2(30, 30),
                                      new GridVector2(25, 15),
                                      new GridVector2(45, 15),
                                      new GridVector2(45, 10),
                                      new GridVector2(55, 0),
                                      new GridVector2(25, 5),
                                      new GridVector2(10, 10)};

            return output;
        }

        public static GridVector2[] CreateTestPolygonInteriorRingVerticies(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = new GridVector2[] {new GridVector2(12.5,12.5),
                                      new GridVector2(22.5, 12.5),
                                      new GridVector2(24.5, 17.5),
                                      new GridVector2(17.5, 25.5),
                                      new GridVector2(12.5, 17.5),
                                     new GridVector2(12.5, 12.5)};

            return output;
        }

        public static GridPolygon CreateTestPolygon(bool IncludeHole, GridVector2? offset = new GridVector2?())
        {
            GridVector2[] holy_cps = CreateTestPolygonExteriorVerticies();
            List<GridVector2[]> listInnerRings = new List<GridVector2[]>();

            if (IncludeHole)
            {
                GridVector2[] holy_hole = CreateTestPolygonInteriorRingVerticies();
                listInnerRings.Add(holy_hole);
            }

            //When I made this I did not center polygon on 0,0, so just recenter after creation for now
            GridPolygon uncentered_poly = new GridPolygon(holy_cps, listInnerRings);
            GridPolygon centered_poly = uncentered_poly.Translate(-uncentered_poly.Centroid);

            if (offset.HasValue)
                return centered_poly.Translate(offset.Value);
            else
                return centered_poly;

        }
    }

    public enum StandardModel
    {
        PolyOverNotchedBox,
        PolyOverNotchedBoxOffset, 
        PolyFourLevelStraightProcess,
        Custom
    }


    public static class StandardModels
    {
        static StandardModels()
        {
            SharedModel = StandardModel.PolyFourLevelStraightProcess;
        }

        private static StandardModel _SharedModel;

        public static StandardModel SharedModel
        {
            get { return _SharedModel; }
            set
            {
                _SharedModel = value;
                UpdateSharedModel(_SharedModel);
            }
        }

        private static GridPolygon[] _SharedModelPolygons;

        public static GridPolygon[] SharedModelPolygons
        {
            get{
                return _SharedModelPolygons;
            }
        }

        private static double[] _SharedModelZ;

        public static double[] SharedModelZ
        {
            get
            {
                return _SharedModelZ;
            }
        }

        private static GraphLib.Edge<ulong>[] _SharedModelEdges;

        public static GraphLib.Edge<ulong>[] SharedModelEdges
        {
            get
            {
                return _SharedModelEdges;
            }
        }

        private static MeshGraph _sharedGraph;

        public static MeshGraph SharedGraph
        {
            get
            {
                return _sharedGraph;
            }

        }

        private static void UpdateSharedModel(StandardModel selection)
        {
            _sharedGraph = null; 
            switch(selection)
            {
                case StandardModel.PolyOverNotchedBox:
                    _SharedModelPolygons = PolygonOverNotchedBox(out _SharedModelZ, out _SharedModelEdges);
                    break;
                case StandardModel.PolyOverNotchedBoxOffset:
                    _SharedModelPolygons = PolygonOverNotchedBoxOffset(out _SharedModelZ, out _SharedModelEdges);
                    break;
                case StandardModel.PolyFourLevelStraightProcess:
                    _SharedModelPolygons = PolygonFourLevelProcess(out _SharedModelZ, out _SharedModelEdges);
                    break;
                case StandardModel.Custom:
                    _SharedModelPolygons = PolygonFromServer(out _SharedModelZ, out _SharedModelEdges, out _sharedGraph);
                    break; 
            }

            if(_sharedGraph == null)
                _sharedGraph = BuildMeshGraph(_SharedModelPolygons, _SharedModelZ, _SharedModelEdges, 10, GridVector3.Zero);
        } 


        public static GridPolygon[] PolygonOverNotchedBox(out double[] Z, out GraphLib.Edge<ulong>[] edges)
        {
            GridPolygon SimpleA = StandardGeometryModels.CreateTestPolygon(false); 
            GridPolygon SimpleB = StandardGeometryModels.CreateBoxPolygon(new GridRectangle(-5, 5, -10, 10));

            Z = new double[] { 0, 10 };

            edges = new MeshEdge[] { new MeshEdge(0, 1) };

            return new GridPolygon[] { SimpleA, SimpleB };
        }

        public static GridPolygon[] PolygonOverNotchedBoxOffset(out double[] Z, out GraphLib.Edge<ulong>[] edges)
        {
            GridPolygon SimpleA = StandardGeometryModels.CreateTestPolygon(false);
            GridPolygon SimpleB = StandardGeometryModels.CreateBoxPolygon(new GridRectangle(-35, -25, -10, 10));

            Z = new double[] { 0, 10 };

            edges = new MeshEdge[] { new MeshEdge(0, 1) };

            return new GridPolygon[] { SimpleA, SimpleB };
        }
        
        public static GridPolygon[] PolygonFourLevelProcess(out double[] Z, out GraphLib.Edge<ulong>[] edges)
        {
            GridPolygon A = StandardGeometryModels.CreateBoxPolygon(new GridRectangle(-5, 5, -10, 10));
            GridPolygon B = StandardGeometryModels.CreateBoxPolygon(new GridRectangle(-5, 5, -10, 10));
            GridPolygon C = StandardGeometryModels.CreateBoxPolygon(new GridRectangle(-5, 5, -10, 10));
            GridPolygon D = StandardGeometryModels.CreateBoxPolygon(new GridRectangle(-5, 5, -10, 10));

            Z = new double[] { 0, 10, 20, 30 };
            edges = new GraphLib.Edge<ulong>[] { new MeshEdge(0,1),
                                                 new MeshEdge(1,2),
                                                 new MeshEdge(2,3) };

            return new GridPolygon[] { A, B, C, D };
        }

        public static GridPolygon[] PolygonFromServer(out double[] Z, out GraphLib.Edge<ulong>[] edges, out MeshGraph mGraph)
        {
            ulong[] TroubleIDS = new ulong[] {
              1333661, //Z = 2
              1333662, //Z = 3
              1333665 //Z =2

            };
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(TroubleIDS, DataSource.EndpointMap[Endpoint.TEST]);

            GridVector2[] centers = graph.Nodes.Values.Select(n => n.Geometry.Centroid()).ToArray();

            foreach(AnnotationVizLib.MorphologyNode node in graph.Nodes.Values)
            {
                node.Geometry = node.Geometry.Translate(-centers[0]);
            }
            
            mGraph = MeshGraphBuilder.ConvertToMeshGraph(graph);
            AnnotationVizLib.MorphologyNode[] nodes = graph.Nodes.Values.ToArray();
            Dictionary<ulong, ulong> NodeIDtoArrayID = new Dictionary<ulong, ulong>();
            for(int i = 0; i < nodes.Length; i++)
            {
                NodeIDtoArrayID.Add(nodes[i].ID, (ulong)i);
            }

            GridPolygon[] Polygons = nodes.Select(n => n.Geometry.ToPolygon()).ToArray();
            Z = nodes.Select(n => n.Z).ToArray();
            edges = mGraph.Edges.Values.Select(e => new MeshEdge(NodeIDtoArrayID[e.SourceNodeKey],
                                                                 NodeIDtoArrayID[e.TargetNodeKey],
                                                                 e.SourcePort,
                                                                 e.TargetPort)).ToArray();
            return Polygons;
        }

        public static MeshGraph BuildMeshGraph(IShape2D[] shapes, double[] ZLevels, GraphLib.Edge<ulong>[] edges, double SectionThickness, GridVector3 translate)
        {
            MeshGraph graph = new MeshGraph
            {
                SectionThickness = SectionThickness
            };

            for (int i = 0; i < shapes.Length; i++)
            {
                MorphologyMesh.MeshNode node = new MeshNode((ulong)i)
                {
                    Mesh = SmoothMeshGraphGenerator.CreateNodeMesh(shapes[i].Translate(translate), ZLevels[i], (ulong)i)
                };
                graph.AddNode(node);
                node.MeshGraph = graph;
                node.CapPortZ = ZLevels[i];
                node.CapPort = SmoothMeshGraphGenerator.CreatePort(shapes[i]);
            }

            foreach (MeshEdge edge in edges)
            {
                if (graph.Nodes.ContainsKey(edge.SourceNodeKey) && graph.Nodes.ContainsKey(edge.TargetNodeKey))
                {
                    edge.SourcePort = SmoothMeshGraphGenerator.CreatePort(shapes[edge.SourceNodeKey], false);
                    edge.TargetPort = SmoothMeshGraphGenerator.CreatePort(shapes[edge.TargetNodeKey], false);
                    graph.AddEdge(edge);
                }
            }

            return graph;
        }
    }

     
}
