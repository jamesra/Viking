using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands;
using Vector2 = Geometry.Vector2;

namespace WebAnnotation.UI.ActionViews
{
    /// <summary>
    /// Choice fill for a pen cut. Draws the cached cell and, when present, only the local patch.
    /// Started from <see cref="Change2DContourActionView"/> so the button mask does not triangulate the far side of the cell.
    /// </summary>
    internal sealed class CutChoiceFillView : IRenderable
    {
        readonly Polygon _original;
        readonly Polygon _excludedPatch;
        readonly Polygon _addedPatch;
        readonly IReadOnlyList<Vector2> _path;
        readonly Color _color;
        Task<PositionColorMeshModel[]> _build;
        PositionColorMeshModel[] _meshes;

        public CutChoiceFillView(Polygon original, Polygon excludedPatch, Polygon addedPatch, IReadOnlyList<Vector2> path, Color color)
        {
            _original = original;
            _excludedPatch = excludedPatch;
            _addedPatch = addedPatch;
            _path = path;
            _color = color;
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle overlay)
        {
            EnsureBuild();
            if (_meshes is null)
            {
                if (_build.Status == TaskStatus.RanToCompletion)
                    _meshes = _build.Result;
                else if (_build.IsFaulted || _build.IsCanceled)
                    _meshes = [];
                else
                    return;
            }

            if (_meshes is null || _meshes.Length == 0)
                return;

            switch (overlay)
            {
                case OverlayStyle.Alpha:
                    // Null selects the shared BasicEffect inside MeshView. Cull matches SolidPolygonView so the counter-clockwise fill stays visible.
                    MeshView<VertexPositionColor>.Draw(device, scene, (BasicEffect)null, CullMode.CullClockwiseFace, FillMode.Solid, meshmodels: _meshes);
                    break;
                case OverlayStyle.Luma:
                    MeshView<VertexPositionColor>.Draw(device, scene, DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device), CullMode.CullClockwiseFace, FillMode.Solid, meshmodels: _meshes);
                    break;
            }
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle overlay, IRenderable[] items)
        {
            foreach (IRenderable item in items)
                item?.Draw(device, scene, overlay);
        }

        void EnsureBuild()
        {
            if (_build is not null || _meshes is not null)
                return;

            Polygon original = _original;
            Polygon excluded = _excludedPatch;
            Polygon added = _addedPatch;
            IReadOnlyList<Vector2> path = _path;
            Color color = _color;
            Task<PolygonCutFill> cache = PolygonCutFillCache.Begin(original);
            _build = Task.Run(() => Build(cache, excluded, added, path, color));
        }

        static PositionColorMeshModel[] Build(Task<PolygonCutFill> cache, Polygon excluded, Polygon added, IReadOnlyList<Vector2> path, Color color)
        {
            PolygonCutFill fill;
            try
            {
                fill = cache.GetAwaiter().GetResult();
            }
            catch (System.Exception)
            {
                return [];
            }

            int[] baseIndices = fill.TriangleIndices as int[] ?? [.. fill.TriangleIndices];
            if (excluded is not null && path is { Count: >= 2 })
            {
                int[] capOrdinals = fill.CapTriangleOrdinals(path, excluded.ExteriorRing);
                baseIndices = fill.IndicesExcludingOrdinals(capOrdinals);
            }

            List<PositionColorMeshModel> meshes = new(2);
            PositionColorMeshModel baseMesh = PolygonCutMeshBuilder.FromIndices(fill, baseIndices, color);
            if (baseMesh is not null)
                meshes.Add(baseMesh);

            // The excluded cap is drawn by the other choice. Only a growth lobe is added here, in the same color.
            PositionColorMeshModel patchMesh = PolygonCutMeshBuilder.FromPatch(added, color);
            if (patchMesh is not null)
                meshes.Add(patchMesh);

            return [.. meshes];
        }
    }
}
