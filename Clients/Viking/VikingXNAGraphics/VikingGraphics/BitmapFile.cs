using System;
using System.Collections.Generic;
using System.IO; 
using System.Text;
using System.Drawing; 
using System.Drawing.Imaging;
using System.Diagnostics; 

namespace VikingXNA
{
    /// <summary>
    /// THis is an interface for creating and writing BMP files in small chunks.  It is used to aggregate many screenshots into a single image
    /// without keeping all the screenshots in memory. It writes 16bpp bitmaps
    /// </summary>
    public class BitmapFile
    {
        public readonly Int32 Width;
        readonly Int32 PaddedWidth; 
        public readonly Int32 Height;
        public readonly Int32 BPP;
        public readonly Int32 BytesPerPixel; 

        private FileStream _FileStream;
        private UInt32 _PixelDataOffset; 

        public BitmapFile(string Filename, Int32 width, Int32 height, Int32 bpp)
        {
            this.Width = width;
            this.Height = height;
            this.BPP = bpp;
            this.BytesPerPixel = bpp / 8;

            Int32 Pad = width % 4 == 0 ? 0 : 4 - (width % 4);
            this.PaddedWidth = width + Pad;

            _FileStream = System.IO.File.Create(Filename);

            WriteBMPHeader();
        }

        public void Close()
        {
            _FileStream.Close();
            _FileStream = null; 
        }
        
        private void WriteBMPHeader()
        {
            MemoryStream stream = new MemoryStream(0x40);
            BinaryWriter binaryWriter = new BinaryWriter(stream);

            Boolean EightBitGreyscale = BytesPerPixel == 1;

            UInt32 PaletteOffset = (UInt32)(256 * 4);

            UInt32 HeaderSize = 0x36; 

            //Write "BM"
            binaryWriter.Write((Int16)0x4D42);

            UInt32 filesize = (UInt32)(PaddedWidth * Height * BytesPerPixel) + HeaderSize + PaletteOffset;
            _FileStream.SetLength(filesize);

            binaryWriter.Write(filesize);

            Int16 Reserved = 0;
            binaryWriter.Write(Reserved);
            binaryWriter.Write(Reserved);

            //Offset to image data
            _PixelDataOffset = HeaderSize + PaletteOffset;
            binaryWriter.Write((UInt32)_PixelDataOffset);

            //Size of extended header
            binaryWriter.Write((Int32)0x28);

            binaryWriter.Write(Width);
            binaryWriter.Write(-Height);

            Int16 NumColorPlanes = 1;
            binaryWriter.Write(NumColorPlanes);

            binaryWriter.Write((Int16)BPP);

            Int32 Compression = 0;
            binaryWriter.Write(Compression);

            UInt32 Size = (UInt32)(Width * Height * BytesPerPixel);
            binaryWriter.Write(Size);

            UInt32 PixelsPerMeter = 2835;
            binaryWriter.Write(PixelsPerMeter);
            binaryWriter.Write(PixelsPerMeter);

            UInt32 numColors = EightBitGreyscale ? (UInt32)256 : (UInt32)0;
            UInt32 numImportantColors = EightBitGreyscale ? (UInt32)256 : (UInt32)0;
            
            binaryWriter.Write(numColors);
            binaryWriter.Write(numImportantColors);

            /* Write a greyscale palette if we're writing index files*/
            if (EightBitGreyscale)
            {
                for (int i = 0; i < 256; i++)
                {
                    binaryWriter.Write((Byte)i);
                    binaryWriter.Write((Byte)i);
                    binaryWriter.Write((Byte)i);
                    binaryWriter.Write((Byte)0);
                }
            }
            

            _FileStream.Write(stream.ToArray(), 0, (int)stream.Length);

            binaryWriter.Close();
            stream.Close(); 
        }

        /// <summary>
        /// Returns the offset to a pixel at the specific coordinates
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        private long Offset(UInt32 X, UInt32 Y)
        {
            long offset = (Y * this.PaddedWidth) + X;
            offset = offset * BytesPerPixel;
            offset += this._PixelDataOffset;
            
            return offset; 
        }
        
        /// <summary>
        /// Write the Byte[] to the ROI of the target
        /// </summary>
        /// <param name="Target"></param>
        /// <param name="ROI"></param>
        /// <param name="Data"></param>
        public void WriteRectangle(Rectangle ROI, Byte[] Data)
        {
            /*Sanity checks*/
            if (ROI.Width > Width)
                throw new ArgumentOutOfRangeException("ROI", "BitmapFile::WriteRectangle, Width exceeds bitmap dimensions"); 
            if (ROI.Height > Height)
                throw new ArgumentOutOfRangeException("ROI", "BitmapFile::WriteRectangle, Height exceeds bitmap dimensions"); 
            if (ROI.X < 0 || ROI.X > Width)
                throw new ArgumentOutOfRangeException("ROI", "BitmapFile::WriteRectangle, X is outside bitmap bounds");
            if (ROI.Y < 0 || ROI.Y > Height)
                throw new ArgumentOutOfRangeException("ROI", "BitmapFile::WriteRectangle, Y is outside bitmap bounds");
            if (ROI.X + ROI.Width > Width)
                throw new ArgumentOutOfRangeException("ROI", "BitmapFile::WriteRectangle, X+Width is outside bitmap bounds");
            if (ROI.Y + ROI.Height > Height)
                throw new ArgumentOutOfRangeException("ROI", "BitmapFile::WriteRectangle, Y+Height is outside bitmap bounds");
            if (ROI.Width * ROI.Height * this.BytesPerPixel != Data.Length)
                throw new ArgumentException("BitmapFile::WriteRectangle, Data[] size does not match ROI"); 

            //Write each scanline of the BMP
            for (uint y = (uint)ROI.Y; y < (uint)ROI.Y + (uint)ROI.Height; y++)
            {
                //Seek to the start of the scanline
                long fileOffset = Offset((uint)ROI.X, y);

                //Find the Data[] offset
                long dataOffset = ((y - ROI.Y) * ROI.Width) * this.BytesPerPixel; 

                _FileStream.Seek(fileOffset, SeekOrigin.Begin); 

                //Write scanline to the Bitmap
                _FileStream.Write(Data, (int)dataOffset, ROI.Width * BytesPerPixel); 
            }

            _FileStream.Flush(); 
        }
    }
}
