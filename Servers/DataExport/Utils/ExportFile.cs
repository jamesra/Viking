namespace DataExport.Controllers;

/// <summary>
/// Writes one export to a private file under <c>Output/</c> and streams it to the caller, deleting the
/// file once the response is finished. Used by every Network, Morphology and Motif export action.
/// </summary>
/// <remarks>
/// The on-disk name is a fresh GUID, never the download name. Download names carry only the requested IDs
/// (cut at 140 characters) and a date to the second, so two requests for the same IDs in the same second,
/// or two long ID lists with the same prefix, used to write the same path: the second save threw a
/// sharing violation or truncated the file the first response was still streaming. Nothing serves or
/// reuses <c>Output/</c> later (it is outside the web root), so each file lives only for its own response.
/// </remarks>
public static class ExportFile
{
    /// <summary>The folder under the content root that holds exports while they are streamed.</summary>
    public const string OutputFolderName = "Output";

    /// <summary>
    /// Saves an export with <paramref name="save"/> and returns it as a download named
    /// <paramref name="downloadName"/>.
    /// </summary>
    /// <param name="httpContext">The request; the stream is registered for disposal on its response.</param>
    /// <param name="contentRootPath">The application content root; the file goes in its <see cref="OutputFolderName"/> folder.</param>
    /// <param name="downloadName">The file name the caller sees; only its extension reaches the disk.</param>
    /// <param name="contentType">The response media type.</param>
    /// <param name="save">Writes the export to the full path it is given and closes the file.</param>
    /// <returns>
    /// A result that streams the file and deletes it when the stream closes: after the last byte is sent,
    /// or when the client aborts the download. A file whose save or open fails is deleted before the
    /// exception propagates.
    /// </returns>
    public static FileStreamResult Send(HttpContext httpContext, string contentRootPath, string downloadName,
        string contentType, Action<string> save)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(save);

        string outputDirectory = Directory.CreateDirectory(Path.Combine(contentRootPath, OutputFolderName)).FullName;
        string path = CreateUniquePath(outputDirectory, Path.GetExtension(downloadName));

        FileStream stream;
        try
        {
            save(path);
            stream = OpenForStreaming(path);
        }
        catch
        {
            TryDelete(path);
            throw;
        }

        // FileStreamResult disposes the stream when it finishes writing. Registering it as well covers a
        // request that ends before the result runs, so the file never outlives its request.
        httpContext.Response.RegisterForDispose(stream);
        return new FileStreamResult(stream, contentType) { FileDownloadName = downloadName };
    }

    /// <summary>Returns a path in <paramref name="outputDirectory"/> that no other export uses.</summary>
    /// <param name="outputDirectory">The folder the file goes in.</param>
    /// <param name="extension">The extension including its dot, as <see cref="Path.GetExtension(string)"/> returns it.</param>
    public static string CreateUniquePath(string outputDirectory, string extension) =>
        Path.Combine(outputDirectory, Guid.NewGuid().ToString("N") + extension);

    /// <summary>
    /// Opens a saved export for reading; the file is deleted when the stream is closed (on Windows, by the
    /// operating system, so also when the process dies mid-download).
    /// </summary>
    public static FileStream OpenForStreaming(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The original failure is the one worth reporting; a leftover file is harmless.
        }
    }
}
