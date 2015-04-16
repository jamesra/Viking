using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnnotationVizLib;

namespace AnnotationUtilsTests
{
    /// <summary>
    /// Summary description for MotifGraphTest
    /// </summary>
    [TestClass]
    public class MotifGraphTest
    {
        //public string Endpoint = "https://connectomes.utah.edu/Services/RabbitBinary/Annotate.svc";
        public string Endpoint = "https://websvc1.connectomes.utah.edu/RC1/Annotation/Service.svc";
        private System.Net.NetworkCredential userCredentials; 

        public MotifGraphTest()
        {
            userCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
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
        public void GenerateMotifGraph()
        { 
            AnnotationVizLib.MotifGraph graph = new MotifGraph();
            graph = MotifGraph.BuildGraph(this.Endpoint, this.userCredentials);

            System.Diagnostics.Debug.Assert(graph != null);

            MotifDOTView dotGraph = AnnotationVizLib.MotifDOTView.ToDOT(graph);

            string DotFileFullPath = "C:\\Temp\\Motif.dot";
            dotGraph.SaveDOT(DotFileFullPath);

            string[] Types = new string[] {"svg"};

            MotifDOTView.Convert("dot", DotFileFullPath, Types);  
        }
    }
}
