using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Controls
{

    /// <summary>
    /// Interaction logic for StructureTypeTree.xaml
    /// </summary>
    public partial class StructureTypeTree : UserControl
    {
        public System.Collections.ObjectModel.ObservableCollection<IStructureTypeReadOnly> RootStructureTypes
        {
            get { return (System.Collections.ObjectModel.ObservableCollection<IStructureTypeReadOnly>)GetValue( RootStructureTypesProperty); }
            set { SetValue( RootStructureTypesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for  RootStructureTypes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty  RootStructureTypesProperty =
            DependencyProperty.Register("RootStructureTypes", typeof(ObservableCollection<IStructureTypeReadOnly>), 
                typeof(StructureTypeTree), new PropertyMetadata());
         

        public StructureTypeTree()
        {
            InitializeComponent();
            //if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            if(true)
            {
                // Load design-time books.
                WebAnnotationModel.State.Endpoint = new Uri("https://websvc1.connectomes.utah.edu/RABBIT/Annotation/Service.svc");
                WebAnnotationModel.State.UserCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
                RootStructureTypes = new System.Collections.ObjectModel.ObservableCollection<IStructureTypeReadOnly>(Store.StructureTypes.GetObjectsByIDs(Store.StructureTypes.RootObjects, true));
                tree_view.ItemsSource = RootStructureTypes;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
