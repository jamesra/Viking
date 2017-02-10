using EmptyKeys.UserInterface;
using EmptyKeys.UserInterface.Generated;
using EmptyKeys.UserInterface.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuneWpfControlLibrary;
using System;
using DXViews;
using VikingXNA;
using VikingXNAGraphics;
using RoundCurve;
using RoundLineCode;
using Geometry;
using System.Collections.Generic;

namespace Viewer
{
    enum TestMode
    {
        TEXT,
        CURVE_LABEL,
        CURVE,
        LINESTYLES,
        CURVESTYLES,
        CLOSEDCURVE
    };

    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class MonoTestbed : Game
    {
        GraphicsDeviceManager graphics;
        public SpriteBatch spriteBatch;

        private Root root;

        private int nativeScreenWidth;
        private int nativeScreenHeight;

        MeshView meshView = new DXViews.MeshView();

        public RoundLineManager lineManager = new RoundLineCode.RoundLineManager();
        public CurveManager curveManager = new CurveManager();
        public VikingXNA.Scene Scene;
        public VikingXNA.Camera Camera;
        public SpriteFont fontArial;
        public BasicEffect basicEffect;
        public AnnotationOverBackgroundLumaEffect overlayEffect;

        CurveTest curveTest = new CurveTest();
        CurveViewTest curveViewTest = new CurveViewTest();
        LabelViewsTest labelTest = new LabelViewsTest();
        LineViewStylesTest lineStyleTest = new LineViewStylesTest();
        CurveViewStylesTest curveStyleTest = new CurveViewStylesTest();
        ClosedCurveViewTest closedCurveTest = new ClosedCurveViewTest();

        TestMode Mode = TestMode.CURVE_LABEL;

        RenderTarget2D renderTarget = null;
        EKLibrary.RenderTargetViewModel rtViewModel = new EKLibrary.RenderTargetViewModel();

        public MonoTestbed()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreparingDeviceSettings += graphics_PreparingDeviceSettings;
            graphics.DeviceCreated += graphics_DeviceCreated;
            Content.RootDirectory = "Content";
        }

        void graphics_DeviceCreated(object sender, EventArgs e)
        {
            Engine engine = new MonoGameEngine(GraphicsDevice, nativeScreenWidth, nativeScreenHeight);
        }

        private void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            nativeScreenWidth = graphics.PreferredBackBufferWidth;
            nativeScreenHeight = graphics.PreferredBackBufferHeight;

            graphics.PreferredBackBufferWidth = 1920;
            graphics.PreferredBackBufferHeight = 1280;
            graphics.PreferMultiSampling = true;
            graphics.GraphicsProfile = GraphicsProfile.HiDef;
            graphics.SynchronizeWithVerticalRetrace = true;
            graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;            
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 16;            
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();

