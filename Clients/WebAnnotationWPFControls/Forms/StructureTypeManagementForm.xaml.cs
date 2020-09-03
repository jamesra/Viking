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
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Annotation.Interfaces;
using System.Diagnostics;
using WebAnnotationModel;
using Annotation.ViewModels.Commands;
using WebAnnotationModel.Objects;

namespace WebAnnotation.WPF.Forms
{ 
    /// <summary>
    /// Interaction logic for StructureTypeManagementForm.xaml
    /// </summary>
    public partial class StructureTypeManagementForm : Window
    {


        public ICommand DropPermittedSourceTypeCommand
        {
            get { return (ICommand)GetValue(DropPermittedSourceTypeCommandProperty); }
            set { SetValue(DropPermittedSourceTypeCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DropPermittedSourceTypeCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DropPermittedSourceTypeCommandProperty =
            DependencyProperty.Register("DropPermittedSourceTypeCommand", typeof(ICommand), typeof(StructureTypeManagementForm), new PropertyMetadata());



        public ICommand DropPermittedTargetTypeCommand
        {
            get { return (ICommand)GetValue(DropPermittedTargetTypeCommandProperty); }
            set { SetValue(DropPermittedTargetTypeCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DropPermittedTargetTypeCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DropPermittedTargetTypeCommandProperty =
            DependencyProperty.Register("DropPermittedTargetTypeCommand", typeof(ICommand), typeof(StructureTypeManagementForm), new PropertyMetadata());



        public ICommand DropPermittedBidirectionalTypeCommand
        {
            get { return (ICommand)GetValue(DropPermittedBidirectionalTypeCommandProperty); }
            set { SetValue(DropPermittedBidirectionalTypeCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DropPermittedBidirectionalTypeCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DropPermittedBidirectionalTypeCommandProperty =
            DependencyProperty.Register("DropPermittedBidirectionalTypeCommand", typeof(ICommand), typeof(StructureTypeManagementForm), new PropertyMetadata());



        public StructureTypeManagementForm()
        {
            InitializeComponent();
            /*
            ListPermittedBidirectional.Items.Filter = new Predicate<object>(o => FilterBidirectional(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLink));
            ListPermittedInputs.Items.Filter = new Predicate<object>(o => FilterInputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLink));
            ListPermittedOutputs.Items.Filter = new Predicate<object>(o => FilterOutputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLink));
            */
            this.Loaded += this.OnLoaded; 

            System.Diagnostics.Debug.Assert(ListPermittedBidirectional.Items.CanFilter, "Collection view does not support required filtering");
            
            
        }

        public void OnLoaded(object sender, EventArgs e)
        {
            /*
            CollectionView PermittedSourceTypesCollection = CollectionViewSource.GetDefaultView(ListPermittedInputs.ItemsSource) as CollectionView;
            CollectionView PermittedTargetTypesCollection = CollectionViewSource.GetDefaultView(ListPermittedOutputs.ItemsSource) as CollectionView;
            CollectionView PermittedBidirectionalTypesCollection = CollectionViewSource.GetDefaultView(ListPermittedBidirectional.ItemsSource) as CollectionView;

            PermittedSourceTypesCollection.Filter = new Predicate<object>(o => FilterInputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLink));
            PermittedTargetTypesCollection.Filter = new Predicate<object>(o => FilterOutputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLink));
            PermittedBidirectionalTypesCollection.Filter = new Predicate<object>(o => FilterBidirectional(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLink));
            */
        }
          
        public static bool FilterBidirectional(object selected, IPermittedStructureLink obj)
        {
            var selectedType = selected as IStructureType;
            if (selectedType == null)
                throw new ArgumentException(string.Format("Could not convert data context {0} to IStructureType", selected));

            bool result = obj != null &&
                (selectedType.ID == obj.SourceTypeID ||
                 selectedType.ID == obj.TargetTypeID) && obj.Directional == false; 
            return result;
        }

        public static bool FilterInputs(object selected, IPermittedStructureLink obj)
        {
            var selectedType = selected as IStructureType;
            if (selectedType == null)
                throw new ArgumentException(string.Format("Could not convert data context {0} to IStructureType", selected));

            bool result = obj != null &&
                (selectedType.ID == obj.TargetTypeID) && obj.Directional == true;
            return result;
        }

        public static bool FilterOutputs(object selected, IPermittedStructureLink obj)
        {
            var selectedType = selected as IStructureType;
            if (selectedType == null)
                throw new ArgumentException(string.Format("Could not convert data context {0} to IStructureType", selected));

            bool result = obj != null &&
                (selectedType.ID == obj.SourceTypeID) && obj.Directional == true;
            return result;
        }

        private void StructureTypesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Trace.WriteLine(string.Format("Selected TreeViewItem {1} -> {0}", e.NewValue, e.OldValue));
        }
        
        private void StructureTypeTreeItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                FrameworkElement element = sender as FrameworkElement;
                DataObject data = new DataObject(element.DataContext);
                data.SetData(typeof(IStructureType), element.DataContext);
                DragDrop.DoDragDrop(element, data, DragDropEffects.Link);
                e.Handled = true;
            }
        }

        private void InputListDrop(object sender, DragEventArgs e)
        { 

            IStructureType value = e.Data.GetData(typeof(IStructureType)) as IStructureType;
            if(value == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }
            
            var type = SelectedStructureTypeHeader.DataContext as StructureTypeObj;
            if (type == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }

            PermittedStructureLinkObj key = new PermittedStructureLinkObj((long)value.ID, type.ID, Bidirectional: false);
            
            if(type.PermittedLinks.Contains(key))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }
            else
            {
                key = Store.PermittedStructureLinks.Create(key);
//                key = Store.PermittedStructureLinks.Add(key);
                e.Handled = true;
                return;
            }
        }

        private void OutputListDrop(object sender, DragEventArgs e)
        { 
            IStructureType value = e.Data.GetData(typeof(IStructureType)) as IStructureType;
            if (value == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }

            var type = SelectedStructureTypeHeader.DataContext as StructureTypeObj;
            if (type == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }

            PermittedStructureLinkObj key = new PermittedStructureLinkObj(type.ID, (long)value.ID, Bidirectional: false);

            if (type.PermittedLinks.Contains(key))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }
            else
            {
                key = Store.PermittedStructureLinks.Create(key);
                e.Handled = true;
                return;
            }
        }

        private void BidirectionalListDrop(object sender, DragEventArgs e)
        { 

            IStructureType value = e.Data.GetData(typeof(IStructureType)) as IStructureType;
            if (value == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }

            var type = SelectedStructureTypeHeader.DataContext as StructureTypeObj;
            if (type == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }

            PermittedStructureLinkObj key = new PermittedStructureLinkObj((long)value.ID, type.ID, Bidirectional: true);

            if (type.PermittedLinks.Contains(key))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false;
                return;
            }
            else
            {
                key = Store.PermittedStructureLinks.Create(key);
                e.Handled = true;
                return;
            } 
        }

        private void StructureTypeTreeItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = e.ChangedButton == MouseButton.Left;
        }

        private void StructureTypeTreeItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource is Visual source)
            {
                var control = (ContentControl)sender;
                DependencyObject parent = control;
                while(!(parent is TreeViewItem))
                {
                    parent = VisualTreeHelper.GetParent(parent);
                    if (parent == null)
                        return;
                }

                TreeViewItem item = (TreeViewItem)parent;
                item.IsSelected = true;
            }
        }
    }
}
