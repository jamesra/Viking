using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic; 
using System.Text;
using System.Linq;

namespace VikingXNAGraphics
{
    public static class Global
    {
        public const uint NumCurveInterpolationsDefault = 5;

        public static Microsoft.Xna.Framework.Content.ContentManager Content { get; set; }

        private static Microsoft.Xna.Framework.Graphics.SpriteFont _DefaultFont = null;
        /// <summary>
        /// Users of this library must set te
        /// </summary>
        public static Microsoft.Xna.Framework.Graphics.SpriteFont DefaultFont
        {
            get
            {
                if (_DefaultFont == null)
                {
                    _DefaultFont = Content.Load<SpriteFont>(@"Arial");
                }

                return _DefaultFont;
            }
            set { _DefaultFont = value; }
        }
    }

    /// <summary>
    /// This lists the built-in textures we have embedded in circles
    /// </summary>
    public enum BuiltinTexture
    {
        /// <summary>
        /// A solid color icon with no texture.
        /// </summary>
        None,
        /// <summary>
        /// An outline of a circle
        /// </summary>
        Circle,
        Plus,
        Minus,
        UpArrow,
        DownArrow,
        Chain,
        /// <summary>
        /// A half circle surrounding a circle.  Used for Structure Links
        /// </summary>
        Connect,
        /// <summary>
        /// A large X, typically indicates a cancel action
        /// </summary>
        X
    }

    public interface IIconTexture
    {
        BuiltinTexture Icon { get; }
    }

    public static class GlobalPrimitives
    {
        public static Texture2D CircleTexture;
        public static Texture2D PlusTexture;
        public static Texture2D MinusTexture;
        public static Texture2D UpArrowTexture;
        public static Texture2D DownArrowTexture;
        public static Texture2D ChainTexture;
        public static Texture2D ConnectTexture;
        public static Texture2D CircleXTexture;

        public static Texture2D GetTexture(this BuiltinTexture tex)
        {
            switch(tex)
            {
                case BuiltinTexture.None:
                    return null;
                case BuiltinTexture.Circle:
                    return CircleTexture;
                case BuiltinTexture.Plus:
                    return PlusTexture;
                case BuiltinTexture.Minus:
                    return MinusTexture;
                case BuiltinTexture.UpArrow:
                    return UpArrowTexture;
                case BuiltinTexture.DownArrow:
                    return DownArrowTexture;
                case BuiltinTexture.Chain:
                    return ChainTexture;
                case BuiltinTexture.Connect:
                    return ConnectTexture;
                case BuiltinTexture.X:
                    return CircleXTexture;
                default:
                    throw new NotImplementedException(string.Format("Missing texture for CircleIcon enumeration value {0}", tex));
            }
        }

        public static readonly VertexPositionColorTexture[] SquareVerts = new VertexPositionColorTexture[] {
            new VertexPositionColorTexture(new Vector3(-1,1,0), Color.White, Vector2.Zero), 
            new VertexPositionColorTexture(new Vector3(1,1,0), Color.White, Vector2.UnitX), 
            new VertexPositionColorTexture(new Vector3(-1,-1,0), Color.White, Vector2.UnitY), 
            new VertexPositionColorTexture(new Vector3(1,-1,0), Color.White, Vector2.One) };

        public static readonly int[] SquareIndicies = new int[] { 2, 1, 0, 3, 1, 2 };

        /// <summary>
        /// Stores a unit circle/square index buffer for each device we know about.
        /// </summary>
        private static readonly Dictionary<GraphicsDevice, IndexBuffer> unit_circle_index_buffers = new Dictionary<GraphicsDevice, IndexBuffer>();

        /// <summary>
        /// Gets the index buffer for unit square built with two triangles
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static IndexBuffer GetUnitSquareIndexBuffer(GraphicsDevice device)
        {
            return GetUnitCircleIndexBuffer(device);
        }

