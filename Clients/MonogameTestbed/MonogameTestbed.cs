﻿using Microsoft.Xna.Framework;
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
        GEOMETRY
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

        SortedDictionary<TestMode, IGraphicsTest> listTests = new SortedDictionary<TestMode, IGraphicsTest>();

        TestMode Mode = TestMode.GEOMETRY;

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

            foreach (IGraphicsTest test in listTests.Values)
            {
                test.Init(this);
            }
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
            if(GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Keys.Escape))
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

        public void Init(MonoTestbed window)
        {
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


    public class CurveViewTest : IGraphicsTest
    {
        CurveView curveView;
        CurveLabel leftCurveLabel;
        CurveLabel rightCurveLabel;


        public void Init(MonoTestbed window)
        {
            GridVector2[] cps = CreateTestCurve3(0, 100);
            curveView = new CurveView(cps, Color.Red, false, MonoTestbed.NumCurveInterpolations);
            leftCurveLabel = new CurveLabel("The quick brown fox jumps over the lazy dog", cps, Color.Black, false);
            rightCurveLabel = new CurveLabel("C 1485", cps, Color.PaleGoldenrod, false);
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

    public class LabelViewsTest : IGraphicsTest
    {
        CurveLabel curveLabel;
        LabelView labelView;

        public void Init(MonoTestbed window)
        {
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

        public void Init(MonoTestbed window)
        {
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
