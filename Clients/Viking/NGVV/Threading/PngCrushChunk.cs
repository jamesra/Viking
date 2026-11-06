#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Viking
{
    /// <summary>
    /// Reads the <c>nornir_crush</c> tEXt chunk Nornir writes in crushed PNG tiles. Only the tile's <c>gain</c> is needed: the
    /// section's step and offset come from the VikingXML.
    /// </summary>
    internal static class PngCrushChunk
    {
        private const string Keyword = "nornir_crush";
        private const int MaxTextChunkBytes = 64 * 1024;
        private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly Regex GainPattern = new("\"gain\"\\s*:\\s*(?<value>[-+0-9.eE]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// The gain recorded in the PNG's <c>nornir_crush</c> chunk, or null when the stream is not a PNG, has no such chunk or the
        /// chunk has no gain. Leaves the stream where it started so the image can be decoded afterwards; a stream that cannot seek is
        /// not read.
        /// </summary>
        public static double? TryReadGain(Stream stream)
        {
            if (!stream.CanSeek)
                return null;

            long start = stream.Position;
            try
            {
                return ReadGain(stream);
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException or ObjectDisposedException)
            {
                return null;
            }
            finally
            {
                stream.Position = start;
            }
        }

        private static double? ReadGain(Stream stream)
        {
            byte[] header = new byte[8];
            if (!ReadFully(stream, header, 8))
                return null;

            for (int i = 0; i < Signature.Length; i++)
            {
                if (header[i] != Signature[i])
                    return null;
            }

            // The chunk sits after IHDR, before the pixel data, so stop at the first IDAT.
            while (ReadFully(stream, header, 8))
            {
                long length = ((long)header[0] << 24) | ((long)header[1] << 16) | ((long)header[2] << 8) | header[3];
                string type = Encoding.ASCII.GetString(header, 4, 4);
                if (type is "IDAT" or "IEND")
                    return null;

                if (type == "tEXt" && length <= MaxTextChunkBytes)
                {
                    byte[] data = new byte[length];
                    if (!ReadFully(stream, data, (int)length))
                        return null;

                    stream.Seek(4, SeekOrigin.Current);
                    int separator = Array.IndexOf(data, (byte)0);
                    if (separator == Keyword.Length && Encoding.ASCII.GetString(data, 0, separator) == Keyword)
                        return ParseGain(Encoding.ASCII.GetString(data, separator + 1, data.Length - separator - 1));
                }
                else
                {
                    stream.Seek(length + 4, SeekOrigin.Current);
                }
            }

            return null;
        }

        /// <summary>The <c>gain</c> number in the chunk's JSON text, or null when it is missing or not a number.</summary>
        internal static double? ParseGain(string json)
        {
            Match match = GainPattern.Match(json);
            return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double gain)
                ? gain
                : null;
        }

        private static bool ReadFully(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                    return false;

                read += n;
            }

            return true;
        }
    }
}
