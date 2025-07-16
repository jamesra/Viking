using DataExport.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DataExport.Tests
{
    [TestClass]
    public class RequestVariablesTest
    {
        [TestMethod]
        public void TestODataQueryParametersAsync()
        {
            var context = new DefaultHttpContext();
            Uri.TryCreate("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute, out Uri endpoint);

            Task<ICollection<long>> task_ids = RequestVariables.GetIDsFromQueryAsync(endpoint, "Structures?$filter=ID eq 180");
            task_ids.Wait();
            ICollection<long> ids = task_ids.Result;

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Count == 1);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Contains(180));
        }

        [TestMethod]
        public async void TestODataQueryParameters()
        {
            var context = new DefaultHttpContext();
            Uri.TryCreate("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute, out Uri endpoint);

            ICollection<long> network_ids = RequestVariables.GetIDsFromQuery(endpoint, "Network(IDs=[172]Hops=0)");
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(network_ids.Count == 1);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(network_ids.Contains(172));

            ICollection<long> ids = RequestVariables.GetIDsFromQuery(endpoint, "Structures?$filter=ID eq 180");
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Count == 1);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Contains(180));

            
        }

        [TestMethod]
        public async void TestRequestParametersForODataQueries()
        {
            var context = new DefaultHttpContext();
            // Set up query parameters for DefaultHttpContext
            var query = new Microsoft.AspNetCore.Http.QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "id", "180,476" } });
            context.Request.QueryString = new QueryString("?id=180,476");
            context.Request.Query = query;
            Uri.TryCreate("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute, out Uri endpoint);
              
            ICollection<long> ids = RequestVariables.GetIDsFromQueryData(context.Request.Query);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Count == 1);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Contains(180));


        }
    }
}
