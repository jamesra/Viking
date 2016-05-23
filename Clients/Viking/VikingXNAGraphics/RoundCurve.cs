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
using Geometry;
#endregion


namespace RoundCurve
{
    public enum HorizontalAlignment
    {
        Center,
        Left,
        Right
    }

    /// <summary>
    /// Represents a single line segment.  Drawing is handled by the RoundLineManager class.
    /// </summary>
    public partial class RoundCurve
    {
        private GridVector2[] _controlPoints;
        private double[] _tangent_thetas; // Length of the line
        private double[] _distance_to_origin; //Distance of each control point to the origin of the line
        private double[] _distance_to_origin_normalized; //Distance of each control point to the origin of the line
        private bool _Closed;

        public GridVector2[] ControlPoints
        {
            get
            {
                return _controlPoints;
            }
            set
            {
                _controlPoints = value;
                RecalcDistanceAndTheta();
            }
        } 

       public bool Closed { get { return _Closed; } }

        public double[] Distance { get { return _distance_to_origin; } }
        public double[] DistanceNormalized { get { return _distance_to_origin_normalized; } }
        public double[] Theta { get { return _tangent_thetas; } }

        public double TotalDistance { get { return _distance_to_origin.Last(); } }


        public RoundCurve(GridVector2[] ControlPoints, bool Closed)
        {
            this._Closed = Closed;
            this.ControlPoints = ControlPoints;
            
        } 

        private static double[] CalcLineDistances(GridVector2[] points)
        {
            double total_distance = 0;
            double[] point_distances = new double[points.Length];
            point_distances[0] = 0;

            for (int i = 1; i < points.Length; i++)
            {
                double step_distance = GridVector2.Distance(points[i], points[i - 1]);
                total_distance += step_distance;
                point_distances[i] = total_distance;
            }
             
            return point_distances;
        }

        private static double[] CalcLineTangents(GridVector2[] points, bool Closed)
        {
            double[] tangents = new double[points.Length];

            int numPoints = points.Length;
            
            for (int i = 1; i < numPoints - 1; i++)
            {
                tangents[i] = GridVector2.Angle(points[i - 1], points[i + 1]);
            }

            if (Closed)
            {
                tangents[0] = GridVector2.Angle(points[numPoints - 2], points[1]);
                tangents[numPoints-1] = GridVector2.Angle(points[numPoints - 2], points[1]);
            }
            else
            {
                tangents[0] = (float)GridVector2.Angle(points[0], points[1]);
                tangents[numPoints - 1] = GridVector2.Angle(points[numPoints - 2], points[numPoints - 1]);
            }
            
            return tangents;
        }

        protected void RecalcDistanceAndTheta()
        {
            this._distance_to_origin = CalcLineDistances(this._controlPoints);
            double TotalDistance = _distance_to_origin.Last();
            this._distance_to_origin_normalized = _distance_to_origin.Select(d => d / TotalDistance).ToArray();
            this._tangent_thetas = CalcLineTangents(this._controlPoints, this._Closed);
        }

        public override string ToString()
        {
            return string.Format("{1} - {2}", _controlPoints[0], _controlPoints.Last());
        }
    };

    // A vertex type for drawing RoundLines, including an instance index
    struct RoundCurveVertex
    {
        public Vector3 pos;
        public Vector2 scaleTrans;
        public float index;

        public RoundCurveVertex(Vector3 pos, Vector2 tex, float index)
        {
            this.pos = pos;
            this.scaleTrans = tex;
            this.index = index;
        }

        public static int SizeInBytes = 6 * sizeof(float);

        public static VertexElement[] VertexElements = new VertexElement[]
            {
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                //new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.Normal, 0),
                new VertexElement(3*sizeof(float), VertexElementFormat.Vector2, VertexElementUsage.Normal, 0),
                new VertexElement(5*sizeof(float), VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0),
            };
    }
    

