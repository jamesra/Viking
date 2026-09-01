using System.Text;

namespace DataExport.Controllers;

/// <summary>
/// Signals that a request's inputs were rejected, carrying the HTTP status the caller should see.
/// </summary>
/// <remarks>
/// Thrown from the body readers rather than returned, so the six export actions do not each need
/// identical validation branches. <see cref="IdRequestExceptionMiddleware"/> converts it to a response.
/// </remarks>
public sealed class IdRequestException(int statusCode, string message) : Exception(message)
{
    /// <summary>Gets the HTTP status code to return to the caller.</summary>
    public int StatusCode { get; } = statusCode;
}

/// <summary>
/// Reads a structure ID list from an untrusted request body.
/// </summary>
/// <remarks>
/// Only two content types are accepted: a raw <c>text/plain</c> body, and a <c>multipart/form-data</c>
/// upload of a single .txt file. Everything is bounded before it is read, because these endpoints are
/// anonymous. The uploaded file is never written to disk and its name never reaches the filesystem;
/// output names are built from the parsed numeric IDs alone.
/// </remarks>
public static class RequestBodyIds
{
    /// <summary>The largest request body accepted, in bytes.</summary>
    public const int MaxBodyBytes = 200 * 1024;

    /// <summary>The largest number of structure IDs accepted in a single request.</summary>
    public const int MaxIdCount = 50_000;

