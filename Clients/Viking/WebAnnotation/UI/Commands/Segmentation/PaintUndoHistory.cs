#nullable enable

using System;
using System.Collections.Generic;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The paint a <see cref="PaintCanvas"/> held before each completed stroke, newest last, so Ctrl+Z can step back one stroke at a time.
    /// </summary>
    /// <remarks>
    /// An entry is the canvas the stroke was painted on and a copy of its pixels from before the stroke. A canvas that has since grown is
    /// a different object (see <see cref="PaintCanvas.GrownToInclude"/>), so restoring an entry also brings back the canvas it belonged to.
    /// The history is bounded by entry count and by the bytes the copies hold; the oldest entries go first. Used on the UI thread only.
    /// </remarks>
    internal sealed class PaintUndoHistory(int maxEntries = PaintUndoHistory.DefaultMaxEntries, long maxBytes = PaintUndoHistory.DefaultMaxBytes)
    {
        public const int DefaultMaxEntries = 50;
        public const long DefaultMaxBytes = 128L * 1024 * 1024;

        private sealed record Entry(PaintCanvas Canvas, byte[] Pixels);

        private readonly LinkedList<Entry> entries = new();
        private long bytes;

        public int Count => entries.Count;

        public bool CanUndo => entries.Count > 0;

        /// <summary>A copy of the pixels of <paramref name="canvas"/> as they are now, to hand to <see cref="Push"/> if the stroke about to start changes anything.</summary>
        public static byte[] Snapshot(PaintCanvas canvas) => (byte[])canvas.Pixels.Clone();

        /// <summary>Records that <paramref name="canvas"/> held <paramref name="pixelsBefore"/> before the stroke that just completed.</summary>
        public void Push(PaintCanvas canvas, byte[] pixelsBefore)
        {
            entries.AddLast(new Entry(canvas, pixelsBefore));
            bytes += pixelsBefore.Length;
            while (entries.Count > 1 && (entries.Count > maxEntries || bytes > maxBytes))
            {
                bytes -= entries.First!.Value.Pixels.Length;
                entries.RemoveFirst();
            }
        }

        /// <summary>
        /// Takes back the newest stroke: its canvas gets the pixels it had before the stroke (the whole raster is reported dirty) and is
        /// returned. False when there is nothing to undo.
        /// </summary>
        public bool TryUndo(out PaintCanvas? canvas)
        {
            canvas = null;
            if (entries.Last is null)
                return false;

            Entry entry = entries.Last.Value;
            entries.RemoveLast();
            bytes -= entry.Pixels.Length;
            entry.Canvas.RestorePixels(entry.Pixels);
            canvas = entry.Canvas;
            return true;
        }

        public void Clear()
        {
            entries.Clear();
            bytes = 0;
        }
    }
}
