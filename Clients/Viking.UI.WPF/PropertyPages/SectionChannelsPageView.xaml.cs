using System;
using Viking.Common;
using Viking.UI.WPF.Models;
using Viking.VolumeModel;

namespace Viking.UI.WPF.PropertyPages
{
    [PropertyPage("Viking.ViewModels.SectionViewModel, VikingCore", priority: 1)]
    public partial class SectionChannelsPageView : PropertyPageViewBase
    {
        private dynamic _section;

        public SectionChannelsPageView()
        {
            InitializeComponent();
        }

        public override string Title => "Channels";

        protected override void OnContextUpdated(object context)
        {
            _section = context ?? throw new ArgumentNullException(nameof(context));

            // Try to load persisted channel settings first
            ChannelInfo[] channelInfos = LoadPersistedChannels();

            // If no persisted settings, use current section's channel info
            if (channelInfos is null || channelInfos.Length == 0)
            {
                channelInfos = _section.ChannelInfoArray as ChannelInfo[];
            }

            string[] channelNames = _section.VolumeViewModel.ChannelNames as string[];
            ChannelSetup.SetChannelData(channelInfos, channelNames);
        }

        private ChannelInfo[] LoadPersistedChannels()
        {
            try
            {
                int currentNumber = (int)_section.Number;
                string volumeLocalDir = GetVolumeLocalDirectory();

                if (string.IsNullOrWhiteSpace(volumeLocalDir))
                {
                    return null;
                }

                var allSettings = SectionReferenceSettings.LoadForVolume(volumeLocalDir);

                if (allSettings.TryGetValue(currentNumber, out var sectionRefs))
                {
                    if (sectionRefs.Channels != null && sectionRefs.Channels.Length > 0)
                    {
                        return SectionReferenceSettings.FromDto(sectionRefs.Channels);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to load persisted channels: {ex.Message}");
                return null;
            }
        }

        public override bool ValidateChanges() =>
            // currently no validation rules beyond greyscale vs color selection.
            true;

        public override void SaveChanges()
        {
            ChannelInfo[] channels = ChannelSetup.Channels;
            System.Diagnostics.Trace.WriteLine($"SectionChannelsPageView: Saving {channels?.Length ?? 0} channels for section {_section.Number}", "PropertyPages");
            _section.ChannelInfoArray = channels;

            // Persist channel settings if non-default (non-empty array)
            int currentNumber = (int)_section.Number;
            string volumeLocalDir = GetVolumeLocalDirectory();

            if (string.IsNullOrWhiteSpace(volumeLocalDir))
            {
                return;
            }

            var allSettings = SectionReferenceSettings.LoadForVolume(volumeLocalDir);

            // Get or create section references for this section
            if (!allSettings.TryGetValue(currentNumber, out var sectionRefs))
            {
                sectionRefs = new SectionReferences();
                allSettings[currentNumber] = sectionRefs;
            }

            // Check if channels differ from defaults (default is empty array)
            if (SectionReferenceSettings.HasNonDefaultChannels(channels))
            {
                // Persist non-default channel settings
                sectionRefs.Channels = SectionReferenceSettings.ToDto(channels);
            }
            else
            {
                // Remove channel settings if default
                sectionRefs.Channels = null;
            }

            // Remove entire section entry if both channels and references are default
            if (sectionRefs.Channels is null &&
                sectionRefs.ReferenceAbove is null &&
                sectionRefs.ReferenceBelow is null)
            {
                allSettings.Remove(currentNumber);
            }

            SectionReferenceSettings.SaveForVolume(volumeLocalDir, allSettings);
            System.Diagnostics.Trace.WriteLine($"SectionChannelsPageView: Settings saved to {volumeLocalDir}", "PropertyPages");
        }

        private string GetVolumeLocalDirectory()
        {
            try
            {
                dynamic volume = _section?.VolumeViewModel;
                if (volume is null)
                {
                    return null;
                }

                // Access the Volume object through the ViewModel
                dynamic volumeModel = volume.Volume;
                if (volumeModel is null)
                {
                    return null;
                }

                // Get LocalVolumeDir using the public property
                return volumeModel.LocalVolumeDir as string;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to get volume local directory: {ex.Message}");
                return null;
            }
        }
    }
}