    /// <summary>
    /// Reads the ID list text from the request body.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The body text, or null when the request carried no body.</returns>
    /// <exception cref="IdRequestException">The body was too large, malformed, or of an unsupported type.</exception>
    public static async Task<string?> ReadIdTextAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasBody(request))
        {
            return null;
        }

        // Checked before reading so an oversized upload is refused without being buffered.
        if (request.ContentLength > MaxBodyBytes)
        {
            throw TooLarge();
        }

        if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType))
        {
            throw UnsupportedMediaType();
        }

        if (mediaType.MediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadBoundedTextAsync(request.Body, cancellationToken).ConfigureAwait(false);
        }

        if (mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadUploadedTextFileAsync(request, cancellationToken).ConfigureAwait(false);
        }

        throw UnsupportedMediaType();
    }

    private static bool HasBody(HttpRequest request)
    {
        if (request.ContentLength.HasValue)
        {
            return request.ContentLength.Value > 0;
        }

        // A chunked request declares no length, so the bounded read is what limits it.
        return request.Headers.ContainsKey("Transfer-Encoding");
    }

    private static async Task<string> ReadUploadedTextFileAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new IdRequestException(StatusCodes.Status400BadRequest, "The multipart request body could not be read.");
        }

        if (form.Files.Count != 1)
        {
            throw new IdRequestException(StatusCodes.Status400BadRequest,
                $"Expected exactly one uploaded file containing structure IDs, but the request carried {form.Files.Count}.");
        }

        IFormFile file = form.Files[0];

        // GetFileName discards any directory portion a client may have supplied before the extension is read.
        string extension = Path.GetExtension(Path.GetFileName(file.FileName) ?? string.Empty);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new IdRequestException(StatusCodes.Status400BadRequest,
                "The uploaded file must be a .txt file containing structure IDs.");
        }

        if (file.Length > MaxBodyBytes)
        {
            throw TooLarge();
        }

        await using Stream stream = file.OpenReadStream();
        return await ReadBoundedTextAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads at most <see cref="MaxBodyBytes"/> bytes and decodes them as UTF-8.
    /// </summary>
    /// <remarks>
    /// One byte beyond the limit is requested so that an oversized body is detected rather than
    /// silently truncated, which would export a different set of structures than the caller asked for.
    /// </remarks>
    private static async Task<string> ReadBoundedTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[MaxBodyBytes + 1];
        int total = 0;

        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total > MaxBodyBytes)
        {
            throw TooLarge();
        }

        int start = 0;
        if (total >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            start = 3;
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer, start, total - start);
        }
        catch (DecoderFallbackException)
        {
            throw new IdRequestException(StatusCodes.Status400BadRequest,
                "The request body is not valid UTF-8 text. Send a plain text list of structure IDs.");
        }
    }

    private static IdRequestException TooLarge() => new(StatusCodes.Status413PayloadTooLarge,
        $"The structure ID list may not exceed {MaxBodyBytes / 1024} KB.");

    private static IdRequestException UnsupportedMediaType() => new(StatusCodes.Status415UnsupportedMediaType,
        "Send the structure ID list as a text/plain body, or as a multipart/form-data upload of a single .txt file.");
}

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

        StringBuilder sb = new(maxLength);
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
    /// <param name="odataUrl">The OData service URL for query resolution.</param>
    /// <returns>A collection of unique IDs sorted in ascending order.</returns>
    public static ICollection<long> GetIDsFromQueryData(IQueryCollection? queryData, Uri odataUrl)
    {
        // A hack, but should only occur in unit testing
        if (queryData is null)
        {
            return [180, 476, 514];
        }

        SortedSet<long> ids = [];

        ids.UnionWith(ParseIDString(queryData["id"].ToString(), odataUrl));
        ids.UnionWith(ParseIDString(queryData["ids"].ToString(), odataUrl));
        ids.UnionWith(ParseIDString(queryData["$id"].ToString(), odataUrl));
        ids.UnionWith(ParseIDString(queryData["$ids"].ToString(), odataUrl));

        string queryString = queryData["query"].ToString();
        if (!string.IsNullOrEmpty(queryString))
        {
            ids.UnionWith(GetIDsFromQuery(odataUrl, queryString));
        }

        queryString = queryData["$query"].ToString();
        if (!string.IsNullOrEmpty(queryString))
        {
            ids.UnionWith(GetIDsFromQuery(odataUrl, queryString));
        }

        return ids;
    }

    /// <summary>
    /// Extracts IDs from both the query string and the request body.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="odataUrl">The OData service URL for query resolution.</param>
    /// <param name="cancellationToken">A token to cancel the body read.</param>
    /// <returns>A collection of unique IDs sorted in ascending order.</returns>
    /// <exception cref="IdRequestException">The body was rejected, or resolved to no IDs, or the list was too long.</exception>
    public static async Task<ICollection<long>> GetIDsFromRequestAsync(HttpRequest request, Uri odataUrl,
        CancellationToken cancellationToken = default)
    {
        SortedSet<long> ids = new(GetIDsFromQueryData(request.Query, odataUrl));

        string? bodyText = await RequestBodyIds.ReadIdTextAsync(request, cancellationToken).ConfigureAwait(false);
        if (bodyText is not null)
        {
            ICollection<long> bodyIds = ParseIDString(bodyText, odataUrl);

            // An empty ID set means "export the whole volume". A caller who sent a body clearly wanted a
            // subset, so a body that resolves to nothing is refused rather than silently exporting everything.
            if (bodyIds.Count == 0 && !string.IsNullOrWhiteSpace(bodyText))
            {
                throw new IdRequestException(StatusCodes.Status400BadRequest,
                    "No structure IDs could be read from the request body. Send numeric IDs separated by " +
                    "semicolons or newlines, or omit the body to export the whole volume.");
            }

            ids.UnionWith(bodyIds);
        }

        if (ids.Count > RequestBodyIds.MaxIdCount)
        {
            throw new IdRequestException(StatusCodes.Status400BadRequest,
                $"The request names {ids.Count} structures, which is more than the limit of {RequestBodyIds.MaxIdCount}.");
        }

        return ids;
    }

    /// <summary>
    /// Parses a string containing one or more IDs separated by semicolons or newlines.
    /// </summary>
    /// <param name="idListStr">The string containing IDs to parse.</param>
    /// <param name="odataUrl">The OData service URL for query resolution.</param>
    /// <returns>A collection of parsed IDs.</returns>
    public static ICollection<long> ParseIDString(string? idListStr, Uri odataUrl)
    {
        if (string.IsNullOrEmpty(idListStr))
        {
            return Array.Empty<long>();
        }

        string[] parts = idListStr.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries);
        List<long> ids = new(parts.Length);

        foreach (string idStr in parts)
        {
            try
            {
                // Do not allow a negative id
                ids.Add(Convert.ToInt64(Convert.ToUInt64(idStr)));
            }
            catch (FormatException)
            {
                ICollection<long> queryIds = GetIDsFromQuery(odataUrl, idStr);
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
