using Geometry;
using Geometry.Meshing;
using MIConvexHull;
using MIConvexHullExtensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;


namespace MonogameTestbed
{


    class PolyBranchAssignmentView
    {
        public Polygon[] Polygons = null;
        public double[] PolyZ = null;
        public PointSetView[] PolyPointsView = null;
        public PointSetView MeshVertsView = null;
        private readonly LineSetView TrianglesView = new();

        readonly LineView[] lineViews = null;
        List<LineView> polyRingViews = null;

        readonly BajajGeneratorMesh FirstPassTriangulation = null;
        readonly MeshView<VertexPositionColor> meshView = null;
        readonly MeshModel<VertexPositionColor> meshViewModel = null;

        List<LineSetView> RegionPolygonViews;

        public bool ShowFaces = false;
        public bool ShowPolygons = true;
        public bool ShowRegionPolygons = false;

        public Color Color
        {
            get => TrianglesView.color;
            set => TrianglesView.color = value;
        }

        public PolyBranchAssignmentView(Polygon[] polys, double[] Z)
        {
            Polygons = polys;
            Polygons.AddPointsAtAllIntersections(Z);
            PolyZ = Z;

            UpdatePolyViews();

            //UpdateTriangulation();
            //UpdateMeshView();
        }

        public void UpdatePolyViews()
        {
            List<PointSetView> listPointSetView = [];

            polyRingViews = [];

            foreach (Polygon p in Polygons)
            {
                PointSetView psv = new();

                List<Geometry.Vector2> points = [.. p.ExteriorRing];
                foreach (Polygon innerPoly in p.InteriorPolygons)
                {
                    points.AddRange(innerPoly.ExteriorRing);
                }

                psv.Points = points;

                psv.Color = Color.Random();
                psv.LabelIndex = false;

                psv.UpdateViews();
                listPointSetView.Add(psv);

                Color color = Color.Random();

                polyRingViews.AddRange(p.AllSegments.Select(s => new LineView(s, 0.25, color, LineStyle.Standard)));
            }

            PolyPointsView = [.. listPointSetView];
        }

        /*
        private void BuildAPort(IMesh mesh, Dictionary<Geometry.Vector2, PolygonIndex> pointToPoly)
        {
            List<Geometry.Vector2> points = pointToPoly.Keys.ToList();
            Debug.Assert(mesh.Vertices.Select(v => v.ToVector2()).SequenceEqual(points));

            Mesh3D<IVertex3D<PolygonIndex>> SearchMesh = new Mesh3D<IVertex3D<PolygonIndex>>();

            SearchMesh.AddVerticies(pointToPoly.Keys.Select(v => new Vertex3D<PolygonIndex>(v.ToVector3(0), pointToPoly[v])).ToArray());
            SearchMesh.AddFaces(mesh.Triangles.Select(t => new Face(t.GetVertexID(0), t.GetVertexID(1), t.GetVertexID(2)) as IFace).ToArray()); 
        }
        */
        //Returns the line type for a line with a given midpoint.  The Polygons A & B must be different


        /*
        public void UpdateMeshView()
        { 
            var mesh = Polygons.Triangulate();
            FirstPassTriangulation = PolyBranchAssignmentView.ToMorphRenderMesh(mesh, Polygons, PolyZ);
            MeshVertsView = PointSetView.CreateFor(FirstPassTriangulation);
            FirstPassTriangulation.ClassifyMeshEdges();
            //ReclassifyMeshEdges(FirstPassTriangulation);
            TrianglesView = UpdateMeshLines(FirstPassTriangulation, "");
            lineViews = TrianglesView.LineViews.ToArray();

            FirstPassTriangulation.IdentifyRegionsViaFaces();
            PairOffRegions(FirstPassTriangulation);
            meshViewModel = CreateRegionView(FirstPassTriangulation);
            CreateRegionPolygonViews(FirstPassTriangulation);
            //meshViewModel = CreateFaceView(FirstPassTriangulation);
            meshView = new MeshView<VertexPositionColor>();
            meshView.models.Add(meshViewModel);
        }
        */

