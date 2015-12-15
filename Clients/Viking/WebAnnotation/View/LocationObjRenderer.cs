using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

using Geometry;
using VikingXNAGraphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotation.UI;
using WebAnnotationModel;
using WebAnnotation.ViewModel;
using WebAnnotation.View;
using SqlGeometryUtils;

namespace WebAnnotation
{
    /// <summary>
    /// Sort LocationObj's according to rendering requirements
    /// </summary>
    public class LocationObjDrawOrderComparison : IComparer<LocationObj>, IComparer<LocationCanvasView>
    {
        #region IComparer<LocationObj> Members

        int IComparer<LocationObj>.Compare(LocationObj x, LocationObj y)
        {
            return CompareObj(x, y);
        }

        #endregion

        int IComparer<LocationCanvasView>.Compare(LocationCanvasView x, LocationCanvasView y)
        {
            LocationObj X = x.modelObj;
            LocationObj Y = y.modelObj;

            return CompareObj(X, Y);
        }

        private static int CompareObj(LocationObj x, LocationObj y)
        {
            //First sort by type
            if (x.TypeCode != y.TypeCode)
            {
                return x.TypeCode - y.TypeCode;
            }

            //if the type is the same, sort by section differential
            return (int)(x.Section - y.Section);
        }
    }

    static class DeviceStateManager
    {
        static BlendState OriginalBlendState;
        static RasterizerState OriginalRasterState;

        static BlendState ShapeRendererBlendState = null;
        static RasterizerState ShapeRendererRasterizerState = null;

        static DepthStencilState depthstencilState;

        public static void SaveDeviceState(GraphicsDevice graphicsDevice)
        {
            OriginalBlendState = graphicsDevice.BlendState;
            OriginalRasterState = graphicsDevice.RasterizerState;
        }

        public static void RestoreDeviceState(GraphicsDevice graphicsDevice)
        {
            if (OriginalBlendState != null)
                graphicsDevice.BlendState = OriginalBlendState;

            if (OriginalRasterState != null)
                graphicsDevice.RasterizerState = OriginalRasterState;
        }

        public static void SetRenderStateForShapes(GraphicsDevice graphicsDevice)
        {
            if (ShapeRendererBlendState == null || ShapeRendererBlendState.IsDisposed)
            {
                ShapeRendererBlendState = new BlendState();

                ShapeRendererBlendState.AlphaSourceBlend = Blend.SourceAlpha;
                ShapeRendererBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
                ShapeRendererBlendState.ColorSourceBlend = Blend.SourceAlpha;
                ShapeRendererBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            }

            graphicsDevice.BlendState = ShapeRendererBlendState;
        }

        public static void SetRasterizerStateForShapes(GraphicsDevice graphicsDevice)
        {
            if (ShapeRendererRasterizerState == null || ShapeRendererRasterizerState.IsDisposed)
            {
                ShapeRendererRasterizerState = new RasterizerState();
                ShapeRendererRasterizerState.FillMode = FillMode.Solid;
                ShapeRendererRasterizerState.CullMode = CullMode.None;
            }

            graphicsDevice.RasterizerState = ShapeRendererRasterizerState;
        }

        public static void SetDepthStencilValue(GraphicsDevice device, int StencilValue)
        {
            if (depthstencilState != null)
            {
                depthstencilState.Dispose();
                depthstencilState = null;
            }

            if (depthstencilState == null || depthstencilState.IsDisposed)
            {
                depthstencilState = new DepthStencilState();
                depthstencilState.DepthBufferEnable = true;
                depthstencilState.DepthBufferWriteEnable = true;
                depthstencilState.DepthBufferFunction = CompareFunction.LessEqual;

                depthstencilState.StencilEnable = true;
                depthstencilState.StencilFunction = CompareFunction.GreaterEqual;
                depthstencilState.ReferenceStencil = StencilValue;
                depthstencilState.StencilPass = StencilOperation.Replace;

                device.DepthStencilState = depthstencilState;
            }
        }

        public static int GetDepthStencilValue(GraphicsDevice device)
        {
            return device.DepthStencilState.ReferenceStencil; 
        } 
    }

    /// <summary>
    /// This class draws LocationObj's
    /// </summary>
    static class LocationObjRenderer
    {
        static BlendState OriginalBlendState;
        static RasterizerState OriginalRasterState;

        static BlendState RendererBlendState = null;
        static RasterizerState RendererRasterizerState = null; 

