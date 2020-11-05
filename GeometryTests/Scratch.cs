using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace GeometryTests
{
    interface IFoo
    {
        double value();
    }

    class Foo : IFoo
    {
        double val = 0;

        double IFoo.value()
        {
            return val;
        }
    }

    /// <summary>
    /// Summary description for Scractch
    /// </summary>
    [TestClass]
    public class Scratch
    {
        public Scratch()
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
            Foo f = new Foo();
            IFoo myInterface = f as IFoo;

            Foo fooReturns = myInterface as Foo;

            Debug.Assert(fooReturns != null);

            //
            // TODO: Add test logic	here
            //
        }
    }
}
