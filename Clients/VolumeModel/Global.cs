using System;
using System.IO;

namespace Viking.VolumeModel
{
    public static class Global
    {
        /// <summary>
        /// This is used to force clients to recalculate cached transforms
        /// </summary>
        public static readonly DateTime OldestValidCachedTransform = new DateTime(year: 2022, month: 01, day: 01,
            hour: 00, minute: 00, second: 00, DateTimeKind.Utc);

        /// <summary>
        /// Caches tiles, should call ReduceMemoryFootprint occasionally if memory use is a concern
        /// </summary>
        static public TileCache TileCache = new TileCache();

        static public Byte[] ReadToBuffer(this Stream stream, long BytesToRead)
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
