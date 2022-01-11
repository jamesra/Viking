using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Geometry
{
    public static class StreamUtil
    {
        public static string[] ToLines(this System.IO.Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek)
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin);
            }
             
            List<string> lines = new List<string>(); 

            using (System.IO.StreamReader MosaicStream = new System.IO.StreamReader(stream))
            {
                string line = MosaicStream.ReadLine();
                while (line != null)
                {
                    lines.Add(line);
                    line = MosaicStream.ReadLine();
                } 

                return lines.ToArray();
            }
        }

        public static async Task<string[]> ToLinesAsync(this System.IO.Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek)
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin);

                byte[] buffer = new byte[stream.Length];
                int bytesRead = await stream.ReadAsync(buffer, 0, (int)stream.Length);
                while (bytesRead < stream.Length)
                {
                    bytesRead += await stream.ReadAsync(buffer, bytesRead, (int)stream.Length - bytesRead);
                }

                using (System.IO.StreamReader MosaicStream = new System.IO.StreamReader(new MemoryStream(buffer, false)))
                {
                    string streamData = await MosaicStream.ReadToEndAsync();
                    return streamData.ToLines();
                }
            }
            else
            {
                using (System.IO.StreamReader MosaicStream = new System.IO.StreamReader(stream))
                {
                    string streamData = await MosaicStream.ReadToEndAsync();
                    return streamData.ToLines();
                }
            } 
        }

        private static Regex splitLinesRegex = new Regex(@"\r\n|\r|\n", RegexOptions.Compiled);
        public static string[] ToLines(this string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var lines = splitLinesRegex.Split(input);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }

            return lines; 
        }
    }
}
