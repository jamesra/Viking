using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry.Transforms;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for TransformAdditionTest
    /// </summary>
    [TestClass]
    public class TransformAdditionTest
    {
        public TransformAdditionTest()
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
        public void TestMethod1()
        {
            string ControlStosFile = "..\\..\\35-37_grid.stos";
            string MappedStosFile = "..\\..\\34-35_grid.stos";
            string outputStosFile = "..\\..\\34-37_grid.stos";

            TriangulationTransform ControlTriangulation = null;
            TriangulationTransform MappedTriangulation = null;
            using (System.IO.FileStream controlStosTextStream = System.IO.File.OpenRead(ControlStosFile))
            {
                 ControlTriangulation = TransformFactory.ParseStos(controlStosTextStream,
                                                                                            new StosTransformInfo(37, 35, DateTime.UtcNow),
                                                                                             1) as TriangulationTransform;
            }

            using (System.IO.FileStream mappedStosTextStream = System.IO.File.OpenRead(MappedStosFile))
            {
                MappedTriangulation = TransformFactory.ParseStos(mappedStosTextStream,
                                                                                            new StosTransformInfo(35, 34, DateTime.UtcNow),
                                                                                             1) as TriangulationTransform;
            }

            TriangulationTransform SliceToVolumeTriangulation = TriangulationTransform.Transform(ControlTriangulation,
                                                                                               MappedTriangulation,
                                                                                               new StosTransformInfo(37, 34,
                                                                                               DateTime.UtcNow));
            using (System.IO.StreamWriter fs = System.IO.File.CreateText(outputStosFile))
            {
                ((Geometry.IITKSerialization)SliceToVolumeTriangulation).WriteITKTransform(fs);

                fs.Flush();
            }
        }
    }
}
