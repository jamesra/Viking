using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public class TextureCircleView(Texture2D texture, Circle circle, Color color) : CircleView(circle, color)
    {
        public Texture2D Texture = texture;

        public static TextureCircleView CreateUpArrow(Circle circle, Color color) => new TextureCircleView(GlobalPrimitives.UpArrowTexture, circle, color);

        public static TextureCircleView CreateDownArrow(Circle circle, Color color)
        {
            TextureCircleView view = new(GlobalPrimitives.DownArrowTexture, circle, color);
            return view;
        }

        /// <summary>
        /// The border round a grey arrow marker (<see cref="ArrowBorderColorFor"/>): the outline color the label outline preference gives
        /// labels (white when it is off), so it stands out from the image as labels do. Read each time arrows are drawn, so it follows the preference.
        /// </summary>
        public static Color ArrowBorderColor()
        {
            LabelOutlineStyle style = LabelView.OutlineStyleAccessor();
            return LabelView.OutlineColors(Color.Black, style == LabelOutlineStyle.Off ? LabelOutlineStyle.BlackTextWhiteOutline : style).Outline;
        }

        /// <summary>Saturation (HSV) at and above which an arrow's color counts as saturated, so its border is the pale version.</summary>
        public const float SaturatedArrowThreshold = 0.5f;

        /// <summary>Below this saturation an arrow's color is grey and has no hue to borrow, so its border is <see cref="ArrowBorderColor"/>.</summary>
        public const float GreyArrowThreshold = 0.1f;

        /// <summary>Saturation of the pale border round a saturated arrow.</summary>
        public const float PaleBorderSaturation = 0.25f;

        /// <summary>
        /// The border of an arrow drawn in <paramref name="arrowColor"/>: the same hue at full brightness, fully saturated when the
        /// arrow's color is pale and pale (<see cref="PaleBorderSaturation"/>) when it is saturated, so the border differs from the
        /// fill while keeping its hue. A grey arrow has no hue and keeps the label-outline border <paramref name="greyBorder"/>.
        /// </summary>
        public static Color ArrowBorderColorFor(Color arrowColor, Color greyBorder)
        {
            float r = arrowColor.R / 255f;
            float g = arrowColor.G / 255f;
            float b = arrowColor.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float chroma = max - min;
            float saturation = max > 0 ? chroma / max : 0;
            if (saturation < GreyArrowThreshold)
                return greyBorder;

            float hue; // In sixths of a turn, 0 to 6
            if (max == r)
                hue = (g - b) / chroma;
            else if (max == g)
                hue = ((b - r) / chroma) + 2;
            else
                hue = ((r - g) / chroma) + 4;
            if (hue < 0)
                hue += 6;

            float borderSaturation = saturation >= SaturatedArrowThreshold ? PaleBorderSaturation : 1f;
            return FromHsv(hue, borderSaturation, 1f);
        }

        /// <summary>
        /// How far a colored arrow's luma is moved from the image under it (<see cref="ArrowFillLumaShift"/>): toward black by this fraction
        /// for a light cell color, toward white for a dark one.
        /// </summary>
        public const float ArrowLumaContrast = 0.40f;

        /// <summary>Perceptual luma (0 to 1) of a cell color at and above which its arrow is drawn darker than the cell, below it lighter.</summary>
        public const float ArrowLumaMid = 0.5f;

        /// <summary>
        /// True when an arrow in <paramref name="arrowColor"/> has a hue, so it is drawn without a border, lighter or darker than its cell
        /// (<see cref="ArrowFillLumaShift"/>). A grey arrow keeps its border.
        /// </summary>
        public static bool ArrowHasHue(Color arrowColor)
        {
            float max = Math.Max(arrowColor.R, Math.Max(arrowColor.G, arrowColor.B)) / 255f;
            float min = Math.Min(arrowColor.R, Math.Min(arrowColor.G, arrowColor.B)) / 255f;
            return max > 0 && (max - min) / max >= GreyArrowThreshold;
        }

        /// <summary>
        /// The <see cref="OverlayShaderEffect.MarkerFillLumaShift"/> of a colored arrow: the ring and arrow keep the cell's hue and the
        /// image's texture, with the luma moved <see cref="ArrowLumaContrast"/> toward black when the cell color is light
        /// (perceptual luma at or above <see cref="ArrowLumaMid"/>) and toward white when it is dark, so the marker stands apart from
        /// the cell it sits on without a border.
        /// </summary>
        public static float ArrowFillLumaShift(Color arrowColor)
        {
            float luma = ((0.3f * arrowColor.R) + (0.59f * arrowColor.G) + (0.11f * arrowColor.B)) / 255f;
            return luma >= ArrowLumaMid ? -ArrowLumaContrast : ArrowLumaContrast;
        }

        /// <summary>RGB of a color with <paramref name="hue"/> in sixths of a turn (0 to 6) and HSV saturation and value from 0 to 1.</summary>
        private static Color FromHsv(float hue, float saturation, float value)
        {
            float chroma = value * saturation;
            float x = chroma * (1 - Math.Abs((hue % 2) - 1));
            float m = value - chroma;
            (float r, float g, float b) = (int)hue switch
            {
                0 => (chroma, x, 0f),
                1 => (x, chroma, 0f),
                2 => (0f, chroma, x),
                3 => (0f, x, chroma),
                4 => (x, 0f, chroma),
                _ => (chroma, 0f, x),
            };
            return new Color(r + m, g + m, b + m);
        }

        /// <summary>
        /// The <see cref="MarkerOutlineMap"/> drawn in place of an arrow texture, so every arrow (overlapped link markers, adjacent
        /// locations, adjacent polyline points) gets the contrasting border; null for any other texture or before the host built them.
        /// </summary>
        private static Texture2D OutlinedArrow(Texture2D texture)
        {
            if (texture is null)
                return null;
            if (texture == GlobalPrimitives.UpArrowTexture)
                return GlobalPrimitives.UpArrowOutlinedTexture;
            if (texture == GlobalPrimitives.DownArrowTexture)
                return GlobalPrimitives.DownArrowOutlinedTexture;
            return null;
        }

        public static TextureCircleView CreatePlusCircle(Circle circle, Color color) => new TextureCircleView(GlobalPrimitives.PlusTexture, circle, color);

        public static TextureCircleView CreateMinusCircle(Circle circle, Color color) => new TextureCircleView(GlobalPrimitives.MinusTexture, circle, color);

        public static TextureCircleView CreateCircle(Circle circle, Color color)
        {
            TextureCircleView view = new(GlobalPrimitives.CircleTexture, circle, color);
            return view;
        }

        public static TextureCircleView CreateChainCircle(Circle circle, Color color)
        {
            TextureCircleView view = new(GlobalPrimitives.ChainTexture, circle, color);
            return view;
        }

        public override VertexPositionColorTexture[] BackgroundVerts
        {
            get
            {
                if (_BackgroundVerts is null)
                {
                    _BackgroundVerts = CircleView.VerticiesForCircle(this.Circle);
                    for (int i = 0; i < _BackgroundVerts.Length; i++)
                    {
                        _BackgroundVerts[i].Color = Color;
                    }
                }

                return _BackgroundVerts;
            }
        }


        public new static void SetupGraphicsDevice(GraphicsDevice device, BasicEffect basicEffect)
        {
            //Note one still needs to set the texture for the effect before rendering after calling this method
            //DeviceStateManager.SaveDeviceState(device);
            //DeviceStateManager.SetRenderStateForShapes(device);
            //DeviceStateManager.SetRasterizerStateForShapes(device);

            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;

        }


        public new static void SetupGraphicsDevice(GraphicsDevice device, OverlayShaderEffect overlayEffect)
        {
            //Note one still needs to set the texture for the effect before rendering after calling this method
            //DeviceStateManager.SaveDeviceState(device);
            //DeviceStateManager.SetRenderStateForShapes(device);
            //DeviceStateManager.SetRasterizerStateForShapes(device);
        }


        public new static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            //DeviceStateManager.RestoreDeviceState(graphicsDevice);

            basicEffect.Texture = null;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        public new static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, OverlayShaderEffect overlayEffect)
        {
            //DeviceStateManager.RestoreDeviceState(graphicsDevice); 
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          BasicEffect basicEffect,
                          TextureCircleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            TextureCircleView.SetupGraphicsDevice(device, basicEffect);

            var textureGroups = listToDraw.GroupBy(l => l.Texture);
            foreach (var textureGroup in textureGroups)
            {
                TextureCircleView[] views = [.. textureGroup];
                basicEffect.Texture = textureGroup.Key;
                basicEffect.TextureEnabled = true;

                VertexPositionColorTexture[] VertArray = AggregatePrimitives(views, out int[] indicies, out int vertexCount, out int indexCount);

                if (vertexCount > 0 && indexCount > 0)
                {
                    using (VertexBuffer vb = new VertexBuffer(device, typeof(VertexPositionColorTexture), vertexCount, BufferUsage.None))
                    using (IndexBuffer ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indexCount, BufferUsage.None))
                    {
                        vb.SetData(VertArray, 0, vertexCount);
                        ib.SetData(indicies, 0, indexCount);

                        foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                        {
                            pass.Apply();

                            device.SetVertexBuffer(vb);
                            device.Indices = ib;
                            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexCount / 3);
                        }
                    }
                }
            }

            TextureCircleView.RestoreGraphicsDevice(device, basicEffect);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayShaderEffect overlayEffect,
                          TextureCircleView[] listToDraw,
                          bool opacityAsIfDrawnTwice = false)
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

                // An arrow is drawn from its outlined map; an older compiled shader without the technique draws it plain
                Texture2D outlined = OutlinedArrow(textureGroup.Key);
                bool drawOutlined = outlined is not null && overlayEffect.HasTechnique(OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOutlinedOverlayEffect);
                Color greyBorder = ArrowBorderColor();
                if (drawOutlined)
                {
                    overlayEffect.AnnotationTexture = outlined;
                    overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOutlinedOverlayEffect;
                }
                else
                {
                    overlayEffect.AnnotationTexture = textureGroup.Key;
                    overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOverlayEffect;
                }

                foreach (TextureCircleView cv in textureGroup)
                {
                    // A colored arrow has no border and is drawn lighter or darker than its cell; a grey arrow keeps the label-outline border
                    if (drawOutlined)
                    {
                        if (ArrowHasHue(cv.Color))
                        {
                            overlayEffect.SetMarkerBorder(Color.Black, 0f, LabelOutlineEffect.OutlineBlur);
                            overlayEffect.MarkerFillLumaShift = ArrowFillLumaShift(cv.Color);
                        }
                        else
                        {
                            overlayEffect.SetMarkerBorder(ArrowBorderColorFor(cv.Color, greyBorder), LabelOutlineEffect.OutlineWidth, LabelOutlineEffect.OutlineBlur);
                            overlayEffect.MarkerFillLumaShift = 0f;
                        }
                    }

                    overlayEffect.AnnotationColorHSL = opacityAsIfDrawnTwice ? WithOpacityAsIfDrawnTwice(cv.HSLColor) : cv.HSLColor;
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

        /// <summary>
        /// <paramref name="color"/> with the opacity two overlapping draws of it give, 1 - (1 - a)^2. The markers drawn
        /// through <see cref="CircleView.Draw(GraphicsDevice, IScene, OverlayStyle, IRenderable[])"/> (adjacent-section
        /// arrows, overlapped-link markers, pen action buttons) had their look tuned when that path drew them twice.
        /// </summary>
        internal static Color WithOpacityAsIfDrawnTwice(Color color)
        {
            float a = color.A / 255f;
            color.A = (byte)Math.Round(255f * a * (2f - a));
            return color;
        }

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            switch (Overlay)
            {
                case OverlayStyle.Alpha:
                    BasicEffect effect = CircleView.GetOrCreateBasicEffect(device);
                    if (effect != null)
                    {
                        effect.World = scene.World;
                        effect.View = scene.View;
                        effect.Projection = scene.Projection;
                        TextureCircleView.Draw(device, scene, effect, new CircleView[] { this });
                    }
                    break;
                case OverlayStyle.Luma:
                    OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
                    TextureCircleView.Draw(device, scene, overlayEffect, new CircleView[] { this });
                    break;
            }
        }
    }


    /// <summary>
    /// A circle drawn by the procedural <see cref="OverlayShaderEffect.Techniques.CircleRingOverlayEffect"/>:
    /// a band over the outer quarter of the radius (dark over bright tissue, light over dark) that fades to clear over the next eighth, so the image
    /// under a polygon vertex stays visible. Needs no texture. Drawn through <see cref="OverlayShaderEffect"/>
    /// by the overlay-effect overload of <see cref="CircleView.Draw(GraphicsDevice, IScene, OverlayShaderEffect, CircleView[])"/>
    /// whatever the overlay style; the BasicEffect overload falls back to a flat disc.
    /// </summary>
    public class RingCircleView(Circle circle, Color color) : CircleView(circle, color)
    {
        /// <summary>
        /// Overrides <see cref="OverlayShaderEffect.RingSolidStart"/> (radius fraction where the opaque band begins) for
        /// this ring only. Null keeps the effect's value, which polygon vertices use.
        /// </summary>
        public float? SolidStart { get; set; }

        /// <summary>Overrides <see cref="OverlayShaderEffect.RingFadeStart"/> for this ring only. Null keeps the effect's value.</summary>
        public float? FadeStart { get; set; }

        /// <summary>
        /// Draws the rings with <paramref name="overlayEffect"/>. Changes the effect's technique and the device's
        /// vertex/index buffers; call on the render thread.
        /// </summary>
        public static void Draw(GraphicsDevice device, IScene scene, OverlayShaderEffect overlayEffect, RingCircleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            device.Indices = GlobalPrimitives.GetUnitCircleIndexBuffer(device);
            device.SetVertexBuffer(GlobalPrimitives.GetUnitCircleVertexBuffer(device));
            overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleRingOverlayEffect;

            // The band parameters are shared effect state, so put back what the polygon vertices expect.
            float defaultSolidStart = overlayEffect.RingSolidStart;
            float defaultFadeStart = overlayEffect.RingFadeStart;
            try
            {
                Matrix worldViewProj = scene.World * scene.ViewProj;
                foreach (RingCircleView cv in listToDraw)
                {
                    overlayEffect.RingSolidStart = cv.SolidStart ?? defaultSolidStart;
                    overlayEffect.RingFadeStart = cv.FadeStart ?? defaultFadeStart;
                    overlayEffect.AnnotationColorHSL = cv.HSLColor;
                    overlayEffect.WorldViewProjMatrix = cv.ModelMatrix * worldViewProj;

                    foreach (EffectPass pass in overlayEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
                    }
                }
            }
            finally
            {
                overlayEffect.RingSolidStart = defaultSolidStart;
                overlayEffect.RingFadeStart = defaultFadeStart;
            }
        }
    }


    public class CircleView : IColorView, IViewPosition2D, IRenderable
    {
        #region static

        private static readonly object BasicEffectCacheLock = new();
        private static readonly Dictionary<GraphicsDevice, BasicEffect> BasicEffectCache = [];
        private static readonly object BufferCacheLock = new();
        private static readonly Dictionary<GraphicsDevice, (VertexBuffer vb, IndexBuffer ib, int vertexCapacity, int indexCapacity)> BufferCache = [];

        internal static BasicEffect GetOrCreateBasicEffect(GraphicsDevice device)
        {
            if (device == null || device.IsDisposed)
                return null;
            lock (BasicEffectCacheLock)
            {
                if (BasicEffectCache.TryGetValue(device, out var effect) && effect != null && !effect.IsDisposed)
                    return effect;
                BasicEffectCache.Remove(device);
                var newEffect = new BasicEffect(device);
                BasicEffectCache[device] = newEffect;
                return newEffect;
            }
        }

        /// <summary>
        /// Drops cached BasicEffect and circle GPU buffers after a device reset.
        /// Called from GraphicsDeviceService.ClearDeviceDependentCaches.
        /// </summary>
        public static void ClearDeviceDependentCaches()
        {
            lock (BasicEffectCacheLock)
            {
                BasicEffectCache.Clear();
            }
            lock (BufferCacheLock)
            {
                BufferCache.Clear();
            }
        }

        private static void GetOrCreateCircleBuffers(GraphicsDevice device, int vertexCount, int indexCount,
            out VertexBuffer vb, out IndexBuffer ib)
        {
            vb = null;
            ib = null;
            if (device == null || device.IsDisposed || vertexCount <= 0 || indexCount <= 0)
                return;
            lock (BufferCacheLock)
            {
                if (BufferCache.TryGetValue(device, out var cached) && cached.vb != null && !cached.vb.IsDisposed
                    && cached.vertexCapacity >= vertexCount && cached.indexCapacity >= indexCount)
                {
                    vb = cached.vb;
                    ib = cached.ib;
                    return;
                }
                cached.vb?.Dispose();
                cached.ib?.Dispose();
                vb = new VertexBuffer(device, typeof(VertexPositionColorTexture), Math.Max(vertexCount, 256), BufferUsage.None);
                ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, Math.Max(indexCount, 384), BufferUsage.None);
                BufferCache[device] = (vb, ib, vb.VertexCount, ib.IndexCount);
            }
        }

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
        public static bool IsCircleVisible(double radius, Geometry.Vector2 center, VikingXNA.Scene scene)
        {
            Circle circle = new(center, radius);

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


        private Circle _Circle;
        public Circle Circle
        {
            get => _Circle;
            set
            {
                ClearCachedData();
                _Circle = value;
                UpdateModelMatrix();
            }
        }

        public Geometry.Vector2 VolumePosition => _Circle.Center;

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
        public void ClearCachedData() => _BackgroundVerts = null;

        /// <summary>
        /// Return true if the circle would be visible if rendered into the scene
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool IsVisible(VikingXNA.Scene scene) => IsCircleVisible(Radius, VolumePosition, scene);

        public CircleView(Circle circle, Color color)
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

        /// <summary>
        /// Create billboard primitive the size and position of the circle
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="Verts"></param>
        /// <returns></returns>
        protected static VertexPositionColorTexture[] VerticiesForCircle(Circle circle)
        {
            VertexPositionColorTexture[] Verts = new VertexPositionColorTexture[GlobalPrimitives.SquareVerts.Length];
            GlobalPrimitives.SquareVerts.CopyTo(Verts, 0);

            for (int i = 0; i < Verts.Length; i++)
            {
                Verts[i].Position *= (float)circle.Radius;
                Verts[i].Position.X += (float)circle.Center.X;
                Verts[i].Position.Y += (float)circle.Center.Y;
            }

            return Verts;
        }

        protected VertexPositionColorTexture[] _BackgroundVerts = null;
        public virtual VertexPositionColorTexture[] BackgroundVerts
        {
            get
            {
                _BackgroundVerts ??= VerticiesForCircle(this.Circle);

                return _BackgroundVerts;
            }
        }

        Geometry.Vector2 IViewPosition2D.Position
        {
            get => this.VolumePosition;

            set => Circle = new Circle(value, this.Radius);
        }

        /// <summary>
        /// The verticies should really be cached and handed up to LocationObjRenderer so all similiar objects can be rendered in one
        /// call.  This method is in the middle of a change from using triangles to draw circles to using textures. 
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="DirectionToVisiblePlane"></param>
        /// <param name="color"></param>
        public VertexPositionColorTexture[] GetCircleBackgroundVerts(Microsoft.Xna.Framework.Color HSLColor, out int[] indicies)
        {
            //            Geometry.Vector2 Pos = this.VolumePosition;

            //Can't populate until we've referenced CircleVerts
            indicies = GlobalPrimitives.SquareIndicies;
            //            float radius = (float)this.Radius;

            VertexPositionColorTexture[] verts = BackgroundVerts;

            float SatScalar = HSLColor.B / 255f;

            //Draw an opaque border around the background
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Color = HSLColor;
                //verts[i].Color.G = (byte)((((float)HSLColor.G / 255f) * SatScalar) * 255); // This line restores the nice luma blending effect I had pre-curce annotations
            }

            return verts;
        }

        public static void SetupGraphicsDevice(GraphicsDevice device, BasicEffect basicEffect)
        {
            DeviceStateManager.SaveDeviceState(device);
            /*DeviceStateManager.SetRenderStateForShapes(device);
            DeviceStateManager.SetRasterizerStateForShapes(device);
            */

            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;
        }


        public static void SetupGraphicsDevice(GraphicsDevice device, OverlayShaderEffect overlayEffect)
        {
            DeviceStateManager.SaveDeviceState(device);

            if (overlayEffect != null)
            {
                //overlayEffect.AnnotateWithCircle((float)0.05, 0.5f);
            }
        }

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            DeviceStateManager.RestoreDeviceState(graphicsDevice);

            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, OverlayShaderEffect overlayEffect) => DeviceStateManager.RestoreDeviceState(graphicsDevice);

        protected static VertexPositionColorTexture[] AggregatePrimitives(CircleView[] listToDraw, out int[] indicies, out int vertexCount, out int indexCount)
        {
            VertexPositionColorTexture[] VertArray = new VertexPositionColorTexture[listToDraw.Length * 4];
            indicies = new int[listToDraw.Length * 6];

            int iNextVert = 0;
            int iNextVertIndex = 0;

            for (int iObj = 0; iObj < listToDraw.Length; iObj++)
            {
                CircleView locToDraw = listToDraw[iObj];
                if (locToDraw is null)
                    continue;

                VertexPositionColorTexture[] objVerts = locToDraw.GetCircleBackgroundVerts(locToDraw.HSLColor, out int[] locIndicies);

                if (objVerts is null)
                    continue;

                Array.Copy(objVerts, 0, VertArray, iNextVert, objVerts.Length);

                for (int iVert = 0; iVert < locIndicies.Length; iVert++)
                {
                    indicies[iNextVertIndex + iVert] = locIndicies[iVert] + iNextVert;
                }

                iNextVert += objVerts.Length;
                iNextVertIndex += locIndicies.Length;
            }

            vertexCount = iNextVert;
            indexCount = iNextVertIndex;
            return VertArray;
        }


        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          BasicEffect basicEffect,
                          CircleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            //Draw textured circles in the array
            TextureCircleView[] arrayTextureCircles = [.. listToDraw.Select(c => c as TextureCircleView).Where(c => c as TextureCircleView != null)];
            TextureCircleView.Draw(device, scene, basicEffect, arrayTextureCircles);

            //Draw untextured circles in the array
            listToDraw = [.. listToDraw.Where(c => c as TextureCircleView is null)];
            if (listToDraw.Length == 0)
                return;

            CircleView.SetupGraphicsDevice(device, basicEffect);
            VertexPositionColorTexture[] VertArray = AggregatePrimitives(listToDraw, out int[] indicies, out int vertexCount, out int indexCount);

            if (vertexCount > 0 && indexCount > 0)
            {
                GetOrCreateCircleBuffers(device, vertexCount, indexCount, out VertexBuffer vb, out IndexBuffer ib);
                if (vb != null && ib != null)
                {
                    vb.SetData(VertArray, 0, vertexCount);
                    ib.SetData(indicies, 0, indexCount);

                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.SetVertexBuffer(vb);
                        device.Indices = ib;
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexCount / 3);
                    }
                }
            }

            CircleView.RestoreGraphicsDevice(device, basicEffect);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayShaderEffect overlayEffect,
                          CircleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            // Rings switch the effect technique, so they are drawn last.
            RingCircleView[] arrayRingCircles = [.. listToDraw.OfType<RingCircleView>()];
            listToDraw = [.. listToDraw.Where(c => c is not RingCircleView)];

            device.Indices = GlobalPrimitives.GetUnitCircleIndexBuffer(device);
            device.SetVertexBuffer(GlobalPrimitives.GetUnitCircleVertexBuffer(device));

            //Draw textured circles in the array. They switch the technique, so the caller's is put back for the solid circles.
            TextureCircleView[] arrayTextureCircles = [.. listToDraw.OfType<TextureCircleView>()];
            if (arrayTextureCircles.Length > 0)
            {
                EffectTechnique solidTechnique = overlayEffect.CurrentTechnique;
                TextureCircleView.Draw(device, scene, overlayEffect, arrayTextureCircles);
                overlayEffect.CurrentTechnique = solidTechnique;
                device.Indices = GlobalPrimitives.GetUnitCircleIndexBuffer(device);
                device.SetVertexBuffer(GlobalPrimitives.GetUnitCircleVertexBuffer(device));
            }

            //Draw untextured circles in the array
            CircleView[] arraySolidCircles = arrayTextureCircles.Length == 0 ? listToDraw : [.. listToDraw.Where(c => c is not TextureCircleView)];
            if (arraySolidCircles.Length == 0)
            {
                RingCircleView.Draw(device, scene, overlayEffect, arrayRingCircles);
                return;
            }

            //CircleView.SetupGraphicsDevice(device, overlayEffect);
            //overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorAlphaOverlayEffect;

            Matrix worldViewProj = scene.World * scene.ViewProj;
            foreach (CircleView cv in arraySolidCircles)
            {
                //overlayEffect.AnnotationColorHSL = cv.HSLColor.SetAlpha(0.5f);
                overlayEffect.AnnotationColorHSL = cv.HSLColor;
                overlayEffect.WorldViewProjMatrix = cv.ModelMatrix * worldViewProj;
                overlayEffect.InputLumaAlphaValue = 0f;

                //int[] indicies;
                //VertexPositionColorTexture[] VertArray = AggregatePrimitives(listToDraw, out indicies);

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

            RingCircleView.Draw(device, scene, overlayEffect, arrayRingCircles);
            //CircleView.RestoreGraphicsDevice(device, overlayEffect);
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
            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            overlayEffect.Technique = GetTechnique(Overlay);

            CircleView[] circles = [.. items.OfType<CircleView>()];
            TextureCircleView[] textured = [.. circles.OfType<TextureCircleView>()];
            if (textured.Length > 0)
            {
                device.Indices = GlobalPrimitives.GetUnitCircleIndexBuffer(device);
                device.SetVertexBuffer(GlobalPrimitives.GetUnitCircleVertexBuffer(device));
                TextureCircleView.Draw(device, scene, overlayEffect, textured, opacityAsIfDrawnTwice: true);
                overlayEffect.Technique = GetTechnique(Overlay);
                circles = [.. circles.Where(c => c is not TextureCircleView)];
            }

            CircleView.Draw(device, scene, overlayEffect, circles);
        }

        public virtual void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items) => CircleView.Draw(device, scene, Overlay, items);

        public virtual void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay) => CircleView.Draw(device, scene, Overlay, [this]);

        #endregion
    }
}
