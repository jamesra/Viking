using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Viking.Common.UI;

namespace Viking.VolumeModel
{
    public class Section
    {
        /// <summary>
        /// Friendly name of the section
        /// </summary>
        [Column("Name")]
        public string Name;

        /// <summary>
        /// The number of this section in the sequence
        /// </summary>
        [Column("Number")]
        public int Number;

        /// <summary>
        /// Notes embedded in the VikingXML describing this section
        /// </summary>
        [Column("Notes")]
        public string Notes = "";

        /// <summary>
        /// The path that needs to appended to the volume path to reach the section
        /// </summary>
        private string _SectionSubPath;
        public string SectionSubPath
        {
            get { return _SectionSubPath; }
        }

        /// <summary>
        /// Path to the section, including volume path 
        /// </summary>
        public string Path => $"{volume.Host}/{_SectionSubPath}";

        private ChannelInfo[] _ChannelInfo = new ChannelInfo[0];

        /// <summary>
        /// These settings describe which colors to use to render the section and it's neighbors.
        /// They override any other settings.  An empty array indicates global settings should be used.
        /// </summary>
        public ChannelInfo[] ChannelInfoArray
        {
            get { return _ChannelInfo; }
            set
            {
                if (null == value)
                {
                    _ChannelInfo = new ChannelInfo[0];
                    return;
                }

                _ChannelInfo = value;
            }
        }

        /// <summary>
        /// The volume this section belongs to
        /// </summary>
        public Volume volume;

        #region Channel & Transform Inventory & State

        /// <summary>
        /// Contains a list of all transforms that can be applied to the <Pyramid> transforms
        /// </summary>
        public List<string> PyramidTransformNames = new List<string>();

        /// <summary>
        /// Contains a list of all tilesets which are pre-transformed
        /// </summary>
        public List<string> TilesetNames = new List<string>();

        /// <summary>
        /// Name and descriptive structure of pyramids supported by the section usable by <transforms>
        /// </summary>
        public SortedList<string, Pyramid> ImagePyramids = new SortedList<string, Pyramid>();

        public string DefaultPyramid = "";
        public string DefaultTileset = "";
        public string DefaultPyramidTransform = "";

        public string DefaultChannel
        {
            get
            {
                if (String.IsNullOrEmpty(DefaultTileset))
                    return DefaultPyramid;

                return DefaultTileset;
            }
        }

        /// <summary>
        /// The names of all channels in this section
        /// </summary>
        public List<string> ChannelNames = new List<string>();

        #endregion

        /// <summary>
        /// This maps a transform name to a MappingBase object which knows how to position the individual tiles into the transform space
        /// </summary>
        public System.Collections.Generic.Dictionary<string, MappingBase> WarpedTo = new Dictionary<string, MappingBase>();

        /// <summary>
        /// This transform contains the tile transformation for mosaics (usually grid.mosaic) which will be warped into volume space
        /// </summary>
        public List<string> VolumeTransformList = new List<string>();

        public UnitsAndScale.IAxisUnits XYScale
        {
            get
            {
                return this.volume.DefaultXYScale;
            }
        }

        /// <summary>
        /// Current the section number padded with four digits.  Could be a different name, 
        /// but look at what other code would break before changing.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Number.ToString("D4");
        }

        public override int GetHashCode()
        {
            return Number;
        }


        /// <summary>
        /// This can be called to inform the section to do the math to warp the section on a separate thread in anticipation
        /// of being used in the near future
        /// </summary>
        /// <param name="transform"></param>
        public void PrepareTransform(string transform)
        {
            if (WarpedTo.ContainsKey(transform) == false)
                return;

            SectionToVolumeMapping map = WarpedTo[transform] as SectionToVolumeMapping;
            if (map == null)
                return;

            //Launch a separate thread to begin warping

            //System.Threading.ThreadStart threadDelegate = new System.Threading.ThreadStart(map.Warp);
            System.Threading.ThreadPool.QueueUserWorkItem(map.Warp);
            //System.Threading.Thread newThread = new System.Threading.Thread(threadDelegate);
            //newThread.Start();
        }


        private Section(Volume vol)
        {
            this.volume = vol;
        }

