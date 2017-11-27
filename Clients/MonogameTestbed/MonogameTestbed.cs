using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using VikingXNAGraphics;
using VikingXNA;
using RoundCurve;
using RoundLineCode;
using Geometry;
using System.Collections.Generic;
using MonoGame.Framework;
using MonoGame;
using System.Diagnostics;

namespace MonogameTestbed
{
    enum TestMode
    {
        TEXT,
        CURVE_LABEL,
        CURVE,
        LINESTYLES,
        CURVESTYLES,
        CLOSEDCURVE,
        POLYGON2D, 
        MESH,
        GEOMETRY,
        MORPHOLOGY,
        TRIANGLEALGORITHM, 
        BRANCHPORT,
        POLYWRAPPING,
        BRANCHASSIGNMENT,
    };

    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class MonoTestbed : Game
    {
        GraphicsDeviceManager graphics;
        public SpriteBatch spriteBatch;

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
        Polygon2DTest polygon2DTest = new Polygon2DTest();
        MeshTest meshTest = new MeshTest();
        GeometryTest geometryTest = new GeometryTest();
        MorphologyTest morphologyTest = new MorphologyTest();
        TriangleAlgorithmTest triangleTest = new TriangleAlgorithmTest();
        BranchPointTest branchTest = new BranchPointTest();
        PolywrappingTest polyWrapTest = new PolywrappingTest();
        BranchAssignmentTest brachAssignmentTest = new BranchAssignmentTest();

        SortedDictionary<TestMode, IGraphicsTest> listTests = new SortedDictionary<TestMode, IGraphicsTest>();

        TestMode Mode = TestMode.BRANCHASSIGNMENT;

        public static uint NumCurveInterpolations = 10;

        public MonoTestbed()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreparingDeviceSettings += graphics_PreparingDeviceSettings;
            Content.RootDirectory = "Content";
        }

