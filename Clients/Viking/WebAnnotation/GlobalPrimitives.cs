using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework; 
using Microsoft.Xna.Framework.Graphics;
using Geometry; 

namespace WebAnnotation
{
    static class GlobalPrimitives
    {
        static public Texture2D CircleTexture;
        static public Texture2D UpArrowTexture;
        static public Texture2D DownArrowTexture;

        static readonly public VertexPositionColorTexture[] SquareVerts = new VertexPositionColorTexture[] {
            new VertexPositionColorTexture(new Vector3(-1,1,0), Color.White, Vector2.Zero), 
            new VertexPositionColorTexture(new Vector3(1,1,0), Color.White, Vector2.UnitX), 
            new VertexPositionColorTexture(new Vector3(-1,-1,0), Color.White, Vector2.UnitY), 
            new VertexPositionColorTexture(new Vector3(1,-1,0), Color.White, Vector2.One) };

        static readonly public int[] SquareIndicies = new int[] { 2, 1, 0, 3, 1, 2 };

//        static public VertexDeclaration VertexPositionColorTextureDecl = null;

        static VertexPositionColor[] _UpTriVerts = null;
        static public VertexPositionColor[] UpTriVerts
        {
            get
            {
                if (_UpTriVerts != null)
                    return _UpTriVerts;

                float TwoPi = (float)(Math.PI * 2);
                _UpTriVerts = new VertexPositionColor[3];
                for (int i = 0; i < _UpTriVerts.Length; i++)
                {
                    float theta = TwoPi * ((float)i / (float)_UpTriVerts.Length);
                    theta += (float)Math.PI / 2;
                    float x1 = (float)Math.Cos(theta);
                    float y1 = (float)Math.Sin(theta);

                    _UpTriVerts[i] = new VertexPositionColor(new Vector3(x1, y1, 0),
                                                                Microsoft.Xna.Framework.Color.White);
                }

                return _UpTriVerts;
            }
        }

        static VertexPositionColor[] _DownTriVerts = null;
        static public VertexPositionColor[] DownTriVerts
        {
            get
            {
                if (_DownTriVerts != null)
                    return _DownTriVerts;

                float TwoPi = (float)(Math.PI * 2);
                float OneSixthPi = TwoPi / 6;
                _DownTriVerts = new VertexPositionColor[3];
                for (int i = 0; i < _DownTriVerts.Length; i++)
                {
                    float theta = (TwoPi * ((float)i / (float)_DownTriVerts.Length)) + OneSixthPi;
                    theta += (float)Math.PI / 2;
                    float x1 = (float)Math.Cos(theta);
                    float y1 = (float)Math.Sin(theta);

                    _DownTriVerts[i] = new VertexPositionColor(new Vector3(x1, y1, 0),
                                                                Microsoft.Xna.Framework.Color.White);
                }

                return _DownTriVerts;
            }
        }

        static public int[] CircleVertIndicies = null;
        static public int[] CircleBorderIndicies = null;

        static VertexPositionColor[] _CircleVerts = null;
        static public VertexPositionColor[] CircleVerts
        {
            get
            {
                if (_CircleVerts != null)
                    return _CircleVerts;

                //Create a circle of verticies
                int numPts = 64;

                CircleVertIndicies = new int[numPts + 2];
                CircleBorderIndicies = new int[numPts + 1];
                _CircleVerts = new VertexPositionColor[numPts + 1];
                //The first point is the center
                _CircleVerts[0] = new VertexPositionColor(new Vector3(0, 0, 0),
                                                          Microsoft.Xna.Framework.Color.White);
                CircleVertIndicies[0] = 0;
                float TwoPi = (float)(Math.PI * 2);
                for (int i = 1; i < numPts + 1; i++)
                {
                    float fraction = (float)(i - 1) / (float)numPts;
                    float theta = TwoPi - (TwoPi * (float)fraction); //Need to make triangles present proper face
                    float x1 = (float)Math.Cos(theta);
                    float y1 = (float)Math.Sin(theta);

                    _CircleVerts[i] = new VertexPositionColor(new Vector3(x1, y1, 0),
                                                                Microsoft.Xna.Framework.Color.White);
                    CircleVertIndicies[i] = i;
                    CircleBorderIndicies[i - 1] = i;
                }
                CircleVertIndicies[numPts + 1] = 1; //Complete the circle
                CircleBorderIndicies[numPts] = 1; //Complete the circle border

                return _CircleVerts;
            }
        }

