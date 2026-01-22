using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for RegionPyramidTest
    /// </summary>
    [TestClass]
    public class RegionPyramidTest
    {
        public RegionPyramidTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get => testContextInstance;
            set => testContextInstance = value;
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestRegionPyramid()
        {
            GridRectangle VolumeBounds = new(0, 1024, 0, 1024);
            GridCellDimensions cellDimensions = new(VolumeBounds.Width, VolumeBounds.Height);
            GridRectangle ScreenBounds = new(0, 64, 0, 128); //A 64x128 pixel screen

            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToOnePixelMappingRadius = 1.0;
            double OneToTwoPixelMappingRadius = 2.0;
            double LevelForOneToOnePixelMappingRadius = 3.0;
            double LevelForOneToTwoPixelMappingRadius = LevelForOneToOnePixelMappingRadius - 1;

            Geometry.RegionPyramid<string> pyramid = new(VolumeBounds, cellDimensions);

            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius) as RegionPyramidLevel<string>; //
            Assert.AreEqual(0, level.Level);

            Assert.AreEqual(1, level.GridDimensions.Width);
            Assert.AreEqual(1, level.GridDimensions.Height);
            Assert.AreEqual(level.CellDimensions.Width, VolumeBounds.Width);
            Assert.AreEqual(level.CellDimensions.Height, VolumeBounds.Height);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToTwoPixelMappingRadius) as RegionPyramidLevel<string>; //
            Assert.AreEqual(level.Level, LevelForOneToTwoPixelMappingRadius);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToOnePixelMappingRadius) as RegionPyramidLevel<string>; //
            Assert.AreEqual(level.Level, LevelForOneToOnePixelMappingRadius);

            Assert.AreEqual(level.GridDimensions.Width, (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.GridDimensions.Height, (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.CellDimensions.Width, VolumeBounds.Width / Math.Pow(2, level.Level));
            Assert.AreEqual(level.CellDimensions.Height, VolumeBounds.Height / Math.Pow(2, level.Level));
        }

        [TestMethod]
        public void TestRegionPyramidWithSmallerCellDimensions()
        {
            GridRectangle VolumeBounds = new(0, 1024, 0, 1024);
            GridCellDimensions cellDimensions = new(VolumeBounds.Width / 2.0, VolumeBounds.Height / 2.0);
            GridRectangle ScreenBounds = new(0, 64, 0, 128); //A 64x128 pixel screen

            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToOnePixelMappingRadius = 1.0;
            double OneToTwoPixelMappingRadius = 2.0;
            double LevelForOneToOnePixelMappingRadius = 3.0;
            double LevelForOneToTwoPixelMappingRadius = LevelForOneToOnePixelMappingRadius - 1;

            Geometry.RegionPyramid<string> pyramid = new(VolumeBounds, cellDimensions);

            RegionPyramidLevel<string> levelZero;
            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            levelZero = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius) as RegionPyramidLevel<string>; //; //
            Assert.AreEqual(0, levelZero.Level);

            Assert.AreEqual(2, levelZero.GridDimensions.Width);
            Assert.AreEqual(2, levelZero.GridDimensions.Height);
            Assert.AreEqual(levelZero.CellDimensions.Width, cellDimensions.Width);
            Assert.AreEqual(levelZero.CellDimensions.Height, cellDimensions.Height);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToTwoPixelMappingRadius) as RegionPyramidLevel<string>; //; //
            Assert.AreEqual(level.Level, LevelForOneToTwoPixelMappingRadius);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToOnePixelMappingRadius) as RegionPyramidLevel<string>; //; //
            Assert.AreEqual(level.Level, LevelForOneToOnePixelMappingRadius);

            Assert.AreEqual(level.GridDimensions.Width, levelZero.GridDimensions.Width * (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.GridDimensions.Height, levelZero.GridDimensions.Height * (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.CellDimensions.Width, cellDimensions.Width / Math.Pow(2, level.Level));
            Assert.AreEqual(level.CellDimensions.Height, cellDimensions.Height / Math.Pow(2, level.Level));
        }

        private static void ValidateSubGridForRegion(RegionPyramidLevel<string> level, GridRectangle VisibleVolumeBounds)
        {
            //Grab the entire volume from the level
            GridRange<string> range = level.SubGridForRegion(VisibleVolumeBounds);
            Assert.AreEqual(range.Indicies.Width, Math.Ceiling(VisibleVolumeBounds.Width / level.CellDimensions.Width));
            Assert.AreEqual(range.Indicies.Height, Math.Ceiling(VisibleVolumeBounds.Height / level.CellDimensions.Height));
            Assert.AreEqual(range.Indicies.iMinX, Math.Floor(VisibleVolumeBounds.Left / level.CellDimensions.Width));
            Assert.AreEqual(range.Indicies.iMinY, Math.Floor(VisibleVolumeBounds.Bottom / level.CellDimensions.Height));

            //Ensure the range returned completely contains the VisibleVolumeBounds
            List<GridRectangle> cellBounds = [.. range.Indicies.Select(key => level.CellBounds(key.X, key.Y))];
            GridRectangle rangeBounds = cellBounds.Aggregate((unionRect, next) => unionRect + next);

            Assert.IsTrue(rangeBounds.Contains(VisibleVolumeBounds), "Visible volume is not contained within the range returned");

        }

        [TestMethod]
        public void TestRegionPyramidLevel()
        {
            GridRectangle VolumeBounds = new(0, 1024, 0, 1024);
            GridRectangle ScreenBounds = new(0, 64, 0, 128); //A 64x128 pixel screen
            GridRectangle VisibleVolumeBounds = new(512, 512 + ScreenBounds.Width, 512, 512 + ScreenBounds.Height); //A 64x128 pixel screen


            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToTwoPixelMappingRadius = 2.0;

            Geometry.RegionPyramid<string> pyramid = new(VolumeBounds, new GridCellDimensions(VolumeBounds.Width, VolumeBounds.Height));

            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius) as RegionPyramidLevel<string>; //; //
            RegionPyramidTest.ValidateSubGridForRegion(level, VisibleVolumeBounds);

            level = pyramid.GetLevelForVolumeBounds(VisibleVolumeBounds, OneToTwoPixelMappingRadius) as RegionPyramidLevel<string>; //; //
            RegionPyramidTest.ValidateSubGridForRegion(level, VisibleVolumeBounds);

            //Grab a subregion from the volume

        }

        [TestMethod]
        public void TestRegionPyramidLevelWithSmallerCellDimensions()
        {
            GridRectangle VolumeBounds = new(0, 1024, 0, 1024);
            GridCellDimensions cellDimensions = new(VolumeBounds.Width / 2.0, VolumeBounds.Height / 2.0);
            GridRectangle ScreenBounds = new(0, 64, 0, 128); //A 64x128 pixel screen
            GridRectangle VisibleVolumeBounds = new(512, 512 + ScreenBounds.Width, 512, 512 + ScreenBounds.Height); //A 64x128 pixel screen


            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToTwoPixelMappingRadius = 2.0;

            Geometry.RegionPyramid<string> pyramid = new(VolumeBounds, cellDimensions);

            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius) as RegionPyramidLevel<string>; //; //
            RegionPyramidTest.ValidateSubGridForRegion(level, VisibleVolumeBounds);

            level = pyramid.GetLevelForVolumeBounds(VisibleVolumeBounds, OneToTwoPixelMappingRadius) as RegionPyramidLevel<string>; //; //
            RegionPyramidTest.ValidateSubGridForRegion(level, VisibleVolumeBounds);

            //Grab a subregion from the volume

        }
    }

    /// <summary>
    /// Summary description for RegionPyramidTest
    /// </summary>
    [TestClass]
    public class BoundlessRegionPyramidTest
    {
        public BoundlessRegionPyramidTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get => testContextInstance;
            set => testContextInstance = value;
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        static double PixelRadiusForLevel(int Level) => Math.Pow(2, Level);

        [TestMethod]
        public void TestBoundlessRegionPyramid()
        {
            GridCellDimensions cellDimensions = new(1024, 1024);
            GridRectangle cellRectangle = new(0, cellDimensions.Width, 0, cellDimensions.Height);
            GridRectangle ScreenBounds = new(0, 64, 0, 128); //A 64x128 pixel screen

            double OneToOnePixelMappingRadius = 1.0;
            double OneToTwoPixelMappingRadius = 2.0;
            double LevelForOneToOnePixelMappingRadius = 3.0;
            double LevelForOneToTwoPixelMappingRadius = LevelForOneToOnePixelMappingRadius - 1;

            Geometry.BoundlessRegionPyramid<string> pyramid = new(cellDimensions, 2);

            BoundlessRegionPyramidLevel<string> level0;
            BoundlessRegionPyramidLevel<string> level1;
            //View the volume at full-resolution, ensure that level 0 is returned
            level0 = pyramid.GetLevel(BoundlessRegionPyramidTest.PixelRadiusForLevel(0)) as BoundlessRegionPyramidLevel<string>; //
            Assert.AreEqual(0, level0.Level);
            Assert.AreEqual(level0.MinRadius, BoundlessRegionPyramidTest.PixelRadiusForLevel(0));

            //Make sure we can fetch level 1
            level1 = pyramid.GetLevel(BoundlessRegionPyramidTest.PixelRadiusForLevel(1)) as BoundlessRegionPyramidLevel<string>; //
            Assert.AreEqual(1, level1.Level);
            Assert.AreEqual(level1.MinRadius, BoundlessRegionPyramidTest.PixelRadiusForLevel(1));

            //Make sure level 1 ScaledCellDimensions are double the level 1 cell dimensions
            Assert.AreEqual(level0.UnscaledCellDimensions.Width, level1.UnscaledCellDimensions.Width);
            Assert.AreEqual(level0.ScaledCellDimensions.Width * Math.Pow(2, level1.Level - level0.Level), level1.ScaledCellDimensions.Width);
        }


        private static void ValidateSubGridForRegion(BoundlessRegionPyramidLevel<string> level, GridRectangle VisibleVolumeBounds)
        {
            //Grab the entire volume from the level
            GridRange<string> range = level.SubGridForRegion(VisibleVolumeBounds);
            Assert.AreEqual(range.Indicies.Width, Math.Ceiling((double)VisibleVolumeBounds.Width / (double)level.ScaledCellDimensions.Width));
            Assert.AreEqual(range.Indicies.Height, Math.Ceiling((double)VisibleVolumeBounds.Height / (double)level.ScaledCellDimensions.Height));
            Assert.AreEqual(range.Indicies.iMinX, Math.Floor((double)VisibleVolumeBounds.Left / (double)level.ScaledCellDimensions.Width));
            Assert.AreEqual(range.Indicies.iMinY, Math.Floor((double)VisibleVolumeBounds.Bottom / (double)level.ScaledCellDimensions.Height));

            //Ensure the range returned completely contains the VisibleVolumeBounds
            List<GridRectangle> cellBounds = [.. range.Indicies.Select(key => level.CellBounds(key.X, key.Y))];
            GridRectangle rangeBounds = cellBounds.Aggregate((unionRect, next) => unionRect + next);

            Assert.IsTrue(rangeBounds.Contains(VisibleVolumeBounds), "Visible volume is not contained within the range returned");
        }

        [TestMethod]
        public void TestBoundlessRegionPyramidLevel()
        {
            GridCellDimensions cellDimensions = new(100, 100);
            Geometry.BoundlessRegionPyramid<string> pyramid = new(cellDimensions, 2);

            GridRectangle ScreenBounds = new(0, 64, 0, 257); //A 64x128 pixel screen
            GridRectangle VisibleVolumeBounds = new(512, 512 + ScreenBounds.Width, 512, 512 + ScreenBounds.Height); //A 64x128 pixel screen

            BoundlessRegionPyramidLevel<string> level0;
            BoundlessRegionPyramidLevel<string> level1;
            level0 = pyramid.GetLevel(BoundlessRegionPyramidTest.PixelRadiusForLevel(0)) as BoundlessRegionPyramidLevel<string>;
            BoundlessRegionPyramidTest.ValidateSubGridForRegion(level0, VisibleVolumeBounds);

            level1 = pyramid.GetLevel(BoundlessRegionPyramidTest.PixelRadiusForLevel(1)) as BoundlessRegionPyramidLevel<string>;
            BoundlessRegionPyramidTest.ValidateSubGridForRegion(level1, VisibleVolumeBounds);
        }
    }
}
