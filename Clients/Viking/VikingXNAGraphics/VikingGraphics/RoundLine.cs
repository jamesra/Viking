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
                new VertexElement(0, 0, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Position, 0),
                new VertexElement(0, 12, VertexElementFormat.Vector2, VertexElementMethod.Default, VertexElementUsage.Normal, 0),
                new VertexElement(0, 20, VertexElementFormat.Vector2, VertexElementMethod.Default, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(0, 28, VertexElementFormat.Single, VertexElementMethod.Default, VertexElementUsage.TextureCoordinate, 1),
            };
    }



    /// <summary>
    /// Class to handle drawing a list of RoundLines.
    /// </summary>
    public class RoundLineManager
    {
        private GraphicsDevice device;
        private Effect effect;
        private EffectParameter viewProjMatrixParameter;
        private EffectParameter instanceDataParameter;
        private EffectParameter timeParameter;
        private EffectParameter lineRadiusParameter;
        private EffectParameter lineColorParameter;
        private EffectParameter blurThresholdParameter;
        private VertexBuffer vb;
        private IndexBuffer ib;
        private VertexDeclaration vdecl;
        private int numInstances;
        private int numVertices;
        private int numIndices;
        private int numPrimitivesPerInstance;
        private int numPrimitives;
        private int bytesPerVertex;
        float[] translationData;

        public int NumLinesDrawn;
        public float BlurThreshold = 0.97f;


        public void Init(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("RoundLine");
            viewProjMatrixParameter = effect.Parameters["viewProj"];
            instanceDataParameter = effect.Parameters["instanceData"];
            timeParameter = effect.Parameters["time"];
            lineRadiusParameter = effect.Parameters["lineRadius"];
            lineColorParameter = effect.Parameters["lineColor"];
            blurThresholdParameter = effect.Parameters["blurThreshold"];

            CreateRoundLineMesh();
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
        private void CreateRoundLineMesh()
        {
            const int primsPerCap = 12; // A higher primsPerCap produces rounder endcaps at the cost of more vertices
            const int verticesPerCap = primsPerCap * 2 + 2;
            const int primsPerCore = 4;
            const int verticesPerCore = 8;

            numInstances = 200;
            numVertices = (verticesPerCore + verticesPerCap + verticesPerCap) * numInstances;
            numPrimitivesPerInstance = primsPerCore + primsPerCap + primsPerCap;
            numPrimitives = numPrimitivesPerInstance * numInstances;
            numIndices = 3 * numPrimitives;
            short[] indices = new short[numIndices];
            bytesPerVertex = RoundLineVertex.SizeInBytes;
            RoundLineVertex[] tri = new RoundLineVertex[numVertices];
            translationData = new float[numInstances * 4]; // Used in Draw()

            int iv = 0;
            int ii = 0;
            int iVertex;
            int iIndex;
            for (int instance = 0; instance < numInstances; instance++)
            {
                // core vertices
                const float pi2 = MathHelper.PiOver2;
                const float threePi2 = 3 * pi2;
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

                // left halfdisc
                iVertex = iv;
                iIndex = ii;
                for (int i = 0; i < primsPerCap + 1; i++)
                {
                    float deltaTheta = MathHelper.Pi / primsPerCap;
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
                    float deltaTheta = MathHelper.Pi / primsPerCap;
                    float theta0 = 3 * MathHelper.PiOver2 + i * deltaTheta;
                    float theta1 = theta0 + deltaTheta / 2;
                    float theta2 = theta0 + deltaTheta;
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

            vb = new VertexBuffer(device, numVertices * bytesPerVertex, BufferUsage.None);
            vb.SetData<RoundLineVertex>(tri);
            vdecl = new VertexDeclaration(device, RoundLineVertex.VertexElements);

            ib = new IndexBuffer(device, numIndices * 2, BufferUsage.None, IndexElementSize.SixteenBits);
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
            VertexDeclaration oldVertexDeclaration = device.VertexDeclaration;
            device.VertexDeclaration = vdecl;
            device.Vertices[0].SetSource(vb, 0, bytesPerVertex);
            device.Indices = ib;

            viewProjMatrixParameter.SetValue(viewProjMatrix);
            timeParameter.SetValue(time);
            lineColorParameter.SetValue(lineColor.ToVector4());
            lineRadiusParameter.SetValue(lineRadius);
            blurThresholdParameter.SetValue(BlurThreshold);

            int iData = 0;
            translationData[iData++] = roundLine.P0.X;
            translationData[iData++] = roundLine.P0.Y;
            translationData[iData++] = roundLine.Rho;
            translationData[iData++] = roundLine.Theta;
            instanceDataParameter.SetValue(translationData);

            if (techniqueName == null)
                effect.CurrentTechnique = effect.Techniques["AlphaGradient"];
            else
                effect.CurrentTechnique = effect.Techniques[techniqueName];
            effect.Begin();
            EffectPass pass = effect.CurrentTechnique.Passes[0];

            pass.Begin();

            int numInstancesThisDraw = 1;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numInstancesThisDraw);
            NumLinesDrawn += numInstancesThisDraw;
            
            pass.End();

            effect.End();

            device.VertexDeclaration = oldVertexDeclaration;
            device.RenderState.CullMode = CullMode.None;
        }


        /// <summary>
        /// Draw a list of Lines.
        /// </summary>
        public void Draw(List<RoundLine> roundLines, float lineRadius, Color lineColor, Matrix viewProjMatrix, 
            float time, string techniqueName)
        {
            VertexDeclaration oldVertexDeclaration = device.VertexDeclaration;
            device.VertexDeclaration = vdecl;
            device.Vertices[0].SetSource(vb, 0, bytesPerVertex);
            device.Indices = ib;

            viewProjMatrixParameter.SetValue(viewProjMatrix);
            timeParameter.SetValue(time);
            lineColorParameter.SetValue(lineColor.ToVector4());
            lineRadiusParameter.SetValue(lineRadius);
            blurThresholdParameter.SetValue(BlurThreshold);
            
            if (techniqueName == null)
                effect.CurrentTechnique = effect.Techniques["AlphaGradient"];
            else
                effect.CurrentTechnique = effect.Techniques[techniqueName]; 
            effect.Begin();
            EffectPass pass = effect.CurrentTechnique.Passes[0];
            
            pass.Begin();
            
            int iData = 0;
            int numInstancesThisDraw = 0;
            foreach (RoundLine roundLine in roundLines)
            {
                translationData[iData++] = roundLine.P0.X;
                translationData[iData++] = roundLine.P0.Y;
                translationData[iData++] = roundLine.Rho;
                translationData[iData++] = roundLine.Theta;
                numInstancesThisDraw++;

                if (numInstancesThisDraw == numInstances)
                {
                    instanceDataParameter.SetValue(translationData);
                    effect.CommitChanges();
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numInstancesThisDraw);
                    NumLinesDrawn += numInstancesThisDraw;
                    numInstancesThisDraw = 0;
                    iData = 0;
                }
            }
            if (numInstancesThisDraw > 0)
            {
                instanceDataParameter.SetValue(translationData);
                effect.CommitChanges();
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numInstancesThisDraw);
                NumLinesDrawn += numInstancesThisDraw;
            }
            pass.End();
            
            effect.End();
            
            device.VertexDeclaration = oldVertexDeclaration;
            device.RenderState.CullMode = CullMode.None; 
        }
    }
}
