using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Prism;
using System.ComponentModel.Composition;

namespace Jotunn.AnnotationView
{
    /// <summary>
    /// Interaction logic for LocationOverlay.xaml
    /// </summary>
    public partial class AnnotationsOverlay : UserControl
    {
        //[Import("MainPanelSectionGridControl")]
        //Viking.VolumeView.SectionGridControl MainPanelSectionGrid;

        public AnnotationsOverlay()
        {
            InitializeComponent();

            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();

            this.SectionsGrid.DataContext = volume; 
            this.SectionsGrid.ItemsSource = volume.SectionViewModels;
            //this.SectionsGrid.ItemsSource = MainPanelSectionGrid.ItemsSource;
            /*
            this.DataContext = volume;
            this.DataContext = Viking.VolumeView.

            InitializeComponent();

            //The item value is the section number
            List<Viking.VolumeViewModel.SectionViewModel> listViewModels = new List<Viking.VolumeViewModel.SectionViewModel>(volume.SectionViewModels.Count);
            List<int> keys = new List<int>(volume.SectionViewModels.Keys);
            keys.Sort();
            foreach(int section in keys)
            {
                listViewModels.Add(volume.SectionViewModels[section]);
            }

            this.SectionsGrid.ItemsSource = listViewModels;
            //this.SectionsGrid.DataContext = volume.SectionViewModels;
             */
        }
    }
}
