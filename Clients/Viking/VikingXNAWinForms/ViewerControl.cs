using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundCurve;
using VikingXNA;
using VikingXNAGraphics;

namespace VikingXNAWinForms;

public class ViewerControl : GraphicsDeviceControl
{
    public RoundLineCode.RoundLineManager LineManager => DeviceEffectsStore<RoundLineCode.RoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);

    public RoundLineCode.LumaOverlayRoundLineManager LumaOverlayLineManager => DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);

    public RoundCurve.CurveManager CurveManager => DeviceEffectsStore<CurveManager>.GetOrCreateForDevice(this.Device, this.Content);

    public RoundCurve.CurveManagerHSV LumaOverlayCurveManager => DeviceEffectsStore<CurveManagerHSV>.GetOrCreateForDevice(this.Device, this.Content);

    public PolygonOverlayEffect PolygonOverlayEffect => DeviceEffectsStore<PolygonOverlayEffect>.GetOrCreateForDevice(this.Device, this.Content);

    public OverlayShaderEffect AnnotationOverlayEffect => DeviceEffectsStore<OverlayShaderEffect>.GetOrCreateForDevice(this.Device, this.Content);

    public BasicEffect? basicEffect;

    public TileLayoutEffect? tileLayoutEffect;
    public MergeHSVImagesEffect? mergeHSVImagesEffect;
    public ChannelOverlayEffect? channelOverlayEffect;

    public readonly uint MaxTextureWidth = 4096;
    public readonly uint MaxTextureHeight = 4096;

    public Camera Camera = new();

    #region Fonts

    public Microsoft.Xna.Framework.Graphics.SpriteBatch? spriteBatch = null;
    public Microsoft.Xna.Framework.Graphics.SpriteFont? fontArial = null;

    static readonly Dictionary<string, Vector2> LabelToSize = [];

    public static Vector2 GetLabelSize(SpriteFont font, string label)
    {
        if (font is null)
            throw new ArgumentNullException(nameof(font));

        //Label can't be empty or the offset measured is zero
        if (String.IsNullOrEmpty(label))
            label = " ";

        if (LabelToSize.TryGetValue(label, out var size))
            return size;

        LabelToSize[label] = font.MeasureString(label);

        return LabelToSize[label];
    }

    #endregion

    private Scene? _scene;
    /// <summary>
    /// Combination of the viewport and a camera used to draw this control
    /// </summary>
    [Browsable(true)]
    [DefaultValue(null)]
    [Category("Camera Settings")]
    public Scene? Scene
    {
        get => _scene;
        set
        {
            if (_scene == value)
                return;

            if (_scene is not null)
                _scene.OnSceneChanged -= this.OnSceneChanged;

            _scene = value;

            if (_scene is not null)
                _scene.OnSceneChanged += this.OnSceneChanged;
        }
    }

    protected virtual void OnSceneChanged(object sender, PropertyChangedEventArgs e)
    {

    }

    private Matrix worldMatrix => Matrix.Identity;

    /// <summary>
    /// The current world view projection matrix for the camera
    /// </summary>
    public Matrix WVPMatrix;

    /// <summary>
    /// When set to true we do not wait for all textures to load before drawing the screen
    /// </summary>
    public bool AsynchTextureLoad = true;

    public static float MaxImageDimension => 1000000;

    /// <summary>
    /// Initializes the transforms used for the 3D model.
    /// </summary>
    private void InitializeTransform()
    {
        if (Device is null)
            throw new InvalidOperationException("Graphics device is not initialized");

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
        basicEffect = new BasicEffect(Device)
        {
            //   basicEffect.DiffuseColor = new Vector3(0.1f, 0.1f, 0.1f);
            //   basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
            //   basicEffect.SpecularPower = 5.0f;
            AmbientLightColor = new Vector3(1f, 1f, 1f)
        };

        Matrix WorldViewProj = Scene.WorldViewProj;

        Effect effectTileLayout = Content.Load<Effect>("TileLayout");
        this.tileLayoutEffect = new TileLayoutEffect(effectTileLayout)
        {
            WorldViewProjMatrix = WorldViewProj
        };

        Effect effectHSVMerge = Content.Load<Effect>("MergeHSVImages");
        this.mergeHSVImagesEffect = new MergeHSVImagesEffect(effectHSVMerge)
        {
            WorldViewProjMatrix = WorldViewProj
        };

        Effect effectChannelOverlay = Content.Load<Effect>("ChannelOverlayShader");
        this.channelOverlayEffect = new ChannelOverlayEffect(effectChannelOverlay)
        {
            WorldViewProjMatrix = WorldViewProj
        };

    }

    public ViewerControl() : base()
    {
        Scene = null; //Initialize to null so that the Scene property setter can set it properly
        Downsample = 1.0;
        InitializeComponent();
    }

    /// <summary>
    /// Disposes the control and unsubscribes from device events.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from device events
            if (graphicsDeviceService != null)
            {
                graphicsDeviceService.DeviceResetting -= OnDeviceResetting;
                graphicsDeviceService.DeviceReset -= OnDeviceReset;
            }

            // Dispose effects
            basicEffect?.Dispose();
            basicEffect = null;
            tileLayoutEffect = null;
            mergeHSVImagesEffect = null;
            channelOverlayEffect = null;

            // Dispose sprite batch
            spriteBatch?.Dispose();
            spriteBatch = null;

            // Dispose states
            DefaultDepthState?.Dispose();
            DefaultDepthState = null;
            DefaultBlendState?.Dispose();
            DefaultBlendState = null;

            // Dispose screenshot render target
            ScreenshotRenderTarget?.Dispose();
            ScreenshotRenderTarget = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Objects used to render screenshots
    /// </summary>
    RenderTarget2D? ScreenshotRenderTarget;

    protected override void Initialize()
    {
        if (!DesignMode)
        {
            // Subscribe to device reset events
            if (graphicsDeviceService != null)
            {
                graphicsDeviceService.DeviceResetting += OnDeviceResetting;
                graphicsDeviceService.DeviceReset += OnDeviceReset;
            }

            //vertexDeclaration = VertexPositionNormalTexture.VertexDeclaration;

            InitializeTransform();
            InitializeEffect();


            DeviceEffectsStore<RoundLineCode.RoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
            DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
            DeviceEffectsStore<RoundCurve.CurveManager>.GetOrCreateForDevice(this.Device, this.Content);
            DeviceEffectsStore<RoundCurve.CurveManagerHSV>.GetOrCreateForDevice(this.Device, this.Content);
            DeviceEffectsStore<CircleInstancedEffect>.GetOrCreateForDevice(this.Device, this.Content);
        }
    }

    /// <summary>
    /// Called when the graphics device is about to be reset.
    /// Dispose of device-dependent resources that need to be recreated.
    /// </summary>
    protected virtual void OnDeviceResetting(object sender, EventArgs e)
    {
        // Dispose effects that will be invalid after reset
        basicEffect?.Dispose();
        basicEffect = null;

        // Note: tileLayoutEffect, mergeHSVImagesEffect, channelOverlayEffect
        // wrap Effect objects loaded from content, which are managed by ContentManager.
        // We just null our references; they'll be recreated in OnDeviceReset.
        tileLayoutEffect = null;
        mergeHSVImagesEffect = null;
        channelOverlayEffect = null;

        // Dispose sprite batch
        spriteBatch?.Dispose();
        spriteBatch = null;
        fontArial = null;

        // Dispose depth/blend states
        DefaultDepthState?.Dispose();
        DefaultDepthState = null;
        DefaultBlendState?.Dispose();
        DefaultBlendState = null;
    }

    /// <summary>
    /// Called after the graphics device has been reset.
    /// Recreate device-dependent resources.
    /// </summary>
    protected virtual void OnDeviceReset(object sender, EventArgs e)
    {
        // Reinitialize effects
        InitializeEffect();

        // Reinitialize effect stores (they were cleared in GraphicsDeviceService.ResetDevice)
        DeviceEffectsStore<RoundLineCode.RoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
        DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(this.Device, this.Content);
        DeviceEffectsStore<RoundCurve.CurveManager>.GetOrCreateForDevice(this.Device, this.Content);
        DeviceEffectsStore<RoundCurve.CurveManagerHSV>.GetOrCreateForDevice(this.Device, this.Content);
        DeviceEffectsStore<PolygonOverlayEffect>.GetOrCreateForDevice(this.Device, this.Content);
        DeviceEffectsStore<OverlayShaderEffect>.GetOrCreateForDevice(this.Device, this.Content);
        DeviceEffectsStore<CircleInstancedEffect>.GetOrCreateForDevice(this.Device, this.Content);

        // Font store will be recreated on demand
        DeviceFontStore.GetOrCreateForDevice(this.Device, this.Content);
    }

    /// <summary>
    /// Boundaries of the render target in world space
    /// </summary>
    /// <returns></returns>
    public Geometry.GridRectangle RenderTargetBounds()
    {
        if (Device is null)
            return new GridRectangle(0, 0, 10, 10);

        //For debugging
        const int offset = 0;

        //GridVector2 TopLeft = ScreenToWorld(offset, offset);
        //GridVector2 BottomLeft = ScreenToWorld(offset, GraphicsDevice.Viewport.Height - offset);
        //GridVector2 TopRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, offset);

        GridVector2 BottomLeft = Scene.ScreenToWorld(offset, Device.Viewport.Height - offset);
        GridVector2 TopRight = Scene.ScreenToWorld(Device.Viewport.Width - offset, offset);
        GridRectangle rect = new(BottomLeft, TopRight.X - BottomLeft.X, TopRight.Y - BottomLeft.Y);
        return rect;
    }

    [Browsable(true)]
    [Category("Camera Settings")]
    [DefaultValue(1.0)]
    public virtual double Downsample
    {
        set => Camera.Downsample = value;
        get => Camera.Downsample;
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

        if (Device is null)
            return data;

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

            if (ScreenshotRenderTarget != null)
            {
                data = new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[ScreenshotRenderTarget.Width * ScreenshotRenderTarget.Height];
                ScreenshotRenderTarget.GetData<Microsoft.Xna.Framework.Graphics.PackedVector.Byte4>(data);
            }


            //         Draw(); 
        }
        finally
        {
            if (Device != null)
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
        if (this.Scene != null)
            Draw(this.Scene, null);
    }

    private DepthStencilState? DefaultDepthState = null;
    private BlendState? DefaultBlendState = null;

    private void UpdateEffectMatricies(Scene drawnScene)
    {
        Matrix worldViewProj = drawnScene.WorldViewProj;

        //Enables some basic effect characteristics, such as vertex coloring and default lighting.
        if (basicEffect != null)
        {
            basicEffect.Projection = drawnScene.Projection;
            basicEffect.View = drawnScene.Camera.View;
            basicEffect.World = drawnScene.World;
        }

        if (tileLayoutEffect != null)
            tileLayoutEffect.WorldViewProjMatrix = worldViewProj;
        if (this.channelOverlayEffect != null)
            this.channelOverlayEffect.WorldViewProjMatrix = worldViewProj;
        if (this.mergeHSVImagesEffect != null)
            this.mergeHSVImagesEffect.WorldViewProjMatrix = worldViewProj;
        if (this.AnnotationOverlayEffect != null)
            this.AnnotationOverlayEffect.WorldViewProjMatrix = worldViewProj;
        if (this.PolygonOverlayEffect != null)
            this.PolygonOverlayEffect.WorldViewProjMatrix = worldViewProj;
    }

    /// <summary>
    /// Render the scene to the specific target.  Calls the normal Draw(scene) method after setting the render target to the passed variable.
    /// </summary>
    /// <param name="drawnScene"></param>
    /// <param name="renderTarget"></param>
    protected void Draw(Scene drawnScene, RenderTarget2D? renderTarget)
    {
        if (Device is null)
            return;

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

        if (AnnotationOverlayEffect != null)
            AnnotationOverlayEffect.RenderTargetSize = drawnScene.Viewport;
        this.LumaOverlayLineManager.RenderTargetSize = drawnScene.Viewport;

#if DEBUG
            if (renderTarget != null)
            {
                Debug.Assert(renderTarget.Bounds.Width >= drawnScene.Viewport.Width &&
                             renderTarget.Bounds.Height >= drawnScene.Viewport.Height);
            }
#endif

        if (DefaultDepthState is null || DefaultDepthState.IsDisposed)
        {
            DefaultDepthState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual,
                StencilEnable = false,
                DepthBufferWriteEnable = true
            };
        }

        Device.DepthStencilState = DefaultDepthState;

        if (DefaultBlendState is null || DefaultBlendState.IsDisposed)
        {
            DefaultBlendState = new BlendState
            {
                AlphaSourceBlend = Blend.SourceAlpha,
                AlphaDestinationBlend = Blend.InverseSourceAlpha,
                ColorSourceBlend = Blend.SourceAlpha,
                ColorDestinationBlend = Blend.InverseSourceAlpha
            };
        }

        Device.BlendState = DefaultBlendState;

        SamplerState? sampleState = Device?.SamplerStates[0];

        if (sampleState is null || sampleState.IsDisposed ||
            (sampleState.AddressU != TextureAddressMode.Clamp || sampleState.AddressV != TextureAddressMode.Clamp))
        {
            try
            {
                sampleState = new SamplerState
                {
                    AddressU = TextureAddressMode.Clamp,    //Compatibility with Reach
                    AddressV = TextureAddressMode.Clamp
                };
                Device.SamplerStates[0] = sampleState;
            }
            catch (Exception)
            {
                if (sampleState != null)
                {
                    sampleState.Dispose();
                    sampleState = null;
                }

                throw;
            }
        }

        if (Device == null) return;
        Device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);

        if (Device.RasterizerState is null ||
            Device.RasterizerState.IsDisposed ||
            Device.RasterizerState.CullMode != CullMode.None)
        {
            RasterizerState? rState = null;
            try
            {
                rState = new RasterizerState
                {
                    CullMode = CullMode.None
                };
                Device.RasterizerState = rState;
            }
            catch (Exception)
            {
                rState?.Dispose();
                rState = null;
                throw;
            }
        }

        UpdateEffectMatricies(drawnScene);

        if (this.spriteBatch is null || this.spriteBatch.GraphicsDevice.IsDisposed)
        {
            if (this.Services.GetService(typeof(IGraphicsDeviceService)) is IGraphicsDeviceService IService)
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
            throw;
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
    protected virtual void Draw(Scene scene) => throw new NotImplementedException();

    public Geometry.GridVector2 ScreenToWorld(double X, double Y) => Scene.ScreenToWorld(X, Y);

    public Geometry.GridVector2 WorldToScreen(double X, double Y) => Scene.WorldToScreen(X, Y);

    protected override void OnClientSizeChanged(EventArgs e)
    {
        UpdateSceneViewport(this.Scene);

        base.OnClientSizeChanged(e);

        this.Refresh();
    }

    protected void UpdateSceneViewport(Scene scene)
    {
        Microsoft.Xna.Framework.Rectangle ClientBounds = new(0,
                                                                                               0,
                                                                                               ClientRectangle.Width,
                                                                                               ClientRectangle.Height);

        if (ClientBounds.Height == 0 || ClientBounds.Width == 0)
        {
            return;
        }

        if (Device is null)
        {
            return;
        }
        //Figure out how much we have to scale the downsample to keep the same scene in view if minimizing

        if (Device is null)
            return;

        Viewport viewport = Device.Viewport;
        if (Device != null)
        {
            this.Downsample = Downsample * (((double)viewport.Width * (double)viewport.Height) / ((double)ClientBounds.Width * (double)ClientBounds.Height));

            if (viewport.Width != ClientBounds.Width ||
                viewport.Height != ClientBounds.Height)
            {
                if (this.graphicsDeviceService != null)
                    this.graphicsDeviceService.ResetDevice(ClientRectangle.Width, ClientRectangle.Height);
            }
        }

        // this.GraphicsDevice.Viewport.Width = ClientSize.Width;
        // GraphicsDevice.Viewport.Height = ClientSize.Height;

        //Trace.WriteLine("Projection Bounds: " + ProjRect.ToString() + " Client Rect: " + ClientRectangle.ToString());

        if (Device != null)
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
