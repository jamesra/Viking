using System.Windows;
using System.Windows.Controls;
using Viking.UI.WPF.ViewModels;

namespace Viking.UI.WPF.Controls
{
    public partial class LoginControl : UserControl
    {
        public LoginControl()
        {
            InitializeComponent();
            DataContextChanged += LoginControl_DataContextChanged;
            Loaded += LoginControl_Loaded;
        }

        private void LoginControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdatePasswordBox();

        private void LoginControl_Loaded(object sender, RoutedEventArgs e) => UpdatePasswordBox();

        private void UpdatePasswordBox()
        {
            if (DataContext is LoginViewModel viewModel && !string.IsNullOrEmpty(viewModel.Password))
            {
                txtPassword.Password = viewModel.Password;
            }
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = txtPassword.Password;
            }
        }
    }
}