        private void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {  
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 600;
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
            
            fontArial = Content.Load<SpriteFont>("Arial");

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

            listTests.Add(TestMode.CURVE, curveTest);
            listTests.Add(TestMode.CURVE_LABEL, curveViewTest);
            listTests.Add(TestMode.TEXT, labelTest);
            listTests.Add(TestMode.LINESTYLES, lineStyleTest);
            listTests.Add(TestMode.CURVESTYLES, curveStyleTest);
            listTests.Add(TestMode.CLOSEDCURVE, closedCurveTest);
            listTests.Add(TestMode.POLYGON2D, polygon2DTest);
            listTests.Add(TestMode.MESH, meshTest);
            listTests.Add(TestMode.GEOMETRY, geometryTest);
            listTests.Add(TestMode.MORPHOLOGY, morphologyTest);
            listTests.Add(TestMode.TRIANGLEALGORITHM, triangleTest);
            listTests.Add(TestMode.BRANCHPORT, branchTest);
            listTests.Add(TestMode.POLYWRAPPING, polyWrapTest);
            listTests.Add(TestMode.BRANCHASSIGNMENT, brachAssignmentTest);
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
             
            this.overlayEffect = DeviceEffectsStore<AnnotationOverBackgroundLumaEffect>.GetOrCreateForDevice(this.GraphicsDevice, Content);
            this.overlayEffect.WorldViewProjMatrix = WorldViewProj;

            PolygonOverlayEffect polyEffect = DeviceEffectsStore<PolygonOverlayEffect>.GetOrCreateForDevice(this.GraphicsDevice, Content);
            polyEffect.WorldViewProjMatrix = WorldViewProj;

            //this.channelEffect.WorldMatrix = worldMatrix;
            //this.channelEffect.ProjectionMatrix = projectionMatrix;
            //this.channelEffect.ViewMatrix = viewMatrix;
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
            if (Keyboard.GetState().IsKeyDown(Keys.F7))
                this.Mode = TestMode.POLYGON2D;
            if (Keyboard.GetState().IsKeyDown(Keys.F8))
                this.Mode = TestMode.MESH;
            if (Keyboard.GetState().IsKeyDown(Keys.F9))
                this.Mode = TestMode.GEOMETRY;
            if (Keyboard.GetState().IsKeyDown(Keys.F10))
                this.Mode = TestMode.MORPHOLOGY;
            if (Keyboard.GetState().IsKeyDown(Keys.F11))
                this.Mode = TestMode.TRIANGLEALGORITHM;
            if (Keyboard.GetState().IsKeyDown(Keys.F12))
                this.Mode = TestMode.BRANCHPORT;
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad1))
                this.Mode = TestMode.POLYWRAPPING;
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad2))
                this.Mode = TestMode.BRANCHASSIGNMENT;

            if (!listTests[Mode].Initialized)
            {
                listTests[Mode].Init(this);
                Debug.Assert(listTests[Mode].Initialized);
            }
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
            if (!listTests[Mode].Initialized)
            {
                listTests[Mode].Init(this);
                Debug.Assert(listTests[Mode].Initialized);
            }

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                //Close the game, but Monogame won't allow it?
            }

            ProcessKeyboard();

            //meshView.Update(gameTime); 

            listTests[Mode].Update();

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            //GraphicsDevice.SetRenderTarget(renderTarget);
            //meshView.Draw(GraphicsDevice);
            GraphicsDevice.SetRenderTarget(null);
            /*
            using (RenderTarget2D rt = DrawToRenderTarget(GraphicsDevice, meshView.Draw))
            {
                rtViewModel.RenderTargetSource.Texture = Engine.Instance.Renderer.CreateTexture(rt);
            }
            */

            // TODO: Add your drawing code here

            RasterizerState state = new RasterizerState();
            state.CullMode = CullMode.None;

            UpdateEffectMatricies(this.Scene);

            SamplerState sampler = new SamplerState();
            GraphicsDevice.RasterizerState = state;
            
           // spriteBatch.Begin();
           if(!listTests[Mode].Initialized)
           {
                listTests[Mode].Init(this);
                Debug.Assert(listTests[Mode].Initialized);
            }

            listTests[Mode].Draw(this); 

          //  spriteBatch.End();

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


    public class CurveTest : IGraphicsTest
    {
        public Texture2D labelTexture;

        double CurveAngle = 3.14159 / 4.0;


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;
            labelTexture = CreateTextureForLabel("The quick brown fox jumps over the lazy dog", window.GraphicsDevice, window.spriteBatch, window.fontArial);
        }

        public void Update()
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

            GridVector2[] cps = CreateTestCurveLagrange(CurveAngle, 100, new GridVector2(-150, 0));
            RoundCurve.RoundCurve curve = new RoundCurve.RoundCurve(cps, false);
            window.curveManager.Draw(new RoundCurve.RoundCurve[] { curve }, 16, Color.Blue, ViewProjMatrix, time, labelTexture);
            window.curveManager.Draw(new RoundCurve.RoundCurve[] { curve }, 16, Color.Blue, ViewProjMatrix, time, TechniqueName);

            GridVector2[] cpsCatmull = CreateTestCurveCatmull(CurveAngle, 100, new GridVector2(150, 0));
            RoundCurve.RoundCurve CatmullCurve = new RoundCurve.RoundCurve(cpsCatmull, false);
            window.curveManager.Draw(new RoundCurve.RoundCurve[] { CatmullCurve }, 16, Color.Blue, ViewProjMatrix, time, labelTexture);
            window.curveManager.Draw(new RoundCurve.RoundCurve[] { CatmullCurve }, 16, Color.Blue, ViewProjMatrix, time, TechniqueName);
             
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

        private static GridVector2[] CreateTestCurveLagrange(double angle, double width, GridVector2 origin)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            GridVector2[] curvePoints = Geometry.Lagrange.FitCurve(cps, 30);
            return curvePoints.Translate(origin);
        }

        private static GridVector2[] CreateTestCurveCatmull(double angle, double width, GridVector2 origin)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };

            GridVector2[] curvePoints = cps.CalculateCurvePoints(30, true);
            return curvePoints.Translate(origin);
        }
    }


    public class CurveViewTest : IGraphicsTest
    {
        CurveView curveViewLagrange;
        CurveView curveViewCatmull;
        CurveLabel leftLagrangeCurveLabel;
        CurveLabel rightLagrangeCurveLabel;
        CurveLabel leftCatmullCurveLabel;
        CurveLabel rightCatmullCurveLabel;


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            GridVector2[] cps = CreateTestCurveLagrange(0, 100, new GridVector2(-100,0));
            curveViewLagrange = new CurveView(cps, Color.Red, false, 2);
            leftLagrangeCurveLabel = new CurveLabel("The quick brown fox jumps over the lazy dog", cps, Color.Black, false);
            rightLagrangeCurveLabel = new CurveLabel("C 1485", cps, Color.PaleGoldenrod, false);

            GridVector2[] cpsCatmull = CreateTestCurveCatmull(0, 100, new GridVector2(100, 0));
            curveViewCatmull = new CurveView(cpsCatmull, Color.Red, true, 2);
            leftCatmullCurveLabel = new CurveLabel("The quick brown fox jumps over the lazy dog", cpsCatmull, Color.Black, false);
            rightCatmullCurveLabel = new CurveLabel("C 1485", cpsCatmull, Color.PaleGoldenrod, false);
        }

        public void Update()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            string TechniqueName = "AnimatedLinear";
            float time = DateTime.Now.Millisecond / 1000.0f;

            double totalLabelLength = (double)(leftLagrangeCurveLabel.Text.Length + rightLagrangeCurveLabel.Text.Length);
            leftLagrangeCurveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;
            rightLagrangeCurveLabel.Alignment = RoundCurve.HorizontalAlignment.Right;
            leftLagrangeCurveLabel.Max_Curve_Length_To_Use_Normalized = (float)(leftLagrangeCurveLabel.Text.Length / totalLabelLength);
            rightLagrangeCurveLabel.Max_Curve_Length_To_Use_Normalized = (float)(rightLagrangeCurveLabel.Text.Length / totalLabelLength);

            CurveView.Draw(window.GraphicsDevice, scene, window.curveManager, window.basicEffect, window.overlayEffect, time, new CurveView[] { curveViewLagrange, curveViewCatmull });
            CurveLabel.Draw(window.GraphicsDevice, scene, window.spriteBatch, window.fontArial, window.curveManager, new CurveLabel[] { leftLagrangeCurveLabel, rightLagrangeCurveLabel, leftCatmullCurveLabel, rightCatmullCurveLabel});

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

        private static GridVector2[] CreateTestCurveLagrange(double angle, double width, GridVector2 origin)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return cps.Translate(origin);
        }

        private static GridVector2[] CreateTestCurveCatmull(double angle, double width, GridVector2 origin)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            
            return cps.Translate(origin);
        }
    }

    public class LabelViewsTest : IGraphicsTest
    {
        CurveLabel curveLabel;
        LabelView labelView;


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            GridVector2[] cps = CreateTestCurve3(0, 100);
            curveLabel = new CurveLabel("CurveLabel", cps, Color.Black, false);
            labelView = new LabelView("LabelView", new GridVector2(0, 0));
        }

        public void Update()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            string TechniqueName = "AnimatedLinear";
            float time = DateTime.Now.Millisecond / 1000.0f;

            curveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;

            window.spriteBatch.Begin();

            CurveLabel.Draw(window.GraphicsDevice, scene, window.spriteBatch, window.fontArial, window.curveManager, new CurveLabel[] { curveLabel });
            labelView.Draw(window.spriteBatch, window.fontArial, window.Scene);

            window.spriteBatch.End();

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

    public class LineViewStylesTest : IGraphicsTest
    {
        List<LineView> listLineViews = new List<LineView>();
        List<LabelView> listLabelViews = new List<LabelView>();


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

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
                listLineViews.Add(new LineView(source, dest, YStep / 1.5, Color.Blue, style, false));

                Y += YStep;

                listLabelViews.Add(new LabelView(style.ToString(), source + new GridVector2(-25, 10)));
            }
        }

        public void Update()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            float time = DateTime.Now.Millisecond / 1000.0f;

            LineView.Draw(window.GraphicsDevice, scene, window.lineManager, listLineViews.ToArray());

            window.spriteBatch.Begin();
            listLabelViews.ForEach(lv => { lv.Draw(window.spriteBatch, window.fontArial, scene); });
            window.spriteBatch.End();
        }
    }


    public class CurveViewStylesTest : IGraphicsTest
    {
        List<CurveView> listLineViews = new List<CurveView>();
        List<LabelView> listLabelViews = new List<LabelView>();


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

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

                listLineViews.Add(new CurveView(new GridVector2[] { source, mid, dest }, Color.Blue, false, MonoTestbed.NumCurveInterpolations, lineWidth: YStep / 1.5, lineStyle: style));

                Y += YStep;

                listLabelViews.Add(new LabelView(style.ToString(), source + new GridVector2(-25, 10)));
            }
        }

        public void Update()
        {
        }

        public void Draw(MonoTestbed window)
        {
            VikingXNA.Scene scene = window.Scene;
            Matrix ViewProjMatrix = scene.ViewProj;
            float time = DateTime.Now.Millisecond / 1000.0f;

            

            CurveView.Draw(window.GraphicsDevice, scene, window.curveManager, window.basicEffect, window.overlayEffect, time, this.listLineViews.ToArray());

            window.spriteBatch.Begin();
            listLabelViews.ForEach(lv => { lv.Draw(window.spriteBatch, window.fontArial, scene); });
            window.spriteBatch.End();
        }
    }

    public class ClosedCurveViewTest : IGraphicsTest
    {
        CurveView curveView;
        CurveLabel curveLabel;
        bool _initialized = false; 
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            GridVector2[] cps = CreateTestCurve(90, 190);
            curveView = new CurveView(cps, Color.Red, true, 10, lineWidth: 64, controlPointRadius: 16, lineStyle: LineStyle.HalfTube);
            curveLabel = new CurveLabel("The quick brown fox jumps over the lazy dog", cps, Color.Black, true);
        }

        public void Update()
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
