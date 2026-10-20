using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Geometry;

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

        /// <summary>
        /// Resolves a .stos fixture copied next to the test assembly (see the Content items in
        /// GeometryTests.csproj), so the path is the same on net48 and net9.0 whatever the output depth.
        /// </summary>
        private static string FixturePath(string fileName) => System.IO.Path.Combine(AppContext.BaseDirectory, fileName);

        /// <summary>
        /// Composing slice 37->35 with 35->34 must map every control point of the result exactly as
        /// applying the two parsed transforms in sequence does. The checked-in 34-37_grid.stos is not
        /// compared: it was written by an older triangulation and has a different point set, so a text
        /// match would fail without a defect in the composition.
        /// </summary>
        [TestMethod]
        public void ComposingGridStosTransformsMatchesSequentialApplication()
        {
            string ControlStosFile = FixturePath("35-37_grid.stos");
            string MappedStosFile = FixturePath("34-35_grid.stos");

            TriangulationTransform ControlTriangulation = null;
            TriangulationTransform MappedTriangulation = null;
            using (System.IO.FileStream controlStosTextStream = System.IO.File.OpenRead(ControlStosFile))
            {
                ControlTriangulation = TransformFactory.ParseStos(controlStosTextStream,
                                                                                           new StosTransformInfo(37, 35, DateTime.UtcNow),
                                                                                            1).Result as TriangulationTransform;
            }

            using (System.IO.FileStream mappedStosTextStream = System.IO.File.OpenRead(MappedStosFile))
            {
                MappedTriangulation = TransformFactory.ParseStos(mappedStosTextStream,
                                                                                            new StosTransformInfo(35, 34, DateTime.UtcNow),
                                                                                             1).Result as TriangulationTransform;
            }

            ITransformControlPoints SliceToVolumeTriangulation = TriangulationTransform.Transform(ControlTriangulation,
                                                                                               MappedTriangulation,
                                                                                               new StosTransformInfo(37, 34,
                                                                                               DateTime.UtcNow));
            Assert.IsNotNull(SliceToVolumeTriangulation);
            Assert.IsTrue(SliceToVolumeTriangulation.MapPoints.Length >= 3);

            // Fixture coordinates reach ~1400 px; 0.01 px is far above double round-off yet well below the
            // pixel-scale errors a wrong composition would produce.
            const double tolerancePixels = 0.01;
            // The composition also synthesizes points on the hull of the control transform, and a point sitting
            // on a triangulation boundary can fail TryTransform by round-off (e.g. X = -0.00). Those are skipped;
            // the requirement is that most points are checked and every checked point agrees.
            int compared = 0;
            foreach (MappingVector2 mapPoint in SliceToVolumeTriangulation.MapPoints)
            {
                if (!MappedTriangulation.TryTransform(mapPoint.MappedPoint, out Vector2 viaMapped) ||
                    !ControlTriangulation.TryTransform(viaMapped, out Vector2 expected))
                    continue;

                compared++;
                double error = Vector2.Distance(expected, mapPoint.ControlPoint);
                Assert.IsTrue(error <= tolerancePixels,
                              $"Composed control point {mapPoint.ControlPoint} differs from sequential {expected} by {error} px for mapped point {mapPoint.MappedPoint}");
            }

            Assert.IsTrue(compared >= SliceToVolumeTriangulation.MapPoints.Length * 0.8,
                          $"Only {compared} of {SliceToVolumeTriangulation.MapPoints.Length} composed points could be checked");

            string itk = ((Geometry.IITKSerialization)SliceToVolumeTriangulation).GetITKTransform();
            Assert.IsFalse(string.IsNullOrWhiteSpace(itk));
        }
    }
}
