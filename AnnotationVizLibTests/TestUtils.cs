using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;

namespace AnnotationVizLibTests
{
    internal static class TestUtils
    {
        public static StructureMorphologyColorMap LoadColorMap(string directory)
        {
            ColorMapWithLong StructureColors = ColorMapWithLong.CreateFromConfigFile(System.IO.Path.Combine(directory, "StructureColors.txt"));
            ColorMapWithLong StructureTypeColors = ColorMapWithLong.CreateFromConfigFile(System.IO.Path.Combine(directory, "StructureTypeColors.txt"));
            ColorMapWithImages ImageColors = ColorMapWithImages.CreateFromConfigFile(System.IO.Path.Combine(directory, "ImageColorMaps.txt"));

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(StructureTypeColors, StructureColors, ImageColors);
            return colorMap;
        }
    }
}
