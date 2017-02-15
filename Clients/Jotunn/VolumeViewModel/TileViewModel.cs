using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.VolumeModel; 
using System.Windows.Data;
using System.Windows; 
using System.Windows.Media;
using System.Windows.Media.Media3D; 
using System.Windows.Media.Imaging; 
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading; 
using Geometry;
using System.Diagnostics; 


namespace Viking.VolumeViewModel
{
    /// <summary>
    /// Represents Tiles to the view
    /// </summary>
    public class TileViewModel : DependencyObject, IComparable<TileViewModel>, IComparable<Tile>
    {
        protected readonly Tile Tile; 
       
        public string UniqueKey
        {
            get { return Tile.UniqueKey; }
        }

        public override string ToString()
        {
            return Tile.UniqueKey; 
        }
        
        public int Downsample
        {
            get { return Tile.Downsample; }
        }

        protected readonly string TilePath; 


        /// <summary>
        /// Updated when the Mesh changes
        /// </summary>
        GridRectangle _Bounds; 
        public GridRectangle Bounds
        {
            get
            {
                return _Bounds; 
            }
        }
        
        #region Dependency Properties

        private static readonly DependencyProperty ModelProperty;
        public GeometryModel3D Model
        {
            get { return (GeometryModel3D)GetValue(TileViewModel.ModelProperty); }
            set { SetValue(TileViewModel.ModelProperty, value); }
        }

        private static readonly DependencyProperty TextureProperty;
        public DiffuseMaterial Texture
        {
            get { return (DiffuseMaterial)GetValue(TileViewModel.TextureProperty); }
            set { SetValue(TileViewModel.TextureProperty, value); }
        }

        private static readonly DependencyProperty MeshProperty;
        public MeshGeometry3D Mesh
        {
            get { return (MeshGeometry3D)GetValue(TileViewModel.MeshProperty); }
            set { SetValue(TileViewModel.MeshProperty, value); }
        }

        #endregion

        /*
        // Typical implementation of CreateInstanceCore
        protected override Freezable CreateInstanceCore()
        {
            return new TileViewModel();
        }
         */

        private static DiffuseMaterial LoadingTexture = null;

        EventHandler OnDownloadCompletedEventHandler = null;
        EventHandler<ExceptionEventArgs> OnDownloadFailedEventHandler = null;
        EventHandler<ExceptionEventArgs> OnDecodeFailedEventHandler = null;

