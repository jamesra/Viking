using System;
using Viking.Common;
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
            ChannelInfo[] channelInfos = _section.ChannelInfoArray as ChannelInfo[];
            string[] channelNames = _section.VolumeViewModel.ChannelNames as string[];
            ChannelSetup.SetChannelData(channelInfos, channelNames);
        }

        public override bool ValidateChanges()
        {
            // currently no validation rules beyond greyscale vs color selection.
            return true;
        }

        public override void SaveChanges()
        {
            ChannelInfo[] channels = ChannelSetup.Channels;
            _section.ChannelInfoArray = channels;
        }
    }
}

