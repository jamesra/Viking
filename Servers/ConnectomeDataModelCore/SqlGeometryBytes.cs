using System;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// Minimal reader of SQL Server's native geometry serialization ([MS-SSCLRT] 2.1), for the shapes NetTopologySuite
    /// cannot read (CurvePolygon). Only the points array is read.
    /// </summary>
    /// <remarks>
    /// Layout: SRID (int32), version (byte, 1 or 2), serialization properties (byte), then either a single point
    /// (property P, 0x08) or a single line segment (property L, 0x10) at offset 6, or the point count (uint32) at offset 6
    /// followed by the points at offset 10. Points are little-endian (x, y) doubles.
    /// </remarks>
    public static class SqlGeometryBytes
    {
        const byte SinglePoint = 0x08;
        const byte SingleLineSegment = 0x10;

        /// <summary>Reads the first point of a serialized geometry.</summary>
        /// <returns>False when the bytes are too short, an unknown version, or hold no points.</returns>
        public static bool TryReadFirstPoint(byte[] bytes, out double x, out double y)
        {
            x = y = 0;
            if (bytes == null || bytes.Length < 6)
                return false;

            byte version = bytes[4];
            if (version != 1 && version != 2)
                return false;

            byte properties = bytes[5];
            int offset;
            if ((properties & (SinglePoint | SingleLineSegment)) != 0)
            {
                offset = 6;
            }
            else
            {
                if (bytes.Length < 10 || BitConverter.ToUInt32(bytes, 6) == 0)
                    return false;
                offset = 10;
            }

            if (bytes.Length < offset + 16)
                return false;

            x = BitConverter.ToDouble(bytes, offset);
            y = BitConverter.ToDouble(bytes, offset + 8);
            return true;
        }
    }
}