        public Section(Volume vol, string path, XElement sectionElement)
            : this(vol)
        {
            this._SectionSubPath = path;

            //reader.Read();
            //XElement sectionElement = XDocument.ReadFrom(reader) as XElement;
            Debug.Assert(sectionElement != null);

            this.Name = sectionElement.HasAttributeCaseInsensitive("name") ? sectionElement.GetAttributeCaseInsensitive("name").Value : null;
            this.Number = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(sectionElement, "number").Value);
            if (this.Name == null)
                this.Name = this.Number.ToString("D4");

            foreach (XNode node in sectionElement.Nodes())
            {
                XElement elem = node as XElement;
                if (elem == null)
                    continue;

                switch (elem.Name.LocalName.ToLower())
                {
                    case "transform":
                        //Load a transform that can be applied to <Pyramids>
                        string Name = IO.GetAttributeCaseInsensitive(elem, "name").Value;
                        string mosaicTransformPath = IO.GetAttributeCaseInsensitive(elem, "path").Value;
                        bool UseForVolume = System.Convert.ToBoolean(IO.GetAttributeCaseInsensitive(elem, "UseForVolume").Value);
                        string TilePrefix = null;
                        if (IO.GetAttributeCaseInsensitive(elem, "FilePrefix") != null)
                            TilePrefix = IO.GetAttributeCaseInsensitive(elem, "FilePrefix").Value;

                        string TilePostfix = IO.GetAttributeCaseInsensitive(elem, "FilePostfix").Value;
                        TilesToSectionMapping mapping = new TilesToSectionMapping(this,
                                                                                Name,
                                                                                this.Path,
                                                                                mosaicTransformPath,
                                                                                TilePrefix,
                                                                                TilePostfix);

                        PyramidTransformNames.Add(Name);

                        if (WarpedTo.ContainsKey(Name))
                            Trace.WriteLine("Duplicate transform found " + Name + " : " + this.Path);
                        else
                            WarpedTo.Add(Name, mapping);

                        if (UseForVolume || string.IsNullOrEmpty(DefaultPyramidTransform))
                        {
                            DefaultPyramidTransform = Name;
                        }

                        break;
                    case "pyramid":
                        //Load an image pyramid whose tiles can be warped using a <transform>
                        Name = IO.GetAttributeCaseInsensitive(elem, "name").Value;
                        //                        string Pyramidpath = GetAttributeCaseInsensitive(elem,"path").Value;

                        /*PORT: The viewmodel needs to set this
                        if (UI.State.CurrentMode.Length == 0)
                            UI.State.CurrentMode = Pyramidpath;
                        */

                        Pyramid pyramid = Pyramid.CreateFromElement(elem, this);
                        if (pyramid == null) //Do not add the pyramid if it has no levels or was invalid for some reason
                        {
                            System.Diagnostics.Trace.WriteLine(string.Format("Unable to parse TilePyramid element of Section #{0}", this.Number));
                            continue;
                        }

                        if (!this.ImagePyramids.ContainsKey(pyramid.Name))
                            this.ImagePyramids.Add(pyramid.Name, pyramid);
                        else
                        {
                            Debug.WriteLine(string.Format("Duplicate Image Pyramid Level {0}-{1}", this.Number, pyramid.Path));
                        }

                        if (DefaultPyramid == null || DefaultPyramid.Length == 0)
                            DefaultPyramid = pyramid.Name;
                        else
                        {
                            if (this.ImagePyramids[DefaultPyramid].GetLevels().Count < pyramid.GetLevels().Count)
                                DefaultPyramid = pyramid.Name;
                        }

                        ChannelNames.Add(pyramid.Name);
                        break;
                    case "tileset":
                        //Load a pre-transformed pyramid whose tiles have a fixed size
                        TileGridMapping tilegridmapping = TileGridMapping.CreateFromTilesetElement(elem, this);
                        if (tilegridmapping == null)
                        {
                            System.Diagnostics.Trace.WriteLine(string.Format("Unable to parse Tileset element of Section #{0}", this.Number));
                            continue;
                        }

                        this.AddTileset(tilegridmapping);
                        DefaultTileset = tilegridmapping.Name;

                        /*PORT: The viewmodel needs to set this
                        if (UI.State.CurrentMode.Length == 0 || UI.State.CurrentMode == "8-bit")
                            UI.State.CurrentMode = mosaicTransformPath;
                        */
                        break;
                    case "channelinfo":
                        this.ChannelInfoArray = ChannelInfo.FromXML(elem);
                        break;
                    case "notes":
                        if (elem.Value != null)
                        {
                            //string SourceFilename = IO.GetAttributeCaseInsensitive(elem, "SourceFilename").Value;

                            string NotesString = elem.Value;

                            NotesString = NotesString.Trim();
                            this.Notes = Uri.UnescapeDataString(NotesString);
                            /*
                            if (String.Compare(encodingType, "txt", true) == 0)
                            {
                                this.Notes = Uri.UnescapeDataString(elem.Value);
                            }
                            else
                            { 

                                //Convert the string from hex to bytes
                                System.Text.UTF8Encoding encoder = new UTF8Encoding();
                                this.Notes = encoder.GetString(encoder.GetBytes(NotesString));
                            }
                             */
                        }

                        break;


                }
            }
        }

        public Section(Volume vol, string path)
            : this(vol)
        {
            this._SectionSubPath = path;
            string filename = System.IO.Path.GetFileNameWithoutExtension(path);

            //We expect the first part of the filename to be a section #:
            int iSectionNumberEnd = 0;
            for (int i = 0; i < filename.Length; i++)
            {
                if (char.IsDigit(filename[i]) == false)
                    break;
                iSectionNumberEnd = i;
            }

            string sectionNumber = filename;
            if (iSectionNumberEnd + 1 < filename.Length)
                sectionNumber = filename.Remove(iSectionNumberEnd + 1);

            try
            {
                this.Number = System.Convert.ToInt32(sectionNumber);
            }
            catch (FormatException e)
            {
                throw new ArgumentException("The name of each directory in a volume must start with a number indicating which section the directory contains.\n" +
                                                     "This directory did not have a section number: " + path, e);
                /*PORT
                System.Windows.Forms.MessageBox.Show("The name of each directory in a volume must start with a number indicating which section the directory contains.\n" +
                                                     "This directory did not have a section number: " + path);
                 */


            }

            LoadLocal(path);
        }


        protected void AddTileset(TileGridMapping mapping)
        {
            WarpedTo.Add(mapping.Name, mapping);

            TilesetNames.Add(mapping.Name);
            VolumeTransformList.Add(mapping.Name);

            ChannelNames.Add(mapping.Name);
        }

        public void AddOCPTileserver(TileServerInfo info)
        {
            foreach (string channelName in info.Channels.Select(c => c.Name))
            {
                string Name = "OCP-" + channelName;
                OCPTileServerMapping mapping = new OCPTileServerMapping(this,
                                                                  Name,
                                                                  channelName,
                                                                  info.FilePrefix, info.FilePostfix,
                                                                  info.TileXDim, info.TileYDim,
                                                                  info.Host, info.CoordSpaceName);

                mapping.PopulateLevels(info.MaxLevel, info.GridXDim, info.GridYDim);

                this.ChannelNames.AddRange(info.Channels.Select(c => c.Path));
                WarpedTo.Add(mapping.Name, mapping);
                TilesetNames.Add(mapping.Name);
                VolumeTransformList.Add(mapping.Name);
                ChannelNames.Add(mapping.Name);
                DefaultTileset = mapping.Name;
            }

        }




        protected void LoadLocal(string path)
        {
            /*
            //List directories under the section, each directory name is an available tile type
            string[] fullPathModes = System.IO.Directory.GetDirectories(this.Path);

            for (int i = 0; i < fullPathModes.Length; i++)
            {
                string mode = System.IO.Path.GetFileNameWithoutExtension(fullPathModes[i]);
                AvailableTileTypes.Add(mode, mode); 
            }

            // Try to find a grid mosaic
            string[] availableMosaics = System.IO.Directory.GetFiles(path, "*.mosaic");

            foreach(string file in availableMosaics)
            {
                string Key = System.IO.Path.GetFileNameWithoutExtension(file);
                TilesToSectionMapping mapping = new TilesToSectionMapping(this, path, file);

                WarpedTo.Add(Key, mapping);

                if (file.ToLower().Contains("grid"))
                {
                    VolumeTransform = Key;
                    this._CurrentTransform = Key;
                }
            }
             */
        }


        /*
        /// <summary>
        /// Called when the section transitions from being the displayed section to being the hidden section
        /// </summary>
        public void Hide()
        {
            //TODO: Purge the tile cache when this occurs
            /*
            foreach (Tile T in this.Tiles.Values)
            {
                T.AbortRequest(); 
            }
             
        }
    */

        /// <summary>
        /// Display modes the section supports such as 8-bit, 16-bit,clahe etc...
        /// </summary>
        public IList<string> Channels
        {
            get
            {
                List<string> _channels = new List<string>(TilesetNames.Count + ImagePyramids.Count);

                _channels.AddRange(TilesetNames);
                _channels.AddRange(ImagePyramids.Keys);

                return _channels.AsReadOnly();
            }

        }

        /// <summary>
        /// Adds a new transform to the section that maps it to the volume space. Returns 
        /// true if section had a grid-refine mapping that could be mapped. Otherwise false
        /// </summary>
        /// <param name="transform">Tranformation to appy. If null is passed then a copy is made unmodified and added under the volume name</param>
        /// <param name="volumeName">Name of the transform</param>
        public MappingBase CreateSectionToVolumeMapping(ITransform transform, string SectionMapping, string UniqueName)
        {
            MappingBase mapBase = this.WarpedTo[SectionMapping];
            Debug.Assert(mapBase != null);

            MappingBase SectionToVolumeMap = null;
            if (mapBase is FixedTileCountMapping)
            {
                SectionToVolumeMap = new SectionToVolumeMapping(this,
                    UniqueName,
                    (FixedTileCountMapping)mapBase,
                    transform);
            }
            else if (mapBase is TileGridMapping)
            {
                //Mapbase is the new tilegrid system
                SectionToVolumeMap = new TileGridToVolumeMapping(this,
                    UniqueName,
                    (TileGridMapping)mapBase,
                    transform);
            }
            else if (mapBase is OCPTileServerMapping)
            {
                SectionToVolumeMap = new OCPTileServerToVolumeMapping(this, UniqueName, (OCPTileServerMapping)mapBase, transform);
            }
            else
            {
                System.Diagnostics.Debug.Fail("Unknown mapping type");
            }

            return SectionToVolumeMap;
        }

        /*
        public void Draw(MappingBase Mapping,
                         ChannelEffect channelEffect,
                         Effect basicEffect,
                         GridRectangle VisibleBounds,
                         double DownSample,
                         bool AsynchTextureLoad)
        {
       
            Tile[] TilesToDraw = Mapping.VisibleTiles(graphicsDevice,
                                                                 VisibleBounds,
                                                                 DownSample,
                                                                 AsynchTextureLoad);
          
            //Find the index of the requested downsample level
            int roundedDownsample = Mapping.NearestAvailableLevel(DownSample);
            int iStartingDownsampleLevel = 0;
            for (int i = 0; i < Mapping.AvailableLevels.Length; i++)
            {
                if (roundedDownsample == Mapping.AvailableLevels[i])
                {
                    iStartingDownsampleLevel = i;
                    break;
                }
            }

            //We only request textures for every other downsample level if they aren't loaded
            List<int> AllowedDownsamplesList = new List<int>();
            AllowedDownsamplesList.Add(roundedDownsample);
            for (int iLevel = iStartingDownsampleLevel+2; iLevel < Mapping.AvailableLevels.Length; iLevel += 2)
            {
                AllowedDownsamplesList.Add(Mapping.AvailableLevels[iLevel]);
            }

            //Draw the tiles
            foreach (Tile tile in TilesToDraw)
            {
                if(tile.HasTexture)
                    tile.Draw(graphicsDevice, DownSample, channelEffect, AsynchTextureLoad);
                else
                {
                    if(AllowedDownsamplesList.Contains(tile.Downsample))
                        tile.Draw(graphicsDevice, DownSample, channelEffect, AsynchTextureLoad);
                }   
            }

            if (Viking.UI.State.ShowMesh)
            {
                SectionToVolumeMapping VolMap = Mapping as SectionToVolumeMapping; 
                if(VolMap != null)
                {
             //       VolMap.VolumeTransform.Draw(graphicsDevice, basicEffect); 
                }

                foreach (Tile tile in TilesToDraw)
                {
                    tile.DrawMesh(graphicsDevice, basicEffect as BasicEffect); 
                }
            }
        }
        */
    }
}
