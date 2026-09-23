using System;
using System.ServiceModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI;

namespace WebAnnotationTests
{
    [TestClass]
    public class ExceptionReportTests
    {
        [TestMethod]
        public void DescribeIncludesInnerExceptionMessage()
        {
            Exception exception = new InvalidOperationException(
                "An error occurred while updating the entries. See the inner exception for details.",
                new Exception("Violation of PRIMARY KEY constraint 'PK_LocationLink'."));

            string text = ExceptionReport.Describe(exception);

            StringAssert.Contains(text, "See the inner exception for details.");
            StringAssert.Contains(text, "PK_LocationLink");
        }

        [TestMethod]
        public void DescribeIncludesFaultDetailInnerException()
        {
            Exception database = new InvalidOperationException(
                "An error occurred while updating the entries. See the inner exception for details.",
                new Exception("String or binary data would be truncated."));
            var fault = new FaultException<ExceptionDetail>(new ExceptionDetail(database));

            string text = ExceptionReport.Describe(fault);

            StringAssert.Contains(text, "See the inner exception for details.");
            StringAssert.Contains(text, "truncated");
        }

        [TestMethod]
        public void DescribeOmitsRepeatedMessages()
        {
            const string same = "An error occurred while updating the entries. See the inner exception for details.";
            Exception exception = new InvalidOperationException(same, new InvalidOperationException(same));

            string text = ExceptionReport.Describe(exception);

            Assert.AreEqual(same, text);
        }
    }
}
