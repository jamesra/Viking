using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Geometry
{
    public static class StreamUtil
    {
        public static async Task<string[]> StreamToLines(System.IO.Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek)
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin);
            }

            using (System.IO.StreamReader MosaicStream = new System.IO.StreamReader(stream))
            {
                string streamData = await MosaicStream.ReadToEndAsync().ConfigureAwait(false);

                var lines = Regex.Split(streamData, "\r\n|\r|\n");
                 
                //List<string> output = new List<string>(lines.Length);

                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = lines[i].Trim();
                }

                return lines;  
            }
        }
    }
}
