using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA; 
using Utilities;
using System.Drawing.Imaging; 

namespace VikingXNA
{
    public class ViewerControl : GraphicsDeviceControl
    {
        public Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch = null;
        public Microsoft.Xna.Framework.Graphics.SpriteFont fontArial = null;

        public RoundLineCode.RoundLineManager LineManager = new RoundLineCode.RoundLineManager();  

        VertexDeclaration vertexDeclaration;
        public BasicEffect basicEffect;

        public ChannelEffect channelEffect; 

        private Vector3 _CameraLookAt = new Vector3(0, 0, 0);
        private float _CameraDistance = 500f;

        private float _CameraPan = MathHelper.ToRadians(0f);
        private float _CameraTilt = MathHelper.ToRadians(0f);
        private float _CameraRotation = MathHelper.ToRadians(0f); 

        public Matrix worldMatrix;
        public Matrix viewMatrix;
        public Matrix projectionMatrix;
        public readonly float drawdistance = 10000f;
        public readonly float neardrawdistance = 0.5f;

        public Rectangle VisibleScreenRect = new Rectangle(0, 0, 640, 480);
        
        public static float MaxImageDimension
        {
            get
            {
                return 1000000;
            }
        }

        #region CameraControl

        public Rectangle ProjectionBounds
        {
            get
            {
                double ZScale = _CameraDistance / drawdistance;

                if (ClientRectangle.Width == 0)
                    return new Rectangle(0, 0, 640, 480);

                if (GraphicsDevice == null)
                    return new Rectangle(0, 0, 640, 480); 

//                double aspect = (double)GraphicsDevice.Viewport.Height / (double)GraphicsDevice.Viewport.Width;
  //              GraphicsDevice.Viewport.AspectRatio
    //            aspect = (double)this.ClientRectangle.Height / (double)this.ClientRectangle.Width;

                double aspect = (double)VisibleScreenRect.Height / (double)VisibleScreenRect.Width; 

                float VisibleWidth = (float)(ZScale * MaxImageDimension);

                Rectangle R = new Rectangle(0, 0, (int)(VisibleWidth), (int)(VisibleWidth * aspect));
                return R; 
            }
        }

        public virtual float CameraDistance
        {
            get { return _CameraDistance; }
            set
            {
                if (value <= neardrawdistance )
                    value = neardrawdistance + float.Epsilon;
                else if (value >= drawdistance - (drawdistance / 1000) )
                    value = drawdistance - (drawdistance / 1000);

                _CameraDistance = value;
                UpdateViewMatrix();

//                GridRectangle Bounds = VisibleBounds();
            }
        }

        public float CameraPan
        {
            get { return MathHelper.ToDegrees(_CameraPan); }
            set
            {
                _CameraPan = MathHelper.ToRadians(value);

                UpdateViewMatrix();
            }
        }

        public float CameraTilt
        {
            get { return MathHelper.ToDegrees(_CameraTilt); }
            set
            {
                if (value >= 90)
                    value = 89;
                else if (value <= 0)
                    value = float.Epsilon;

                _CameraTilt = MathHelper.ToRadians(value);
                UpdateViewMatrix();

            }
        }

        public Vector3 CameraLookAt
        {
            get
            {
                return _CameraLookAt;
            }
            set
            {
                /*
                if (value.X < -world.PixelSize.Width)
                    value.X = -world.PixelSize.Width;
                else if (value.X > world.PixelSize.Width * 2)
                    value.X = world.PixelSize.Width * 2;
                if (value.Y < -world.PixelSize.Height)
                    value.Y = -world.PixelSize.Height;
                else if (value.Y > world.PixelSize.Height * 2)
                    value.Y = world.PixelSize.Height * 2;
                
                try
                {
                    value.Z = Global.World.Terrain.GetHeight(new Vector2(value.X, value.Y));
                }
                catch
                {
                    value.Z = Global.SeaLevel;
                }
                */
                _CameraLookAt = value;

                UpdateViewMatrix();
            }
        }

