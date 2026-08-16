using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace Viking.ViewModels
{
    /// <summary>
    /// A tile is a combination of:
    ///     A unique identifier determined by texture file name
    ///     A texture, which may or may not be loaded
    ///     A set of verticies to position the tile in space
    /// </summary>
    public class TileView : IDisposable, IEquatable<TileView>
    {
        readonly TileViewModel _tileViewModel;

        /// <summary>
        /// Stores the verticies used for drawing
        /// </summary>
        private VertexBuffer? VertBuffer = null;

        private IndexBuffer? IndBuffer = null;

        /// <summary>
        /// Indices passed to render call specifying triangle verticies
        /// </summary>
        int[] TriangleIndicies => _tileViewModel.TriangleIndicies;

        /// <summary>
        /// Size of the tile in memory
        /// </summary>
        public readonly int Size;

        /// <summary>
        /// The amount the tile has been downsampled by
        /// </summary>
        public int Downsample => _tileViewModel.Downsample;

        /// <summary>
        /// World-space bounds of the tile (from the underlying TileViewModel).
        /// </summary>
        public Rectangle Bounds => _tileViewModel.Bounds;

        /// <summary>
        /// Section number (Z) of the tile (from the underlying TileViewModel.UniqueKey).
        /// </summary>
        public int Section => _tileViewModel.UniqueKey.Section;

        /// <summary>
        /// Setting this to true indicates we've already asked the server for this texture and it was not found.  We should stop asking.
        /// </summary>
        public bool ServerTextureNotFound { get; internal set; }

        /// <summary>
        /// This is not null if we have a thread loading our texture.  It can be cancelled to abort the loading.
        /// </summary>
        private CancellationTokenSource? TextureLoadCancellationTokenSource = null;

        /// <summary>
        /// Set to true when DrawSection has queued a fire-and-forget load task but
        /// GetOrLoadTextureAsync has not yet begun executing.  Prevents redundant
        /// Task.Run launches on subsequent draw frames.
        /// </summary>
        private volatile bool _loadQueued;

        private CancellationToken? _SectionLoadingToken;
        public bool SectionLoadingCancelled => _SectionLoadingToken?.IsCancellationRequested ?? false;

        /// <summary>
        /// This should only be written via the texture member 
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D? _texture;

        Microsoft.Xna.Framework.Graphics.Texture2D? texture
        {
            get
            {
                if (ServerTextureNotFound)
                    return null;

                var texture = _texture;
                if (texture != null)
                {
                    //Ensure the texture is valid
                    if (texture.IsDisposed || texture.GraphicsDevice.IsDisposed)
                    {

                        Interlocked.CompareExchange(ref _texture, null, texture);
                    }
                }

                return _texture;
            }
            set
            {
                var originalTexture = Interlocked.Exchange(ref _texture, value);
                originalTexture?.Dispose();
                //DisposeTextureThreadingObj disposeObj = new DisposeTextureThreadingObj(_texture);
                //ThreadPool.QueueUserWorkItem(disposeObj.ThreadPoolCallback);
                //Global.RemoveTexture(_texture);  //Texture removed from global records within the thread
            }
        }

        /// <summary>
        /// Called from the main-thread texture queue pump to assign a newly created texture. Internal for use by PendingTextureQueue.
        /// </summary>
        internal void SetTextureFromQueue(Texture2D tex)
        {
            texture = tex;
            var previousCts = Interlocked.Exchange(ref TextureLoadCancellationTokenSource, null);
            previousCts?.Dispose();
            _SectionLoadingToken = null;
        }

        /// <summary>
        /// True if _texture is non-null and neither the texture nor its device are disposed.
        /// Thread-safe: captures the field once to avoid TOCTOU races.
        /// </summary>
        private bool IsTextureUsable
        {
            get
            {
                var tex = _texture;
                return tex != null && !tex.IsDisposed && !tex.GraphicsDevice.IsDisposed;
            }
        }

        internal bool HasTexture => IsTextureUsable;

        internal bool TextureReadComplete =>
            (IsTextureUsable || this.ServerTextureNotFound)
            && this.TextureLoadCancellationTokenSource is null;

        internal bool TextureNeedsLoading =>
            !ServerTextureNotFound
            && !IsTextureUsable
            && !_loadQueued
            && TextureLoadCancellationTokenSource is null;

        internal bool TextureIsLoading => TextureLoadCancellationTokenSource != null;

        /// <summary>
        /// Marks this tile as having a queued load so that TextureNeedsLoading
        /// returns false until GetOrLoadTextureAsync begins executing.
        /// </summary>
        internal void MarkLoadQueued() => _loadQueued = true;

        public int TileID;

        public readonly string TextureFileName;
        public readonly string TextureCachedFileName;

        /// <summary>
        /// Transform/mapping name used in the cache key (same as TileViewModelCache.TileKey second component).
        /// </summary>
        public readonly string TransformName;

        private readonly int MipMapLevels = 1;

        /// <summary>
        /// Mipmap levels for texture loading. Internal for use by TextureRequestQueue.
        /// </summary>
        internal int MipMapLevelsForLoad => MipMapLevels;

        private readonly Color TileColor;

        //private Object thisLock = new Object();

        private readonly ReaderWriterLockSlim rwTextureLock = new();

        private static ushort IntToShort(int value) => (ushort)value;

        public TileView(TileViewModel tileViewModel,
                             string textureFileName,
                             string cachedTextureFileName,
                             int mipMapLevels,
                             int size,
                             string transformName)
        {
            this._tileViewModel = tileViewModel;
            this.Size = size;
            this.TileID = textureFileName.GetHashCode();
            this.TextureFileName = textureFileName;
            this.TextureCachedFileName = cachedTextureFileName;
            this.TransformName = transformName ?? string.Empty;
            this.MipMapLevels = mipMapLevels;

            Random r = new(TileID);

            this.TileColor = new Color((float)(r.NextDouble() * 0.5) + 0.5f, (float)(r.NextDouble() * 0.5) + 0.5f, (float)(r.NextDouble() * 0.5) + 0.5f, 0.5f);

            //TryCreateCacheDirectory(System.IO.Path.GetDirectoryName(cachedTextureFileName));
        }

        /// <summary>
        /// Logical identity matches TileViewModel.UniqueKey so pending set and other collections deduplicate by tile.
        /// </summary>
        public override int GetHashCode() => _tileViewModel.UniqueKey.GetHashCode();

        /// <summary>
        /// Logical identity matches TileViewModel.UniqueKey so pending set and other collections deduplicate by tile.
        /// </summary>
        public override bool Equals(object obj) => Equals(obj as TileView);

        /// <summary>
        /// Logical identity matches TileViewModel.UniqueKey so pending set and other collections deduplicate by tile.
        /// </summary>
        public bool Equals(TileView? other) =>
            other is not null && _tileViewModel.UniqueKey == other._tileViewModel.UniqueKey;

        /*
        private static void TryCreateCacheDirectory(string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(path);
            }
            catch
            {
                return;
            }
        }*/

        /// <summary>
        /// Create a vertex buffer for our verticies
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private static VertexBuffer CreateVertexBuffer(GraphicsDevice device, VolumeModel.PositionNormalTextureVertex[] Vertices)
        {
            if (Vertices.Length == 0)
                return null;

            VertexPositionNormalTexture[] vertArray = new VertexPositionNormalTexture[Vertices.Length];

            for (int i = 0; i < Vertices.Length; i++)
            {
                Geometry.Vector3 pos = Vertices[i].Position;
                Geometry.Vector3 norm = Vertices[i].Normal;
                Geometry.Vector2 tex = Vertices[i].Texture;

                vertArray[i] = new VertexPositionNormalTexture(new Vector3((float)pos.X, (float)pos.Y, (float)pos.Z),
                                                                new Vector3((float)norm.X, (float)norm.Y, (float)norm.Z),
                                                                new Vector2((float)tex.X, (float)tex.Y));

            }

            VertexBuffer vb = null;
            try
            {
                vb = new VertexBuffer(device, typeof(VertexPositionNormalTexture), vertArray.Length, BufferUsage.None);

                vb.SetData<VertexPositionNormalTexture>(vertArray);
            }
            catch (Exception)
            {
                vb?.Dispose();
                vb = null;
                throw;
            }

            return vb;
        }

        public override string ToString() => TextureFileName;

        public void FreeTexture()
        {
            try
            {
                AbortRequest();

                //This disposes of the texture
                this.texture = null;

                rwTextureLock.EnterWriteLock();

                if (VertBuffer != null)
                {
                    this.VertBuffer.Dispose();
                    this.VertBuffer = null;
                }

                if (IndBuffer != null)
                {
                    this.IndBuffer.Dispose();
                    this.IndBuffer = null;
                }
            }
            finally
            {
                rwTextureLock.ExitWriteLock();
            }

        }

        public void AbortRequest()
        {
            var tokenSource = Interlocked.Exchange(ref TextureLoadCancellationTokenSource, null);
            if (tokenSource != null && !tokenSource.IsCancellationRequested)
            {
                Trace.WriteLine($"Aborting {this.TextureFileName}");
                tokenSource.Cancel();
            }
            tokenSource?.Dispose();

            _SectionLoadingToken = null;
        }

        /// <summary>
        /// Returns a texture if it is loaded, otherwise begins a request to get the texture
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        public async Task<Texture2D> GetOrLoadTextureAsync(GraphicsDevice graphicsDevice, CancellationToken token)
        {
            _loadQueued = false;
            _SectionLoadingToken = token;

            if (token.IsCancellationRequested)
            {
                return null;
            }

            //Check if the texture's graphics device has been disposed, in which case load a new texture

            //Don't bother asking if we've already tried
            if (this.ServerTextureNotFound)
            {
#if DEBUG
                {
                    //Don't know how this could happen, but we should not have a texture if the server does not.  This indicates the code will leak resources
                    //Debug.Assert(TexReader is null);
                    Debug.Assert(texture is null);
                }
#endif

                return null;
            }

            var currentTexture = texture;
            if (currentTexture != null)
                return currentTexture;

            // Already in the queue (or dequeued but not yet completed); don't start another load.
            if (PendingTextureQueue.IsTileViewPending(this) || TextureRequestQueue.IsTileViewPending(this))
            {
                return null;
            }

            // Enqueue to priority-sorted request queue (both HTTP and local paths)
            return await TextureRequestQueue.EnqueueRequest(this, graphicsDevice, token).ConfigureAwait(false);
        }

        private Texture2D CompleteTextureReadTask(TextureReaderV2 texReader, Task<Texture2D> texTask)
        {
            var tokenSource = Interlocked.Exchange(ref TextureLoadCancellationTokenSource, null);
            if (tokenSource is null || tokenSource.IsCancellationRequested)
                return null;

            this.ServerTextureNotFound = texReader.TextureNotFound;

            if (texTask.IsFaulted == false && texTask.IsCanceled == false && texReader.HasTexture)
            {
                // Task is known completed; .Result is safe. Caller runs on background thread.
                this.texture = texTask.Result;
                return this.texture;
            }

            return null;
        }


#if DEBUG
        private static bool NullGridWarningPrinted = false;
#endif

        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.TileLayoutEffect effect, bool AsynchTextureLoad, bool UseColor)
        {
            if (TriangleIndicies is null)
            {
#if DEBUG
                if (!NullGridWarningPrinted)
                {
                    NullGridWarningPrinted = true;
                    Trace.WriteLine("Null Grid Indices for " + this.TextureFileName, "Tile");
                }
#endif

                return;
            }

            if (TriangleIndicies.Length == 0)
            {
#if DEBUG
                if (!NullGridWarningPrinted)
                {
                    NullGridWarningPrinted = true;
                    Trace.WriteLine("No Grid Indices for " + this.TextureFileName, "Tile");
                }
#endif
                return;
            }

            Texture2D currentTexture = null;
            try
            {
                //rwTextureLock.EnterReadLock();

                //Texture2D currentTexture = GetOrRequestTexture(graphicsDevice);  
                currentTexture = this.texture;

                //Do not draw if we don't have a texture
                if (currentTexture is null)
                    return;

                if (currentTexture.IsDisposed)
                    return;

                //Create the verticies if they don't exist or if they've been disposed (device reset)
                if (this.VertBuffer is null || this.VertBuffer.IsDisposed)
                {
                    // Dispose old buffer if it exists but is disposed (to be safe)
                    if (this.VertBuffer is not null && this.VertBuffer.IsDisposed)
                    {
                        this.VertBuffer = null;
                    }
                    VertBuffer = CreateVertexBuffer(graphicsDevice, _tileViewModel.Vertices);
                }

                if (VertBuffer is null || VertBuffer.VertexCount == 0)
                    return;

                //Create Index buffer if it doesn't exist or if it's been disposed (device reset)
                if (IndBuffer is null || IndBuffer.IsDisposed)
                {
                    // Dispose old buffer if it exists but is disposed (to be safe)
                    if (IndBuffer is not null && IndBuffer.IsDisposed)
                    {
                        IndBuffer = null;
                    }
                    IndBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, _tileViewModel.TriangleIndicies.Length, BufferUsage.None);
                    IndBuffer.SetData<ushort>(Array.ConvertAll<int, ushort>(TriangleIndicies, new Converter<int, ushort>(IntToShort)));
                }
            }
            finally
            {
                // rwTextureLock.ExitReadLock();
            }

            graphicsDevice.SetVertexBuffer(this.VertBuffer);
            graphicsDevice.Indices = this.IndBuffer;

            effect.Texture = currentTexture;

            if (UseColor)
                effect.TileColor = TileColor;


            //PORT XNA 4
            //effect.effect.Begin();

            foreach (EffectPass pass in effect.effect.CurrentTechnique.Passes)
            {
                //PORT XNA 4
                //pass.Begin();
                pass.Apply();

                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TriangleIndicies.Length / 3);
                /*
                graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                                        this.Vertices,
                                        0,
                                        this.Vertices.Length, 
                                        TriangleIndicies, 
                                        0,
                                        TriangleIndicies.Length / 3);
                */
                //PORT XNA 4
                //pass.End();
            }

            //PORT XNA 4
            //effect.effect.End(); 
        }

        //PORT XNA 4
        //static VertexDeclaration VertexPositionColorDeclaration = null;
        VertexBuffer? vbMesh = null;
        IndexBuffer? ibMesh = null;
        //PORT XNA 4
        readonly VertexPositionColor[]? MeshVerticies = null;

        //int[] MeshEdges = null;

        public static VertexPositionColor[] CreateMeshVerticies(TileViewModel t, Color color)
        {
            VertexPositionColor[] meshVerticies = new VertexPositionColor[t.Vertices.Length];

            if (meshVerticies.Length == 0)
                throw new ArgumentException("No verticies for tile", "t");

            for (int iVert = 0; iVert < meshVerticies.Length; iVert++)
            {
                meshVerticies[iVert] = new VertexPositionColor(new Vector3((float)t.Vertices[iVert].Position.X,
                                                                               (float)t.Vertices[iVert].Position.Y, (float)0)
                                                                                                            , color);
            }

            return meshVerticies;
        }

        private void CreateMesh(GraphicsDevice graphicsDevice)
        {
            Random ColorGen = new(this.GetHashCode());
            byte[] randColorBytes = new byte[3];
            ColorGen.NextBytes(randColorBytes);
            randColorBytes[0] = randColorBytes[0] < 128 ? (byte)(randColorBytes[0] + 128) : randColorBytes[0];
            randColorBytes[1] = randColorBytes[1] < 128 ? (byte)(randColorBytes[1] + 128) : randColorBytes[1];
            randColorBytes[2] = randColorBytes[2] < 128 ? (byte)(randColorBytes[2] + 128) : randColorBytes[2];
            Color color = new(randColorBytes[0], randColorBytes[1], randColorBytes[2]);
            VertexPositionColor[] meshVerticies = TileView.CreateMeshVerticies(this._tileViewModel, color);

            vbMesh = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), meshVerticies.Length, BufferUsage.None);
            vbMesh.SetData<VertexPositionColor>(meshVerticies);

            List<int> TrianglesAsLines = [];

            for (int i = 0; i < TriangleIndicies.Length; i += 3)
            {
                TrianglesAsLines.Add(TriangleIndicies[i]);
                TrianglesAsLines.Add(TriangleIndicies[i + 1]);
                TrianglesAsLines.Add(TriangleIndicies[i + 1]);
                TrianglesAsLines.Add(TriangleIndicies[i + 2]);
                TrianglesAsLines.Add(TriangleIndicies[i + 2]);
                TrianglesAsLines.Add(TriangleIndicies[i]);
            }

            ibMesh = new IndexBuffer(graphicsDevice, typeof(int), TrianglesAsLines.Count, BufferUsage.None);
            ibMesh.SetData<int>([.. TrianglesAsLines]);
        }

        public void DrawMesh(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            // Check if mesh buffers need to be created or recreated (after device reset)
            if (vbMesh is null || vbMesh.IsDisposed || ibMesh is null || ibMesh.IsDisposed)
            {
                // Dispose any existing disposed buffers
                if (vbMesh is not null && vbMesh.IsDisposed)
                    vbMesh = null;
                if (ibMesh is not null && ibMesh.IsDisposed)
                    ibMesh = null;

                CreateMesh(graphicsDevice);
                //If this tile has no verticies vbMesh can be null even after a call to CreateMesh
                if (vbMesh is null)
                    return;
            }

            if (vbMesh.VertexCount == 0)
                return;

            //PORT XNA 4
            //graphicsDevice.VertexDeclaration = TileViewModel.VertexPositionColorDeclaration;

            basicEffect.Texture = null;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;

            DepthStencilState originalDepthState = graphicsDevice.DepthStencilState;

            DepthStencilState newDepthState = new()
            {
                DepthBufferEnable = false,
                StencilEnable = false
            };

            try
            {
                graphicsDevice.DepthStencilState = newDepthState;

                graphicsDevice.SetVertexBuffer(vbMesh);
                graphicsDevice.Indices = ibMesh;

                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, ibMesh.IndexCount / 2);
                }
            }
            finally
            {
                if (originalDepthState != null && !originalDepthState.IsDisposed)
                    graphicsDevice.DepthStencilState = originalDepthState;
                newDepthState?.Dispose();
            }
        }

        private VikingXNAGraphics.LabelView? _TileLabel = null;
        internal VikingXNAGraphics.LabelView TileLabel
        {
            get
            {
                _TileLabel ??= new VikingXNAGraphics.LabelView(this._tileViewModel.TextureFullPath, this._tileViewModel.Bounds.Center, Color.Yellow, scaleFontWithScene: true, fontSize: Math.Max(_tileViewModel.Bounds.Width, _tileViewModel.Bounds.Height) / 25.0);

                return _TileLabel;
            }
        }

        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene)
        {


            /*
            float Scale = (float)(1.0f / _Parent.StatusMagnification);
            Vector2 Offset;

            _Parent.spriteBatch.Begin();

            for (int i = 0; i < this.Tile.Vertices.Length; i++)
            {
                Geometry.Vector2 ControlPositionScreen = _Parent.WorldToScreen(this.Tile.Vertices[i].Position.X, this.Tile.Vertices[i].Position.Y); 

                Offset = _Parent.GetLabelSize(_Parent.fontArial, i.ToString());
                Offset.X /= 2f;
                Offset.Y /= 2f;

                _Parent.spriteBatch.DrawString(_Parent.fontArial,
                                        i.ToString(),
                                        new Vector2((float)ControlPositionScreen.X, (float)ControlPositionScreen.Y),
                                        this.TileColor,
                                        0,
                                        Offset,
                                        Scale,
                                        SpriteEffects.None,
                                        0); 
            }

            if (this.Tile.Vertices.Length > 0)
            {
                double TileNameX = this.Tile.Bounds.Left + (this.Tile.Bounds.Width / 2);
                double TileNameY = this.Tile.Bounds.Bottom + (this.Tile.Bounds.Height / 2);
                Geometry.Vector2 NamePositionScreen = _Parent.WorldToScreen(TileNameX, TileNameY);
                Offset = _Parent.GetLabelSize(_Parent.fontArial, this.Tile.TextureFullPath);
                Offset.X /= 2f;
                Offset.Y /= 2f;

                _Parent.spriteBatch.DrawString(_Parent.fontArial,
                                        this.Tile.TextureFullPath.ToString(),
                                        new Vector2((float)NamePositionScreen.X, (float)NamePositionScreen.Y),
                                        this.TileColor,
                                        0,
                                        Offset,
                                        Scale,
                                        SpriteEffects.None,
                                        0);
            }


            _Parent.spriteBatch.End();
            */
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                AbortRequest();

                try
                {
                    rwTextureLock.EnterWriteLock();

                    //This disposes of the texture
                    //_texture = null;
                    if (this._texture != null)
                    {
                        if (!this._texture.IsDisposed)
                        {
                            this._texture.Dispose();
                            this._texture = null;
                        }
                    }

                    vbMesh?.Dispose();
                    vbMesh = null;
                    ibMesh?.Dispose();
                    ibMesh = null;

                    if (VertBuffer != null)
                    {
                        this.VertBuffer.Dispose();
                        this.VertBuffer = null;
                    }

                    if (IndBuffer != null)
                    {
                        this.IndBuffer.Dispose();
                        this.IndBuffer = null;
                    }
                }
                finally
                {
                    rwTextureLock.ExitWriteLock();
                }
            }
        }


        #endregion
    }
}
