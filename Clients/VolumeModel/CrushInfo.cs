#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Utils;

namespace Viking.VolumeModel
{
    /// <summary>
    /// The noise-crush parameters Nornir writes on a crushed filter's <c>Pyramid</c> and <c>Tileset</c> elements in VikingXML.
    /// A crushed tile stores a code per pixel instead of a contrast-stretched intensity. Every tile and level of a section shares
    /// the same quanta, so this is read once per channel. The PNG is normalized, so a code is the sample times the storage maximum
    /// (255 for 8-bit PNGs), not times <c>2^Bits - 1</c>.
    /// </summary>
    public sealed class CrushInfo
    {
        /// <summary>Counts per code (the quanta size).</summary>
        public int Step { get; }

        /// <summary>Counts below the first code.</summary>
        public int Offset { get; }

        /// <summary>Bits of the code range; the largest valid code is 2^Bits - 1.</summary>
        public int Bits { get; }

        /// <summary>Display range in codes from Nornir's code histogram, or null when the export did not include one.</summary>
        public (double Min, double Max)? DisplayRange { get; }

        /// <summary>Pixels per code over the whole section at full resolution; null when the export did not include it.</summary>
        public IReadOnlyList<long>? CodeHistogram { get; }

        /// <summary>True when the tiles are 8-bit PNGs, the only storage Viking decodes.</summary>
        public bool IsSupported => Bits <= 8;

        /// <summary>The largest code a PNG sample can hold: 255 for the 8-bit PNGs Viking decodes.</summary>
        public const double StorageMaximum = 255.0;

        public CrushInfo(int step, int offset, int bits, (double Min, double Max)? displayRange, IReadOnlyList<long>? codeHistogram)
        {
            Step = step;
            Offset = offset;
            Bits = bits;
            DisplayRange = displayRange;
            CodeHistogram = codeHistogram;
        }

        /// <summary>
        /// Reads the Crush* attributes (and the optional <c>CodeHistogram</c> child) of a Pyramid or Tileset element.
        /// Returns null when the element is not marked crushed (no CrushVersion) or the attributes are unusable, so legacy
        /// output and a crushed filter's stretched tilesets keep drawing as they did.
        /// </summary>
        public static CrushInfo? FromElement(XElement element)
        {
            if (!element.TryGetAttributeCaseInsensitive("CrushVersion", out _))
                return null;

            if (!TryInt(element, "CrushStep", out int step) || step < 1 ||
                !TryInt(element, "CrushBits", out int bits) || bits < 1)
                return null;

            TryInt(element, "CrushOffset", out int offset);

            (double, double)? range = null;
            if (TryDouble(element, "CrushDisplayMin", out double min) && TryDouble(element, "CrushDisplayMax", out double max) && max > min)
                range = (min, max);

            long[]? histogram = null;
            XElement? histogramElement = element.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "CodeHistogram", StringComparison.OrdinalIgnoreCase));
            if (histogramElement is not null)
            {
                string[] parts = histogramElement.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                long[] counts = new long[parts.Length];
                bool ok = parts.Length > 0;
                for (int i = 0; ok && i < parts.Length; i++)
                    ok = long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out counts[i]);

                if (ok)
                    histogram = counts;
            }

            return new CrushInfo(step, offset, bits, range, histogram);
        }

        /// <summary>
        /// The display window in PNG sample units (0 to 1) to show the codes with: Nornir's range when it sent one, otherwise a
        /// range from the histogram, otherwise every code. Bin 0 is ignored when the histogram is used because the section edge puts
        /// millions of empty pixels there.
        /// </summary>
        public (float Min, float Max) DefaultWindow()
        {
            (double min, double max) = DisplayRange ?? HistogramRange() ?? (0, (1 << Math.Min(Bits, 16)) - 1);
            return ((float)(min / StorageMaximum), (float)(max / StorageMaximum));
        }

        /// <summary>The codes that hold <paramref name="lowFraction"/> and <paramref name="highFraction"/> of the pixels, skipping bin 0.</summary>
        private (double, double)? HistogramRange(double lowFraction = 0.0001, double highFraction = 0.9999)
        {
            if (CodeHistogram is null || CodeHistogram.Count < 2)
                return null;

            long total = 0;
            for (int i = 1; i < CodeHistogram.Count; i++)
                total += CodeHistogram[i];

            if (total <= 0)
                return null;

            double low = -1;
            double high = CodeHistogram.Count - 1;
            long running = 0;
            for (int i = 1; i < CodeHistogram.Count; i++)
            {
                running += CodeHistogram[i];
                if (low < 0 && running >= total * lowFraction)
                    low = i;

                if (running >= total * highFraction)
                {
                    high = i;
                    break;
                }
            }

            return high > low ? (low, high) : null;
        }

        // Every Crush* attribute is optional, so these use the non-throwing lookup.
        private static bool TryInt(XElement element, string name, out int value)
        {
            value = 0;
            return element.TryGetAttributeCaseInsensitive(name, out XAttribute attribute) &&
                   int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryDouble(XElement element, string name, out double value)
        {
            value = 0;
            return element.TryGetAttributeCaseInsensitive(name, out XAttribute attribute) &&
                   double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
