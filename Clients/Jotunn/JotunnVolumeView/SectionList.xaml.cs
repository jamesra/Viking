using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
