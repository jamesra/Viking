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
    /// What a user chose to show a crushed channel with: the codes shown from black to white and a gamma. A gamma above 1 lifts
    /// the dark and middle tones, 1 leaves the window linear.
    /// </summary>
    internal readonly record struct CrushContrastSetting(double Min, double Max, double Gamma = 1.0)
    {
        public const double MinGamma = 0.25;
        public const double MaxGamma = 4.0;

        public static double ClampGamma(double gamma) => double.IsNaN(gamma) ? 1.0 : Math.Min(Math.Max(gamma, MinGamma), MaxGamma);
    }

    /// <summary>
    /// The contrast settings users chose for crushed channels, kept on this computer, per volume, section and channel. Nothing is
    /// sent to a server and the volume's VikingXML is not changed.
    /// </summary>
    internal sealed class CrushContrastStore
    {
        private readonly object _lock = new();
        private readonly string _path;
        private Dictionary<(string Volume, int Section, string Channel), CrushContrastSetting>? _settings;

        public static string DefaultPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viking", "CrushContrast.xml");

        public static CrushContrastStore Default { get; } = new(DefaultPath);

        public CrushContrastStore(string path) => _path = path;

        /// <summary>The setting the user chose, or null when the section uses the volume's display range.</summary>
        public CrushContrastSetting? Get(string? volume, int section, string channel)
        {
            lock (_lock)
            {
                return Settings().TryGetValue(Key(volume, section, channel), out var setting) ? setting : null;
            }
        }

        /// <summary>Remembers a setting and saves it. A window with no width is ignored; a gamma outside the range is clamped.</summary>
        public void Set(string? volume, int section, string channel, double min, double max, double gamma = 1.0)
        {
            if (!(max > min))
                return;

            lock (_lock)
            {
                Settings()[Key(volume, section, channel)] = new CrushContrastSetting(min, max, CrushContrastSetting.ClampGamma(gamma));
                Save();
            }
        }

        /// <summary>Forgets the user's setting so the section returns to the volume's display range.</summary>
        public void Clear(string? volume, int section, string channel)
        {
            lock (_lock)
            {
                if (Settings().Remove(Key(volume, section, channel)))
                    Save();
            }
        }

        private static (string, int, string) Key(string? volume, int section, string channel) =>
            ((volume ?? "").ToUpperInvariant(), section, channel.ToUpperInvariant());

        private Dictionary<(string Volume, int Section, string Channel), CrushContrastSetting> Settings()
        {
            if (_settings is not null)
                return _settings;

            _settings = [];
            try
            {
                if (!File.Exists(_path))
                    return _settings;

                foreach (XElement e in XDocument.Load(_path).Root?.Elements("Window") ?? Enumerable.Empty<XElement>())
                {
                    if (int.TryParse((string?)e.Attribute("section"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int section) &&
                        double.TryParse((string?)e.Attribute("min"), NumberStyles.Float, CultureInfo.InvariantCulture, out double min) &&
                        double.TryParse((string?)e.Attribute("max"), NumberStyles.Float, CultureInfo.InvariantCulture, out double max) &&
                        max > min)
                    {
                        // Files written before gamma existed have no gamma attribute and mean a linear window.
                        double gamma = double.TryParse((string?)e.Attribute("gamma"), NumberStyles.Float, CultureInfo.InvariantCulture, out double g) ? g : 1.0;
                        _settings[Key((string?)e.Attribute("volume"), section, (string?)e.Attribute("channel") ?? "")] =
                            new CrushContrastSetting(min, max, CrushContrastSetting.ClampGamma(gamma));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // An unreadable file means no saved settings; the next Set writes a fresh one.
                Trace.WriteLine($"Could not read crush contrast settings from {_path}: {ex.Message}", "Crush");
                _settings = [];
            }

            return _settings;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                XDocument doc = new(new XElement("CrushContrast",
                    Settings().OrderBy(w => w.Key.Volume).ThenBy(w => w.Key.Section).ThenBy(w => w.Key.Channel).Select(w =>
                        new XElement("Window",
                            new XAttribute("volume", w.Key.Volume),
                            new XAttribute("section", w.Key.Section.ToString(CultureInfo.InvariantCulture)),
                            new XAttribute("channel", w.Key.Channel),
                            new XAttribute("min", w.Value.Min.ToString("R", CultureInfo.InvariantCulture)),
                            new XAttribute("max", w.Value.Max.ToString("R", CultureInfo.InvariantCulture)),
                            new XAttribute("gamma", w.Value.Gamma.ToString("R", CultureInfo.InvariantCulture))))));

                string temp = _path + ".tmp";
                doc.Save(temp);
                if (File.Exists(_path))
                    File.Delete(_path);
                File.Move(temp, _path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Losing the file only loses the remembered settings, so the viewer keeps working.
                Trace.WriteLine($"Could not save crush contrast settings to {_path}: {ex.Message}", "Crush");
            }
        }
    }
}
