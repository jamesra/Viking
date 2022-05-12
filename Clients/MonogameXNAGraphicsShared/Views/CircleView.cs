using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class TextureCircleView : CircleView
    {
        public Texture2D Texture;
        //bool FlipTexture = false;

        public TextureCircleView(Texture2D texture, GridCircle circle, Color color) : base(circle, color)
        {
            this.Texture = texture;
        }

        public static TextureCircleView CreateUpArrow(GridCircle circle, Color color)
        {
            return new TextureCircleView(GlobalPrimitives.UpArrowTexture, circle, color);
        }

        public static TextureCircleView CreateDownArrow(GridCircle circle, Color color)
        {
            TextureCircleView view = new TextureCircleView(GlobalPrimitives.DownArrowTexture, circle, color);
            return view;
        }

        public static TextureCircleView CreatePlusCircle(GridCircle circle, Color color)
        {
            return new TextureCircleView(GlobalPrimitives.PlusTexture, circle, color);
        }

        public static TextureCircleView CreateMinusCircle(GridCircle circle, Color color)
        {
            return new TextureCircleView(GlobalPrimitives.MinusTexture, circle, color);
        }

        public static TextureCircleView CreateCircle(GridCircle circle, Color color)
        {
            TextureCircleView view = new TextureCircleView(GlobalPrimitives.CircleTexture, circle, color);
            return view;
        }

        public static TextureCircleView CreateChainCircle(GridCircle circle, Color color)
        {
            TextureCircleView view = new TextureCircleView(GlobalPrimitives.ChainTexture, circle, color);
            return view;
        }

        public override VertexPositionColorTexture[] BackgroundVerts
        {
            get
            {
                if (_BackgroundVerts == null)
                {
                    _BackgroundVerts = CircleView.VerticiesForCircle(this.Circle);
                    for(int i = 0; i < _BackgroundVerts.Length; i++)
                    {
                        _BackgroundVerts[i].Color = Color;
                    }
                }

                return _BackgroundVerts;
            }
        }


        public static void SetupGraphicsDevice(GraphicsDevice device, BasicEffect basicEffect)
        {
            //Note one still needs to set the texture for the effect before rendering after calling this method
            //DeviceStateManager.SaveDeviceState(device);
            //DeviceStateManager.SetRenderStateForShapes(device);
            //DeviceStateManager.SetRasterizerStateForShapes(device);

            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;
            
        }

        
        public static void SetupGraphicsDevice(GraphicsDevice device, OverlayShaderEffect overlayEffect)
        {
            //Note one still needs to set the texture for the effect before rendering after calling this method
            //DeviceStateManager.SaveDeviceState(device);
            //DeviceStateManager.SetRenderStateForShapes(device);
            //DeviceStateManager.SetRasterizerStateForShapes(device);
        }
        

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            //DeviceStateManager.RestoreDeviceState(graphicsDevice);

            basicEffect.Texture = null;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, OverlayShaderEffect overlayEffect)
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
            foreach(var textureGroup in textureGroups)
            {
                TextureCircleView[] views = textureGroup.ToArray();
                basicEffect.Texture = textureGroup.Key;
                basicEffect.TextureEnabled = true;
                
                int[] indicies;
                VertexPositionColorTexture[] VertArray = AggregatePrimitives(views, out indicies);

                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                                         VertArray,
                                                                                         0,
                                                                                         VertArray.Length,
                                                                                         indicies,
                                                                                         0,
                                                                                         indicies.Length / 3);
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
                TextureCircleView[] views = textureGroup.ToArray();
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
                            6,
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
                    BasicEffect effect = new BasicEffect(device);
                    effect.World = scene.World;
                    effect.View = scene.View;
                    effect.Projection = scene.Projection;
                    TextureCircleView.Draw(device, scene, effect, new CircleView[] { this });
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

        //static double BeginFadeCutoff = 0.1;
        static double InvisibleCutoff = 1.5f;

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
                _HSLColor = value.ConvertToHSL();
                ClearCachedData();
            }
        }

        private Microsoft.Xna.Framework.Color _HSLColor;
        public Microsoft.Xna.Framework.Color HSLColor => _HSLColor;

        /// <summary>
        /// Called when we have changed a property that affects rendering
        /// </summary>
        public void ClearCachedData()
        {
            _BackgroundVerts = null;            
        }

        /// <summary>
        /// Return true if the circle would be visible if rendered into the scene
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool IsVisible(VikingXNA.Scene scene)
        {
            if (!scene.VisibleWorldBounds.Intersects(this.Circle))
                return false; 

            double maxDimension = Math.Max(scene.VisibleWorldBounds.Width, scene.VisibleWorldBounds.Height);
            double LocToScreenRatio = Radius * 2.0 / maxDimension;
            if (LocToScreenRatio > InvisibleCutoff)
                return false;

            double maxPixelDimension = Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight);
            if (Radius * 2.0 <= maxPixelDimension)
                return false;

            return true;
        }
        
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
                if (_BackgroundVerts == null)
                {
                    _BackgroundVerts = VerticiesForCircle(this.Circle);
                }

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

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, OverlayShaderEffect overlayEffect)
        {
            DeviceStateManager.RestoreDeviceState(graphicsDevice); 
        }

        protected static VertexPositionColorTexture[] AggregatePrimitives(CircleView[] listToDraw, out int[] indicies)
        {
            VertexPositionColorTexture[] VertArray = new VertexPositionColorTexture[listToDraw.Length * 4];
            indicies = new int[listToDraw.Length * 6];

            int iNextVert = 0;
            int iNextVertIndex = 0;

            for (int iObj = 0; iObj < listToDraw.Length; iObj++)
            {
                CircleView locToDraw = listToDraw[iObj];
                if (locToDraw == null)
                    continue; 

                int[] locIndicies;
                VertexPositionColorTexture[] objVerts = locToDraw.GetCircleBackgroundVerts(locToDraw.HSLColor, out locIndicies);

                if (objVerts == null)
                    continue;

                Array.Copy(objVerts, 0, VertArray, iNextVert, objVerts.Length);

                for (int iVert = 0; iVert < locIndicies.Length; iVert++)
                {
                    indicies[iNextVertIndex + iVert] = locIndicies[iVert] + iNextVert;
                }

                iNextVert += objVerts.Length;
                iNextVertIndex += locIndicies.Length;
            }

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
            TextureCircleView[] arrayTextureCircles = listToDraw.Select(c => c as TextureCircleView).Where(c => c as TextureCircleView != null).ToArray();
            TextureCircleView.Draw(device, scene, basicEffect, arrayTextureCircles);

            //Draw untextured circles in the array
            listToDraw = listToDraw.Where(c => c as TextureCircleView == null).ToArray();
            if (listToDraw.Length == 0)
                return;

            CircleView.SetupGraphicsDevice(device, basicEffect);
            int[] indicies;
            VertexPositionColorTexture[] VertArray = AggregatePrimitives(listToDraw, out indicies);

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                device.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                                     VertArray,
                                                                                     0,
                                                                                     VertArray.Length,
                                                                                     indicies,
                                                                                     0,
                                                                                     indicies.Length / 3);
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
            TextureCircleView[] arrayTextureCircles = listToDraw.Select(c => c as TextureCircleView).Where(c => c as TextureCircleView != null).ToArray();
            TextureCircleView.Draw(device, scene, overlayEffect, arrayTextureCircles);

            //Draw untextured circles in the array
            listToDraw = listToDraw.Where(c => c as TextureCircleView == null).ToArray();
            if (listToDraw.Length == 0)
                return;

            CircleView[] arraySolidCircles = listToDraw.Where(c => c as TextureCircleView == null).ToArray();

            //CircleView.SetupGraphicsDevice(device, overlayEffect);
            //overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorAlphaOverlayEffect;
            
            foreach (CircleView cv in arraySolidCircles)
            {
                overlayEffect.AnnotationColorHSL = cv.HSLColor.SetAlpha(0.5f);
                overlayEffect.WorldViewProjMatrix = (cv.ModelMatrix * scene.World) * scene.ViewProj;
                overlayEffect.InputLumaAlphaValue = 0f;

                //int[] indicies;
                //VertexPositionColorTexture[] VertArray = AggregatePrimitives(listToDraw, out indicies);

                foreach (EffectPass pass in overlayEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                            0,
                            0,
                            6,
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

            CircleView.RestoreGraphicsDevice(device, overlayEffect);
        }


        public static void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            overlayEffect.Technique = Overlay == OverlayStyle.Alpha ? OverlayShaderEffect.Techniques.CircleSingleColorAlphaOverlayEffect :
                OverlayShaderEffect.Techniques.CircleSingleColorLumaOverlayEffect;

            CircleView.Draw(device, scene, overlayEffect, items.Select(i => i as CircleView).Where(i => i != null).ToArray());
            TextureCircleView.Draw(device, scene, overlayEffect, items.Select(i => i as TextureCircleView).Where(i => i != null).ToArray());
        }

        public virtual void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            CircleView.Draw(device, scene, Overlay, items);
        }

        public virtual void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            CircleView.Draw(device, scene, Overlay, new IRenderable[] { this });
        }

        #endregion
    }
}
