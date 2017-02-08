using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Utils;

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

        public readonly Geometry.AxisUnits XYScale;
        

        public Pyramid(string name, string path)
        {
            this.Name = name;
            this.Path = path; 
        }

        public Pyramid(XElement PyramidElement)
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

        public void AddLevel(int level, string path)
        {
            LevelsToPaths.Add(level, path);
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
