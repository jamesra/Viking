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
        private GridRectangle? _Bounds; 
        public override GridRectangle Bounds
        {
            get
            {
                if (_Bounds.HasValue == false)
                {
                    GridRectangle bounds = Geometry.Transforms.ReferencePointBasedTransform.CalculateControlBounds(TileTransforms);
                    _Bounds = new GridRectangle?(bounds);
                }

                return _Bounds.Value;
            }
        }

        protected ReferencePointBasedTransform[] _TileTransforms = null;

        public virtual ReferencePointBasedTransform[] TileTransforms
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
        /// Filename of local cache of transforms
        /// </summary>
        abstract public string CachedTransformsFileName
        {
            get;
        }

        internal string TileTextureFileName(int number)
        {
            Geometry.Transforms.TileTransformInfo info = this._TileTransforms[number].Info as TileTransformInfo;
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

        protected virtual ReferencePointBasedTransform[] LoadFromCache()
        {
            //The corrupted memory error disappeared when I stopped using the cache.  There are also 
            //memory leak issues documented on MSDN regarding BinaryFormatters
            //return null;

            ReferencePointBasedTransform[] transforms = null;

            try
            {
                using (FileStream fstream = new FileStream(CachedTransformsFileName, FileMode.Open, FileAccess.Read))
                {
                    BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                    transforms = binFormatter.Deserialize(fstream) as ReferencePointBasedTransform[];
                }
            }
            catch (Exception)
            {
                transforms = null;
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

            //Setup a larger boundary outside of which we release textures
            GridRectangle releaseBounds = VisibleBounds; //Tiles outside this quad will have textures released
            GridRectangle loadBounds = VisibleBounds;  //Tiles inside this quad will have textures loaded
            GridRectangle abortBounds = VisibleBounds; //Tiles outside this quad will have HTTP requests aborted
            releaseBounds.Scale(1.25 * DownSample);
            loadBounds.Scale(1.1f);
            abortBounds.Scale(1.20f * DownSample);

            //Get ready by loading a smaller texture in case the user scrolls this direction 
            //Once we have smaller textures then increase the quality
//            int predictiveDownsample = DownSample * 4 > 64 ? 64 : (int)DownSample * 4;

            int roundedDownsample = NearestAvailableLevel(DownSample); 
           
            ReferencePointBasedTransform[] Tranforms = this.TileTransforms;
            if (TileTransforms == null)
                return VisibleTiles;

            int ExpectedTileCount = Tranforms.Length;
            List<Tile> TilesToDraw = new List<Tile>(ExpectedTileCount);
//            List<Tile> TilesToLoad = new List<Tile>(ExpectedTileCount);

            foreach (TriangulationTransform T in Tranforms)
            {
                //If this tile has been transformed out of existence then skip it
                if (T.MapPoints.Length < 3)
                    continue;

                if (VisibleBounds.Intersects(T.ControlBounds))
                {
                 //   bool LoadOnly = false; 
                    TileTransformInfo info = T.Info as TileTransformInfo; 
                    string name = TileFileName(info.TileFileName, roundedDownsample);
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
                    string UniqueID = Tile.CreateUniqueKey(Section.Number, Name, CurrentPyramid.Name, roundedDownsample, info.TileFileName);
                    Tile tile = Global.TileCache.Fetch(UniqueID);
                    if(tile == null)
                    {
                        int MipMapLevels = 1; //No mip maps
                        if (roundedDownsample == this.AvailableLevels[AvailableLevels.Length - 1])
                            MipMapLevels = 0; //Generate mipmaps for lowest res texture

                        //First create a new tile
                        PositionNormalTextureVertex[] verticies = Tile.CalculateVerticies(T, info);

                        if(T.TriangleIndicies != null)
                        {

                            tile = Global.TileCache.ConstructTile(UniqueID,
                                                                 verticies,
                                                                 T.TriangleIndicies,
                                                                 this.TilePath + '/' + name,
                                                                 name,
                                //PORT: TileCacheName(T.Number, roundedDownsample),
                                                                 this.Name,
                                                                 roundedDownsample,
                                                                 MipMapLevels);//T.ImageHeight * T.ImageWidth / roundedDownsample);
                        }
                    }
                    
                    if(tile != null)
                        VisibleTiles.AddTile(roundedDownsample, tile);

                    /*
                    if (LoadOnly)
                    {
                        TilesToLoad.Add(tile);
                        continue;
                    }
                     */

                    
                    //I used to call draw here, but when exporting frames I want to have all tiles launch threads to load thier textures and then wait.
                    //It is much faster than doing one texture at a time
                    //PORT: Removed
                    //tile.GetTexture(graphicsDevice, true);
                    TilesToDraw.Add(tile);

                    //PORT: Modified to return all tiles of lower resolution
                    
                    /*
                    //See if any tiles of alternate resolution are already loaded                    
                    //Use them if the correct resolution doesn't have a texture, otherwise abort any requests
                    bool TextureFound = tile.HasTexture; 

                    foreach(int testLevel in UI.State.DownsampleLevels)
                    {
                        if(testLevel == roundedDownsample)
                            continue;

                        string altResName = TileFileName(T.Number, testLevel);
                        Tile altResTile = Global.TileCache.GetTile(altResName, this.Name);
                        if (altResTile != null)
                        {
                            //Stop the network request if we've already got a texture
                            //TODO: Once we have a list of textures to draw, send it to TextureConstructor
                            //and have it abort textures not in the list
                            if (TextureFound)
                                altResTile.AbortRequest();
                            else
                            {
                                if (altResTile.HasTexture)
                                {
                                    TilesToDraw.Add(altResTile);
                                    TextureFound = true;
                                }
                            }
                        }
                    }
                     */
                    
                }

                /*PORT We no longer handle tiles to be loaded and the aborting tiles here
                else
                {
                    if (loadBounds.Intersects(T.CachedControlBounds))
                    {
                        string name = TileFileName(T.Number, predictiveDownsample);
                        Tile tile = Global.TileCache.GetTile(name, this.Name);
                        if(tile == null)
                        {
                            //First create a new tile
                            int MipMapLevels = 1; //No mip maps
                            if (roundedDownsample == this.AvailableLevels[AvailableLevels.Length - 1])
                                MipMapLevels = 0; //Generate mipmaps for lowest res texture

                            VertexPositionNormalTexture[] verticies = Tile.CalculateVerticies(T);
                            tile = Global.TileCache.ConstructTile(verticies,
                                                                 T.TriangleIndicies,
                                                                 name,
                                                                 TileCacheName(T.Number, predictiveDownsample),
                                                                 this.Name,
                                                                 roundedDownsample, 
                                                                 MipMapLevels,
                                                                 T.ImageHeight * T.ImageWidth / predictiveDownsample);
                        }

                        TilesToLoad.Add(tile); 
                    }
                    //Sometimes tiles can overlap both zones, so else if is used
                    else if (abortBounds.Intersects(T.CachedControlBounds) == false)
                    {
                        foreach (int testLevel in UI.State.DownsampleLevels)
                        {
                            string AbortTileName = TileFileName(T.Number, testLevel);
                            Tile tile = Global.TileCache.GetTile(AbortTileName, this.Name);
                            if (tile != null)
                            {
                                tile.AbortRequest();
                            }
                        }
                    }
                }
                 */
            }

            /*PORT Model does not load textures
            foreach (Tile tile in TilesToLoad)
            {
                //We are asking for a lower quality texture, but tile will keep any existing higher quality textures
                if (AsynchTextureLoad)
                    tile.GetTexture(graphicsDevice, true);
            }
            

            //TODO: Give a list of tiles to the TileConstructor that are OK to have outstanding load requests.
            //All other tiles will be aborted

            List<Tile> SafeTiles = new List<Tile>(TilesToLoad.Count + TilesToDraw.Count);
            SafeTiles.AddRange(TilesToLoad);
            SafeTiles.AddRange(TilesToDraw);

            Trace.WriteLine("Drawing " + TilesToDraw.Count.ToString() + " Tiles", "VolumeModel");

            return TilesToDraw.ToArray(); 
            */

            return VisibleTiles;

        }
    }
}
