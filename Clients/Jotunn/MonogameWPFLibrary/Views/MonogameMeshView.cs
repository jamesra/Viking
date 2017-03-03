using System;
using System.Windows;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Diagnostics; 
using Microsoft.Xna.Framework;
using Microsoft.Xna;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Framework.WpfInterop;
using MonoGame.Framework.WpfInterop.Input;
using MonogameWPFLibrary.ViewModels;
using System.Collections.ObjectModel;

namespace MonogameWPFLibrary.Views
{
    public class MeshView : WpfGame
    {
        private IGraphicsDeviceService _graphicsDeviceManager;
        private WpfKeyboard _keyboard;
        private WpfMouse _mouse;

        private static RoutedUICommand resetRotationCommand;
        private static RoutedUICommand translateCameraPositionCommand;
          
        /// <summary>
        /// Increment the center number
        /// </summary>
        public static RoutedUICommand ResetRotationCommand
        {
            get { return resetRotationCommand; }
        }

        /// <summary>
        /// Move the position of the camera
        /// </summary>
        public static RoutedUICommand TranslateCameraPositionCommand
        {
            get { return translateCameraPositionCommand; }
        }

        public MonogameCamera3D Camera
        {
            get;
            set;
        }
         

        public Geometry.GridBox BoundingBox
        {
            get { return (Geometry.GridBox)GetValue(BoundingBoxProperty); }
            protected set { SetValue(BoundingBoxPropertyKey, value); }
        }

        // Using a DependencyProperty as the backing store for View.  This enables animation, styling, binding, etc...
        protected static readonly DependencyPropertyKey BoundingBoxPropertyKey = DependencyProperty.RegisterReadOnly("BoundingBox", typeof(Geometry.GridBox), typeof(MeshView), 
                                                                                                                     new PropertyMetadata(new Geometry.GridBox(new double[] { 0, 0, 0 }, new double[] { 1, 1, 1 })));

        public static readonly DependencyProperty BoundingBoxProperty = BoundingBoxPropertyKey.DependencyProperty;
        
        public System.Collections.ObjectModel.ObservableCollection<MeshViewModel> Models
        {
            get { return (ObservableCollection<MeshViewModel>)GetValue(ModelsProperty); }
            set { SetValue(ModelsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Models.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ModelsProperty =
            DependencyProperty.Register("Models", typeof(ObservableCollection<MeshViewModel>), typeof(MeshView),
                new PropertyMetadata(new ObservableCollection<MeshViewModel>(), OnModelsChanged));

        private static void OnModelsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            MeshView view = o as MeshView;
            if (view != null)
            {
                view.BoundingBox = view.Models.Select(m => m.BoundingBox).Aggregate((a, b) => Geometry.GridBox.Union(a, b));
            }
        }

        public Matrix World
        {
            get { return (Matrix)GetValue(WorldProperty); }
            set { SetValue(WorldProperty, value); }
        }

        // Using a DependencyProperty as the backing store for World.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WorldProperty =
            DependencyProperty.Register("World", typeof(Matrix), typeof(MeshView),
                new FrameworkPropertyMetadata(Matrix.CreateTranslation(new Vector3(0, 0, 0)), FrameworkPropertyMetadataOptions.AffectsRender));
         

        static MeshView()
        {
            resetRotationCommand = new RoutedUICommand("ResetRotation", "ResetRotationCommand", typeof(MeshView));
            translateCameraPositionCommand = new RoutedUICommand("Translate camera position", "TranslateCameraPositionCommand", typeof(MeshView));
        }

        public MeshView()
        {
            this.Camera = new MonogameCamera3D();
            CommandManager.RegisterClassCommandBinding(typeof(MeshView), new CommandBinding(MeshView.ResetRotationCommand, OnResetRotation));
            CommandManager.RegisterClassCommandBinding(typeof(MeshView), new CommandBinding(MeshView.TranslateCameraPositionCommand, OnTranslateCameraPosition));
        }

        private void OnResetRotation(object sender, RoutedEventArgs e)
        {
            ResetRotation();
        }

        private void ResetRotation()
        {
            
        }

        private void OnTranslateCameraPosition(object sender, RoutedEventArgs e)
        { 

        }



        protected override void Initialize()
        {
            // must be initialized. required by Content loading and rendering (will add itself to the Services)
            _graphicsDeviceManager = new WpfGraphicsDeviceService(this);

            // wpf and keyboard need reference to the host control in order to receive input
            // this means every WpfGame control will have it's own keyboard & mouse manager which will only react if the mouse is in the control
            _keyboard = new WpfKeyboard(this);
            _mouse = new WpfMouse(this);

            Models.Add(new MeshViewModel(MonogameWPFLibrary.Models.Tetrahedron.verts, MonogameWPFLibrary.Models.Tetrahedron.edges));
            Models.Add(new MeshViewModel(MonogameWPFLibrary.Models.Tetrahedron.verts, MonogameWPFLibrary.Models.Tetrahedron.edges, new Vector3(3,0,0)));

            // must be called after the WpfGraphicsDeviceService instance was created
            base.Initialize();
        }

        private TimeSpan elapsedTime = new TimeSpan();

        protected override void Update(GameTime time)
        {
            //IsActive is set to false if part of a tab control where the tab is not selected or if the control is not visible on the user's screen
 //           if (!this.IsActive)
 //              return;

            // every update we can now query the keyboard & mouse for our WpfGame
            var mouseState = _mouse.GetState();
            var keyboardState = _keyboard.GetState();
            
            elapsedTime += time.ElapsedGameTime;
            double angle = ((double)((elapsedTime.TotalMilliseconds / 500) % 1000)) / 1000.0;
            angle *= 360; // Math.PI * 2.0;
            /*
            Matrix rotMatrix = Matrix.CreateRotationX((float)angle);

            foreach (MeshViewModel meshViewModel in Models)
            {
                meshViewModel.Rotation = rotMatrix;
            }
            */
        }

        protected override void Draw(GameTime time)
        {
            //IsActive is set to false if part of a tab control where the tab is not selected or if the control is not visible on the user's screen
            //if (!this.IsActive)
            //    return;

            _graphicsDeviceManager.GraphicsDevice.Clear(Color.Transparent);

            GraphicsDevice device = _graphicsDeviceManager.GraphicsDevice;

            RasterizerState rstate = new RasterizerState();
            rstate.CullMode = CullMode.None;
            rstate.DepthClipEnable = true;
            rstate.FillMode = FillMode.WireFrame;

            device.RasterizerState = rstate; 

            using (BasicEffect effect = new BasicEffect(device))
            {
                effect.View = Camera.View;
                effect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45), (float)device.Viewport.Width / (float)device.Viewport.Height, 0.1f, 500000f);
                effect.AmbientLightColor = Color.White.ToVector3();
                effect.VertexColorEnabled = true;

                foreach (MeshViewModel meshViewModel in Models)
                {

                    effect.World = meshViewModel.World;

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, meshViewModel.Verticies, 0, meshViewModel.Verticies.Length, meshViewModel.Edges, 0, meshViewModel.Edges.Length / 3);
                    }
                }
            }
        }
    }
}
