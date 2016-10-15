using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public static class StreamUtil
    {
        public static string[] StreamToLines(System.IO.Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            using (System.IO.StreamReader MosaicStream = new System.IO.StreamReader(stream))
            {
                List<string> Lines;
                if (stream.CanSeek)
                {
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    Lines = new List<string>((int)(stream.Length / 80));
                }
                else
                {
                    Lines = new List<string>(100);
                }
                
                while (!MosaicStream.EndOfStream)
                {
                    Lines.Add(MosaicStream.ReadLine());
                }
                 
                return Lines.ToArray(); 
            }
        }
    }
}