        /// <summary>
        /// Creates a MeshModel for the mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private static MeshModel<VertexPositionColor> CreateFaceView(MorphRenderMesh mesh)
        {
            MeshModel<VertexPositionColor> model = new();

            mesh.ConvertAllFacesToTriangles();

            model.Vertices = [.. mesh.Vertices.Select((v, i) => new VertexPositionColor(v.Position.XY().ToXNAVector3(), Color.Transparent))];

            foreach (IFace face in mesh.Faces)
            {
                model.AppendEdges(face.iVerts);

                Color regionColor = VikingXNAGraphics.ColorExtensions.Random();
                foreach (int iVert in face.iVerts)
                {
                    model.Vertices[iVert].Color = regionColor;
                }
            }

            return model;
        }

        internal static MeshModel<VertexPositionColor> CreateRegionView(BajajGeneratorMesh mesh)
        {
            if (mesh.Regions is null)
                return null;

            if (mesh.Regions.Count == 0)
                return null;

            MeshModel<VertexPositionColor> model = new();

            mesh.ConvertAllFacesToTriangles();

            model.Vertices = [.. mesh.Vertices.Select((v, i) => new VertexPositionColor(v.Position.XY().ToXNAVector3(), Color.Transparent))];

            foreach (MorphMeshRegion region in mesh.Regions)
            {
                int[] edgeVerts = [.. region.Faces.SelectMany(f => f.iVerts)];
                model.AppendEdges(edgeVerts);

                Color regionColor = region.Type.GetColor();
                foreach (int iVert in edgeVerts)
                {
                    model.Vertices[iVert].Color = regionColor;
                }
            }

            return model;
        }

        public void CreateRegionPolygonViews(BajajGeneratorMesh mesh)
        {
            List<LineSetView> views = [];

            foreach (MorphMeshRegion region in mesh.Regions)
            {
                Polygon poly = region.Polygon;
                LineSetView lineView = new();
                Color c = Color.Random();
                c.A = 128;
                lineView.LineViews = [.. poly.ExteriorSegments.Select(l => new LineView(l, 0.5, c, LineStyle.Standard))];
                views.Add(lineView);
            }

            this.RegionPolygonViews = views;
        }

        /// <summary>
        /// Pair off nearby regions on adjacent sections to create meshes between
        /// </summary>
        private static Dictionary<MorphMeshRegion, List<MorphMeshRegion>> PairOffRegions(BajajGeneratorMesh mesh)
        {
            MorphMeshRegion[] AllRegions = [.. mesh.Regions.Where(r => r.Type == RegionType.EXPOSED)];
            SortedSet<double> ZLevels = [.. AllRegions.SelectMany(r => r.ZLevel).Distinct()];
            Dictionary<MorphMeshRegion, List<MorphMeshRegion>> RegionToCandidates = [];

            //Identify which regions each region could be matched to
            foreach (MorphMeshRegion region in AllRegions)
            {
                List<MorphMeshRegion> Candidates = [.. AllRegions.Where(r => r.ZLevel.Intersect(region.ZLevel).IsEmpty).OrderBy(c => c.Polygon.Distance(region.Polygon))];
                RegionToCandidates[region] = Candidates;
            }

            return RegionToCandidates;
        }

        /*
        private static void ReclassifyMeshEdges(MorphRenderMesh mesh)
        {
            Polygon[] Polygons = mesh.Polygons;

            foreach (MorphMeshEdge edge in mesh.MorphEdges)
            {
                if((edge.Type & EdgeType.VALID) == 0)
                {
                    continue;
                }

                MorphMeshVertex A = mesh.GetVertex(edge.A);
                MorphMeshVertex B = mesh.GetVertex(edge.B);

                if(!BajajMeshGenerator.Theorem2(Polygons, A.PolyIndex, B.Position.XY()))
                {
                    edge.Type = EdgeType.FLIPPED_DIRECTION;
                }
            }

            return;
        }*/

