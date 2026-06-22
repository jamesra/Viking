using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class TextureCircleView(Texture2D texture, GridCircle circle, Color color, BuiltinTexture icon = BuiltinTexture.None) : CircleView(circle, color)
    {
        public Texture2D Texture = texture;

        /// <summary>Texture layer index for instanced circle drawing (BuiltinTexture ordinal).</summary>
        public BuiltinTexture Icon { get; } = icon;

        public static TextureCircleView CreateUpArrow(GridCircle circle, Color color) => new(GlobalPrimitives.UpArrowTexture, circle, color, BuiltinTexture.UpArrow);

        public static TextureCircleView CreateDownArrow(GridCircle circle, Color color) => new(GlobalPrimitives.DownArrowTexture, circle, color, BuiltinTexture.DownArrow);

        public static TextureCircleView CreatePlusCircle(GridCircle circle, Color color) => new(GlobalPrimitives.PlusTexture, circle, color, BuiltinTexture.Plus);

        public static TextureCircleView CreateMinusCircle(GridCircle circle, Color color) => new(GlobalPrimitives.MinusTexture, circle, color, BuiltinTexture.Minus);

        public static TextureCircleView CreateCircle(GridCircle circle, Color color) => new(GlobalPrimitives.CircleTexture, circle, color, BuiltinTexture.Circle);

        public static TextureCircleView CreateChainCircle(GridCircle circle, Color color) => new(GlobalPrimitives.ChainTexture, circle, color, BuiltinTexture.Chain);

        public static TextureCircleView CreateConnectCircle(GridCircle circle, Color color) => new(GlobalPrimitives.ConnectTexture, circle, color, BuiltinTexture.Connect);

        public static TextureCircleView CreateXCircle(GridCircle circle, Color color) => new(GlobalPrimitives.CircleXTexture, circle, color, BuiltinTexture.X);

        public new static void SetupGraphicsDevice(GraphicsDevice device, OverlayShaderEffect overlayEffect)
        {
            //Note one still needs to set the texture for the effect before rendering after calling this method
            //DeviceStateManager.SaveDeviceState(device);
            //DeviceStateManager.SetRenderStateForShapes(device);
            //DeviceStateManager.SetRasterizerStateForShapes(device);
        }


        public new static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, OverlayShaderEffect overlayEffect)
        {
            //DeviceStateManager.RestoreDeviceState(graphicsDevice); 
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayShaderEffect overlayEffect,
                          TextureCircleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            //var rstate = new RasterizerState();
            //rstate.CullMode = CullMode.None;
            //device.RasterizerState = rstate; 

            TextureCircleView.SetupGraphicsDevice(device, overlayEffect);

            device.Indices = GlobalPrimitives.GetUnitCircleIndexBuffer(device);
            device.SetVertexBuffer(GlobalPrimitives.GetUnitCircleVertexBuffer(device));

            //overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOverlayEffect;
            var textureGroups = listToDraw.GroupBy(l => l.Texture);
            foreach (var textureGroup in textureGroups)
            {
                TextureCircleView[] views = [.. textureGroup];
                overlayEffect.AnnotationTexture = textureGroup.Key;
                overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOverlayEffect;

                foreach (TextureCircleView cv in textureGroup)
                {
                    overlayEffect.AnnotationColorHSL = cv.HSLColor;
                    overlayEffect.WorldViewProjMatrix = (cv.ModelMatrix * scene.World) * scene.ViewProj;
                    overlayEffect.InputLumaAlphaValue = 0f;
                    /*
                    int[] indicies;
                    VertexPositionColorTexture[] VertArray = AggregatePrimitives(views, out indicies);
                    */
                    foreach (EffectPass pass in overlayEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                            0,
                            0,
                            2);

                        /*device.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                                             VertArray,
                                                                                             0,
                                                                                             VertArray.Length,
                                                                                             indicies,
                                                                                             0,
                                                                                             indicies.Length / 3);
                                                                                             */
                    }
                }
            }

            TextureCircleView.RestoreGraphicsDevice(device, overlayEffect);
        }

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            CircleView.Draw(device, scene, Overlay, [this]);
        }
    }


    public class CircleView : IColorView, IViewPosition2D, IRenderable
    {
        #region static

        //static double BeginFadeCutoff = 0.1;
        static readonly double InvisibleCutoff = 1.5f;

        /// <summary>
        /// Optional accessor function to get the current smallest rendered size setting.
        /// If null, the smallest rendered size check is skipped.
        /// </summary>
        public static Func<double> SmallestRenderedSizeAccessor { get; set; }

        /// <summary>
        /// Return true if a circle with the given radius and center would be visible if rendered into the scene.
        /// </summary>
        /// <param name="radius">Circle radius in world coordinates</param>
        /// <param name="center">Circle center in world coordinates</param>
        /// <param name="scene">Scene to check visibility against</param>
        /// <returns>True if the circle would be visible</returns>
        public static bool IsCircleVisible(double radius, GridVector2 center, VikingXNA.Scene scene)
        {
            GridCircle circle = new(center, radius);

            // Check if circle intersects visible world bounds
            if (!scene.VisibleWorldBounds.Intersects(circle))
                return false;

            // Check the existing InvisibleCutoff ratio logic
            double maxDimension = Math.Max(scene.VisibleWorldBounds.Width, scene.VisibleWorldBounds.Height);
            double LocToScreenRatio = radius * 2.0 / maxDimension;
            if (LocToScreenRatio > InvisibleCutoff)
                return false;

            // Check smallest rendered size if accessor is provided
            if (SmallestRenderedSizeAccessor != null)
            {
                double smallestRenderedSize = SmallestRenderedSizeAccessor();
                // Calculate circle diameter in pixels 
                if (radius * 2.0 < smallestRenderedSize)
                    return false;
            }

            return true;
        }

        #endregion

        protected Matrix ModelMatrix = Matrix.Identity;


        private GridCircle _Circle;
        public GridCircle Circle
        {
            get => _Circle;
            set
            {
                ClearCachedData();
                _Circle = value;
                UpdateModelMatrix();
            }
        }

        public GridVector2 VolumePosition => _Circle.Center;

        public double Radius => _Circle.Radius;

        public float Alpha
        {
            get => _Color.GetAlpha();
            set => Color = this._Color.SetAlpha(value);
        }


        private Microsoft.Xna.Framework.Color _Color;
        public Microsoft.Xna.Framework.Color Color
        {
            get => _Color;
            set
            {
                _Color = value;
                _HSLColor = value.ConvertToHCL();
                ClearCachedData();
            }
        }

        private Microsoft.Xna.Framework.Color _HSLColor;
        public Microsoft.Xna.Framework.Color HSLColor => _HSLColor;

        /// <summary>
        /// Called when we have changed a property that affects rendering
        /// </summary>
        public void ClearCachedData() { }

        /// <summary>
        /// Return true if the circle would be visible if rendered into the scene
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool IsVisible(VikingXNA.Scene scene) => IsCircleVisible(Radius, VolumePosition, scene);

        public CircleView(GridCircle circle, Color color)
        {
            this.Circle = circle;
            this.Color = color;

            UpdateModelMatrix();
            //this.ModelMatrix = Matrix.CreateTranslation(circle.Center.ToXNAVector3(0)) * Matrix.CreateScale((float)circle.Radius);
        }

        private void UpdateModelMatrix()
        {
            this.ModelMatrix = Matrix.CreateScale((float)_Circle.Radius,
                                                  (float)_Circle.Radius,
                                                  1f) * Matrix.CreateTranslation(_Circle.Center.ToXNAVector3(0));
        }

        #region Render Code

        GridVector2 IViewPosition2D.Position
        {
            get => this.VolumePosition;

            set => Circle = new GridCircle(value, this.Radius);
        }

        public static void SetupGraphicsDevice(GraphicsDevice device, OverlayShaderEffect overlayEffect)
        {
            DeviceStateManager.SaveDeviceState(device);

            if (overlayEffect != null)
            {
                //overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
            }
        }

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, OverlayShaderEffect overlayEffect) => DeviceStateManager.RestoreDeviceState(graphicsDevice);

        /// <summary>
        /// Draw all circles (solid and textured) in one or more instanced draw calls using CircleInstancedEffect.
        /// </summary>
        public static void Draw(GraphicsDevice device,
            VikingXNA.IScene scene,
            CircleInstancedEffect instancedEffect,
            CircleView[] listToDraw,
            Vector2? renderTargetSize = null,
            Texture2D backgroundTexture = null,
            float inputLumaAlpha = 0f)
        {
            if (listToDraw == null || listToDraw.Length == 0)
                return;

            Matrix viewProj = scene.World * scene.ViewProj;
            Vector2 rtSize = renderTargetSize ?? new Vector2(device.Viewport.Width, device.Viewport.Height);

            var circleData = new Vector4[CircleInstancedEffect.MaxInstancesPerBatch];
            var circleColors = new Vector4[CircleInstancedEffect.MaxInstancesPerBatch];
            int maxBatch = CircleInstancedEffect.MaxInstancesPerBatch;

            for (int chunkStart = 0; chunkStart < listToDraw.Length; chunkStart += maxBatch)
            {
                int count = Math.Min(maxBatch, listToDraw.Length - chunkStart);
                for (int i = 0; i < count; i++)
                {
                    CircleView cv = listToDraw[chunkStart + i];
                    circleData[i] = new Vector4((float)cv.Circle.Center.X, (float)cv.Circle.Center.Y, (float)cv.Circle.Radius,
                        cv is TextureCircleView tcv ? (float)(int)tcv.Icon : 0f);
                    circleColors[i] = cv.HSLColor.ToVector4();
                }
                instancedEffect.Draw(viewProj, rtSize, backgroundTexture, inputLumaAlpha, circleData, circleColors, count);
            }
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayShaderEffect overlayEffect,
                          CircleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            device.Indices = GlobalPrimitives.GetUnitCircleIndexBuffer(device);
            device.SetVertexBuffer(GlobalPrimitives.GetUnitCircleVertexBuffer(device));

            //Draw textured circles in the array
            TextureCircleView[] arrayTextureCircles = [.. listToDraw.Select(c => c as TextureCircleView).Where(c => c as TextureCircleView != null)];
            TextureCircleView.Draw(device, scene, overlayEffect, arrayTextureCircles);

            //Draw untextured circles in the array
            listToDraw = [.. listToDraw.Where(c => c as TextureCircleView is null)];
            if (listToDraw.Length == 0)
                return;

            CircleView[] arraySolidCircles = [.. listToDraw.Where(c => c as TextureCircleView is null)];

            Matrix worldViewProj = scene.World * scene.ViewProj;
            foreach (CircleView cv in arraySolidCircles)
            {
                overlayEffect.AnnotationColorHSL = cv.HSLColor;
                overlayEffect.WorldViewProjMatrix = cv.ModelMatrix * worldViewProj;
                overlayEffect.InputLumaAlphaValue = 0f;

                foreach (EffectPass pass in overlayEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                            0,
                            0,
                            2);
                }
            }
        }


        static OverlayShaderEffect.Techniques GetTechnique(OverlayStyle style)
        {
            return style switch
            {
                OverlayStyle.Alpha => OverlayShaderEffect.Techniques.CircleSingleColorAlphaOverlayEffect,
                OverlayStyle.Luma => OverlayShaderEffect.Techniques.CircleSingleColorLumaOverlayEffect,
                _ => throw new NotImplementedException("GetTechnique: Unknown Overlay Style " + style.ToString()),
            };
        }


        public static void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            CircleView[] allCircles = [.. items.Select(i => i as CircleView).Where(i => i != null)];
            if (allCircles.Length == 0)
                return;

            var instancedEffect = VikingXNAGraphics.DeviceEffectsStore<CircleInstancedEffect>.TryGet(device);
            if (instancedEffect != null)
            {
                instancedEffect.SetTechnique(Overlay);
                Vector2 rtSize = new Vector2(device.Viewport.Width, device.Viewport.Height);
                Texture2D backgroundTexture = null;
                float inputLumaAlpha = 0f;
                var overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
                if (overlayEffect != null)
                {
                    if (Overlay == OverlayStyle.Luma)
                    {
                        inputLumaAlpha = overlayEffect.InputLumaAlphaValue;
                        if (overlayEffect.effect?.Parameters["BackgroundTexture"] != null)
                        {
                            try { backgroundTexture = overlayEffect.effect.Parameters["BackgroundTexture"].GetValueTexture2D(); } catch (Exception ex) { Trace.WriteLine($"CircleView: BackgroundTexture read failed: {ex}", "CircleView"); }
                        }
                    }
                }
                CircleView.Draw(device, scene, instancedEffect, allCircles, rtSize, backgroundTexture, inputLumaAlpha);
                return;
            }

            OverlayShaderEffect overlayEffectFallback = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (overlayEffectFallback != null)
            {
                overlayEffectFallback.Technique = GetTechnique(Overlay);
                CircleView.Draw(device, scene, overlayEffectFallback, allCircles);
            }
        }

        public virtual void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items) => CircleView.Draw(device, scene, Overlay, items);

        public virtual void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay) => CircleView.Draw(device, scene, Overlay, [this]);

        #endregion
    }
}
