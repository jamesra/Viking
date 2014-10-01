using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Viking.VolumeModel
{
    public class Global
    {
        /// <summary>
        /// Caches tiles, should call ReduceMemoryFootprint occasionally if memory use is a concern
        /// </summary>
        static public TileCache TileCache = new TileCache();

        static public Byte[] ReadToBuffer(Stream stream, long BytesToRead)
        {
            Byte[] streamBuffer = new Byte[BytesToRead];

            DateTime loopStart = DateTime.UtcNow;
            TimeSpan elapsed;
            long BytesRead = 0;
            do
            {
                BytesRead += stream.Read(streamBuffer, (int)BytesRead, (int)BytesToRead - (int)BytesRead);
                elapsed = new TimeSpan(DateTime.UtcNow.Ticks - loopStart.Ticks);
            }
            while (BytesRead < BytesToRead && elapsed.TotalSeconds < 60);

            return streamBuffer; 
        }
    }
}
