using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using VikingXNA;
using Geometry;
using RoundCurve;
using System.ComponentModel;
using System.Drawing.Imaging; 

namespace VikingXNAWinForms
{
    public class ViewerControl : GraphicsDeviceControl
    {
        public RoundLineCode.RoundLineManager LineManager
        {
            get
            {
                return DeviceEffectsStore<RoundLineCode.RoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
            }
            
        }
        
        public RoundLineCode.LumaOverlayRoundLineManager LumaOverlayLineManager
        {
            get
            {
                return DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
            }
        }

        public RoundCurve.CurveManager CurveManager
        {
            get
            {
                return DeviceEffectsStore<CurveManager>.GetOrCreateForDevice(this.Device, this.Content);
            }
        }

        public RoundCurve.CurveManagerHSV LumaOverlayCurveManager
        {
            get
            {
                return DeviceEffectsStore<CurveManagerHSV>.GetOrCreateForDevice(this.Device, this.Content);
            }
        }

        public PolygonOverlayEffect PolygonOverlayEffect
        {
            get
            {
                return DeviceEffectsStore<PolygonOverlayEffect>.GetOrCreateForDevice(this.Device, this.Content);
            }
        }

        public AnnotationOverBackgroundLumaEffect AnnotationOverlayEffect
        {
            get
            {
                return DeviceEffectsStore<AnnotationOverBackgroundLumaEffect>.GetOrCreateForDevice(this.Device, this.Content);
            }
        } 

        public BasicEffect basicEffect;

        public TileLayoutEffect tileLayoutEffect;
        public MergeHSVImagesEffect mergeHSVImagesEffect;
        public ChannelOverlayEffect channelOverlayEffect; 

        public readonly uint MaxTextureWidth = 4096;
        public readonly uint MaxTextureHeight = 4096; 

        public Camera Camera = new Camera();

        #region Fonts

        public Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch = null;
        public Microsoft.Xna.Framework.Graphics.SpriteFont fontArial = null;

        static Dictionary<string, Vector2> LabelToSize = new Dictionary<string, Vector2>(); 

        public Vector2 GetLabelSize(SpriteFont font, string label)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            //Label can't be empty or the offset measured is zero
            if (String.IsNullOrEmpty(label))
                label = " ";

            if (LabelToSize.ContainsKey(label))
                return LabelToSize[label];
            
            LabelToSize[label] = font.MeasureString(label);

            return LabelToSize[label];
        }

        #endregion

        private Scene _scene;
        /// <summary>
        /// Combination of the viewport and a camera used to draw this control
        /// </summary>
        public Scene Scene
        {
            get { return _scene; }
            set
            {
                if (_scene == value)
                    return;

                if (_scene != null)
                    _scene.OnSceneChanged -= this.OnSceneChanged;

                _scene = value;

                _scene.OnSceneChanged += this.OnSceneChanged;
            }
        }

