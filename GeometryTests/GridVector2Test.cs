using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting; 
using Geometry;
using System.Diagnostics; 

namespace UtilitiesTests
{
    /// <summary>
    /// Summary description for GridVector2Test
    /// </summary>
    [TestClass]
    public class GridVector2Test
    {
        public GridVector2Test()
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
        public void TestAngle()
        {
            GridVector2 A = new GridVector2(5, 0);
            GridVector2 B = new GridVector2(2.5, 2.5); 

            double PI4 = Math.PI / 4;

            double angle = GridVector2.Angle(A,B);
            Debug.Assert(angle - Global.Epsilon < (3.0 * PI4) &&
                         angle + Global.Epsilon > (3.0 * PI4)); 

            A = new GridVector2(5, 0);
            B = new GridVector2(2.5, -2.5); 

            angle = GridVector2.Angle(A,B);
            Debug.Assert(angle - Global.Epsilon < (-3.0 * PI4) &&
                         angle + Global.Epsilon > (-3.0 * PI4)); 
            
            //
            // TODO: Add test logic	here
            //
        }

        [TestMethod]
        public void TestAngle2()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            GridVector2 Origin = new GridVector2(0, 0);
            GridVector2 A = new GridVector2(1, 0);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(-1, 0);

            double Degree90 = GridVector2.ArcAngle(Origin, A, B);
            Debug.Assert(Degree90 == Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Debug.Assert(Degree90 == -Pi2);
             
            double Degree180 = GridVector2.ArcAngle(Origin, A, C);
            Debug.Assert(Degree180 == Math.PI);
             
            double Degree0 = GridVector2.Angle(Origin, A); 
            Debug.Assert(Degree0 == 0); 

            Degree90 = GridVector2.Angle(Origin, B); 
            Debug.Assert(Degree90 == Pi2); 
        }

        [TestMethod]
        public void TestAngle3()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            GridVector2 Origin = new GridVector2(0, 0);
            GridVector2 A = new GridVector2(1, 0);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(-1, 0);
            GridVector2 D = new GridVector2(Math.Cos(Pi4), Math.Sin(Pi4));

            double degree45 = GridVector2.ArcAngle(Origin, A, D);
            double result = GridVector2.Angle(Origin, D);
            Debug.Assert(degree45 == Pi4);
            Debug.Assert(result == Pi4);
        }
    }
}
