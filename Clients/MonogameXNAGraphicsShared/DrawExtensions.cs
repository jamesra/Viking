using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Text;
using System.Collections.Generic;

namespace VikingXNA
{
    public static class DrawExtensions
    {
        public static void Draw(this Geometry.Transforms.TriangulationTransform transform, GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            VertexPositionColor[] ctrlVerticies = new VertexPositionColor[transform.MapPoints.Length];
            VertexPositionColor[] mapVerticies = new VertexPositionColor[transform.MapPoints.Length];

            List<int> listIndicies = new List<int>(transform.Edges.Length / 2);
            for (int iStartPoint = 0; iStartPoint < transform.Edges.Length; iStartPoint++)
            {
                List<int> edgeList = transform.Edges[iStartPoint];

                foreach (int iEndPoint in edgeList)
                {
                    //Skip if we would have added it earlier
                    if (iEndPoint < iStartPoint)
                        continue;

                    listIndicies.AddRange(new int[] { iStartPoint, iEndPoint });
                }
            }

            int[] indicies = listIndicies.ToArray();

            for (int i = 0; i < transform.MapPoints.Length; i++)
            {
                GridVector2 CtrlP = transform.MapPoints[i].ControlPoint;
                GridVector2 MapP = transform.MapPoints[i].MappedPoint;
                Vector3 ctrlPosition = new Vector3((Single)CtrlP.X, (Single)CtrlP.Y, 1);
                Vector3 mapPosition = new Vector3((Single)MapP.X, (Single)MapP.Y, 1);

                ctrlVerticies[i] = new VertexPositionColor(ctrlPosition, Microsoft.Xna.Framework.Color.Gold);
                mapVerticies[i] = new VertexPositionColor(mapPosition, Microsoft.Xna.Framework.Color.Red);  
            }

            //PORT XNA 4 
            /*
            //graphicsDevice.RenderState.PointSize = 5.0f;

            VertexDeclaration oldVertexDeclaration = graphicsDevice.VertexDeclaration;

            if (DrawExtensions.VertexPositionColorDeclaration == null)
            {
                DrawExtensions.VertexPositionColorDeclaration = new VertexDeclaration(
                    graphicsDevice,
                    VertexPositionColor.VertexElements
                    );
            }

            graphicsDevice.VertexDeclaration = DrawExtensions.VertexPositionColorDeclaration;
            */
            basicEffect.Texture = null; 
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;

            //PORT XNA 4
            //basicEffect.CommitChanges();

            //PORT XNA 4
            //basicEffect.Begin();

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                //PORT XNA 4
                //pass.Begin();
                pass.Apply(); 

        //        graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, mapVerticies, 0, mapVerticies.Length, indicies, 0, indicies.Length / 2); 
                if(ctrlVerticies != null && ctrlVerticies.Length > 0)
                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, ctrlVerticies, 0, ctrlVerticies.Length, indicies, 0, indicies.Length / 2);

                //PORT XNA 4
                //pass.End();
            }

            //PORT XNA 4
            //basicEffect.End();

//            graphicsDevice.VertexDeclaration = oldVertexDeclaration; 
        }
    }
}