        /// <summary>
        /// Setup the graphicsDevice and effect to render the type of obj passed
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="obj"></param>
        public static void SetupGraphicsDevice(GraphicsDevice graphicsDevice,
                                               BasicEffect basicEffect,
                                               VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                                               LocationCanvasView obj,
                                               long SectionNumber)
        {
            //oldVertexDeclaration = graphicsDevice.VertexDeclaration;
            DeviceStateManager.SaveDeviceState(graphicsDevice);
            DeviceStateManager.SetRenderStateForShapes(graphicsDevice);
            DeviceStateManager.SetRasterizerStateForShapes(graphicsDevice); 
            
            int SectionDelta = (int)(obj.Z - SectionNumber);

            switch (obj.TypeCode)
            {
                case LocationType.POINT:
                    overlayEffect.AnnotateWithCircle((float)0.05);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.OPENCURVE:
                    overlayEffect.AnnotateWithCircle((float)0.05);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.CLOSEDCURVE:
                    overlayEffect.AnnotateWithCircle((float)0.05);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.POLYGON:
                    overlayEffect.AnnotateWithCircle((float)0.05);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.POLYLINE:
                    overlayEffect.AnnotateWithCircle((float)0.05);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.CIRCLE:
                    //Are we rendering on the same section or above/below?
                    if (SectionDelta < 0)
                    {
                        overlayEffect.AnnotateWithTexture(GlobalPrimitives.UpArrowTexture);
                    }
                    else if (SectionDelta == 0)
                    {
                        overlayEffect.AnnotateWithCircle((float)0.05);
                    }
                    else
                    {
                        overlayEffect.AnnotateWithTexture(GlobalPrimitives.UpArrowTexture);
                    }

                    basicEffect.TextureEnabled = true;
                    basicEffect.VertexColorEnabled = true;
                    basicEffect.LightingEnabled = false; 

                    break;
                default:
                    break; 
            }
        }

        /// <summary>
        /// Put the graphics device state back where we found it
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            DeviceStateManager.RestoreDeviceState(graphicsDevice);

            basicEffect.Texture = null;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        public static void Draw(List<LocationCanvasView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            List<LocationOpenCurveView> OpenCurveLocations = listToDraw.Where(l => l.TypeCode == LocationType.OPENCURVE).Cast<LocationOpenCurveView>().ToList();
            DrawOpenCurveBackgrounds(OpenCurveLocations, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, Scene, SectionNumber);

            List<LocationClosedCurveView> ClosedCurveLocations = listToDraw.Where(l => l.TypeCode == LocationType.CLOSEDCURVE).Cast<LocationClosedCurveView>().ToList();
            DrawClosedCurveBackgrounds(ClosedCurveLocations, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, Scene, SectionNumber);

            //TODO: Use Group by instead of select
            LocationCircleView[] CircleLocations = listToDraw.Where(l => l.TypeCode == LocationType.CIRCLE).Cast<LocationCircleView>().ToArray();
            LocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, CircleLocations);
        }

        /// <summary>
        /// Draw the list of locations as they should appear for the given section number
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="SectionNumber"></param>
        public static void DrawBackgrounds(List<LocationCanvasView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            List<LocationOpenCurveView> OpenCurveLocations = listToDraw.Where(l => l.TypeCode == LocationType.OPENCURVE).Cast<LocationOpenCurveView>().ToList();
            DrawOpenCurveBackgrounds(OpenCurveLocations, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, Scene, VisibleSectionNumber);

            List<LocationClosedCurveView> ClosedCurveLocations = listToDraw.Where(l => l.TypeCode == LocationType.CLOSEDCURVE).Cast<LocationClosedCurveView>().ToList();
            DrawClosedCurveBackgrounds(ClosedCurveLocations, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, Scene, VisibleSectionNumber);

            //TODO: Use Group by instead of select
            LocationCircleView[] CircleLocations = listToDraw.Where(l => l.GetType() == typeof(LocationCircleView)).Cast<LocationCircleView>().ToArray();
            LocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, CircleLocations);

