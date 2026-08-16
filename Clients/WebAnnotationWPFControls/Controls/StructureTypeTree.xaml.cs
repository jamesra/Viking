using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using WebAnnotationModel;

namespace WebAnnotation.UI.Controls
{

    /// <summary>
    /// Interaction logic for StructureTypeTree.xaml
    /// </summary>
    public partial class StructureTypeTree : UserControl
    {
        public System.Collections.ObjectModel.ObservableCollection<IStructureTypeReadOnly> RootStructureTypes
        {
            get => (System.Collections.ObjectModel.ObservableCollection<IStructureTypeReadOnly>)GetValue(RootStructureTypesProperty);
            set => SetValue(RootStructureTypesProperty, value);
        }

        // Using a DependencyProperty as the backing store for  RootStructureTypes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootStructureTypesProperty =
            DependencyProperty.Register("RootStructureTypes", typeof(ObservableCollection<IStructureTypeReadOnly>),
                typeof(StructureTypeTree), new PropertyMetadata());


        public event EventHandler<ulong> StructureTypeSelected;

        public StructureTypeTree()
        {
            InitializeComponent();

            if (Store.IsInitialized)
            {
                RootStructureTypes = new System.Collections.ObjectModel.ObservableCollection<IStructureTypeReadOnly>(Store.StructureTypes.GetObjectsByIDs(Store.StructureTypes.RootObjects, true, CancellationToken.None).Result);
                tree_view.ItemsSource = RootStructureTypes;
            }

            tree_view.SelectedItemChanged += OnSelectedItemChanged;
        }

        void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is IStructureTypeReadOnly type)
                StructureTypeSelected?.Invoke(this, type.ID);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
