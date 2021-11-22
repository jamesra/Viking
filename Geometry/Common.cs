using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Geometry
{
    public static class StreamUtil
    {
        public static async Task<string[]> ToLinesAsync(this System.IO.Stream stream)
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
                return streamData.ToLines();
            }
        }

        public static string[] ToLines(this string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
               
            var lines = Regex.Split(input, "\r\n|\r|\n");
             
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }

            return lines; 
        }
    }
}
