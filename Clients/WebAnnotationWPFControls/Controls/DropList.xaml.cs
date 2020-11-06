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
using Annotation.Interfaces;

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