        public float CameraRotation
        {
            get
            {
                return MathHelper.ToDegrees(_CameraRotation);
            }
            set
            {
                if (float.IsNaN(value))
                    return; 

                float val = MathHelper.ToRadians(value);

                if (float.IsNaN(val))
                    val = 0.0f; 

                _CameraRotation = val; 

                UpdateViewMatrix();
            }
        }

        private void UpdateViewMatrix()
        {
            //Matrix TiltMatrix = Matrix.Identity; //Matrix.CreateRotationX(_CameraTilt);// *Matrix.CreateRotationY(_CameraPan);
            //Matrix rotationMatrix = Matrix.CreateRotationZ(_CameraRotation);
            Vector3 CameraPos = Vector3.Backward;
            Vector3 transformedPos = CameraPos; 
            //Vector3 transformedPos = Vector3.Transform(CameraPos, TiltMatrix);
            //transformedPos = Vector3.Transform(transformedPos, rotationMatrix);
            transformedPos = transformedPos * _CameraDistance;
            transformedPos = transformedPos + _CameraLookAt;

            viewMatrix = Matrix.CreateLookAt(transformedPos, _CameraLookAt, Vector3.UnitY);
            //        worldMatrix = Matrix.CreateTranslation(_CameraLookAt);l *Matrix.CreateRotationZ(_CameraPan);// * 

            projectionMatrix = Matrix.CreateOrthographic((float)ProjectionBounds.Width, (float)ProjectionBounds.Height, neardrawdistance, drawdistance);
            
         //   worldMatrix = Matrix.CreateRotationZ(_CameraRotation); 
        }

        #endregion


        /// <summary>
        /// Initializes the transforms used for the 3D model.
        /// </summary>
        private void InitializeTransform()
        {
            // Use the world matrix to tilt the cube along x and y axes.
            worldMatrix = Matrix.Identity; // CreateRotationX(_CameraTilt) * Matrix.CreateRotationZ(_CameraPan);

            UpdateViewMatrix();

            projectionMatrix = Matrix.CreateOrthographic((float)ProjectionBounds.Width, (float)ProjectionBounds.Height, neardrawdistance, drawdistance); 
        }

        /// <summary>
        /// Initializes the basic effect (parameter setting and technique selection)
        /// used for the 3D model.
        /// </summary>
        private void InitializeEffect()
        {
            basicEffect = new BasicEffect(GraphicsDevice, null);
         //   basicEffect.DiffuseColor = new Vector3(0.1f, 0.1f, 0.1f);
         //   basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
         //   basicEffect.SpecularPower = 5.0f;
            basicEffect.AmbientLightColor = new Vector3(1f, 1f, 1f);

            basicEffect.Projection = projectionMatrix;
            basicEffect.World = worldMatrix;
            basicEffect.View = viewMatrix;
            basicEffect.Projection = projectionMatrix;

            Effect effect = Content.Load<Effect>("ChannelShader");
            this.channelEffect = new ChannelEffect(effect);
            this.channelEffect.WorldMatrix = worldMatrix;
            this.channelEffect.ProjectionMatrix = projectionMatrix;
            this.channelEffect.ViewMatrix = viewMatrix;
        }
        
        public ViewerControl() : base()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Objects used to render screenshots
        /// </summary>
        Texture2D Screenshot;

        /// <summary>
        /// Objects used to render screenshots
        /// </summary>
        RenderTarget2D ScreenshotRenderTarget;

        protected override void Initialize()
        {
            if (!DesignMode)
            {
               
                vertexDeclaration = new VertexDeclaration(
                    GraphicsDevice,
                    VertexPositionNormalTexture.VertexElements
                    );

                InitializeTransform();
                InitializeEffect();


                this.LineManager.Init(GraphicsDevice, Content);
            }
        }