            AdjacentLocationCircleView[] AdjacentCircleLocations = listToDraw.Where(l => l.GetType() == typeof(AdjacentLocationCircleView)).Cast<AdjacentLocationCircleView>().ToArray();
            AdjacentLocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, AdjacentCircleLocations, VisibleSectionNumber);
        }


        public static void DrawOpenCurveBackgrounds(List<LocationOpenCurveView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            SetupGraphicsDevice(graphicsDevice, basicEffect, overlayEffect, listToDraw[0], SectionNumber);

            foreach (LocationOpenCurveView loc in listToDraw)
            {
                CurveView.Draw(graphicsDevice, overlayLineManager, basicEffect, loc.VolumeControlPoints, loc.VolumeCurveControlPoints, loc.Parent.Type.Color.ConvertToHSL(0.5f), loc.Width);
            }

            RestoreGraphicsDevice(graphicsDevice, basicEffect);
        }

        public static void DrawClosedCurveBackgrounds(List<LocationClosedCurveView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            SetupGraphicsDevice(graphicsDevice, basicEffect, overlayEffect, listToDraw[0], SectionNumber);

            foreach (LocationClosedCurveView loc in listToDraw)
            {
                CurveView.Draw(graphicsDevice, overlayLineManager, basicEffect, loc.VolumeControlPoints, loc.VolumeCurveControlPoints, loc.Parent.Type.Color.ConvertToHSL(0.5f), loc.Width);
            }

            RestoreGraphicsDevice(graphicsDevice, basicEffect);
        }

        /*
        /// <summary>
        /// Draw the list of locations as they should appear for the given section number
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="SectionNumber"></param>
        public static void DrawOverlappedAdjacentLinkedLocations(List<LocationCircleView> listToDraw, VikingXNA.Scene Scene, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            IComparer<LocationCanvasView> LocComparer = new LocationObjDrawOrderComparison();
            int iStart = 0;

            //Set the graphics device state to render the appropriate type
            do
            {
                int iEnd = listToDraw.Count; //Need to initialize or loop never ends

                LocationCanvasView StartingObj = listToDraw[iStart];

                SetupGraphicsDevice(graphicsDevice, basicEffect, overlayEffect, StartingObj, SectionNumber);

                overlayEffect.AnnotateWithTexture(GlobalPrimitives.UpArrowTexture);
                List<VertexPositionColorTexture> VertArray = new List<VertexPositionColorTexture>(listToDraw.Count * 4 * 2);
                List<int> indicies = new List<int>(listToDraw.Count * 6 * 2);

                int iNextVert = 0;
                int iNextVertIndex = 0;
                for (int iObj = iStart; iObj < listToDraw.Count; iObj++)
                {
                    LocationCircleView locToDraw = listToDraw[iObj];
                    int[] locIndicies;

                    if (!locToDraw.OverlappingLocationLinksCanBeSeen(Scene.Camera.Downsample))
                        continue;
                    
                    VertexPositionColorTexture[] objVerts = locToDraw.GetLinkedLocationBackgroundVerts(Scene.VisibleWorldBounds, Scene.Camera.Downsample, 
                                                                                                       out locIndicies);

                    if (objVerts == null)
                        continue;

                    if (objVerts.Length == 0)
                        continue; 

                    VertArray.AddRange(objVerts);

                    for (int iVert = 0; iVert < locIndicies.Length; iVert++)
                    {
                        indicies.Add( locIndicies[iVert] + iNextVert);
                    }

                    iNextVert += objVerts.Length;
                    iNextVertIndex += locIndicies.Length;
                }

                if (VertArray.Count > 0 && indicies.Count > 0)
                {
                    foreach (EffectPass pass in overlayEffect.effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                                             VertArray.ToArray(),
                                                                                             0,
                                                                                             VertArray.Count,
                                                                                             indicies.ToArray(),
                                                                                             0,
                                                                                             indicies.Count / 3);


                    }
                }

                //Remove the drawn objects from the list
                iStart = iEnd;
                //listToDraw.RemoveRange(0, iEnd);
            }
            while (iStart < listToDraw.Count);

            RestoreGraphicsDevice(graphicsDevice, basicEffect); 
        }
        */

        /// <summary>
        /// Divide the label into two lines
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        public static string[] SplitLabel(string label)
        {  
            //Split the string at the first space before the midpoint
            string topRow = "";
            string bottomRow = "";
            string[] labelParts = label.Split();

            if (labelParts.Length <= 2)
                return labelParts;

            foreach (string word in labelParts)
            {
                if (topRow.Length + word.Length + 1 <= (label.Length / 2))
                {
                    if (topRow.Length == 0)
                        topRow += word;
                    else
                        topRow += " " + word;
                }
                else
                {
                    if (bottomRow.Length == 0)
                        bottomRow += word;
                    else
                        bottomRow += " " + word;
                }
            }

            topRow = topRow.TrimEnd();
            bottomRow = bottomRow.TrimEnd();

            return new String[] { topRow, bottomRow };
        } 
    }
}
