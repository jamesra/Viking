using Geometry;
using Rectangle = Geometry.Rectangle;
using Geometry.JSON;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

//using OTVTable = System.Collections.Concurrent.ConcurrentDictionary<Geometry.PointIndex, Geometry.PointIndex>;
//using SliceChordRTree = RTree.RTree<MorphologyMesh.ISliceChord>;


namespace MonogameTestbed
{
    class RegionView
    {
        public List<LineSetView> PolygonViews;
        public List<LabelView> LabelViews;

        public bool HasGeometry =>
            PolygonViews is { Count: > 0 } && PolygonViews.Exists(v => v.LineViews is { Count: > 0 });

        public void Draw(MonoTestbed window, Scene scene)
        {
            LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. PolygonViews.SelectMany(rpv => rpv.LineViews)]);
            DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
            LabelView.Draw(window.spriteBatch, window.fontArial, scene, LabelViews);
            DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
        }
    }


    class BajajOTVAssignmentView
    {
        public readonly IShape2D[] Shapes = null;
        public readonly double[] ShapeZ = null;
        //public PointSetView[] PolyPointsView = null;
        public PointSetView IncompletedVertexView = null;

        public int? iShownLineView = null;
        public List<LineSetView> listLineViews = [];
        public bool ShowLines => iShownLineView.HasValue;

        //private LineSetView lineViews = new LineSetView();
        //private LineSetView unfiltered_lineViews = new LineSetView();
        //List<LineView> polyRingViews = null;
        public PointSetView MeshVertsView = null;

        readonly TriangulationView triView = null;

        PolygonSetView PolyViews;
        List<LineView> OTVTableView = null;

        BajajGeneratorMesh FirstPassTriangulation = null;

        public List<RegionView> RegionViews = [];

        public CullMode CullMode = CullMode.CullCounterClockwiseFace;

        public int? iShownMesh = null;
        public List<MeshView<VertexPositionColor>> MeshViews = [];


        public bool ShowMesh => iShownMesh.HasValue;

        readonly MeshModel<VertexPositionColor> meshViewModel = null;

        //LineView[] lineViews = null;

        public int? iShownRegion = null;
        readonly List<LineSetView> RegionPolygonViews;
        readonly List<LabelView> RegionLabelViews;

        public bool ShowFaces = false;
        public bool ShowPolygons = true;
        public bool ShowRegionPolygons => iShownRegion.HasValue;

        public bool ShowCompletedVerticies = true;
        public bool ShowAllEdges = false;

        public object ViewsLock = new();

        //Every annotation size below is a world measurement, and the camera is fitted to the slice, so a large
        //slice makes the constructor defaults sub-pixel.  These are the sizes we want on screen instead, in
        //pixels, so a slice reads the same whether it spans 200nm or 20um and whatever the capture resolution is.
        private const double VertexRadiusPixels = 4.0;
        private const double LineWidthPixels = 2.0;
        private const double RegionEdgePixels = 3.5;
        private const double LabelHeightPixels = 15.0;

        /// <summary>
        /// Viewport height the pixel sizes above were chosen against.  Screenshots are captured fullscreen at the
        /// monitor's native resolution, so a fixed pixel size would occupy a steadily smaller share of a larger
        /// frame.  LabelView also hides any text below 1/200th of the viewport height, so text in particular has
        /// to grow with the frame to stay drawn at all.
        /// </summary>
        private const double ReferenceViewportHeight = 1200.0;

        /// <summary>
        /// Reference height used while capturing screenshots.  A capture is reviewed as an image file, and the
        /// readers of those files (including image-capable tools) routinely rescale a 2560x1440 frame down to
        /// around a thousand pixels wide, which takes 15px text below the point of being readable.  Sizing
        /// against a frame this small makes annotations survive that reduction.
        /// </summary>
        private const double CaptureReferenceViewportHeight = 576.0;

        /// <summary>
        /// Smallest index label we are willing to draw before giving up on it entirely.
        /// </summary>
        private const double MinLabelHeightPixels = 6.0;

        /// <summary>
        /// Share of an edge's length its label may span.  Text running the full length reaches the vertices at both
        /// ends, where it collides with the labels of every other edge meeting there, so it is held short of them.
        /// </summary>
        private const double EdgeLabelLengthFraction = 0.6;

        private double _scaledForWorldPerPixel;
        private int _scaledForViewportHeight;

        /// <summary>
        /// Labels shrunk below this world size are skipped instead of drawn as unreadable smudges.
        /// </summary>
        private double _minLabelWorldSize;

        /// <summary>
        /// The list of currently-enabled sub-views, rebuilt every Draw. Exposed so the legend HUD can describe
        /// the active view state.
        /// </summary>
        public string LastViewLabels { get; private set; } = string.Empty;

        public IndexLabelType VertexLabelType
        {
            get => PolyViews?.PointLabelType ?? IndexLabelType.NONE;
            set
            {
                if (PolyViews is not null)
                    PolyViews.PointLabelType = value;
            }
        }

        public bool ShowPolyIndexLabels => PolyViews?.LabelPolygonIndex ?? false;

        public bool ShowMeshIndexLabels => PolyViews?.LabelIndex ?? false;


        public bool ShowPolyPositionLabels => PolyViews?.LabelPosition ?? false;

        readonly System.Threading.Tasks.Task BajajMeshGenerationTask = null;

        public bool ShowOtvChords { get; set; }

        public bool IsMeshGenerationFinished => BajajMeshGenerationTask is null || BajajMeshGenerationTask.IsCompleted;

        public bool IsMeshFaulted => BajajMeshGenerationTask?.IsFaulted == true;

        public Exception MeshFault => BajajMeshGenerationTask?.Exception?.GetBaseException();

        public BajajOTVAssignmentView(AnnotationVizLib.MorphologyGraph graph)
        {
            Trace.WriteLine("Begin Simplification of Polygons");
            SliceGraph sliceGraph = SliceGraph.Create(graph, 2.0).Result;
            Trace.WriteLine("End Simplification of Polygons");

            Debug.Assert(sliceGraph.Nodes.Count == 1, "Test was written expecting a single slice");

            Slice slice = sliceGraph.Nodes.Values.First();

            SliceTopology topology = sliceGraph.GetTopology(slice);

            Shapes = topology.Shapes;
            ShapeZ = topology.ShapeZ;

            //BajajGeneratorMesh.AddCorrespondingVertices(Polygons);

            BajajMeshGenerationTask = System.Threading.Tasks.Task.Run(() =>
            {
                //Create our mesh with only the verticies
                FirstPassTriangulation = new BajajGeneratorMesh(topology, slice);//Polygons, PolyZ, IsUpper, slice);
                GenerateMesh(FirstPassTriangulation);
            });
        }

        public BajajOTVAssignmentView(SliceGraph sliceGraph, Slice slice)
        {
            SliceTopology topology = sliceGraph.GetTopology(slice);

            Shapes = topology.Shapes;
            ShapeZ = topology.ShapeZ;

            //Create our mesh with only the verticies
            PolyViews = new PolygonSetView(Shapes.Select(s => s as Polygon), PolygonSetView.DefaultColorMapping)
            {
                PointLabelType = IndexLabelType.MESH
            };

            //BajajGeneratorMesh.AddCorrespondingVertices(Polygons);

            BajajMeshGenerationTask = System.Threading.Tasks.Task.Run(() =>
            {
                FirstPassTriangulation = new BajajGeneratorMesh(topology, slice);
                GenerateMesh(FirstPassTriangulation);
            });
        }

        public BajajOTVAssignmentView(IShape2D[] shapes, double[] Z)
        {
            ///Takes a set of polygons and Z values and generates a meshView
            //Polygons = polys.Select(p => p.Simplify(2.0)).ToArray();
            Shapes = [.. shapes.Select(p => p)];
            double MinZ = Z.Min(); //Translate our Z values to an origin of 0 so we can render meshes in 2D easily
            ShapeZ = [.. Z.Select(z => z - MinZ)];
            //Bajaj Step 3
            //Polygons.AddPointsAtAllIntersections(Z);
            //Create our mesh with only the verticies

            BajajMeshGenerationTask = System.Threading.Tasks.Task.Run(() =>
            {
                //Create our mesh with only the verticies
                FirstPassTriangulation = new BajajGeneratorMesh(Shapes, ShapeZ, [.. ShapeZ.Select(z_ => z_ != MinZ)]);
                GenerateMesh(FirstPassTriangulation);
            });
        }


        private Geometry.Vector2 VertexPositionAverage = Geometry.Vector2.Zero;

        /// <summary>
        /// Display progress as we triangulate the polygons. 
        /// The polygons were translated, so we need to restore them to the original positions
        /// </summary>
        /// <param name="mesh"></param>
        private void OnTriangulationProgress(TriangulationMesh<Vertex2D<List<int>>> mesh)
        {
            triView?.OnTriangulationProgress(mesh);
            System.Threading.Thread.Sleep(0);
        }

        private void OnTriangulateRegionProgress(TriangulationMesh<IVertex2D<int>> mesh)
        {
            triView?.OnTriangulationProgress(mesh);
            System.Threading.Thread.Sleep(0);
        }

        private void OnSecondPassRegionProgress(TriangulationMesh<IVertex2D<PolygonIndex>> mesh)
        {
            triView?.OnTriangulationProgress(mesh);
            System.Threading.Thread.Sleep(0);
        }

        /*
        double lineWidth = 1;
        //Ensure we have a view of the current triangulation
        if(listLineViews.Count == 0)
        {
            listLineViews.Add(LineSetView.Create(mesh, Color.Black.SetAlpha(0.5f), linewidth: lineWidth));
            iShownLineView = 0;

            //Vertices never change, so set them on creation and leave them.
            foreach (LabelView label in listLineViews[0].LineLabels)
            {
                label.Position += VertexPositionAverage;
                label.Color = Color.LightBlue;
            }
        }
        else
        {
            //Vertices never change, so only recreate the lines

            //listLineViews[0] = LineSetView.Create(mesh, Color.Black, linewidth: 3);
            listLineViews[0].LineViews = LineSetView.CreateLineList(mesh, Color.Black.SetAlpha(0.5f), linewidth: lineWidth * 2);
        }

        foreach(LineView line in listLineViews[0].LineViews)
        {
            line.Position += VertexPositionAverage;
        }
        */

        //listLineViews[0].Name = "Current Triangulation";

        //} 

        internal void GenerateMesh(BajajGeneratorMesh FirstPassTriangulation)
        {
            string JSONPolyString = Shapes.ToJArray().ToString();
            Trace.WriteLine(JSONPolyString);

            lock (ViewsLock)
            {
                this.RegionViews.Clear();
                this.listLineViews.Clear();
                this.MeshViews.Clear();

                //Reset the average vertex position in case input changed from the last mesh
                VertexPositionAverage = FirstPassTriangulation.CalculateAverageVertexPositionXY();

                //Create our mesh with only the verticies
                PolyViews = new PolygonSetView(Shapes.Select(s => s as Polygon), PolygonSetView.DefaultColorMapping, 2)
                {
                    PointLabelType = IndexLabelType.MESH
                };
                if (this.VertexLabelType == IndexLabelType.NONE)
                    this.VertexLabelType = IndexLabelType.MESH;

                this.MeshVertsView = PointSetView.CreateFor(FirstPassTriangulation);
            }

            InvalidateAnnotationScale();

            //UpdatePolyViews();

            string temp = FirstPassTriangulation.Vertices.Select(v => v.Position.XY()).Distinct().ToJSON();
            Trace.WriteLine(temp);
            BajajMeshGenerator.AddDelaunayEdges(FirstPassTriangulation, OnProgress: null);

            AddLineView(FirstPassTriangulation, "FirstPassDelaunay");

            var RegionPairingGraph = BajajMeshGenerator.GenerateRegionGraph(FirstPassTriangulation);

            FirstPassTriangulation.RemoveInvalidEdges();

            AddLineView(FirstPassTriangulation, "Remove Invalid Edges");
            AddMeshView(FirstPassTriangulation, "Remove Invalid Edges");

            BajajMeshGenerator.CompleteCorrespondingVertexFaces(FirstPassTriangulation);
            AddLineView(FirstPassTriangulation, "CompleteCorrespondingVertexFaces");
            AddMeshView(FirstPassTriangulation, "CompleteCorrespondingVertexFaces");
            AddRegionView(CreateRegionPolygonViews(FirstPassTriangulation));

            SliceChordRTree rTree = FirstPassTriangulation.CreateChordTree(ShapeZ);
            List<OTVTable> listOTVTables = RegionPairingGraph.MergeAndCloseRegionsPass(FirstPassTriangulation, rTree, OnTriangulateRegionProgress);

            AddMeshView(FirstPassTriangulation, "MergeAndCloseRegionsPass");
            AddLineView(FirstPassTriangulation, "MergeAndCloseRegionsPass");

            var IncompleteVerticies = BajajMeshGenerator.IdentifyIncompleteVerticies(FirstPassTriangulation);

            PointSetView incompleteView = CreateCompletedVertexView(IncompleteVerticies, Color.DarkRed);
            incompleteView.LabelIndex = false;
            incompleteView.LabelPosition = false;
            PointSetView meshVerts = PointSetView.CreateFor(FirstPassTriangulation);
            lock (ViewsLock)
            {
                IncompletedVertexView = incompleteView;
                this.MeshVertsView = meshVerts;
                CreateChordViews(FirstPassTriangulation, listOTVTables);
            }

            InvalidateAnnotationScale();

            List<MorphMeshVertex> FirstPassIncompleteVerticies = BajajMeshGenerator.FirstPassSliceChordGeneration(FirstPassTriangulation, ShapeZ);

            AddMeshView(FirstPassTriangulation, "FirstPassSliceChordGeneration");
            AddLineView(FirstPassTriangulation, "FirstPassSliceChordGeneration");

            BajajMeshGenerator.FirstPassFaceGeneration(FirstPassTriangulation, FirstPassIncompleteVerticies);

            FirstPassIncompleteVerticies = BajajMeshGenerator.IdentifyIncompleteVerticies(FirstPassTriangulation);

            AddMeshView(FirstPassTriangulation, "FirstPassFaceGeneration");

            MorphMeshRegionGraph SecondPassRegions = MorphRenderMesh.SecondPassRegionDetection(FirstPassTriangulation, FirstPassIncompleteVerticies, OnSecondPassRegionProgress);
            AddRegionView(CreateRegionPolygonViews(FirstPassTriangulation, SecondPassRegions.Nodes.Keys));

            SecondPassRegions.MergeAndCloseRegionsPass(FirstPassTriangulation, rTree, OnTriangulateRegionProgress);

            AddMeshView(FirstPassTriangulation, "Second MergeAndCloseRegionsPass");
            AddLineView(FirstPassTriangulation, "Second MergeAndCloseRegionsPass");

            // Match BajajMeshGenerator.GenerateFaces: only cap open stack ends, not interior slice pairs.
            if (FirstPassTriangulation.Slice?.HasSliceAbove == false)
            {
                FirstPassTriangulation.CapMeshEnd(true, OnTriangulateRegionProgress);
                AddMeshView(FirstPassTriangulation, "Cap upper polygons");
                AddLineView(FirstPassTriangulation, "Cap upper polygons");
            }

            if (FirstPassTriangulation.Slice?.HasSliceBelow == false)
            {
                FirstPassTriangulation.CapMeshEnd(false, OnTriangulateRegionProgress);
                AddMeshView(FirstPassTriangulation, "Cap lower polygons");
                AddLineView(FirstPassTriangulation, "Cap lower polygons");
            }

            FirstPassTriangulation.EnsureFacesHaveExternalNormals();
            FirstPassTriangulation.RecalculateNormals();

            AddLineView(FirstPassTriangulation, "Second MergeAndCloseRegionsPass");

            //Seed the interactive selection now that every view exists.  Without this nothing is selected, so Draw
            //skips every branch and the window stays empty until the user steps to a shot.  The screenshot path
            //assigns shot indices directly afterwards, so it is unaffected.
            CheckViewIndexBoundaries();
        }

        private void AddLineView(BajajGeneratorMesh mesh, string name)
        {
            LineSetView view = PolyBranchAssignmentView.UpdateMeshLines(mesh, name);
            lock (ViewsLock)
                listLineViews.Add(view);

            InvalidateAnnotationScale();
        }

        private void AddMeshView(BajajGeneratorMesh mesh, string name)
        {
            try
            {
                MeshView<VertexPositionColor> view = CreateMeshView(mesh, name);
                lock (ViewsLock)
                    MeshViews.Add(view);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AddMeshView({name}) failed: {ex}");
            }
        }

        private void AddRegionView(RegionView view)
        {
            lock (ViewsLock)
                RegionViews.Add(view);

            InvalidateAnnotationScale();
        }

        /// <summary>
        /// Forces the next draw to re-apply annotation sizes.  Views are published by the meshing task while the
        /// camera sits still, so without this a view created after a scale pass keeps its sub-pixel default.
        /// </summary>
        private void InvalidateAnnotationScale() => _scaledForWorldPerPixel = 0;

        /// <summary>
        /// Resizes every annotation view so it covers a constant number of screen pixels.  Recomputed only when
        /// the zoom actually changes, because resizing a point set rebuilds its circles and labels.
        /// </summary>
        private void ScaleAnnotationsToScene(Scene scene)
        {
            //Downsample is world units per pixel by construction: Scene builds its orthographic volume as
            //Viewport dimensions * Downsample.  Reading it avoids the lazily rebuilt VisibleWorldBounds that
            //ScreenPixelSizeInVolume goes through, which takes a semaphore.
            double worldPerPixel = scene?.Camera?.Downsample ?? 0;
            if (worldPerPixel <= 0 || double.IsNaN(worldPerPixel) || double.IsInfinity(worldPerPixel))
                return;

            int viewportHeight = Math.Max(1, scene.Viewport.Height);
            if (Math.Abs(worldPerPixel - _scaledForWorldPerPixel) <= worldPerPixel * 1e-6 &&
                viewportHeight == _scaledForViewportHeight)
                return;

            _scaledForWorldPerPixel = worldPerPixel;
            _scaledForViewportHeight = viewportHeight;

            //World units per pixel of the size we want, grown so a larger capture keeps the same proportions.
            double reference = Program.options?.Screenshots == true ? CaptureReferenceViewportHeight : ReferenceViewportHeight;
            double unit = worldPerPixel * Math.Max(1.0, viewportHeight / reference);

            double vertexRadius = VertexRadiusPixels * unit;
            double lineWidth = LineWidthPixels * unit;
            double regionEdge = RegionEdgePixels * unit;
            double labelSize = LabelHeightPixels * unit;

            //Labels shrunk to fit their own geometry can end up too small to read.  Drawing them anyway produces a
            //grey mush, so a label below this size is dropped; the marker or edge underneath still shows.  The
            //floor also has to clear LabelView's own rule of hiding text below 1/200th of the viewport height.
            _minLabelWorldSize = Math.Max(MinLabelHeightPixels, viewportHeight / 180.0) * worldPerPixel;

            lock (ViewsLock)
            {
                PolyViews?.SetDrawScale(vertexRadius, lineWidth, labelSize);
                ScaleVertexLabels(MeshVertsView, vertexRadius, labelSize);

                //Only a handful of incomplete vertices are ever marked, so they keep the readable size.
                ScalePoints(IncompletedVertexView, vertexRadius, labelSize);

                foreach (LineSetView view in listLineViews)
                    ScaleLines(view, lineWidth, labelSize);

                foreach (RegionView region in RegionViews)
                {
                    foreach (LineSetView view in region.PolygonViews ?? [])
                        ScaleLines(view, regionEdge, labelSize);

                    ScaleLabels(region.LabelViews, labelSize);
                }

                foreach (LineSetView view in RegionPolygonViews ?? [])
                    ScaleLines(view, regionEdge, labelSize);

                ScaleLabels(RegionLabelViews, labelSize);

                foreach (LineView line in OTVTableView ?? [])
                    line.LineWidth = (float)lineWidth;
            }
        }

        /// <summary>
        /// Largest font, in world units, that keeps <paramref name="text"/> inside <paramref name="room"/> world
        /// units of space.  Glyphs average about 0.55 em wide, so a label needs that much room per character.
        /// </summary>
        /// <summary>
        /// True when a label survived shrinking at a size still worth reading.
        /// </summary>
        private bool IsLegible(LabelView label) => label != null && label.FontSize >= _minLabelWorldSize;

        private static double LabelSizeToFit(string text, double room, double maxSize)
        {
            int chars = Math.Max(1, text?.Length ?? 1);
            return Math.Min(maxSize, room / (0.55 * chars));
        }

        private static void ScalePoints(PointSetView view, double radius, double labelSize)
        {
            if (view is null)
                return;

            //Assigning the radius rebuilds the labels and sizes them from it, so the text size is applied after.
            view.PointRadius = radius;
            ScaleLabels(view.LabelViews, labelSize);
        }

        /// <summary>
        /// Sizes the mesh vertex labels individually.  Vertices crowd together along the contour rings rather than
        /// spreading evenly, so each label is fitted to the gap between its own point and the next one instead of
        /// every label sharing one size derived from the average.
        /// </summary>
        private static void ScaleVertexLabels(PointSetView view, double radius, double maxLabelSize)
        {
            if (view is null)
                return;

            view.PointRadius = radius;

            LabelView[] labels = view.LabelViews;
            if (labels is null || labels.Length == 0)
                return;

            //Mesh vertex order is not spatial, so the room a label has is its distance to the nearest other vertex
            //rather than to the next one in the list.
            QuadTree<int> tree = new();
            bool empty = true;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] is null)
                    continue;

                //Coincident verticies are common where shapes correspond, and the tree rejects a second point at the
                //same place.  Its idea of "same place" is a tolerance rather than exact equality, so the tree has to
                //be the one asked; comparing positions ourselves let near-coincident labels through and threw.
                if (!empty && NearestDistance(tree, labels[i].Position) <= Geometry.Global.Epsilon)
                    continue;

                tree.Add(labels[i].Position, i);
                empty = false;
            }

            foreach (LabelView label in labels)
            {
                if (label is null)
                    continue;

                double gap = 0;
                foreach (var candidate in tree.FindNearestPoints(label.Position, 2))
                {
                    if (candidate.Distance > Geometry.Global.Epsilon)
                    {
                        gap = candidate.Distance;
                        break;
                    }
                }

                //A lone vertex, or one sharing its position with others, gets the full size.  PointSetView already
                //offsets coincident labels onto separate lines so they do not pile up.
                label.FontSize = gap <= 0 ? maxLabelSize : LabelSizeToFit(label.Text, gap, maxLabelSize);
            }
        }

        /// <summary>
        /// Distance from <paramref name="position"/> to the closest point already in <paramref name="tree"/>.
        /// </summary>
        private static double NearestDistance(QuadTree<int> tree, Geometry.Vector2 position)
        {
            foreach (var candidate in tree.FindNearestPoints(position, 1))
                return candidate.Distance;

            return double.MaxValue;
        }

        /// <summary>
        /// Sizes each edge label to the edge it annotates.  A label is drawn rotated along its segment, so the
        /// segment's own length is the room it has, and short edges shrink their text instead of the whole view
        /// dropping to one size that either overlaps everywhere or is illegible everywhere.
        /// </summary>
        private static void ScaleLines(LineSetView view, double width, double maxLabelSize)
        {
            if (view is null)
                return;

            view.LineRadius = width;
            foreach (LineView line in view.LineViews ?? [])
                line.LineWidth = (float)width;

            List<LabelView> labels = view.LineLabels;
            List<LineView> lines = view.LineViews;
            if (labels is null)
                return;

            //UpdateMeshLines appends a label for every line it adds, so the two lists run in step.
            bool perEdge = lines != null && lines.Count == labels.Count;
            List<Geometry.Vector2> offsets = view.LineLabelOffsetDirections;
            bool haveOffsets = offsets != null && offsets.Count == labels.Count;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelView label = labels[i];
                if (label is null)
                    continue;

                if (!perEdge)
                {
                    label.FontSize = maxLabelSize;
                    continue;
                }

                LineView line = lines[i];
                Geometry.Vector2 a = new(line.Source.X, line.Source.Y);
                Geometry.Vector2 b = new(line.Destination.X, line.Destination.Y);
                label.FontSize = LabelSizeToFit(label.Text, Geometry.Vector2.Distance(a, b) * EdgeLabelLengthFraction, maxLabelSize);

                //Re-seat the label each time the size changes, since how far it has to clear the edge depends on
                //how tall the text ended up.
                if (haveOffsets)
                    label.Position = ((a + b) / 2.0) + (offsets[i] * label.FontSize * 0.7);
            }
        }

        private static void ScaleLabels(IEnumerable<LabelView> labels, double fontSize)
        {
            foreach (LabelView label in labels ?? [])
                label.FontSize = fontSize;
        }

        private void CheckViewIndexBoundaries()
        {
            lock (ViewsLock)
            {
                ViewIndex.ClampOrClear(ref iShownRegion, RegionViews.Count);
                iShownLineView ??= ViewIndex.LastOrNull(listLineViews.Count);
                iShownMesh ??= ViewIndex.LastOrNull(MeshViews.Count);
                ViewIndex.ClampOrClear(ref iShownLineView, listLineViews.Count);
                ViewIndex.ClampOrClear(ref iShownMesh, MeshViews.Count);
            }
        }



        public static RegionView CreateRegionPolygonViews(BajajGeneratorMesh mesh, IEnumerable<MorphMeshRegion> regions = null)
        {
            regions ??= mesh.Regions;

            List<LineSetView> views = [];
            List<LabelView> label_views = [];

            foreach (MorphMeshRegion region in regions)
            {
                Polygon poly = region.Polygon;
                LineSetView lineView = new();
                Color c = region.Type.GetColor();
                c.A = 128;
                lineView.LineViews = [.. poly.ExteriorSegments.Select(l => new LineView(l, 4, c, LineStyle.Standard))];
                views.Add(lineView);

                label_views.Add(new LabelView(region.ToString(), poly.Centroid));
            }

            RegionView regionView = new()
            {
                PolygonViews = views,
                LabelViews = label_views
            };

            return regionView;
        }

        public static MeshView<VertexPositionColor> CreateMeshView(BajajGeneratorMesh mesh, string name)
        {
            MeshModel<VertexPositionColor> meshViewModel = CreateFaceView(mesh);

            //Adjust the meshViewModel Z coordinates so we can see the mesh in 2D


            double maxZ = mesh.ShapeZ.Max();
            double minZ = mesh.ShapeZ.Min();
            double ZRange = maxZ - minZ;

            meshViewModel.ModelMatrix = Microsoft.Xna.Framework.Matrix.CreateTranslation(new Microsoft.Xna.Framework.Vector3(0, 0, -(float)mesh.BoundingBox.CenterPoint.Z)) * Microsoft.Xna.Framework.Matrix.CreateScale(1, 1, 1f / (float)ZRange);//).ToXNAVector3());

            /*
                        for (int iVert =0; iVert < meshViewModel.Vertices.Length;iVert++)
                        {
                            double Z = meshViewModel.Vertices[iVert].Position.Z;
                            Z = (Z - minZ) / ZRange;
                            meshViewModel.Vertices[iVert].Position.Z = (float)Z;
                        }
                        */

            MeshView<VertexPositionColor> meshView = new()
            {
                Name = name
            };
            meshView.models.Add(meshViewModel);
            return meshView;
        }

        public static MeshView<VertexPositionColor> CreateMeshView(ICollection<MorphRenderMesh> meshes, string name)
        {

            MeshView<VertexPositionColor> meshView = new()
            {
                Name = name
            };

            foreach (MorphRenderMesh mesh in meshes)
            {
                MeshModel<VertexPositionColor> meshViewModel = CreateFaceView(mesh);
                meshView.models.Add(meshViewModel);
            }

            Trace.WriteLine(string.Format("{0} models rendered", meshView.models.Count));
            return meshView;
        }

        public static void UpdateMeshView()
        {
            /*
            UpdateMeshVertView(FirstPassTriangulation);
            ClassifyMeshEdges(FirstPassTriangulation);
            //ReclassifyMeshEdges(FirstPassTriangulation);
            UpdateMeshLines(FirstPassTriangulation);
            lineViews = TrianglesView.LineViews.ToArray();

            FirstPassTriangulation.IdentifyRegions();
            PairOffRegions(FirstPassTriangulation);
            meshViewModel = CreateRegionView(FirstPassTriangulation);
            CreateRegionPolygonViews(FirstPassTriangulation);
            //meshViewModel = CreateFaceView(FirstPassTriangulation);
            meshView = new MeshView<VertexPositionColor>();
            meshView.models.Add(meshViewModel);
            */
        }

        public static PointSetView CreateCompletedVertexView(List<MorphMeshVertex> verticies, Color color)
        {
            PointSetView psv = new()
            {
                Color = color,
                LabelIndex = true,
                PointRadius = 1.25,
                Points = [.. verticies.Select(v => v.Position.XY())]
            };
            return psv;
        }

        /*
        public void UpdatePolyViews()
        {
            List<PointSetView> listPointSetView = new List<PointSetView>();

            polyRingViews = new List<LineView>();

            foreach (Polygon p in Polygons)
            {
                PointSetView psv = new PointSetView();

                List<Geometry.Vector2> points = p.ExteriorRing.ToList();
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

                polyRingViews.AddRange(p.AllSegments.Select(s => new LineView(s, 1, color, LineStyle.Standard, false)));
            }

            PolyPointsView = listPointSetView.ToArray();
        }
        */

        private static Color GetVertColor(MorphRenderMesh mesh, MorphMeshVertex v, float alpha = 1f)
        {
            if (v.MedialAxisIndex.HasValue)
                return Color.MediumPurple.SetAlpha(alpha);

            if (v.Corresponding.HasValue)
                return Color.DarkSlateBlue.SetAlpha(alpha);

            if (v.ShapeIndex is null)
                return Color.Aqua.SetAlpha(alpha); //This should never happen at the time I'm writing this code.

            if (v.IsFaceSurfaceComplete(mesh))
                if (mesh.IsUpperShape[v.ShapeIndex.ShapeIndex])// Position.Z == mesh.BoundingBox.minVals[2])
                    return Color.LimeGreen.SetAlpha(alpha);
                else
                    return Color.ForestGreen.SetAlpha(alpha);

            if (mesh.IsUpperShape[v.ShapeIndex.ShapeIndex])
                return Color.Orange.SetAlpha(alpha);
            else
                return Color.Red.SetAlpha(alpha);

        }

        internal static MeshModel<VertexPositionColor> CreateFaceView(MorphRenderMesh mesh)
        {
            if (mesh.Faces is null)
                return null;


            MeshModel<VertexPositionColor> model = new()
            {

                //double MinZ = mesh.BoundingBox.minVals[2];
                //double MaxZ = mesh.BoundingBox.maxVals[2];


                Vertices = [.. mesh.Vertices.Select((v, i) => new VertexPositionColor(v.Position.ToXNAVector3(), GetVertColor(mesh, v)))] //Color.Orange.SetAlpha(0.5f) /*ColorExtensions.CreateGrayscale((v.Position.Z - MinZ) / (MaxZ - MinZ))*/)).ToArray();
            };

            foreach (IFace face in mesh.Faces)
            {
                model.AppendEdges(face.iVerts);

                /*Color regionColor = Color.Gold;
                foreach (int iVert in face.iVerts)
                {
                    model.Vertices[iVert].Color = regionColor;
                }*/
            }
            /*
            foreach(MorphMeshEdge edge in mesh.MorphEdges)
            {
                Color color = edge.Type.GetColor();

                //TEMP
                color = color.SetAlpha(1.0f);

                model.Vertices[edge.A].Color = color;
                model.Vertices[edge.B].Color = color;
            }*/

            return model;
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

        /*
        /// <summary>
        /// Find a partner for all regions
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Dictionary<MorphMeshRegion, MorphMeshRegion> PairRegions(MorphRenderMesh mesh)
        {
            Dictionary<MorphMeshRegion, MorphMeshRegion> Pairs = new Dictionary<MorphMeshRegion, MorphMeshRegion>();

            foreach (MorphMeshRegion region in mesh.Regions)
            {
                MorphMeshRegion partner = FindRegionPartner(mesh, region);
                if (partner != null)
                {
                    Pairs[region] = partner;
                }
            }

            return Pairs;
        }

        /// <summary>
        /// Find the region that has the most edges to the passed region using the edges in the mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        private static MorphMeshRegion FindRegionPartner(MorphRenderMesh mesh, MorphMeshRegion region)
        {
            MorphMeshVertex[] verts = region.Vertices.Select(i => (MorphMeshVertex)mesh.Vertices[i]).ToArray();
            SortedSet<int> RegionVerts = new SortedSet<int>(region.Vertices);
            IEdgeKey[] edgeKeys = verts.SelectMany(v => v.Edges).ToArray();
            MorphMeshEdge[] edges = edgeKeys.Select(key => mesh[key]).Where(edge => edge.Type != EdgeType.CONTOUR).ToArray();

            //Identify all edges connected to the region
            MorphMeshEdge[] ConnectingEdges = edges.Where(e => RegionVerts.Contains(e.A)).Union(edges.Where(e => RegionVerts.Contains(e.B))).ToArray();
            //List all of the verticies the edges of our region connect to
            int[] LinkedVerts = ConnectingEdges.Select(e => RegionVerts.Contains(e.A) ? e.B : e.A).ToArray();
            //int[] LinkedVerts = edges.Where(e => RegionVerts.Contains(e.A)).Select(e => e.B).Union(edges.Where(e => RegionVerts.Contains(e.B)).Select(e => e.A)).ToArray();

            
            int MaxLinks = 0;
            MorphMeshRegion BestLink = null;
            foreach (MorphMeshRegion other in mesh.Regions.Where(r => region != r && region.Type.IsValidPair(r.Type) && r.Z != region.Z))
            {
                SortedSet<int> OtherRegionVerts = new SortedSet<int>(other.Vertices);
                int Count = LinkedVerts.Where(lv => OtherRegionVerts.Contains(lv)).Count();
                if (Count > MaxLinks)
                {
                    BestLink = other;
                    MaxLinks = Count;
                }
            }

            return BestLink;
        }
        

        public static OTVTable IdentifyChordCandidatesForRegionPair(BajajGeneratorMesh mesh, MorphMeshRegion source, MorphMeshRegion target, SliceChordTestType Tests, SliceChordRTree rTree = null)
        {
            if(rTree is null)
            {
                rTree = mesh.CreateChordTree(source.ZLevel.Union(target.ZLevel));
            }

            OTVTable Table = new OTVTable(); 

            //TODO: Add flags to this call to select which tests are used to built the OTV table
            BajajMeshGenerator.CreateOptimalTilingVertexTable(source.Vertices.Select(i => ((MorphMeshVertex)mesh[i]).PolyIndex.Value), target.Vertices.Select(i => ((MorphMeshVertex)mesh[i]).PolyIndex.Value), mesh.Polygons, mesh.IsUpperPolygon,
                                                                                            Tests, out Table, ref rTree);
             
            return Table;
        }
        */

        private void CreateChordViews(MorphRenderMesh mesh, List<OTVTable> OTVTables)
        {
            this.OTVTableView ??= [];

            foreach (var OTVTable in OTVTables)
            {
                List<LineView> ChordView = CreateChordView(mesh, OTVTable, ColorExtensions.Random());
                this.OTVTableView.AddRange(ChordView);
            }
        }


        /// <summary>
        /// Return true if the chord could be added to the mesh without conflicting with any existing geometry in the mesh and 
        /// if the chord describes a valid EdgeType
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="sc"></param>
        /// <param name="ChordRTree"></param>
        /// <returns></returns>
        private static bool CouldAddSliceChord(BajajGeneratorMesh mesh, SliceChord sc, SliceChordRTree ChordRTree, SliceChordTestType Tests, out SliceChordTestType failures) => BajajMeshGenerator.IsSliceChordValid(sc.Origin, mesh.Shapes, mesh.GetSameLevelShapes(sc), mesh.GetAdjacentLevelShapes(sc), sc.Target, ChordRTree, Tests, out failures);



        private static List<LineView> CreateChordView(MorphRenderMesh mesh, OTVTable table, Color color)
        {
            List<SliceChord> CandidateChords = BajajMeshGenerator.CreateChordCandidateList(mesh, table);

            var RejectedChords = CandidateChords.Where(sc => !mesh.Contains(sc.Origin, sc.Target));
            var AcceptedChords = CandidateChords.Where(sc => mesh.Contains(sc.Origin, sc.Target));

            List<LineView> lineViews = [.. RejectedChords.Select(sc => new LineView(sc.Line, 1.0, color, LineStyle.Ladder))];
            lineViews.AddRange(AcceptedChords.Select(sc => new LineView(sc.Line, 1.0, color, LineStyle.Glow)));

            return lineViews;
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, MonoTestbed.DefaultBackground, 1.0f, 0);

            //The camera fit depends on the slice and the viewport, so sizes have to follow it rather than be set
            //once at construction.
            ScaleAnnotationsToScene(scene);

            StringBuilder ViewLabels = new();


            if (RegionViews != null && ViewIndex.InRange(iShownRegion, RegionViews.Count))
            {
                RegionViews[iShownRegion.Value].Draw(window, scene);
                ViewLabels.AppendLine("Y: Region Pass #" + iShownRegion.Value);
            }

            if (MeshViews != null && ViewIndex.InRange(iShownMesh, MeshViews.Count))
            {
                DeviceStateManager.SaveDeviceState(window.GraphicsDevice);

                DepthStencilState dstate = new()
                {
                    DepthBufferEnable = true,
                    StencilEnable = false,
                    DepthBufferWriteEnable = true,
                    DepthBufferFunction = CompareFunction.LessEqual
                };

                window.GraphicsDevice.DepthStencilState = dstate;

                //Matrix oldWorld = scene.World;


                //Adjust the meshViewModel Z coordinates so we can see the mesh in 2D
                /*
                double maxZ = this.PolyZ.Max();
                double minZ = this.PolyZ.Min();
                double ZRange = maxZ - minZ;

                scene.World = Matrix.CreateScale(new Vector3(1, 1, 1f / (float)ZRange)) * Matrix.CreateTranslation(new Vector3(0, 0, -(float)minZ));
                */


                MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, scene, CullMode.None);


                ViewLabels.AppendLine("A: " + MeshViews[iShownMesh.Value].Name);
                DeviceStateManager.RestoreDeviceState(window.GraphicsDevice);

                //scene.World = oldWorld;
            }

            if (triView != null && iShownLineView.HasValue == false)
            {
                triView.Draw(window, scene, window.lineManager);
                ViewLabels.AppendLine("B: Trianglulation View");
            }
            else if (listLineViews != null && ViewIndex.InRange(iShownLineView, listLineViews.Count))
            {
                int iShownLine = iShownLineView.Value;
                LineSetView lineView = listLineViews[iShownLine];

                DeviceStateManager.SaveDeviceState(window.GraphicsDevice);

                DepthStencilState dstate = new()
                {
                    DepthBufferEnable = true,
                    StencilEnable = false,
                    DepthBufferWriteEnable = true,
                    DepthBufferFunction = CompareFunction.LessEqual
                };
                window.GraphicsDevice.DepthStencilState = dstate;

                RasterizerState rstate = new()
                {
                    CullMode = CullMode.None,
                    FillMode = Microsoft.Xna.Framework.Graphics.FillMode.Solid,
                    DepthClipEnable = true
                };
                window.GraphicsDevice.RasterizerState = rstate;

                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, 0);

                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. lineView.LineViews]);
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);

                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 10);


                DeviceStateManager.RestoreDeviceState(window.GraphicsDevice);

                //CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, lineView.LineLables.ToArray());
                //Edge colors still carry the edge type, so the view stays useful where the text had to be dropped.
                LabelView[] legible = [.. lineView.LineLabels.Where(IsLegible)];
                foreach (var labelsByFont in legible.GroupBy(l => l.font))
                {
                    LabelView.Draw(window.spriteBatch, labelsByFont.Key, scene, [.. labelsByFont]);
                }

                ViewLabels.AppendLine("B: " + lineView.Name +
                    (legible.Length < lineView.LineLabels.Count ? $" ({lineView.LineLabels.Count - legible.Length} labels too small, zoom in)" : ""));
            }
            /*
            if (lineViews != null && ShowPolygons && !ShowRegionPolygons)
            {
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, 0);
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, lineViews.LineViews.ToArray());
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 10);
                CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, lineViews.LineLables.ToArray());
                ViewLabels.AppendLine("Chords");
            }

            if (unfiltered_lineViews != null && ShowAllEdges)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, unfiltered_lineViews.LineViews.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, unfiltered_lineViews.LineLables.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("Triangulation");
            }*/

            if (IncompletedVertexView != null && ShowCompletedVerticies)
            {
                IncompletedVertexView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                ViewLabels.AppendLine("Incomplete Vertices");
            }

            if (MeshVertsView != null && (this.VertexLabelType & IndexLabelType.MESH) > 0 && ViewIndex.InRange(iShownLineView, listLineViews.Count))
            {
                //Markers always draw; only the index text that shrank past readable is held back, so a crowded ring
                //still shows where its vertices are.
                CircleView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha, MeshVertsView.PointViews);

                LabelView[] all = MeshVertsView.LabelViews ?? [];
                LabelView[] legibleVerts = [.. all.Where(IsLegible)];
                if (legibleVerts.Length > 0)
                    LabelView.Draw(window.spriteBatch, window.fontArial, scene, legibleVerts);

                ViewLabels.AppendLine("Mesh verticies" +
                    (legibleVerts.Length < all.Length ? $" ({all.Length - legibleVerts.Length} labels too small, zoom in)" : ""));
            }

            if (RegionPolygonViews != null && ShowRegionPolygons)
            {

                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. RegionPolygonViews.SelectMany(rpv => rpv.LineViews)]);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, RegionLabelViews);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("Y: Region Polygon Views");
            }

            if (OTVTableView != null && ShowOtvChords && OTVTableView.Count > 0)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. OTVTableView]);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("OTV Table");
            }

            if (this.PolyViews != null && !ShowRegionPolygons && ((this.VertexLabelType & IndexLabelType.MESH) == 0))
            {
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                PolyViews.Draw(window, scene);
                ViewLabels.AppendLine("Poly Views");
            }

            // The enabled sub-views are surfaced to the legend HUD (see MonoTestbed.DrawLegendHUD) instead of
            // being rendered inline here.
            LastViewLabels = ViewLabels.ToString();

        }


        public void Draw3D(MonoTestbed window, Scene3D scene)
        {
            StringBuilder ViewLabels = new();

            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, MonoTestbed.DefaultBackground, 1.0f, 0);

            DepthStencilState dstate = new()
            {
                DepthBufferEnable = true,
                StencilEnable = false,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual
            };

            window.GraphicsDevice.DepthStencilState = dstate;
            //window.GraphicsDevice.BlendState = BlendState.Opaque;


            if (ViewIndex.InRange(iShownMesh, MeshViews.Count))
            {
                double maxZ = this.ShapeZ.Max();
                double minZ = this.ShapeZ.Min();
                double ZRange = maxZ - minZ;

                Microsoft.Xna.Framework.Matrix oldWorld = scene.World;
                scene.World = Microsoft.Xna.Framework.Matrix.CreateScale(new Microsoft.Xna.Framework.Vector3(1, 1, (float)ZRange));

                MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, scene, CullMode);

                ViewLabels.AppendLine(MeshViews[iShownMesh.Value].Name);

                scene.World = oldWorld;
            }

            // The enabled sub-views are surfaced to the legend HUD (see MonoTestbed.DrawLegendHUD) instead of
            // being rendered inline here.
            LastViewLabels = ViewLabels.ToString();
        }


        internal List<BajajCaptureShot> EnumerateDefaultShots()
        {
            lock (ViewsLock)
                return EnumerateDefaultShotsUnlocked();
        }

        internal List<BajajCaptureShot> EnumerateDefaultShotsUnlocked()
        {
            List<BajajCaptureShot> shots = [BajajCaptureShot.Overview2D()];

            if (OTVTableView is { Count: > 0 })
                shots.Add(BajajCaptureShot.OtvChords());

            for (int i = 0; i < MeshViews.Count; i++)
            {
                string name = string.IsNullOrWhiteSpace(MeshViews[i].Name) ? $"mesh-{i}" : MeshViews[i].Name;
                shots.Add(BajajCaptureShot.Mesh(i, name, view3d: false));
                shots.Add(BajajCaptureShot.Mesh(i, name, view3d: true));
            }

            for (int i = 0; i < listLineViews.Count; i++)
            {
                string name = string.IsNullOrWhiteSpace(listLineViews[i].Name) ? $"lines-{i}" : listLineViews[i].Name;
                shots.Add(BajajCaptureShot.Lines(i, name));
            }

            for (int i = 0; i < RegionViews.Count; i++)
            {
                if (RegionViews[i].HasGeometry)
                    shots.Add(BajajCaptureShot.Region(i));
            }

            return shots;
        }

        internal List<BajajCaptureShot> EnumerateInteractiveShotsUnlocked()
        {
            List<BajajCaptureShot> shots = [BajajCaptureShot.Overview2D()];

            if (OTVTableView is { Count: > 0 })
                shots.Add(BajajCaptureShot.OtvChords());

            for (int i = 0; i < listLineViews.Count; i++)
            {
                string name = string.IsNullOrWhiteSpace(listLineViews[i].Name) ? $"lines-{i}" : listLineViews[i].Name;
                shots.Add(BajajCaptureShot.Lines(i, name));
            }

            for (int i = 0; i < MeshViews.Count; i++)
            {
                string name = string.IsNullOrWhiteSpace(MeshViews[i].Name) ? $"mesh-{i}" : MeshViews[i].Name;
                shots.Add(BajajCaptureShot.Mesh(i, name, view3d: false));
            }

            for (int i = 0; i < RegionViews.Count; i++)
                shots.Add(BajajCaptureShot.Region(i));

            return shots;
        }

        /// <summary>
        /// Axis-aligned bounds of the line, region, or OTV overlay a shot will draw.
        /// Used to frame screenshot cameras on chord and region views instead of the full slice pair.
        /// </summary>
        internal bool TryGetShotBounds(BajajCaptureShot shot, out Rectangle bounds)
        {
            lock (ViewsLock)
                return TryGetShotBoundsUnlocked(shot, out bounds);
        }

        internal bool TryGetShotBoundsUnlocked(BajajCaptureShot shot, out Rectangle bounds)
        {
            if (shot.ShowOtvChords)
                return TryLineBounds(OTVTableView, out bounds);

            if (shot.LineIndex is int lineIndex && lineIndex >= 0 && lineIndex < listLineViews.Count)
                return TryLineBounds(listLineViews[lineIndex].LineViews, out bounds);

            if (shot.RegionIndex is int regionIndex && regionIndex >= 0 && regionIndex < RegionViews.Count)
                return TryLineBounds(RegionViews[regionIndex].PolygonViews.SelectMany(v => v.LineViews), out bounds);

            bounds = default;
            return false;
        }

        private static bool TryLineBounds(IEnumerable<LineView> lines, out Rectangle bounds)
        {
            bounds = default;
            if (lines is null)
                return false;

            bool any = false;
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            foreach (LineView line in lines)
            {
                any = true;
                minX = Math.Min(minX, Math.Min(line.Source.X, line.Destination.X));
                minY = Math.Min(minY, Math.Min(line.Source.Y, line.Destination.Y));
                maxX = Math.Max(maxX, Math.Max(line.Source.X, line.Destination.X));
                maxY = Math.Max(maxY, Math.Max(line.Source.Y, line.Destination.Y));
            }

            if (!any)
                return false;

            if (minX == maxX)
            {
                minX -= 1;
                maxX += 1;
            }

            if (minY == maxY)
            {
                minY -= 1;
                maxY += 1;
            }

            bounds = new Rectangle(minX, maxX, minY, maxY);
            return true;
        }

        internal void ApplyShot(BajajCaptureShot shot)
        {
            ArgumentNullException.ThrowIfNull(shot);

            lock (ViewsLock)
                ApplyShotUnlocked(shot);
        }

        internal void ApplyShotUnlocked(BajajCaptureShot shot)
        {
            iShownMesh = shot.MeshIndex;
            iShownLineView = shot.LineIndex;
            iShownRegion = shot.RegionIndex;
            ShowOtvChords = shot.ShowOtvChords;
            // Overview/OTV clear mesh labels so contour PolyViews draw. Other shots restore MESH
            // labels, which hides that overlay so line/mesh/region geometry is visible.
            VertexLabelType = shot.ClearVertexLabels ? IndexLabelType.NONE : IndexLabelType.MESH;
        }
    }


    /// <summary>
    /// Represents all of the information to recreate a failure case for a particular slice in the morphology generator
    /// </summary>
    class BajajRepro
    {
        /// <summary>
        /// Description of the bug the locations demonstrate
        /// </summary>
        public readonly string Description;

        /// <summary>
        /// Volume endpoint the locations came from
        /// </summary>
        public readonly Uri Endpoint;

        /// <summary>
        /// LocationIDs in the slice we are testing
        /// </summary>
        public readonly ulong[] SliceLocations;

        /// <summary>
        /// Number of hops from the slice to load to ensure the corresponding verticies are added from adjacent slices to ensure the reproduction of the error can occur
        /// </summary>
        public int Hops = 1;

        /// <summary>
        /// Degree to which we should simplify the polygon so that all verticies are with x pixels of the predicted curve given by the control points
        /// </summary>
        public double Tolerance = 2.0;

        /// <summary>
        /// Morphology graph containing annotations from the server.
        /// </summary>
        public AnnotationVizLib.MorphologyGraph Morphology;

        /// <summary>
        /// Slice graph generated from the Morphology graph which contains the slice we need to build to demonstrate the bug
        /// </summary>
        public SliceGraph Graph;

        private BajajRepro(Uri endpoint, string description, double tolerance = 1)
        {
            Endpoint = endpoint;
            this.Description = description;
            this.Tolerance = tolerance;
        }

        public BajajRepro(ulong[] slice, Uri endpoint, string description = null, double tolerance = 1) : this(endpoint, description)
        {
            SliceLocations = slice;
        }

        public BajajRepro(ulong A, ulong B, Uri endpoint, string description = null, double tolerance = 1) : this(endpoint, description)
        {
            SliceLocations = [A, B];
        }

        public BajajRepro(ulong A, ulong B, ulong C, Uri endpoint, string description = null, double tolerance = 1) : this(endpoint, description)
        {
            SliceLocations = [A, B, C];
        }

        public BajajRepro(ulong A, ulong B, ulong C, ulong D, Uri endpoint, string description = null, double tolerance = 1) : this(endpoint, description)
        {
            SliceLocations = [A, B, C, D];
        }

        public void Initialize(double tolerance = 2.0, int hops = 1)
        {
            //SliceLocations are LOCATION IDs (not structure IDs), so fetch by location: this loads the parent
            //structure and its neighborhood so the slice's locations and edges are present in the graph.
            Morphology = AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDs([.. SliceLocations.Select(id => (long)id)], Endpoint, hops);

            //Find the linked locations and add those to the graph
            //////////////

            //BajajMeshGenerator.ConvertToMeshGraph(graph);

            AnnotationVizLib.MorphologyNode[] nodes = [.. Morphology.Nodes.Values];
            //wrapView = new MonogameTestbed.BajajOTVAssignmentView(nodes.Select(n => n.Geometry.ToPolygon()).ToArray(), nodes.Select(n=> n.Z).ToArray()); 

            Graph = SliceGraph.Create(Morphology, tolerance).Result;
        }

        /// <summary>
        /// Returns the slice this case should test
        /// </summary>
        /// <returns></returns>
        public Slice GetSlice()
        {
            Slice slice = Graph.Nodes.FirstOrDefault(n => SliceLocations.All(id => n.Value.AllNodes.Contains(id))).Value;
            if (slice is null)
            {
                throw new InvalidOperationException(
                    $"No slice contains all location IDs [{string.Join(", ", SliceLocations)}]. " +
                    $"SliceGraph has {Graph.Nodes.Count} slice(s).");
            }
            return slice;
        }

        /// <summary>
        /// Returns the slice this case should test
        /// </summary>
        /// <returns></returns>
        public static Slice GetSlice(SliceGraph graph, ulong[] SliceLocations)
        {
            Slice slice = graph.Nodes.FirstOrDefault(n => SliceLocations.All(id => n.Value.AllNodes.Contains(id))).Value;
            Debug.Assert(slice != null, "We should be able to find the slice we are trying to test");
            return slice;
        }
    }

    /// <summary>
    /// This tests how we create faces that connect two polygons at different Z levels
    /// </summary>
    class BajajAssignmentTest : IGraphicsTest, ITestLegend, ITestHotkeyHelp
    {
        public string Title => this.GetType().Name;

        public IReadOnlyList<HotkeyBinding> GetHotkeyBindings() =>
        [
            new("V / Left shoulder", "Toggle 2D / 3D view"),
            new("K / Left stick", "Toggle backface culling"),
            new("PageDown / Mouse X2", "Next diagnostic shot (2D)"),
            new("PageUp / Mouse X1", "Previous diagnostic shot (2D)"),
            new("Start", "Rebuild mesh for current slice pair"),
            new("Back", "Reset 3D camera to slice bounds"),
            new("A / B / Y / X", "Cycle meshes, lines, regions (gamepad)"),
            new("Right shoulder", "Cycle vertex label modes"),
            new("Right stick", "Toggle position labels"),
        ];

        public string ModeDescription
        {
            get
            {
                System.Text.StringBuilder sb = new();
                sb.AppendLine("Bajaj slice mesh-generation test: triangulates and meshes a stack of polygon slices to reproduce mesh generator failures.");
                if (CurrentTestCase is not null)
                {
                    if (!string.IsNullOrWhiteSpace(CurrentTestCase.Description))
                        sb.AppendLine("Case: " + CurrentTestCase.Description);
                    if (CurrentTestCase.Endpoint is not null)
                        sb.AppendLine("Endpoint: " + CurrentTestCase.Endpoint);
                    if (CurrentTestCase.SliceLocations is { Length: > 0 })
                        sb.AppendLine("Slice locations: " + string.Join(", ", CurrentTestCase.SliceLocations));
                }
                sb.Append(Draw3D ? "View: 3D mesh  (V: 2D)" : "View: 2D  (V: 3D, PgUp/PgDn: stage)");
                return sb.ToString();
            }
        }

        public string ActiveViewDescription => wrapView?.LastViewLabels ?? string.Empty;

        private static readonly LegendEntry[] _legendEntries =
        [
            new("Medial axis vertex", Color.MediumPurple),
            new("Corresponding vertex", Color.DarkSlateBlue),
            new("Face complete (upper shape)", Color.LimeGreen),
            new("Face complete (lower shape)", Color.ForestGreen),
            new("Incomplete vertex (upper shape)", Color.Orange),
            new("Incomplete vertex (lower shape)", Color.Red),
            new("Vertex missing shape index (unexpected)", Color.Aqua),
            new("Incomplete-vertices overlay", Color.DarkRed),
            new("Current triangulation edges", Color.Black),
            new("Accepted chord", Color.LightGray, LineStyle.Glow),
            new("Rejected chord", Color.LightGray, LineStyle.Ladder),
            new("Region polygon (random color per region)", Color.Gray),
        ];

        public IReadOnlyList<LegendEntry> LegendEntries => _legendEntries;
        readonly ulong[] GlialDebug1 = [
          133887, //Z = 2
          133882
        ];
        readonly ulong[] BasicBranchTroubleIDS = [
          240719, //Z = 537
          240720, //Z = 536
          240721, //Z = 536
        ];
        readonly ulong[] BasicBranchInteriorHole = [
          236909, //Z = 1
          236910, //Z = 1
          236911 //Z =2
        ];
        readonly ulong[] BasicInteriorHoleOverAdjacentExteriorRing = [
          256816, //Z = 1
          256818
        ];
        readonly ulong[] HorseshoeInteriorHoleOverAdjacentExteriorRing = [
          260138, //Z = 1
          260139
        ];
        readonly ulong[] DelaunayTest = [
            133882,
            133887
        ];
        readonly ulong[] DelaunayTest2 = [
            133888,
            133883
        ];
        readonly ulong[] DelaunayTest3 = [
            133890,
            133884
        ];
        readonly ulong[] DelaunayTest4 = [
            133917,
            133912
        ];
        readonly ulong[] DelaunayTest5 = [
            133901,
            133896
        ];
        readonly ulong[] DelaunayTest6 = [
            133920,
            133915
        ];
        readonly ulong[] DelaunayTest7 = [
            133923,
            133917
        ];
        readonly ulong[] DelaunayTest8 = [
            82601,
            82599
        ];

        /// <summary>
        /// The faces on edge after closing untiled region
        /// </summary>
        readonly ulong[] DelaunayTest9 = [
            58687,
            58685
        ];

        /// <summary>
        /// Clockwise triangulation vert
        /// </summary>
        readonly ulong[] DelaunayTest10 = [
            108603,
            108610,
            108534
        ];

        /// <summary>
        /// Created line with duplicate points
        /// FALSE POSITIVE HOLE DETECTION due to overlapping contours of thin process.
        /// </summary>
        readonly ulong[] DelaunayTest11 =
        [
            102640,
            102645,
            102436
        ];

        /// <summary>
        /// 3 Z-levels.  OTV Tiling table assertion
        /// </summary>
        readonly ulong[] DelaunayTest12 =
        [
            102557,
            102564,
            263477,
            102410,
            263476
        ];

        /// <summary>
        /// Adding correspoinding verticies, adding same point twice
        /// </summary>
        readonly ulong[] DelaunayTest13 =
        [
            58685,
            58682
        ];
        readonly ulong[] DelaunayTest14 =
        [
            58708,
            58706
        ];
        readonly ulong[] DelaunayTest15 =
        [
            82356,
            58677
        ];

        //Possible infinite loop in FindCloseableFaces
        readonly ulong[] DelaunayTest16 =
        [
            105877,
            105879,
            105837
        ];

        //Infinite loop adding constrained edges
        readonly ulong[] DelaunayTest17 =
        [
            133018 ,
            133001
        ];

        //Vertices that create an additional correspondance point when nudged
        readonly ulong[] DelaunayTest18 =
        [
            145437 ,
            145435
        ];
        readonly ulong[] DelaunayTest19 =
        [
            82607 ,
            82604
        ];
        readonly ulong[] DelaunayTest20 =
        [
            139799 ,
            139796
        ];


        /// <summary>
        /// The set of cases we want to be able to run successfully
        /// </summary>
        readonly BajajRepro[] ReproSet =
        [
            new(1333661, 1333662, 1333665, DataSource.EndpointMap[Endpoint.TEST], "NightmareTroubleIDS"),
            new(82617, 82647, 82679, DataSource.EndpointMap[Endpoint.TEST], "Polygons with internal polygon"),
            new(82884, 82908, DataSource.EndpointMap[Endpoint.TEST], "Polygons with internal polygon merging with external concavity"),
            new(82612, 82617, 82647, DataSource.EndpointMap[Endpoint.TEST], "Polygons with internal polygon"),
            new(139799, 139796, DataSource.EndpointMap[Endpoint.RPC1], "Delaunay error that only occurs after corresponding points are added for polygons outside the slice"),
            new(145431, 145428, DataSource.EndpointMap[Endpoint.RPC1], "Region with no perimeter"),
            new(100542, 100547, DataSource.EndpointMap[Endpoint.RPC1], "Unknown"),
            new(100804, 100807, DataSource.EndpointMap[Endpoint.RPC1], "Corresponding points in region"),
            new(100418, 100419, DataSource.EndpointMap[Endpoint.RPC1], "Corresponding points in region P:1 iVert:74 of 235"),
            new(58699 ,  58696, DataSource.EndpointMap[Endpoint.RPC1], "We should always be able to find an edge to add to our perimeter until we exhaust the list of unassigned perimeter edges"),
            new(140324, 140323, DataSource.EndpointMap[Endpoint.RPC1], "Expected two faces for edge removed for constraint"),
            new(139807, 139803, DataSource.EndpointMap[Endpoint.RPC1], "Expected two faces for edge removed for constraint?"),
            new(139667, 139664, DataSource.EndpointMap[Endpoint.RPC1], "Face 76,77,78 must have non-zero area"),
            new(140323, 140322, DataSource.EndpointMap[Endpoint.RPC1], "Added edge intersects existing edge"),
            new(140327, 140325, DataSource.EndpointMap[Endpoint.RPC1], "New edge 281-679 intersects existing edges"),
            new(100516, 100517, DataSource.EndpointMap[Endpoint.RPC1], "New edge 222-224 intersects existing edges {223-460}"),
            new(82928 , 82916, DataSource.EndpointMap[Endpoint.RPC1], "New edge {2-22} intersects existing edges {3-24}"),
            new(99027, 99028, DataSource.EndpointMap[Endpoint.RPC1], "New edge 1-10 intersects existing edges"),
            new(100516, 100517, DataSource.EndpointMap[Endpoint.RPC1], "New edge 222-224 intersects existing edges"),
            new(145542, 145539, DataSource.EndpointMap[Endpoint.RPC1], "New edge 1-11 intersects existing edges"),
            new(113933, 113927, DataSource.EndpointMap[Endpoint.RPC1], "New edge 175-177 intersects existing edges: 176-446"),
            new([146097, 146105, 146107, 146108, 274054], DataSource.EndpointMap[Endpoint.RPC1], "System.InvalidCastException"),
            new(146420, 146425 , 146426, DataSource.EndpointMap[Endpoint.RPC1], "We should always be able to find an edge to add to our perimeter until we exhaust the list of unassigned perimeter edges"),
            new(158786, 158787, DataSource.EndpointMap[Endpoint.RPC1], "Infinite recursion in edge flip"),
            new(211283, 211284, DataSource.EndpointMap[Endpoint.RPC1], "New edge 0-6 intersects existing edges: 2-7"),
            new(269861, 269862, DataSource.EndpointMap[Endpoint.RPC1], "QuadTreeWithUniqueValues: : 'Index was out of range."),
            new(108279, 108280, DataSource.EndpointMap[Endpoint.RPC1], "We should always be able to find an edge to add to our perimeter until we exhaust the list of unassigned perimeter edges"),
            new(282225, 282226, DataSource.EndpointMap[Endpoint.RPC1], "Interior points must be inside Face"),
            new(269802, 269803, DataSource.EndpointMap[Endpoint.RPC1], "Exterior polygon ring must be valid"),
            new(269709, 269708, DataSource.EndpointMap[Endpoint.RPC1], "Medial Axis approximate vertex must be within polygonal boundary"),
            new(282070, 282069, DataSource.EndpointMap[Endpoint.RPC1], "New edge 0-18 intersects existing edges: 8-9"),
            new(145755, 145741, 146044, 146089, DataSource.EndpointMap[Endpoint.RPC1], "Expect two faces for any edge removed for intersecting an edge constraint"),
            new(158560, 158561, DataSource.EndpointMap[Endpoint.RPC1], "Adjacent corresponding edges?"),
            new(100418, 100419, DataSource.EndpointMap[Endpoint.RPC1], "System.NotImplementedException: Corresponding points in region P:1 iVert:74 of 235"),
            new(100011, 100021, DataSource.EndpointMap[Endpoint.RPC1], "New edge 225-511 intersects existing edges: 237-238"),
            new(82682, 82680, DataSource.EndpointMap[Endpoint.RPC1], "RTree error on vertex insert.",1.0), //Fixed by not adding an existing point in an interior hole to the exterior polygon again
            new(100119,  100121, DataSource.EndpointMap[Endpoint.RPC1], "New edge 54-57 intersects existing edges: 56-411",1.0),
            new(108602,  108528, DataSource.EndpointMap[Endpoint.RPC1], "We should always be able to find an edge to add to our perimeter until we exhaust the list of unassigned perimeter edges",1.0),
            new(113919,  113910, DataSource.EndpointMap[Endpoint.RPC1], "Duplicate point found in exterior ring", 1.0),
            new(269802 , 269803, DataSource.EndpointMap[Endpoint.RPC1], "Index out of range", 3.0),
            new(85470 , 85449, DataSource.EndpointMap[Endpoint.RPC1], "Scale check", 2.0),
            new(169273, 5653, DataSource.EndpointMap[Endpoint.RC1], "Slice 51: nonManifold:1 holes:1 (U:169273 Z62 / D:5653 Z61, structure 180)")
        ];

        private BajajRepro[] _cases;

        /// <summary>
        /// <see cref="ReproSet"/> followed by any ad-hoc cases named on the command line or in the capture request,
        /// so an arbitrary slice can be inspected in the viewer without first being committed to the repro set.
        /// </summary>
        private BajajRepro[] Cases => _cases ??= [.. ReproSet, .. AdHocCases()];

        private static IEnumerable<BajajRepro> AdHocCases()
        {
            foreach (var request in Program.options?.CaptureRequest?.ReproLocations ?? [])
            {
                if (request.Locations is not { Length: > 1 })
                    continue;

                Uri endpoint = ResolveEndpoint(request.Endpoint) ?? Program.options?.EndpointUri;
                yield return new BajajRepro(request.Locations, endpoint,
                                            request.Description ?? $"ad-hoc {string.Join("/", request.Locations)}",
                                            request.Tolerance ?? 1.0);
            }

            if (Program.options?.ReproLocations is { Count: > 1 } locations)
                yield return new BajajRepro([.. locations], Program.options.EndpointUri, $"ad-hoc {string.Join("/", locations)}", Program.options.ReproTolerance);
        }

        private static Uri ResolveEndpoint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (DataSource.EndpointMap.TryGetValue(name.ToEnum<Endpoint>(), out Uri uri))
                return uri;

            return new Uri(name);
        }

        /// <summary>
        /// Index of the reprocase we want to display on load
        /// </summary>
        readonly int CurrentReproCase = 41;

        BajajRepro CurrentTestCase = null;

        Scene scene;
        Scene3D scene3D;
        MouseState _lastMouse;
        bool _mouseSeen;
        int? _displayShotIndex;
        int _pendingShotDelta;
        readonly TestInputContext Input = new();
        readonly Polygon A;
        readonly Polygon B;
        readonly PointSetViewCollection Points_A = new(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new(Color.Red, Color.Pink, Color.Plum);
        readonly Camera3DManipulator Camera3DManipulator = new();

        BajajOTVAssignmentView wrapView = null;

        bool Draw3D = false;

        bool _initialized = false;
        public bool Initialized => _initialized;

        readonly AnnotationVizLib.MorphologyGraph Graph;

        enum ScreenshotPhase
        {
            Inactive,
            WaitFullscreen,
            WaitMesh,
            Capture,
            NextRepro,
            Done
        }

        MonoTestbed _host;
        ScreenshotPhase _screenshotPhase = ScreenshotPhase.Inactive;
        List<int> _reproQueue;
        int _reproQueueIndex;
        string _screenshotRoot;
        CaptureManifest _manifest;
        CaptureManifestCase _currentManifestCase;
        string _currentCaseFolder;


        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            this.scene3D = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D())
            {
                MaxDrawDistance = 1000000,
                MinDrawDistance = 1
            };

            Input.UpdateTrackers();

            _host = window;
            _reproQueue = ResolveReproQueue();
            _reproQueueIndex = 0;

            if (Program.options?.Screenshots == true)
            {
                _screenshotRoot = ScreenshotCapture.BajajOutputRoot();
                Directory.CreateDirectory(_screenshotRoot);
                _manifest = new CaptureManifest();
                _screenshotPhase = ScreenshotPhase.WaitFullscreen;
            }

            try
            {
                LoadReproAtQueueIndex(window, restoreCamera: Program.options?.Screenshots != true);
            }
            catch (Exception ex)
            {
                if (_screenshotPhase == ScreenshotPhase.Inactive)
                    throw;

                RecordCaseError(ex.ToString());
                _screenshotPhase = ScreenshotPhase.NextRepro;
            }

            return Task.CompletedTask;
        }

        public void Update()
        {
            UpdateScreenshotCapture();
            if (_screenshotPhase is ScreenshotPhase.Capture or ScreenshotPhase.Done or ScreenshotPhase.NextRepro)
                return;
            if (wrapView is null)
                return;

            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Input.Gamepad.Update(state);
            Input.Keyboard.Update(Microsoft.Xna.Framework.Input.Keyboard.GetState());
            UpdateDisplayShotInput();

            if (!Draw3D)
                Input.CameraManipulator.Update(scene.Camera);
            else
                Camera3DManipulator.Update(this.scene3D.Camera, scene3D.Viewport.Width, scene3D.Viewport.Height);

            if (Input.Gamepad.A_Clicked)
            {
                wrapView.iShownMesh = wrapView.iShownMesh.HasValue ? wrapView.iShownMesh.Value + 1 : 0;
                if (wrapView.iShownMesh.HasValue && wrapView.iShownMesh.Value >= wrapView.MeshViews.Count)
                {
                    wrapView.iShownMesh = null;
                }
            }

            if (Input.Gamepad.B_Clicked)
            {
                wrapView.iShownLineView = wrapView.iShownLineView.HasValue ? wrapView.iShownLineView.Value + 1 : 0;
                if (wrapView.iShownLineView.HasValue && wrapView.iShownLineView.Value >= wrapView.listLineViews.Count)
                {
                    wrapView.iShownLineView = null;
                }

                Trace.WriteLine(wrapView.iShownLineView.ToString());

                /*wrapView.ShowPolygons = !wrapView.ShowPolygons;
                wrapView.ShowAllEdges = !wrapView.ShowAllEdges;
                */
            }

            if (Input.Gamepad.Y_Clicked)
            {
                //Cycle throught the various region passes as Y is clicked
                wrapView.iShownRegion = wrapView.iShownRegion.HasValue ? wrapView.iShownRegion.Value + 1 : 0;
                if (wrapView.iShownRegion.HasValue && wrapView.iShownRegion.Value >= wrapView.RegionViews.Count)
                {
                    wrapView.iShownRegion = null;
                }

            }


            if (Input.Gamepad.X_Clicked)
            {
                //wrapView.ShowCompletedVerticies = !wrapView.ShowCompletedVerticies;
                wrapView.iShownRegion = wrapView.iShownRegion.HasValue ? wrapView.iShownRegion.Value - 1 : wrapView.RegionViews.Count - 1;
                if (wrapView.iShownRegion.HasValue && wrapView.iShownRegion.Value < 0)
                {
                    wrapView.iShownRegion = null;
                }
            }

            if (Input.Gamepad.Start_Clicked)
            {
                //Recalculate the mesh from scratch
                var Graph = CurrentTestCase.Morphology;
                Slice slice = CurrentTestCase.GetSlice();

                wrapView = new MonogameTestbed.BajajOTVAssignmentView(CurrentTestCase.Graph, slice);
                _displayShotIndex = null;
                _pendingShotDelta = 0;
            }

            if (Input.Gamepad.RightShoulder_Clicked)
            {
                if ((wrapView.VertexLabelType & (IndexLabelType.MESH | IndexLabelType.POLYGON)) == 0)
                {
                    wrapView.VertexLabelType |= IndexLabelType.MESH;
                }
                else if ((wrapView.VertexLabelType & IndexLabelType.POLYGON) > 0)
                {
                    wrapView.VertexLabelType = IndexLabelType.NONE;
                }
                else if ((wrapView.VertexLabelType & IndexLabelType.MESH) == 0)
                {
                    wrapView.VertexLabelType |= IndexLabelType.MESH;
                    wrapView.VertexLabelType ^= IndexLabelType.POLYGON;
                }
                else if ((wrapView.VertexLabelType & IndexLabelType.POLYGON) == 0)
                {
                    wrapView.VertexLabelType |= IndexLabelType.POLYGON;
                    wrapView.VertexLabelType ^= IndexLabelType.MESH;
                }
            }

            if (Input.Gamepad.RightStick_Clicked)
            {
                wrapView.VertexLabelType ^= IndexLabelType.POSITION;
            }

            if (Input.Gamepad.LeftStick_Clicked || Input.Keyboard.Pressed(Keys.K))
            {
                wrapView.CullMode = wrapView.CullMode == CullMode.None ? CullMode.CullCounterClockwiseFace : CullMode.None;
            }

            //Keyboard alternative to the shoulder button, which is the only way to reach the 3D view without a
            //gamepad attached.
            if (Input.Gamepad.LeftShoulder_Clicked || Input.Keyboard.Pressed(Keys.V))
            {
                this.Draw3D = !this.Draw3D;
            }

            if (Input.Gamepad.Back_Clicked)
            {
                Geometry.Rectangle bbox = wrapView.Shapes.BoundingBox();
                double MinZ = wrapView.ShapeZ.Min();
                double MaxZ = wrapView.ShapeZ.Max();
                double Depth = MaxZ - MinZ;
                scene3D.Camera.Position = (bbox.Center.ToVector3(0) + new Geometry.Vector3(0, 0, 100f * (float)Depth)).ToXNAVector3();
                scene3D.Camera.LookAt = new Microsoft.Xna.Framework.Vector3((float)bbox.Center.X, (float)bbox.Center.Y, 0); // bbox.CenterPoint.ToXNAVector3();
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

        public void Draw(MonoTestbed window)
        {
            if (_screenshotPhase == ScreenshotPhase.Capture)
            {
                try
                {
                    CaptureCurrentCase(window);
                }
                catch (Exception ex)
                {
                    RecordCaseError(ex.ToString());
                }

                _screenshotPhase = ScreenshotPhase.NextRepro;
                return;
            }

            DrawCurrentView(window);
        }

        public void UnloadContent(MonoTestbed window) => this.scene.SaveCamera(TestMode.BAJAJTEST);

        private List<int> ResolveReproQueue()
        {
            CaptureRequestFile request = Program.options?.CaptureRequest;
            IEnumerable<int> raw;
            if (request?.Repro is { Length: > 0 })
                raw = request.Repro;
            else if (Program.options?.ReproAll == true)
                raw = Enumerable.Range(0, Cases.Length);
            else if (Program.options?.ReproIndices is { Count: > 0 } indices)
                raw = indices;
            else if (Cases.Length > ReproSet.Length)
                raw = Enumerable.Range(ReproSet.Length, Cases.Length - ReproSet.Length);
            else
                raw = [CurrentReproCase];

            List<int> queue = [];
            foreach (int i in raw.Distinct())
            {
                if (i < 0 || i >= Cases.Length)
                {
                    string msg = $"Skipping out-of-range ReproSet index {i} (valid 0..{Cases.Length - 1})";
                    Console.WriteLine(msg);
                    Trace.WriteLine(msg);
                    continue;
                }

                queue.Add(i);
            }

            if (queue.Count == 0)
                queue.Add(Math.Clamp(CurrentReproCase, 0, Cases.Length - 1));

            return queue;
        }

        private void LoadReproAtQueueIndex(MonoTestbed window, bool restoreCamera)
        {
            int index = _reproQueue[_reproQueueIndex];
            CurrentTestCase = Cases[index];
            _currentCaseFolder = $"case-{index:D2}-{ScreenshotCapture.SanitizeFilePart(CurrentTestCase.Description)}";
            _currentManifestCase = new CaptureManifestCase
            {
                Index = index,
                Description = CurrentTestCase.Description,
                LocationIds = CurrentTestCase.SliceLocations,
                Endpoint = CurrentTestCase.Endpoint?.ToString(),
                Folder = _currentCaseFolder
            };

            CurrentTestCase.Initialize(tolerance: 1.0);
            Slice slice = CurrentTestCase.GetSlice();
            wrapView = new BajajOTVAssignmentView(CurrentTestCase.Graph, slice);
            FitCameras(window, restoreCamera);
            _displayShotIndex = null;
            _pendingShotDelta = 0;
        }

        private void FitCameras(MonoTestbed window, bool restoreSaved)
        {
            if (wrapView?.Shapes is null || wrapView.Shapes.Length == 0)
            {
                return;
            }

            Geometry.Rectangle bRect = wrapView.Shapes.BoundingBox();
            bool restored = restoreSaved && window.Scene.RestoreCamera(TestMode.BAJAJTEST);
            if (restored == false)
            {
                scene.Camera.LookAt = bRect.Center.ToXNAVector2();
                scene.Camera.Downsample = FitDownsample(window, bRect);
            }


            AnnotationVizLib.MorphologyGraph morphology = CurrentTestCase.Morphology;
            if (morphology?.Nodes is not { Count: > 0 })
                return;

            Box bbox = new(bRect, morphology.Nodes.Values.Min(n => n.Z), morphology.Nodes.Values.Max(n => n.Z));
            double depth = Math.Max(bbox.Depth, 1);
            scene3D.Camera.Position = (bbox.CenterPoint.XY().ToVector3(0) + new Geometry.Vector3(0, 0, 10f * (float)depth)).ToXNAVector3();
            scene3D.Camera.LookAt = new Microsoft.Xna.Framework.Vector3((float)bbox.CenterPoint.X, (float)bbox.CenterPoint.Y, 0);
        }


        /// <summary>
        /// Zoom that fits the whole rectangle on screen.  Scaling to width alone crops the top and bottom of any
        /// shape taller than the viewport aspect allows, and because contours are hollow outlines the result is a
        /// frame that looks empty with only the left and right arcs clipping the edges.
        /// </summary>
        private static double FitDownsample(MonoTestbed window, Geometry.Rectangle rect, double pad = 1.1)
        {
            int width = Math.Max(1, window.GraphicsDevice.Viewport.Width);
            int height = Math.Max(1, window.GraphicsDevice.Viewport.Height);
            double byWidth = rect.Width * pad / width;
            double byHeight = rect.Height * pad / height;
            return Math.Max(Math.Max(byWidth, byHeight), 0.01);
        }

        private void ApplyShotCamera(BajajCaptureShot shot)
        {
            if (shot.LookAtX.HasValue && shot.LookAtY.HasValue)
                scene.Camera.LookAt = new Microsoft.Xna.Framework.Vector2(shot.LookAtX.Value, shot.LookAtY.Value);
            if (shot.Downsample.HasValue)
                scene.Camera.Downsample = shot.Downsample.Value;
        }

        /// <summary>
        /// Honor an explicit capture-request camera, otherwise zoom 2D chord/region shots to their overlay bounds.
        /// Overview and mesh shots fall back to the slice-pair framing from <see cref="FitCameras"/>.
        /// </summary>
        private void FrameShotCamera(MonoTestbed window, BajajCaptureShot shot)
        {
            if (shot.LookAtX.HasValue || shot.Downsample.HasValue)
            {
                ApplyShotCamera(shot);
                return;
            }

            if (!Draw3D && wrapView is not null && wrapView.TryGetShotBounds(shot, out Rectangle bounds))
            {
                double padX = Math.Max(bounds.Width * 0.1, 1);
                double padY = Math.Max(bounds.Height * 0.1, 1);
                Rectangle padded = new(bounds.Left - padX, bounds.Right + padX, bounds.Bottom - padY, bounds.Top + padY);
                scene.Camera.LookAt = padded.Center.ToXNAVector2();
                scene.Camera.Downsample = FitDownsample(window, padded, pad: 1.0);
                return;
            }

            FitCameras(window, restoreSaved: false);
        }

        /// <summary>
        /// Steps the interactive view through <see cref="BajajOTVAssignmentView.EnumerateDefaultShots"/>,
        /// the same list screenshot capture uses. Ignored while a screenshot dump is running.
        /// Mouse X2 / PageDown go forward; X1 / PageUp go back. Wrap around. The first press
        /// starts at overview (forward) or the last shot (back) rather than guessing the stacked A/B/Y overlays.
        /// PageUp/PageDown are omitted in 3D so they can pan world Z on the camera.
        /// </summary>
        private void UpdateDisplayShotInput()
        {
            MouseState mouse = Mouse.GetState();
            bool x2Clicked = false;
            bool x1Clicked = false;
            if (_mouseSeen)
            {
                x2Clicked = mouse.XButton2 == ButtonState.Pressed && _lastMouse.XButton2 != ButtonState.Pressed;
                x1Clicked = mouse.XButton1 == ButtonState.Pressed && _lastMouse.XButton1 != ButtonState.Pressed;
            }

            _lastMouse = mouse;
            _mouseSeen = true;

            if (_screenshotPhase != ScreenshotPhase.Inactive)
                return;

            if (x2Clicked || (!Draw3D && Input.Keyboard.Pressed(Keys.PageDown)))
                _pendingShotDelta++;
            else if (x1Clicked || (!Draw3D && Input.Keyboard.Pressed(Keys.PageUp)))
                _pendingShotDelta--;

            TryApplyPendingShotStep();
        }

        /// <summary>
        /// Applies queued forward/back steps without blocking the game thread on mesh-generation locks.
        /// Interactive order is overview, then line/chord passes (OTV only if chords exist), then 2D mesh, then regions.
        /// A click during view construction is kept in <see cref="_pendingShotDelta"/> until the lock is free.
        /// </summary>
        private void TryApplyPendingShotStep()
        {
            if (_pendingShotDelta == 0 || wrapView is null)
                return;

            if (!Monitor.TryEnter(wrapView.ViewsLock))
                return;

            try
            {
                List<BajajCaptureShot> shots = wrapView.EnumerateInteractiveShotsUnlocked();
                if (shots.Count == 0)
                    return;

                int next;
                if (_displayShotIndex is int current && current >= 0 && current < shots.Count)
                    next = Mod(_displayShotIndex.Value + _pendingShotDelta, shots.Count);
                else
                    next = _pendingShotDelta > 0 ? 0 : shots.Count - 1;

                BajajCaptureShot shot = shots[next];
                wrapView.ApplyShotUnlocked(shot);
                Draw3D = shot.Draw3D;
                _displayShotIndex = next;
                _pendingShotDelta = 0;
            }
            finally
            {
                Monitor.Exit(wrapView.ViewsLock);
            }
        }

        private static int Mod(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }

        private void UpdateScreenshotCapture()
        {
            switch (_screenshotPhase)
            {
                case ScreenshotPhase.WaitFullscreen:
                    if (_host is not null)
                        SyncCaptureViewports(_host);
                    _screenshotPhase = ScreenshotPhase.WaitMesh;
                    break;

                case ScreenshotPhase.WaitMesh:
                    if (wrapView is null)
                    {
                        _screenshotPhase = ScreenshotPhase.NextRepro;
                        return;
                    }

                    if (!wrapView.IsMeshGenerationFinished)
                        return;

                    // Capture whatever views exist even when generation faulted (overview / partial stages).
                    _screenshotPhase = ScreenshotPhase.Capture;
                    break;

                case ScreenshotPhase.NextRepro:
                    AdvanceToNextRepro();
                    break;

                case ScreenshotPhase.Done:
                    break;
            }
        }

        private void AdvanceToNextRepro()
        {
            while (true)
            {
                _reproQueueIndex++;
                if (_reproQueueIndex >= _reproQueue.Count)
                {
                    ScreenshotCapture.WriteManifest(_screenshotRoot, _manifest);
                    _screenshotPhase = ScreenshotPhase.Done;
                    if (Program.options?.Quiet == true)
                        _host?.Exit();
                    return;
                }

                try
                {
                    LoadReproAtQueueIndex(_host, restoreCamera: false);
                    _screenshotPhase = ScreenshotPhase.WaitMesh;
                    return;
                }
                catch (Exception ex)
                {
                    RecordCaseError(ex.ToString());
                }
            }
        }

        private void CaptureCurrentCase(MonoTestbed window)
        {
            if (wrapView is null)
                throw new InvalidOperationException("Cannot capture screenshots before the Bajaj view is created.");
            SyncCaptureViewports(window);
            FitCameras(window, restoreSaved: false);
            List<BajajCaptureShot> defaults = wrapView.EnumerateDefaultShots();
            List<BajajCaptureShot> shots = ScreenshotCapture.ResolveRequestedShots(defaults, Program.options?.CaptureRequest?.Shots);

            string caseDir = System.IO.Path.Combine(_screenshotRoot, _currentCaseFolder);
            Directory.CreateDirectory(caseDir);

            for (int i = 0; i < shots.Count; i++)
            {
                BajajCaptureShot shot = shots[i];
                wrapView.ApplyShot(shot);
                Draw3D = shot.Draw3D;
                FrameShotCamera(window, shot);

                string fileName = $"{i:D2}-{shot.FileSlug}.png";
                string fullPath = System.IO.Path.Combine(caseDir, fileName);
                ScreenshotCapture.SavePng(window.GraphicsDevice, fullPath, () =>
                {
                    DrawCurrentView(window);
                    window.DrawLegendHUD();
                });

                _currentManifestCase.Shots.Add(new CaptureManifestShot
                {
                    Stage = shot.Stage,
                    View = shot.View,
                    RelativePath = System.IO.Path.Combine(_currentCaseFolder, fileName).Replace('\\', '/'),
                    LookAtX = scene.Camera.LookAt.X,
                    LookAtY = scene.Camera.LookAt.Y,
                    Downsample = scene.Camera.Downsample
                });
            }

            if (wrapView.IsMeshFaulted)
            {
                string message = wrapView.MeshFault?.ToString() ?? "Mesh generation faulted.";
                _currentManifestCase.Error = message;
                File.WriteAllText(System.IO.Path.Combine(caseDir, "error.txt"), message);
            }

            _manifest.Cases.Add(_currentManifestCase);
            ScreenshotCapture.WriteManifest(_screenshotRoot, _manifest);
            DrawCurrentView(window);
        }

        private void DrawCurrentView(MonoTestbed window)
        {
            //A scene built at load time keeps whatever viewport the device had then.  The window is resizable and
            //the back buffer is sized from the display, so a stale viewport both stretches the projection and lands
            //screen-space labels at the wrong pixels.
            MonoTestbed.SyncViewport(scene, window.GraphicsDevice);
            MonoTestbed.SyncViewport(scene3D, window.GraphicsDevice);

            if (!Draw3D)
                wrapView?.Draw(window, scene);
            else
                wrapView?.Draw3D(window, scene3D);
        }

        private void RecordCaseError(string message)
        {
            Console.WriteLine(message);
            Trace.WriteLine(message);

            int index = _reproQueue is { Count: > 0 } && _reproQueueIndex < _reproQueue.Count
                ? _reproQueue[_reproQueueIndex]
                : CurrentReproCase;

            _currentManifestCase ??= new CaptureManifestCase
            {
                Index = index,
                Description = CurrentTestCase?.Description,
                LocationIds = CurrentTestCase?.SliceLocations,
                Endpoint = CurrentTestCase?.Endpoint?.ToString(),
                Folder = _currentCaseFolder ?? $"case-{index:D2}-error"
            };
            _currentManifestCase.Error = message;

            if (_manifest is not null && !_manifest.Cases.Contains(_currentManifestCase))
                _manifest.Cases.Add(_currentManifestCase);

            if (!string.IsNullOrEmpty(_screenshotRoot))
            {
                string folder = _currentManifestCase.Folder ?? _currentCaseFolder ?? $"case-{index:D2}-error";
                string dir = System.IO.Path.Combine(_screenshotRoot, folder);
                Directory.CreateDirectory(dir);
                File.WriteAllText(System.IO.Path.Combine(dir, "error.txt"), message);
                ScreenshotCapture.WriteManifest(_screenshotRoot, _manifest);
            }

            _currentManifestCase = null;
        }

        /// <summary>
        /// Fullscreen at native monitor resolution and copy the live viewport onto both Bajaj scenes
        /// so PNG dumps are not stuck at the 1600×1200 windowed back buffer.
        /// </summary>
        private void SyncCaptureViewports(MonoTestbed window)
        {
            window.EnsureExportFullscreen();
            window.SyncSceneViewport();
            Viewport viewport = window.GraphicsDevice.Viewport;
            if (scene is not null)
                scene.Viewport = viewport;
            if (scene3D is not null)
                scene3D.Viewport = viewport;
        }
    }
}
