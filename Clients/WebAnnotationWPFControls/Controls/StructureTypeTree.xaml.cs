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
using WebAnnotationModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Annotation.Interfaces;
using System.ComponentModel.Design; 

namespace WebAnnotation.UI.Controls 
{

    /// <summary>
    /// Interaction logic for StructureTypeTree.xaml
    /// </summary>
    public partial class StructureTypeTree : UserControl
    {
        public System.Collections.ObjectModel.ObservableCollection<IStructureType> RootStructureTypes
        {
            get { return (System.Collections.ObjectModel.ObservableCollection<IStructureType>)GetValue( RootStructureTypesProperty); }
            set { SetValue( RootStructureTypesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for  RootStructureTypes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty  RootStructureTypesProperty =
            DependencyProperty.Register("RootStructureTypes", typeof(ObservableCollection<IStructureType>), 
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
                RootStructureTypes = new System.Collections.ObjectModel.ObservableCollection<IStructureType>(Store.StructureTypes.GetObjectsByIDs(Store.StructureTypes.RootObjects, true));
                tree_view.ItemsSource = RootStructureTypes;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