            this.IsMouseVisible = true;

            
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice); 

            SpriteFont font = Content.Load<SpriteFont>("Segoe_UI_15_Bold");
            fontArial = Content.Load<SpriteFont>("Arial");
            FontManager.DefaultFont = Engine.Instance.Renderer.CreateFont(font);
            root = new Root(); 
            root.DataContext = rtViewModel;

            FontManager.Instance.LoadFonts(Content);

            // TODO: use this.Content to load your game content here
            RelayCommand GoToLocationCommand = new RelayCommand(new Action<object>(OnF10));
            KeyBinding f10 = new KeyBinding(GoToLocationCommand, KeyCode.F10, ModifierKeys.None);
            root.InputBindings.Add(f10);


            Camera = new VikingXNA.Camera();
            Camera.LookAt = new Vector2(0, 0);
            Camera.Downsample = 0.5;
            Scene = new VikingXNA.Scene(graphics.GraphicsDevice.Viewport, Camera);

            lineManager.Init(GraphicsDevice, Content);
            curveManager.Init(GraphicsDevice, Content);

            RasterizerState state = new RasterizerState();
            state.CullMode = CullMode.None;
            //state.FillMode = FillMode.WireFrame;

            GraphicsDevice.RasterizerState = state;

            InitializeEffects();

            curveTest.Init(this);
            curveViewTest.Init(this);
            labelTest.Init(this);
            lineStyleTest.Init(this);
            curveStyleTest.Init(this);
            closedCurveTest.Init(this);

            renderTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            rtViewModel.RenderTargetSource.Texture = Engine.Instance.Renderer.CreateTexture(renderTarget);
        }

        /// <summary>
        /// Initializes the basic effect (parameter setting and technique selection)
        /// used for the 3D model.
        /// </summary>
        private void InitializeEffects()
        {
            basicEffect = new BasicEffect(this.GraphicsDevice);
            //   basicEffect.DiffuseColor = new Vector3(0.1f, 0.1f, 0.1f);
            //   basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
            //   basicEffect.SpecularPower = 5.0f;
            basicEffect.AmbientLightColor = new Vector3(1f, 1f, 1f);
            /*
            basicEffect.Projection = projectionMatrix;
            basicEffect.World = worldMatrix;
            basicEffect.View = camera.View;
            basicEffect.Projection = projectionMatrix;
            */

            Matrix WorldViewProj = Scene.WorldViewProj;

            Effect AnnotationOverlayShader = Content.Load<Effect>("AnnotationOverlayShader");
            this.overlayEffect = new AnnotationOverBackgroundLumaEffect(AnnotationOverlayShader);
            this.overlayEffect.WorldViewProjMatrix = WorldViewProj;

            //this.channelEffect.WorldMatrix = worldMatrix;
            //this.channelEffect.ProjectionMatrix = projectionMatrix;
            //this.channelEffect.ViewMatrix = viewMatrix;
        }

        private void OnF10(object obj)
        {
            GoToLocationForm form = new GoToLocationForm();
            form.Show();
        }

        private void ProcessKeyboard()
        {
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.F1))
                this.Mode = TestMode.CURVE;
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.F2))
                this.Mode = TestMode.CURVE_LABEL;
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.F3))
                this.Mode = TestMode.TEXT;
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.F4))
                this.Mode = TestMode.LINESTYLES;
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.F5))
                this.Mode = TestMode.CURVESTYLES;
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.F6))
                this.Mode = TestMode.CLOSEDCURVE;
        }

        private void UpdateEffectMatricies(Scene drawnScene)
        {
            basicEffect.Projection = drawnScene.Projection;
            basicEffect.View = drawnScene.Camera.View;
            basicEffect.World = drawnScene.World;

            overlayEffect.WorldViewProjMatrix = drawnScene.WorldViewProj;
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            ProcessKeyboard();

            // TODO: Add your update logic here
            root.UpdateInput(gameTime.ElapsedGameTime.TotalMilliseconds);
            root.UpdateLayout(gameTime.ElapsedGameTime.TotalMilliseconds);

            meshView.Update(gameTime); 

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            GraphicsDevice.SetRenderTarget(renderTarget);
            meshView.Draw(GraphicsDevice);
            GraphicsDevice.SetRenderTarget(null);
            /*
            using (RenderTarget2D rt = DrawToRenderTarget(GraphicsDevice, meshView.Draw))
            {
                rtViewModel.RenderTargetSource.Texture = Engine.Instance.Renderer.CreateTexture(rt);
            }
            */
            root.Draw(gameTime.ElapsedGameTime.TotalMilliseconds);

            // TODO: Add your drawing code here

            RasterizerState state = new RasterizerState();
            state.CullMode = CullMode.None;

            UpdateEffectMatricies(this.Scene);

            SamplerState sampler = new SamplerState();
            GraphicsDevice.RasterizerState = state;

            Matrix ViewProjMatrix = Scene.ViewProj;
              
            spriteBatch.Begin();

            switch (Mode)
            {
                case TestMode.CURVE:
                    curveTest.Draw(this);
                    break;
                case TestMode.CURVE_LABEL:
                    curveViewTest.Draw(this);
                    break;
                case TestMode.TEXT:
                    labelTest.Draw(this);
                    break;
                case TestMode.LINESTYLES:
                    lineStyleTest.Draw(this);
                    break;
                case TestMode.CURVESTYLES:
                    curveStyleTest.Draw(this);
                    break;
                case TestMode.CLOSEDCURVE:
                    closedCurveTest.Draw(this);
                    break;
            }

            spriteBatch.End(); 

            base.Draw(gameTime);
        }

        protected RenderTarget2D DrawToRenderTarget(GraphicsDevice device, Action<GraphicsDevice> drawAction)
        {
            RenderTarget2D target = new RenderTarget2D(device, device.Viewport.Width, device.Viewport.Height);

            RenderTargetBinding[] oldRenderTargets = device.GetRenderTargets();
            device.SetRenderTarget(target);
            device.Clear(Color.Transparent);

            drawAction(device);

            device.SetRenderTargets(oldRenderTargets);

            return target;
        }
    }


    public class CurveTest
    {
        public Texture2D labelTexture;

        double CurveAngle = 3.14159 / 4.0;

        public void Init(MonoTestbed window)
        {
            labelTexture = CreateTextureForLabel("The quick brown fox jumps over the lazy dog", window.GraphicsDevice, window.spriteBatch, window.fontArial);
        }

        public void ProcessGamepad()
        {
            CurveAngle += GamePad.GetState(PlayerIndex.One).ThumbSticks.Right.X;
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            string TechniqueName = "AnimatedLinear";
            float time = DateTime.Now.Millisecond / 1000.0f;

            RoundLine line = new RoundLine(new Vector2((float)(-50.0f * Math.Cos(CurveAngle)), (float)(-50.0f * Math.Sin(CurveAngle)) + 50.0f),
                                           new Vector2((float)(50.0f * Math.Cos(CurveAngle)), (float)(50.0f * Math.Sin(CurveAngle)) + 50.0f));
            window.lineManager.Draw(new RoundLine[] { line }, 16, Color.Red, ViewProjMatrix, time, labelTexture);

            GridVector2[] cps = CreateTestCurve2(CurveAngle, 100);
            RoundCurve.RoundCurve curve = new RoundCurve.RoundCurve(cps, false);
            window.curveManager.Draw(new RoundCurve.RoundCurve[] { curve }, 16, Color.Blue, ViewProjMatrix, time, labelTexture);
            window.curveManager.Draw(new RoundCurve.RoundCurve[] { curve }, 16, Color.Blue, ViewProjMatrix, time, TechniqueName);
        }

        public Texture2D CreateTextureForLabel(string label, GraphicsDevice device,
                              SpriteBatch spriteBatch,
                              SpriteFont font)
        {
            Vector2 labelDimensions = font.MeasureString(label);
            RenderTarget2D target = new RenderTarget2D(device, (int)labelDimensions.X * 2, (int)labelDimensions.Y * 2);

            RenderTargetBinding[] oldRenderTargets = device.GetRenderTargets();
            device.SetRenderTarget(target);
            device.Clear(Color.Transparent);

            spriteBatch.Begin();
            spriteBatch.DrawString(font, label, new Vector2(0, 0), Color.Yellow, 0, new Vector2(0, 0), 2, SpriteEffects.None, 0);
            spriteBatch.End();

            device.SetRenderTargets(oldRenderTargets);

            return target;
        }

        private static GridVector2[] CreateTestCurve(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-width,width),
                                                   new GridVector2(-width * Math.Cos(angle), -width * Math.Sin(angle)),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return cps;
        }

        private static GridVector2[] CreateTestCurve2(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return Geometry.Lagrange.FitCurve(cps, 30);
        }
    }


    public class CurveViewTest
    {
        CurveView curveView;
        CurveLabel leftCurveLabel;
        CurveLabel rightCurveLabel;

        public void Init(MonoTestbed window)
        {
            GridVector2[] cps = CreateTestCurve3(0, 100);
            curveView = new CurveView(cps, Color.Red, false);
            leftCurveLabel = new CurveLabel("The quick brown fox jumps over the lazy dog", cps, Color.Black, false);
            rightCurveLabel = new CurveLabel("C 1485", cps, Color.PaleGoldenrod, false);
        }

        public void ProcessGamepad()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            string TechniqueName = "AnimatedLinear";
            float time = DateTime.Now.Millisecond / 1000.0f;

            double totalLabelLength = (double)(leftCurveLabel.Text.Length + rightCurveLabel.Text.Length);
            leftCurveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;
            rightCurveLabel.Alignment = RoundCurve.HorizontalAlignment.Right;
            leftCurveLabel.Max_Curve_Length_To_Use_Normalized = (float)(leftCurveLabel.Text.Length / totalLabelLength);
            rightCurveLabel.Max_Curve_Length_To_Use_Normalized = (float)(rightCurveLabel.Text.Length / totalLabelLength);

            CurveView.Draw(window.GraphicsDevice, scene, window.curveManager, window.basicEffect, window.overlayEffect, time, new CurveView[] { curveView });
            CurveLabel.Draw(window.GraphicsDevice, scene, window.spriteBatch, window.fontArial, window.curveManager, new CurveLabel[] { leftCurveLabel, rightCurveLabel });

        }

        private static GridVector2[] CreateTestCurve(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-width,width),
                                                   new GridVector2(-width * Math.Cos(angle), -width * Math.Sin(angle)),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return cps;
        }

        private static GridVector2[] CreateTestCurve2(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return cps;
        }

        private static GridVector2[] CreateTestCurve3(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-100,100),
                                                   new GridVector2(-50, 0),
                                                   new GridVector2(0,100),
                                                   new GridVector2(100,0) };
            return cps;
        }
    }

    public class LabelViewsTest
    {
        CurveLabel curveLabel;
        LabelView labelView;

        public void Init(MonoTestbed window)
        {
            GridVector2[] cps = CreateTestCurve3(0, 100);
            curveLabel = new CurveLabel("CurveLabel", cps, Color.Black, false);
            labelView = new LabelView("LabelView", new GridVector2(0, 0));
        }

        public void ProcessGamepad()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            string TechniqueName = "AnimatedLinear";
            float time = DateTime.Now.Millisecond / 1000.0f;

            curveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;

            CurveLabel.Draw(window.GraphicsDevice, scene, window.spriteBatch, window.fontArial, window.curveManager, new CurveLabel[] { curveLabel });
            labelView.Draw(window.spriteBatch, window.fontArial, window.Scene);

            curveLabel.FontSize = (time * 8f) + 8f;
            labelView.FontSize = (time * 8f) + 8f;
        }

        private static GridVector2[] CreateTestCurve(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-width,width),
                                                   new GridVector2(-width * Math.Cos(angle), -width * Math.Sin(angle)),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return cps;
        }

        private static GridVector2[] CreateTestCurve2(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return cps;
        }

        private static GridVector2[] CreateTestCurve3(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-50, 0),
                                                   new GridVector2(0,0),
                                                   new GridVector2(100,0) };
            return cps;
        }
    }

    public class LineViewStylesTest
    {
        List<LineView> listLineViews = new List<LineView>();
        List<LabelView> listLabelViews = new List<LabelView>();

        public void Init(MonoTestbed window)
        {
            double MinX = -100;
            double MaxX = 100;

            double MinY = -100;
            double MaxY = 100;
            double NumLineTypes = (double)Enum.GetValues(typeof(LineStyle)).Length;
            double YStep = (MaxY - MinY) / NumLineTypes;

            double Y = MinY;

            foreach (LineStyle style in Enum.GetValues(typeof(LineStyle)))
            {
                GridVector2 source = new GridVector2(MinX, Y);
                GridVector2 dest = new GridVector2(MaxX, Y);
                listLineViews.Add(new LineView(source, dest, YStep / 1.5, Color.Blue, style));

                Y += YStep;

                listLabelViews.Add(new LabelView(style.ToString(), source + new GridVector2(-25, 10)));
            }
        }

        public void ProcessGamepad()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            float time = DateTime.Now.Millisecond / 1000.0f;

            LineView.Draw(window.GraphicsDevice, scene, window.lineManager, listLineViews.ToArray());

            listLabelViews.ForEach(lv => { lv.Draw(window.spriteBatch, window.fontArial, scene); });
        }
    }


    public class CurveViewStylesTest
    {
        List<CurveView> listLineViews = new List<CurveView>();
        List<LabelView> listLabelViews = new List<LabelView>();

        public void Init(MonoTestbed window)
        {
            double MinX = -100;
            double MaxX = 100;

            double MinY = -100;
            double MaxY = 100;
            double NumLineTypes = (double)Enum.GetValues(typeof(LineStyle)).Length;
            double YStep = (MaxY - MinY) / NumLineTypes;

            double Y = MinY;

            foreach (LineStyle style in Enum.GetValues(typeof(LineStyle)))
            {
                GridVector2 source = new GridVector2(MinX, Y);
                GridVector2 mid = new GridVector2(MinX + (MaxX - MinX / 2.0), Y - 30);
                GridVector2 dest = new GridVector2(MaxX, Y);

                listLineViews.Add(new CurveView(new GridVector2[] { source, mid, dest }, Color.Blue, false, lineWidth: YStep / 1.5, lineStyle: style));

                Y += YStep;

                listLabelViews.Add(new LabelView(style.ToString(), source + new GridVector2(-25, 10)));
            }
        }

        public void ProcessGamepad()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            float time = DateTime.Now.Millisecond / 1000.0f;

            CurveView.Draw(window.GraphicsDevice, scene, window.curveManager, window.basicEffect, window.overlayEffect, time, this.listLineViews.ToArray());

            listLabelViews.ForEach(lv => { lv.Draw(window.spriteBatch, window.fontArial, scene); });
        }
    }

    public class ClosedCurveViewTest
    {
        CurveView curveView;
        CurveLabel curveLabel;

        public void Init(MonoTestbed window)
        {
            GridVector2[] cps = CreateTestCurve(90, 190);
            curveView = new CurveView(cps, Color.Red, true, null, lineWidth: 64, controlPointRadius: 16, lineStyle: LineStyle.HalfTube);
            curveLabel = new CurveLabel("The quick brown fox jumps over the lazy dog", cps, Color.Black, true);
        }

        public void ProcessGamepad()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            float time = DateTime.Now.Millisecond / 1000.0f;

            curveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;

            CurveView.Draw(window.GraphicsDevice, scene, window.curveManager, window.basicEffect, window.overlayEffect, time, new CurveView[] { curveView });
            CurveLabel.Draw(window.GraphicsDevice, scene, window.spriteBatch, window.fontArial, window.curveManager, new CurveLabel[] { curveLabel });

        }

        private static GridVector2[] CreateTestCurve(double height, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-width,0),
                                                   new GridVector2(-width / 2.0, -height/4),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width / 2.0, height),
                                                   new GridVector2(width,0),
                                                   new GridVector2(0,-height)};
            return cps;
        }
    }
}
