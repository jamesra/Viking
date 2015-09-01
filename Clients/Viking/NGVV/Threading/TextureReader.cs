using System;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Net;
using Viking.Common;
using System.Drawing.Imaging;

namespace Viking
{
    internal class AsyncState : IDisposable
    {
        const int BUFFER_SIZE = 65536;
        public HttpWebRequest request = null;
        public HttpWebResponse response = null;
        public Byte[] databuffer = null;
        public Stream responseStream = null; 
        public int TotalRead 
        {
            get { return this._TotalRead;}
            set { Debug.Assert(value >= this._TotalRead); _TotalRead = value; }
        }

        private int _TotalRead = 0;

        public string TextureURI = null; 

        public int ID = 0;
        private static int NextID = 0; 

        public AsyncState(string uri)
        {
            this.TextureURI = uri;

            this.ID = AsyncState.NextID++;

            //Trace.WriteLine("Creating AsyncState #" + this.ID.ToString() + ": " + this.TextureURI); 
        }

        public override string ToString()
        {
            return TextureURI;
        }
        
        public int BytesRemaining
        {
            get
            {
                return (int)(response.ContentLength - TotalRead);
            }
        }

        public int ReadRequestSize()
        { 
            return BUFFER_SIZE < BytesRemaining ? BUFFER_SIZE : BytesRemaining;
        } 

