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
                    overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.OPENCURVE:
                    overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.CLOSEDCURVE:
                    overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.POLYGON:
                    overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
                    basicEffect.Texture = null;
                    basicEffect.TextureEnabled = false;
                    basicEffect.VertexColorEnabled = true;
                    break;
                case LocationType.POLYLINE:
                    overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
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
                        overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
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

        /// <summary>
        /// Draw the list of locations as they should appear for the given section number
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="SectionNumber"></param>
        public static void DrawBackgrounds(List<LocationCanvasView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, 
                                           VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            int NumDepths = listToDraw.Max(l => l.ParentDepth);
            int DepthStencilStepSize = 10;

            int StartingDepthStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice);
            int EndingDepthStencilValue = StartingDepthStencilValue + (DepthStencilStepSize * NumDepths);
            int DepthStencilValue = EndingDepthStencilValue;
            
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, DepthStencilValue);

            var depthGroups = listToDraw.GroupBy(l => l.ParentDepth).OrderBy(l => l.Key).Reverse(); 
            foreach(var depthGroup in depthGroups)
            { 
                var typeGroups = depthGroup.GroupBy(l => l.GetType());
                foreach (var typeGroup in typeGroups)
                {
                    if (typeGroup.Key == typeof(LocationOpenCurveView))
                    {
                        LocationOpenCurveView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, typeGroup.Cast<LocationOpenCurveView>().ToArray());
                    }
                    else if (typeGroup.Key == typeof(LocationClosedCurveView))
                    {
                        LocationClosedCurveView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, typeGroup.Cast<LocationClosedCurveView>().ToArray());
                    }
                    else if (typeGroup.Key == typeof(LocationLineView))
                    {
                        LocationLineView.Draw(graphicsDevice, Scene, overlayLineManager, basicEffect, overlayEffect, typeGroup.Cast<LocationLineView>().ToArray());
                    }
                    else if (typeGroup.Key == typeof(LocationCircleView))
                    {
                        LocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, typeGroup.Cast<LocationCircleView>().ToArray());
                    }
                    else if (typeGroup.Key == typeof(AdjacentLocationCircleView))
                    {
                        AdjacentLocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, typeGroup.Cast<AdjacentLocationCircleView>().ToArray(), VisibleSectionNumber);
                    }
                    else if (typeGroup.Key == typeof(AdjacentLocationLineView))
                    {
                        AdjacentLocationLineView.Draw(graphicsDevice, Scene, overlayLineManager, basicEffect, overlayEffect, typeGroup.Cast<AdjacentLocationLineView>().ToArray(), VisibleSectionNumber);
                    }
                    else
                    {
                        throw new ArgumentException("Cannot draw background for unknown type" + typeGroup.Key.FullName);
                    }
                }

                DepthStencilValue -= DepthStencilStepSize;

                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, float.MaxValue, 0);

                DeviceStateManager.SetDepthStencilValue(graphicsDevice, DepthStencilValue);
            }

            DeviceStateManager.SetDepthStencilValue(graphicsDevice, EndingDepthStencilValue + 1);
        }
        

        public static void DrawPolyLineBackgrounds(List<LocationLineView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager, VikingXNA.Scene Scene, int SectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            LocationLineView.Draw(graphicsDevice, Scene, overlayLineManager, basicEffect, overlayEffect, listToDraw.ToArray());
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
