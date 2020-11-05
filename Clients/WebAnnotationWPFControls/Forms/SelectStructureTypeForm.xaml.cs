using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Configuration;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WebAnnotationModel;
using Annotation.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Annotation.ViewModels;

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// Interaction logic for StructureTypeChooser.xaml
    /// </summary>
    public partial class SelectStructureTypeForm 
    {

        /*public System.Collections.ObjectModel.ObservableCollection<IStructureType> RootStructureTypes
        {
            get { return (System.Collections.ObjectModel.ObservableCollection<IStructureType>)GetValue(RootStructureTypesProperty); }
            set { SetValue(RootStructureTypesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for  RootStructureTypes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootStructureTypesProperty =
            DependencyProperty.Register("RootStructureTypes", typeof(ObservableCollection<IStructureType>),
                typeof(SelectStructureTypeForm), new PropertyMetadata());
         
        public ObservableCollection<ulong> FavoriteStructureTypeIDs
        {
            get { return (ObservableCollection<ulong>)GetValue(FavoriteStructureTypeIDsProperty); }
            set { SetValue(FavoriteStructureTypeIDsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FavoriteStructureTypeIDs.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FavoriteStructureTypeIDsProperty =
            DependencyProperty.Register("FavoriteStructureTypeIDs", typeof(ObservableCollection<ulong>), typeof(SelectStructureTypeForm), new PropertyMetadata());
        */

        public SelectStructureTypeForm( )
        { 
            InitializeComponent();
            this.MaxHeight = System.Windows.SystemParameters.FullPrimaryScreenHeight / 2;
        }

        private void StructureTypeTreeItem_Click(object sender, RoutedEventArgs e)
        {
            Control ctrl = sender as Control;
            if(ctrl == null)
            { 
                Trace.WriteLine(string.Format("Clicked {0} which was not a control, exiting handler", ctrl.DataContext));
                return;
            }
            else
            {
                Trace.WriteLine(string.Format("Clicked {0}", ctrl.DataContext));
            }

            var StructureType = ctrl.DataContext as IStructureType;
            if(StructureType == null)
            {
                throw new ArgumentException(string.Format("Expected an IStructureType in Data Context"));
            }

            var viewModel = this.DataContext as FavoriteStructureIDsViewModel;

            viewModel.FavoriteStructureTypeIDs.Add(StructureType.ID); 
        }

        private void FavoriteStructureTypes_DeleteClick(object sender, RoutedEventArgs e)
        {
            Control ctrl = sender as Control;
            if (ctrl == null)
            {
                Trace.WriteLine(string.Format("Clicked {0} which was not a control, exiting handler", ctrl.DataContext));
                return;
            }
            else
            {
                Trace.WriteLine(string.Format("Clicked {0}", ctrl.DataContext));
            }

            var StructureType = ctrl.DataContext as IStructureType;
            if (StructureType == null)
            {
                throw new ArgumentException(string.Format("Expected an IStructureType in Data Context"));
            }

            var viewModel = this.DataContext as FavoriteStructureIDsViewModel;

            viewModel.FavoriteStructureTypeIDs.Remove(StructureType.ID); 
        }
    }
}
