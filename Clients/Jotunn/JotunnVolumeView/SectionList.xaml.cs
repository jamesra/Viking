using System.Windows.Controls;

namespace Viking.VolumeView
{
    /// <summary>
    /// Interaction logic for SectionList.xaml
    /// </summary>
    public partial class SectionList : TabItem
    {
        public SectionList()
        {
            InitializeComponent();

            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();
            this.DataContext = volume;

            this.SectionsList.ItemsSource = volume.SectionViewModels.Values;
        }
    }
}
