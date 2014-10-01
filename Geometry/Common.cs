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

            System.IO.StreamReader MosaicStream = new System.IO.StreamReader(stream);
            if (stream.CanSeek)
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin); 
            }

            List<string> Lines = new List<string>();
            while (!MosaicStream.EndOfStream)
            {
                string line = MosaicStream.ReadLine();
                Lines.Add(line);
            }

            MosaicStream.Close();

            return Lines.ToArray(); 
        }
    }
}
