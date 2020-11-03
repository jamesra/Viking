using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using Geometry.Transforms;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Viking.VolumeModel
{
    /// <summary>
    /// This is the base class for transforms that use the original tiles where the number of tiles is 
    /// fixed at each resolution and the size varies
    /// </summary>
    public abstract class FixedTileCountMapping : MappingBase 
    {
        private GridRectangle? _VolumeBounds; 
        public override GridRectangle ControlBounds
        {
            get
            {
                if (_VolumeBounds.HasValue == false)
                {
                    GridRectangle bounds = Geometry.Transforms.ReferencePointBasedTransform.CalculateControlBounds(TileTransforms.Cast<ITransformControlPoints>().ToArray());
                    _VolumeBounds = new GridRectangle?(bounds);
                }

                return _VolumeBounds.Value;
            }
        }

        private GridRectangle? _SectionBounds;

        public override GridRectangle? SectionBounds
        {
            get
            {
                if (_SectionBounds.HasValue == false)
                {
                    GridRectangle bounds = Geometry.Transforms.ReferencePointBasedTransform.CalculateMappedBounds(TileTransforms.Cast<ITransformControlPoints>().ToArray());
                    _SectionBounds = new GridRectangle?(bounds);
                }

                return _SectionBounds.Value; 
            }
        }

        public override GridRectangle? VolumeBounds
        {
            get
            {
                if (_VolumeBounds.HasValue == false)
                {
                    GridRectangle bounds = Geometry.Transforms.ReferencePointBasedTransform.CalculateControlBounds(TileTransforms.Cast<ITransformControlPoints>().ToArray());
                    _VolumeBounds = new GridRectangle?(bounds);
                }

                return _VolumeBounds.Value;
            }
        }

        public override UnitsAndScale.IAxisUnits XYScale
        {
            get
            {
                return CurrentPyramid.XYScale;
            }
        }

        protected ITransform[] _TileTransforms = null;

        public virtual ITransform[] TileTransforms
        {
            get { return _TileTransforms; }
        }

        //PORT: Only used for mipmaps so we don't need to know anymore
        private Pyramid _CurrentPyramid = null; 

        /// <summary>
        /// We need to know which pyramid we are working against so we know how many levels are available
        /// </summary>
        public Pyramid CurrentPyramid { 
            get { return _CurrentPyramid; }
            set {_CurrentPyramid = value;}
        }

        public override int[] AvailableLevels
        {
            get { 
                if(_CurrentPyramid == null)
                    throw new InvalidOperationException("No image pyramid set in FixedTileCountMapping, not using mapping manager?");

                return _CurrentPyramid.GetLevels().ToArray();
            }
        }

        /// <summary>
        /// Adjust the downsample level to match the difference between the scale used in the pyramid/mapping and the default scale for the volume
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected double AdjustDownsampleForScale(double input)
        {
            if (this.CurrentPyramid.XYScale == null)
                return input;

            double relative_scale = this.CurrentPyramid.XYScale.Value / this.Section.XYScale.Value;
            return input / relative_scale;
        }


        /// <summary>
        /// Filename of local cache of transforms
        /// </summary>
        abstract public string CachedTransformsFileName
        {
            get;
        }

        internal string TileTextureFileName(int number)
        {
            Geometry.Transforms.TileTransformInfo info = ((ITransformInfo)this._TileTransforms[number]).Info as TileTransformInfo;
            if (info == null)
                return null;

            return info.TileFileName; 
        }

        internal string TileFileName(string filename, int DownsampleLevel)
        {
            
      //      string filename = this._TileTransforms[number].TileFileName;
            /*
            string filename = "";
            
            if (this.TilePrefix != null)
            {
                if(this.TilePrefix != "")                
                    filename = this.TilePrefix + '.';
            }

            //TEMP: Hack for Korenberg/Iris output.  Remove and make better
            if (this.TilePrefix != null)
                filename += number.ToString("D3");
            else
                filename += number.ToString("D4"); 

            if (TilePostfix != null)
            {
                filename += this.TilePostfix;
            }
            */

           /* PORT: The viewModel should handle current mode and path
            string tileFileName = this.Section.Path +
                                System.IO.Path.DirectorySeparatorChar + CurrentPyramid +
                                System.IO.Path.DirectorySeparatorChar + DownsampleLevel.ToString("D3") +
                                System.IO.Path.DirectorySeparatorChar + filename;
             
            */
            string tileFileName = CurrentPyramid.Path +
                                System.IO.Path.DirectorySeparatorChar + DownsampleLevel.ToString("D3") +
                                System.IO.Path.DirectorySeparatorChar + filename;

          /*  string tileFileName = DownsampleLevel.ToString("D3") +
                                  System.IO.Path.DirectorySeparatorChar + filename;
            */
            return tileFileName;
             
        }
        
        public FixedTileCountMapping(Section section, string name, string Prefix, string Postfix) :
            base(section, name, Prefix, Postfix)
        {
        }

        #region CacheIO

        protected virtual void SaveToCache()
        {
            //The corrupted memory error disappeared when I stopped using the cache.  There are also 
            //memory leak issues documented on MSDN regarding BinaryFormatters
            //return;

            using(FileStream fstream = new FileStream(CachedTransformsFileName, FileMode.Create, FileAccess.Write))
            {
                BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                
                //new System.Runtime.Serialization.Formatters.Binary.

                if(_TileTransforms != null)
                    binFormatter.Serialize(fstream, _TileTransforms);
            }
        }

        protected virtual ITransform[] LoadFromCache()
        {
            //The corrupted memory error disappeared when I stopped using the cache.  There are also 
            //memory leak issues documented on MSDN regarding BinaryFormatters
            //return null;

            ITransform[] transforms = null;

            try
            {
                using (FileStream fstream = new FileStream(CachedTransformsFileName, FileMode.Open, FileAccess.Read))
                {
                    BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                    transforms = binFormatter.Deserialize(fstream) as ITransform[];
                }
            }
            catch (Exception e)
            {
                transforms = null;
                Trace.WriteLine(string.Format("Unable to load {0} from cache", CachedTransformsFileName));
                System.IO.File.Delete(CachedTransformsFileName);
            }
            
            return transforms;
        }

        #endregion

        protected virtual TilePyramid VisibleTiles(GridRectangle VisibleBounds,
                                                GridQuad SectionVisibleBounds,
                                                double DownSample)
        {
            TilePyramid VisibleTiles = new TilePyramid(VisibleBounds); 

            double scaledDownsampleLevel = AdjustDownsampleForScale(DownSample);

            //Setup a larger boundary outside of which we release textures
            GridRectangle releaseBounds = VisibleBounds; //Tiles outside this quad will have textures released
            GridRectangle loadBounds = VisibleBounds;  //Tiles inside this quad will have textures loaded
            GridRectangle abortBounds = VisibleBounds; //Tiles outside this quad will have HTTP requests aborted
            releaseBounds.Scale(1.25 * scaledDownsampleLevel);
            loadBounds.Scale(1.1f);
            abortBounds.Scale(1.20f * scaledDownsampleLevel);

            //Get ready by loading a smaller texture in case the user scrolls this direction 
            //Once we have smaller textures then increase the quality
            //            int predictiveDownsample = DownSample * 4 > 64 ? 64 : (int)DownSample * 4;

            int roundedDownsample = NearestAvailableLevel(DownSample);
            int roundedScaledDownsample = NearestAvailableLevel(scaledDownsampleLevel);

            if (roundedDownsample == int.MaxValue || roundedScaledDownsample == int.MaxValue)
                return VisibleTiles;
           
            ITransform[] Tranforms = this.TileTransforms;
            if (TileTransforms == null)
                return VisibleTiles;

            int ExpectedTileCount = Tranforms.Length;
            List<Tile> TilesToDraw = new List<Tile>(ExpectedTileCount);
//            List<Tile> TilesToLoad = new List<Tile>(ExpectedTileCount);

            foreach (ITransform T in Tranforms)
            {
                IControlPointTriangulation T_Triangulation = T as IControlPointTriangulation;
                //If this tile has been transformed out of existence then skip it
                if (T_Triangulation.MapPoints.Length < 3)
                    continue;

                ITransformControlPoints T_ControlPoints = T as ITransformControlPoints;
                if (VisibleBounds.Intersects(T_ControlPoints.ControlBounds))
                {
                    //   bool LoadOnly = false; 
                    ITransformInfo T_Info = T as ITransformInfo;
                    TileTransformInfo info = T_Info.Info as TileTransformInfo; 
                    string name = TileFileName(info.TileFileName, roundedScaledDownsample);
                    /*
                    if (SectionVisibleBounds != null)
                    {
                        GridRectangle MosaicPosition = new GridRectangle(T.mapPoints[0].ControlPoint, T.ImageWidth, T.ImageHeight);
                        if (SectionVisibleBounds.Contains(MosaicPosition) == false)
                        {
                            name = TileFileName(T.Number,predictiveDownsample);
                            LoadOnly = true; 
                            continue;
                        }
                    }
                     */
                    string UniqueID = Tile.CreateUniqueKey(Section.Number, Name, CurrentPyramid.Name, roundedScaledDownsample, info.TileFileName);
                    Tile tile = Global.TileCache.Fetch(UniqueID);
                    if(tile == null && Global.TileCache.ContainsKey(UniqueID) == false)
                    {
                        int MipMapLevels = 1; //No mip maps
                        if (roundedScaledDownsample == this.AvailableLevels[AvailableLevels.Length - 1])
                            MipMapLevels = 0; //Generate mipmaps for lowest res texture

                        //First create a new tile
                        PositionNormalTextureVertex[] verticies = Tile.CalculateVerticies(T_ControlPoints, info);

                        if(T_Triangulation != null && T_Triangulation.TriangleIndicies != null)
                        {

                            tile = Global.TileCache.ConstructTile(UniqueID,
                                                                 verticies,
                                                                 T_Triangulation.TriangleIndicies,
                                                                 this.TilePath + '/' + name,
                                                                 name,
                                //PORT: TileCacheName(T.Number, roundedDownsample),
                                                                 this.Name,
                                                                 roundedScaledDownsample,
                                                                 MipMapLevels);//T.ImageHeight * T.ImageWidth / roundedDownsample);
                        }
                    }
                    
                    if(tile != null)
                        VisibleTiles.AddTile(roundedDownsample, tile);
                     
                    TilesToDraw.Add(tile);
                }
            }

            return VisibleTiles;
        }
    }
}
