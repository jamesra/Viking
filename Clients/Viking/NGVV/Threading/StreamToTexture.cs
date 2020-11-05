namespace Viking.Threading
{
    /*
    class StreamToTexture : IDisposable
    {
        /// <summary>
        /// Stream of texture data in a format processable by the Bitmap class
        /// </summary>
        
        private Stream textureStream;

        private Texture2D _texture;

        private readonly bool CreateMipMaps = true;

        private readonly GraphicsDevice graphicsdevice; 

        public Func<GraphicsDevice, Stream, bool, Texture2D> TextureFromStreamDelegate;

        public Texture2D texture
        {
            get { return _texture; }
            set { _texture = value; }
        }


        public StreamToTexture(GraphicsDevice graphicsDevice, Stream stream, bool createMipMaps)
        {
            this.graphicsdevice = graphicsDevice; 
            this.textureStream = stream;
            this.CreateMipMaps = createMipMaps;

            this.TextureFromStreamDelegate = this.TextureFromStream; 
        }


        private void InvokeTextureFromStream(GraphicsDevice graphicsDevice, Stream stream, bool mipmap, AsyncCallback EndTextureFromStream)
        {
            StreamToTexture obj = new StreamToTexture(graphicsDevice, stream, mipmap);
            Func<GraphicsDevice, Stream, bool, Texture2D> action = obj.TextureFromStream;
            
            IAsyncResult result = action.BeginInvoke(graphicsDevice, stream, mipmap, EndTextureFromStream, this);
        }
         
        public void Dispose(bool DisposeManagedObjects)
        {
            if (_texture != null)
            {
                _texture.Dispose();
                _texture = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); 
        }

        public Texture2D TextureFromStream()
        { 
            //We load greyscale images, XNA doesn't support loading greyscale by default, so run it through Bitmap instead
            int Width;
            int Height;
            BitmapData data = null;
            Byte[] rgbValues;
            int PixelSize;

            Trace.WriteLine("Unpacking texture from stream on thread ID: " + Thread.CurrentContext.ContextID.ToString());

            using (Bitmap image = new System.Drawing.Bitmap(textureStream))
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

                data = null;
            }

            if (rgbValues != null)
            {
                int WidthHeight = Width * Height;
                Byte[] pixelBytes = new Byte[WidthHeight];

                for (int iSourceByte = 0, iDestByte = 0; iDestByte < WidthHeight; iSourceByte += PixelSize)
                {
                    pixelBytes[iDestByte++] = rgbValues[iSourceByte];
                }

                if (graphicsdevice == null)
                    return null;

                if (graphicsdevice.IsDisposed)
                    return null;

                Texture2D tex = null;
                try
                {
                    if (graphicsdevice.GraphicsProfile == GraphicsProfile.Reach)
                    {
                        tex = new Texture2D(graphicsdevice, Width, Height, CreateMipMaps, SurfaceFormat.Color);
                        tex.SetData<int>(Array.ConvertAll<byte, int>(pixelBytes, new Converter<byte, int>((x) => (int)x << 24)));
                        //return Texture2D.FromStream(graphicsDevice, stream);
                    }
                    else
                    {
                        tex = new Texture2D(graphicsdevice, Width, Height, CreateMipMaps, SurfaceFormat.Alpha8);
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
            }

            return null;
        }
    } 
     */
}
