// RoundLine.cs
// By Michael D. Anderson
// Version 3.00, Mar 12 2009
//
// A class to efficiently draw thick lines with rounded ends.

#region Using Statements
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System.Linq;
#endregion


namespace RoundLineCode
{

    /// <summary>
    /// Represents a single line segment.  Drawing is handled by the RoundLineManager class.
    /// </summary>
    public partial class RoundLine
    {
        private Vector2 p0; // Begin point of the line
        private Vector2 p1; // End point of the line
        private float rho; // Length of the line
        private float theta; // Angle of the line

        public Vector2 P0
        {
            get
            {
                return p0;
            }
            set
            {
                p0 = value;
                RecalcRhoTheta();
            }
        }
        public Vector2 P1
        {
            get
            {
                return p1;
            }
            set
            {
                p1 = value;
                RecalcRhoTheta();
            }
        }
        public float Rho { get { return rho; } }
        public float Theta { get { return theta; } }


        public RoundLine(Vector2 p0, Vector2 p1)
        {
            this.p0 = p0;
            this.p1 = p1;
            RecalcRhoTheta();
        }


        public RoundLine(float x0, float y0, float x1, float y1)
        {
            this.p0 = new Vector2(x0, y0);
            this.p1 = new Vector2(x1, y1);
            RecalcRhoTheta();
        }


        protected void RecalcRhoTheta()
        {
            Vector2 delta = P1 - P0;
            rho = delta.Length();
            theta = (float)Math.Atan2(delta.Y, delta.X);
        }

        public override string ToString()
        {
            return string.Format("{0},{1} - {2},{3} rho:{4} theta:{5}", P0.X, P0.Y, P1.X, P1.Y, Rho, Theta);
        }
    };


    // A "degenerate" RoundLine where both endpoints are equal
    public class Disc : RoundLine
    {
        public Disc(Vector2 p) : base(p, p) { }
        public Disc(float x, float y) : base(x, y, x, y) { }
        public Vector2 Pos
        {
            get
            {
                return P0;
            }
            set
            {
                P0 = value;
                P1 = value;
            }
        }
    };


    // A vertex type for drawing RoundLines, including an instance index
    struct RoundLineVertex
    {
        public Vector3 pos;
        public Vector2 rhoTheta;
        public Vector2 scaleTrans;
        public float index;


        public RoundLineVertex(Vector3 pos, Vector2 norm, Vector2 tex, float index)
        {
            this.pos = pos;
            this.rhoTheta = norm;
            this.scaleTrans = tex;
            this.index = index;
        }

        public static int SizeInBytes = 8 * sizeof(float);