        /// <summary>
        /// Create a LineSetView that provides a 2D representation of a MorphRenderMesh, showing each line and the line's type.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static LineSetView UpdateMeshLines(MorphRenderMesh mesh, string Name)
        {
            IEdgeKey[] edgeKeys = [.. mesh.Edges.Keys];
            LineSetView TrianglesView = new();
            List<LineView> lineViews = new(edgeKeys.Length);
            //List<CurveLabel> lineLabels = new List<CurveLabel>(edgeKeys.Length);
            List<LabelView> lineLabels = new(edgeKeys.Length);
            List<Geometry.Vector2> labelOffsets = new(edgeKeys.Length);

            const double lineWidth = 1.0;

            foreach (IEdgeKey edgeKey in edgeKeys)
            {
                MorphMeshEdge edge = mesh.GetEdge(edgeKey);

                if (edge.Type == EdgeType.CORRESPONDING) //Avoid creating perfectly vertical lines with the same start and end points
                    continue;

                LineSegment segment = mesh.ToSegment(edgeKey);
                LineView lineView = new(segment, lineWidth, edge.Type.GetColor(), LineStyle.Standard);
                lineViews.Add(lineView);

                /*CurveLabel lineLabel = CurveLabel.CreateLineLabel(edge.Type.ToString(), segment, Color.White, lineWidth: lineWidth);*/
                LabelView lineLabel = new(edge.Type.ToString() + " " + edgeKey.ToString(), segment, Color.White, lineWidth: lineWidth, scaleFontWithScene: true);
                lineLabels.Add(lineLabel);
                labelOffsets.Add(InwardOffsetDirection(mesh, edge, segment));
            }


            TrianglesView.color = Color.Red;
            TrianglesView.LineViews = lineViews;
            TrianglesView.LineLabels = lineLabels;
            TrianglesView.LineLabelOffsetDirections = labelOffsets;
            TrianglesView.Name = Name;
            return TrianglesView;
        }

        /// <summary>
        /// Unit vector perpendicular to <paramref name="segment"/> aimed into a face that uses the edge.  An edge
        /// label centered on the edge straddles the line it names, which reads as belonging to neither side; the
        /// caller offsets it along this direction so it sits on the surface the edge borders instead.
        /// </summary>
        /// <returns>Zero when the edge has no face yet, or the face is degenerate.</returns>
        private static Geometry.Vector2 InwardOffsetDirection(MorphRenderMesh mesh, MorphMeshEdge edge, LineSegment segment)
        {
            //Once the ends are capped a contour edge has a face on both sides: the band between the two slices, and
            //the flat cap closing its own slice.  The cap sits on the far side, so choosing it throws the label off
            //the surface entirely - which is why contour labels landed outside the cell.  Faces that span both
            //slices are the surface the label belongs on, so those win.
            MorphMeshFace face = edge.Faces.FirstOrDefault(f => FaceSpansSlices(mesh, f)) ?? edge.Faces.FirstOrDefault();
            if (face is null)
                return Geometry.Vector2.Zero;

            Geometry.Vector2 centroid = Geometry.Vector2.Zero;
            foreach (int iVert in face.iVerts)
                centroid += mesh[iVert].Position.XY();
            centroid /= face.iVerts.Length;

            //Only the component across the edge moves the label off the line; the component along it would slide
            //the text away from the edge's midpoint.
            Geometry.Vector2 toCentroid = centroid - segment.PointAlongLine(0.5);
            Geometry.Vector2 along = Geometry.Vector2.Normalize(segment.Direction);
            Geometry.Vector2 across = toCentroid - (along * Geometry.Vector2.Dot(toCentroid, along));

            return across.Magnitude <= double.Epsilon ? Geometry.Vector2.Zero : Geometry.Vector2.Normalize(across);
        }

        /// <summary>
        /// True when a face bridges the two slices being tiled rather than lying flat inside one of them, which
        /// separates the tiled surface from the caps that close each slice.
        /// </summary>
        private static bool FaceSpansSlices(MorphRenderMesh mesh, MorphMeshFace face)
        {
            if (face is null || face.iVerts.Length == 0)
                return false;

            double z = mesh[face.iVerts[0]].Position.Z;
            foreach (int iVert in face.iVerts)
            {
                if (mesh[iVert].Position.Z != z)
                    return true;
            }

            return false;
        }



        /*
        public static BajajGeneratorMesh ToMorphRenderMesh(IMesh3D<IVertex3D> mesh, IShape2D[] Shapes, double[] ShapeZ)
        {
            double MinZ = ShapeZ.Min();
            BajajGeneratorMesh output = new BajajGeneratorMesh(Shapes, ShapeZ, IsUpperShape: ShapeZ.Select(Z => Z != MinZ).ToArray());
            BajajMeshGenerator.AddTriangulationEdgesToMesh(mesh, output);
            return output;
        }
        */


        public void Draw(MonoTestbed window, Scene scene)
        {


            if (lineViews != null && ShowPolygons)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, lineViews);
            }

