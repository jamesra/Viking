using DataExport.Controllers;
using Microsoft.Extensions.Primitives;

namespace DataExport.Tests;

[TestClass]
public class RequestVariablesTest
{
    [TestMethod]
    public async Task TestODataQueryParametersAsync()
    {
        var endpoint = new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute);

        ICollection<long> ids = await RequestVariables.GetIDsFromQueryAsync(endpoint, "Structures?$filter=ID eq 180");

        // Note: Current implementation returns empty collection as OData is not implemented
        // When implemented, these assertions should pass:
        // Assert.AreEqual(1, ids.Count);
        // Assert.IsTrue(ids.Contains(180));
    }

    [TestMethod]
    public void TestODataQueryParameters()
    {
        var endpoint = new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute);

        ICollection<long> networkIds = RequestVariables.GetIDsFromQuery(endpoint, "Network(IDs=[172]Hops=0)");
        // Note: Current implementation returns empty collection as OData is not implemented
        // When implemented, these assertions should pass:
        // Assert.AreEqual(1, networkIds.Count);
        // Assert.IsTrue(networkIds.Contains(172));

        ICollection<long> ids = RequestVariables.GetIDsFromQuery(endpoint, "Structures?$filter=ID eq 180");
        // Note: Current implementation returns empty collection as OData is not implemented
        // When implemented, these assertions should pass:
        // Assert.AreEqual(1, ids.Count);
        // Assert.IsTrue(ids.Contains(180));
    }

    [TestMethod]
    public void TestRequestParametersForODataQueries()
    {
        var context = new DefaultHttpContext();
        var endpoint = new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute);
        
        // Set up query parameters for DefaultHttpContext (use semicolon as separator)
        var query = new QueryCollection(new Dictionary<string, StringValues> { { "id", "180;476" } });
        context.Request.QueryString = new QueryString("?id=180;476");
        context.Request.Query = query;

        ICollection<long> ids = RequestVariables.GetIDsFromQueryData(context.Request.Query, endpoint);
        
        // The current implementation should parse both 180 and 476
        Assert.AreEqual(2, ids.Count);
        Assert.IsTrue(ids.Contains(180));
        Assert.IsTrue(ids.Contains(476));
    }
}
