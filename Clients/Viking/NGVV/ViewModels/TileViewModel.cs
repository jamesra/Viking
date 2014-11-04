using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using Viking.UI;
using Viking.ViewModels; 
using Geometry;
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
        /// This should only be written via the texture member 
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D _texture;

        Microsoft.Xna.Framework.Graphics.Texture2D texture
        {
            get {
                if (_texture != null)
                {
                    if (_texture.IsDisposed)
                        _texture = null;
                }
                return _texture; 
            }
            set
            {
                if (value == _texture)
                    return;

                if (_texture != null)
                {
                    DisposeTextureThreadingObj disposeObj = new DisposeTextureThreadingObj(_texture);
                    ThreadPool.QueueUserWorkItem(disposeObj.ThreadPoolCallback);
                    //Global.RemoveTexture(_texture);  //Texture removed from global records within the thread
                }

                _texture = value;
            }
        }

        internal bool HasTexture
        {
            get { return this._texture != null; }
        }

        private TextureReader _TexReader;
        private TextureReader TexReader
        {
            get { return _TexReader; }
            set {
                lock (this)
                {
                    if (_TexReader != null && _TexReader != value)
                    {
                        _TexReader.Dispose();
                        _TexReader = null;
                    }

                    _TexReader = value;
                }
            }

        }

        public int TileID; 

        public readonly string TextureFileName; 
        public readonly string TextureCachedFileName; 

        private int MipMapLevels = 1;

        private Color TileColor;

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
            
            VertexPositionNormalTexture[] vertArray = new VertexPositionNormalTexture[Verticies.Length];

            for(int i = 0; i < Verticies.Length; i++)
            {
                GridVector3 pos = Verticies[i].Position;
                GridVector3 norm = Verticies[i].Normal; 
                GridVector2 tex = Verticies[i].Texture;

                vertArray[i] = new VertexPositionNormalTexture( new Vector3((float)pos.X,(float)pos.Y, (float)pos.Z),
                                                                new Vector3((float)norm.X,(float)norm.Y,(float)norm.Z),
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
            //Stop trying to load textures if we have a request out
            AbortRequest();

            lock (this)
            {
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

        }

        public void AbortRequest()
        {
            if (this.TexReader != null)
            {
                //Other methods may be counting on TexReader not being set to null, so take a lock
                lock (this)
                {
                    if (this.TexReader != null)
                    {
                        this.TexReader.AbortRequest();
                        this.TexReader = null;
                    }
                }
            }
        }

        public Texture2D GetTexture(GraphicsDevice graphicsDevice, bool AsynchTextureLoad)
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
                //Don't know how this could happen, but we should not have a texture if the server does not.  This indicates the code will leak resources
                Debug.Assert(TexReader == null);
                Debug.Assert(texture == null);
                return null;
            }

            //If we are trying to read a new texture, check if it is finished yet
            if (TexReader != null)
            {
                lock (this) //Don't let other threads change TexReader state
                {
                    if (TexReader != null) //In case someone freed TexReader before taking the lock
                    {
                        if (TexReader.FinishedReading)
                        {
                            TextureReader texReader = this.TexReader;

                            bool Entered = Monitor.TryEnter(texReader);
                            if (Entered)
                            {
                                //If reading the texture failed don't change our state. 
                                if (texReader.FinishedReading)
                                {
                                    if (texReader.HasTexture)
                                    {
                                        this.texture = texReader.GetTexture();
                                        this.TexReader = null;
                                    }
                                    else
                                    {
                                        //We should stop asking if the server just doesn't have it. 
                                        this.ServerTextureNotFound = texReader.TextureNotFound;
                                        this.TexReader.Dispose();
                                        this.TexReader = null; 
                                        return null;
                                    } 
                                }

                                Monitor.Exit(texReader);
                            }
                        }
                    }
                }
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
                //In this path we either have no texture, or a low-res texture.  Ask for a new one if we haven't already
                if (this.TexReader == null)
                {
                    //If the section is read over a network provide a cache path too
                    if (State.volume.IsLocal == false)
                    {
                        this.TexReader = new TextureReader(graphicsDevice,
                                                           new Uri(this.TextureFileName), 
                                                           this.TextureCachedFileName,
                                                           this.MipMapLevels);
                    }
                    else
                    {
                        this.TexReader = new TextureReader(graphicsDevice,
                                                            new Uri(this.TextureFileName),
                                                           this.MipMapLevels);
                    }

                    if (AsynchTextureLoad)
                    {
                        //TexReader.Go();

                        ThreadPool.QueueUserWorkItem(TexReader.ThreadPoolCallback);
                        //TexReader.ThreadPoolCallback(System.Threading.Thread.CurrentContext);
                    }
                    else
                    {
                        //I think reentrant paint calls make this lock useless
                        lock (this)
                        {
                            TextureReader texReader = this.TexReader;
                            this._TexReader = null;
                            texReader.ThreadPoolCallback(System.Threading.Thread.CurrentContext);

                            texReader.DoneEvent.WaitOne();

 //                           Debug.Assert(texReader.HasTexture);
                            if (texReader.HasTexture)
                            {
                                this.texture = texReader.GetTexture();
                            }
                            else
                            {
                                Trace.WriteLine("Synchronous texture reader does not have texture: " + TextureFileName);
                                
                                this.ServerTextureNotFound = texReader.TextureNotFound;
                            }

                            texReader.Dispose();
                            texReader = null;
                        }
                    }
                }
                //We have a pending request, but we've been asked to load the texture NOW
                else if (this.TexReader != null && AsynchTextureLoad == false)
                {
                    //I think reentrant paint calls make this lock useless
                    lock (this)
                    {
                        if (this.TexReader != null)
                        {
                            TextureReader texReader = this.TexReader;
                            this._TexReader = null; 
                            texReader.DoneEvent.WaitOne(); //Timeout after a minute?

                            Debug.Assert(texReader != null);
                            Debug.Assert(texReader.HasTexture);

                            if (texReader.HasTexture)
                            {
                                this.texture = texReader.GetTexture();
                            }

                            texReader.Dispose();
                            texReader = null; 
                        }
                        else
                        {
                            Debug.Assert(this.texture != null, "Someone nulled out our texture reader, but didn't leave a texture during Synch texture load");
                        }
                    }
                }

                return this.texture; // Can return null or a texture with a different downsample level if we have one
            }                 
        }

        /*
        public void FileReadComplete(IAsyncResult ar)
        {
            Debug.Assert(ar.IsCompleted, "Wasn't expecting Asynch read not to complete successfully");
            if (!ar.IsCompleted)
            {
                //ReadRequestInProgress = false;
                return;
            }

            object[] objs = ar.AsyncState as object[];
//            FileStream stream = objs[0] as FileStream;
            GraphicsDevice graphicsDevice = objs[1] as GraphicsDevice;
            byte[] fileBuffer = objs[2] as byte[];
            int Level = (int)objs[3];
            
            MemoryStream MemStream = null;
            try
            {
                MemStream = new MemoryStream(fileBuffer, false);

                Monitor.Enter(this);

                texture = Texture2D.FromStream(graphicsDevice, MemStream);
            }
            finally
            {
                if (MemStream != null)
                {
                    MemStream.Close();
                    MemStream.Dispose(); 
                    MemStream = null; 
                }
            }

            Trace.WriteLine("AsSynch load completed " + Level.ToString("D3") + " " + this.TextureFileName, "Tile");

            //ReadRequestInProgress = false;

            Monitor.Exit(this);
        }
         */

