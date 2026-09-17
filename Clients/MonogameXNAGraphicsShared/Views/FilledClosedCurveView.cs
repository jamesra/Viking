using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    /// <summary>
    /// A polygon where we smooth the verticies on the borders using a curve.  Interior holes are also smoothed
    /// </summary>
    public class FilledClosedCurvePolygonView : IColorView
    {
        public Color Color { get; set; }
        public float Alpha
        {
            get => Color.GetAlpha();
            set => Color = Color.SetAlpha(value);
        }

        private CurveViewControlPoints _ExteriorCurveControlPoints;
        private ICollection<CurveViewControlPoints> _InteriorCurveControlPoints;

        private readonly MeshModel<VertexPositionColor> _mesh;

        public FilledClosedCurvePolygonView(ICollection<Geometry.Vector2> exteriorControlPoints, ICollection<Geometry.Vector2[]> interiorPolyControlPoints, Color color, uint numInterpolations)
        {
            this.Color = color;
            InitializeCurveControlPoints(exteriorControlPoints, interiorPolyControlPoints, numInterpolations);
            _mesh = CreateMesh();
        }

        private void InitializeCurveControlPoints(ICollection<Geometry.Vector2> exteriorControlPoints, ICollection<Geometry.Vector2[]> interiorPolyControlPoints, uint numInterpolations)
        {
            this._ExteriorCurveControlPoints = new CurveViewControlPoints(exteriorControlPoints, numInterpolations, true);

            _InteriorCurveControlPoints = new CurveViewControlPoints[interiorPolyControlPoints.Count];

            foreach (Geometry.Vector2[] interiorPoints in interiorPolyControlPoints)
            {
                CurveViewControlPoints interiorCurve = new(interiorPoints, numInterpolations, true);
                _InteriorCurveControlPoints.Add(interiorCurve);
            }
        }

        private MeshModel<VertexPositionColor> CreateMesh()
        {
            MeshModel<VertexPositionColor> mesh = TriangleNetExtensions.CreateMeshForPolygon2D(_ExteriorCurveControlPoints.CurvePoints,
                                                                                               [.. _InteriorCurveControlPoints.Select(ic => ic.CurvePoints)],
                                                                                               Color);
            return mesh;
        }

        public static void Draw(GraphicsDevice device, IScene scene, IEnumerable<FilledClosedCurvePolygonView> views) => MeshView<VertexPositionColor>.Draw(device, scene, DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device), meshmodels: views.Select(v => v._mesh));
    }
}
