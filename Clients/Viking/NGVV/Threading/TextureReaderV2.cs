using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Viking.VolumeModel;
using Viking.ViewModels;

namespace Viking;


class TextureReaderV2 : IDisposable
{
    readonly Uri Filename;
    readonly string CacheFilename = string.Empty;
    GraphicsDevice? graphicsDevice = null;
    private Texture2D? _Result = null;
    private ManualResetEvent? DoneEvent = new(false);
    public bool FinishedReading = false;
    //        public RefreshDelegate RefreshMethod; 

    public static int nextid = 0;

    private readonly Action? OnCompletionCallback;

    public int ID { get; private set; }

    //AsyncState BodyRequestState = null;

    private bool IsDisposed = false;

    static readonly System.Net.Cache.RequestCachePolicy HeaderCachePolicy = new(System.Net.Cache.RequestCacheLevel.Revalidate);
    static readonly System.Net.Cache.RequestCachePolicy BodyCachePolicy = new(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

    private bool _TextureNotFound = false;


    static TextureReaderV2()
    {

    }

    /// <summary>
    /// TextureNotFound is set to true when we successfully communicated with the server and it did not have the requested texture
    /// </summary>
    public bool TextureNotFound
    {
        get => _TextureNotFound;
        protected set => _TextureNotFound = value;
    }

    private static bool TextureErrorReported = false;

    private readonly int MipMapLevels = 1;

    /// <summary>
    /// Returns true if a call to GetTexture will return a non-null value
    /// </summary>
    public bool HasTexture => _Result != null;

    /// <summary>
    /// Set to true when the reader has been aborted
    /// </summary>
    protected bool Aborted => CancelToken?.IsCancellationRequested ?? false;

    //private Object thisLock = new Object();

    private readonly ReaderWriterLockSlim rwResultLock = new();

    /// <summary>
    /// Returns the result.  This method can only be called once.
    /// By taking the texture you are responsible for calling dispose.
    /// </summary>
    /// <returns></returns>
    public Texture2D? GetTexture()
    {
        Texture2D? retVal;
        try
        {
            rwResultLock.EnterUpgradeableReadLock();
            if (_Result is null)
                return null;

            try
            {
                rwResultLock.EnterWriteLock();
                retVal = _Result;
                _Result = null;
            }
            finally
            {
                rwResultLock.ExitWriteLock();
            }
        }
        finally
        {
            rwResultLock.ExitUpgradeableReadLock();
        }

        return retVal;
    }

    private readonly CancellationTokenSource CancelToken;

    /// <summary>
    /// When provided, used only for semaphore waits so section change cancels waiters; in-flight loads use CancelToken only.
    /// </summary>
    private readonly CancellationToken? _sectionToken;

    public bool SectionLoadCancelled => _sectionToken?.IsCancellationRequested ?? false;

    /// <summary>
    /// When non-null, ProcessQueue will call SetTextureFromQueue on this tile after creating the texture.
    /// </summary>
    private readonly TileView? TileViewOwner;

    public TextureReaderV2(GraphicsDevice graphicsDevice, Uri textureUri, string cacheFilename, int mipMapLevels, Action? OnCompletion, CancellationTokenSource token, TileView? tileViewOwner = null, CancellationToken? sectionToken = null)
        : this(graphicsDevice, textureUri, mipMapLevels, OnCompletion, token, tileViewOwner, sectionToken)
    {
        CacheFilename = cacheFilename;
    }

    /// <summary>
    /// This texture reader is used when we don't have a cachepath to check before making the request
    /// </summary>
    /// <param name="graphicsDevice"></param>
    /// <param name="filename"></param>
    /// <param name="downsample"></param>
    /// <param name="tileViewOwner">When provided, the created texture is assigned to this tile via PendingTextureQueue.</param>
    /// <param name="sectionToken">When provided, used only for semaphore waits; section change cancels waiters while in-flight loads continue.</param>
    public TextureReaderV2(GraphicsDevice graphicsDevice, Uri textureURI, int mipMapLevels, Action? OnCompletion, CancellationTokenSource token, TileView? tileViewOwner = null, CancellationToken? sectionToken = null)
    {
        CancelToken = token;
        _sectionToken = sectionToken;
        this.OnCompletionCallback = OnCompletion;
        this.TileViewOwner = tileViewOwner;
        this.ID = TextureReaderV2.nextid++;
        this.graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        this.Filename = textureURI;
        this.MipMapLevels = mipMapLevels;

        //Trace.WriteLine("Create TextureReader for " + textureURI.ToString());
#if DEBUG
        Viking.Global.AddTextureReader(this, this.Filename.ToString());
#endif
    }

    public override string ToString() => "TR: " + this.ID.ToString();

    public void AbortRequest() =>
        //In case we have finished loading the texture, but the texture has not been assigned to the tile, 
        //dispose of the texture
        CancelToken.Cancel();

    private static void HandleCachedFileException(Exception e, string CacheFilename) =>
        //Trace.WriteLine(e.Message, "TextureUse");
        DeleteFileFromCache(CacheFilename);


    private static long TriedToCreateDirectory = 0;
    private static void DeleteFileFromCache(string CacheFilename)
    {
        if (string.IsNullOrEmpty(CacheFilename))
            return;

        try
        {
            LocalTextureCache.DeleteCachedFile(CacheFilename);
        }
        catch (System.IO.DirectoryNotFoundException)
        {
            if (Interlocked.Read(ref TriedToCreateDirectory) == 0)
            {
                Trace.WriteLine($"Failed To delete cache file from non-existant directory (probably OK): {CacheFilename}", "TextureUse");
                TryCreatingCacheDirectory(CacheFilename);
            }
        }
    }

    private static void TryCreatingCacheDirectory(string cachefilename)
    {
        if (Interlocked.Read(ref TriedToCreateDirectory) == 0)
        {
            var dirname = System.IO.Path.GetDirectoryName(cachefilename);
            try
            {
                System.IO.Directory.CreateDirectory(dirname);
            }
            catch
            {
                Trace.WriteLine($"Unable to create cache directory {dirname ?? "null"}");
            }
        }
    }

    /// <summary>
    /// True when the cache path exists and has non-zero length. Used to prefer disk loads and bypass HTTP throttle.
    /// </summary>
    internal static bool HasUsableCacheFile(string? cacheFilename)
    {
        if (string.IsNullOrEmpty(cacheFilename))
            return false;

        try
        {
            var info = new FileInfo(cacheFilename);
            return info.Exists && info.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Load texture bytes from the local cache only — no HTTP. Returns null if missing, empty, or corrupt.
    /// </summary>
    internal async Task<Texture2D?> TryLoadingFromDiskOnly(string cacheFilename, CancellationToken token)
    {
        try
        {
            if (string.IsNullOrEmpty(cacheFilename))
                return null;

            FileInfo cacheFileInfo = new(cacheFilename);
            if (cacheFileInfo.Directory?.Exists == false)
            {
                TryCreatingCacheDirectory(cacheFilename);
                return null;
            }

            if (cacheFileInfo.Exists == false)
                return null;

            if (cacheFileInfo.Length == 0)
            {
                DeleteFileFromCache(cacheFilename);
                return null;
            }

            if (token.IsCancellationRequested)
                return null;

            FileStream stream;
            try
            {
                stream = new FileStream(cacheFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                return null;
            }

            using (stream)
            {
                if (token.IsCancellationRequested)
                    return null;

                return await GetTextureFromStreamAsync(graphicsDevice, stream).ConfigureAwait(false);
            }
        }
        catch (ArgumentException e)
        {
            HandleCachedFileException(e, cacheFilename);
            return null;
        }
        catch (InvalidOperationException e)
        {
            HandleCachedFileException(e, cacheFilename);
            return null;
        }
        catch (Exception e)
        {
            HandleCachedFileException(e, cacheFilename);
            throw;
        }
    }

    /// <summary>
    /// Disk-first cache load (no HTTP). Network download is handled by TryLoadingFromServer.
    /// </summary>
    internal async Task<Texture2D?> TryLoadingFromCacheOrServer(Uri textureUri, string CacheFilename, CancellationToken token)
    {
        return await TryLoadingFromDiskOnly(CacheFilename, token).ConfigureAwait(false);
    }

    private async Task<Texture2D> TryLoadingFromServer(Uri textureUri, CancellationToken token)
    {
        if (Aborted || IsDisposed)
            return null;

        //Trace.WriteLine("Checking server: " + textureUri.ToString() + " thread #" + Thread.CurrentThread.ManagedThreadId.ToString());
        try
        {
            HttpClient client = Global.HttpClient;
            {
                int nRetries = 5;

                HttpResponseMessage? response = null;
                while (nRetries >= 0)
                {
                    response = await client
                        .GetAsync(textureUri, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
                    if (false == response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                            response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                        {
                            nRetries--;
                            Debug.WriteLine($"Failed to load {textureUri} : Delaying for retry");
                            await Task.Delay(Geometry.Global.GetRandomRequestDelay(), token);
                            continue;
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            this.TextureNotFound = true;
                            break;
                        }
                    }
                    else
                    {
                        return await TryLoadingFromHttpClientResponse(response, CacheFilename, token).ConfigureAwait(false);
                    }
                }

                if (response != null)
                {
                    Trace.WriteLine($"Failed to load {textureUri} : {response.StatusCode}");
                }
                return null;
            }
        }
        catch (ArgumentException e)
        {
            Trace.WriteLine($"Failed to load {textureUri}: {e.Message}", "TextureUse");
        }
        catch (WebException e)
        {
            ProcessTextureWebException(e);
        }
        catch (System.Net.Sockets.SocketException e)
        {
            Trace.WriteLine("Socket Exception: " + textureUri + " " + e.Message);
            //this.SetTexture(null);
        }
        catch (System.Net.Http.HttpRequestException e)
        {
            Trace.WriteLine("HttpRequestException: " + textureUri + " " + e.Message);
            //this.SetTexture(null);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return null;
    }

    private async Task<Texture2D> TryLoadingFromHttpClientResponse(HttpResponseMessage response, string CacheFilename, CancellationToken token)
    {
        try
        {
            if (response.IsSuccessStatusCode == false)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    this.TextureNotFound = true;

                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return null;


            using MemoryStream memStream = new(data, false);
            var tex = await GetTextureFromStreamAsync(graphicsDevice, memStream).ConfigureAwait(false);


            if (CacheFilename != null && tex != null)
            {
                memStream.Seek(0, SeekOrigin.Begin);
                try
                {
                    bool cached = await Global.TextureCache.AddAsync(CacheFilename, memStream).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                }
            }

            return tex;
        }
        catch (ArgumentException e)
        {
            var uri = response.RequestMessage?.RequestUri;
            Trace.WriteLine($"Failed to load {(uri != null ? uri.ToString() : "response")}: {e.Message}", "TextureUse");
            return null;
        }
        catch (IOException ex)
        {
            return null;
        }
    }


    private async Task<Texture2D> HandleWebResponse(HttpWebResponse response)
    {
        {
            //Trace.WriteLine("HandleWebResponse on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

            if (response is null)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                this.TextureNotFound = true;
                return null;
            }
            else if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }
            else if (response.ContentLength < 0)
            {
                return null;
            }
            else if (Aborted)
            {
                return null;
            }

            /*
            if (Aborted || IsDisposed)
            {
                if (BodyRequestState != null)
                {
                    //Trace.WriteLine("Ignoring EndGetServerResponse response for: " + this.Filename.ToString());
                    BodyRequestState.Dispose();
                    BodyRequestState = null;
                    return;
                }
            }
            */

            try
            {
                //AsyncState state = this.BodyRequestState; //new AsyncState(this.Filename.ToString());

                //state.response = response;
                //state.databuffer = new byte[response.ContentLength];
                //state.responseStream = response.GetResponseStream(); 

                //BodyRequestState.response = response;
                //Stream stream = response.GetResponseStream();

                //BodyRequestState.databuffer = new byte[response.ContentLength];
                //BodyRequestState.responseStream = response.GetResponseStream();

                //I tried very hard to make async reads of the server response work. Unfortunately it always resulted in an access violation.  Data is now read synchronously.

                //state.responseStream.BeginRead(state.databuffer, 0, (int)state.ReadRequestSize(), new AsyncCallback(this.EndReadResponseStream), state);

                //Byte[] data = state.databuffer;


                //Byte[] data = new byte[response.ContentLength];
                Texture2D? result = null;
                using (MemoryStream memStream = new())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        if (Aborted)
                            return null;

                        if (stream is null)
                            return null;

                        stream.ReadTimeout = 60000;

                        stream.CopyTo(memStream);
                    }
                    /*

                    int BytesRead = 0;
                    stream.ReadTimeout = 30000; //30 seconds to read a ~4Kx4K tile should be plenty of time.  The default was 300 seconds.
                    while (BytesRead < response.ContentLength)
                    {
                        BytesRead += await stream.ReadAsync(data, BytesRead, (data.Length - BytesRead)).ConfigureAwait(false);
                    }
                    */

                    //state.Dispose();
                    Debug.Assert(graphicsDevice != null);
                    result = await GetTextureFromStreamAsync(graphicsDevice, memStream).ConfigureAwait(false);

                    if (CacheFilename != null && result != null)
                    {
                        memStream.Seek(0, SeekOrigin.Begin);
                        await Global.TextureCache.AddAsync(CacheFilename, memStream);
                    }
                }

                /*if (CacheFilename != null && result != null)
                {
                    using(Stream stream = response.GetResponseStream())
                    {
                        //stream.Seek(0, SeekOrigin.Begin);
                        await Global.TextureCache.AddAsync(CacheFilename, stream);
                    }
                }*/

                //data = null;
                return result;
            }
            catch (WebException e)
            {
                ProcessTextureWebException(e);
            }
            catch (InvalidOperationException e)
            {
                //TODO: There is an interaction with aborting requests where an corrupt version of the image ends up in the cache and continues to be used.  I have to 
                //figure out how to flush that bad image out of the cache if this occurs. Currently the workaround is to never cache images


                //Trace.WriteLine(e.Message, "TextureUse");

            }
            catch (ArgumentException e)
            {
                //Very rare, usually the result of a corrupt file
                Trace.WriteLine("Unanticipated Argument Exception loading texture: " + response.ResponseUri.ToString(), "TextureUse");
                Trace.WriteLine(e.Message, "TextureUse");

                this.TextureNotFound = true;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unanticipated Exception loading texture: " + response.ResponseUri.ToString(), "TextureUse");
                Trace.WriteLine(e.Message, "TextureUse");

                throw;
            }
        }

        return null;
    }


    /// <summary>
    /// Set objects texture to Null, records if the server responds with 404 not found, prints helpful error message
    /// </summary>
    /// <param name="e"></param>
    private void ProcessTextureWebException(WebException e)
    {
        if (e.Status == WebExceptionStatus.RequestCanceled)
        {
            //Trace.WriteLine("Request Cancelled: " + state.request.Address.ToString());
        }
        else
        {
            using HttpWebResponse ErrorResponse = (HttpWebResponse)e.Response;

            if (ErrorResponse != null)
            {

                //If the server doesn't have the tile write this down so we stop asking...
                if (ErrorResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    this.TextureNotFound = true;
                }
                else if (ErrorResponse.StatusCode == HttpStatusCode.InternalServerError)
                {
                    this.TextureNotFound = true;
                    //Trace.WriteLine("WebException: " + state.request.Address.ToString());
                    //Trace.WriteLine(ErrorResponse.StatusCode + " : " + ErrorResponse.StatusDescription, "TextureUse");
                }
            }
        }
    }

    private void TryDeleteFile(string filepath)
    {
        try
        {
            System.IO.File.Delete(filepath);
        }
        catch (System.IO.IOException e)
        {
            Trace.WriteLine("Could not delete file: " + Filename);
            Trace.WriteLine(e.Message);
        }
    }

    private async Task<byte[]> StreamToBytesAsync(Stream stream, CancellationToken token)
    {
        byte[] data = new byte[stream.Length];
        int bytesRead = 0;
        while (bytesRead < stream.Length)
        {
            bytesRead += await stream.ReadAsync(data, bytesRead, (int)(stream.Length - bytesRead), token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return null;
            //Trace.WriteLineIf(bytesRead < stream.Length, "Not all bytes read on first try when loading filestream: " + this.CacheFilename);
        }

        return data;
    }

    /// <summary>
    /// Only allow loading a single texture at a time
    /// </summary>
    readonly SemaphoreSlim LoadTextureSemaphore = new(1, 1);

    /// <summary>
    /// Set the max concurrent texture load workers to a direct limit (1-256).
    /// Delegates to TextureRequestQueue.
    /// </summary>
    public static void SetMaxConcurrentRequestLimit(int max)
    {
        TextureRequestQueue.SetMaxWorkers(max);
    }
    /// <param name="allowNetwork">
    /// When false, only attempt a disk cache read (no HTTP). Used by the disk fast-path so network work can take MaxWorkers separately.
    /// </param>
    public async Task<Texture2D> LoadTexture(bool allowNetwork = true)
    {
        CancellationToken token = this.CancelToken.Token;
        if (token.IsCancellationRequested)
            return null;
        if (_sectionToken?.IsCancellationRequested == true)
            return null;

        CancellationToken semaphoreToken = _sectionToken ?? token;


        try
        {
            await LoadTextureSemaphore.WaitAsync(semaphoreToken).ConfigureAwait(false);

            //Trace.WriteLine("ThreadPoolCallback for " + ID.ToString() + " " + this.Filename.ToString());
            /*Nothing to do if we were aborted already*/
            if (Aborted || IsDisposed)
            {
                //Trace.WriteLine("Ignoring threadcallback for: " + this.Filename.ToString());
                return null;
            }

            if (Filename.Scheme.ToLower() == "http" || Filename.Scheme.ToLower() == "https")
            {
                Texture2D texture = null;
                try
                {
                    texture = await TryLoadingFromDiskOnly(CacheFilename, token).ConfigureAwait(false);
                    if (texture is null && allowNetwork)
                        texture = await TryLoadingFromServer(this.Filename, token).ConfigureAwait(false);
                }
                catch (OutOfMemoryException e)
                {
                    Trace.WriteLine("Out of memory exception: " + CacheFilename);
                }
                catch (ArgumentException)
                {
                    Trace.WriteLine("Problem loading cached tile, deleting and loading from server: " +
                                    CacheFilename);
                    TryDeleteFile(CacheFilename);
                    texture = await TryLoadingFromServer(this.Filename, token);
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    return null;
                }
                catch (System.OperationCanceledException)
                {
                    return null;
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Problem loading cached tile {CacheFilename}, deleting and loading from server.\n{e}");
                    TryDeleteFile(CacheFilename);
                    texture = await TryLoadingFromServer(this.Filename, token);
                }

                SetTexture(texture);
                return this._Result;
            }
            else
            {
                {
                    if (Aborted)
                        return null;

                    try
                    {
                        using (FileStream stream = System.IO.File.OpenRead(Filename.ToString()))
                        {
                            if (stream != null)
                            {
                                //byte[] data = await StreamToBytesAsync(stream).ConfigureAwait(false);
                                var texture = await GetTextureFromStreamAsync(graphicsDevice, stream);
                                if (token.IsCancellationRequested)
                                    return null;

                                SetTexture(texture);

                                if (texture != null)
                                    Global.AddTexture(texture, Filename.ToString());
                                //tex = Texture2D.FromStream(graphicsDevice, stream);
                                //lock (this)
                                //{
                                //tex = TextureFromStream(graphicsDevice, stream, this.MipMapLevels > 0);
                                //}
                            }
                        }

                        return _Result;
                    }
                    catch (IOException e)
                    {
                        //Print out the first error, but don't flood the output in case we simply have a section where we are
                        //missing some tiles. 
                        if (!TextureErrorReported)
                        {
                            //Trace.WriteLine("Error loading texture: " + e.ToString(), "TextureUse");
                            TextureErrorReported = true;
                        }
                    }
                    catch (Exception e)
                    {
                        //Print out the first error, but don't flood the output in case we simply have a section where we are
                        //missing some tiles. 
                        if (!TextureErrorReported)
                        {
                            //Trace.WriteLine("Error loading texture: " + e.ToString(), "TextureUse");
                            TextureErrorReported = true;
                        }

                        throw;
                    }
                }

            }
        }
        finally
        {
            LoadTextureSemaphore.Release();
        }

        return null;

    }

    protected void SetTexture(Texture2D tex)
    {
        try
        {
            rwResultLock.EnterWriteLock();
            //Trace.WriteLine("SetTexture: " + this.Filename.ToString()); 

            if (IsDisposed && tex != null)
            {
                tex.Dispose();
                Global.RemoveTexture(tex);
                tex = null;
            }

            /*
            if (BodyRequestState != null)
            {
                this.BodyRequestState.Dispose();
                this.BodyRequestState = null;
            }
            */

            this._Result = tex;
            this.FinishedReading = true;
            graphicsDevice = null;

            if (!IsDisposed)
                DoneEvent.Set();
        }
        finally
        {
            rwResultLock.ExitWriteLock();
        }

        System.Threading.Tasks.Task.Run(() => OnCompletionCallback?.Invoke());
    }

    public override bool Equals(object obj)
    {
        if (obj is not TextureReaderV2 Tobj)
            return false;

        return Tobj.Filename == this.Filename;
    }

    public override int GetHashCode() => Filename.GetHashCode();

    public static bool operator ==(TextureReaderV2 left, TextureReaderV2 right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(TextureReaderV2 left, TextureReaderV2 right) => !(left == right);

    public bool UseMipMaps => this.MipMapLevels > 0;

    /*
    protected Texture2D TextureFromStream(GraphicsDevice device, Byte[] streamdata, bool UseMipMaps)
    {
        TextureData data = TextureReaderV2.TextureDataFromStream(streamdata);
        if (data is null)
        { 
            return null;
        }

        Texture2D tex = TextureFromData(graphicsDevice, data, UseMipMaps); 
        return tex;
    }
    */

    protected Task<Texture2D> GetTextureFromBytesAsync(GraphicsDevice device, byte[] streamdata)
    {
        Debug.Assert(device != null);
        //Trace.WriteLine("TextureFromStreamAsync: " + this.Filename.ToString()); 

        using MemoryStream stream = new(streamdata);
        return GetTextureFromTextureDataAsync(device, TextureDataFromStream(stream));
    }


    protected async Task<Texture2D> GetTextureFromStreamAsync(GraphicsDevice device, Stream streamdata)
    {
        Debug.Assert(device != null);
        //Trace.WriteLine("TextureFromStreamAsync: " + this.Filename.ToString()); 
        if (this.Aborted || this.IsDisposed)
            return null;

        TextureData data = TextureReaderV2.TextureDataFromStream(streamdata);
        if (data.IsEmpty)
        {
            return null;
        }

        return await GetTextureFromTextureDataAsync(device, data);
    }


    protected async Task<Texture2D> GetTextureFromTextureDataAsync(GraphicsDevice device, TextureData data)
    {
        if(SectionLoadCancelled)
            return null;

        var tcs = new TaskCompletionSource<Texture2D>();
        PendingTextureQueue.Enqueue(data, UseMipMaps, tcs, tileView: TileViewOwner, fileKey: Filename?.ToString()); 
        return await tcs.Task.ConfigureAwait(false);
    }


    public static TextureData TextureDataFromStream(Stream stream)
    {
        //We load greyscale images, XNA doesn't support loading greyscale by default, so run it through Bitmap instead
        int Width;
        int Height;
        BitmapData? data = null;
        Byte[] rgbValues;
        int PixelSize;

        //Trace.WriteLine("TextureFromStream on thread ID: " + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());

        try
        {
            using (Bitmap image = new(stream))
            {
                Width = image.Width;
                Height = image.Height;

                System.Drawing.Rectangle rect = new(0, 0, image.Width, image.Height);
                try
                {
                    data = image.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, image.PixelFormat);

                    PixelSize = data.Stride / data.Width;
                    IntPtr ptr = data.Scan0;
                    int TotalBytes = data.Stride * data.Height;
                    rgbValues = new Byte[TotalBytes];
                    if (data.Stride % data.Width == 0)
                    {
                        //The easy case...
                        System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, TotalBytes);
                    }
                    else
                    {
                        //Copy one line at a time so the empty bytes at the end of each line don't show up in our array
                        byte[] lineValues = new byte[data.Width * PixelSize];
                        for (int iY = 0; iY < data.Height; iY++)
                        {
                            //                            int yDataOffset = iY * data.Width;

                            //The documentation and tooltips for the Marshal.Copy function are just wrong...
                            System.Runtime.InteropServices.Marshal.Copy(ptr + (iY * data.Stride), lineValues, 0,
                                data.Width * PixelSize);

                            Array.Copy(lineValues, 0, rgbValues, iY * (data.Width * PixelSize),
                                data.Width * PixelSize);
                        }
                    }

                    //Grrr... have to remap every pixel using the palette.
                    if (image.Palette.Entries.Length > 0)
                    {
                        for (int i = 0; i < rgbValues.Length; i++)
                        {
                            rgbValues[i] = image.Palette.Entries[rgbValues[i]].R;
                        }
                    }
                }
                finally
                {
                    image.UnlockBits(data);
                    data = null;
                }
            }

            if (rgbValues != null)
            {
                int WidthHeight = Width * Height;
                Byte[] pixelBytes = new Byte[WidthHeight];

                for (int iSourceByte = 0, iDestByte = 0; iDestByte < WidthHeight; iSourceByte += PixelSize)
                {
                    pixelBytes[iDestByte++] = rgbValues[iSourceByte];
                }

                return new TextureData(pixelBytes, Width, Height);
            }
        }
        catch (System.OutOfMemoryException e)
        {
            Trace.WriteLine("Out of memory when allocating texture");
            return default;
        }

        return default;
    }

    // Prefer GetTextureFromTextureDataAsync (via TextureReaderV2) which routes through PendingTextureQueue.
    /*
    public static Texture2D TextureFromStream(GraphicsDevice graphicsDevice, Stream texStream, bool mipmap)
    {
        TextureData texData = TextureDataFromStream(texStream);
        return TextureFromData(graphicsDevice, texData, mipmap);
    }*/

    /// <summary>
    /// Creates a Texture2D from decoded greyscale bytes. Always uses SurfaceFormat.Color with
    /// luminance in the alpha channel so TileLayoutToGreyscaleEffect (reads .a) matches both
    /// Reach and newer MonoGame DirectX, where Alpha8 does not sample into .a the same way as XNA 3.7.
    /// Called from PendingTextureQueue on the device thread.
    /// </summary>
    public static Texture2D? TextureFromData(GraphicsDevice graphicsDevice, in TextureData texdata, bool mipmap)
    {
        if (graphicsDevice is null)
            return null;

        if (graphicsDevice.IsDisposed)
            return null;

        if (texdata.pixelBytes is null)
            return null;

        Texture2D? tex = null;
        try
        {
            Debug.Assert(texdata.width * texdata.height == texdata.pixelBytes.Length);
            tex = new Texture2D(graphicsDevice, texdata.width, texdata.height, mipmap, SurfaceFormat.Color);
            tex.SetData<int>(Array.ConvertAll<byte, int>(texdata.pixelBytes, x => (int)x << 24));
        }
        catch (Exception e)
        {
            tex?.Dispose();
            tex = null;
            throw;
        }

        return tex;

    }


    #region IDisposable Members

    protected virtual void Dispose(bool disposing)
    {
        try
        {
            rwResultLock.EnterWriteLock();

            if (IsDisposed)
                return;

            //Trace.WriteLine("Dispose TextureReader: " + this.Filename.ToString());
            IsDisposed = true;

            //Debug.Assert(_Result is null);
            _Result?.Dispose();
            _Result = null;
            DoneEvent?.Close();
            DoneEvent = null;
        }
        finally
        {
            rwResultLock.ExitWriteLock();
        }
#if DEBUG
        Global.RemoveTextureReader(this);
#endif
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