            if (polyRingViews != null && ShowPolygons)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. polyRingViews]);
            }

            if (meshView != null && ShowFaces)
            {
                meshView.Draw(window.GraphicsDevice, window.Scene, CullMode.None);
            }

            if (MeshVertsView != null && ShowFaces)
            {
                MeshVertsView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
            }

            if (RegionPolygonViews != null && ShowRegionPolygons)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. RegionPolygonViews.SelectMany(rpv => rpv.LineViews)]);
            }

            if (PolyPointsView != null && ShowPolygons)
            {
                foreach (PointSetView psv in PolyPointsView)
                {
                    psv.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                }
            }
        }
    }


    /// <summary>
    /// This tests how we create faces that connect two polygons at different Z levels
    /// </summary>
    class BranchAssignmentTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        readonly TestInputContext Input = new();


        static readonly Polygon SimpleA = new([ new(0,0),
                                                                         new(10,0),
                                                                         new(10,10),
                                                                         new(0,10),
                                                                         new(0,0) ]);

        static readonly Polygon SimpleB = new([ new(5,5),
                                                                         new(15,5),
                                                                         new(15,15),
                                                                         new(5,15),
                                                                         new(5,5) ]);


        /*
        long[] TroubleIDS = new long[] {
            82701, //Z: 234
            82881, //Z: 233
            82882,
            82883
            };*/
        /*
    long[] TroubleIDS = new long[] {
      //  58664,
        58666,
        58668
    };
    */
        /*
        //Polygons with internal polygon
        long[] TroubleIDS = new long[] {
          //  58664,
            82617,
            82647,
            82679,

        };
        */


        /*
        //Polygons with internal polygon merging with external concavity
        long[] TroubleIDS = new long[] {
          //  58664,
            82884, //Z: 767
            82908, //Z: 768

        };
        */
        /*
        //Polygons with internal polygon
        long[] TroubleIDS = new long[] {
          //  58664,
            82612, //Z: 756
            82617, //Z: 757 Small Branch
            82647, //Z: 757
            //82679, //Z: 758
            //82620, //Z: 758 Small Branch

        };
        */

        //Polygons with internal polygon merging with external concavity
        readonly long[] TroubleIDS = [
          1333661, //Z = 2
          1333662, //Z = 3
          1333665 //Z =2

        ];
        Scene scene;
        readonly Polygon A;
        readonly Polygon B;
        readonly PointSetViewCollection Points_A = new(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new(Color.Red, Color.Pink, Color.Plum);
        PolyBranchAssignmentView wrapView = null;

        bool _initialized = false;
        public bool Initialized => _initialized;

        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Input.UpdateTrackers();

            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDs(TroubleIDS, DataSource.EndpointMap[ENDPOINT.RPC1]);

            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(TroubleIDS, true, DataSource.EndpointMap[Endpoint.TEST]);
            AnnotationVizLib.MorphologyNode[] nodes = [.. graph.Nodes.Values];
            Polygon[] Polygons = [.. nodes.Select(n => n.Geometry.ToPolygon())];

            //Polygon[] Polygons = new Polygon[] { SimpleA, SimpleB };

            wrapView = new MonogameTestbed.PolyBranchAssignmentView(Polygons, [.. nodes.Select(n => n.Z)]);
            //wrapView = new MonogameTestbed.PolyBranchAssignmentView(Polygons, new double[] { 0, 10 });

            window.Scene.Camera.LookAt = Polygons.BoundingBox().Center.ToXNAVector2();

            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window) => this.scene.SaveCamera(TestMode.BRANCHASSIGNMENT);

        public void Update()
        {
            GamePadState state = Input.Update(scene);

            if (Input.Gamepad.A_Clicked)
            {
                wrapView.ShowFaces = !wrapView.ShowFaces;
            }

            if (Input.Gamepad.B_Clicked)
            {
                wrapView.ShowPolygons = !wrapView.ShowPolygons;
            }

            if (Input.Gamepad.Y_Clicked)
            {
                wrapView.ShowRegionPolygons = !wrapView.ShowRegionPolygons;
            }

            /*
            if(Input.Gamepad.RightShoulder_Clicked)
            {
                wrapView.NumLinesToDraw++;
            }

            if (Input.Gamepad.LeftShoulder_Clicked)
            {
                wrapView.NumLinesToDraw--;
            }

            if (Input.Gamepad.Y_Clicked)
            {
                wrapView.ShowFinalLines = !wrapView.ShowFinalLines;
            }*/
        }

        public void Draw(MonoTestbed window) => wrapView?.Draw(window, scene);
    }
}
