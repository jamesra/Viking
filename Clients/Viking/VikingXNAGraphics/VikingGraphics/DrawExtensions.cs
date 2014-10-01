using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Utilities;

namespace VikingXNA
{
    public static class DrawExtensions
    {
        static VertexDeclaration VertexPositionColorDeclaration = null; 

        public static void Draw(this Utilities.GridTransform transform, GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            VertexPositionColor[] ctrlVerticies = new VertexPositionColor[transform.mapPoints.Length];
            VertexPositionColor[] mapVerticies = new VertexPositionColor[transform.mapPoints.Length];

            List<int> listIndicies = new List<int>(transform.Edges.Count / 2);
            foreach(int iStartPoint in transform.Edges.Keys)
            {
                List<int> edgeList = transform.Edges[iStartPoint];

                foreach (int iEndPoint in edgeList)
                {
                    //Skip if we would have added it earlier
                    if (iEndPoint < iStartPoint)
                        continue;

                    listIndicies.AddRange(new int[] { iStartPoint, iEndPoint});
                }
            }

            int[] indicies = listIndicies.ToArray();

            for (int i = 0; i < transform.mapPoints.Length; i++)
            {
                GridVector2 CtrlP = transform.mapPoints[i].ControlPoint;
                GridVector2 MapP = transform.mapPoints[i].MappedPoint;
                Vector3 ctrlPosition = new Vector3((Single)CtrlP.X, (Single)CtrlP.Y, 1);
                Vector3 mapPosition = new Vector3((Single)MapP.X, (Single)MapP.Y, 1);

                ctrlVerticies[i] = new VertexPositionColor(ctrlPosition, Microsoft.Xna.Framework.Graphics.Color.Gold);
                mapVerticies[i] = new VertexPositionColor(mapPosition, Microsoft.Xna.Framework.Graphics.Color.Red);  
            }

            graphicsDevice.RenderState.PointSize = 5.0f;

            VertexDeclaration oldVertexDeclaration = graphicsDevice.VertexDeclaration;

            if (DrawExtensions.VertexPositionColorDeclaration == null)
            {
                DrawExtensions.VertexPositionColorDeclaration = new VertexDeclaration(
                    graphicsDevice,
                    VertexPositionColor.VertexElements
                    );
            }

            graphicsDevice.VertexDeclaration = DrawExtensions.VertexPositionColorDeclaration;

            basicEffect.Texture = null; 
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.CommitChanges();


            basicEffect.Begin();

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Begin();

        //        graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, mapVerticies, 0, mapVerticies.Length, indicies, 0, indicies.Length / 2); 
                if(ctrlVerticies != null && ctrlVerticies.Length > 0)
                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, ctrlVerticies, 0, ctrlVerticies.Length, indicies, 0, indicies.Length / 2);
                
                pass.End();
            }

            basicEffect.End();

            graphicsDevice.VertexDeclaration = oldVertexDeclaration; 
        }
    }
}
