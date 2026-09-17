using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jotunn.Common;

namespace Jotunn
{
    public sealed class BookmarkEntry
    {
        public string Name { get; set; }
        public int Section { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Downsample { get; set; }
    }

    /// <summary>
    /// JSON bookmarks per volume name under LocalApplicationData/Jotunn.
    /// </summary>
    public static class BookmarkStore
    {
        static string FilePath(string volumeName)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Jotunn");
            Directory.CreateDirectory(dir);
            string safe = string.IsNullOrWhiteSpace(volumeName) ? "volume" : volumeName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return Path.Combine(dir, safe + ".bookmarks.json");
        }

        public static List<BookmarkEntry> Load(string volumeName)
        {
            string path = FilePath(volumeName);
            if (!File.Exists(path))
                return [];
            try
            {
                return JsonSerializer.Deserialize<List<BookmarkEntry>>(File.ReadAllText(path)) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static void Save(string volumeName, List<BookmarkEntry> bookmarks)
        {
            File.WriteAllText(FilePath(volumeName), JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static BookmarkEntry FromVisibleRegion(string name, int section, VisibleRegionInfo region)
        {
            return new BookmarkEntry
            {
                Name = name,
                Section = section,
                X = region.Center.X,
                Y = region.Center.Y,
                Downsample = region.Downsample
            };
        }
    }
}
