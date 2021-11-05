using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Viking
{

    class TextureReaderV2 : IDisposable
    {
        readonly Uri Filename;
        readonly string CacheFilename;
        GraphicsDevice graphicsDevice = null;
        private Texture2D _Result = null;
        private ManualResetEvent DoneEvent = new ManualResetEvent(false);
        public bool FinishedReading = false;
        //        public RefreshDelegate RefreshMethod; 

        public static int nextid = 0;

        private Action OnCompletionCallback;

        public int ID { get; private set; }

        //AsyncState BodyRequestState = null;

        private bool IsDisposed = false;

        static System.Net.Cache.RequestCachePolicy HeaderCachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
        static System.Net.Cache.RequestCachePolicy BodyCachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

        private bool _TextureNotFound = false;

        /// <summary>
        /// TextureNotFound is set to true when we successfully communicated with the server and it did not have the requested texture
        /// </summary>
        public bool TextureNotFound
        {
            get
            {
                return _TextureNotFound;
            }
            protected set
            {
                _TextureNotFound = value;
            }
        }

        static private bool TextureErrorReported = false;

        private readonly int MipMapLevels = 1;

        /// <summary>
        /// Returns true if a call to GetTexture will return a non-null value
        /// </summary>
        public bool HasTexture
        {
            get { return _Result != null; }
        }

        /// <summary>
        /// Set to true when the reader has been aborted
        /// </summary>
        protected bool Aborted { get { return CancelToken != null ? CancelToken.IsCancellationRequested : false; } }

        //private Object thisLock = new Object();

        private readonly ReaderWriterLockSlim rwResultLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Returns the result.  This method can only be called once.
        /// By taking the texture you are responsible for calling dispose.
        /// </summary>
        /// <returns></returns>
        public Texture2D GetTexture()
        {
            Texture2D retVal;
            try
            {
                rwResultLock.EnterUpgradeableReadLock();
                if (_Result == null)
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

        private readonly CancellationToken CancelToken;

        public TextureReaderV2(GraphicsDevice graphicsDevice, Uri textureUri, string cacheFilename, int mipMapLevels, Action OnCompletion, CancellationToken token)
            : this(graphicsDevice, textureUri, mipMapLevels, OnCompletion, token)
        {
            CacheFilename = cacheFilename;
        }

        /// <summary>
        /// This texture reader is used when we don't have a cachepath to check before making the request
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="filename"></param>
        /// <param name="downsample"></param>
        public TextureReaderV2(GraphicsDevice graphicsDevice, Uri textureURI, int mipMapLevels, Action OnCompletion, CancellationToken token)
        {
            CancelToken = token;
            this.OnCompletionCallback = OnCompletion;
            this.ID = TextureReaderV2.nextid++;
            if (graphicsDevice == null)
                throw new ArgumentException("TextureReader: Graphics device cannot be null");

            this.graphicsDevice = graphicsDevice;
            this.Filename = textureURI;
            this.MipMapLevels = mipMapLevels;

            //Trace.WriteLine("Create TextureReader for " + textureURI.ToString());
#if DEBUG
            Viking.Global.AddTextureReader(this, this.Filename.ToString());
#endif
        }

        public override string ToString()
        {
            return "TR: " + this.ID.ToString();
        }

        private static HttpWebRequest CreateBasicRequest(Uri textureURI)
        {
            HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(textureURI);
            if (textureURI.Scheme.ToLower() == "https")
                request.Credentials = UI.State.UserCredentials;

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            request.Timeout = 60001;

            return request;
        }

        public void AbortRequest()
        {

            {
                /*
                //Abort the request if we haven't already
                if (BodyRequestState != null)
                {
                    if (BodyRequestState.request != null)
                    {
                        try
                        {
                            BodyRequestState.request.Abort();
                            BodyRequestState = null;
                        }
                        catch (WebException)
                        {
                            //Trace.WriteLine(e.Message, "TextureReader.Dispose");
                        }
                    }
                }
                */

                //In case we have finished loading the texture, but the texture has not been assigned to the tile, 
                //dispose of the texture
                if (_Result != null)
                {
                    if (_Result.IsDisposed == false)
                    {
                        DisposeTextureThreadingObj.DisposeTexture(_Result);
                        //DisposeTextureThreadingObj disposeObj = new DisposeTextureThreadingObj(_Result);
                        //ThreadPool.QueueUserWorkItem(disposeObj.ThreadPoolCallback);
                    }

                    _Result = null;
                }
            }
        }

        /// <summary>
        /// Validates the provide file against the last modified date of the web resource
        /// </summary>
        /// <param name="CacheFilename"></param>
        /// <param name="textureUri"></param>
        /// <returns></returns>
        private static async Task<bool> CachedResourceIsValidAsync(string CacheFilename, Uri textureUri)
        {
            if (textureUri == null)
                return true;

            System.IO.FileInfo fileinfo = new FileInfo(CacheFilename);

            if (!fileinfo.Exists)
                return false;

            if (fileinfo.Length == 0)
                return false;

            HttpWebRequest headerRequest = TextureReaderV2.CreateBasicRequest(textureUri);
            headerRequest.Method = "HEAD";
            headerRequest.CachePolicy = TextureReaderV2.HeaderCachePolicy;
            try
            {
                using (HttpWebResponse headerResponse =
                    await headerRequest.GetResponseAsync().ConfigureAwait(false) as HttpWebResponse)
                {
                    if (headerResponse != null)
                    {
                        return headerResponse.LastModified.ToUniversalTime() <=
                                     fileinfo.LastWriteTimeUtc;
                    }
                    
                    return false;
                }
            }
            catch (WebException e)
            {
                //No online resource found, use the cached version
                Trace.WriteLine($"No online resource found at {textureUri}, using cache version {CacheFilename}.\nWeb Exception: {e.Status} - {e.Message}");
                return true;
            }
        }

        /// <summary>
        /// Validates the provide file against the last modified date of the web resource
        /// </summary>
        /// <param name="CacheFilename"></param>
        /// <param name="textureUri"></param>
        /// <returns></returns>
        private static bool CachedResourceIsValid(string CacheFilename, Uri textureUri)
        {
            return CachedResourceIsValidAsync(CacheFilename, textureUri).Result;
        }

        private static void HandleCachedFileException(Exception e, string CacheFilename)
        {
            //Trace.WriteLine(e.Message, "TextureUse");
            DeleteFileFromCache(CacheFilename);
        }

        private static void DeleteFileFromCache(string CacheFilename)
        { 
            try
            {
                System.IO.File.Delete(CacheFilename);
            }
            catch (System.IO.FileNotFoundException)
            {
                Trace.WriteLine($"Failed To delete non-existent cache file (probably OK): {CacheFilename}", "TextureUse");
            }
            catch (System.IO.IOException e)
            {
                Trace.WriteLine("Failed To delete bad cache file: {CacheFilename}\n{e}", "TextureUse");
            }
        }

        internal static async Task<FileStream> TryLoadingFromCache(Uri textureUri, string CacheFilename)
        {
            System.IO.FileStream TileStream = null;
            try
            {
                //First, check the cache to see if it is locally available
                if (CacheFilename != null)
                {
                    if (false == await CachedResourceIsValidAsync(CacheFilename, textureUri).ConfigureAwait(false))
                    {
                        //Trace.WriteLine("Deleting stale cache file: " + CacheFilename, "TextureUse");
                        await Task.Run(() => DeleteFileFromCache(CacheFilename)).ConfigureAwait(false);
                        return null;
                    }
                    else
                    {
                        return Global.TextureCache.Fetch(CacheFilename);
                    }
                }
            }
            catch (ArgumentException e)
            {
                //This exception seems to occur when a cached image file is not valid

                //TODO: There is an interaction with aborting requests where an corrupt version of the image ends up in the cache and continues to be used.  I have to 
                //figure out how to flush that bad image out of the cache if this occurs. Currently the workaround is to never cache images
                HandleCachedFileException(e, CacheFilename);
                return null;
            }
            catch (InvalidOperationException e)
            {
                //This exception seems to occur when a cached image file is not valid

                //TODO: There is an interaction with aborting requests where an corrupt version of the image ends up in the cache and continues to be used.  I have to 
                //figure out how to flush that bad image out of the cache if this occurs. Currently the workaround is to never cache images
                HandleCachedFileException(e, CacheFilename);
                return null;
            }
            catch (Exception e)
            {
                HandleCachedFileException(e, CacheFilename);
                throw;
            }

            return TileStream;
        }

        private async Task<Texture2D> TryLoadingFromServer(Uri textureUri)
        {
            {
                if (Aborted || IsDisposed)
                    return null;

                //Trace.WriteLine("Checking server: " + textureUri.ToString() + " thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

                HttpWebRequest bodyRequest = TextureReaderV2.CreateBasicRequest(textureUri);
                bodyRequest = CreateBasicRequest(textureUri);
                bodyRequest.CachePolicy = TextureReaderV2.BodyCachePolicy;

                //this.BodyRequestState = new AsyncState(textureUri.ToString());

                //For some reason loading from the server using ASync requests often results in an Access Violation.  I've resorted to syncronous requests.

                //BodyRequestState.request = bodyRequest;
                //BodyRequestState.request.BeginGetResponse(new AsyncCallback(EndGetServerResponse), BodyRequestState);

                try
                {
                    HttpWebResponse response = await bodyRequest.GetResponseAsync().ConfigureAwait(false) as HttpWebResponse;
                    return await HandleWebResponse(response).ConfigureAwait(false);
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

                return null;
            }
        }


        private async Task<Texture2D> HandleWebResponse(HttpWebResponse response)
        {
            {
                //Trace.WriteLine("HandleWebResponse on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

                if (response == null)
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
                    Texture2D result = null;
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (Stream stream = response.GetResponseStream())
                        {
                            if (Aborted)
                                return null;

                            stream.ReadTimeout = 60000;

                            await stream.CopyToAsync(memStream);
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
                using (HttpWebResponse ErrorResponse = (HttpWebResponse)e.Response)
                {

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
        }

        private void TryDeleteFile(string filepath)
        {
            try
            {
                if (System.IO.File.Exists(filepath))
                {
                    System.IO.File.Delete(filepath);
                }
            }
            catch (System.IO.IOException e)
            {
                Trace.WriteLine("Could not delete file: " + Filename);
                Trace.WriteLine(e.Message);
            }
        }

        private async Task<byte[]> StreamToBytesAsync(Stream stream)
        {
            byte[] data = new byte[stream.Length];
            int bytesRead = 0;
            while (bytesRead < stream.Length)
            {
                bytesRead += await stream.ReadAsync(data, bytesRead, (int)(stream.Length - bytesRead), CancelToken).ConfigureAwait(false);

                if (CancelToken.IsCancellationRequested)
                    return null;
                //Trace.WriteLineIf(bytesRead < stream.Length, "Not all bytes read on first try when loading filestream: " + this.CacheFilename);
            }

            return data;
        }

        /// <summary>
        /// Only allow loading a single texture at a time
        /// </summary>
        SemaphoreSlim LoadTextureSemaphore = new SemaphoreSlim(1, 1);
        public async Task<Texture2D> LoadTexture()
        {
            try
            {
                await LoadTextureSemaphore.WaitAsync(CancelToken).ConfigureAwait(false);

                //Trace.WriteLine("ThreadPoolCallback for " + ID.ToString() + " " + this.Filename.ToString());
                /*Nothing to do if we were aborted already*/
                if (Aborted || IsDisposed)
                {
                    //Trace.WriteLine("Ignoring threadcallback for: " + this.Filename.ToString());
                    return null;
                }

                Texture2D tex = null;

                if (Filename.Scheme.ToLower() == "http" || Filename.Scheme.ToLower() == "https")
                {
                    {
                        using (FileStream texStream = await TextureReaderV2
                            .TryLoadingFromCache(this.Filename, CacheFilename).ConfigureAwait(false))
                        {
                            if (texStream != null && texStream.Length > 0)
                            {
                                //Trace.WriteLine("Found in cache: " + this.Filename.ToString());
                                try
                                {
                                    //TextureFromStream(texStream, this.MipMapLevels > 0);
                                    /*
                                    byte[] data = await StreamToBytesAsync(texStream).ConfigureAwait(false); 
                                    if (CancelToken.IsCancellationRequested)
                                        return null;
                                    */
                                    var texture = await GetTextureFromStreamAsync(graphicsDevice, texStream)
                                        .ConfigureAwait(false);
                                    if (CancelToken.IsCancellationRequested)
                                        return null;

                                    SetTexture(texture);

                                    return this._Result;
                                }
                                catch (OutOfMemoryException e)
                                {
                                    Trace.WriteLine("Out of memory exception: " + CacheFilename);
                                }
                                catch (ArgumentException e)
                                {
                                    Trace.WriteLine("Problem loading cached tile, deleting and loading from server: " +
                                                    CacheFilename);
                                    TryDeleteFile(CacheFilename);
                                    //Continue and try to load from server
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine($"Problem loading cached tile {CacheFilename}, deleting and loading from server.\n{e}");
                                    TryDeleteFile(CacheFilename);
                                    //Continue and try to load from server
                                }
                            }
                        }

                        //If we didn't already load the tilestream from the cache 
                        var server_texture = await TryLoadingFromServer(this.Filename).ConfigureAwait(false);

                        if (Aborted)
                            return null;

                        SetTexture(server_texture);
                        return _Result;
                    }
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
                                    var texture = await GetTextureFromStreamAsync(graphicsDevice, stream).ConfigureAwait(false);
                                    if (CancelToken.IsCancellationRequested)
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
             
            System.Threading.Tasks.Task.Run(() =>
            {
                OnCompletionCallback?.Invoke();
            });
        }

        public override bool Equals(object obj)
        {
            TextureReaderV2 Tobj = obj as TextureReaderV2;
            if (Tobj == null)
                return false;

            if (Tobj.Filename != this.Filename)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Filename.GetHashCode();
        }

        public bool UseMipMaps
        {
            get { return this.MipMapLevels > 0; }
        }

        /*
        protected Texture2D TextureFromStream(GraphicsDevice device, Byte[] streamdata, bool UseMipMaps)
        {
            TextureData data = TextureReaderV2.TextureDataFromStream(streamdata);
            if (data == null)
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

            using (MemoryStream stream = new MemoryStream(streamdata))
            {
                return GetTextureFromTextureDataAsync(device, TextureDataFromStream(stream));
            }
        }


        protected Task<Texture2D> GetTextureFromStreamAsync(GraphicsDevice device, Stream streamdata)
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

            return GetTextureFromTextureDataAsync(device, data);
        }


        protected async Task<Texture2D> GetTextureFromTextureDataAsync(GraphicsDevice device, TextureData data)
        {
            Func<Texture2D> a = new Func<Texture2D>(() =>
            {
                try
                {
                    return TextureReaderV2.TextureFromData(device, in data, this.UseMipMaps);
                    //this.SetTexture(texture);
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Exception loading texture: {this.Filename.ToString()}");
                    throw;
                }
            });

            //Ensure we create the texture on the main thread
            if (Viking.UI.State.Appwindow.InvokeRequired)
            {
                return await Viking.UI.State.MainThreadDispatcher.InvokeAsync(a);
            }
            else
            {
                return a();
            }

            //return _Result;
        }


        public static TextureData TextureDataFromStream(Stream stream)
        {
            //We load greyscale images, XNA doesn't support loading greyscale by default, so run it through Bitmap instead
            int Width;
            int Height;
            BitmapData data = null;
            Byte[] rgbValues;
            int PixelSize;

            //Trace.WriteLine("TextureFromStream on thread ID: " + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());

            try
            {
                using (Bitmap image = new System.Drawing.Bitmap(stream))
                {
                    Width = image.Width;
                    Height = image.Height;

                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
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

        public static Texture2D TextureFromStream(GraphicsDevice graphicsDevice, Stream texStream, bool mipmap)
        {
            TextureData texData = TextureDataFromStream(texStream);
            return TextureFromData(graphicsDevice, texData, mipmap);
        }

        public static Texture2D TextureFromData(GraphicsDevice graphicsDevice, in TextureData texdata, bool mipmap)
        {
            if (graphicsDevice == null)
                return null;

            if (graphicsDevice.IsDisposed)
                return null;

            if (texdata.pixelBytes == null)
                return null;

            //Trace.WriteLine("TextureFromData: " + this.Filename.ToString()); 
            Texture2D tex = null;
            try
            {
                Debug.Assert(texdata.width * texdata.height == texdata.pixelBytes.Length);
                if (graphicsDevice.GraphicsProfile == GraphicsProfile.Reach)
                {
                    tex = new Texture2D(graphicsDevice, texdata.width, texdata.height, mipmap, SurfaceFormat.Color);
                    tex.SetData<int>(Array.ConvertAll<byte, int>(texdata.pixelBytes, new Converter<byte, int>((x) => (int)x << 24)));
                }
                else
                {
                    tex = new Texture2D(graphicsDevice, texdata.width, texdata.height, mipmap, SurfaceFormat.Alpha8);
                    tex.SetData<Byte>(texdata.pixelBytes);
                }
            }
            catch (Exception e)
            {
                if (tex != null)
                {
                    tex.Dispose();
                    tex = null;
                }
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

                //Debug.Assert(_Result == null);
                if (_Result != null)
                {
                    _Result.Dispose();
                    _Result = null;
                }

                if (DoneEvent != null)
                {
                    DoneEvent.Close();
                    DoneEvent = null;
                }
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
}