        protected virtual void OnSceneChanged(object sender, PropertyChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private Matrix worldMatrix
        {
            get
            {
                return Matrix.Identity;
            }
        }     

        /// <summary>
        /// The current world view projection matrix for the camera
        /// </summary>
        public Matrix WVPMatrix;

        /// <summary>
        /// When set to true we do not wait for all textures to load before drawing the screen
        /// </summary>
        public bool AsynchTextureLoad = true;
        
        public static float MaxImageDimension
        {
            get
            {
                return 1000000;
            }
        }

        /// <summary>
        /// Initializes the transforms used for the 3D model.
        /// </summary>
        private void InitializeTransform()
        {
            
            this.Scene = new VikingXNA.Scene(Device.Viewport, this.Camera); 

            // Use the world matrix to tilt the cube along x and y axes.
//            worldMatrix = Matrix.Identity; // CreateRotationX(_CameraTilt) * Matrix.CreateRotationZ(_CameraPan);

  //          projectionMatrix = Matrix.CreateOrthographic((float)ProjectedArea.Width, (float)ProjectedArea.Height, MinDrawDistance, MaxDrawDistance);
            
        
        }

        /// <summary>
        /// Initializes the basic effect (parameter setting and technique selection)
        /// used for the 3D model.
        /// </summary>
        private void InitializeEffect()
        {
            basicEffect = new BasicEffect(Device);
         //   basicEffect.DiffuseColor = new Vector3(0.1f, 0.1f, 0.1f);
         //   basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
         //   basicEffect.SpecularPower = 5.0f;
            basicEffect.AmbientLightColor = new Vector3(1f, 1f, 1f);
            
            Matrix WorldViewProj = Scene.WorldViewProj; 

            Effect effectTileLayout = Content.Load<Effect>("TileLayout");
            this.tileLayoutEffect = new TileLayoutEffect(effectTileLayout);
            this.tileLayoutEffect.WorldViewProjMatrix = WorldViewProj;

            Effect effectHSVMerge = Content.Load<Effect>("MergeHSVImages");
            this.mergeHSVImagesEffect = new MergeHSVImagesEffect(effectHSVMerge);
            this.mergeHSVImagesEffect.WorldViewProjMatrix = WorldViewProj;

            Effect effectChannelOverlay = Content.Load<Effect>("ChannelOverlayShader");
            this.channelOverlayEffect = new ChannelOverlayEffect(effectChannelOverlay);
            this.channelOverlayEffect.WorldViewProjMatrix = WorldViewProj;

        }
        
        public ViewerControl() : base()
        {
            InitializeComponent();
        }
        /// <summary>
        /// Objects used to render screenshots
        /// </summary>
        RenderTarget2D ScreenshotRenderTarget;

        protected override void Initialize()
        {
            if (!DesignMode)
            {
               
                //vertexDeclaration = VertexPositionNormalTexture.VertexDeclaration;

                InitializeTransform();
                InitializeEffect();


                DeviceEffectsStore<RoundLineCode.RoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
                DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
                DeviceEffectsStore<RoundCurve.CurveManager>.GetOrCreateForDevice(this.Device, this.Content);
                DeviceEffectsStore<RoundCurve.CurveManagerHSV>.GetOrCreateForDevice(this.Device, this.Content);
                 
            }
        }
        
        public Geometry.GridRectangle RenderTargetBounds()
        {
            if (Device == null)
                return new GridRectangle(0, 0, 10, 10);

            //For debugging
            const int offset = 0;

            //GridVector2 TopLeft = ScreenToWorld(offset, offset);
            //GridVector2 BottomLeft = ScreenToWorld(offset, GraphicsDevice.Viewport.Height - offset);
            //GridVector2 TopRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, offset);

            GridVector2 BottomLeft = Scene.ScreenToWorld(offset, Device.Viewport.Height - offset);
            GridVector2 TopRight = Scene.ScreenToWorld(Device.Viewport.Width - offset, offset);
            GridRectangle rect = new GridRectangle(BottomLeft, TopRight.X - BottomLeft.X, TopRight.Y - BottomLeft.Y);
            return rect;
        }

        public virtual double Downsample
        {
            set
            {
                Camera.Downsample = value; 
            }
            get
            {
                return Camera.Downsample; 
            }
        }

        /// <summary>
        /// Takes a capture and sends it to the clipboard
        /// </summary>
        protected Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[] CaptureArea(Geometry.GridRectangle Rect, float Downsample)
        {
            Debug.Assert((Rect.Width / Downsample) < 4096 && (Rect.Height / Downsample) < 4096);
            Debug.Assert(this.PaintCallRefCount == 0);

//            Vector3 OriginalCameraLookAt = this.Camera.LookAt;
            //float OriginalCameraDistance = this.CameraDistance;
 //           Rectangle OriginalVisibleRect = this.VisibleScreenRect; 

            int Width = (int)Math.Round(Rect.Width / Downsample);
            int Height = (int)Math.Round(Rect.Height / Downsample);

            Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[] data = new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[Width * Height];

            try
            {
                // Initialize our RenderTarget
                ScreenshotRenderTarget = new RenderTarget2D(Device, 
                    Width,
                    Height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.Depth24Stencil8);

                Device.SetRenderTarget(ScreenshotRenderTarget);

                bool OldAsynchTextureLoad = AsynchTextureLoad;
                AsynchTextureLoad = false;
           //     Draw(Downsample);
                AsynchTextureLoad = OldAsynchTextureLoad;

                Device.SetRenderTarget(null);

                

                data = new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[ScreenshotRenderTarget.Width * ScreenshotRenderTarget.Height];
                ScreenshotRenderTarget.GetData<Microsoft.Xna.Framework.Graphics.PackedVector.Byte4>(data);


       //         Draw(); 
            }
            finally
            {
                Device.SetRenderTarget(null);

                if (ScreenshotRenderTarget != null)
                {
                    ScreenshotRenderTarget.Dispose();
                    ScreenshotRenderTarget = null;
                }

                
//                this.CameraLookAt = OriginalCameraLookAt;
               // this.CameraDistance = OriginalCameraDistance;
            }

        
            return data;
        }


        protected override void Draw()
        {
            Draw(this.Scene, null);
        }

        private DepthStencilState DefaultDepthState = null;
        private BlendState DefaultBlendState = null; 

        private void UpdateEffectMatricies(Scene drawnScene)
        {
            Matrix worldViewProj = drawnScene.WorldViewProj;

            //Enables some basic effect characteristics, such as vertex coloring and default lighting.
            basicEffect.Projection = drawnScene.Projection;
            basicEffect.View = drawnScene.Camera.View;
            basicEffect.World = drawnScene.World;

            tileLayoutEffect.WorldViewProjMatrix = worldViewProj;
            this.channelOverlayEffect.WorldViewProjMatrix = worldViewProj;
            this.mergeHSVImagesEffect.WorldViewProjMatrix = worldViewProj;
            this.AnnotationOverlayEffect.WorldViewProjMatrix = worldViewProj;
            this.PolygonOverlayEffect.WorldViewProjMatrix = worldViewProj;
        }

        protected void Draw(Scene drawnScene, RenderTarget2D renderTarget)
        { 
            Device.SetRenderTarget(renderTarget);
            try
            {
                Device.Viewport = drawnScene.Viewport;
            }
            catch (ArgumentException)
            {
                UpdateSceneViewport(drawnScene);
                Device.Viewport = drawnScene.Viewport;

            }
            
            AnnotationOverlayEffect.RenderTargetSize = drawnScene.Viewport;
            this.LumaOverlayLineManager.RenderTargetSize = drawnScene.Viewport;

#if DEBUG
            if (renderTarget != null)
            {
                Debug.Assert(renderTarget.Bounds.Width >= drawnScene.Viewport.Width &&
                             renderTarget.Bounds.Height >= drawnScene.Viewport.Height);
                
            }
#endif

            if (DefaultDepthState == null || DefaultDepthState.IsDisposed)
            {
                DefaultDepthState = new DepthStencilState();

                DefaultDepthState.DepthBufferEnable = true;
                DefaultDepthState.DepthBufferFunction = CompareFunction.LessEqual;
                DefaultDepthState.StencilEnable = false;
                DefaultDepthState.DepthBufferWriteEnable = true;
            }

            Device.DepthStencilState = DefaultDepthState;

            if (DefaultBlendState == null || DefaultBlendState.IsDisposed)
            {
                DefaultBlendState = new BlendState();
                DefaultBlendState.AlphaSourceBlend = Blend.SourceAlpha;
                DefaultBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
                DefaultBlendState.ColorSourceBlend = Blend.SourceAlpha;
                DefaultBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            }

            Device.BlendState = DefaultBlendState;

            SamplerState sampleState = Device.SamplerStates[0];

            if (sampleState == null || sampleState.IsDisposed ||
                (sampleState.AddressU != TextureAddressMode.Clamp || sampleState.AddressV != TextureAddressMode.Clamp))
            {
                try
                {
                    sampleState = new SamplerState();
                    sampleState.AddressU = TextureAddressMode.Clamp;    //Compatability with Reach
                    sampleState.AddressV = TextureAddressMode.Clamp;
                    Device.SamplerStates[0] = sampleState;
                }
                catch (Exception)
                {
                    if (sampleState != null)
                    {
                        sampleState.Dispose();
                        sampleState = null;
                    }
                }
            }
            
            Device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);

            if (Device.RasterizerState == null || 
                Device.RasterizerState.IsDisposed ||
                Device.RasterizerState.CullMode != CullMode.None)
            {
                RasterizerState rState = null; 
                try
                {
                    rState = new RasterizerState();
                    rState.CullMode = CullMode.None;
                    Device.RasterizerState = rState;
                }
                catch (Exception)
                {
                    if (rState != null)
                    {
                        rState.Dispose();
                        rState = null;
                    }
                }
            }

            UpdateEffectMatricies(drawnScene);

            if (this.spriteBatch == null || this.spriteBatch.GraphicsDevice.IsDisposed)
            {
                IGraphicsDeviceService IService = this.Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
                if (IService != null)
                {
                    spriteBatch = new SpriteBatch(IService.GraphicsDevice);
                    fontArial = Content.Load<SpriteFont>(@"Arial");
                }
            }

//            GridRectangle Bounds = VisibleBounds();
            

#if !DEBUG
            try
            {
#endif
                //Since draw can be called from other methods than paint calls,
                //such as screencaptures, increment the PaintCallRefCount here
                PaintCallRefCount++;

                // Draw the control using the GraphicsDevice.
                Draw(drawnScene);
#if !DEBUG
            }
            catch (Exception except)
            {
                throw except;
            }
            finally
            {
#endif
                PaintCallRefCount--;
#if !DEBUG
            }
#endif
        }