        public GridVector2 ScreenToWorld(int X, int Y)
        {
            //The screen coordinates used by Windows and XNA put the Y origin at the top and bottom of the screen
            Y = this.VisibleScreenRect.Height - Y; 
            Rectangle bounds = ProjectionBounds;
            float XScale = X / (float)this.VisibleScreenRect.Width;
            float YScale = Y / (float)this.VisibleScreenRect.Height;

            Vector3 position = new Vector3(bounds.X + (bounds.Width * XScale), bounds.Y + (bounds.Height * YScale), 0);
            position.X -= bounds.Width / 2;
            position.Y -= bounds.Height / 2;
//            Matrix Rot = Matrix.CreateRotationZ(MathHelper.ToRadians(CameraRotation));
            //Vector3 RotPosition = Vector3.Transform(position, Rot);
            Vector3 RotPosition = position; 
            RotPosition.X += CameraLookAt.X;
            RotPosition.Y += CameraLookAt.Y;


            return new GridVector2(RotPosition.X, RotPosition.Y); 

      //      float scale = drawdistance / _CameraDistance;
      //      return new Vector3(X * scale, -Y * scale, 0); 
        }

        public GridVector2 WorldToScreen(double X, double Y)
        {
            Vector3 p = GraphicsDevice.Viewport.Project(new Vector3((float)X, (float)Y, 0), projectionMatrix, viewMatrix, worldMatrix);

            return new GridVector2(p.X, p.Y); 
        }

        /// <summary>
        /// Unused legacy function from when we supported screen rotation
        /// </summary>
        /// <returns></returns>
        public GridQuad VisibleQuad()
        {
            if (GraphicsDevice == null)
                return new GridQuad(new GridVector2(0, 0), 10, 10); 

            //For debugging
            const int offset = 0;

            GridVector2 TopLeft = ScreenToWorld(offset,offset);
            GridVector2 BottomRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, GraphicsDevice.Viewport.Height - offset);
            GridVector2 BottomLeft = ScreenToWorld(offset, GraphicsDevice.Viewport.Height - offset);

            GridVector2 TopRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, offset);

