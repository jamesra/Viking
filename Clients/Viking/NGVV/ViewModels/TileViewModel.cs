using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Viking.UI;
using Viking.VolumeModel;

namespace Viking.ViewModels
{
    /// <summary>
    /// A tile is a combination of:
    ///     A unique identifier determined by texture file name
    ///     A texture, which may or may not be loaded
    ///     A set of verticies to position the tile in space
    /// </summary>
    public class TileViewModel : IDisposable
    {
        readonly Tile Tile;

        /// <summary>
        /// Stores the verticies used for drawing
        /// </summary>
        private VertexBuffer VertBuffer = null;

        private IndexBuffer IndBuffer = null;

        /// <summary>
        /// Indicies passed to render call specifying triangle verticies
        /// </summary>
        int[] TriangleIndicies { get { return Tile.TriangleIndicies; } }

        /// <summary>
        /// Size of the tile in memory
        /// </summary>
        public readonly int Size;

        /// <summary>
        /// The amount the tile has been downsampled by
        /// </summary>
        public int Downsample { get { return Tile.Downsample; } }

        /// <summary>
        /// Setting this to true indicates we've already asked the server for this texture and it was not found.  We should stop asking.
        /// </summary>
        public bool ServerTextureNotFound = false;

        /// <summary>
        /// This is not null if we have a thread loading our texture.  It can be cancelled to abort the loading.
        /// </summary>
        private CancellationTokenSource TextureLoadCancellationTokenSource = null;

        /// <summary>
        /// This should only be written via the texture member 
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D _texture;

        Microsoft.Xna.Framework.Graphics.Texture2D texture
        {
            get
            {
                try
                {
                    rwTextureLock.EnterUpgradeableReadLock();

                    if (_texture != null)
                    {
                        try
                        {
                            rwTextureLock.EnterWriteLock();

                            if (_texture.IsDisposed)
                                _texture = null;

                            if (_texture.GraphicsDevice.IsDisposed)
                                _texture = null;
                        }
                        finally
                        {
                            rwTextureLock.ExitWriteLock();
                        }
                    }

                    if (ServerTextureNotFound)
                        return null;

                    return _texture;
                }
                finally
                {
                    rwTextureLock.ExitUpgradeableReadLock();
                }
            }
            set
            {
                try
                {
                    rwTextureLock.EnterWriteLock();
                    if (value == _texture)
                        return;

                    if (_texture != null)
                    {
                        DisposeTextureThreadingObj.DisposeTexture(_texture);
                        //DisposeTextureThreadingObj disposeObj = new DisposeTextureThreadingObj(_texture);
                        //ThreadPool.QueueUserWorkItem(disposeObj.ThreadPoolCallback);
                        //Global.RemoveTexture(_texture);  //Texture removed from global records within the thread
                    }

                    _texture = value;
                }
                finally
                {
                    rwTextureLock.ExitWriteLock();
                }
            }
        }

        internal bool HasTexture
        {
            get { return this._texture != null; }
        }

        internal bool TextureReadComplete
        {
            get { return (this._texture != null || this.ServerTextureNotFound) && this.TexReader == null; }
        }

        internal bool TextureNeedsLoading
        {
            get { return this.ServerTextureNotFound == false && this.texture == null && this.TexReader == null; }
        }

        internal bool TextureIsLoading
        {
            get { return TexReader != null; }
        }

        private TextureReaderV2 _TexReader;
        private TextureReaderV2 TexReader
        {
            get { return _TexReader; }
            set
            {
                try
                {
                    rwTextureLock.EnterWriteLock();
                    if (_TexReader != null && _TexReader != value)
                    {
                        _TexReader.Dispose();
                        _TexReader = null;
                    }

                    _TexReader = value;
                }
                finally
                {
                    rwTextureLock.ExitWriteLock();
                }
            }
        }

        public int TileID;

        public readonly string TextureFileName;
        public readonly string TextureCachedFileName;

        private int MipMapLevels = 1;

        private Color TileColor;

        //private Object thisLock = new Object();

        private readonly ReaderWriterLockSlim rwTextureLock = new ReaderWriterLockSlim();

        private static ushort IntToShort(int value)
        {
            return (ushort)value;
        }

        public TileViewModel(Tile tile,
                             string textureFileName,
                             string cachedTextureFileName,
                             int mipMapLevels,
                             int size)
        {
            this.Tile = tile;
            this.Size = size;
            this.TileID = textureFileName.GetHashCode();
            this.TextureFileName = textureFileName;
            this.TextureCachedFileName = cachedTextureFileName;
            this.MipMapLevels = mipMapLevels;

            Random r = new Random(TileID);

            this.TileColor = new Color((float)(r.NextDouble() * 0.5) + 0.5f, (float)(r.NextDouble() * 0.5) + 0.5f, (float)(r.NextDouble() * 0.5) + 0.5f, 0.5f);

        }

