using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class TextureCircleView(Texture2D texture, GridCircle circle, Color color) : CircleView(circle, color)
    {
        public Texture2D Texture = texture;

        public static TextureCircleView CreateUpArrow(GridCircle circle, Color color) => new TextureCircleView(GlobalPrimitives.UpArrowTexture, circle, color);

        public static TextureCircleView CreateDownArrow(GridCircle circle, Color color)
        {
            TextureCircleView view = new(GlobalPrimitives.DownArrowTexture, circle, color);
            return view;
        }

        public static TextureCircleView CreatePlusCircle(GridCircle circle, Color color) => new TextureCircleView(GlobalPrimitives.PlusTexture, circle, color);

        public static TextureCircleView CreateMinusCircle(GridCircle circle, Color color) => new TextureCircleView(GlobalPrimitives.MinusTexture, circle, color);

        public static TextureCircleView CreateCircle(GridCircle circle, Color color)
        {
            TextureCircleView view = new(GlobalPrimitives.CircleTexture, circle, color);
            return view;
        }

        public static TextureCircleView CreateChainCircle(GridCircle circle, Color color)
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
        public void ClearCachedData() => _BackgroundVerts = null;

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

        /// <summary>
        /// Create billboard primitive the size and position of the circle
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="Verts"></param>
        /// <returns></returns>
        protected static VertexPositionColorTexture[] VerticiesForCircle(GridCircle circle)
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

        GridVector2 IViewPosition2D.Position
        {
            get => this.VolumePosition;

            set => Circle = new GridCircle(value, this.Radius);
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
            //            GridVector2 Pos = this.VolumePosition;

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
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertexCount, 0, indexCount / 3);
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

            CircleView.Draw(device, scene, overlayEffect, [.. items.Select(i => i as CircleView).Where(i => i != null)]);
            TextureCircleView.Draw(device, scene, overlayEffect, [.. items.Select(i => i as TextureCircleView).Where(i => i != null)]);
        }

        public virtual void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items) => CircleView.Draw(device, scene, Overlay, items);

        public virtual void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay) => CircleView.Draw(device, scene, Overlay, [this]);

        #endregion
    }
}
