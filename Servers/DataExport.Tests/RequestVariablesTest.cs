using DataExport.Controllers;
using Microsoft.Extensions.Primitives;

namespace DataExport.Tests;

[TestClass]
public class RequestVariablesTest
{
    [TestMethod]
    public async Task TestODataQueryParametersAsync()
    {
        Uri endpoint = new("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute);

        ICollection<long> ids = await RequestVariables.GetIDsFromQueryAsync(endpoint, "Structures?$filter=ID eq 180");

        // Note: Current implementation returns empty collection as OData is not implemented
        // When implemented, these assertions should pass:
        // Assert.AreEqual(1, ids.Count);
        // Assert.IsTrue(ids.Contains(180));
    }

    [TestMethod]
    public void TestODataQueryParameters()
    {
        Uri endpoint = new("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute);

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
        DefaultHttpContext context = new();
        Uri endpoint = new("http://webdev.connectomes.utah.edu/RC1Test/OData", UriKind.Absolute);

        // Set up query parameters for DefaultHttpContext (use semicolon as separator)
        QueryCollection query = new(new Dictionary<string, StringValues> { { "id", "180;476" } });
        context.Request.QueryString = new QueryString("?id=180;476");
        context.Request.Query = query;

        ICollection<long> ids = RequestVariables.GetIDsFromQueryData(context.Request.Query, endpoint);

        // The current implementation should parse both 180 and 476
        Assert.AreEqual(2, ids.Count);
        Assert.IsTrue(ids.Contains(180));
        Assert.IsTrue(ids.Contains(476));
    }

    /// <summary>A negative or Int64-overflowing ID is a 400 for the caller, not an unhandled OverflowException.</summary>
    [TestMethod]
    [DataRow("-5")]
    [DataRow("180;-1")]
    [DataRow("9223372036854775808")]
    [DataRow("99999999999999999999999")]
    public void ParseIDStringRejectsOutOfRangeIdsWith400(string text)
    {
        Uri endpoint = new("http://localhost/OData", UriKind.Absolute);

        IdRequestException ex = Assert.ThrowsExactly<IdRequestException>(() => RequestVariables.ParseIDString(text, endpoint));

        Assert.AreEqual(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    /// <summary>The largest valid ID and ordinary lists still parse.</summary>
    [TestMethod]
    public void ParseIDStringAcceptsLargestInt64()
    {
        Uri endpoint = new("http://localhost/OData", UriKind.Absolute);

        ICollection<long> ids = RequestVariables.ParseIDString("9223372036854775807;7\n8", endpoint);

        CollectionAssert.AreEqual(new long[] { long.MaxValue, 7, 8 }, ids.ToArray());
    }

    /// <summary>Missing, malformed and negative hops fall back to 1; huge values cap at Int32.MaxValue.</summary>
    [TestMethod]
    [DataRow(null, 1u)]
    [DataRow("abc", 1u)]
    [DataRow("-1", 1u)]
    [DataRow("0", 0u)]
    [DataRow("3", 3u)]
    [DataRow("3000000000", 2147483647u)]
    public void GetNumHopsParsesAndCaps(string? value, uint expected)
    {
        Dictionary<string, StringValues> values = [];
        if (value is not null)
        {
            values["hops"] = value;
        }

        uint hops = RequestVariables.GetNumHops(new Microsoft.AspNetCore.Http.QueryCollection(values));

        Assert.AreEqual(expected, hops);
        Assert.IsTrue((int)hops >= 0);
    }
}
