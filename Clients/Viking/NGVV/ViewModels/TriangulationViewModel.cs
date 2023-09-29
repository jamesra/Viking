using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNAGraphics;

namespace Viking.ViewModels
{
    class TriangulationViewModel : IDisposable
    {
        VertexBuffer vbMappedMesh = null;
        VertexBuffer vbControlMesh = null;
        IndexBuffer ibMesh = null;

        public Color MappedColor = new Color(255, 242, 0);
        public Color ControlColor = new Color(0, 255, 0);

        private readonly MappingGridVector2[] MapPoints;
        private readonly int[] TriangleIndicies;

        public TriangulationViewModel(IControlPointTriangulation Mapping)
        {
            this.MapPoints = Mapping.MapPoints;
            this.TriangleIndicies = Mapping.TriangleIndicies;
        }

        public TriangulationViewModel(MappingGridVector2[] mapPoints, int[] triangleIndicies)
        {
            this.MapPoints = mapPoints;
            this.TriangleIndicies = triangleIndicies;
        }

        private void CreateMesh(GraphicsDevice graphicsDevice)
        {
            VertexPositionColor[] MappedMeshVerticies = new VertexPositionColor[this.MapPoints.Length];
            VertexPositionColor[] ControlMeshVerticies = new VertexPositionColor[this.MapPoints.Length];

            for (int iVert = 0; iVert < MappedMeshVerticies.Length; iVert++)
            {
                GridVector2 MappedVect = this.MapPoints[iVert].MappedPoint;
                GridVector2 ControlVect = this.MapPoints[iVert].ControlPoint;

                MappedMeshVerticies[iVert] = new VertexPositionColor(new Vector3((float)MappedVect.X, (float)MappedVect.Y, (float)0),
                                                               MappedColor);
                ControlMeshVerticies[iVert] = new VertexPositionColor(new Vector3((float)ControlVect.X, (float)ControlVect.Y, (float)0),
                                                              ControlColor);
            }

            List<int> TrianglesAsLines = new List<int>();

            for (int i = 0; i < TriangleIndicies.Length; i += 3)
            {
                TrianglesAsLines.Add(TriangleIndicies[i]);
                TrianglesAsLines.Add(TriangleIndicies[i + 1]);
                TrianglesAsLines.Add(TriangleIndicies[i + 1]);
                TrianglesAsLines.Add(TriangleIndicies[i + 2]);
                TrianglesAsLines.Add(TriangleIndicies[i + 2]);
                TrianglesAsLines.Add(TriangleIndicies[i]);
            }


            vbMappedMesh = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), MappedMeshVerticies.Length, BufferUsage.None);
            vbMappedMesh.SetData<VertexPositionColor>(MappedMeshVerticies);
            vbControlMesh = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), ControlMeshVerticies.Length, BufferUsage.None);
            vbControlMesh.SetData<VertexPositionColor>(ControlMeshVerticies);
            ibMesh = new IndexBuffer(graphicsDevice, typeof(int), TrianglesAsLines.Count, BufferUsage.None);
            ibMesh.SetData<int>(TrianglesAsLines.ToArray());
        }

        public void DrawMesh(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {

            if (vbMappedMesh == null)
            {
                CreateMesh(graphicsDevice);
            }

            if (vbMappedMesh.VertexCount == 0)
                return;

            //PORT XNA 4
            //graphicsDevice.VertexDeclaration = TileViewModel.VertexPositionColorDeclaration;

            basicEffect.Texture = null;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;

            DepthStencilState originalDepthState = graphicsDevice.DepthStencilState;

            DepthStencilState newDepthState = new DepthStencilState
            {
                DepthBufferEnable = false,
                StencilEnable = false
            };
            graphicsDevice.DepthStencilState = newDepthState;

            graphicsDevice.SetVertexBuffer(vbMappedMesh);
            graphicsDevice.Indices = ibMesh;
            //PORT XNA 4
            //basicEffect.CommitChanges();

            //PORT XNA 4
            //basicEffect.Begin();

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                //PORT XNA 4
                //pass.Begin();
                pass.Apply();

                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, vbMappedMesh.VertexCount, 0, ibMesh.IndexCount / 2);

            }

            graphicsDevice.SetVertexBuffer(vbControlMesh);

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                //PORT XNA 4
                //pass.Begin();
                pass.Apply();

                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, vbControlMesh.VertexCount, 0, ibMesh.IndexCount / 2);
            }

            graphicsDevice.DepthStencilState = originalDepthState;

        } 

        LabelView[] _Labels = null;

        LabelView[] Labels
        {
            get
            {
                if (_Labels == null)
                {
                    _Labels = CreateLabels(this.MapPoints);
                }

                return _Labels;
            }
        }

        public static LabelView[] CreateLabels(MappingGridVector2[] map_points)
        {
            LabelView[] labels = new LabelView[(map_points.Length + 1) * 2];

            for (int i = 0; i < map_points.Length; i++)
            {
                LabelView control_label = new LabelView(i.ToString(), map_points[i].ControlPoint, anchor: Anchor.TopCenter, scaleFontWithScene: true);
                LabelView mapped_label = new LabelView(i.ToString(), map_points[i].MappedPoint, anchor: Anchor.BottomCenter, scaleFontWithScene: true);

                labels[i * 2] = control_label;
                labels[(i * 2) + 1] = mapped_label;
            }

            if (!labels.Any())
                return labels;

            var lineHeight = labels[0].font.LineSpacing;
             
            labels[map_points.Length] = new LabelView("Control Points", new GridVector2(15, 15), anchor: Anchor.CenterLeft, scaleFontWithScene: false); ;
            labels[map_points.Length + 1] = new LabelView("Mapped Points", new GridVector2(15, 15 + (lineHeight * 2.15)), anchor: Anchor.CenterLeft, scaleFontWithScene: false); ;

            return labels;
        }

        public void DrawLabels(Viking.UI.Controls.SectionViewerControl _Parent)
        {

            LabelView.Draw(_Parent.spriteBatch, VikingXNAGraphics.Global.DefaultFont, _Parent.Scene, this.Labels);
        }



        #region IDisposable Members

        public void Dispose()
        {
            lock (this)
            {

                if (vbMappedMesh != null)
                {
                    vbMappedMesh.Dispose();
                    vbMappedMesh = null;
                }

                if (vbControlMesh != null)
                {
                    vbControlMesh.Dispose();
                    vbControlMesh = null;
                }

                if (ibMesh != null)
                {
                    ibMesh.Dispose();
                    ibMesh = null;
                }

            }
        }

        #endregion
    }
}
