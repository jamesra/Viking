using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Viking.VolumeModel;

namespace Viking.UI.WPF.Models
{
    /// <summary>
    /// Serializable DTO for ChannelInfo with hex color string
    /// </summary>
    public class ChannelInfoDto
    {
        public string ChannelName { get; set; }
        public int SectionSource { get; set; }
        public int? FixedSectionNumber { get; set; }
        public string ColorHex { get; set; }
    }

    /// <summary>
    /// Represents the reference sections (above and below) and channel mappings for a specific section
    /// </summary>
    public class SectionReferences
    {
        public int? ReferenceAbove { get; set; }
        public int? ReferenceBelow { get; set; }
        public ChannelInfoDto[] Channels { get; set; }
    }

    /// <summary>
    /// Manages persistence of section reference settings for a specific volume.
    /// Settings are stored in a JSON file in the volume's local cache directory.
    /// </summary>
    public static class SectionReferenceSettings
    {
        private const string SettingsFileName = "section-references.json";

        /// <summary>
        /// Load section reference settings for a specific volume
        /// </summary>
        /// <param name="volumeLocalDir">The local directory path for the volume (Volume.Paths.LocalVolumeDir)</param>
        /// <returns>Dictionary mapping section number to its reference settings</returns>
        public static Dictionary<int, SectionReferences> LoadForVolume(string volumeLocalDir)
        {
            if (string.IsNullOrWhiteSpace(volumeLocalDir))
            {
                return new Dictionary<int, SectionReferences>();
            }

            string filePath = Path.Combine(volumeLocalDir, SettingsFileName);

            if (!File.Exists(filePath))
            {
                return new Dictionary<int, SectionReferences>();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<Dictionary<int, SectionReferences>>(json);
                return settings ?? new Dictionary<int, SectionReferences>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to load section reference settings from {filePath}: {ex.Message}");
                return new Dictionary<int, SectionReferences>();
            }
        }

        /// <summary>
        /// Save section reference settings for a specific volume
        /// </summary>
        /// <param name="volumeLocalDir">The local directory path for the volume (Volume.Paths.LocalVolumeDir)</param>
        /// <param name="settings">Dictionary mapping section number to its reference settings</param>
        public static void SaveForVolume(string volumeLocalDir, Dictionary<int, SectionReferences> settings)
        {
            if (string.IsNullOrWhiteSpace(volumeLocalDir))
            {
                return;
            }

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(volumeLocalDir))
                {
                    Directory.CreateDirectory(volumeLocalDir);
                }

                string filePath = Path.Combine(volumeLocalDir, SettingsFileName);

                // If settings dictionary is empty, delete the file
                if (settings == null || settings.Count == 0)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to save section reference settings to {volumeLocalDir}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the default reference section numbers for a given section
        /// </summary>
        /// <param name="currentSectionNumber">The current section number</param>
        /// <returns>Tuple of (defaultAbove, defaultBelow)</returns>
        public static (int defaultAbove, int defaultBelow) GetDefaultReferences(int currentSectionNumber)
        {
            return (currentSectionNumber + 1, currentSectionNumber - 1);
        }

        /// <summary>
        /// Check if the given references are different from defaults
        /// </summary>
        /// <param name="currentSectionNumber">The current section number</param>
        /// <param name="referenceAbove">The reference section above</param>
        /// <param name="referenceBelow">The reference section below</param>
        /// <returns>True if either reference differs from default</returns>
        public static bool IsNonDefault(int currentSectionNumber, int? referenceAbove, int? referenceBelow)
        {
            var (defaultAbove, defaultBelow) = GetDefaultReferences(currentSectionNumber);
            return referenceAbove != defaultAbove || referenceBelow != defaultBelow;
        }

        /// <summary>
        /// Convert ChannelInfo to ChannelInfoDto for serialization
        /// </summary>
        public static ChannelInfoDto ToDto(ChannelInfo channelInfo)
        {
            if (channelInfo == null)
            {
                return null;
            }

            return new ChannelInfoDto
            {
                ChannelName = channelInfo.ChannelName,
                SectionSource = (int)channelInfo.SectionSource,
                FixedSectionNumber = channelInfo.FixedSectionNumber,
                ColorHex = $"#{channelInfo.Color.A:X2}{channelInfo.Color.R:X2}{channelInfo.Color.G:X2}{channelInfo.Color.B:X2}"
            };
        }

        /// <summary>
        /// Convert ChannelInfoDto back to ChannelInfo
        /// </summary>
        public static ChannelInfo FromDto(ChannelInfoDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            var channelInfo = new ChannelInfo
            {
                ChannelName = dto.ChannelName ?? string.Empty,
                SectionSource = (ChannelInfo.SectionInfo)dto.SectionSource,
                FixedSectionNumber = dto.FixedSectionNumber
            };

            // Parse color from hex string
            try
            {
                if (!string.IsNullOrWhiteSpace(dto.ColorHex))
                {
                    channelInfo.Color = Geometry.Graphics.Color.FromInteger(dto.ColorHex);
                }
                else
                {
                    channelInfo.Color = new Geometry.Graphics.Color(255, 255, 255, 255);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to parse color '{dto.ColorHex}': {ex.Message}");
                channelInfo.Color = new Geometry.Graphics.Color(255, 255, 255, 255);
            }

            return channelInfo;
        }

        /// <summary>
        /// Convert array of ChannelInfo to array of ChannelInfoDto
        /// </summary>
        public static ChannelInfoDto[] ToDto(ChannelInfo[] channels)
        {
            if (channels == null || channels.Length == 0)
            {
                return null;
            }

            return channels.Select(ToDto).ToArray();
        }

        /// <summary>
        /// Convert array of ChannelInfoDto back to array of ChannelInfo
        /// </summary>
        public static ChannelInfo[] FromDto(ChannelInfoDto[] dtos)
        {
            if (dtos == null || dtos.Length == 0)
            {
                return Array.Empty<ChannelInfo>();
            }

            return dtos.Select(FromDto).Where(c => c != null).ToArray();
        }

        /// <summary>
        /// Check if channel settings differ from defaults (default is empty array for greyscale mode)
        /// </summary>
        public static bool HasNonDefaultChannels(ChannelInfo[] channels)
        {
            return channels != null && channels.Length > 0;
        }
    }
}

