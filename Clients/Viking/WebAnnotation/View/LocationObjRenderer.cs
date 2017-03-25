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
    /// This class draws LocationObj's
    /// </summary>
    static class LocationObjRenderer
    {
        /// <summary>
        /// Draw the list of locations as they should appear for the given section number.
        /// 
        /// When we draw backgrounds we want to treat them as opaque even if they have some alpha in the color.
        /// The alpha indicates how much of the background texture shows through.  We do not want to blend with 
        /// other annotation backgrounds.
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="basicEffect"></param>
        /// <param name="SectionNumber"></param>
        public static void DrawBackgrounds(List<LocationCanvasView> listToDraw, GraphicsDevice graphicsDevice, BasicEffect basicEffect, 
                                           AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            if (listToDraw.Count == 0)
                return;

            int MaxCanvasViewDepth = listToDraw.Max(l => l.ParentDepth);
             
            int StartingDepthStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice);
            const int DepthStencilStepSize = 5;
            int EndingDepthStencilValue = StartingDepthStencilValue + (DepthStencilStepSize * MaxCanvasViewDepth);
            int DepthStencilValue = EndingDepthStencilValue;

            var depthGroups = listToDraw.GroupBy(l => l.ParentDepth).OrderBy(l => l.Key).Reverse();

            DeviceStateManager.SaveDeviceState(graphicsDevice);
            
            DeviceStateManager.SetRasterizerStateForShapes(graphicsDevice);

            foreach (var depthGroup in depthGroups)
            {
                //We render twice.  The first time we only update the Z-buffer. 
                //The second time we write colors, but only when the Z-buffer is equal to the objects Z-value.
                //This ensures that overlapping locations are not rendered overlapping which obscures the TEM textures underneath.
                DeviceStateManager.SetDepthStencilValue(graphicsDevice, DepthStencilValue);
                DeviceStateManager.SetDepthBuffer(graphicsDevice, CompareFunction.LessEqual);
                DeviceStateManager.SetRenderStateForShapes(graphicsDevice, ColorWriteChannels.None);
                
                // graphicsDevice.BlendState.ColorWriteChannels = ColorWriteChannels.None;

                //Draw backgrounds once to populate the Z-buffer and stencil buffer but do not write colors
                DrawBackgroundsAtDepth(depthGroup, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, overlayCurveManager, Scene, VisibleSectionNumber);

              //  graphicsDevice.BlendState.ColorWriteChannels = ColorWriteChannels.All;
                DeviceStateManager.SetDepthStencilValue(graphicsDevice, DepthStencilValue, stencilFunction: CompareFunction.Equal);
                DeviceStateManager.SetDepthBuffer(graphicsDevice, CompareFunction.Equal);
                DeviceStateManager.SetRenderStateForShapes(graphicsDevice, ColorWriteChannels.All);

                //Draw backgrounds again and only update colors where the depth and stencil values match
                DrawBackgroundsAtDepth(depthGroup, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, overlayCurveManager, Scene, VisibleSectionNumber);
                
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, float.MaxValue, 0);

                DepthStencilValue -= DepthStencilStepSize;
            }
             
            DeviceStateManager.SetDepthBuffer(graphicsDevice, CompareFunction.LessEqual);
            DeviceStateManager.RestoreDeviceState(graphicsDevice);
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, EndingDepthStencilValue + 1); 
        }

        private static void DrawBackgroundsAtDepth(IGrouping<int, LocationCanvasView> depthGroup, GraphicsDevice graphicsDevice, BasicEffect basicEffect,
                                           AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            IEnumerable<IGrouping<Type, LocationCanvasView>> typeGroups = depthGroup.GroupBy(l => l.GetType());

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
                else if (typeGroup.Key == typeof(LocationPolygonView))
                {
                    LocationPolygonView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, typeGroup.Cast<LocationPolygonView>().ToArray());
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
        }

        public static void DrawCanvasView(ICollection<LocationCanvasView> views, GraphicsDevice graphicsDevice, BasicEffect basicEffect,
                                           AnnotationOverBackgroundLumaEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            IEnumerable<IGrouping<Type, LocationCanvasView>> typeGroups = views.GroupBy(l => l.GetType());

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
                else if (typeGroup.Key == typeof(LocationPolygonView))
                {
                    LocationPolygonView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, typeGroup.Cast<LocationPolygonView>().ToArray());
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
