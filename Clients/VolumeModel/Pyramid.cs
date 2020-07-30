using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Utils;
using Geometry;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Records the data stored in the <pyramid> element in VikingXML, currently a name, path, and list of levels
    /// </summary>
    public class Pyramid
    {
        public readonly string Name;
        public readonly string Path;

        /// <summary>
        /// Maps downsample levels to the path
        /// </summary>
        private SortedList<int, string> LevelsToPaths = new SortedList<int, string>();

        public Geometry.AxisUnits XYScale { get; protected set; }


        protected Pyramid(string name, string path)
        {
            this.Name = name;
            this.Path = path; 
        }

        protected Pyramid(XElement PyramidElement)
        {
            this.Name = IO.GetAttributeCaseInsensitive(PyramidElement,"name").Value;
            this.Path = IO.GetAttributeCaseInsensitive(PyramidElement, "path").Value;

            XElement scale_elem = PyramidElement.Elements().Where(elem => elem.Name.LocalName == "Scale").FirstOrDefault();
            if (scale_elem != null)
                this.XYScale = scale_elem.ParseScale();
            
            //Examine the XML document and determine the scale
            IEnumerable<XElement> LevelElements = PyramidElement.Elements().Where(elem => elem.Name.LocalName == "Level");
            foreach (XElement levelElement in LevelElements)
            {
                int Downsample = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(levelElement,"Downsample").Value);
                string LevelPath = IO.GetAttributeCaseInsensitive(levelElement, "path").Value;

                AddLevel(Downsample, LevelPath); 
            }
        }

        public static Pyramid CreateFromElement(XElement PyramidElement, Section section)
        {
            IEnumerable<XElement> LevelElements = PyramidElement.Elements().Where(elem => elem.Name.LocalName == "Level");

            //Do not create a pyramid if there are no level elements
            if (LevelElements.Count() == 0)
                return null;

            string Name = IO.GetAttributeCaseInsensitive(PyramidElement, "name").Value;
            string Path = IO.GetAttributeCaseInsensitive(PyramidElement, "path").Value;

            Pyramid pyramid = new Pyramid(Name, Path);

            XElement scale_elem = PyramidElement.Elements().Where(elem => elem.Name.LocalName == "Scale").FirstOrDefault();
            if (scale_elem != null)
                pyramid.XYScale = scale_elem.ParseScale();

            //Examine the XML document and determine the scale
            
            foreach (XElement levelElement in LevelElements)
            {
                int Downsample = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(levelElement, "Downsample").Value);
                string LevelPath = IO.GetAttributeCaseInsensitive(levelElement, "path").Value;

                pyramid.AddLevel(Downsample, LevelPath);
            }

            return pyramid;
             
        }
        /*
            string Name = IO.GetAttributeCaseInsensitive(PyramidElement, "name").Value;
            string mosaicTransformPath = IO.GetAttributeCaseInsensitive(PyramidElement, "path").Value;
            int TileSizeX = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(PyramidElement, "TileXDim").Value);
            int TileSizeY = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(PyramidElement, "TileYDim").Value);
            string TilePrefix = IO.GetAttributeCaseInsensitive(PyramidElement, "FilePrefix").Value;
            string TilePostfix = IO.GetAttributeCaseInsensitive(PyramidElement, "FilePostfix").Value;
            string TileGridPath = IO.GetAttributeCaseInsensitive(PyramidElement, "path").Value;
            string GridTileFormat = null;

            XElement scale_elem = PyramidElement.Elements().Where(elem => elem.Name.LocalName == "Scale").FirstOrDefault();
            AxisUnits XYScale = null;
            if (scale_elem != null)
                XYScale = scale_elem.ParseScale();

            XAttribute GridTileFormatAttribute = PyramidElement.Attribute("CoordFormat");
            if (GridTileFormatAttribute != null)
            {
                GridTileFormat = TileGridMapping.GridTileFormatStringFromPythonString(GridTileFormatAttribute.Value.ToString());
            }

            //If the tileset node has no entries, then don't create a TileGridMapping
            if (PyramidElement.Nodes().Count() == 0)
                return null;

            TileGridMapping mapping = new TileGridMapping(section, Name, TilePrefix, TilePostfix,
                                                          TileSizeX, TileSizeY, TileGridPath, GridTileFormat, XYScale);


            foreach (XNode node in PyramidElement.Nodes())
            {
                XElement elem = node as XElement;
                if (elem == null)
                    continue;

                //Fetch the name if we know it
                switch (elem.Name.LocalName)
                {
                    case "Level":
                        mapping.AddLevel(System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "Downsample").Value),
                                       System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "GridDimX").Value),
                                       System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "GridDimY").Value),
                                       IO.GetAttributeCaseInsensitive(elem, "path").Value);
                        break;
                }
            }

            return mapping;
        }
        */
        public bool AddLevel(int level, string path)
        {
            if (LevelsToPaths.ContainsKey(level) == false)
            {
                LevelsToPaths.Add(level, path);
                return true;
            }

            return false;
        }

        public IList<int> GetLevels()
        {
            return LevelsToPaths.Keys;
        }

        public string PathForLevel(int level)
        {
            if (LevelsToPaths.ContainsKey(level) == false)
                return null;
            
            return LevelsToPaths[level];
        }
    }
}
