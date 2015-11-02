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
        public static void SetupGraphicsDevice(GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, LocationCanvasView obj, long SectionNumber)
        {
            //oldVertexDeclaration = graphicsDevice.VertexDeclaration;
            OriginalBlendState = graphicsDevice.BlendState;
            OriginalRasterState = graphicsDevice.RasterizerState;

            if (RendererBlendState == null || RendererBlendState.IsDisposed)
            {
                RendererBlendState = new BlendState();


                RendererBlendState.AlphaSourceBlend = Blend.SourceAlpha;
                RendererBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
                RendererBlendState.ColorSourceBlend = Blend.SourceAlpha;
                RendererBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            }

            graphicsDevice.BlendState = RendererBlendState;

            if (RendererRasterizerState == null || RendererRasterizerState.IsDisposed)
            {
                RendererRasterizerState = new RasterizerState();
                RendererRasterizerState.FillMode = FillMode.Solid;
                RendererRasterizerState.CullMode = CullMode.None; 
            }

            graphicsDevice.RasterizerState = RendererRasterizerState; 
            
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
            if(OriginalBlendState != null)
                graphicsDevice.BlendState = OriginalBlendState;

            if(OriginalRasterState != null)
                graphicsDevice.RasterizerState = OriginalRasterState; 
            
            basicEffect.Texture = null;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        /// <summary>
        /// Draw the list of locations as they should appear for the given section number
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="SectionNumber"></param>
        public static void DrawBackgrounds(List<LocationCanvasView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            List<LocationOpenCurveView> OpenCurveLocations = listToDraw.Where(l => l.TypeCode == LocationType.OPENCURVE).Cast<LocationOpenCurveView>().ToList();
            DrawOpenCurveBackgrounds(OpenCurveLocations, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, Scene, SectionNumber);

            List<LocationClosedCurveView> ClosedCurveLocations = listToDraw.Where(l => l.TypeCode == LocationType.CLOSEDCURVE).Cast<LocationClosedCurveView>().ToList();
            DrawClosedCurveBackgrounds(ClosedCurveLocations, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, Scene, SectionNumber);

            //TODO: Use Group by instead of select
            List<LocationCircleView> CircleLocations = listToDraw.Where(l => l.TypeCode == LocationType.CIRCLE).Cast<LocationCircleView>().ToList();
            DrawCircleBackgrounds(CircleLocations, graphicsDevice, basicEffect, overlayEffect, Scene, SectionNumber);
        }

        public static void DrawOpenCurveBackgrounds(List<LocationOpenCurveView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            SetupGraphicsDevice(graphicsDevice, basicEffect, overlayEffect, listToDraw[0], SectionNumber);

            foreach (LocationOpenCurveView loc in listToDraw)
            {
                CurveView.Draw(graphicsDevice, overlayLineManager, basicEffect, loc.VolumeShape.ToPoints(), 3, false, loc.Parent.Type.Color.ConvertToHSL(0.5f), loc.Width);
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
                CurveView.Draw(graphicsDevice, overlayLineManager, basicEffect, loc.VolumeShape.ToPoints(), 3, true, loc.Parent.Type.Color.ConvertToHSL(0.5f), loc.Width);
            }

            RestoreGraphicsDevice(graphicsDevice, basicEffect);
        }

        /// <summary>
        /// Draw the list of locations as they should appear for the given section number
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="SectionNumber"></param>
        public static void DrawCircleBackgrounds(List<LocationCircleView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            IComparer<LocationCircleView> LocComparer = new LocationObjDrawOrderComparison();
            int iStart = 0;

            //Set the graphics device state to render the appropriate type
            

            do
            {
                int iEnd = listToDraw.Count; //Need to initialize or loop never ends

                LocationCircleView StartingObj = listToDraw[iStart];

                SetupGraphicsDevice(graphicsDevice, basicEffect, overlayEffect, StartingObj, SectionNumber);
                VertexPositionColorTexture[] VertArray = new VertexPositionColorTexture[listToDraw.Count * 4];
                int[] indicies = new int[listToDraw.Count * 6];

                int iNextVert = 0;
                int iNextVertIndex = 0;
                for (int iObj = iStart; iObj < listToDraw.Count; iObj++)
                {
                    LocationCircleView locToDraw = listToDraw[iObj];
                    int[] locIndicies;
                    VertexPositionColorTexture[] objVerts = locToDraw.GetBackgroundVerticies(Scene.VisibleWorldBounds, Scene.Camera.Downsample, (int)((long)SectionNumber - (long)locToDraw.Z),
                                                                                                   out locIndicies);

                    if(objVerts == null)
                        continue;

                    Array.Copy(objVerts, 0, VertArray, iNextVert, objVerts.Length);
                    

                    for (int iVert = 0; iVert < locIndicies.Length; iVert++)
                    {
                        indicies[iNextVertIndex + iVert] = locIndicies[iVert] + iNextVert;
                        
                    }

                    iNextVert += objVerts.Length;
                    iNextVertIndex += locIndicies.Length;
                }

                foreach (EffectPass pass in overlayEffect.effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                                         VertArray,
                                                                                         0,
                                                                                         VertArray.Length,
                                                                                         indicies,
                                                                                         0,
                                                                                         indicies.Length / 3); 

                }
               
                //Remove the drawn objects from the list
                iStart = iEnd; 
                //listToDraw.RemoveRange(0, iEnd);
            }
            while (iStart < listToDraw.Count);

            RestoreGraphicsDevice(graphicsDevice, basicEffect); 
        }

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
                    //Array.Copy(objVerts, 0, VertArray, iNextVert, objVerts.Length);

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
