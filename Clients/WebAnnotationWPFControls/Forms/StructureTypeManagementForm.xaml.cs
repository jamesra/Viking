using Viking.AnnotationServiceTypes.Interfaces;
using Annotation.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WebAnnotationModel;

namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// Interaction logic for StructureTypeManagementForm.xaml
    /// </summary>
    public partial class StructureTypeManagementForm : Window
    {
        private readonly Func<StructureTypeObj> createNewType;

        /// <summary>Creates the window; the host sets <see cref="FrameworkElement.DataContext"/> to the structure type store.</summary>
        /// <param name="createNewType">
        /// Runs the host's create-structure-type flow for a root type when the user clicks Create New Type, and returns
        /// the type the server created, or null when nothing was created. This library cannot reach the WinForms
        /// properties dialog that flow shows, so the host supplies it.
        /// </param>
        public StructureTypeManagementForm(Func<StructureTypeObj> createNewType)
        {
            this.createNewType = createNewType ?? throw new ArgumentNullException(nameof(createNewType));
            InitializeComponent();
            /*
            ListPermittedBidirectional.Items.Filter = new Predicate<object>(o => FilterBidirectional(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLinkReadOnly));
            ListPermittedInputs.Items.Filter = new Predicate<object>(o => FilterInputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLinkReadOnly));
            ListPermittedOutputs.Items.Filter = new Predicate<object>(o => FilterOutputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLinkReadOnly));
            */
            this.Loaded += this.OnLoaded;
            this.Closing += this.OnClosing;

            System.Diagnostics.Debug.Assert(ListPermittedBidirectional.Items.CanFilter, "Collection view does not support required filtering");
        }

        public void OnLoaded(object sender, EventArgs e)
        {
            /*
            CollectionView PermittedSourceTypesCollection = CollectionViewSource.GetDefaultView(ListPermittedInputs.ItemsSource) as CollectionView;
            CollectionView PermittedTargetTypesCollection = CollectionViewSource.GetDefaultView(ListPermittedOutputs.ItemsSource) as CollectionView;
            CollectionView PermittedBidirectionalTypesCollection = CollectionViewSource.GetDefaultView(ListPermittedBidirectional.ItemsSource) as CollectionView;

            PermittedSourceTypesCollection.Filter = new Predicate<object>(o => FilterInputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLinkReadOnly));
            PermittedTargetTypesCollection.Filter = new Predicate<object>(o => FilterOutputs(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLinkReadOnly));
            PermittedBidirectionalTypesCollection.Filter = new Predicate<object>(o => FilterBidirectional(StructureTypeDetailsTabControl.DataContext, o as IPermittedStructureLinkReadOnly));
            */
        }


        /// <summary>
        /// Title-bar X or Alt+F4 while structure type or link rule edits are pending: asks whether to save them (as
        /// Apply does) or close and leave them pending in the stores. A failed save shows why and keeps the window
        /// open, with the unsaved edits still pending.
        /// </summary>
        private void OnClosing(object sender, CancelEventArgs e)
        {
            // The General tab's text boxes update the type on lost focus, and clicking X does not move focus, so the
            // edit being typed would otherwise be neither counted nor saved.
            if (Keyboard.FocusedElement is TextBox focused)
                focused.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            string question = StructureTypeManagerClosePrompt.Question(Store.StructureTypes.PendingChangeCount,
                                                                       Store.PermittedStructureLinks.PendingChangeCount);
            if (question is null ||
                MessageBox.Show(this, question, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                StructureTypeObjViewModel.SaveAllPending();
            }
            catch (Exception exp)
            {
                Trace.WriteLine($"Structure Type Manager save on close failed: {exp}");
                MessageBox.Show(this, $"The changes could not be saved and are still pending:\n{exp.Message}", Title,
                                MessageBoxButton.OK, MessageBoxImage.Error);
                e.Cancel = true;
            }
        }

        private void StructureTypesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => Trace.WriteLine(string.Format("Selected TreeViewItem {1} -> {0}", e.NewValue, e.OldValue));

        private void StructureTypeTreeItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                FrameworkElement element = sender as FrameworkElement;
                DataObject data = new(element.DataContext);
                data.SetData(typeof(IStructureTypeReadOnly), element.DataContext);
                DragDrop.DoDragDrop(element, data, DragDropEffects.Link);
                e.Handled = true;
            }
        }

        private void StructureTypeTreeItem_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = e.ChangedButton == MouseButton.Left;

        private void StructureTypeTreeItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource is Visual source)
            {
                ContentControl control = (ContentControl)sender;
                DependencyObject parent = control;
                while (parent is not TreeViewItem)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                    if (parent is null)
                        return;
                }

                try
                {
                    TreeViewItem item = parent as TreeViewItem;
                    item.IsSelected = true;
                }
                catch (Exception exp)
                {
                    Trace.WriteLine($"{exp}");
                }
            }
        }

        private void OnCreateNewType(object sender, RoutedEventArgs e)
        {
            StructureTypeObj created = createNewType();
            if (created is null)
                return;

            // The tree's root items rebind from the store's RootObjects notification, but their containers are generated
            // in the next layout pass; select after it.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => SelectRootType(created.ID)));
        }

        /// <summary>Selects and scrolls to the root type with this ID; does nothing when the tree does not show it.</summary>
        private void SelectRootType(long id)
        {
            foreach (object item in StructureTypesTree.Items)
            {
                if (item is StructureTypeObj type && type.ID == id &&
                    StructureTypesTree.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem container)
                {
                    container.IsSelected = true;
                    container.BringIntoView();
                    return;
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
                if (this.StructureTypeDetailsTabControl.DataContext is StructureTypeObjViewModel model)
                {
                    model.AssignParentCommand?.Execute(newParent);
                }
            }
        }
    }
}
