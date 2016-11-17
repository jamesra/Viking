using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;
using Geometry.Transforms;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for TransformFactoryTest
    /// </summary>
    [TestClass]
    public class TransformFactoryTest
    {
        public TransformFactoryTest()
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

        private void TestRegex(string expression, string input)
        {
            Regex regex = new Regex(expression);
            Match match = regex.Match(input);
            Assert.IsTrue(match.Success, string.Format("Could not match input to regular expression:\nInput:{0}\nRegex{1}", input, expression));
        }

        [TestMethod]
        public void TestTransformParameters()
        {
            
            string transform_name = "GridTransform_double_2_2 ";
            string transform_regex = @"\s*(?<transform_name>\S+)\s+";
            TestRegex(transform_regex, transform_name);

            string transform_vp = "GridTransform_double_2_2 vp 8 ";
            string transform_vp_regex = @"\s*(?<transform_name>\S+)\s+vp\s*(?<num_vp_params>\d+)\s+";
            TestRegex(transform_vp_regex, transform_vp);

            string transform_vp_param = "GridTransform_double_2_2 vp 8 0 0.00 5.120000000000e+002 fp 7 ";
            string transform_vp_param_regex = @"\s*(?<transform_name>\S+)\s+vp\s*(?<num_vp_params>\d+)\s+(?<vp_params>(\d+(\.\d+(e\+\d+)?)?\s+)+)fp\s+(?<num_fp_params>\d+)\s+";
            TestRegex(transform_vp_param_regex, transform_vp_param);

            string transform =          "GridTransform_double_2_2 vp 8 0.000000000000e+000 0.000000000000e+000 5.120000000000e+002 0.000000000000e+000 0.000000000000e+000 2.560000000000e+002 5.120000000000e+002 2.560000000000e+002 fp 7 0.000000000000e+000 1.000000000000e+000 1.000000000000e+000 0.000000000000e+000 0.000000000000e+000 5.120000000000e+002 2.560000000000e+002";
            string full_transform_regex = @"\s*(?<transform_name>\S+)\s+vp\s*(?<num_vp_params>\d+)\s+(?<vp_params>(\d+(\.\d+(e\+\d+)?)?\s+)+)fp\s+(?<num_fp_params>\d+)\s+(?<fp_params>(\d+(\.\d+(e\+\d+)?)?\s+)+)";

            TestRegex(full_transform_regex, transform);

            /*var tp = Geometry.Transforms.TransformParameters.ParseTransform(transform);

            Assert.AreEqual(tp.variableParameters.Length, 8);
            Assert.AreEqual(tp.fixedParameters.Length, 7);
            */
            //
            // TODO: Add test logic here
            //
        }
    }

}