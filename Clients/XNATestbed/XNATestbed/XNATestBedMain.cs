using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using VikingXNAGraphics;
using RoundLineCode;
using RoundCurve;
using Geometry;

namespace XNATestbed
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class XNATestBedMain : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        RoundLineManager lineManager = new RoundLineCode.RoundLineManager();
        CurveManager curveManager = new CurveManager();
        VikingXNA.Scene Scene;
        VikingXNA.Camera Camera;
        SpriteFont fontArial;
        Texture2D labelTexture;

        double CurveAngle = 3.14159 / 4.0;

        public XNATestBedMain()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

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
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            fontArial = Content.Load<SpriteFont>(@"Arial");

            // TODO: use this.Content to load your game content here
            Camera = new VikingXNA.Camera();
            Camera.LookAt = new Vector2(0, 0);
            Camera.Downsample = 0.5;
            Scene = new VikingXNA.Scene(graphics.GraphicsDevice.Viewport, Camera);


            lineManager.Init(GraphicsDevice, Content);
            curveManager.Init(GraphicsDevice, Content);

            labelTexture = CreateTextureForLabel("The quick brown fox jumps over the lazy dog", GraphicsDevice, spriteBatch, fontArial);

            RasterizerState state = new RasterizerState();
            state.CullMode = CullMode.None;
            //state.FillMode = FillMode.WireFrame;

            GraphicsDevice.RasterizerState = state; 
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
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
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            ProcessGamepad();

            // TODO: Add your update logic here

            base.Update(gameTime);
        }

        private void ProcessGamepad()
        {
            Camera.Downsample += -GamePad.GetState(PlayerIndex.One).ThumbSticks.Right.Y;
            Camera.LookAt += GamePad.GetState(PlayerIndex.One).ThumbSticks.Left;
            CurveAngle += GamePad.GetState(PlayerIndex.One).ThumbSticks.Right.X;
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            RasterizerState state = new RasterizerState();
            state.CullMode = CullMode.None; 
            //state.FillMode = FillMode.WireFrame;

            GraphicsDevice.RasterizerState = state;

            Matrix ViewProjMatrix = Scene.Camera.View * Scene.Projection;
            GraphicsDevice.Clear(Color.CornflowerBlue);
            string TechniqueName = "AnimatedLinear";
            float time = DateTime.Now.Millisecond / 1000.0f;
            
            RoundLine line = new RoundLine(new Vector2((float)(-50.0f * Math.Cos(CurveAngle)), (float)(-50.0f * Math.Sin(CurveAngle)) + 50.0f),
                                           new Vector2((float)(50.0f * Math.Cos(CurveAngle)), (float)(50.0f * Math.Sin(CurveAngle)) + 50.0f));
            lineManager.Draw(new RoundLine[] { line }, 16, Color.Red, ViewProjMatrix, time, TechniqueName);
            
            GridVector2[] cps = CreateTestCurve2(CurveAngle, 100);
            RoundCurve.RoundCurve curve = new RoundCurve.RoundCurve(cps);
            //curveManager.Draw(new RoundCurve.RoundCurve[] { curve }, 16, Color.Blue, ViewProjMatrix, DateTime.Now.Second, labelTexture);
            curveManager.Draw(new RoundCurve.RoundCurve[] { curve }, 16, Color.Blue, ViewProjMatrix, time, TechniqueName);
            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }

        

        private GridVector2[] CreateTestCurve(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(-width,width),
                                                   new GridVector2(-width * Math.Cos(angle), -width * Math.Sin(angle)),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
                                                   //new GridVector2(width * Math.Cos(angle), /*width * Math.Sin(angle))};
            return cps;
        }

        private GridVector2[] CreateTestCurve2(double angle, double width)
        {
            GridVector2[] cps = new GridVector2[] {new GridVector2(width,width),
                                                   new GridVector2(0, width),
                                                   new GridVector2(0,0),
                                                   new GridVector2(width,0) };
            return Geometry.Lagrange.FitCurve(cps, 30);
            //new GridVector2(width * Math.Cos(angle), /*width * Math.Sin(angle))};
            //return cps;
        }

        public Texture2D CreateTextureForLabel(string label, GraphicsDevice device,
                              SpriteBatch spriteBatch,
                              SpriteFont font)
        { 
            Vector2 labelDimensions = font.MeasureString(label);
            RenderTarget2D target = new RenderTarget2D(device, (int)labelDimensions.X * 2, (int)labelDimensions.Y * 2);

            RenderTargetBinding[] oldRenderTargets = device.GetRenderTargets();
            device.SetRenderTarget(target);

            spriteBatch.Begin();
            spriteBatch.DrawString(font, label, new Vector2(0, 0), Color.Yellow, 0, new Vector2(0,0), 2, SpriteEffects.None, 0);
            spriteBatch.End();

            device.SetRenderTargets(oldRenderTargets);

            return target;
        }
    }
}