        public static VertexElement[] VertexElements = new VertexElement[]
            {
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.Normal, 0),
                new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(28, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1),
            };
    }



    /// <summary>
    /// Class to handle drawing a list of RoundLines.
    /// </summary>
    public class RoundLineManager
    {
        protected GraphicsDevice device;
        protected Effect effect;
        protected EffectParameter viewProjMatrixParameter;
        protected EffectParameter instanceDataParameter;
        protected EffectParameter timeParameter;
        protected EffectParameter lineRadiusParameter;
        protected EffectParameter lineColorParameter;
        protected EffectParameter blurThresholdParameter;
        protected EffectParameter textureParameter;
        protected EffectParameter minTextureCoordinateParameter; //The texture coordinate at the start of the curve
        protected EffectParameter maxTextureCoordinateParameter; //The texture coordinate at the end of the curve
        protected VertexBuffer vb;
        protected IndexBuffer ib;
        protected VertexDeclaration vdecl;
        protected int MaxInstancesPerBatch = 200;
        protected int numVertices;
        protected int numIndices;
        protected int numPrimitivesPerInstance;
        protected int numPrimitives;
        protected int bytesPerVertex;
        protected float[] translationData;

        //public int NumLinesDrawn;
        public float DefaultBlurThreshold = 0.97f;


        public virtual void Init(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("RoundLine");
            LoadParameters(effect);
            CreateRoundLineMesh();
        }

        /// <summary>
        /// Initialize to use HSV overlay onto a background texture
        /// </summary>
        /// <param name="device"></param>
        /// <param name="content"></param>
        public void InitHSV(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("RoundLineHSV");
            LoadParameters(effect);
            CreateRoundLineMesh();
        }

        protected void LoadParameters(Effect e)
        {
            viewProjMatrixParameter = e.Parameters["viewProj"];
            instanceDataParameter = e.Parameters["instanceData"];
            timeParameter = e.Parameters["time"];
            lineRadiusParameter = e.Parameters["lineRadius"];
            lineColorParameter = e.Parameters["lineColor"];
            blurThresholdParameter = e.Parameters["blurThreshold"];
            textureParameter = e.Parameters["Texture"];
            minTextureCoordinateParameter = e.Parameters["texture_x_min"];
            maxTextureCoordinateParameter = e.Parameters["texture_x_max"];
        }

        public string[] TechniqueNames
        {
            get
            {
                string[] names = new string[effect.Techniques.Count];
                int index = 0;
                foreach (EffectTechnique technique in effect.Techniques)
                    names[index++] = technique.Name;
                return names;
            }
        }

        public bool IsTechnique(string name)
        {
            return TechniqueNames.Contains(name);
        }

        




        /// <summary>
        /// Create a mesh for a RoundLine.
        /// </summary>
        /// <remarks>
        /// The RoundLine mesh has 3 sections:
        /// 1.  Two quads, from 0 to 1 (left to right)
        /// 2.  A half-disc, off the left side of the quad
        /// 3.  A half-disc, off the right side of the quad
        ///
        /// The X and Y coordinates of the "normal" encode the rho and theta of each vertex
        /// The "texture" encodes whether to scale and translate the vertex horizontally by length and radius
        /// </remarks>
        protected void CreateRoundLineMesh()
        {
            const int primsPerCap = 9; // A higher primsPerCap produces rounder endcaps at the cost of more vertices
            const int verticesPerCap = primsPerCap * 2 + 2;
            const int primsPerCore = 4;
            const int verticesPerCore = 8;
            const float pi2 = MathHelper.PiOver2;
            const float threePi2 = 3 * pi2;

            numVertices = verticesPerCore * MaxInstancesPerBatch;
            numVertices = (verticesPerCore + verticesPerCap + verticesPerCap) * MaxInstancesPerBatch;
            numPrimitivesPerInstance = primsPerCore + primsPerCap + primsPerCap;
            numPrimitives = numPrimitivesPerInstance * MaxInstancesPerBatch;
            numIndices = 3 * numPrimitives;
            short[] indices = new short[numIndices];
            bytesPerVertex = RoundLineVertex.SizeInBytes;
            RoundLineVertex[] tri = new RoundLineVertex[numVertices];
            translationData = new float[MaxInstancesPerBatch * 4]; // Used in Draw()

            int iv = 0;
            int ii = 0;
            int iVertex;
            int iIndex;
            for (int instance = 0; instance < MaxInstancesPerBatch; instance++)
            {
                // core vertices
                iVertex = iv;
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, -1.0f, 0), new Vector2(1, threePi2), new Vector2(0, 0), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, -1.0f, 0), new Vector2(1, threePi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, threePi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, threePi2), new Vector2(0, 0), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, pi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, pi2), new Vector2(0, 0), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, 1.0f, 0), new Vector2(1, pi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundLineVertex(new Vector3(0.0f, 1.0f, 0), new Vector2(1, pi2), new Vector2(0, 0), instance);

                // core indices
                indices[ii++] = (short)(iVertex + 0);
                indices[ii++] = (short)(iVertex + 1);
                indices[ii++] = (short)(iVertex + 2);
                indices[ii++] = (short)(iVertex + 2);
                indices[ii++] = (short)(iVertex + 3);
                indices[ii++] = (short)(iVertex + 0);

                indices[ii++] = (short)(iVertex + 4);
                indices[ii++] = (short)(iVertex + 6);
                indices[ii++] = (short)(iVertex + 5);
                indices[ii++] = (short)(iVertex + 6);
                indices[ii++] = (short)(iVertex + 7);
                indices[ii++] = (short)(iVertex + 5);

                //Create discs that cap the line
                float deltaTheta = MathHelper.Pi / primsPerCap;

                // left halfdisc
                iVertex = iv;
                iIndex = ii;
                for (int i = 0; i < primsPerCap + 1; i++)
                {
                    float theta0 = MathHelper.PiOver2 + i * deltaTheta;
                    float theta1 = theta0 + deltaTheta / 2;
                    // even-numbered indices are at the center of the halfdisc
                    tri[iVertex + 0] = new RoundLineVertex(new Vector3(0, 0, 0), new Vector2(0, theta1), new Vector2(0, 0), instance);

                    // odd-numbered indices are at the perimeter of the halfdisc
                    float x = (float)Math.Cos(theta0);
                    float y = (float)Math.Sin(theta0);
                    tri[iVertex + 1] = new RoundLineVertex(new Vector3(x, y, 0), new Vector2(1, theta0), new Vector2(1, 0), instance);

                    if (i < primsPerCap)
                    {
                        // indices follow this pattern: (0, 1, 3), (2, 3, 5), (4, 5, 7), ...
                        indices[iIndex + 0] = (short)(iVertex + 0);
                        indices[iIndex + 1] = (short)(iVertex + 1);
                        indices[iIndex + 2] = (short)(iVertex + 3);
                        iIndex += 3;
                        ii += 3;
                    }
                    iVertex += 2;
                    iv += 2;
                }

                // right halfdisc
                for (int i = 0; i < primsPerCap + 1; i++)
                {
                    float theta0 = 3 * MathHelper.PiOver2 + i * deltaTheta;
                    float theta1 = theta0 + deltaTheta / 2;
                    //                    float theta2 = theta0 + deltaTheta;
                    // even-numbered indices are at the center of the halfdisc
                    tri[iVertex + 0] = new RoundLineVertex(new Vector3(0, 0, 0), new Vector2(0, theta1), new Vector2(0, 1), instance);

                    // odd-numbered indices are at the perimeter of the halfdisc
                    float x = (float)Math.Cos(theta0);
                    float y = (float)Math.Sin(theta0);
                    tri[iVertex + 1] = new RoundLineVertex(new Vector3(x, y, 0), new Vector2(1, theta0), new Vector2(1, 1), instance);

                    if (i < primsPerCap)
                    {
                        // indices follow this pattern: (0, 1, 3), (2, 3, 5), (4, 5, 7), ...
                        indices[iIndex + 0] = (short)(iVertex + 0);
                        indices[iIndex + 1] = (short)(iVertex + 1);
                        indices[iIndex + 2] = (short)(iVertex + 3);
                        iIndex += 3;
                        ii += 3;
                    }
                    iVertex += 2;
                    iv += 2;
                }
            }


            vdecl = new VertexDeclaration(RoundLineVertex.VertexElements);
            vb = new VertexBuffer(device, vdecl, numVertices * bytesPerVertex, BufferUsage.None);
            vb.SetData<RoundLineVertex>(tri);

            ib = new IndexBuffer(device, IndexElementSize.SixteenBits, numIndices * 2, BufferUsage.None);
            ib.SetData<short>(indices);
        }



        /// <summary>
        /// Compute a reasonable "BlurThreshold" value to use when drawing RoundLines.
        /// See how wide lines of the specified radius will be (in pixels) when drawn
        /// to the back buffer.  Then apply an empirically-determined mapping to get
        /// a good BlurThreshold for such lines.
        /// </summary>
        public float ComputeBlurThreshold(float lineRadius, Matrix viewProjMatrix, float viewportWidth)
        {
            Vector4 lineRadiusTestBase = new Vector4(0, 0, 0, 1);
            Vector4 lineRadiusTest = new Vector4(lineRadius, 0, 0, 1);
            Vector4 delta = lineRadiusTest - lineRadiusTestBase;
            Vector4 output = Vector4.Transform(delta, viewProjMatrix);
            output.X *= viewportWidth;

            double newBlur = 0.125 * Math.Log(output.X) + 0.4;

            return MathHelper.Clamp((float)newBlur, 0.5f, 0.99f);
        }


        /// <summary>
        /// Draw a single RoundLine.  Usually you want to draw a list of RoundLines
        /// at a time instead for better performance.
        /// </summary>
        public void Draw(RoundLine roundLine, float lineRadius, Color lineColor, Matrix viewProjMatrix,
            float time, string techniqueName)
        {
            device.SetVertexBuffer(vb);
            device.Indices = ib;

            viewProjMatrixParameter.SetValue(viewProjMatrix);
            timeParameter.SetValue(time);

            if (techniqueName == null)
                effect.CurrentTechnique = effect.Techniques["Standard"];
            else
                effect.CurrentTechnique = effect.Techniques[techniqueName];

            DrawOnConfiguredDevice(roundLine, lineRadius, lineColor, DefaultBlurThreshold);
            
            device.SetVertexBuffer(null);
            device.Indices = null;
        }

        public void Draw(RoundLine[] roundLines, float[] lineRadius, Color[] lineColor, Matrix viewProjMatrix,
            float time, string techniqueName)
        {
            device.SetVertexBuffer(vb);
            device.Indices = ib;

            viewProjMatrixParameter.SetValue(viewProjMatrix);
            timeParameter.SetValue(time);

            if (techniqueName == null)
                effect.CurrentTechnique = effect.Techniques["Standard"];
            else
                effect.CurrentTechnique = effect.Techniques[techniqueName];

            for (int i = 0; i < roundLines.Length; i++)
            {
                DrawOnConfiguredDevice(roundLines[i], lineRadius[i], lineColor[i], this.DefaultBlurThreshold);
            }

            device.SetVertexBuffer(null);
            device.Indices = null;
        }

        private void DrawOnConfiguredDevice(RoundLine roundLine, float lineRadius, Color lineColor, double BlurThreshold)
        {
            lineColorParameter.SetValue(lineColor.ToVector4());
            lineRadiusParameter.SetValue(lineRadius);
            blurThresholdParameter.SetValue(DefaultBlurThreshold);

            int iData = 0;
            translationData[iData++] = roundLine.P0.X;
            translationData[iData++] = roundLine.P0.Y;
            translationData[iData++] = roundLine.Rho;
            translationData[iData++] = roundLine.Theta;
            instanceDataParameter.SetValue(translationData);
            
            EffectPass pass = effect.CurrentTechnique.Passes[0];

            pass.Apply();

            int numInstancesThisDraw = 1;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numInstancesThisDraw);
            //NumLinesDrawn += numInstancesThisDraw;
        }


        /// <summary>
        /// Draw a list of Lines in batches, up to the maximum number of instances for the shader.
        /// </summary>
        public void Draw(IEnumerable<RoundLine> roundLines, float lineRadius, Color lineColor, Matrix viewProjMatrix,
            float time, Texture2D texture)
        {
            textureParameter.SetValue(texture);
            Draw(roundLines, lineRadius, lineColor, viewProjMatrix, time, "Textured");
        }

        /// <summary>
        /// Draw a list of Lines in batches, up to the maximum number of instances for the shader.
        /// </summary>
        public void Draw(IEnumerable<RoundLine> roundLines, float lineRadius, Color lineColor, Matrix viewProjMatrix,
            float time, string techniqueName)
        {            
            device.SetVertexBuffer(vb);
            device.Indices = ib;

            viewProjMatrixParameter.SetValue(viewProjMatrix);
            timeParameter.SetValue(time);
            lineColorParameter.SetValue(lineColor.ToVector4());
            lineRadiusParameter.SetValue(lineRadius);
            blurThresholdParameter.SetValue(DefaultBlurThreshold);

            if (techniqueName == null)
                effect.CurrentTechnique = effect.Techniques["Standard"];
            else
                effect.CurrentTechnique = effect.Techniques[techniqueName];
            
            int iData = 0;
            int numInstancesThisDraw = 0;
            foreach (RoundLine roundLine in roundLines)
            {                
                translationData[iData++] = roundLine.P0.X;
                translationData[iData++] = roundLine.P0.Y;
                translationData[iData++] = roundLine.Rho;
                translationData[iData++] = roundLine.Theta;
                numInstancesThisDraw++;

                if (numInstancesThisDraw == MaxInstancesPerBatch)
                {
                    instanceDataParameter.SetValue(translationData);
                    EffectPass pass = effect.CurrentTechnique.Passes[0];
                    pass.Apply();
                    
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numInstancesThisDraw);
                    //NumLinesDrawn += numInstancesThisDraw;
                    numInstancesThisDraw = 0;
                    iData = 0;
                }
            }

            if (numInstancesThisDraw > 0)
            {
                instanceDataParameter.SetValue(translationData);
                EffectPass pass = effect.CurrentTechnique.Passes[0];
                pass.Apply();
                
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numInstancesThisDraw);
                //NumLinesDrawn += numInstancesThisDraw;
            }
            
        }

    }

    public class LumaOverlayRoundLineManager : RoundLineManager
    { 
        private EffectParameter _BackgroundTexture;
        private EffectParameter _RenderTargetSize;

        public Texture LumaTexture
        {
            set
            {
                _BackgroundTexture.SetValue(value);
            }
        }

        public Viewport RenderTargetSize
        {
            set
            {
                _RenderTargetSize.SetValue(new Vector2(value.Width, value.Height));
            }

        }

        public override void Init(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("RoundLineHSV");
            _BackgroundTexture = effect.Parameters["BackgroundTexture"];
            _RenderTargetSize = effect.Parameters["RenderTargetSize"];
            LoadParameters(effect);
            CreateRoundLineMesh();
        }
    }
}
