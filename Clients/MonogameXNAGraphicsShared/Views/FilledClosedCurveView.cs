using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;

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
            get { return Color.GetAlpha(); }
            set { Color = Color.SetAlpha(value); }
        }

        private CurveViewControlPoints _ExteriorCurveControlPoints;
        private ICollection<CurveViewControlPoints> _InteriorCurveControlPoints;

        private MeshModel<VertexPositionColor> _mesh;

        public FilledClosedCurvePolygonView(ICollection<GridVector2> exteriorControlPoints, ICollection<GridVector2[]> interiorPolyControlPoints, Color color, uint numInterpolations)
        {
            this.Color = color; 
            InitializeCurveControlPoints(exteriorControlPoints, interiorPolyControlPoints, numInterpolations);
            _mesh = CreateMesh();
        }

        private void InitializeCurveControlPoints(ICollection<GridVector2> exteriorControlPoints, ICollection<GridVector2[]> interiorPolyControlPoints, uint numInterpolations)
        {
            this._ExteriorCurveControlPoints = new CurveViewControlPoints(exteriorControlPoints, numInterpolations, true);

            _InteriorCurveControlPoints = new CurveViewControlPoints[interiorPolyControlPoints.Count];

            foreach (GridVector2[] interiorPoints in interiorPolyControlPoints)
            {
                CurveViewControlPoints interiorCurve = new CurveViewControlPoints(interiorPoints, numInterpolations, true);
                _InteriorCurveControlPoints.Add(interiorCurve);
            }
        }

        private MeshModel<VertexPositionColor> CreateMesh()
        {
            MeshModel<VertexPositionColor> mesh = TriangleNetExtensions.CreateMeshForPolygon2D(_ExteriorCurveControlPoints.CurvePoints,
                                                                                               _InteriorCurveControlPoints.Select(ic => ic.CurvePoints).ToArray(),
                                                                                               Color);
            return mesh;
        }

        public static void Draw(GraphicsDevice device, Scene scene, IEnumerable<FilledClosedCurvePolygonView> views)
        {
            MeshView<VertexPositionColor>.Draw(device, scene, views.Select(v => v._mesh));
        }
    }
}
