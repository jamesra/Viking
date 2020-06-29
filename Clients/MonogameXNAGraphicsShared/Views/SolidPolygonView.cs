using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class SolidPolygonView : IColorView, IRenderable, IScale, IViewPosition2D
    {
        PositionColorMeshModel meshModel = null;

        MeshView<VertexPositionColor> modelView = null;

        /// <summary>
        /// Centroid of the polygon
        /// </summary>
        GridVector2 _Position;

        float _Scale = 1.0f;

        public SolidPolygonView(GridPolygon poly, Color color)
        {
            GridVector2 _Position = poly.Centroid;
            
            //Center the polygon to reduce rounding error and because we'll position the polygon with the matrix
            GridPolygon centered_poly = poly.Translate(-_Position);

            var Mesh = centered_poly.Triangulate();

            meshModel = Mesh.ToVertexPositionColorMeshModel(color);
            meshModel.ModelMatrix = Matrix.CreateTranslation((float)_Position.X, (float)_Position.Y, 0);
            
            modelView = new MeshView<VertexPositionColor>();

            modelView.models.Add(meshModel); 
        }

        public Color Color { get => meshModel.Color; set => meshModel.Color = value; }
        public float Alpha { get => meshModel.Alpha; set => meshModel.Alpha = value; }
        public float Scale { get => _Scale;
            set
            {
                _Scale = value;
                UpdateModelMatrix();
            }
        }

        public GridVector2 Position
        {
            get => _Position;

            set
            {
                _Position = value;
                UpdateModelMatrix();
            }
        }

        private void UpdateModelMatrix()
        {
            meshModel.ModelMatrix = Matrix.CreateScale(_Scale, _Scale, _Scale) * Matrix.CreateTranslation((float)_Position.X, (float)_Position.Y, 0);
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            switch (Overlay)
            {
                case OverlayStyle.Alpha:
                    modelView.Draw(device, scene, CullMode.CullClockwiseFace);
                    break;
                case OverlayStyle.Luma:
                    PolygonOverlayEffect effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device);
                    modelView.Draw(device, scene, effect,
                        CullMode.CullClockwiseFace);
                    break;
                default:
                    throw new NotImplementedException();
            } 
        }

        public static void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IEnumerable<SolidPolygonView> items)
        {
            switch (Overlay)
            {
                case OverlayStyle.Alpha:
                    MeshView<VertexPositionColor>.Draw(device, scene,
                        effect: null,
                        cullmode: CullMode.CullClockwiseFace,
                        fillMode: FillMode.Solid,
                        meshViews: items.Select(item => item.modelView).ToArray());
                    break;
                case OverlayStyle.Luma:
                    PolygonOverlayEffect effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device);
                    MeshView<VertexPositionColor>.Draw(device, scene,
                        effect,
                        CullMode.CullClockwiseFace,
                        FillMode.Solid,
                        items.Select(item => item.meshModel));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            SolidPolygonView.Draw(device, scene, Overlay, items.Select(item => item as SolidPolygonView).Where(item => item != null).ToArray());
        }
    }
}