        public virtual void Dispose(bool disposing)
        {
            if(disposing)
            {
                //Trace.WriteLine("Disposing AsyncState #" + this.ID.ToString() + ": " + this.TextureURI);   
                if (request != null)
                {
                    this.request.Abort();
                    this.request = null;
                }

                if (responseStream != null)
                {
                    responseStream.Close();
                    responseStream = null;
                }

                if (response != null)
                {
                    response.Close();
                    response = null;
                }

                databuffer = null;  
            }
        }
         
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); 
        }
    }

     

    /// <summary>
    /// Holder for texture data created on threads so that textures are created only on the main thread
    /// XNA's behavior for multi-threaded GPU use is ambiguous
    /// </summary>
    internal class TextureData
    {
        public Byte[] pixelBytes { get; private set; }
        public int width { get; private set; }
        public int height { get; private set; }

        public TextureData(Byte[] data, int width, int height)
        {
            this.pixelBytes = data;
            this.width = width;
            this.height = height; 
        }
    }
   
    class TextureReader : IDisposable 
    {
        readonly Uri Filename;
        readonly string CacheFilename;
        GraphicsDevice graphicsDevice = null;
        private Texture2D _Result = null;
        public ManualResetEvent DoneEvent = new ManualResetEvent(false);
        public bool FinishedReading = false;
//        public RefreshDelegate RefreshMethod; 

        public static int nextid = 0; 

        public int ID { get; private set;}

        AsyncState BodyRequestState = null;

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


        private int MipMapLevels = 1; 

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
        protected bool Aborted = false; 


        /// <summary>
        /// Returns the result.  This method can only be called once.
        /// By taking the texture you are responsible for calling dispose.
        /// </summary>
        /// <returns></returns>
        public Texture2D GetTexture()
        {
            Texture2D retVal;
            lock (this)
            {
                retVal = _Result;

                _Result = null;
            }

            return retVal; 
        }

        public TextureReader(GraphicsDevice graphicsDevice, Uri textureUri, string cacheFilename, int mipMapLevels, RefreshDelegate refreshMethod)
            : this(graphicsDevice, textureUri, cacheFilename, mipMapLevels)
        {
            //RefreshMethod = refreshMethod; 
        }

        public TextureReader(GraphicsDevice graphicsDevice, Uri textureUri, string cacheFilename, int mipMapLevels)
            : this(graphicsDevice, textureUri, mipMapLevels)
        {
           
            CacheFilename = cacheFilename; 
        }

        /// <summary>
        /// This texture reader is used when we don't have a cachepath to check before making the request
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="filename"></param>
        /// <param name="downsample"></param>
        public TextureReader(GraphicsDevice graphicsDevice, Uri textureURI, int mipMapLevels)
        {
            this.ID = TextureReader.nextid++; 
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
            lock (this)
            {
                Aborted = true;

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

                //In case we have finished loading the texture, but the texture has not been assigned to the tile, 
                //dispose of the texture
                if (_Result != null)
                {
                    if (_Result.IsDisposed == false)
                    {
                        DisposeTextureThreadingObj disposeObj = new DisposeTextureThreadingObj(_Result);
                        ThreadPool.QueueUserWorkItem(disposeObj.ThreadPoolCallback);
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
        private static bool CachedResourceIsValid(string CacheFilename, Uri textureUri)
        {
            if(textureUri == null)
                return true;

            if(!System.IO.File.Exists(CacheFilename))
                return false;
             
            HttpWebRequest headerRequest = TextureReader.CreateBasicRequest(textureUri);
            headerRequest.Method = "HEAD";
            headerRequest.CachePolicy = TextureReader.HeaderCachePolicy;
            using (HttpWebResponse headerResponse = headerRequest.GetResponse() as HttpWebResponse)
            {
                bool valid = headerResponse.LastModified.ToUniversalTime() <= System.IO.File.GetLastWriteTimeUtc(CacheFilename);
                return valid;
            }
        }

        private static void HandleCachedFileException(Exception e, string CacheFilename)
        {
            //Trace.WriteLine(e.Message, "TextureUse");
            DeleteFileFromCache(CacheFilename);
        }

        private static void DeleteFileFromCache(string CacheFilename)
        { 
            //Trace.WriteLine("Deleting bad cache file: " + CacheFilename, "TextureUse");
            if (!System.IO.File.Exists(CacheFilename))
                return;

            try
            {
                System.IO.File.Delete(CacheFilename); 
            }
            catch (System.IO.IOException)
            {
                Trace.WriteLine("Failed To delete bad cache file: " + CacheFilename, "TextureUse");
            }
        }


        internal static Stream TryLoadingFromCache( Uri textureUri, string CacheFilename)
        { 
            System.IO.Stream TileStream = null; 
            try
            {
                //First, check the cache to see if it is locally available
                if (CacheFilename != null)
                {
                    if (!CachedResourceIsValid(CacheFilename, textureUri))
                    {
                        //Trace.WriteLine("Deleting stale cache file: " + CacheFilename, "TextureUse");
                        DeleteFileFromCache(CacheFilename);
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
                return null;
            }

            return TileStream; 
        }

        private void TryLoadingFromServer(Uri textureUri)
        {
            lock (this)
            {
                if (Aborted || IsDisposed)
                    return;

                //Trace.WriteLine("Checking server: " + textureUri.ToString() + " thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

                HttpWebRequest bodyRequest = TextureReader.CreateBasicRequest(textureUri);
                bodyRequest = CreateBasicRequest(textureUri);
                bodyRequest.CachePolicy = TextureReader.BodyCachePolicy;
                
                this.BodyRequestState = new AsyncState(textureUri.ToString());

                //For some reason loading from the server using ASync requests often results in an Access Violation.  I've resorted to syncronous requests.

                BodyRequestState.request = bodyRequest;
                //BodyRequestState.request.BeginGetResponse(new AsyncCallback(EndGetServerResponse), BodyRequestState);
                
                
                try
                {
                    HttpWebResponse response = bodyRequest.GetResponse() as HttpWebResponse;
                    HandleWebResponse(response);
                }
                catch (WebException e)
                {
                    ProcessTextureWebException(e, BodyRequestState);
                }
            }
        }

         

        private void EndGetServerResponse(IAsyncResult asyncResult)
        {
            lock(this)
            {
                if(Aborted || IsDisposed)
                    return;

                AsyncState requestState = (AsyncState)asyncResult.AsyncState;
                try
                {
                    
                    // Set the State of request to asynchronous.
                   
                    HttpWebRequest bodyRequest = (HttpWebRequest)requestState.request;
                    // End the Asynchronous response.

                    requestState.response = requestState.request.EndGetResponse(asyncResult) as HttpWebResponse;
                    HandleWebResponse(requestState.response); 
                }
                catch (WebException e)
                {
                    ProcessTextureWebException(e, requestState);
                }
                catch (InvalidOperationException e)
                {
                    //TODO: There is an interaction with aborting requests where an corrupt version of the image ends up in the cache and continues to be used.  I have to 
                    //figure out how to flush that bad image out of the cache if this occurs. Currently the workaround is to never cache images


                    //Trace.WriteLine(e.Message, "TextureUse");

                    this.SetTexture(null);
                }
                catch (Exception e)
                {
                    //Trace.WriteLine("Unanticipated exception loading texture: " + requestState.request.RequestUri.ToString(), "TextureUse");
                    //Trace.WriteLine(e.Message, "TextureUse");

                    this.SetTexture(null);
                }
            } 
        }


        private void HandleWebResponse(HttpWebResponse response)
        {
            lock (this)
            {
                //Trace.WriteLine("HandleWebResponse on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

                if (response == null)
                {
                    this.SetTexture(null);
                    return;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    this.TextureNotFound = true;
                }
                else if (response.StatusCode != HttpStatusCode.OK)
                {
                    this.SetTexture(null);
                    return;
                }
                

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
                    Byte[] data = new byte[response.ContentLength];

                    using (Stream stream = response.GetResponseStream())
                    {
                        Debug.Assert(stream != null);
                        if (stream == null)
                        {
                            this.SetTexture(null);
                            return;
                        }

                        int BytesRead = 0;
                        while (BytesRead < response.ContentLength)
                        {
                            BytesRead += stream.Read(data, BytesRead, (data.Length - BytesRead));
                        }
                    }

                    if (CacheFilename != null)
                    {
                        Action<String, byte[]> AddToCache = Global.TextureCache.AddAsync;
                        AddToCache.BeginInvoke(CacheFilename, data, null, null);
                    }

                    //state.Dispose();

                    TextureFromStreamAsync(graphicsDevice, data);
                }
                catch (WebException e)
                {
                    ProcessTextureWebException(e, BodyRequestState);
                }
                catch (InvalidOperationException e)
                {
                    //TODO: There is an interaction with aborting requests where an corrupt version of the image ends up in the cache and continues to be used.  I have to 
                    //figure out how to flush that bad image out of the cache if this occurs. Currently the workaround is to never cache images


                    //Trace.WriteLine(e.Message, "TextureUse");

                    this.SetTexture(null);
                }
                catch (Exception e)
                {
                    //Trace.WriteLine("Unanticipated exception loading texture: " + requestState.request.RequestUri.ToString(), "TextureUse");
                    //Trace.WriteLine(e.Message, "TextureUse");

                    this.SetTexture(null);
                }
            }
        }


        /// <summary>
        /// Set objects texture to Null, records if the server responds with 404 not found, prints helpful error message
        /// </summary>
        /// <param name="e"></param>
        /// <param name="state"></param>
        private void ProcessTextureWebException(WebException e, AsyncState state)
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
                        else
                        {
                            //Trace.WriteLine("WebException: " + state.request.Address.ToString());
                            //Trace.WriteLine(ErrorResponse.StatusCode + " : " + ErrorResponse.StatusDescription, "TextureUse");
                        }
                    }
                }
            }

            this.SetTexture(null);
        }


        private void EndReadResponseStream(IAsyncResult asyncResult)
        {
            AsyncState requestState = (AsyncState)asyncResult.AsyncState;
             
            lock (this)
            {
                try
                {
                    //Trace.WriteLine("EndReadResponseStream: " + requestState.TextureURI); 
                    int read = requestState.responseStream.EndRead(asyncResult);
                    requestState.TotalRead += read;

                    if (Aborted || IsDisposed)
                    {
                        //Trace.WriteLine("Ignoring EndReadResponseStream response for: " + this.Filename.ToString());
                        requestState.Dispose();
                        requestState = null;
                        return;
                    }

                    if (requestState.TotalRead < requestState.response.ContentLength)
                    {
                        Debug.Assert(requestState.ReadRequestSize() + requestState.TotalRead <= requestState.response.ContentLength);
                        requestState.responseStream.BeginRead(requestState.databuffer, requestState.TotalRead, requestState.ReadRequestSize(), new AsyncCallback(this.EndReadResponseStream), requestState);
                    }
                    else
                    {
                        if (CacheFilename != null)
                        {
                            Global.TextureCache.AddAsync(CacheFilename, requestState.databuffer);
                        }
                         
//                        Texture2D tex = TextureFromStream(graphicsDevice, requestState.databuffer, this.MipMapLevels > 0);
 //                       this.SetTexture(tex); 

                        //Trace.WriteLine("TextureFromStream: " + requestState.TextureURI); 
                        TextureFromStreamAsync(graphicsDevice, requestState.databuffer);
                    }
                }
                catch (System.OutOfMemoryException e)
                {
                    //Trace.WriteLine("Out of memory loading texture: " + requestState.request.RequestUri.ToString(), "TextureUse");
                    //Trace.WriteLine(e.Message, "TextureUse");
                    //                        System.GC.Collect();

                    this.SetTexture(null);
                }
                catch (WebException e)
                {
                    ProcessTextureWebException(e, requestState);
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

        private Byte[] StreamToBytes(Stream stream)
        {
            byte[] data = new byte[stream.Length];
            int bytesRead = 0;
            while (bytesRead < stream.Length)
            {
                bytesRead += stream.Read(data, bytesRead, (int)(stream.Length - bytesRead));

                //Trace.WriteLineIf(bytesRead < stream.Length, "Not all bytes read on first try when loading filestream: " + this.CacheFilename);
            }

            return data;
        }

        public void Go()
        {
            ThreadPoolCallback(null); 
        }
    
        public void ThreadPoolCallback(Object threadContext)
        {
            
            //Trace.WriteLine("ThreadPoolCallback for " + ID.ToString() + " " + this.Filename.ToString());
            /*Nothing to do if we were aborted already*/
            if (Aborted || IsDisposed)
            {
                //Trace.WriteLine("Ignoring threadcallback for: " + this.Filename.ToString());
                return;
            }

            Texture2D tex = null;

            if (Filename.Scheme.ToLower() == "http" || Filename.Scheme.ToLower() == "https")
            {
                Stream texStream = null;

                lock (this)
                {
                    if (Aborted || IsDisposed)
                    {
                        //Trace.WriteLine("Ignoring threadcallback for: " + this.Filename.ToString());
                        return;
                    }

                    try
                    {
                        
                        texStream = TextureReader.TryLoadingFromCache(this.Filename, CacheFilename);
                        if (texStream != null)
                        {
                            //Trace.WriteLine("Found in cache: " + this.Filename.ToString());
                            try
                            {
                                //TextureFromStream(texStream, this.MipMapLevels > 0);

                                byte[] data = StreamToBytes(texStream);
                                TextureFromStreamAsync(graphicsDevice, data);

                                return;
                            }
                            catch (OutOfMemoryException e)
                            {
                                SetTexture(null); 
                            }
                            catch (ArgumentException e)
                            {
                                TryDeleteFile(CacheFilename);

                                SetTexture(null);
                            }
                            finally
                            {
                                texStream.Close();
                                texStream = null; 
                            }
                        }
                        
                        //If we didn't already load the tilestream from the cache
                        

                        TryLoadingFromServer(this.Filename);
                        //SetTexture(null);
                        return;
                    } 
                    finally
                    {
                        if (texStream != null)
                        {
                            texStream.Close(); 
                            texStream = null;
                        }
                    }
                }
            }
            else
            {
                lock (this)
                {
                    if (Aborted)
                        return;

                    try
                    {
                        using (FileStream stream = System.IO.File.OpenRead(Filename.ToString()))
                        {
                            if (stream != null)
                            {
                                byte[] data = StreamToBytes(stream);
                                TextureFromStreamAsync(graphicsDevice, data);
                                //tex = Texture2D.FromStream(graphicsDevice, stream);
                                //lock (this)
                                //{
                                    //tex = TextureFromStream(graphicsDevice, stream, this.MipMapLevels > 0);
                                //}
                            }
                        }

                        if (tex != null)
                            Global.AddTexture(tex, Filename.ToString());

                        SetTexture(tex);
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
                    }
                }

            }

            
        }

        protected void SetTexture(Texture2D tex)
        {
            lock (this)
            {
                //Trace.WriteLine("SetTexture: " + this.Filename.ToString()); 

                if (IsDisposed && tex != null)
                {
                    tex.Dispose();
                    Global.RemoveTexture(tex);
                    tex = null; 
                }

                if (BodyRequestState != null)
                {
                    this.BodyRequestState.Dispose();
                    this.BodyRequestState = null;
                }
                
                this._Result = tex;
                this.FinishedReading = true;
                graphicsDevice = null;

                if(!IsDisposed)
                    DoneEvent.Set();
            }
        }

        public override bool Equals(object obj)
        {
            TextureReader Tobj = obj as TextureReader;
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

        protected void TextureFromStream(GraphicsDevice device, Byte[] streamdata)
        {
            TextureData data = TextureReader.TextureDataFromStream(device, streamdata);
            if(data == null)
            {
                this.SetTexture(null);
                return;
            }

            Texture2D tex = TextureFromData(graphicsDevice, data, this.MipMapLevels> 0);
            this.SetTexture(tex);
            return;
        }
        
        protected void TextureFromStreamAsync(GraphicsDevice device, Byte[] streamdata)
        {
            //Trace.WriteLine("TextureFromStreamAsync: " + this.Filename.ToString()); 
            if (this.Aborted || this.IsDisposed)
                return;

            Func<GraphicsDevice, byte[], TextureData> func = TextureReader.TextureDataFromStream;

            IAsyncResult result = func.BeginInvoke(device, streamdata, EndTextureDataFromStream, func);
        }

        protected void EndTextureDataFromStream(IAsyncResult result)
        {
            lock(this)
            {
                if (this.Aborted || this.IsDisposed)
                    return;

                try
                {
                    //Trace.WriteLine("EndTextureDataFromStream: " + this.Filename.ToString()); 

                    Func<GraphicsDevice, byte[], TextureData> func = result.AsyncState as Func<GraphicsDevice, byte[], TextureData>;

                    TextureData texdata = func.EndInvoke(result);

                    if (texdata == null)
                    {
                        this.SetTexture(null);
                        return;
                    }

                    //Trace.WriteLine("CreateTextureFromData: " + this.Filename.ToString()); 
                    Texture2D tex = TextureFromData(graphicsDevice, texdata, this.MipMapLevels > 0);
                    this.SetTexture(tex); 
                }
                catch (ArgumentException e)
                { 
                    //Trace.WriteLine("Could not create texture");
                    this.SetTexture(null);
                }
            }
        }

        


        public static TextureData TextureDataFromStream(GraphicsDevice graphicsDevice, byte[] streamdata)
        { 
            using (MemoryStream stream = new MemoryStream(streamdata))
            {
                return TextureDataFromStream(graphicsDevice, stream);
            } 
        }

        public static TextureData TextureDataFromStream(GraphicsDevice graphicsDevice, Stream stream)
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
                        byte[] lineValues = new byte[TotalBytes];
                        for (int iY = 0; iY < data.Height; iY++)
                        {
                            //                            int yDataOffset = iY * data.Width;

                            //The documentation and tooltips for the Marshal.Copy function are just wrong...
                            System.Runtime.InteropServices.Marshal.Copy(ptr + (iY * data.Stride), lineValues, 0, data.Width * PixelSize);

                            Array.Copy(lineValues, 0, rgbValues, iY * (data.Width * PixelSize), data.Width * PixelSize);
                        }
                    }

                    //Grrr... have to remap every pixel using the palette
                    if (image.Palette.Entries.Length > 0)
                    {
                        for (int i = 0; i < rgbValues.Length; i++)
                        {
                            rgbValues[i] = image.Palette.Entries[rgbValues[i]].R;
                        }
                    }

                    image.UnlockBits(data);
                    data = null;

                }
            }
            catch (ArgumentException e)
            {
                return null;
            }
            catch (System.OutOfMemoryException e)
            {
                Trace.WriteLine("Out of memory when allocating texture"); 
                return null;
            }

            if (rgbValues != null)
            {
                int WidthHeight = Width * Height;
                Byte[] pixelBytes = new Byte[WidthHeight];

                for (int iSourceByte = 0, iDestByte = 0; iDestByte < WidthHeight; iSourceByte += PixelSize)
                {
                    pixelBytes[iDestByte++] = rgbValues[iSourceByte];
                }

                if (graphicsDevice == null)
                    return null;

                if (graphicsDevice.IsDisposed)
                    return null;

                return new TextureData(pixelBytes, Width, Height);
    /*
                Texture2D tex = null;
                try
                {
                    if (graphicsDevice.GraphicsProfile == GraphicsProfile.Reach)
                    {
                        tex = new Texture2D(graphicsDevice, Width, Height, mipmap, SurfaceFormat.Color);
                        tex.SetData<int>(Array.ConvertAll<byte, int>(pixelBytes, new Converter<byte, int>((x) => (int)x << 24)));
                        //return Texture2D.FromStream(graphicsDevice, stream);
                    }
                    else
                    {
                        tex = new Texture2D(graphicsDevice, Width, Height, mipmap, SurfaceFormat.Alpha8);
                        tex.SetData<Byte>(pixelBytes);
                    }
                }
                catch
                {
                    if (tex != null)
                    {
                        tex.Dispose();
                        tex = null;
                    }
                }

                return tex;
     */
            }


            return null;
        }

        public static Texture2D TextureFromStream(GraphicsDevice graphicsDevice, Stream texStream, bool mipmap)
        {
            TextureData texData = TextureDataFromStream(graphicsDevice, texStream);
            return TextureFromData(graphicsDevice, texData, mipmap);
        }

        public static Texture2D TextureFromData(GraphicsDevice graphicsDevice, TextureData texdata, bool mipmap)
        {
            //Trace.WriteLine("TextureFromData: " + this.Filename.ToString()); 
            Texture2D tex = null;
            try
            {
                Debug.Assert(texdata.width * texdata.height == texdata.pixelBytes.Length);
                if (graphicsDevice.GraphicsProfile == GraphicsProfile.Reach)
                {
                    tex = new Texture2D(graphicsDevice, texdata.width, texdata.height, mipmap, SurfaceFormat.Color);
                    tex.SetData<int>(Array.ConvertAll<byte, int>(texdata.pixelBytes, new Converter<byte, int>((x) => (int)x << 24)));
                    //return Texture2D.FromStream(graphicsDevice, stream);
                }
                else
                {       
                    tex = new Texture2D(graphicsDevice, texdata.width, texdata.height, mipmap, SurfaceFormat.Alpha8);
                    tex.SetData<Byte>(texdata.pixelBytes);
                }
            }
            catch
            {
                if (tex != null)
                {
                    tex.Dispose();
                    tex = null;
                }
            }

            return tex;

        }


        #region IDisposable Members

        protected virtual void Dispose(bool disposing)
        {
            lock (this)
            {
                //Trace.WriteLine("Dispose TextureReader: " + this.Filename.ToString());
                IsDisposed = true;


                //Debug.Assert(_Result == null);
                if (_Result != null)
                {
                    _Result.Dispose();
                    _Result = null;
                }

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
                        catch (WebException e)
                        {
                            //Trace.WriteLine(e.Message, "TextureReader.Dispose");
                        }
                    }

                    BodyRequestState.Dispose();
                }

                if (DoneEvent != null)
                {
                    DoneEvent.Close();
                    DoneEvent = null;
                }
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
