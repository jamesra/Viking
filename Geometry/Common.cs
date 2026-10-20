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
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek)
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin);
            }

            List<string> lines = [];

            using System.IO.StreamReader MosaicStream = new(stream);
            string line = MosaicStream.ReadLine();
            while (line != null)
            {
                lines.Add(line);
                line = MosaicStream.ReadLine();
            }

            return [.. lines];
        }

        /// <summary>
        /// Reads the whole stream as text (BOM-detected, UTF-8 by default) and splits it with <see cref="ToLines(string)"/>.
        /// Callers: <c>TilesToSectionMapping</c> (.mosaic files) and <c>TransformFactory</c> (.stos files).
        /// </summary>
        /// <param name="stream">Stream to read. A seekable stream is rewound to the start and read into one buffer
        /// sized by <see cref="System.IO.Stream.Length"/>; reading stops at end of stream even if that comes before
        /// <c>Length</c> (a file truncated while open), and only the bytes actually read are decoded.</param>
        /// <returns>The trimmed lines; empty input yields one empty line.</returns>
        public static async Task<string[]> ToLinesAsync(this System.IO.Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek)
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin);

                byte[] buffer = new byte[stream.Length];
                int bytesRead = 0;
                while (bytesRead < buffer.Length)
                {
                    int read = await stream.ReadAsync(buffer, bytesRead, buffer.Length - bytesRead);
                    if (read == 0)
                        break;
                    bytesRead += read;
                }

                using System.IO.StreamReader MosaicStream = new(new MemoryStream(buffer, 0, bytesRead, false));
                string streamData = await MosaicStream.ReadToEndAsync();
                return streamData.ToLines();
            }
            else
            {
                using System.IO.StreamReader MosaicStream = new(stream);
                string streamData = await MosaicStream.ReadToEndAsync();
                return streamData.ToLines();
            }
        }

        private static readonly Regex splitLinesRegex = new(@"\r\n|\r|\n", RegexOptions.Compiled);
        public static string[] ToLines(this string input)
        {
            if (input is null)
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