        static TileViewModel()
        {
            #if DEBUG
            LoadingTexture = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(32, 128, 128, 128)));
            #else
            LoadingTexture = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));
            #endif
            LoadingTexture.Freeze();

            TileViewModel.ModelProperty = DependencyProperty.Register("Model",
                                                                                   typeof(GeometryModel3D),
                                                                                   typeof(TileViewModel),
                                                                                   new FrameworkPropertyMetadata(new GeometryModel3D(),
                                                                                                                 FrameworkPropertyMetadataOptions.AffectsRender));

            TileViewModel.TextureProperty = DependencyProperty.Register("Texture",
                                                                                   typeof(DiffuseMaterial),
                                                                                   typeof(TileViewModel),
                                                                                   new FrameworkPropertyMetadata(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(32, 128, 128, 128))),
                                                                                                                 FrameworkPropertyMetadataOptions.AffectsRender,
                                                                                                                 new PropertyChangedCallback(OnTextureChanged))
                                                                                                                 );

            //This probably doesn't need to be a dependancy object since it is read only as soon as tile is set
            TileViewModel.MeshProperty = DependencyProperty.Register("Mesh",
                                                                                   typeof(MeshGeometry3D),
                                                                                   typeof(TileViewModel),
                                                                                   new FrameworkPropertyMetadata(new MeshGeometry3D(),
                                                                                                                 FrameworkPropertyMetadataOptions.AffectsRender,
                                                                                                                 new PropertyChangedCallback(OnMeshChanged))); 
        }

        private static System.Windows.Threading.Dispatcher _MainUIDispatcher = null;
        
        public TileViewModel(Tile t, string TilePath)
        {
            OnDownloadCompletedEventHandler = new EventHandler(DownloadCompleted);
            OnDownloadFailedEventHandler = new EventHandler<ExceptionEventArgs>(DownloadFailed);
            OnDecodeFailedEventHandler = new EventHandler<ExceptionEventArgs>(DecodeFailed); 

            if (_MainUIDispatcher == null)
                _MainUIDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher; 

            this.TilePath = TilePath + '/';
            this._Bounds = t.Bounds; 
            
            CreateModel();
            this.Tile = t;

            //CreateMesh(value);
            

            
            System.Threading.Tasks.Task create_mesh_task = new System.Threading.Tasks.Task(() => CreateMesh(this.Tile));
            create_mesh_task.Start();
            //Thread mesh_worker = new Thread(new ThreadStart(CreateMesh(Tile)));
            //mesh_worker.Start();

            //Action<Tile> createMeshAction = new Action<Tile>(CreateMesh);
            //createMeshAction.BeginInvoke(Tile, null, null);
            //System.Threading.Tasks.Task t = new System.Threading.Tasks.(createMeshAction); 

            //    Debug.WriteLine("TileViewModel: " + t.TextureFullPath);

            //System.Threading.Tasks.Task create_texture_task = new System.Threading.Tasks.Task(LoadTexture);
            //create_texture_task.Start();
            Thread worker = new Thread(new ThreadStart(LoadTexture));
            worker.Start();

        }

        protected void CreateModel()
        {
            GeometryModel3D model = new GeometryModel3D();
                                    
            Binding meshBinding = new Binding();
            meshBinding.Source = this;
            meshBinding.Path = new PropertyPath("Mesh");
            meshBinding.Mode = BindingMode.OneWay;
            BindingOperations.SetBinding(model, GeometryModel3D.GeometryProperty, meshBinding);


            Binding matBinding = new Binding();
            matBinding.Source = this;            
            matBinding.Path = new PropertyPath("Texture");
            matBinding.Mode = BindingMode.OneWay;
            BindingOperations.SetBinding(model, GeometryModel3D.MaterialProperty, matBinding);
            BindingOperations.SetBinding(model, GeometryModel3D.BackMaterialProperty, matBinding);

            this.Model = model;
        }


        protected void CreateMesh(Tile t)
        {
            MeshGeometry3D mesh = null;
            
            if (t == null)
            {
                _MainUIDispatcher.BeginInvoke(new Action<MeshGeometry3D>(delegate(MeshGeometry3D NewMesh) { this.Mesh = new MeshGeometry3D(); }), mesh);
                return;
            }

            //Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Normal,
            //(Action)(() => { mesh = new MeshGeometry3D(); }));
            mesh = new MeshGeometry3D();

            foreach(PositionNormalTextureVertex v in t.Verticies)
            {
                mesh.Positions.Add(new Point3D(v.Position.X, v.Position.Y, v.Position.Z));
                mesh.Normals.Add(new Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
                mesh.TextureCoordinates.Add( new Point(v.Texture.X, v.Texture.Y));
            }

            mesh.TriangleIndices = new Int32Collection(t.TriangleIndicies);

            mesh.Freeze();

            //Mesh = mesh; 
            _MainUIDispatcher.BeginInvoke(new Action<MeshGeometry3D>(delegate(MeshGeometry3D NewMesh) { this.Mesh = NewMesh; }),
                                                                                                        DispatcherPriority.Background,
                                                                                                        mesh);

        }
        
    //    protected void LoadTexture(Tile t)
        protected void LoadTexture()
        {
     //       ImageBrush i = new ImageBrush();
    //        i.ViewportUnits = BrushMappingMode.Absolute; 
            if (Tile == null)
                return;
            
            //Check for the image brush in memory already, if it is not there request it to load
            ImageBrush imageBrush = Global.BrushCache.Fetch(Tile.TextureFullPath) as ImageBrush;
            if (imageBrush != null)
            {
                DiffuseMaterial mat = new DiffuseMaterial(imageBrush);
                mat.Freeze();

                _MainUIDispatcher.BeginInvoke(new Action<DiffuseMaterial>(delegate(DiffuseMaterial material) { Texture = material; }), mat);
 //               Texture = mat;
            }
            else
            {
                //This puts a light grey overlay on tiles which have a higher resolution which has not loaded yet
                _MainUIDispatcher.BeginInvoke(new Action<DiffuseMaterial>(delegate(DiffuseMaterial material) { Texture = material;}),  TileViewModel.LoadingTexture);
                
                BitmapImage bmp = new BitmapImage();

                /*
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render,
                                                   (Action)(() => { bmp = new BitmapImage(); }));
                                                   */
                bmp.BeginInit();
                
                bmp.UriSource = new Uri(this.TilePath + Tile.TextureFullPath);
                
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriCachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
                bmp.DownloadCompleted += OnDownloadCompletedEventHandler;
                bmp.DownloadFailed += OnDownloadFailedEventHandler;
                bmp.DecodeFailed += OnDecodeFailedEventHandler;
                bmp.EndInit();
                
                if (bmp.IsDownloading == false)
                    DownloadCompleted(bmp, new EventArgs());
                else
                {
                    // Spin waiting for the bitmap to finish loading...
                    Dispatcher.Run(); 
                }
            }
                        
      //      bmp.Freeze(); 
      //      i.ImageSource = bmp;
     //       i.Freeze(); 

      //      return i; 
        }

        /// <summary>
        /// Create a transparent texture when download fails
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DownloadFailed(object sender, ExceptionEventArgs e)
        {
            BitmapImage bmp = sender as BitmapImage;
            bmp.DownloadCompleted -= OnDownloadCompletedEventHandler;
            bmp.DownloadFailed -= OnDownloadFailedEventHandler;

            DiffuseMaterial mat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(0,128,0,0)));

            mat.Freeze();

            _MainUIDispatcher.BeginInvoke(new Action<DiffuseMaterial>(delegate(DiffuseMaterial material) { Texture = material; }),
                                                    DispatcherPriority.Background,
                                                    mat);
            //Texture = mat;

            System.Diagnostics.Trace.WriteLine("Failed to load: " + bmp.UriSource.ToString());

            if (Dispatcher.CurrentDispatcher != _MainUIDispatcher)
                Dispatcher.CurrentDispatcher.InvokeShutdown();
        }

        /// <summary>
        /// Create a transparent texture when decode fails
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DecodeFailed(object sender, ExceptionEventArgs e)
        {
            BitmapImage bmp = sender as BitmapImage;
            bmp.DownloadCompleted -= OnDownloadCompletedEventHandler;
            bmp.DownloadFailed -= OnDownloadFailedEventHandler;
            DiffuseMaterial mat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(0, 0, 128, 0)));
            mat.Freeze();

            _MainUIDispatcher.BeginInvoke(new Action<DiffuseMaterial>(delegate(DiffuseMaterial material) { Texture = material; }),
                                          DispatcherPriority.Background,
                                          mat);
            //Texture = mat;

            System.Diagnostics.Trace.WriteLine("Failed to decode: " + bmp.UriSource.ToString());

            if (Dispatcher.CurrentDispatcher != _MainUIDispatcher)
                Dispatcher.CurrentDispatcher.InvokeShutdown();
        }

        protected void DownloadCompleted(object sender, EventArgs e)
        {
            BitmapImage bmp = sender as BitmapImage;
            bmp.DownloadCompleted -= OnDownloadCompletedEventHandler;
            bmp.DownloadFailed -= OnDownloadFailedEventHandler;
            bmp.Freeze();

            ImageBrush i = new ImageBrush();
            i.ViewportUnits = BrushMappingMode.Absolute;

            i.ImageSource = bmp;
            i.Freeze();

            Global.BrushCache.Add(this.Tile.TextureFullPath, i);

            DiffuseMaterial mat = new DiffuseMaterial(i);
            mat.Freeze();

            _MainUIDispatcher.BeginInvoke(new Action<DiffuseMaterial>(delegate(DiffuseMaterial material) { Texture = material; }),
                                          DispatcherPriority.Background,
                                          mat);
            //Texture = mat;

            //System.Diagnostics.Trace.WriteLine("Loaded: " + bmp.UriSource.ToString());

            if (Dispatcher.CurrentDispatcher != _MainUIDispatcher)
                Dispatcher.CurrentDispatcher.InvokeShutdown();
        }
        
        
        protected static void OnTextureChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
