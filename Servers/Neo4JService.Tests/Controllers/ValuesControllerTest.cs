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
    public class ValuesControllerTest
    {
        [TestMethod]
        public void Get()
        {
            // Arrange
            QueryController controller = new QueryController();

            // Act
            IEnumerable<string> result = controller.Get();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            Assert.AreEqual("value1", result.ElementAt(0));
            Assert.AreEqual("value2", result.ElementAt(1));
        }
        
        [TestMethod]
        public void Post()
        {
            // Arrange
            QueryController controller = new QueryController();

            // Act
            string result = controller.Post("MATCH p=()-[r:AggregateLink]->() RETURN p LIMIT 10");

            Console.WriteLine(result);



            // Assert
        }
        
    }
}
