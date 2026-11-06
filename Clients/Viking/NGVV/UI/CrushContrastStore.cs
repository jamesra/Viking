#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Viking.UI
{
    /// <summary>
    /// The contrast windows a user chose for crushed channels, kept on this computer. A window is the codes shown from black to
    /// white, per volume, section and channel. Nothing is sent to a server and the volume's VikingXML is not changed.
    /// </summary>
    internal sealed class CrushContrastStore
    {
        private readonly object _lock = new();
        private readonly string _path;
        private Dictionary<(string Volume, int Section, string Channel), (double Min, double Max)>? _windows;

        public static string DefaultPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viking", "CrushContrast.xml");

        public static CrushContrastStore Default { get; } = new(DefaultPath);

        public CrushContrastStore(string path) => _path = path;

        /// <summary>The window in codes the user chose, or null when the section uses the volume's display range.</summary>
        public (double Min, double Max)? Get(string? volume, int section, string channel)
        {
            lock (_lock)
            {
                return Windows().TryGetValue(Key(volume, section, channel), out var window) ? window : null;
            }
        }

        /// <summary>Remembers a window in codes and saves it. A window with no width is ignored.</summary>
        public void Set(string? volume, int section, string channel, double min, double max)
        {
            if (!(max > min))
                return;

            lock (_lock)
            {
                Windows()[Key(volume, section, channel)] = (min, max);
                Save();
            }
        }

        /// <summary>Forgets the user's window so the section returns to the volume's display range.</summary>
        public void Clear(string? volume, int section, string channel)
        {
            lock (_lock)
            {
                if (Windows().Remove(Key(volume, section, channel)))
                    Save();
            }
        }

        private static (string, int, string) Key(string? volume, int section, string channel) =>
            ((volume ?? "").ToUpperInvariant(), section, channel.ToUpperInvariant());

        private Dictionary<(string Volume, int Section, string Channel), (double Min, double Max)> Windows()
        {
            if (_windows is not null)
                return _windows;

            _windows = [];
            try
            {
                if (!File.Exists(_path))
                    return _windows;

                foreach (XElement e in XDocument.Load(_path).Root?.Elements("Window") ?? Enumerable.Empty<XElement>())
                {
                    if (int.TryParse((string?)e.Attribute("section"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int section) &&
                        double.TryParse((string?)e.Attribute("min"), NumberStyles.Float, CultureInfo.InvariantCulture, out double min) &&
                        double.TryParse((string?)e.Attribute("max"), NumberStyles.Float, CultureInfo.InvariantCulture, out double max) &&
                        max > min)
                    {
                        _windows[Key((string?)e.Attribute("volume"), section, (string?)e.Attribute("channel") ?? "")] = (min, max);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // An unreadable file means no saved windows; the next Set writes a fresh one.
                Trace.WriteLine($"Could not read crush contrast settings from {_path}: {ex.Message}", "Crush");
                _windows = [];
            }

            return _windows;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                XDocument doc = new(new XElement("CrushContrast",
                    Windows().OrderBy(w => w.Key.Volume).ThenBy(w => w.Key.Section).ThenBy(w => w.Key.Channel).Select(w =>
                        new XElement("Window",
                            new XAttribute("volume", w.Key.Volume),
                            new XAttribute("section", w.Key.Section.ToString(CultureInfo.InvariantCulture)),
                            new XAttribute("channel", w.Key.Channel),
                            new XAttribute("min", w.Value.Min.ToString("R", CultureInfo.InvariantCulture)),
                            new XAttribute("max", w.Value.Max.ToString("R", CultureInfo.InvariantCulture))))));

                string temp = _path + ".tmp";
                doc.Save(temp);
                if (File.Exists(_path))
                    File.Delete(_path);
                File.Move(temp, _path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Losing the file only loses the remembered windows, so the viewer keeps working.
                Trace.WriteLine($"Could not save crush contrast settings to {_path}: {ex.Message}", "Crush");
            }
        }
    }
}