        static private VertexPositionColorTexture[] CircleVerticies(GridVector2 Pos, float Radius, Microsoft.Xna.Framework.Color color)
        {
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[GlobalPrimitives.SquareVerts.Length];
            GlobalPrimitives.SquareVerts.CopyTo(verts, 0); 

            //Scale and color the verticies
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Position.X *= Radius;
                verts[i].Position.Y *= Radius;

                verts[i].Position.X += (float)Pos.X;
                verts[i].Position.Y += (float)Pos.Y;
                verts[i].Color = color;
            }

            return verts;
        }


        static public void DrawCircle(GraphicsDevice graphicsDevice,
                BasicEffect basicEffect, 
                GridVector2 Pos,
                double Radius,
                Microsoft.Xna.Framework.Color color)
        {
            //A better way to implement this is to just render a circle texture and add color using lighting, but 
            //this will work for now
            VertexPositionColorTexture[] verts;

            //Can't populate until we've referenced CircleVerts
            int[] indicies;
            float radius = (float)Radius;

            //Figure out if we should draw triangles instead
            verts = CircleVerticies(Pos, (float)Radius, color);
            indicies = GlobalPrimitives.SquareIndicies;

            basicEffect.Texture = GlobalPrimitives.CircleTexture;
            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;

             foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
             {
                 pass.Apply();

                 graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                               verts,
                                                                               0,
                                                                               verts.Length,
                                                                               indicies,
                                                                               0,
                                                                               2);
             }

             basicEffect.TextureEnabled = false;
             basicEffect.VertexColorEnabled = false;
        }
         

        static public void DrawPolyline(RoundLineCode.RoundLineManager LineManager,
                                   BasicEffect basicEffect,
                                   IList<GridVector2> LineVerticies,
                                   double LineWidth,
                                   Microsoft.Xna.Framework.Color color)
        {
            RoundLineCode.RoundLine[] drawn_lines = new RoundLineCode.RoundLine[LineVerticies.Count - 1];
            GridVector2[] verts = LineVerticies.ToArray();
            for (int i = 0; i < LineVerticies.Count - 1; i++)
            {
                drawn_lines[i] = new RoundLineCode.RoundLine(new Microsoft.Xna.Framework.Vector2((float)verts[i].X, (float)verts[i].Y),
                                                             new Microsoft.Xna.Framework.Vector2((float)verts[i + 1].X, (float)verts[i + 1].Y));
            }
            LineManager.Draw(drawn_lines, (float)LineWidth, color, basicEffect.View * basicEffect.Projection, 0, null);
        }


        static public void DrawPoints(RoundLineCode.RoundLineManager LineManager,
                                   BasicEffect basicEffect,
                                   IList<GridVector2> Verticies,
                                   double Radius,
                                   Microsoft.Xna.Framework.Color color)
        {
            RoundLineCode.Disc[] points = new RoundLineCode.Disc[Verticies.Count - 1];

            GridVector2[] verts = Verticies.ToArray();
            for (int i = 0; i < Verticies.Count - 1; i++)
            {
                points[i] = new RoundLineCode.Disc((float)verts[i].X, (float)verts[i].Y);
            }
            LineManager.Draw(points, (float)Radius, color, basicEffect.View * basicEffect.Projection, 0, null);
        }
         
        static public void AppendVertLists(IEnumerable<VertexPositionColorTexture> sourceList, List<VertexPositionColorTexture> targetList, IEnumerable<int> indicies, ref List<int> listIndicies)
        {
            int iStartVert = targetList.Count;
            targetList.AddRange(sourceList);

            foreach (int i in indicies)
            {
                listIndicies.Add(i + iStartVert);
            }
        }
    }
}
