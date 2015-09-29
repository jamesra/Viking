using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;

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
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
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
            GridRectangle VolumeBounds = new GridRectangle(0, 1024, 0, 1024);
            GridCellDimensions cellDimensions = new GridCellDimensions(VolumeBounds.Width, VolumeBounds.Height);
            GridRectangle ScreenBounds = new GridRectangle(0, 64, 0, 128); //A 64x128 pixel screen

            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToOnePixelMappingRadius = 1.0;
            double OneToTwoPixelMappingRadius = 2.0;
            double LevelForOneToOnePixelMappingRadius = 3.0;
            double LevelForOneToTwoPixelMappingRadius = LevelForOneToOnePixelMappingRadius - 1;

            Geometry.RegionPyramid<string> pyramid = new RegionPyramid<string>(VolumeBounds, cellDimensions);

            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius); //
            Assert.AreEqual(level.Level, 0);

            Assert.AreEqual(level.GridDimensions.Width, 1);
            Assert.AreEqual(level.GridDimensions.Height, 1);
            Assert.AreEqual(level.CellDimensions.Width, VolumeBounds.Width);
            Assert.AreEqual(level.CellDimensions.Height, VolumeBounds.Height);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToTwoPixelMappingRadius); //
            Assert.AreEqual(level.Level, LevelForOneToTwoPixelMappingRadius);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToOnePixelMappingRadius); //
            Assert.AreEqual(level.Level, LevelForOneToOnePixelMappingRadius);

            Assert.AreEqual(level.GridDimensions.Width, (int)Math.Pow(2,LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.GridDimensions.Height, (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.CellDimensions.Width, VolumeBounds.Width / Math.Pow(2, level.Level));
            Assert.AreEqual(level.CellDimensions.Height, VolumeBounds.Height / Math.Pow(2, level.Level));
        }

        [TestMethod]
        public void TestRegionPyramidWithSmallerCellDimensions()
        {
            GridRectangle VolumeBounds = new GridRectangle(0, 1024, 0, 1024);
            GridCellDimensions cellDimensions = new GridCellDimensions(VolumeBounds.Width / 2.0, VolumeBounds.Height / 2.0);
            GridRectangle ScreenBounds = new GridRectangle(0, 64, 0, 128); //A 64x128 pixel screen

            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToOnePixelMappingRadius = 1.0;
            double OneToTwoPixelMappingRadius = 2.0;
            double LevelForOneToOnePixelMappingRadius = 3.0;
            double LevelForOneToTwoPixelMappingRadius = LevelForOneToOnePixelMappingRadius - 1;

            Geometry.RegionPyramid<string> pyramid = new RegionPyramid<string>(VolumeBounds, cellDimensions);

            RegionPyramidLevel<string> levelZero;
            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            levelZero = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius); //
            Assert.AreEqual(levelZero.Level, 0);

            Assert.AreEqual(levelZero.GridDimensions.Width, 2);
            Assert.AreEqual(levelZero.GridDimensions.Height, 2);
            Assert.AreEqual(levelZero.CellDimensions.Width, cellDimensions.Width);
            Assert.AreEqual(levelZero.CellDimensions.Height, cellDimensions.Height);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToTwoPixelMappingRadius); //
            Assert.AreEqual(level.Level, LevelForOneToTwoPixelMappingRadius);

            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, OneToOnePixelMappingRadius); //
            Assert.AreEqual(level.Level, LevelForOneToOnePixelMappingRadius);

            Assert.AreEqual(level.GridDimensions.Width, levelZero.GridDimensions.Width * (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.GridDimensions.Height, levelZero.GridDimensions.Height * (int)Math.Pow(2, LevelForOneToOnePixelMappingRadius));
            Assert.AreEqual(level.CellDimensions.Width, cellDimensions.Width / Math.Pow(2, level.Level));
            Assert.AreEqual(level.CellDimensions.Height, cellDimensions.Height / Math.Pow(2, level.Level));
        }

        private void ValidateSubGridForRegion(RegionPyramidLevel<string> level, GridRectangle VisibleVolumeBounds)
        {
            //Grab the entire volume from the level
            GridRange<string> range = level.SubGridForRegion(VisibleVolumeBounds);
            Assert.AreEqual(range.Indicies.Width, Math.Ceiling(VisibleVolumeBounds.Width / level.CellDimensions.Width));
            Assert.AreEqual(range.Indicies.Height, Math.Ceiling(VisibleVolumeBounds.Height / level.CellDimensions.Height));
            Assert.AreEqual(range.Indicies.iMinX, Math.Floor(VisibleVolumeBounds.Left / level.CellDimensions.Width));
            Assert.AreEqual(range.Indicies.iMinY, Math.Floor(VisibleVolumeBounds.Bottom / level.CellDimensions.Height)); 
        }

        [TestMethod]
        public void TestRegionPyramidLevel()
        {
            GridRectangle VolumeBounds = new GridRectangle(0, 1024, 0, 1024);
            GridRectangle ScreenBounds = new GridRectangle(0, 64, 0, 128); //A 64x128 pixel screen
            GridRectangle VisibleVolumeBounds = new GridRectangle(512, 512 + ScreenBounds.Width, 512, 512 + ScreenBounds.Height); //A 64x128 pixel screen
            

            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToTwoPixelMappingRadius = 2.0;

            Geometry.RegionPyramid<string> pyramid = new RegionPyramid<string>(VolumeBounds, new GridCellDimensions(VolumeBounds.Width, VolumeBounds.Height));

            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius); //
            ValidateSubGridForRegion(level, VisibleVolumeBounds);
             
            level = pyramid.GetLevelForVolumeBounds(VisibleVolumeBounds, OneToTwoPixelMappingRadius); //
            ValidateSubGridForRegion(level, VisibleVolumeBounds);
            
            //Grab a subregion from the volume
            
        }

        [TestMethod]
        public void TestRegionPyramidLevelWithSmallerCellDimensions()
        {
            GridRectangle VolumeBounds = new GridRectangle(0, 1024, 0, 1024);
            GridCellDimensions cellDimensions = new GridCellDimensions(VolumeBounds.Width / 2.0, VolumeBounds.Height / 2.0);
            GridRectangle ScreenBounds = new GridRectangle(0, 64, 0, 128); //A 64x128 pixel screen
            GridRectangle VisibleVolumeBounds = new GridRectangle(512, 512 + ScreenBounds.Width, 512, 512 + ScreenBounds.Height); //A 64x128 pixel screen


            double FullVolumeVisibleRadius = VolumeBounds.Width / ScreenBounds.Width;
            double OneToTwoPixelMappingRadius = 2.0;

            Geometry.RegionPyramid<string> pyramid = new RegionPyramid<string>(VolumeBounds, cellDimensions);

            RegionPyramidLevel<string> level;
            //View the entire volume, ensure that level 0 is returned
            level = pyramid.GetLevelForScreenBounds(ScreenBounds, FullVolumeVisibleRadius); //
            ValidateSubGridForRegion(level, VisibleVolumeBounds);

            level = pyramid.GetLevelForVolumeBounds(VisibleVolumeBounds, OneToTwoPixelMappingRadius); //
            ValidateSubGridForRegion(level, VisibleVolumeBounds);

            //Grab a subregion from the volume

        }
    }
}