        /// <summary>
        /// Gets the index buffer for unit circle built with two triangles
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static IndexBuffer GetUnitCircleIndexBuffer(GraphicsDevice device)
        {
            IndexBuffer ib = null;

            if (unit_circle_index_buffers.ContainsKey(device))
            {
                ib = unit_circle_index_buffers[device];
                if (ib.IsDisposed)
                {
                    unit_circle_index_buffers.Remove(device);
                    ib = CreateUnitCircleIndexBuffer(device);
                    unit_circle_index_buffers[device] = ib;
                }

                return ib;
            }

            ib = CreateUnitCircleIndexBuffer(device);
            unit_circle_index_buffers[device] = ib;
            return ib;
        }
         

        public static IndexBuffer CreateUnitCircleIndexBuffer(GraphicsDevice device)
        {
            IndexBuffer ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, SquareIndicies.Length, BufferUsage.WriteOnly);
            ib.SetData<int>(SquareIndicies);
            return ib; 
        }


        /// <summary>
        /// Stores a unit circle vertex buffer for each device we know about
        /// </summary>
        private static readonly Dictionary<GraphicsDevice, VertexBuffer> unit_circle_vertex_buffers = new Dictionary<GraphicsDevice, VertexBuffer>();


        // <summary>
        /// Gets vertxe buffer of four vertcies with corners at -1,1.  
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static VertexBuffer GetUnitSquareVertexBuffer(GraphicsDevice device)
        {
            return GetUnitCircleVertexBuffer(device);
        }

        // <summary>
        /// Gets vertxe buffer of four vertcies with corners at -1,1.  Pixel shader should clip pixels outside the unit circle.
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static VertexBuffer GetUnitCircleVertexBuffer(GraphicsDevice device)
        {
            VertexBuffer vb = null; 

            if (unit_circle_vertex_buffers.ContainsKey(device))
            {
                vb = unit_circle_vertex_buffers[device];
                if (vb.IsDisposed)
                {
                    unit_circle_vertex_buffers.Remove(device);
                    vb = CreateUnitCircleVertexBuffer(device);
                    unit_circle_vertex_buffers[device] = vb;
                }

                return vb;
            }

            vb = CreateUnitCircleVertexBuffer(device);
            unit_circle_vertex_buffers[device] = vb;
            return vb;
        }
          
        private static VertexBuffer CreateUnitCircleVertexBuffer(GraphicsDevice device)
        {
            return CreateUnitSquareVertexBuffer(device);
        }
         
        private static VertexBuffer CreateUnitSquareVertexBuffer(GraphicsDevice device)
        {
            var vb = new VertexBuffer(device, typeof(VertexPositionColorTexture), SquareVerts.Length, BufferUsage.WriteOnly);
            vb.SetData<VertexPositionColorTexture>(SquareVerts);
            return vb;
        }

        //        static public VertexDeclaration VertexPositionColorTextureDecl = null;

        static VertexPositionColor[] _UpTriVerts = null;
        public static VertexPositionColor[] UpTriVerts
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
        public static VertexPositionColor[] DownTriVerts
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

        public static int[] CircleVertIndicies = null;
        public static int[] CircleBorderIndicies = null;

        static VertexPositionColor[] _CircleVerts = null;
        public static VertexPositionColor[] CircleVerts
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

        private static VertexPositionColorTexture[] CircleVerticies(GridVector2 Pos, float Radius, Microsoft.Xna.Framework.Color color)
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


        public static void DrawCircle(GraphicsDevice graphicsDevice,
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

            BlendState originalState = graphicsDevice.BlendState;
            graphicsDevice.BlendState = BlendState.NonPremultiplied;

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

            graphicsDevice.BlendState = originalState; 
        }
         

        public static void DrawPolyline(RoundLineCode.RoundLineManager LineManager,
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
            LineManager.Draw(drawn_lines, (float)LineWidth /2.0f, color, basicEffect.View * basicEffect.Projection, 0, "Standard");
        }


        public static void DrawPoints(RoundLineCode.RoundLineManager LineManager,
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
            LineManager.Draw(points, (float)Radius, color, basicEffect.View * basicEffect.Projection, 0, "Standard");
        }
         
        public static void AppendVertLists(IEnumerable<VertexPositionColorTexture> sourceList, List<VertexPositionColorTexture> targetList, IEnumerable<int> indicies, ref List<int> listIndicies)
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
