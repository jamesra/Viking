using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class TriangulationView
    {
        PointSet Points_X = new PointSet();
        PointSetView Points_X_View = new PointSetView(Color.White);

        LabelView[] FaceLabels = null;
        LineView[] TriangulatedEdgeLineViews = null;
        LabelView[] TriangulatedEdgeLabels = null;

        private SortedList<IEdgeKey, LineView> EdgeLineViews = new SortedList<IEdgeKey, LineView>();
        private SortedList<IEdgeKey, LabelView> EdgeLineLabelsViews = new SortedList<IEdgeKey, LabelView>();
        private SortedList<IFace, LabelView> TriFaceLabels = new SortedList<IFace, LabelView>();

        //double LineRadius = 1.5;
        Color LineColor = Color.Beige;

        bool ShowMeshFaces = false;

        MeshView<VertexPositionNormalColor> meshView = new MeshView<VertexPositionNormalColor>();

        GridRectangle BoundingBox;

        public GridVector2 TranslationVector
        {
            get;
            set;
        }


        System.Threading.ReaderWriterLockSlim RWLock = new System.Threading.ReaderWriterLockSlim();

        public TriangulationView()
        {

        }

        public void OnTriangulationProgress(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            UpdateTriangulationViews(mesh);
            System.Threading.Thread.Sleep(0);
        }


        private void UpdateTriangulationViews(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            try
            {
                RWLock.EnterWriteLock();

                if (mesh.Verticies.Count == 0)
                {
                    Points_X = new PointSet(); 
                    FaceLabels = new LabelView[mesh.Faces.Count];
                    meshView = new MeshView<VertexPositionNormalColor>();
                    BoundingBox = new GridRectangle();
                    return;
                }

                if ((Points_X.Count != mesh.Verticies.Count) || (Points_X.Points.First() != mesh.Verticies.First().Position))
                {
                    Points_X = new PointSet(mesh.Verticies.Select(v => v.Position + TranslationVector));
                    Points_X_View.PointRadius = 2;
                    Points_X_View.Points = Points_X;
                    Points_X_View.LabelType = PointLabelType.INDEX;
                    Points_X_View.LabelColor = Color.White;

                    GridRectangle rect = Points_X.BoundingBox();
                    rect *= 1.05;
                    BoundingBox = rect; 
                }

                SortedSet<IEdgeKey> edgesToAdd = new SortedSet<IEdgeKey>(mesh.Edges.Keys);
                SortedSet<IEdgeKey> edgesToRemove = new SortedSet<IEdgeKey>(this.EdgeLineViews.Keys);

                edgesToRemove.ExceptWith(edgesToAdd);
                edgesToAdd.ExceptWith(this.EdgeLineViews.Keys);

                foreach(IEdgeKey edge in edgesToRemove)
                {
                    this.EdgeLineViews.Remove(edge);
                    this.EdgeLineLabelsViews.Remove(edge); 
                }

                foreach (IEdgeKey edge in edgesToAdd)
                {
                    GridLineSegment edgeSeg = mesh.ToGridLineSegment(edge).Translate(TranslationVector); 
                    EdgeLineViews[edge] = new LineView(edgeSeg, 1.5, mesh[edge] as ConstrainedEdge != null ? Color.Yellow : Color.LightGray, LineStyle.Standard);
                    EdgeLineLabelsViews[edge] = new LabelView(edge.ToString(), edgeSeg, scaleFontWithScene: true, lineWidth: 2.0, color: Color.Black);
                }

                foreach(IEdgeKey edge in EdgeLineViews.Keys)
                {
                    EdgeLineViews[edge].Color = mesh[edge] as ConstrainedEdge != null ? Color.Yellow : Color.LightGray;
                }
                /*
                var lineViews = new LineView[mesh.Edges.Count];
                var lineLabels = new LabelView[lineViews.Length];
                var sortedLines = new GridLineSegment[lineViews.Length];
                
                var edgeKeys = mesh.Edges.Keys.ToArray();
                for (int i = 0; i < lineViews.Length; i++)
                {
                    IEdgeKey key = edgeKeys[i];
                    sortedLines[i] = mesh.ToGridLineSegment(key).Translate(TranslationVector);
                    lineViews[i] = new LineView(sortedLines[i], 1.5, mesh[key] as ConstrainedEdge != null ? Color.Yellow : Color.LightGray, LineStyle.Standard);
                    lineLabels[i] = new LabelView(key.ToString(), sortedLines[i], scaleFontWithScene: true, lineWidth: 2.0, color: Color.Black);
                }
                */

                SortedSet<IFace> facesToAdd = new SortedSet<IFace>(mesh.Faces);
                SortedSet<IFace> facesToRemove = new SortedSet<IFace>(this.TriFaceLabels.Keys);

                facesToRemove.ExceptWith(facesToAdd);
                facesToAdd.ExceptWith(this.TriFaceLabels.Keys);

                foreach (IFace face in facesToRemove)
                {
                    this.TriFaceLabels.Remove(face); 
                }

                foreach (IFace face in facesToAdd)
                { 
                    this.TriFaceLabels[face] = new LabelView(face.ToString(),
                                                                         new GridTriangle(face.iVerts.Select(iVert => mesh[iVert].Position + TranslationVector).ToArray()).BaryToVector(new GridVector2(1 / 3.0, 1 / 3.0)),
                                                                         mesh.IsClockwise(face) ? Color.Red.SetAlpha(0.75f) : Color.LightBlue.SetAlpha(0.75f),
                                                                         scaleFontWithScene: true,
                                                                         fontSize: 2.0);
                }

                /*
                FaceLabels = new LabelView[mesh.Faces.Count];
                FaceLabels = mesh.Faces.Select((f, i) => FaceLabels[i] = new LabelView(f.ToString(),
                                                                         new GridTriangle(f.iVerts.Select(iVert => mesh[iVert].Position + TranslationVector).ToArray()).BaryToVector(new GridVector2(1 / 3.0, 1 / 3.0)),
                                                                         mesh.IsClockwise(f) ? Color.Red.SetAlpha(0.75f) : Color.LightBlue.SetAlpha(0.75f),
                                                                         scaleFontWithScene: true,
                                                                         fontSize: 2.0)
                                 ).ToArray();
                                 */

                /*foreach (LabelView label in FaceLabels)
                {
                    label.Color = Color.Blue.SetAlpha(0.5f);
                }*/

                TriangulatedEdgeLineViews = EdgeLineViews.Values.ToArray();
                TriangulatedEdgeLabels = EdgeLineLabelsViews.Values.ToArray();
                FaceLabels = TriFaceLabels.Values.ToArray();

                
                meshView = new MeshView<VertexPositionNormalColor>();      
                /*
                MeshModel<VertexPositionNormalColor> model = CreateMeshModel(mesh);
                model.ModelMatrix = Matrix.CreateTranslation(TranslationVector.ToXNAVector3());
                meshView.models.Add(model);
                */
            }
            finally
            {
                RWLock.ExitWriteLock();
            }
        }

        static MeshModel<VertexPositionNormalColor> CreateMeshModel(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            MeshModel<VertexPositionNormalColor> model = new MeshModel<VertexPositionNormalColor>();

            model.Verticies = mesh.Verticies.Select(v => new VertexPositionNormalColor((v.Position).ToXNAVector3(0) , Vector3.UnitZ, ColorExtensions.Random().SetAlpha(0.5f))).ToArray();
            model.Edges = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
        }

        public void Draw(IRenderInfo window, Scene scene, RoundLineCode.RoundLineManager lineManager)
        {
            try
            {
                RWLock.EnterReadLock();
                /*
                DeviceStateManager.SaveDeviceState(window.device);

                DepthStencilState dstate = new DepthStencilState();
                dstate.DepthBufferEnable = true;
                dstate.StencilEnable = true;
                dstate.DepthBufferWriteEnable = true;
                dstate.DepthBufferFunction = CompareFunction.LessEqual;

                window.device.DepthStencilState = dstate;
                */
                if (meshView != null && ShowMeshFaces)
                {
                    meshView.Draw(window.device, scene, Microsoft.Xna.Framework.Graphics.CullMode.None);
                }

                if (TriangulatedEdgeLineViews != null && ShowMeshFaces == false)
                {
                    LineView.Draw(window.device, scene, lineManager, this.TriangulatedEdgeLineViews);
                    LabelView.Draw(window.spriteBatch, window.font, scene, this.TriangulatedEdgeLabels);
                    //CurveLabel.Draw(window.GraphicsDevice, this.scene, window.spriteBatch, window.fontArial, window.curveManager, this.TriangulatedEdgeLabels);
                }

                Points_X_View.Draw(window.device, scene, OverlayStyle.Alpha);

                //DeviceStateManager.RestoreDeviceState(window.device);

                if (FaceLabels != null)
                {
                    LabelView.Draw(window.spriteBatch, window.font, scene, this.FaceLabels);
                }

                
            }
            catch (System.ApplicationException)
            {
                return;
            }
            finally
            {
                RWLock.ExitReadLock();
            }
        }

        public void DrawLabels(IRenderInfo window, Scene scene)
        {
            try
            {
                RWLock.EnterReadLock();

                if (TriangulatedEdgeLineViews != null && ShowMeshFaces == false)
                {
                    LabelView.Draw(window.spriteBatch, window.font, scene, this.TriangulatedEdgeLabels);
                }

                Points_X_View.Draw(window.device, scene, OverlayStyle.Alpha);

                //DeviceStateManager.RestoreDeviceState(window.device);

                if (FaceLabels != null)
                {
                    LabelView.Draw(window.spriteBatch, window.font, scene, this.FaceLabels);
                }


            }
            catch (System.ApplicationException)
            {
                return;
            }
            finally
            {
                RWLock.ExitReadLock();
            }
        }
    }
}
