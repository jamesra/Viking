using System;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Unique identifier for a tile: section number, transform, channel, downsample, and texture name.
    /// </summary>
    public readonly struct TileUniqueKey : IEquatable<TileUniqueKey>, IComparable<TileUniqueKey>
    {
        public readonly int Section;
        public readonly string Transform;
        public readonly string Channel;
        public readonly int Downsample;
        public readonly string TextureName;
        private readonly int _hashKey;

        public TileUniqueKey(int section, string transform, string channel, int downsample, string textureName)
        {
            Section = section;
            Transform = transform ?? string.Empty;
            Channel = channel ?? string.Empty;
            Downsample = downsample;
            TextureName = textureName ?? string.Empty;
            _hashKey = ComputeHashCode(Section, Downsample, Transform, Channel, TextureName);
        }

        private static int ComputeHashCode(int section, int downsample, string transform, string channel, string textureName)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + section;
                hash = hash * 31 + downsample;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(transform ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(channel ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(textureName ?? string.Empty);
                return hash;
            }
        }

        public static TileUniqueKey Create(int section, string transform, string channel, int downsample, string textureName) =>
            new(section, transform ?? string.Empty, channel ?? string.Empty, downsample, textureName ?? string.Empty);

        public override int GetHashCode() => _hashKey;

        public override bool Equals(object obj) =>
            obj is TileUniqueKey other && Equals(other);

        public bool Equals(TileUniqueKey other) =>
            Section == other.Section &&
            Downsample == other.Downsample &&
            string.Equals(Transform, other.Transform, StringComparison.Ordinal) &&
            string.Equals(Channel, other.Channel, StringComparison.Ordinal) &&
            string.Equals(TextureName, other.TextureName, StringComparison.Ordinal);

        public static bool operator ==(TileUniqueKey left, TileUniqueKey right) => left.Equals(right);
        public static bool operator !=(TileUniqueKey left, TileUniqueKey right) => !left.Equals(right);

        public int CompareTo(TileUniqueKey other)
        {
            int cmp = Section.CompareTo(other.Section);
            if (cmp != 0) return cmp;

            cmp = Downsample.CompareTo(other.Downsample);
            if (cmp != 0) return cmp;

            cmp = string.Compare(Transform, other.Transform, StringComparison.Ordinal);
            if (cmp != 0) return cmp;

            cmp = string.Compare(Channel, other.Channel, StringComparison.Ordinal);
            if (cmp != 0) return cmp;

            return string.Compare(TextureName, other.TextureName, StringComparison.Ordinal);
        }

        public override string ToString() =>
            $"S: {Section:D04} T: {Transform} C: {Channel} DS: {Downsample:D03} T: {TextureName}";
    }
}
