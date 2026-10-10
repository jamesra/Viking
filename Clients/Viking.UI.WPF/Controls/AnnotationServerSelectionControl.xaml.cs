using System.Windows.Controls;
using System.Windows.Input;
using Viking.UI.WPF.ViewModels;

namespace Viking.UI.WPF.Controls
{
    /// <summary>
    /// Login stage UI for choosing among Identity-linked annotation servers.
    /// </summary>
    public partial class AnnotationServerSelectionControl : UserControl
    {
        public AnnotationServerSelectionControl()
        {
            InitializeComponent();
        }

        private void Servers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AnnotationServerSelectionViewModel vm
                && vm.SelectCommand is ICommand cmd
                && cmd.CanExecute(null))
            {
                cmd.Execute(null);
            }
        }
    }
}