        /// <summary>
        /// Create a vertex buffer for our verticies
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private static VertexBuffer CreateVertexBuffer(GraphicsDevice device, VolumeModel.PositionNormalTextureVertex[] Verticies)
        {
            if (Verticies.Length == 0)
                return null;

            VertexPositionNormalTexture[] vertArray = new VertexPositionNormalTexture[Verticies.Length];

            for (int i = 0; i < Verticies.Length; i++)
            {
                GridVector3 pos = Verticies[i].Position;
                GridVector3 norm = Verticies[i].Normal;
                GridVector2 tex = Verticies[i].Texture;

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
                if (vb != null)
                {
                    vb.Dispose();
                    vb = null;
                }
                throw;
            }

            return vb;
        }

        public override string ToString()
        {
            return TextureFileName;
        }

        public void FreeTexture()
        {
            try
            {
                //rwTextureLock.EnterWriteLock();

                //Stop trying to load textures if we have a request out
                if (TextureLoadCancellationTokenSource != null)
                {
                    TextureLoadCancellationTokenSource.Cancel();
                    TextureLoadCancellationTokenSource = null;
                }

                //This disposes of the texture
                this.texture = null;

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
               // rwTextureLock.ExitWriteLock();
            }

        }

        public void AbortRequest()
        {

            //Other methods may be counting on TexReader not being set to null, so take a lock
            try
            {
                rwTextureLock.EnterWriteLock();
                if (TextureLoadCancellationTokenSource != null)
                {
                    TextureLoadCancellationTokenSource.Cancel();
                    TextureLoadCancellationTokenSource = null;
                }

                if (this.TexReader != null)
                {
                    if (this.TexReader.FinishedReading)
                        HandleTextureReaderResult();
                    else
                    {
                        this.TexReader.AbortRequest();
                    }
                }
            }
            finally
            {
                rwTextureLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Takes a completd TextureReader and returns true if a texture was loaded
        /// </summary>
        /// <param name="texReader"></param>
        /// <returns></returns>
        private bool HandleTextureReaderResult()
        {
            try
            {

                GetTextureSemaphore.Wait();
                var tex_reader = this.TexReader;

                if (tex_reader == null)
                {
                    Trace.WriteLine("Missing texture reader in TextureReaderResult");
                    return false;
                }

                //If reading the texture failed don't change our state. 
                if (tex_reader.FinishedReading)
                {
                    try
                    {
                        //rwTextureLock.EnterWriteLock();

                        if (tex_reader.HasTexture)
                        {
                            this.texture = tex_reader.GetTexture();
                        }
                        else
                        {
                            //We should stop asking if the server just doesn't have it. 
                            this.ServerTextureNotFound = tex_reader.TextureNotFound;
                        }

                        this.TexReader.Dispose();
                        this.TexReader = null;

                        return this.texture != null;
                    }
                    finally
                    {
                        //rwTextureLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                GetTextureSemaphore.Release();
            }

            return false;

        }

        private void OnTextureReaderCompleted()
        {
            HandleTextureReaderResult();
        }

        SemaphoreSlim GetTextureSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Returns a texture if it is loaded, otherwise begins a request to get the texture
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        public Texture2D GetOrRequestTexture(GraphicsDevice graphicsDevice)
        {

            //Check if the texture's graphics device has been disposed, in which case load a new texture
            if (texture != null)
            {
                if (texture.GraphicsDevice.IsDisposed)
                {
                    texture = null;
                }
            }

            //Don't bother asking if we've already tried
            if (this.ServerTextureNotFound)
            {
#if DEBUG
                /*lock (thisLock)
                {
                    //Don't know how this could happen, but we should not have a texture if the server does not.  This indicates the code will leak resources
                    Debug.Assert(TexReader == null);
                    Debug.Assert(texture == null);
                }*/
#endif

                return null;
            }

            //If we have a texture that is what we want or better then return it and kill any requests
            if (texture != null)
            {
                //Drop any outstanding requests since we have a texture that works
                AbortRequest();

                return this.texture;
            }
            else
            {
                try
                {
                    GetTextureSemaphore.Wait();
                    if (texture != null)
                        return texture; 

                    //In this path we either have no texture, or a low-res texture.  Ask for a new one if we haven't already
                    if (this.TexReader == null)
                    {
                        if (TextureLoadCancellationTokenSource != null)
                            TextureLoadCancellationTokenSource.Cancel();

                        TextureLoadCancellationTokenSource = new CancellationTokenSource();
                        //If the section is read over a network provide a cache path too
                        if (State.volume.IsLocal == false)
                        {
                            this.TexReader = new TextureReaderV2(graphicsDevice,
                                                                new Uri(this.TextureFileName),
                                                                this.TextureCachedFileName,
                                                                this.MipMapLevels,
                                                                () => this.OnTextureReaderCompleted(),
                                                                TextureLoadCancellationTokenSource.Token);
                        }
                        else
                        {
                            this.TexReader = new TextureReaderV2(graphicsDevice,
                                                                new Uri(this.TextureFileName),
                                                                this.MipMapLevels,
                                                                () => this.OnTextureReaderCompleted(),
                                                                TextureLoadCancellationTokenSource.Token);
                        }

                        this.TexReader.LoadTexture();
                    }
                }
                finally
                {
                    GetTextureSemaphore.Release();
                }

                return null;
            }
        }

        /// <summary>
        /// Returns a texture if it is loaded, otherwise begins a request to get the texture
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        public async Task<Texture2D> GetOrLoadTextureAsync(GraphicsDevice graphicsDevice)
        {
            try
            {
                //rwTextureLock.EnterReadLock();
                //Check if the texture's graphics device has been disposed, in which case load a new texture
                var currentTexture = texture; 
                if (currentTexture != null)
                {
                    if (currentTexture.GraphicsDevice.IsDisposed)
                    {
                        texture = null;
                        return null; 
                    }
                }

                //Don't bother asking if we've already tried
                if (this.ServerTextureNotFound)
                {
#if DEBUG
                    {
                        //Don't know how this could happen, but we should not have a texture if the server does not.  This indicates the code will leak resources
                        Debug.Assert(TexReader == null);
                        Debug.Assert(texture == null);
                    }
#endif

                    return null;
                }

                if (currentTexture != null)
                    return currentTexture;
            }
            finally
            {
                //rwTextureLock.ExitReadLock();
            }

          
            try
            {
                await GetTextureSemaphore.WaitAsync();

                if (texture != null)
                    return this.texture;

                TextureReaderV2 texReader = null;

                //In this path we either have no texture, or a low-res texture.  Ask for a new one if we haven't already
                if (texReader == null)
                {
                    if (TextureLoadCancellationTokenSource != null)
                        TextureLoadCancellationTokenSource.Cancel();

                    TextureLoadCancellationTokenSource = new CancellationTokenSource();
                    //If the section is read over a network provide a cache path too
                    if (State.volume.IsLocal == false)
                    {
                        texReader = new TextureReaderV2(graphicsDevice,
                                                            new Uri(this.TextureFileName),
                                                            this.TextureCachedFileName,
                                                            this.MipMapLevels,
                                                            null,
                                                            TextureLoadCancellationTokenSource.Token);
                    }
                    else
                    {
                        texReader = new TextureReaderV2(graphicsDevice,
                                                            new Uri(this.TextureFileName),
                                                            this.MipMapLevels,
                                                            null,
                                                            TextureLoadCancellationTokenSource.Token);
                    }

                    var result = await texReader.LoadTexture();
                    this.texture = texReader.GetTexture();
                    this.ServerTextureNotFound = texReader.TextureNotFound;

                    return result; 
                }
            }
            finally
            {
                GetTextureSemaphore.Release();
            }

            return null;
        }


#if DEBUG
        private static bool NullGridWarningPrinted = false;
#endif

        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.TileLayoutEffect effect, bool AsynchTextureLoad, bool UseColor)
        {
            if (TriangleIndicies == null)
            {
#if DEBUG
                if (!NullGridWarningPrinted)
                {
                    NullGridWarningPrinted = true;
                    Trace.WriteLine("Null Grid Indicies for " + this.TextureFileName, "Tile");
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
                    Trace.WriteLine("No Grid Indicies for " + this.TextureFileName, "Tile");
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
                if (currentTexture == null)
                    return;

                currentTexture = this.texture;

                //Do not draw if we don't have a texture
                if (currentTexture == null)
                    return;

                if (currentTexture.IsDisposed)
                    return;

                //Create the verticies if they don't exist
                if (this.VertBuffer == null)
                {
                    VertBuffer = CreateVertexBuffer(graphicsDevice, Tile.Verticies);
                }

                if (VertBuffer == null || VertBuffer.VertexCount == 0)
                    return;

                //Create Index buffer if it doesn't exist
                if (IndBuffer == null)
                {
                    IndBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, Tile.TriangleIndicies.Length, BufferUsage.None);
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

                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, VertBuffer.VertexCount, 0, TriangleIndicies.Length / 3);
                /*
                graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                                        this.Verticies,
                                        0,
                                        this.Verticies.Length, 
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
        VertexBuffer vbMesh = null;
        IndexBuffer ibMesh = null;
        //PORT XNA 4
        VertexPositionColor[] MeshVerticies = null;

        //int[] MeshEdges = null;

        public static VertexPositionColor[] CreateMeshVerticies(Tile t, Color color)
        {
            VertexPositionColor[] meshVerticies = new VertexPositionColor[t.Verticies.Length];

            if (meshVerticies.Length == 0)
                throw new ArgumentException("No verticies for tile", "t");

            for (int iVert = 0; iVert < meshVerticies.Length; iVert++)
            {
                meshVerticies[iVert] = new VertexPositionColor(new Vector3((float)t.Verticies[iVert].Position.X,
                                                                               (float)t.Verticies[iVert].Position.Y, (float)0)
                                                                                                            , color);
            }

            return meshVerticies;
        }

        private void CreateMesh(GraphicsDevice graphicsDevice)
        {
            Random ColorGen = new Random(this.GetHashCode());
            byte[] randColorBytes = new byte[3];
            ColorGen.NextBytes(randColorBytes);
            randColorBytes[0] = randColorBytes[0] < 128 ? (byte)(randColorBytes[0] + 128) : randColorBytes[0];
            randColorBytes[1] = randColorBytes[1] < 128 ? (byte)(randColorBytes[1] + 128) : randColorBytes[1];
            randColorBytes[2] = randColorBytes[2] < 128 ? (byte)(randColorBytes[2] + 128) : randColorBytes[2];
            Color color = new Color(randColorBytes[0], randColorBytes[1], randColorBytes[2]);
            VertexPositionColor[] meshVerticies = TileViewModel.CreateMeshVerticies(this.Tile, color);

            vbMesh = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), meshVerticies.Length, BufferUsage.None);
            vbMesh.SetData<VertexPositionColor>(meshVerticies);

            List<int> TrianglesAsLines = new List<int>();

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
            ibMesh.SetData<int>(TrianglesAsLines.ToArray());
        }

        public void DrawMesh(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            if (vbMesh == null)
            {
                CreateMesh(graphicsDevice);
                //If this tile has no verticies vbMesh can be null even after a call to CreateMesh
                if (vbMesh == null)
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

            DepthStencilState newDepthState = new DepthStencilState();
            newDepthState.DepthBufferEnable = false;
            newDepthState.StencilEnable = false;
            graphicsDevice.DepthStencilState = newDepthState;

            graphicsDevice.SetVertexBuffer(vbMesh);
            graphicsDevice.Indices = ibMesh;
            //PORT XNA 4
            //basicEffect.CommitChanges();

            //PORT XNA 4
            //basicEffect.Begin();

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                //PORT XNA 4
                //pass.Begin();
                pass.Apply();

                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, vbMesh.VertexCount, 0, ibMesh.IndexCount / 2);

            }

            if (originalDepthState != null)
                graphicsDevice.DepthStencilState = originalDepthState;

        }

        private VikingXNAGraphics.LabelView _TileLabel = null;
        internal VikingXNAGraphics.LabelView TileLabel
        {
            get
            {
                if (_TileLabel == null)
                {
                    _TileLabel = new VikingXNAGraphics.LabelView(this.Tile.TextureFullPath, this.Tile.Bounds.Center, Color.Yellow, scaleFontWithScene: true, fontSize: Math.Max(Tile.Bounds.Width, Tile.Bounds.Height) / 25.0);
                }

                return _TileLabel;
            }
        }

        public void DrawLabel(Viking.UI.Controls.SectionViewerControl _Parent)
        {


            /*
            float Scale = (float)(1.0f / _Parent.StatusMagnification);
            Vector2 Offset;

            _Parent.spriteBatch.Begin();

            for (int i = 0; i < this.Tile.Verticies.Length; i++)
            {
                GridVector2 ControlPositionScreen = _Parent.WorldToScreen(this.Tile.Verticies[i].Position.X, this.Tile.Verticies[i].Position.Y); 

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

            if (this.Tile.Verticies.Length > 0)
            {
                double TileNameX = this.Tile.Bounds.Left + (this.Tile.Bounds.Width / 2);
                double TileNameY = this.Tile.Bounds.Bottom + (this.Tile.Bounds.Height / 2);
                GridVector2 NamePositionScreen = _Parent.WorldToScreen(TileNameX, TileNameY);
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

                    if (_TexReader != null)
                    {
                        _TexReader.AbortRequest();
                        _TexReader.Dispose();
                        _TexReader = null;
                    }

                    if (vbMesh != null)
                    {
                        vbMesh.Dispose();
                        vbMesh = null;
                    }

                    if (ibMesh != null)
                    {
                        ibMesh.Dispose();
                        ibMesh = null;
                    }

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
