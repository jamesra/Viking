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
        public bool ServerTextureNotFound { get; private set; }

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
                var originalTexture = Interlocked.CompareExchange(ref _texture, value, null);

                if (originalTexture != null)
                {
                    originalTexture.DisposeAsync();
                    //DisposeTextureThreadingObj disposeObj = new DisposeTextureThreadingObj(_texture);
                    //ThreadPool.QueueUserWorkItem(disposeObj.ThreadPoolCallback);
                    //Global.RemoveTexture(_texture);  //Texture removed from global records within the thread
                }  
            }
        }

        internal bool HasTexture => _texture != null;

        internal bool TextureReadComplete
        {
            get { return (this._texture != null || this.ServerTextureNotFound) && this.TextureLoadCancellationTokenSource is null; }
        }

        internal bool TextureNeedsLoading
        {
            get { return this.ServerTextureNotFound == false && this.texture is null && this.TextureLoadCancellationTokenSource is null; }
        }

        internal bool TextureIsLoading => TextureLoadCancellationTokenSource != null;
          
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

            //TryCreateCacheDirectory(System.IO.Path.GetDirectoryName(cachedTextureFileName));
        }
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
            var tokenSource = Interlocked.CompareExchange(ref TextureLoadCancellationTokenSource, TextureLoadCancellationTokenSource,
                TextureLoadCancellationTokenSource);

            if (tokenSource != null)
            {
                Trace.WriteLine($"Aborting {this.TextureFileName}");
                tokenSource.Cancel();
            }
        }

        /// <summary>
        /// Returns a texture if it is loaded, otherwise begins a request to get the texture
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        public async Task<Texture2D> GetOrLoadTextureAsync(GraphicsDevice graphicsDevice, CancellationToken token)
        {
            //Check if the texture's graphics device has been disposed, in which case load a new texture

            //Don't bother asking if we've already tried
            if (this.ServerTextureNotFound)
            {
#if DEBUG
                {
                    //Don't know how this could happen, but we should not have a texture if the server does not.  This indicates the code will leak resources
                    //Debug.Assert(TexReader == null);
                    Debug.Assert(texture == null);
                }
#endif

                return null;
            }

            var currentTexture = texture;
            if (currentTexture != null)
                return currentTexture;
        


        //In this path we either have no texture, or a low-res texture.  Ask for a new one if we haven't already
        if (ServerTextureNotFound == false && Interlocked.CompareExchange(ref this.TextureLoadCancellationTokenSource,
                new CancellationTokenSource(), null) is null)
            {
                TextureReaderV2 texReader = null;

                //If the section is read over a network provide a cache path too
                if (State.volume.IsLocal == false)
                {
                    texReader = new TextureReaderV2(graphicsDevice,
                                                        new Uri(this.TextureFileName),
                                                        this.TextureCachedFileName,
                                                        this.MipMapLevels,
                                                        null,
                                                        TextureLoadCancellationTokenSource);
                }
                else
                {
                    texReader = new TextureReaderV2(graphicsDevice,
                                                        new Uri(this.TextureFileName),
                                                        this.MipMapLevels,
                                                        null,
                                                        TextureLoadCancellationTokenSource);
                }

                var loadTextureTask = texReader.LoadTexture();

                await loadTextureTask.ContinueWith(task => CompleteTextureReadTask(texReader, loadTextureTask), TextureLoadCancellationTokenSource.Token).ConfigureAwait(false); 

                return texture;
            }

            return null;
        }

        private void CompleteTextureReadTask(TextureReaderV2 texReader, Task<Texture2D> texTask)
        {
            var tokenSource = Interlocked.Exchange(ref TextureLoadCancellationTokenSource, null);
            if (tokenSource == null || tokenSource.IsCancellationRequested)
                return;

            this.ServerTextureNotFound = texReader.TextureNotFound;

            if (texTask.IsFaulted == false && texTask.IsCanceled == false && texReader.HasTexture)
            { 
                this.texture = texTask.Result;
            }
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
                if (currentTexture is null)
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
