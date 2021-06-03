using DataExport.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace DataExport.Tests
{
    [TestClass]
    public class RequestVariablesTest
    {
        [TestMethod]
        public void TestODataQueryParametersAsync()
        {
            HttpContext.Current = new HttpContext(new HttpRequest("", "http://tempuri.org", "id=180,476"), new HttpResponse(new System.IO.StringWriter()));
            Uri endpoint;
            Uri.TryCreate("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute, out endpoint);

            Task<ICollection<long>> task_ids = RequestVariables.GetIDsFromQueryAsync(endpoint, "Structures?$filter=ID eq 180");
            task_ids.Wait();
            ICollection<long> ids = task_ids.Result;

            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Count == 1);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Contains(180));
        }

        [TestMethod]
        public async void TestODataQueryParameters()
        {
            HttpContext.Current = new HttpContext(new HttpRequest("", "http://tempuri.org", "id=180,476"), new HttpResponse(new System.IO.StringWriter()));
            Uri endpoint;
            Uri.TryCreate("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute, out endpoint);

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
            HttpRequest test_request = new HttpRequest("", "http://tempuri.org", "$query=(Structures?$filter=startswith(Label,%27CbB5%27)&$select=ID)&Hops=1");
            HttpContext.Current = new HttpContext(test_request, new HttpResponse(new System.IO.StringWriter()));
            Uri endpoint;
            Uri.TryCreate("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute, out endpoint);
              
            ICollection<long> ids = RequestVariables.GetIDsFromQueryData(HttpContext.Current.Request.QueryString);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Count == 1);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(ids.Contains(180));


        }
    }
}
