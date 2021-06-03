using Annotation.Interfaces;
using Annotation.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WebAnnotationModel;

namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// Interaction logic for StructureTypeManagementForm.xaml
    /// </summary>
    public partial class StructureTypeManagementForm : Window
    {  
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

                try
                {
                    TreeViewItem item = parent as TreeViewItem;
                    item.IsSelected = true;
                }
                catch(Exception exp)
                {
                    Trace.WriteLine($"{exp}");
                }
            }
        }

        private void OnChooseColor(object sender, RoutedEventArgs e)
        { 
            
        } 

        private void On_Drop_ParentStructure(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(StructureTypeObj)) is StructureTypeObj newParent)
            {
                if(this.StructureTypeDetailsTabControl.DataContext is StructureTypeObjViewModel model)
                {
                    if (model.AssignParentCommand != null)
                        model.AssignParentCommand.Execute(newParent);
                }
            }
        }
    }
}
