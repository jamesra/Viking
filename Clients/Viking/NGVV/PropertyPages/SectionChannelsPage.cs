using System.Diagnostics;
using Viking.Common;
using Viking.ViewModels;


namespace Viking.PropertyPages
{
    [PropertyPage(typeof(SectionViewModel), 1)]
    public partial class SectionChannelsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        SectionViewModel section;

        public SectionChannelsPage()
        {
            InitializeComponent();
            this.Title = "Channels";
        }

        protected override void OnShowObject(object Object)
        {
            this.section = Object as SectionViewModel;
            Debug.Assert(this.section != null);

            this.channelSetupControl.SetChannelData(section.ChannelInfoArray, section.VolumeViewModel.ChannelNames);
        }

        protected override void OnSaveChanges()
        {
            section.ChannelInfoArray = channelSetupControl.Channels;
        }
    }
}
