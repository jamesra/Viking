using Jotunn.Common;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Viking.VolumeModel;


namespace Viking.VolumeViewModel
{
    /// <summary>
    /// This class encapsulates a mapping of tiles into space, a bounding region, and a downsample level.
    /// Using these three properties it dynamically updates a collection of TileViewModels for display
    /// </summary>
    public class TileMappingViewModel : DependencyObject
    {
        private readonly MappingBase TileMapping = null;

        private TilePyramid TilePyramid = null;

        private static TileViewModelCache TileCache = new TileViewModelCache();

        private static readonly DependencyProperty TilesProperty;
        public ObservableCollection<TileViewModel> Tiles
        {
            get { return (ObservableCollection<TileViewModel>)this.GetValue(TilesProperty); }
            set { this.SetValue(TilesProperty, value); }
        }

        private static readonly DependencyProperty VisibleRegionProperty;
        
         /// <summary>
        /// The visible area
        /// </summary>
        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)this.GetValue(VisibleRegionProperty); }
            set { this.SetValue(VisibleRegionProperty, value); }
        }

        public bool TryVolumeToSection(Point input, out Point output)
        {
            output = new Point();
            if (this.TileMapping == null)
                return false;

            Geometry.GridVector2 outputGV;
            bool success = this.TileMapping.TryVolumeToSection(new Geometry.GridVector2(input.X, input.Y), out outputGV);

            if (success)
            {
                output = new Point(outputGV.X, outputGV.Y);
            }

            return success;
        }

        public bool TrySectionToVolume(Point input, out Point output)
        {
            output = new Point();
            if (this.TileMapping == null)
                return false; 

            Geometry.GridVector2 outputGV;
            bool success = this.TileMapping.TrySectionToVolume(new Geometry.GridVector2(input.X, input.Y), out outputGV);

            if(success)
            {
                output = new Point(outputGV.X, outputGV.Y);
            }

            return success;
        }

        /// <summary>
        /// The center of the current view
        /// </summary>
        public Point ViewCenter
        {
           get 
           {
               return VisibleRegion.Center; 
           }
            
           set
           {
               Rect visRect = VisibleRegion.VisibleRect;
               visRect.X = value.X - (visRect.Width / 2);
               visRect.Y = value.Y - (visRect.Height / 2);
               VisibleRegion = new VisibleRegionInfo(visRect, VisibleRegion.Downsample);
           }
        }

        /// <summary>
        /// The boundary of the entire tile mapping
        /// </summary>
        public Rect Bounds
        {
            get
            {
                Geometry.GridRectangle grect = this.TileMapping.ControlBounds;
                Rect bounds = new Rect(grect.Left, grect.Bottom, grect.Width, grect.Height);
                return bounds; 
            }
        }

        public override string ToString()
        {
            if(TileMapping != null)
                return this.TileMapping.ToString();

            return "Empty Tile Mapping";
        } 
        
        static TileMappingViewModel()
        {
            TileMappingViewModel.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                                   typeof(VisibleRegionInfo),
                                                                                   typeof(TileMappingViewModel),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                                                 FrameworkPropertyMetadataOptions.AffectsRender,
                                                                                                                 new PropertyChangedCallback(OnVisibleRegionChanged)));

            TileMappingViewModel.TilesProperty = DependencyProperty.Register("Tiles",
                                                                                  typeof(ObservableCollection<TileViewModel>),
                                                                                  typeof(TileMappingViewModel),
                                                                                  new FrameworkPropertyMetadata(null,
                                                                                                                FrameworkPropertyMetadataOptions.AffectsRender));
        }

        //private static int NextID = 0;
        //private readonly int ID;

        public TileMappingViewModel()
        {
            this.TileMapping = null;
            //this.ID = TileMappingViewModel.NextID++;
            this.VisibleRegion = new VisibleRegionInfo(0, 0, 10000, 10000, 256);
            this.Tiles = new ObservableCollection<TileViewModel>();
        }
        
        public TileMappingViewModel(MappingBase mapping)
        {
            //this.ID = TileMappingViewModel.NextID++;
            this.Tiles = new ObservableCollection<TileViewModel>();
            this.TileMapping = mapping;
            
            
            this.VisibleRegion = new VisibleRegionInfo(
                                            new Rect(mapping.ControlBounds.Left, mapping.ControlBounds.Bottom, mapping.ControlBounds.Width, mapping.ControlBounds.Height),//Set the visible rect to include the entire mappable area
                                            mapping.AvailableLevels[mapping.AvailableLevels.Length - 1]); //Set the downsample to the highest possible value
                
            
        }


        protected void VisibleRegionChanged(VisibleRegionInfo newValue, VisibleRegionInfo oldValue)
        {
            //if (oldValue.Contains(newValue) == false)
            {
                System.Diagnostics.Trace.WriteLine("VisibleRegionChanged"); 
                UpdateTiles(newValue.VisibleRect, newValue.Downsample);
            }
        }
        
        private static void OnVisibleRegionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            TileMappingViewModel tileMapping = o as TileMappingViewModel;

            System.Diagnostics.Trace.WriteLine("OnVisibleRegionChanged()");
            tileMapping.VisibleRegionChanged((VisibleRegionInfo)e.NewValue, (VisibleRegionInfo)e.OldValue);
        }
        
        protected void UpdateTiles(Rect visRect, double downsample)
        {
            System.Diagnostics.Trace.WriteLine("UpdateTiles()");

            if (visRect.Width == 0 || visRect.Height == 0)
                return; 

            //Find which tiles changed
            //Rect visRect = this.VisibleRect;
            Geometry.GridRectangle visibleGridRect = new Geometry.GridRectangle(visRect.Left, visRect.Right, visRect.Top, visRect.Bottom);
            TilePyramid oldTilePyramid = this.TilePyramid;

            Trace.WriteLine(TileMapping.ToString());

            TilePyramid newTilePyramid = TileMapping.VisibleTiles(visibleGridRect, downsample);

            if (oldTilePyramid != null)
            {
                if (false == oldTilePyramid.Bounds.Intersects(newTilePyramid.Bounds))
                    Tiles.Clear();
                else
                {
                    //Might be able to really speed up culling dead tiles with QuadTrees

                    //Sort out which of the old tiles are no longer visible
                    for (int iTile = 0; iTile < Tiles.Count; iTile++)
                    {
                        TileViewModel t = Tiles[iTile];
                        if (false == visibleGridRect.Intersects(t.Bounds))
                        {
                            Tiles.RemoveAt(iTile);
                            iTile--;
                        }
                        else
                        {
                            //SortedDictionary<string, Tile> tilesForLevel = newTilePyramid.GetTilesForLevel(t.Downsample);
                        }
                    }
                }
            } 

            //Create tiles which are new
            for(int iLevel = newTilePyramid.AvailableLevels.Length -1; iLevel >= 0; iLevel--)
            //for (int iLevel = 0; iLevel >= 0; iLevel--)
            //for (int iLevel = 0; iLevel < newTilePyramid.AvailableLevels.Length; iLevel++)
            {
                int level = newTilePyramid.AvailableLevels[iLevel];
                SortedDictionary<string, Tile> tiles = newTilePyramid.GetTilesForLevel(level);
                bool IgnoreOldPyramid = oldTilePyramid == null;

                //Don't check the old pyramid if it doesn't have the downsample level we are looking at
                if (false == IgnoreOldPyramid)
                {
                    if (oldTilePyramid.AvailableLevels.Length == 0)
                    {
                        IgnoreOldPyramid = true;
                    }
                    else
                    {
                        IgnoreOldPyramid = level < oldTilePyramid.AvailableLevels.Min();

                        if (false == IgnoreOldPyramid)
                        {
                            IgnoreOldPyramid = level > oldTilePyramid.AvailableLevels.Max();
                        }
                    }
                }

                foreach (Tile t in tiles.Values)
                {
                    //We already loaded this one
          //          if (!IgnoreOldPyramid && oldTilePyramid.Bounds.Intersects(t.Bounds))
          //              continue;
          //          else
                    {
                        TileViewModel tileViewModel = null;
                       
                        tileViewModel = TileCache.Fetch(t.UniqueKey);
                        if (tileViewModel == null)
                        {
                            tileViewModel = new TileViewModel(t, TileMapping.TilePath);
                           
                            TileCache.Add(t.UniqueKey, tileViewModel);
                        }
//                        else
 //                       {
                            //Don't add it to the collection again
                            if (Tiles.Contains(tileViewModel))
                                continue; 

                        //}
                    
                        this.Tiles.Add(tileViewModel);
                    }
                }
            }

            //This is horrible, but I do it to trigger the Dependancy property listeners because 3D collections don't subribe to the collection change notifications
            ObservableCollection<TileViewModel> newTiles = new ObservableCollection<TileViewModel>(this.Tiles);
            this.Tiles = newTiles; 

            TilePyramid = newTilePyramid;
        }

    }
}
