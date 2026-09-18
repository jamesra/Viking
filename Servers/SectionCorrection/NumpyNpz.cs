using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// Minimal NumPy .npy / .npz writer and reader (C-order, little-endian).
    /// </summary>
    public static class NumpyNpz
    {
        static readonly byte[] Magic = [0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y'];

        public static void Write(string path, IReadOnlyDictionary<string, NumpyArray> arrays)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using FileStream fs = File.Create(path);
            Write(fs, arrays);
        }

        public static void Write(Stream stream, IReadOnlyDictionary<string, NumpyArray> arrays)
        {
            using ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true);
            foreach (KeyValuePair<string, NumpyArray> kv in arrays)
            {
                ZipArchiveEntry entry = zip.CreateEntry(kv.Key + ".npy", CompressionLevel.Optimal);
                using Stream es = entry.Open();
                WriteNpy(es, kv.Value);
            }
        }

        public static Dictionary<string, NumpyArray> Read(string path)
        {
            using FileStream fs = File.OpenRead(path);
            return Read(fs);
        }

        public static Dictionary<string, NumpyArray> Read(Stream stream)
        {
            Dictionary<string, NumpyArray> result = new(StringComparer.Ordinal);
            using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string name = entry.FullName;
                if (!name.EndsWith(".npy", StringComparison.OrdinalIgnoreCase))
                    continue;
                string key = name.Substring(0, name.Length - 4);
                using Stream es = entry.Open();
                using MemoryStream ms = new();
                es.CopyTo(ms);
                ms.Position = 0;
                result[key] = ReadNpy(ms);
            }

            return result;
        }

        static void WriteNpy(Stream stream, NumpyArray array)
        {
            string descr = array.Descr;
            string shape = FormatShape(array.Shape);
            string headerDict = "{'descr': '" + descr + "', 'fortran_order': False, 'shape': " + shape + ", }";
            byte[] headerUtf = Encoding.ASCII.GetBytes(headerDict);

            // magic(6) + version(2) + hdrlen(2) + header, padded so the data offset is a multiple of 64.
            const int prefix = 10;
            int pad = 64 - ((prefix + headerUtf.Length + 1) % 64);
            if (pad == 64)
                pad = 0;
            byte[] header = new byte[headerUtf.Length + pad + 1];
            Buffer.BlockCopy(headerUtf, 0, header, 0, headerUtf.Length);
            for (int i = headerUtf.Length; i < header.Length - 1; i++)
                header[i] = (byte)' ';
            header[header.Length - 1] = (byte)'\n';

            stream.Write(Magic, 0, Magic.Length);
            stream.WriteByte(0x01);
            stream.WriteByte(0x00);
            ushort headerLen = (ushort)header.Length;
            stream.WriteByte((byte)(headerLen & 0xFF));
            stream.WriteByte((byte)(headerLen >> 8));
            stream.Write(header, 0, header.Length);
            stream.Write(array.Data, 0, array.Data.Length);
        }

        static NumpyArray ReadNpy(Stream stream)
        {
            byte[] magic = new byte[6];
            ReadExact(stream, magic, 6);
            int major = stream.ReadByte();
            int minor = stream.ReadByte();
            if (major < 0)
                throw new InvalidDataException("Truncated npy header.");

            int headerLen;
            if (major == 1)
            {
                byte[] hl = new byte[2];
                ReadExact(stream, hl, 2);
                headerLen = hl[0] | (hl[1] << 8);
            }
            else
            {
                byte[] hl = new byte[4];
                ReadExact(stream, hl, 4);
                headerLen = hl[0] | (hl[1] << 8) | (hl[2] << 16) | (hl[3] << 24);
            }

            byte[] headerBytes = new byte[headerLen];
            ReadExact(stream, headerBytes, headerLen);
            string header = Encoding.ASCII.GetString(headerBytes);
            string descr = ParseQuoted(header, "'descr':");
            int[] shape = ParseShape(header);
            bool fortran = header.IndexOf("'fortran_order': True", StringComparison.Ordinal) >= 0;
            if (fortran)
                throw new NotSupportedException("Fortran-order npy arrays are not supported.");

            int remaining;
            if (stream is MemoryStream ms)
                remaining = (int)(ms.Length - ms.Position);
            else
            {
                using MemoryStream rest = new();
                stream.CopyTo(rest);
                remaining = (int)rest.Length;
                byte[] dataCopy = rest.ToArray();
                return new NumpyArray(descr, shape, dataCopy);
            }

            byte[] data = new byte[remaining];
            ReadExact(stream, data, remaining);
            return new NumpyArray(descr, shape, data);
        }

        static string FormatShape(int[] shape)
        {
            if (shape is null || shape.Length == 0)
                return "()";
            if (shape.Length == 1)
                return "(" + shape[0].ToString(CultureInfo.InvariantCulture) + ",)";
            StringBuilder sb = new();
            sb.Append('(');
            for (int i = 0; i < shape.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(shape[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(')');
            return sb.ToString();
        }

        static string ParseQuoted(string header, string key)
        {
            int i = header.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
                throw new InvalidDataException("npy header missing " + key);
            int q1 = header.IndexOf('\'', i + key.Length);
            int q2 = header.IndexOf('\'', q1 + 1);
            return header.Substring(q1 + 1, q2 - q1 - 1);
        }

        static int[] ParseShape(string header)
        {
            int i = header.IndexOf("'shape':", StringComparison.Ordinal);
            int open = header.IndexOf('(', i);
            int close = header.IndexOf(')', open);
            string inner = header.Substring(open + 1, close - open - 1).Trim();
            if (inner.Length == 0)
                return [];
            string[] parts = inner.Split([','], StringSplitOptions.RemoveEmptyEntries);
            int[] shape = new int[parts.Length];
            for (int p = 0; p < parts.Length; p++)
                shape[p] = int.Parse(parts[p].Trim(), CultureInfo.InvariantCulture);
            return shape;
        }

        static void ReadExact(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int n = stream.Read(buffer, offset, count - offset);
                if (n <= 0)
                    throw new EndOfStreamException();
                offset += n;
            }
        }
    }

    public sealed class NumpyArray
    {
        public NumpyArray(string descr, int[] shape, byte[] data)
        {
            Descr = descr;
            Shape = shape ?? [];
            Data = data ?? [];
        }

        public string Descr { get; }
        public int[] Shape { get; }
        public byte[] Data { get; }

        public static NumpyArray FromFloat32(float[] values, params int[] shape)
            => new("<f4", shape, ToBytes(values, sizeof(float), BitConverter.GetBytes));

        public static NumpyArray FromFloat64(double[] values, params int[] shape)
        {
            byte[] data = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian)
                throw new NotSupportedException("Big-endian hosts are not supported for npz.");
            return new NumpyArray("<f8", shape, data);
        }

        public static NumpyArray FromInt32(int[] values, params int[] shape)
        {
            byte[] data = new byte[values.Length * sizeof(int)];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian)
                throw new NotSupportedException("Big-endian hosts are not supported for npz.");
            return new NumpyArray("<i4", shape, data);
        }

        public static NumpyArray FromInt64(long[] values, params int[] shape)
        {
            byte[] data = new byte[values.Length * sizeof(long)];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian)
                throw new NotSupportedException("Big-endian hosts are not supported for npz.");
            return new NumpyArray("<i8", shape, data);
        }

        public static NumpyArray FromBool(bool[] values, params int[] shape)
        {
            byte[] data = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
                data[i] = values[i] ? (byte)1 : (byte)0;
            return new NumpyArray("|b1", shape, data);
        }

        public static NumpyArray ScalarFloat64(double value) => FromFloat64([value]);

        public static NumpyArray ScalarInt64(long value) => FromInt64([value]);

        public float[] ToFloat32()
        {
            EnsureDescr("<f4");
            float[] values = new float[Data.Length / 4];
            Buffer.BlockCopy(Data, 0, values, 0, Data.Length);
            return values;
        }

        public double[] ToFloat64()
        {
            EnsureDescr("<f8");
            double[] values = new double[Data.Length / 8];
            Buffer.BlockCopy(Data, 0, values, 0, Data.Length);
            return values;
        }

        public int[] ToInt32()
        {
            EnsureDescr("<i4");
            int[] values = new int[Data.Length / 4];
            Buffer.BlockCopy(Data, 0, values, 0, Data.Length);
            return values;
        }

        public long[] ToInt64()
        {
            EnsureDescr("<i8");
            long[] values = new long[Data.Length / 8];
            Buffer.BlockCopy(Data, 0, values, 0, Data.Length);
            return values;
        }

        public bool[] ToBool()
        {
            bool[] values = new bool[Data.Length];
            for (int i = 0; i < Data.Length; i++)
                values[i] = Data[i] != 0;
            return values;
        }

        void EnsureDescr(string expected)
        {
            if (!string.Equals(Descr, expected, StringComparison.Ordinal))
                throw new InvalidDataException($"Expected descr {expected}, got {Descr}.");
        }

        static byte[] ToBytes(float[] values, int _, Func<float, byte[]> unused)
        {
            byte[] data = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian)
                throw new NotSupportedException("Big-endian hosts are not supported for npz.");
            return data;
        }
    }
}
