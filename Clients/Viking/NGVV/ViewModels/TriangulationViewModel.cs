using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using Viking.UI;
using Viking.ViewModels;
using Geometry;
using Viking.VolumeModel; 
using Geometry.Transforms; 

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

        public TriangulationViewModel(TriangulationTransform Mapping)
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

            for (int i = 0; i < TriangleIndicies.Length; i+=3)
            {
                TrianglesAsLines.Add(TriangleIndicies[i]);
                TrianglesAsLines.Add(TriangleIndicies[i+1]);
                TrianglesAsLines.Add(TriangleIndicies[i+1]);
                TrianglesAsLines.Add(TriangleIndicies[i+2]);
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

        }

        static SpriteFont _font;
        static SpriteBatch _spriteBatch;



        public void DrawLabels(Viking.UI.Controls.SectionViewerControl _Parent)
        {  
            float Scale = (float)(1.0f / _Parent.StatusMagnification) * 10;

            _Parent.spriteBatch.Begin();
              
            for (int i = 0; i < MapPoints.Length; i++ )
            {
                GridVector2 ControlPositionScreen = _Parent.WorldToScreen(this.MapPoints[i].ControlPoint.X, this.MapPoints[i].ControlPoint.Y);
                GridVector2 MappedPositionScreen = _Parent.WorldToScreen(this.MapPoints[i].MappedPoint.X, this.MapPoints[i].MappedPoint.Y);

                Vector2 Offset = _Parent.GetLabelSize(_Parent.fontArial, i.ToString());
                Offset.X /= 2f;
                Offset.Y /= 2f;

                _Parent.spriteBatch.DrawString(_Parent.fontArial,
                                        i.ToString(),
                                        new Vector2((float)ControlPositionScreen.X, (float)ControlPositionScreen.Y),
                                        ControlColor,
                                        0,
                                        Offset,
                                        Scale,
                                        SpriteEffects.None,
                                        0);

                _Parent.spriteBatch.DrawString(_Parent.fontArial,
                                       i.ToString(),
                                       new Vector2((float)MappedPositionScreen.X, (float)MappedPositionScreen.Y),
                                       MappedColor,
                                       0,
                                       Offset,
                                       Scale,
                                       SpriteEffects.None,
                                       0);
            }

            _Parent.spriteBatch.DrawString(_Parent.fontArial,
                                        "Control Points",
                                        new Vector2((float)15, (float)15),
                                        ControlColor,
                                        0,
                                        new Vector2(),
                                        .2f,
                                        SpriteEffects.None,
                                        0);

            Vector2 LegendSize = _Parent.GetLabelSize(_Parent.fontArial,  "Control Points");

            _Parent.spriteBatch.DrawString(_Parent.fontArial,
                                   "Mapped Points",
                                   new Vector2((float)15, (float) 3 + 15 + LegendSize.Y),
                                   MappedColor,
                                   0,
                                   new Vector2(),
                                   .2f,
                                   SpriteEffects.None,
                                   0);

            _Parent.spriteBatch.End();
             
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
