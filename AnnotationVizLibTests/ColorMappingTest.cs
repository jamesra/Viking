using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using UnitsAndScale;

namespace AnnotationVizLibTests
{
    [TestClass]
    public class ColorMappingTest
    {
        public static bool ColorsMatch(Color A, Color B)
        {
            return A.R == B.R &&
                   A.G == B.G &&
                   A.B == B.B &&
                   A.A == B.A;
        }
        /// <summary>
        /// Tests that pixels can be read from a colormap image correctly.  The colormap image is 3 columns by 2 rows.
        /// Black Red Green
        /// White Blue Gray128
        /// </summary>
        [TestMethod]
        public void TestColorMapImage()
        {

            Scale scale = new(new AxisUnits(1.0, "nm"),
                                    new AxisUnits(1.0, "nm"),
                                    new AxisUnits(1.0, "nm"));
            ColorMapImageData colormap = null;
            using (System.IO.Stream imagestream = System.IO.File.OpenRead("Resources\\ColorMap3x2.png"))
            {
                colormap = new ColorMapImageData(imagestream, 0, scale);
            }

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, 0), Color.Black));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, 1), Color.White));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(1, 0), Color.FromArgb(255, 0, 0)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(2, 0), Color.FromArgb(0, 255, 0)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(1, 1), Color.FromArgb(0, 0, 255)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(2, 1), Color.FromArgb(128, 128, 128)));

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(3, 0), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, 2), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(-1, 0), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, -1), Color.Empty));
        }


        /// <summary>
        /// Same as TestColorMapImage, but the input image is scaled by a factor of four.
        /// </summary>
        [TestMethod]
        public void TestScaledColorMapImage()
        {

            Scale scale = new(new AxisUnits(1.0 / 4.0, "nm"),
                                    new AxisUnits(1.0 / 4.0, "nm"),
                                    new AxisUnits(1.0 / 4.0, "nm"));
            ColorMapImageData colormap = null;
            using (System.IO.Stream imagestream = System.IO.File.OpenRead("Resources\\ColorMap12x8.png"))
            {
                colormap = new ColorMapImageData(imagestream, 0, scale);
            }

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, 0), Color.Black));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, 1), Color.White));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(1, 0), Color.FromArgb(255, 0, 0)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(2, 0), Color.FromArgb(0, 255, 0)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(1, 1), Color.FromArgb(0, 0, 255)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(2, 1), Color.FromArgb(128, 128, 128)));

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(3, 0), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, 2), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(-1, 0), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(colormap.GetColor(0, -1), Color.Empty));
        }

        /// <summary>
        /// Config colour scalars are multipliers from 0 to 1: 0.5 halves a channel and 1 keeps it, read with a '.'
        /// whatever the machine's culture.
        /// </summary>
        [TestMethod]
        public void TestColorMappingConfigFractionalScalars()
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

                // Columns: Section XOffset YOffset XScale YScale Alpha Red Green Blue Filename
                ColorMapWithImages mapping = ColorMapWithImages.Create("1	0	0	1	1	1	0.5	1	0	ColorMap3x2.png", "Resources");

                Assert.IsTrue(ColorsMatch(mapping.GetColor(0, 1, 1), Color.FromArgb(255, 127, 255, 0)), "white: red halved, green kept, blue dropped");
                Assert.IsTrue(ColorsMatch(mapping.GetColor(2, 1, 1), Color.FromArgb(255, 64, 128, 0)), "gray 128 is scaled, not saturated");
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = culture;
            }
        }

        /// <summary>
        /// Check that we can load a set of images and average them correctly.
        /// </summary>
        [TestMethod]
        public void TestColorMappingConfig()
        {

            string config = System.IO.File.ReadAllText("Resources\\ImageColorMaps.txt");
            ColorMapWithImages mapping = ColorMapWithImages.Create(config, "Resources");

            //We expect the two identical images to be averaged using the color scalars in the text file.
            //ColorMap12x8.png draws each of its 3x2 colour cells as 4x4 pixels and the config scales a pixel to 4 volume
            //units, so one cell spans 16 volume units.
            double ImageScale = 16;

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(0 * ImageScale, 1 * ImageScale, 1), Color.FromArgb(127, 255, 127)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(0 * ImageScale, 0 * ImageScale, 1), Color.Black));

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(3 * ImageScale, 0 * ImageScale, 1), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(0 * ImageScale, 2 * ImageScale, 1), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(-1 * ImageScale, 0 * ImageScale, 1), Color.Empty));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(0 * ImageScale, -1 * ImageScale, 1), Color.Empty));
        }

        /// <summary>
        /// Check that we can load a set of images and average them correctly.
        /// </summary>
        [TestMethod]
        public void TestColorMappingByKeysConfig()
        {

            string config = System.IO.File.ReadAllText("Resources\\StructureTypeColors.txt");
            ColorMapWithLong mapping = ColorMapWithLong.Create(config);

            //We expect the two identical images to be averaged using the color scalars in the text file

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(35), Color.FromArgb(255, 255, 0, 51)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(34), Color.FromArgb(255, 0, 0, 255)));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ColorsMatch(mapping.GetColor(85), Color.FromArgb(255, 255, 255, 255)));

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(mapping.ContainsKey(35));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsFalse(mapping.ContainsKey(0));
        }
    }
}