        /// <summary>
        /// Draws the volume using the bounds and downsample onto the renderTarget.  If renderTarget is null the scene is drawn to the display
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="Bounds"></param>
        /// <param name="DownSample"></param>
        /// <param name="renderTarget"></param>
        protected virtual void Draw(Scene scene)
        {

        }

        public Geometry.GridVector2 ScreenToWorld(double X, double Y)
        {
            return Scene.ScreenToWorld(X, Y); 
        }

        public Geometry.GridVector2 WorldToScreen(double X, double Y)
        {
            return Scene.WorldToScreen(X, Y);
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            UpdateSceneViewport(this.Scene);

            base.OnClientSizeChanged(e);

            this.Refresh(); 
        }

        protected void UpdateSceneViewport(Scene scene)
        {
            Microsoft.Xna.Framework.Rectangle ClientBounds = new Microsoft.Xna.Framework.Rectangle(0,
                                                                                                   0,
                                                                                                   ClientRectangle.Width,
                                                                                                   ClientRectangle.Height);

            if (ClientBounds.Height == 0 || ClientBounds.Width == 0)
            {
                return; 
            }

            if (Device == null)
            {
                return; 
            }
            //Figure out how much we have to scale the downsample to keep the same scene in view if minimizing
            
            Viewport viewport = Device.Viewport;
            if (Device != null)
            {
                this.Downsample = Downsample * (((double)viewport.Width * (double)viewport.Height) / ((double)ClientBounds.Width * (double)ClientBounds.Height));

                if (viewport.Width != ClientBounds.Width ||
                    viewport.Height != ClientBounds.Height)
                {
                    this.graphicsDeviceService.ResetDevice(ClientRectangle.Width, ClientRectangle.Height);
                }
            }

            // this.GraphicsDevice.Viewport.Width = ClientSize.Width;
            // GraphicsDevice.Viewport.Height = ClientSize.Height;

            //Trace.WriteLine("Projection Bounds: " + ProjRect.ToString() + " Client Rect: " + ClientRectangle.ToString());

            scene.Viewport = Device.Viewport; 
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // ViewerControl
            // 
            this.MaximumSize = new System.Drawing.Size(4096, 4096);
            this.ResumeLayout(false);

        }
    }
}
