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

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// Interaction logic for StructureTypeChooser.xaml
    /// </summary>
    public partial class SelectStructureTypeForm 
    {
        public System.Collections.ObjectModel.ObservableCollection<IStructureType> RootStructureTypes
        {
            get { return (System.Collections.ObjectModel.ObservableCollection<IStructureType>)GetValue(RootStructureTypesProperty); }
            set { SetValue(RootStructureTypesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for  RootStructureTypes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootStructureTypesProperty =
            DependencyProperty.Register("RootStructureTypes", typeof(ObservableCollection<IStructureType>),
                typeof(SelectStructureTypeForm), new PropertyMetadata());
         
        public SelectStructureTypeForm()
        {
            InitializeComponent();



        }
    }
}