//            TileViewModel tileViewModel = o as TileViewModel;
        }
        
        protected static void OnMeshChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            TileViewModel tileViewModel = o as TileViewModel;
            tileViewModel.MeshChanged(e.NewValue as MeshGeometry3D);
        }


        protected void MeshChanged(MeshGeometry3D newMesh)
        {
            if (newMesh == null)
                _Bounds = new GridRectangle();

            Rect3D boundRect3D = newMesh.Bounds;
            _Bounds = new GridRectangle();
        }

        #region IComparable<TileViewModel> Members

        int IComparable<TileViewModel>.CompareTo(TileViewModel other)
        {
            if (other == null)
                return 1; 

            if (this.Tile == other.Tile)
                return 0;

            if (this.Tile == null)
                return -1;
            if (other.Tile == null)
                return 1;

            return Tile.UniqueKey.CompareTo(other.Tile.UniqueKey); 
        }

        #endregion

        #region IComparable<Tile> Members

        int IComparable<Tile>.CompareTo(Tile other)
        {
            if (this.Tile == null && other == null)
                return 0;

            if (this.Tile == null)
                return -1; 

            if(other == null)
                return 1;

            if (this.Tile.UniqueKey == other.UniqueKey)
                return 0;

            return Tile.UniqueKey.CompareTo(other.UniqueKey);
        }

        #endregion
    }
}
