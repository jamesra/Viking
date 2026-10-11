using System.Text;
using DataExport.Controllers;

namespace DataExport.Tests;

/// <summary>
/// Pins the limits and rejections of <see cref="RequestBodyIds"/> and the file-name rules of
/// <see cref="OutputNameGenerator"/>. The export endpoints are anonymous, so these bounds are the only
/// guard against oversized or malformed uploads; the name generator output becomes a download file name.
/// </summary>
[TestClass]
public class RequestBodyIdsTest
{
    private static DefaultHttpContext PlainTextRequest(byte[] body, string contentType = "text/plain", bool declareLength = true)
    {
        DefaultHttpContext context = new();
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(body);
        if (declareLength)
        {
            context.Request.ContentLength = body.Length;
        }
        else
        {
            context.Request.Headers["Transfer-Encoding"] = "chunked";
        }

        return context;
    }

    private static DefaultHttpContext MultipartRequest(params (string FileName, byte[] Content)[] files)
    {
        const string boundary = "XBOUNDARYX";
        using MemoryStream ms = new();
        foreach (var (fileName, content) in files)
        {
            byte[] head = Encoding.ASCII.GetBytes(
                $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\nContent-Type: text/plain\r\n\r\n");
            ms.Write(head);
            ms.Write(content);
            ms.Write(Encoding.ASCII.GetBytes("\r\n"));
        }

        ms.Write(Encoding.ASCII.GetBytes($"--{boundary}--\r\n"));
        return PlainTextRequest(ms.ToArray(), $"multipart/form-data; boundary={boundary}");
    }

    private static async Task<IdRequestException> AssertRejectedAsync(DefaultHttpContext context)
    {
        try
        {
            await RequestBodyIds.ReadIdTextAsync(context.Request);
        }
        catch (IdRequestException ex)
        {
            return ex;
        }

        Assert.Fail("Expected an IdRequestException.");
        throw new InvalidOperationException();
    }

    [TestMethod]
    public async Task EmptyBodyReturnsNull()
    {
        DefaultHttpContext context = PlainTextRequest([]);

        Assert.IsNull(await RequestBodyIds.ReadIdTextAsync(context.Request));
    }

    [TestMethod]
    public async Task PlainTextBodyIsReturnedAndUtf8BomIsStripped()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] body = [.. bom, .. Encoding.UTF8.GetBytes("180;476")];

        string? text = await RequestBodyIds.ReadIdTextAsync(PlainTextRequest(body).Request);

        Assert.AreEqual("180;476", text);
    }

    [TestMethod]
    public async Task PlainTextBodyAtTheLimitIsAccepted()
    {
        byte[] body = new byte[RequestBodyIds.MaxBodyBytes];
        Array.Fill(body, (byte)'1');

        string? text = await RequestBodyIds.ReadIdTextAsync(PlainTextRequest(body).Request);

        Assert.AreEqual(RequestBodyIds.MaxBodyBytes, text?.Length);
    }

    [TestMethod]
    public async Task DeclaredOversizedBodyIsRefusedWith413()
    {
        DefaultHttpContext context = PlainTextRequest(new byte[RequestBodyIds.MaxBodyBytes + 1]);

        IdRequestException ex = await AssertRejectedAsync(context);

        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, ex.StatusCode);
    }

    [TestMethod]
    public async Task ChunkedOversizedBodyIsRefusedNotTruncated()
    {
        byte[] body = new byte[RequestBodyIds.MaxBodyBytes + 1];
        Array.Fill(body, (byte)'1');
        DefaultHttpContext context = PlainTextRequest(body, declareLength: false);

        IdRequestException ex = await AssertRejectedAsync(context);

        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, ex.StatusCode);
    }

    [TestMethod]
    public async Task InvalidUtf8IsRefusedWith400()
    {
        DefaultHttpContext context = PlainTextRequest([0x31, 0xFF, 0xFE, 0x32]);

        IdRequestException ex = await AssertRejectedAsync(context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [DataTestMethod]
    [DataRow("application/json")]
    [DataRow("application/x-www-form-urlencoded")]
    [DataRow("not a media type")]
    public async Task OtherContentTypesAreRefusedWith415(string contentType)
    {
        DefaultHttpContext context = PlainTextRequest(Encoding.UTF8.GetBytes("180"), contentType);

        IdRequestException ex = await AssertRejectedAsync(context);

        Assert.AreEqual(StatusCodes.Status415UnsupportedMediaType, ex.StatusCode);
    }

    [TestMethod]
    public async Task ContentTypeMatchIsCaseInsensitive()
    {
        string? text = await RequestBodyIds.ReadIdTextAsync(
            PlainTextRequest(Encoding.UTF8.GetBytes("180"), "TEXT/Plain; charset=utf-8").Request);

        Assert.AreEqual("180", text);
    }

    [TestMethod]
    public async Task SingleTxtUploadIsReadAndDirectoryPartOfNameIsIgnored()
    {
        DefaultHttpContext context = MultipartRequest((@"..\..\evil\ids.TXT", Encoding.UTF8.GetBytes("180\n476")));

        string? text = await RequestBodyIds.ReadIdTextAsync(context.Request);

        Assert.AreEqual("180\n476", text);
    }

    [TestMethod]
    public async Task UploadWithNonTxtExtensionIsRefusedWith400()
    {
        DefaultHttpContext context = MultipartRequest(("ids.txt.exe", Encoding.UTF8.GetBytes("180")));

        IdRequestException ex = await AssertRejectedAsync(context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [TestMethod]
    public async Task UploadOfTwoFilesIsRefusedWith400()
    {
        DefaultHttpContext context = MultipartRequest(
            ("a.txt", Encoding.UTF8.GetBytes("1")),
            ("b.txt", Encoding.UTF8.GetBytes("2")));

        IdRequestException ex = await AssertRejectedAsync(context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [TestMethod]
    public void EmptyIdListIsNamedAll()
    {
        Assert.AreEqual("ALL", OutputNameGenerator.GetFileFriendlyIDList(new List<long>()));
    }

    [TestMethod]
    public void IdListIsUnderscoreJoined()
    {
        Assert.AreEqual("180_476_3", OutputNameGenerator.GetFileFriendlyIDList(new List<long> { 180, 476, 3 }));
    }

    [TestMethod]
    public void LongIdListIsCutOffWithEtcAndStaysShort()
    {
        List<long> ids = Enumerable.Range(100000, 500).Select(i => (long)i).ToList();

        string name = OutputNameGenerator.GetFileFriendlyIDList(ids, maxLength: 20);

        Assert.IsTrue(name.EndsWith("etc", StringComparison.Ordinal));
        Assert.IsTrue(name.Length <= 20 + "_".Length + 20 + "etc".Length, name);
        Assert.IsTrue(name.StartsWith("100000_100001_", StringComparison.Ordinal));
    }

    [TestMethod]
    public void IdListAtExactlyTheLimitHasNoEtc()
    {
        // "12_34" is 5 characters; the cut-off only applies when the length exceeds maxLength.
        Assert.AreEqual("12_34", OutputNameGenerator.GetFileFriendlyIDList(new List<long> { 12, 34 }, maxLength: 5));
    }

    [TestMethod]
    public void DateStringHasFileSafeShape()
    {
        string s = OutputNameGenerator.GetFileFriendlyDateString();

        StringAssert.Matches(s, new System.Text.RegularExpressions.Regex(@"^nw-\d{4}-\d{2}-\d{2} \d{6}$"));
    }
}
