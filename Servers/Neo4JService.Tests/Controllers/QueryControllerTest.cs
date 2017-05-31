using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4JService;
using Neo4JService.Controllers;

namespace Neo4JService.Tests.Controllers
{
    [TestClass]
    public class QueryControllerTest
    {
        [TestMethod]
        public void Get()
        {
            // Arrange
            QueryController controller = new QueryController();

            // Act
            //IEnumerable<string> result = controller.Get();
            /*
            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            Assert.AreEqual("value1", result.ElementAt(0));
            Assert.AreEqual("value2", result.ElementAt(1));
            */
        }
        
        [TestMethod]
        public void Post()
        {
            // Arrange
            QueryController controller = new QueryController();

            // Act
            string result = controller.PostQuery("MATCH p=()-[r:AggregateLink]->() RETURN p LIMIT 10");
            Console.WriteLine(result);
            Assert.IsTrue(result.Length > 0, "No result for valid MATCH query");

            // Act
            bool ExceptionThrown = false;
            try
            {
                string failResult = controller.PostQuery("CALL dbms.security.createUser(\"Neo4JWebService\", \"4%w%o06\", false)");
            }
            catch (ArgumentException e)
            {
                ExceptionThrown = true;
            }

            Assert.IsTrue(ExceptionThrown, "Writable keyword not detected in query");


            // Assert
        }


    }
}
