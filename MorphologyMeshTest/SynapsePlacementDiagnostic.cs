using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live-data diagnostic for child-structure (synapse) placement against RC1. Categorized so it stays out of
    /// normal runs; it reports coordinates rather than asserting, and exists to compare a child structure's
    /// annotation position against the frame its mesh is generated in.
    /// </summary>
    [TestClass]
    public class SynapsePlacementDiagnostic
    {
        private const ulong CellStructureId = 476;
        private const ulong PsdStructureId = 16495;

        [TestMethod]
        [TestCategory("LiveData")]
        [Ignore("Reporting tool, not a regression test: it takes about a minute and depends on RC1 being reachable. Remove this attribute to re-run it.")]
        public async Task ReportPsdPlacementRelativeToCell()
        {
            Uri endpoint = new("http://websvc.codepharm.net/RC1/OData");

            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { (long)CellStructureId }, true, endpoint);

            MorphologyGraph cell = root.Subgraphs[CellStructureId];
            Console.WriteLine($"Cell {CellStructureId}: {cell.Nodes.Count} nodes, {cell.Subgraphs.Count} children");

            Box cellBox = cell.NodesBoundingBox;
            Vector2 cellOrigin = cellBox.CenterPoint.XY();
            Console.WriteLine($"Cell annotation center (nm): ({cellOrigin.X:F0}, {cellOrigin.Y:F0})");
            Console.WriteLine($"Cell annotation bbox   X {cellBox.MinVals[0]:F0}..{cellBox.MaxVals[0]:F0}  Y {cellBox.MinVals[1]:F0}..{cellBox.MaxVals[1]:F0}");

            MorphologyGraph psd = cell.Subgraphs[PsdStructureId];
            Box psdBox = psd.NodesBoundingBox;
            Console.WriteLine($"PSD {PsdStructureId}: {psd.Nodes.Count} nodes, {psd.Edges.Count} edges");
            Console.WriteLine($"PSD annotation center (nm): ({psdBox.CenterPoint.X:F0}, {psdBox.CenterPoint.Y:F0})");
            Console.WriteLine($"PSD annotation bbox    X {psdBox.MinVals[0]:F0}..{psdBox.MaxVals[0]:F0}  Y {psdBox.MinVals[1]:F0}..{psdBox.MaxVals[1]:F0}");

            //Mirrors BajajMultiTest.QueueMeshViews: children are meshed in the parent cell's XY frame.
            SliceGraph psdSlices = await SliceGraph.Create(psd, 2.0, cellOrigin);
            Console.WriteLine($"PSD SliceGraph XYOrigin (nm): ({psdSlices.XYOrigin.X:F0}, {psdSlices.XYOrigin.Y:F0})");
            Console.WriteLine($"PSD SliceGraph slices: {psdSlices.Nodes.Count}");

            foreach (var key in psdSlices.Nodes.Keys.OrderBy(k => k))
            {
                SliceTopology topology = psdSlices.GetTopology(key);
                if (!topology.IsValid || topology.Shapes is null || topology.Shapes.Length == 0)
                {
                    Console.WriteLine($"  slice {key}: INVALID topology");
                    continue;
                }

                Rectangle local = topology.Shapes.BoundingBox();
                Rectangle restored = local.Translate(psdSlices.XYOrigin);
                Console.WriteLine($"  slice {key}: local    X {local.Left:F0}..{local.Right:F0}  Y {local.Bottom:F0}..{local.Top:F0}");
                Console.WriteLine($"  slice {key}: restored X {restored.Left:F0}..{restored.Right:F0}  Y {restored.Bottom:F0}..{restored.Top:F0}");
            }
        }

        /// <summary>
        /// Measures whether the parent cell's generated mesh actually has surface next to the synapse. Bounding
        /// boxes cannot answer this: a thin branching cell fills a small fraction of its own bbox, so a local
        /// hole in the membrane leaves both the bbox and the slice failure count looking healthy.
        /// </summary>
        [TestMethod]
        [TestCategory("LiveData")]
        [Ignore("Reporting tool: meshes a whole cell against RC1 and takes over a minute. Remove this attribute to re-run it.")]
        public async Task ReportCellMeshProximityToPsd()
        {
            Uri endpoint = new("http://websvc.codepharm.net/RC1/OData");

            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { (long)CellStructureId }, true, endpoint);

            MorphologyGraph cell = root.Subgraphs[CellStructureId];
            MorphologyGraph psd = cell.Subgraphs[PsdStructureId];

            Vector2 cellOrigin = cell.NodesBoundingBox.CenterPoint.XY();
            Box psdBox = psd.NodesBoundingBox;
            Vector3 psdCenter = psdBox.CenterPoint;
            Console.WriteLine($"PSD {PsdStructureId} center (nm): ({psdCenter.X:F0}, {psdCenter.Y:F0}, {psdCenter.Z:F0})");

            //Nearest annotation of the cell, which is the distance the mesh ought to roughly reproduce.
            double nearestAnnotation = double.MaxValue;
            ulong nearestAnnotationId = 0;
            foreach (var node in cell.Nodes.Values)
            {
                Vector3 c = node.BoundingBox.CenterPoint;
                double d = Vector3.Distance(c, psdCenter);
                if (d < nearestAnnotation)
                {
                    nearestAnnotation = d;
                    nearestAnnotationId = node.ID;
                }
            }

            Console.WriteLine($"Nearest CELL ANNOTATION to PSD: {nearestAnnotation:F0} nm (location {nearestAnnotationId})");

            //The cell is meshed on its own, exactly as BajajMultiTest meshes it.
            MorphologyGraph cellOnly = root.Subgraphs[CellStructureId];
            SliceGraph slices = await SliceGraph.Create(cellOnly, 2.0, cellOrigin);
            var meshes = await BajajMeshGenerator.ConvertToMesh(slices);

            double nearestVertex = double.MaxValue;
            long vertexCount = 0;
            foreach (var mesh in meshes)
            {
                if (mesh?.Vertices is null)
                    continue;

                foreach (var v in mesh.Vertices)
                {
                    vertexCount++;
                    double dx = v.Position.X + slices.XYOrigin.X - psdCenter.X;
                    double dy = v.Position.Y + slices.XYOrigin.Y - psdCenter.Y;
                    double dz = v.Position.Z - psdCenter.Z;
                    double d2 = (dx * dx) + (dy * dy) + (dz * dz);
                    if (d2 < nearestVertex)
                        nearestVertex = d2;
                }
            }

            nearestVertex = Math.Sqrt(nearestVertex);
            Console.WriteLine($"Cell mesh vertices scanned: {vertexCount}");
            Console.WriteLine($"Nearest CELL MESH VERTEX to PSD: {nearestVertex:F0} nm");
            Console.WriteLine(nearestVertex > 5000
                ? "VERDICT: the cell mesh has NO surface near the synapse (local hole)."
                : "VERDICT: the cell mesh does have surface next to the synapse.");
        }

        /// <summary>
        /// Lists the child structures whose annotations sit furthest from the parent cell, to distinguish a
        /// rendering placement fault from children whose annotation data genuinely lies away from the cell.
        /// </summary>
        [TestMethod]
        [TestCategory("LiveData")]
        [Ignore("Reporting tool: needs RC1 and takes about half a minute. Remove this attribute to re-run it.")]
        public async Task ReportChildrenFurthestFromCell()
        {
            Uri endpoint = new("http://websvc.codepharm.net/RC1/OData");

            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { (long)CellStructureId }, true, endpoint);

            MorphologyGraph cell = root.Subgraphs[CellStructureId];
            Box cellBox = cell.NodesBoundingBox;
            Vector2 cellCenter = cellBox.CenterPoint.XY();

            Console.WriteLine($"Cell annotation bbox X {cellBox.MinVals[0]:F0}..{cellBox.MaxVals[0]:F0}  Y {cellBox.MinVals[1]:F0}..{cellBox.MaxVals[1]:F0}");
            Console.WriteLine($"Cell annotation width {cellBox.MaxVals[0] - cellBox.MinVals[0]:F0} nm");

            Box rootBox = root.BoundingBox;
            Console.WriteLine($"ROOT graph.BoundingBox X {rootBox.MinVals[0]:F0}..{rootBox.MaxVals[0]:F0}  Y {rootBox.MinVals[1]:F0}..{rootBox.MaxVals[1]:F0}");
            Console.WriteLine($"ROOT width {rootBox.MaxVals[0] - rootBox.MinVals[0]:F0} nm  (HUD shows half of this)");

            var offenders = cell.Subgraphs.Values
                .Where(child => child.Nodes.Count > 0)
                .Select(child =>
                {
                    Vector2 c = child.NodesBoundingBox.CenterPoint.XY();
                    return (child.StructureID, Center: c, Distance: Vector2.Distance(c, cellCenter));
                })
                .OrderByDescending(t => t.Distance)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Children: {cell.Subgraphs.Count}. Ten furthest from the cell center ({cellCenter.X:F0}, {cellCenter.Y:F0}):");
            foreach (var o in offenders)
            {
                bool inside = o.Center.X >= cellBox.MinVals[0] && o.Center.X <= cellBox.MaxVals[0]
                           && o.Center.Y >= cellBox.MinVals[1] && o.Center.Y <= cellBox.MaxVals[1];
                Console.WriteLine($"  child {o.StructureID}: center ({o.Center.X:F0}, {o.Center.Y:F0})  dist {o.Distance:F0} nm  insideCellBbox={inside}");
            }
        }

        /// <summary>
        /// Measures the displacement BajajMultiTest's RefreshPlacementOffset would apply to the whole cell. That
        /// offset is "annotation bbox center minus mesh bbox center", so it only equals the true frame transform
        /// (the slice graph's XYOrigin) when the generated mesh covers the annotations. Any slice that fails to
        /// mesh moves the mesh bbox center and translates the entire cell by the difference.
        /// </summary>
        [TestMethod]
        [TestCategory("LiveData")]
        [Ignore("Reporting tool: meshes a whole 2000+ node cell, so it is slow and needs RC1. Remove this attribute to re-run it.")]
        public async Task ReportCellMeshBoundsVersusAnnotationBounds()
        {
            Uri endpoint = new("http://websvc.codepharm.net/RC1/OData");

            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { (long)CellStructureId }, false, endpoint);

            MorphologyGraph cell = root.Subgraphs[CellStructureId];
            Box cellBox = cell.NodesBoundingBox;
            Vector2 cellOrigin = cellBox.CenterPoint.XY();

            Console.WriteLine($"Cell {CellStructureId}: {cell.Nodes.Count} nodes");
            Console.WriteLine($"Annotation bbox X {cellBox.MinVals[0]:F0}..{cellBox.MaxVals[0]:F0}  Y {cellBox.MinVals[1]:F0}..{cellBox.MaxVals[1]:F0}");
            Console.WriteLine($"Annotation center ({cellOrigin.X:F0}, {cellOrigin.Y:F0})");

            SliceGraph slices = await SliceGraph.Create(cell, 2.0, cellOrigin);
            Console.WriteLine($"Slices: {slices.Nodes.Count}");

            var meshes = await BajajMeshGenerator.ConvertToMesh(slices);
            int withGeometry = 0;
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;

            foreach (var mesh in meshes)
            {
                if (mesh?.Vertices is null || mesh.Vertices.Count == 0)
                    continue;

                withGeometry++;
                foreach (var v in mesh.Vertices)
                {
                    minX = Math.Min(minX, v.Position.X);
                    maxX = Math.Max(maxX, v.Position.X);
                    minY = Math.Min(minY, v.Position.Y);
                    maxY = Math.Max(maxY, v.Position.Y);
                }
            }

            Console.WriteLine($"Meshes returned: {meshes.Count}, with geometry: {withGeometry}, slices without a mesh: {slices.Nodes.Count - withGeometry}");

            if (withGeometry == 0)
            {
                Console.WriteLine("No mesh geometry produced; cannot compare bounds.");
                return;
            }

            //Mesh vertices are in the origin-subtracted frame, so add XYOrigin to compare against annotations.
            double meshCenterX = ((minX + maxX) * 0.5) + slices.XYOrigin.X;
            double meshCenterY = ((minY + maxY) * 0.5) + slices.XYOrigin.Y;

            Console.WriteLine($"Mesh bbox (volume) X {minX + slices.XYOrigin.X:F0}..{maxX + slices.XYOrigin.X:F0}  Y {minY + slices.XYOrigin.Y:F0}..{maxY + slices.XYOrigin.Y:F0}");
            Console.WriteLine($"Mesh center (volume) ({meshCenterX:F0}, {meshCenterY:F0})");
            Console.WriteLine($"DISPLACEMENT applied by RefreshPlacementOffset: ({cellOrigin.X - meshCenterX:F0}, {cellOrigin.Y - meshCenterY:F0}) nm");
        }
    }
}
