using System.Text;
using DataExport.Controllers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DataExport.Tests;

/// <summary>
/// Pins the file lifecycle of <see cref="ExportFile"/>: every export gets its own file under Output/, the
/// caller still sees the readable download name, and the file is gone once the response ends, whether it
/// was sent in full, aborted by the client, or never started. Offline: no OData or SQL.
/// </summary>
[TestClass]
public class ExportFileTest
{
    private string _contentRoot = string.Empty;

    private string OutputDirectory => Path.Combine(_contentRoot, ExportFile.OutputFolderName);

    [TestInitialize]
    public void CreateContentRoot()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "DataExport.Tests", "ExportFile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);
    }

    [TestCleanup]
    public void DeleteContentRoot()
    {
        try
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static Action<string> Writes(string text) => path => File.WriteAllText(path, text);

    private static string OnDiskPath(FileStreamResult result) => ((FileStream)result.FileStream).Name;

    private static async Task<string> ReadToEndAsync(FileStreamResult result)
    {
        using StreamReader reader = new(result.FileStream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private string[] FilesInOutput() =>
        Directory.Exists(OutputDirectory) ? Directory.GetFiles(OutputDirectory) : [];

    /// <summary>
    /// The collision this class exists to prevent: the old code saved to Output/&lt;download name&gt;, so a
    /// second export with the same name while the first was streaming threw a sharing violation or
    /// truncated the first caller's file.
    /// </summary>
    [TestMethod]
    public async Task TwoExportsWithTheSameDownloadNameDoNotShareAFile()
    {
        const string name = "nw-180_476_hops_1 nw-2026-10-28 075643.tlp";

        FileStreamResult first = ExportFile.Send(new DefaultHttpContext(), _contentRoot, name, "text/plain", Writes("first export"));
        FileStreamResult second = ExportFile.Send(new DefaultHttpContext(), _contentRoot, name, "text/plain", Writes("second export"));

        try
        {
            Assert.AreNotEqual(OnDiskPath(first), OnDiskPath(second));
            Assert.AreEqual("first export", await ReadToEndAsync(first));
            Assert.AreEqual("second export", await ReadToEndAsync(second));
        }
        finally
        {
            first.FileStream.Dispose();
            second.FileStream.Dispose();
        }
    }

    [TestMethod]
    public void DownloadNameAndContentTypeAreWhatTheCallerGave()
    {
        const string name = "morph-180_476 nw-2026-10-28 075643.json";

        FileStreamResult result = ExportFile.Send(new DefaultHttpContext(), _contentRoot, name, "application/json", Writes("{}"));
        using Stream _ = result.FileStream;

        Assert.AreEqual(name, result.FileDownloadName);
        Assert.AreEqual("application/json", result.ContentType);
    }

    [TestMethod]
    public void OnDiskNameIsPrivateToOutputAndKeepsTheExtension()
    {
        string longName = "nw-" + OutputNameGenerator.GetFileFriendlyIDList(Enumerable.Range(1, 5000).Select(i => (long)i).ToList())
            + "_hops_4294967295 nw-2026-10-28 075643.graphml";

        FileStreamResult result = ExportFile.Send(new DefaultHttpContext(), _contentRoot, longName, "text/plain", Writes("<graphml/>"));
        using Stream _ = result.FileStream;
        string path = OnDiskPath(result);

        Assert.AreEqual(Path.GetFullPath(OutputDirectory), Path.GetDirectoryName(path));
        Assert.AreEqual(".graphml", Path.GetExtension(path));
        Assert.AreEqual(32, Path.GetFileNameWithoutExtension(path).Length, "a GUID in N format, whatever the download name's length");
        Assert.IsFalse(path.Contains("nw-", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FileIsDeletedWhenTheStreamCloses()
    {
        FileStreamResult result = ExportFile.Send(new DefaultHttpContext(), _contentRoot, "a.tlp", "text/plain", Writes("tlp body"));
        string path = OnDiskPath(result);
        Assert.IsTrue(File.Exists(path), "the file must survive until the response has been streamed");

        Assert.AreEqual("tlp body", await ReadToEndAsync(result));
        Assert.IsTrue(File.Exists(path), "reading to the end does not delete it; closing does");

        result.FileStream.Dispose();

        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(0, FilesInOutput().Length);
    }

    [TestMethod]
    public void FailedSaveLeavesNoFileAndPropagates()
    {
        Action<string> partialSave = path =>
        {
            File.WriteAllText(path, "half an export");
            throw new IOException("disk full");
        };

        IOException ex = Assert.ThrowsException<IOException>(() =>
            ExportFile.Send(new DefaultHttpContext(), _contentRoot, "a.json", "application/json", partialSave));

        Assert.AreEqual("disk full", ex.Message);
        Assert.AreEqual(0, FilesInOutput().Length);
    }

    /// <summary>When the half-written file cannot be deleted either, the caller still sees the save's own error.</summary>
    [TestMethod]
    public void SaveErrorIsReportedEvenWhenCleanupFails()
    {
        FileStream? held = null;
        Action<string> saveThatKeepsTheFileOpen = path =>
        {
            held = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            throw new InvalidOperationException("save failed");
        };

        try
        {
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() =>
                ExportFile.Send(new DefaultHttpContext(), _contentRoot, "a.tlp", "text/plain", saveThatKeepsTheFileOpen));
            Assert.AreEqual("save failed", ex.Message);
        }
        finally
        {
            held?.Dispose();
        }
    }

    /// <summary>Arguments are checked before anything is written, so a bad call leaves no file behind.</summary>
    [TestMethod]
    public void NullArgumentsAreRefusedBeforeAnyFileIsWritten()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            ExportFile.Send(null!, _contentRoot, "a.tlp", "text/plain", Writes("x")));
        Assert.ThrowsException<ArgumentNullException>(() =>
            ExportFile.Send(new DefaultHttpContext(), _contentRoot, "a.tlp", "text/plain", null!));

        Assert.AreEqual(0, FilesInOutput().Length);
    }

    /// <summary>A request that ends before the result runs still disposes the stream, so the file is removed.</summary>
    [TestMethod]
    public async Task FileIsDeletedWhenTheRequestCompletesWithoutTheResultRunning()
    {
        RecordingResponseFeature feature = new();
        DefaultHttpContext context = new();
        context.Features.Set<IHttpResponseFeature>(feature);

        FileStreamResult result = ExportFile.Send(context, _contentRoot, "a.dot", "text/plain", Writes("digraph {}"));
        string path = OnDiskPath(result);
        Assert.IsTrue(File.Exists(path));

        await feature.CompleteAsync();

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public async Task ExecutedResultStreamsTheWholeFileThenDeletesIt()
    {
        DefaultHttpContext context = CreateExecutableContext(new MemoryStream());
        string body = new('x', 200_000);

        FileStreamResult result = ExportFile.Send(context, _contentRoot, "nw-1 nw-2026-10-28 075643.tlp", "text/plain", Writes(body));
        string path = OnDiskPath(result);

        await result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));

        MemoryStream sent = (MemoryStream)context.Response.Body;
        Assert.AreEqual(body, Encoding.UTF8.GetString(sent.ToArray()));
        StringAssert.Contains(context.Response.Headers.ContentDisposition.ToString(), "nw-1 nw-2026-10-28 075643.tlp");
        Assert.IsFalse(File.Exists(path));
    }

    /// <summary>A client that disconnects mid-download makes the write throw; the file is still removed.</summary>
    [TestMethod]
    public async Task AbortedDownloadStillDeletesTheFile()
    {
        DefaultHttpContext context = CreateExecutableContext(new FailAfterStream(bytesBeforeFailure: 10_000));

        FileStreamResult result = ExportFile.Send(context, _contentRoot, "a.json", "application/json", Writes(new string('y', 500_000)));
        string path = OnDiskPath(result);

        await Assert.ThrowsExceptionAsync<IOException>(() =>
            result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor())));

        Assert.IsFalse(File.Exists(path));
    }

    private static DefaultHttpContext CreateExecutableContext(Stream responseBody)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMvcCore();

        DefaultHttpContext context = new()
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Response.Body = responseBody;
        return context;
    }

    /// <summary>Records <see cref="IHttpResponseFeature.OnCompleted"/> callbacks so a test can end the request.</summary>
    private sealed class RecordingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _completed = [];

        public override void OnCompleted(Func<object, Task> callback, object state) => _completed.Add((callback, state));

        public async Task CompleteAsync()
        {
            foreach ((Func<object, Task> callback, object state) in _completed)
            {
                await callback(state);
            }
        }
    }

    /// <summary>A response body that accepts some bytes and then fails, as a closed client connection does.</summary>
    private sealed class FailAfterStream(int bytesBeforeFailure) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Length + buffer.Length > bytesBeforeFailure)
            {
                throw new IOException("The client disconnected.");
            }

            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
