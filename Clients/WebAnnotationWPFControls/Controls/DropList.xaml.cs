using Annotation.Interfaces;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WebAnnotation.WPF.Controls
{
    /// <summary>
    /// Interaction logic for DropList.xaml
    /// </summary>
    public partial class DropList : ListBox
    {  
        public ICommand DropCommand
        {
            get { return (ICommand)GetValue(DropCommandProperty); }
            set { SetValue(DropCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DropCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DropCommandProperty =
            DependencyProperty.Register("DropCommand", typeof(ICommand), typeof(DropList), new PropertyMetadata());


        public DropList()
        {
            InitializeComponent();
        }

        private void OnListDrop(object sender, DragEventArgs e)
        {
            if(this.DropCommand == null)
            { 
                e.Handled = false;
                return;
            }

            if(false == DropCommand.CanExecute(e.Data.GetData(typeof(IStructureType))))
            {
                e.Handled = false;
                return; 
            }

            DropCommand.Execute(e.Data.GetData(typeof(IStructureType)));
        }

        private void OnListDragOver(object sender, DragEventArgs e)
        {
            if (this.DropCommand == null)
            {
                e.Handled = false;
                e.Effects = DragDropEffects.None;
                return;
            }

            if (DropCommand.CanExecute(e.Data.GetData(typeof(IStructureType))))
            {
                e.Effects = DragDropEffects.Link;
                e.Handled = true;
                return;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                return;
            }
        }
    }
}
