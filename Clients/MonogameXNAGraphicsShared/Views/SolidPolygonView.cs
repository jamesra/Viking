using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public class SolidPolygonView : IColorView, IRenderable, IScale
    {
        PositionColorMeshModel _meshModel = null;
        Task<PositionColorMeshModel> _modelTask = null;
        /// <summary>
        /// Set when triangulation fails. A faulted task is IsCompleted, and reading Result throws on every paint.
        /// That exception leaves the pen command on screen, so later strokes never start.
        /// </summary>
        bool _modelFailed;

        PositionColorMeshModel meshModel
        {
            get
            {
                try
                {
                    ModelRWLock.EnterUpgradeableReadLock();

                    if (_modelFailed)
                        return null;

                    if (_meshModel is null)
                    {
                        if (_modelTask is null)
                        {
                            _modelTask = Task<PositionColorMeshModel>.Run(() => InitializeModel(InputPolygon, InputColor));
                        }
                        else if (_modelTask.IsFaulted || _modelTask.IsCanceled)
                        {
                            Trace.WriteLine($"Could not generate view for polygon {InputPolygon}: {_modelTask.Exception?.GetBaseException().Message}");
                            _modelFailed = true;
                            _modelTask = null;
                            return null;
                        }
                        else if (_modelTask.IsCompleted)
                        {
                            try
                            {
                                ModelRWLock.EnterWriteLock();
                                _meshModel = _modelTask.Result;
                            }
                            finally
                            {
                                ModelRWLock.ExitWriteLock();
                            }

                            _modelTask = null;
                            if (_meshModel is null)
                                _modelFailed = true;
                            return _meshModel;
                        }

                        //Task is still running.  Return null for now.
                        return null;
                    }

                    return _meshModel;
                }
                finally
                {
                    ModelRWLock.ExitUpgradeableReadLock();
                }
            }
        }

        /*
        MeshView<VertexPositionColor> _modelView = null;

        Task<MeshView<VertexPositionColor>> _modelViewTask = null;
        MeshView<VertexPositionColor> modelView
        {
            get
            {
                try
                {
                    ModelRWLock.EnterUpgradeableReadLock();

                    if (_modelView is null)
                    {
                        if(_modelViewTask is null)
                        {
                            _modelViewTask = Task<MeshView<VertexPositionColor>>.Run(() => InitializeModelView(InputPolygon, InputColor) );
                        }
                        else
                        {
                            if (_modelViewTask.IsCompleted)
                            {
                                try
                                {
                                    ModelRWLock.EnterWriteLock();
                                    _modelView = _modelViewTask.Result;
                                }
                                finally
                                {
                                    ModelRWLock.ExitWriteLock();
                                }

                                _modelViewTask = null;
                                return null;
                            }
                            else if (_modelViewTask.IsFaulted)
                            {
                                Trace.WriteLine(string.Format("Could not generate view for polygon {0}", InputPolygon));
                                _modelViewTask = null;
                                return null;
                            }
                            else if (_modelViewTask.IsCanceled)
                            {
                                Trace.WriteLine(string.Format("Could not generate view for polygon {0}", InputPolygon));
                                _modelViewTask = null;
                                return null;
                            }

                            //Task is still running.  Return null for now.
                            return null;
                        }
                    }

                    return _modelView;
                }
                finally
                {
                    ModelRWLock.ExitUpgradeableReadLock();
                }
            }
        }
        */

        /// <summary>
        /// Centroid of the polygon
        /// </summary>
        Geometry.Vector2 _Position;

        float _Scale = 1.0f;

        /// <summary>
        /// The polygon being displayed
        /// </summary>
        public readonly Polygon InputPolygon;

        /// <summary>
        /// Color passed to the constructor.  Used to create the first mesh.
        /// </summary>
        private Color InputColor;

        readonly System.Threading.ReaderWriterLockSlim ModelRWLock = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="color"></param>
        /// <param name="LazyInit">If Lazy init is set to true, we do not start a task to triangulate the polygon until Draw is called.  At that point triangulation is performed in parallel. </param>
        public SolidPolygonView(Polygon poly, Color color, bool LazyInit = true)
        {
            //Debug.Assert(poly.TotalUniqueVertices < 1000, "This is a huge polygon, why?");

            InputPolygon = poly;
            InputColor = color;

            if (LazyInit == false)
            {
                _meshModel = InitializeModel(InputPolygon, InputColor);
                _modelFailed = _meshModel is null;
            }
        }

        private static PositionColorMeshModel InitializeModel(Polygon InputPolygon, Color InputColor)
        {
            Geometry.Vector2 _Position;
            Polygon centered_poly;
            try
            {
                _Position = InputPolygon.Centroid;
                //Center the polygon to reduce rounding error and because we'll position the polygon with the matrix.
                //Translate rebuilds the ring and rejects a stroke that is not a closed polygon.
                centered_poly = InputPolygon.Translate(-_Position);
            }
            catch (ArgumentException ex)
            {
                Trace.WriteLine($"Could not center polygon for display: {ex.Message}");
                return null;
            }
            TriangulationMesh<IVertex2D<PolygonIndex>> Mesh;
            try
            {
                Mesh = centered_poly.Triangulate();
            }
            catch (EdgesIntersectTriangulationException)
            {
                Mesh = TrySimplifyPolygon(centered_poly);
                if (Mesh is null)
                    throw;
            }
            catch (NonconformingTriangulationException)
            {
                Mesh = TrySimplifyPolygon(centered_poly);
                if (Mesh is null)
                    throw;
            }

            var mesh_model = Mesh.ToVertexPositionColorMeshModel(InputColor);
            mesh_model.ModelMatrix = Matrix.CreateTranslation((float)_Position.X, (float)_Position.Y, 0);
            return mesh_model;
        }

        /// <summary>
        /// Meshes that are ready to draw. In-progress and failed polygons are omitted so paint does not throw.
        /// </summary>
        private static PositionColorMeshModel[] ReadyMeshes(IEnumerable<SolidPolygonView> items)
        {
            List<PositionColorMeshModel> meshes = [];
            foreach (SolidPolygonView item in items)
            {
                PositionColorMeshModel mesh = item.meshModel;
                if (mesh != null)
                    meshes.Add(mesh);
            }

            return [.. meshes];
        }

        private static TriangulationMesh<IVertex2D<PolygonIndex>> TrySimplifyPolygon(Polygon centered_poly)
        {
            Trace.WriteLine(string.Format("Could not triangulate polygon {0}.  Atttempting to simplify", centered_poly));
            Polygon simpler_centered_poly = centered_poly.Simplify(1.0, NumInterpolations: 6);
            if (simpler_centered_poly.TotalUniqueVertices == centered_poly.TotalUniqueVertices)
            {
                return null;
            }

            try
            {
                return simpler_centered_poly.Triangulate();
            }
            catch (EdgesIntersectTriangulationException)
            {
                Trace.WriteLine($"Simplification failed using {simpler_centered_poly}.");
                return null;
            }
        }

        private static MeshView<VertexPositionColor> InitializeModelView(Polygon InputPolygon, Color InputColor)
        {
            var model = InitializeModel(InputPolygon, InputColor);

            MeshView<VertexPositionColor> meshView = new();
            meshView.models.Add(model);
            return meshView;
        }

        public Color Color
        {
            get => _meshModel != null ? _meshModel.Color : InputColor;
            set
            {
                if (_meshModel != null)
                    _meshModel.Color = value;
                else
                    InputColor = value;
            }
        }

        public float Alpha
        {
            get => _meshModel != null ? _meshModel.Alpha : InputColor.GetAlpha();
            set
            {
                if (_meshModel != null)
                    _meshModel.Alpha = value;
                else
                    InputColor.SetAlpha(value);
            }
        }

        public float Scale
        {
            get => _Scale;
            set
            {
                _Scale = value;
                UpdateModelMatrix();
            }
        }

        public Geometry.Vector2 Position
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
            PositionColorMeshModel mesh = meshModel;
            if (mesh is null)
                return;

            mesh.ModelMatrix = Matrix.CreateScale(_Scale, _Scale, _Scale) * Matrix.CreateTranslation((float)_Position.X, (float)_Position.Y, 0);
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay) => Draw(device, scene, Overlay, new SolidPolygonView[] { this });

        public static void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IEnumerable<SolidPolygonView> items)
        {
            PositionColorMeshModel[] meshes = ReadyMeshes(items);
            if (meshes.Length == 0)
                return;

            switch (Overlay)
            {
                case OverlayStyle.Alpha:
                    MeshView<VertexPositionColor>.Draw(device,
                        scene,
                        effect: new BasicEffect(device),
                        cullmode: CullMode.CullClockwiseFace,
                        fillMode: FillMode.Solid,
                        meshmodels: meshes);
                    break;
                case OverlayStyle.Luma:
                    PolygonOverlayEffect effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device);
                    MeshView<VertexPositionColor>.Draw(device,
                        scene,
                        effect,
                        CullMode.CullClockwiseFace,
                        FillMode.Solid,
                        meshmodels: meshes);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items) => SolidPolygonView.Draw(device, scene, Overlay, [.. items.Select(item => item as SolidPolygonView).Where(item => item != null)]);

        public bool Contains(Geometry.Vector2 Position) => this.InputPolygon.Covers(Position);
    }
}