            return new GridQuad(BottomLeft, BottomRight, TopLeft, TopRight); 
        }

        public GridRectangle VisibleBounds()
        {
            if (GraphicsDevice == null)
                return new GridRectangle(0, 0, 10, 10);

             //For debugging
            const int offset = 0;

            //GridVector2 TopLeft = ScreenToWorld(offset, offset);
            //GridVector2 BottomLeft = ScreenToWorld(offset, GraphicsDevice.Viewport.Height - offset);
            //GridVector2 TopRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, offset);
            GridVector2 BottomLeft = ScreenToWorld(offset, VisibleScreenRect.Height - offset);
            GridVector2 TopRight = ScreenToWorld(VisibleScreenRect.Width - offset, offset);
            GridRectangle rect = new GridRectangle(BottomLeft, TopRight.X - BottomLeft.X, TopRight.Y - BottomLeft.Y);
            return rect; 
        }

        public GridRectangle RenderTargetBounds()
        {
            if (GraphicsDevice == null)
                return new GridRectangle(0, 0, 10, 10);

            //For debugging
            const int offset = 0;

            //GridVector2 TopLeft = ScreenToWorld(offset, offset);
            //GridVector2 BottomLeft = ScreenToWorld(offset, GraphicsDevice.Viewport.Height - offset);
            //GridVector2 TopRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, offset);
            GridVector2 BottomLeft = ScreenToWorld(offset, GraphicsDevice.Viewport.Height - offset);
            GridVector2 TopRight = ScreenToWorld(GraphicsDevice.Viewport.Width - offset, offset);
            GridRectangle rect = new GridRectangle(BottomLeft, TopRight.X - BottomLeft.X, TopRight.Y - BottomLeft.Y);
            return rect;
        }

        /// <summary>
        /// The camera distance required for a specified downsample level
        /// </summary>
        /// <param name="downsample"></param>
        /// <returns></returns>
        public float CalculateDistance(float downsample)
        {
            return CalculateDistance(downsample, ClientSize.Width); 
        }

        /// <summary>
        /// The camera distance required for a specified downsample level
        /// </summary>
        /// <param name="downsample"></param>
        /// <returns></returns>
        public float CalculateDistance(float downsample, int ClientWidth)
        {
            double DesiredProjectionWidth = (double)ClientWidth * downsample;

            float DesiredCameraDistance = (float)((DesiredProjectionWidth * this.drawdistance) / MaxImageDimension);

            return DesiredCameraDistance; 
        }

        public double Downsample
        {
            get
            {
                double DownSample = ProjectionBounds.Width / (double)ClientSize.Width;
                return DownSample;
            }
        }

        public double CalculateDownsample()
        {
            double DownSample = ProjectionBounds.Width / (double)ClientSize.Width;

            DownSample = Math.Log(DownSample, 2);
            DownSample = Math.Floor(DownSample);
            DownSample = Math.Pow(2, DownSample);

            if (DownSample < 1)
                DownSample = 1;
            else if (DownSample > 64)
                DownSample = 64;

            return DownSample; 
        }

        /// <summary>
        /// Takes a capture and sends it to the clipboard
        /// </summary>
        protected Byte[] CaptureArea(GridRectangle Rect, float Downsample)
        {
            Debug.Assert((Rect.Width / Downsample) < 4096 && (Rect.Height / Downsample) < 4096);

            Debug.Assert(this.PaintCallRefCount == 0);

            // Initialize our RenderTarget
            ScreenshotRenderTarget = new RenderTarget2D(
                GraphicsDevice,
                (int)Math.Round(Rect.Width / Downsample),
                (int)Math.Round(Rect.Height / Downsample),
                0,
                SurfaceFormat.Rgba64);

            Vector3 OriginalCameraLookAt = this.CameraLookAt;
            float OriginalCameraDistance = this.CameraDistance;

            //Move camera to center of capture area
            this.VisibleScreenRect = new Rectangle(0, 0, 
                                                (int)Math.Round(Rect.Width / Downsample),
                                                (int)Math.Round(Rect.Height / Downsample)); 
            this.CameraLookAt = new Vector3((float)Rect.Center.X, (float)Rect.Center.Y, 0f);
            this.CameraDistance = CalculateDistance(Downsample, (int)Math.Round(Rect.Width / Downsample)); 
            

            //Adjust the camera to the correct zoom. 

            GraphicsDevice.SetRenderTarget(0, ScreenshotRenderTarget);

            Draw();
            
            
            GraphicsDevice.SetRenderTarget(0, null);

            Screenshot = ScreenshotRenderTarget.GetTexture();
            ScreenshotRenderTarget.Dispose();

            Microsoft.Xna.Framework.Graphics.PackedVector.Rgba64[] ImageBuffer = new Microsoft.Xna.Framework.Graphics.PackedVector.Rgba64[Screenshot.Width * Screenshot.Height];
            Screenshot.GetData<Microsoft.Xna.Framework.Graphics.PackedVector.Rgba64>(ImageBuffer);

            Byte[] GreyscaleBuffer = new Byte[Screenshot.Width * Screenshot.Height];
            for (int i = 0; i < Screenshot.Width * Screenshot.Height; i++)
            {
                GreyscaleBuffer[i] = (Byte)(ImageBuffer[i].ToVector4().X * 255);
            }
            
            /*

            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(Screenshot.Width, Screenshot.Height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0,0,bmp.Width, bmp.Height); 

            Texture2D greyScreenshot = new Texture2D(GraphicsDevice, Screenshot.Width, Screenshot.Height, 1, TextureUsage.None, SurfaceFormat.Luminance8);
            greyScreenshot.SetData<Byte>(GreyscaleBuffer); 

            Screenshot.Save("Screenshot.png", ImageFileFormat.Png);
            greyScreenshot.Save("GreyScreenshot.png", ImageFileFormat.Png);
            */
            
         //   greyScreenshot.Dispose(); 

            Screenshot.Dispose();

            

            //Restore camera position
            this.VisibleScreenRect = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height); 
            this.CameraLookAt = OriginalCameraLookAt;
            this.CameraDistance = OriginalCameraDistance;

            return GreyscaleBuffer;
        }
        
        protected override void Draw()
        {
            GraphicsDevice.RenderState.DepthBufferEnable = false;
            GraphicsDevice.RenderState.AlphaBlendEnable = true;
            GraphicsDevice.RenderState.AlphaBlendOperation = BlendFunction.Add;

            
            GraphicsDevice.RenderState.AlphaSourceBlend = Blend.SourceAlpha;
            GraphicsDevice.RenderState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
            GraphicsDevice.RenderState.SourceBlend = Blend.SourceAlpha;
            GraphicsDevice.RenderState.DestinationBlend = Blend.InverseSourceAlpha;
             
            
            GraphicsDevice.SamplerStates[0].AddressU = TextureAddressMode.Wrap;
            GraphicsDevice.SamplerStates[0].AddressV = TextureAddressMode.Wrap;

            GraphicsDevice.Clear(Color.DarkBlue);
            GraphicsDevice.RenderState.CullMode = CullMode.None;
            //          VertexDeclaration basicEffectVertexDeclaration = new VertexDeclaration(graphics.GraphicsDevice, VertexPositionColoredNormal.VertexElements);

            GraphicsDevice.VertexDeclaration = vertexDeclaration;

            //Enables some basic effect characteristics, such as vertex coloring an ddefault lighting.
            basicEffect.Projection = projectionMatrix; 
            basicEffect.View = viewMatrix;
            basicEffect.World = worldMatrix;
            basicEffect.CommitChanges();

            channelEffect.WorldMatrix = worldMatrix;
            channelEffect.ProjectionMatrix = projectionMatrix;
            channelEffect.ViewMatrix = viewMatrix;
            channelEffect.CommitChanges(); 

            if (this.spriteBatch == null || this.spriteBatch.GraphicsDevice.IsDisposed)
            {
                IGraphicsDeviceService IService = this.Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
                if (IService != null)
                {
                    spriteBatch = new SpriteBatch(IService.GraphicsDevice);
                    fontArial = Content.Load<SpriteFont>(@"Arial");
                }
            }

            GridRectangle Bounds = VisibleBounds();
            double DownSample = CalculateDownsample();

#if !DEBUG
            try
            {
#endif
                //Since draw can be called from other methods than paint calls,
                //such as screencaptures, increment the PaintCallRefCount here
                PaintCallRefCount++;
                // Draw the control using the GraphicsDevice.
                Draw(GraphicsDevice, Bounds, DownSample);
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

        protected virtual void Draw(GraphicsDevice graphicsDevice, GridRectangle Bounds, double DownSample)
        {

        }

        protected override void OnResize(EventArgs e)
        {
            this.VisibleScreenRect = new Rectangle(ClientRectangle.X,
                                                ClientRectangle.Y,
                                                ClientRectangle.Width,
                                                ClientRectangle.Height);

            // this.GraphicsDevice.Viewport.Width = ClientSize.Width;
            // GraphicsDevice.Viewport.Height = ClientSize.Height;
            Rectangle ProjRect = ProjectionBounds;
            projectionMatrix = Matrix.CreateOrthographic((float)ProjRect.Width, (float)ProjRect.Height, neardrawdistance, drawdistance); 

            base.OnResize(e);

            this.Refresh(); 
        }

        private void InitializeComponent()
        {
            

            this.SuspendLayout();
            
            this.ResumeLayout(false);

        }
    }
}
