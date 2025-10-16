namespace DataExport.Controllers;

/// <summary>
/// Helper class to generate names for generated files.
/// </summary>
public static class OutputNameGenerator
{
    /// <summary>
    /// Gets a file-friendly date string for use in filenames.
    /// </summary>
    /// <returns>A formatted date-time string.</returns>
    public static string GetFileFriendlyDateString()
    {
        DateTime now = DateTime.Now;
        return $"nw-{now.Year:d4}-{now.Month:d2}-{now.Day:d2} {now.Hour:d2}{now.Minute:d2}{now.Second:d2}";
    }

    /// <summary>
    /// Converts a collection of IDs into a file-friendly string list.
    /// </summary>
    /// <param name="requestIDs">The collection of IDs to convert.</param>
    /// <param name="maxLength">The maximum length of the resulting string.</param>
    /// <returns>A string representation of the ID list.</returns>
    public static string GetFileFriendlyIDList(ICollection<long> requestIDs, int maxLength = 140)
    {
        if (requestIDs.Count == 0)
        {
            return "ALL";
        }

        var sb = new System.Text.StringBuilder(maxLength);
        bool first = true;
        
        foreach (long id in requestIDs)
        {
            if (!first)
            {
                sb.Append('_');
            }
            first = false;

            sb.Append(id);
            if (sb.Length > maxLength)
            {
                sb.Append("etc");
                break;
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// A helper class to pull common URL query parameters from requests.
/// </summary>
public static class RequestVariables
{
    /// <summary>
    /// Extracts IDs from query data, supporting multiple parameter names and query strings.
    /// </summary>
    /// <param name="queryData">The query collection from the HTTP request.</param>
    /// <returns>A collection of unique IDs sorted in ascending order.</returns>
    public static ICollection<long> GetIDsFromQueryData(IQueryCollection? queryData)
    {
        // A hack, but should only occur in unit testing
        if (queryData is null)
        {
            return new long[] { 180, 476, 514 };
        }

        var ids = new SortedSet<long>();

        ids.UnionWith(ParseIDString(queryData["id"].ToString()));
        ids.UnionWith(ParseIDString(queryData["ids"].ToString()));
        ids.UnionWith(ParseIDString(queryData["$id"].ToString()));
        ids.UnionWith(ParseIDString(queryData["$ids"].ToString()));

        string queryString = queryData["query"].ToString();
        if (!string.IsNullOrEmpty(queryString))
        {
            ids.UnionWith(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, queryString));
        }

        queryString = queryData["$query"].ToString();
        if (!string.IsNullOrEmpty(queryString))
        {
            ids.UnionWith(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, queryString));
        }

        return ids;
    }

    /// <summary>
    /// Parses a string containing one or more IDs separated by semicolons or newlines.
    /// </summary>
    /// <param name="idListStr">The string containing IDs to parse.</param>
    /// <returns>A collection of parsed IDs.</returns>
    public static ICollection<long> ParseIDString(string? idListStr)
    {
        if (string.IsNullOrEmpty(idListStr))
        {
            return Array.Empty<long>();
        }

        string[] parts = idListStr.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var ids = new List<long>(parts.Length);
        
        foreach (string idStr in parts)
        {
            try
            {
                // Do not allow a negative id
                ids.Add(Convert.ToInt64(Convert.ToUInt64(idStr)));
            }
            catch (FormatException)
            {
                ICollection<long> queryIds = GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, idStr);
                ids.AddRange(queryIds);
            }
        }

        return ids;
    }
    
    /// <summary>
    /// Retrieves IDs from an OData query string.
    /// </summary>
    /// <param name="odataUri">The OData service URI.</param>
    /// <param name="query">The OData query string.</param>
    /// <returns>A collection of IDs matching the query.</returns>
    public static ICollection<long> GetIDsFromQuery(Uri odataUri, string query)
    {
        // TODO: Replace with AnnotationVizLibODataClient implementation
        // For now, return empty collection
        System.Diagnostics.Trace.WriteLine($"OData query not implemented: {query}");
        return Array.Empty<long>();
    }

    /// <summary>
    /// Asynchronously retrieves IDs from an OData query string.
    /// </summary>
    /// <param name="odataUri">The OData service URI.</param>
    /// <param name="query">The OData query string.</param>
    /// <returns>A task representing the asynchronous operation with a collection of IDs.</returns>
    public static async Task<ICollection<long>> GetIDsFromQueryAsync(Uri odataUri, string query)
    {
        // TODO: Replace with AnnotationVizLibODataClient implementation
        // For now, return empty collection
        await Task.CompletedTask;
        System.Diagnostics.Trace.WriteLine($"OData query not implemented: {query}");
        return Array.Empty<long>();
    }
}