    /// <summary>
    /// Class to handle drawing a list of RoundLines.
    /// </summary>
    public class CurveManager : VikingXNAGraphics.IInitRoundManager
    {
        protected GraphicsDevice device;
        protected Effect effect;
        protected EffectParameter viewProjMatrixParameter;
        protected EffectParameter segmentDataParamter;
        protected EffectParameter timeParameter;
        protected EffectParameter lineRadiusParameter;
        protected EffectParameter lineTotalLengthParameter; //Total length of the line
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
            effect = content.Load<Effect>("RoundCurve");
            LoadParameters(effect);
            CreateRoundLineMesh();
        }
        
        protected void LoadParameters(Effect e)
        {
            viewProjMatrixParameter = e.Parameters["viewProj"];
            
            timeParameter = e.Parameters["time"];
            lineRadiusParameter = e.Parameters["lineRadius"];
            lineColorParameter = e.Parameters["lineColor"];
            blurThresholdParameter = e.Parameters["blurThreshold"];
            segmentDataParamter = e.Parameters["CurveSegmentData"];
            textureParameter = e.Parameters["Texture"];
            lineTotalLengthParameter = e.Parameters["curveTotalLength"];
            minTextureCoordinateParameter = e.Parameters["texture_x_start"];
            maxTextureCoordinateParameter = e.Parameters["texture_x_end"];
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
            const int verticesPerCore = 3;
            const float pi2 = MathHelper.PiOver2;
            const float threePi2 = 3 * pi2;

            numVertices = verticesPerCore * MaxInstancesPerBatch;
            //numVertices = (verticesPerCore + verticesPerCap + verticesPerCap) * MaxInstancesPerBatch;
            numPrimitivesPerInstance = primsPerCore; // + primsPerCap + primsPerCap;
            numPrimitives = numPrimitivesPerInstance * (MaxInstancesPerBatch-1);
            numIndices = 3 * numPrimitives;
            short[] indices = new short[numIndices];
            bytesPerVertex = RoundCurveVertex.SizeInBytes;
            RoundCurveVertex[] tri = new RoundCurveVertex[numVertices];
            translationData = new float[MaxInstancesPerBatch * 4]; // Used in Draw()

            int iv = 0;
            int ii = 0;
            int iVertex;
            int iIndex;
            for (int instance = 0; instance < MaxInstancesPerBatch; instance++)
            {
                //Each control point has three verticies, one centered on the line, and two more a lineradius distance away from the center on opposite sides.
                iVertex = iv;
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, -1.0f, 0), new Vector2(1, threePi2), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, (float)Math.PI), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 1.0f, 0), new Vector2(1, pi2), instance);

                // core indices
                //Don't add indicies for the last line segment
                if (instance + 1 < MaxInstancesPerBatch)
                {
                    indices[ii++] = (short)(iVertex + 0);
                    indices[ii++] = (short)(iVertex + 3);
                    indices[ii++] = (short)(iVertex + 1);

                    indices[ii++] = (short)(iVertex + 1);
                    indices[ii++] = (short)(iVertex + 3);
                    indices[ii++] = (short)(iVertex + 4);

                    indices[ii++] = (short)(iVertex + 1);
                    indices[ii++] = (short)(iVertex + 4);
                    indices[ii++] = (short)(iVertex + 5);

                    indices[ii++] = (short)(iVertex + 1);
                    indices[ii++] = (short)(iVertex + 5);
                    indices[ii++] = (short)(iVertex + 2);
                }

                /*
                // core vertices
                iVertex = iv;
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, -1.0f, 0), new Vector2(1, threePi2), new Vector2(0, 0), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, -1.0f, 0), new Vector2(1, threePi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, threePi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, threePi2), new Vector2(0, 0), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, pi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 0.0f, 0), new Vector2(0, pi2), new Vector2(0, 0), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 1.0f, 0), new Vector2(1, pi2), new Vector2(0, 1), instance);
                tri[iv++] = new RoundCurveVertex(new Vector3(0.0f, 1.0f, 0), new Vector2(1, pi2), new Vector2(0, 0), instance);

                
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
                */
                /*
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
                */
            }


            vdecl = new VertexDeclaration(RoundCurveVertex.VertexElements);
            vb = new VertexBuffer(device, vdecl, numVertices * bytesPerVertex, BufferUsage.None);
            vb.SetData<RoundCurveVertex>(tri);

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
        public void Draw(RoundCurve roundLine, float lineRadius, Color lineColor, Matrix viewProjMatrix,
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

        public void Draw(RoundCurve[] roundLines, float[] lineRadius, Color[] lineColor, Matrix viewProjMatrix,
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

        private void DrawOnConfiguredDevice(RoundCurve roundLine, float lineRadius, Color lineColor, double BlurThreshold)
        {
            lineColorParameter.SetValue(lineColor.ToVector4());
            lineRadiusParameter.SetValue(lineRadius);
            blurThresholdParameter.SetValue(DefaultBlurThreshold);
            lineTotalLengthParameter.SetValue((float)roundLine.TotalDistance);

           
            int SegmentsAlreadyDrawn = 0;
            int numSegmentsThisDraw = 0;
            int numSegmentsToDraw = roundLine.ControlPoints.Count();

            while (SegmentsAlreadyDrawn < numSegmentsToDraw)
            {
                int iData = 0;
                int FirstSegmentToRender = SegmentsAlreadyDrawn;

                //If we draw more than one batch we need to redraw the last point from the former batch
                if (SegmentsAlreadyDrawn > 0)
                    FirstSegmentToRender -= 1;

                int iSegment = 0;
                int iSegmentToDraw = 0;
                //Draw as many segments as we can 
                for (iSegment = 0; iSegment < MaxInstancesPerBatch; iSegment++)
                {
                    iSegmentToDraw = iSegment + FirstSegmentToRender;

                    if (iSegmentToDraw >= numSegmentsToDraw)
                    {
                        iSegment -= 1;
                        break;
                    }
                        

                    translationData[iData++] = (float)roundLine.ControlPoints[iSegmentToDraw].X;
                    translationData[iData++] = (float)roundLine.ControlPoints[iSegmentToDraw].Y;
                    translationData[iData++] = (float)roundLine.DistanceNormalized[iSegmentToDraw];
                    translationData[iData++] = (float)roundLine.Theta[iSegmentToDraw];
                }

                numSegmentsThisDraw = iSegment;

                segmentDataParamter.SetValue(translationData);

                EffectPass pass = effect.CurrentTechnique.Passes[0];

                pass.Apply();

                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, numVertices, 0, numPrimitivesPerInstance * numSegmentsThisDraw);

                SegmentsAlreadyDrawn += numSegmentsThisDraw;
                         
            }
            //NumLinesDrawn += numInstancesThisDraw;
        }


        /// <summary>
        /// Draw a list of Lines in batches, up to the maximum number of instances for the shader.
        /// </summary>
        public void Draw(IEnumerable<RoundCurve> roundLines, float lineRadius, Color lineColor, Matrix viewProjMatrix,
            float time, Texture2D texture)
        {
            textureParameter.SetValue(texture);

            //Accomodate the texture aspect ratio by setting the min,max texture coordinates using the lineRadius and texture aspect ratio
            foreach (RoundCurve c in roundLines)
            {
                double textureAspectRatio = (double)texture.Width / (double)texture.Height;
                double curveAspectRatio = c.TotalDistance / (lineRadius * 2.0);
                double length_of_curve_to_use_for_texture = c.TotalDistance / (double)texture.Width;
                minTextureCoordinateParameter.SetValue(0);
                if (length_of_curve_to_use_for_texture < 1.0)
                    length_of_curve_to_use_for_texture = 1.0;
                maxTextureCoordinateParameter.SetValue((float)length_of_curve_to_use_for_texture);

                Draw(c, lineRadius, lineColor, viewProjMatrix, time, "Textured");
            }
        }

        /// <summary>
        /// Draw a label along the curve.
        /// </summary>
        /// <param name="roundCurve"></param>
        /// <param name="lineRadius"></param>
        /// <param name="lineColor"></param>
        /// <param name="viewProjMatrix"></param>
        /// <param name="time"></param>
        /// <param name="texture">Texture containing the label text</param>
        /// <param name="alignment">Where along the curve to render the label</param>
        /// <param name="max_curve_length_to_allocate">Maximum length of the curve to allocate to the label.  Useful if there are multiple labels to render</param>
        public void DrawLabel(RoundCurve roundCurve, float lineRadius, Color lineColor, Matrix viewProjMatrix,
            float time, Texture2D texture, HorizontalAlignment alignment, float max_curve_length_percentage_to_render_onto = 1.0f)
        {
            textureParameter.SetValue(texture);

            float length_of_curve_to_use_for_texture = LengthOfCurveRequiredToPreserveTextureAspectRatio(texture, (float)roundCurve.TotalDistance * max_curve_length_percentage_to_render_onto, lineRadius * 2);

            Vector2 texture_min_max = TextureStartStopForAlignment(alignment, length_of_curve_to_use_for_texture * max_curve_length_percentage_to_render_onto);

            //double length_of_curve_to_use_for_texture = roundCurve.TotalDistance / (double)texture.Width;
            minTextureCoordinateParameter.SetValue(texture_min_max.X);
            maxTextureCoordinateParameter.SetValue(texture_min_max.Y);

            Draw(roundCurve, lineRadius, lineColor, viewProjMatrix, time, "Textured");
        }
         
        /// <summary>
        /// Draw a list of Lines in batches, up to the maximum number of instances for the shader.
        /// </summary>
        public void Draw(IEnumerable<RoundCurve> roundLines, float lineRadius, Color lineColor, Matrix viewProjMatrix,
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
            
            foreach (RoundCurve roundLine in roundLines)
            {
                DrawOnConfiguredDevice(roundLine, lineRadius, lineColor, DefaultBlurThreshold);
            }
        }

        /// <summary>
        /// Return a normalized fraction of the curve to fill with the texture to preserve the input textures aspect ratio
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="roundCurve"></param>
        /// <param name="lineWidth"></param>
        /// <returns></returns>
        private float LengthOfCurveRequiredToPreserveTextureAspectRatio(Texture2D texture, float TotalDistance, float lineWidth)
        {
            double textureAspectRatio = (double)texture.Width / (double)texture.Height;
            double curveAspectRatio = TotalDistance / lineWidth;
            double length_of_curve_to_use_for_texture = (textureAspectRatio * lineWidth) / TotalDistance;

            return length_of_curve_to_use_for_texture > 1.0 ? 1.0f : (float)length_of_curve_to_use_for_texture;
        }

        private Vector2 TextureStartStopForAlignment(HorizontalAlignment alignment, float NormalizedLengthOfCurveToFillWithTexture)
        {
            switch (alignment)
            {
                case HorizontalAlignment.Left:
                    return new Vector2(0, NormalizedLengthOfCurveToFillWithTexture);
                case HorizontalAlignment.Center:
                    float padding = (1.0f - NormalizedLengthOfCurveToFillWithTexture) / 2.0f;
                    return new Vector2(padding, NormalizedLengthOfCurveToFillWithTexture + padding);
                case HorizontalAlignment.Right:
                    float left_padding = (1.0f - NormalizedLengthOfCurveToFillWithTexture);
                    return new Vector2(left_padding, 1.0f);
                default:
                    throw new ArgumentException("Unknown alignment type " + alignment.ToString());
            }
        }
    }

    public class CurveManagerHSV : CurveManager
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
            effect = content.Load<Effect>("RoundCurveHSV");
            _BackgroundTexture = effect.Parameters["BackgroundTexture"];
            _RenderTargetSize = effect.Parameters["RenderTargetSize"];
            LoadParameters(effect);
            CreateRoundLineMesh();
        }


    }
}
