using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNAGraphics;
using WebAnnotation.View;

namespace WebAnnotation
{
    /// <summary>
    /// This class draws LocationObj's
    /// </summary>
    internal static class LocationObjRenderer
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
                                           OverlayShaderEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            if (listToDraw.Count == 0)
            {
                return;
            }

            int MaxCanvasViewDepth = int.MinValue;
            List<int> depths = [];
            for (int i = 0; i < listToDraw.Count; i++)
            {
                int depth = listToDraw[i].ParentDepth;
                if (depth > MaxCanvasViewDepth)
                    MaxCanvasViewDepth = depth;

                if (!depths.Contains(depth))
                    depths.Add(depth);
            }

            int StartingDepthStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice);
            const int DepthStencilStepSize = 5;
            int EndingDepthStencilValue = StartingDepthStencilValue + (DepthStencilStepSize * MaxCanvasViewDepth);
            int DepthStencilValue = EndingDepthStencilValue;

            //Deepest group first
            depths.Sort();

            DeviceStateManager.SaveDeviceState(graphicsDevice);

            DeviceStateManager.SetRasterizerStateForShapes(graphicsDevice);

            for (int iDepth = depths.Count - 1; iDepth >= 0; iDepth--)
            {
                List<TypeBucket> depthGroup = BucketByType(listToDraw, depths[iDepth]);

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

                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1, 0);

                DepthStencilValue -= DepthStencilStepSize;
            }

            DeviceStateManager.SetDepthBuffer(graphicsDevice, CompareFunction.LessEqual);
            DeviceStateManager.RestoreDeviceState(graphicsDevice);
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, EndingDepthStencilValue + 1);
        }

        /// <summary>
        /// The views of one concrete type, in the order they appeared in the source list.
        /// </summary>
        private sealed class TypeBucket(Type type)
        {
            public readonly Type Type = type;
            public readonly List<LocationCanvasView> Views = [];
        }

        private static void AddToBucket(List<TypeBucket> buckets, LocationCanvasView view)
        {
            Type type = view.GetType();
            for (int i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Type == type)
                {
                    buckets[i].Views.Add(view);
                    return;
                }
            }

            TypeBucket bucket = new(type);
            bucket.Views.Add(view);
            buckets.Add(bucket);
        }

        /// <summary>
        /// Groups the views at one depth by concrete type. Buckets are ordered by each type's first appearance and the views in a
        /// bucket keep their source order, so drawing buckets in order draws in the same order a GroupBy would.
        /// </summary>
        private static List<TypeBucket> BucketByType(List<LocationCanvasView> views, int depth)
        {
            List<TypeBucket> buckets = [];
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].ParentDepth == depth)
                    AddToBucket(buckets, views[i]);
            }

            return buckets;
        }

        private static List<TypeBucket> BucketByType(ICollection<LocationCanvasView> views)
        {
            List<TypeBucket> buckets = [];
            foreach (LocationCanvasView view in views)
                AddToBucket(buckets, view);

            return buckets;
        }

        private static T[] ToArray<T>(List<LocationCanvasView> views) where T : LocationCanvasView
        {
            T[] result = new T[views.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = (T)views[i];

            return result;
        }

        private static void DrawBackgroundsAtDepth(List<TypeBucket> typeGroups, GraphicsDevice graphicsDevice, BasicEffect basicEffect,
                                           OverlayShaderEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            foreach (TypeBucket typeGroup in typeGroups)
            {
                DrawTypeBucket(typeGroup, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, overlayCurveManager, Scene, VisibleSectionNumber);
            }
        }

        private static void DrawTypeBucket(TypeBucket typeGroup, GraphicsDevice graphicsDevice, BasicEffect basicEffect,
                                           OverlayShaderEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            if (typeGroup.Type == typeof(LocationOpenCurveView))
            {
                LocationOpenCurveView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, ToArray<LocationOpenCurveView>(typeGroup.Views));
            }
            else if (typeGroup.Type == typeof(LocationClosedCurveView))
            {
                LocationClosedCurveView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, ToArray<LocationClosedCurveView>(typeGroup.Views));
            }
            else if (typeGroup.Type == typeof(LocationPolygonView))
            {
                LocationPolygonView.Draw(graphicsDevice, Scene, overlayCurveManager, basicEffect, overlayEffect, ToArray<LocationPolygonView>(typeGroup.Views));
            }
            else if (typeGroup.Type == typeof(LocationLineView))
            {
                LocationLineView.Draw(graphicsDevice, Scene, overlayLineManager, basicEffect, overlayEffect, ToArray<LocationLineView>(typeGroup.Views));
            }
            else if (typeGroup.Type == typeof(LocationCircleView))
            {
                LocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, ToArray<LocationCircleView>(typeGroup.Views));
            }
            else if (typeGroup.Type == typeof(AdjacentLocationCircleView))
            {
                AdjacentLocationCircleView.Draw(graphicsDevice, Scene, basicEffect, overlayEffect, ToArray<AdjacentLocationCircleView>(typeGroup.Views), VisibleSectionNumber);
            }
            else if (typeGroup.Type == typeof(AdjacentLocationLineView))
            {
                AdjacentLocationLineView.Draw(graphicsDevice, Scene, overlayLineManager, basicEffect, overlayEffect, ToArray<AdjacentLocationLineView>(typeGroup.Views), VisibleSectionNumber);
            }
            else
            {
                throw new ArgumentException("Cannot draw background for unknown type" + typeGroup.Type.FullName);
            }
        }

        public static void DrawCanvasView(ICollection<LocationCanvasView> views, GraphicsDevice graphicsDevice, BasicEffect basicEffect,
                                           OverlayShaderEffect overlayEffect, RoundLineCode.RoundLineManager overlayLineManager,
                                           RoundCurve.CurveManager overlayCurveManager,
                                           VikingXNA.Scene Scene, int VisibleSectionNumber)
        {
            foreach (TypeBucket typeGroup in BucketByType(views))
            {
                DrawTypeBucket(typeGroup, graphicsDevice, basicEffect, overlayEffect, overlayLineManager, overlayCurveManager, Scene, VisibleSectionNumber);
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
            {
                return labelParts;
            }

            foreach (string word in labelParts)
            {
                if (topRow.Length + word.Length + 1 <= (label.Length / 2))
                {
                    if (topRow.Length == 0)
                    {
                        topRow += word;
                    }
                    else
                    {
                        topRow += " " + word;
                    }
                }
                else
                {
                    if (bottomRow.Length == 0)
                    {
                        bottomRow += word;
                    }
                    else
                    {
                        bottomRow += " " + word;
                    }
                }
            }

            topRow = topRow.TrimEnd();
            bottomRow = bottomRow.TrimEnd();

            return [topRow, bottomRow];
        }
    }
}