#if DEBUG
        private static bool NullGridWarningPrinted = false; 
#endif

        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.TileLayoutEffect effect, bool AsynchTextureLoad, bool UseColor)
        {
            if (TriangleIndicies == null)
            {
                #if DEBUG
                if(!NullGridWarningPrinted)
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

            Texture2D currentTexture = GetTexture(graphicsDevice, AsynchTextureLoad);

            //Do not draw if we don't have a texture
            if (currentTexture == null)
                return;

            lock (this)
            {
                currentTexture = GetTexture(graphicsDevice, AsynchTextureLoad);

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

            for (int i = 0; i < TriangleIndicies.Length; i+=3)
            {
                TrianglesAsLines.Add(TriangleIndicies[i]);
                TrianglesAsLines.Add(TriangleIndicies[i+1]);
                TrianglesAsLines.Add(TriangleIndicies[i+1]);
                TrianglesAsLines.Add(TriangleIndicies[i+2]);
                TrianglesAsLines.Add(TriangleIndicies[i + 2]);
                TrianglesAsLines.Add(TriangleIndicies[i]);
            }

            ibMesh = new IndexBuffer(graphicsDevice, typeof(int),  TrianglesAsLines.Count, BufferUsage.None);
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

        }

        public void DrawLabels(Viking.UI.Controls.SectionViewerControl _Parent)
        {
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
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); 
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing)
            {
                lock (this)
                {
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
            }
        }


        #endregion
    }
}
