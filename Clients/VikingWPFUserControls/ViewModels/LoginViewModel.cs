using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Duende.IdentityModel.Client;

namespace Viking.UI.WPF.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username;
        private string _password;
        private string _identityServerUrl;
        private string _statusMessage;
        private bool _isLoading;
        private bool _rememberCredentials;
        private TokenResponse _bearerToken;
        private NetworkCredential _credentials;

        public LoginViewModel()
        {
            _identityServerUrl = Properties.Settings.Default.IdentityServerURL ?? "https://identity.codepharm.net:5001/";
            _isLoading = false;
            
            LoginCommand = new RelayCommand(async () => await LoginAsync(), () => CanLogin);
            AnonymousCommand = new RelayCommand(LoginAnonymous, () => !IsLoading);
            
            // Try to load saved credentials
            LoadSavedCredentials();
        }

        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                    (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                    (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string IdentityServerUrl
        {
            get => _identityServerUrl;
            set
            {
                if (_identityServerUrl != value)
                {
                    _identityServerUrl = value;
                    OnPropertyChanged();
                    
                    // Save to settings
                    Properties.Settings.Default.IdentityServerURL = value;
                    Properties.Settings.Default.Save();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                    (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (AnonymousCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool RememberCredentials
        {
            get => _rememberCredentials;
            set
            {
                if (_rememberCredentials != value)
                {
                    _rememberCredentials = value;
                    OnPropertyChanged();
                }
            }
        }

        public TokenResponse BearerToken
        {
            get => _bearerToken;
            private set
            {
                _bearerToken = value;
                OnPropertyChanged();
            }
        }

        public NetworkCredential Credentials
        {
            get => _credentials;
            private set
            {
                _credentials = value;
                OnPropertyChanged();
            }
        }

        public bool IsAnonymous { get; private set; }

        public ICommand LoginCommand { get; }
        public ICommand AnonymousCommand { get; }

        private bool CanLogin => !IsLoading && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

        public event EventHandler<LoginSuccessEventArgs> LoginSuccess;
        public event PropertyChangedEventHandler PropertyChanged;

        private void LoadSavedCredentials()
        {
            try
            {
                _rememberCredentials = Properties.Settings.Default.RememberCredentials;
                
                if (_rememberCredentials)
                {
                    var lastUsername = Properties.Settings.Default.LastUsername;
                    var encryptedPassword = Properties.Settings.Default.EncryptedPassword;
                    
                    if (!string.IsNullOrEmpty(lastUsername))
                    {
                        _username = lastUsername;
                        OnPropertyChanged(nameof(Username));
                    }
                    
                    if (!string.IsNullOrEmpty(encryptedPassword))
                    {
                        try
                        {
                            _password = DecryptPassword(encryptedPassword);
                            OnPropertyChanged(nameof(Password));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"Failed to decrypt password: {ex.Message}");
                            // If decryption fails, just don't populate the password
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to load saved credentials: {ex.Message}");
            }
        }
        
        private void SaveCredentials()
        {
            try
            {
                if (RememberCredentials)
                {
                    Properties.Settings.Default.LastUsername = Username;
                    Properties.Settings.Default.RememberCredentials = true;
                    
                    if (!string.IsNullOrEmpty(Password))
                    {
                        Properties.Settings.Default.EncryptedPassword = EncryptPassword(Password);
                    }
                }
                else
                {
                    // Clear saved credentials
                    Properties.Settings.Default.LastUsername = string.Empty;
                    Properties.Settings.Default.EncryptedPassword = string.Empty;
                    Properties.Settings.Default.RememberCredentials = false;
                }
                
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to save credentials: {ex.Message}");
            }
        }
        
        private string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;
                
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] encryptedBytes = ProtectedData.Protect(passwordBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        
        private string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return string.Empty;
                
            byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
            byte[] passwordBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(passwordBytes);
        }

        private async Task LoginAsync()
        {
            IsLoading = true;
            IsAnonymous = false;
            StatusMessage = $"Authenticating to {IdentityServerUrl}...";

            try
            {
                if (!Uri.TryCreate(IdentityServerUrl, UriKind.Absolute, out Uri identityUri))
                {
                    StatusMessage = "Invalid Identity Server URL";
                    return;
                }

                var tokenHelper = new Viking.Tokens.IdentityServerHelper
                {
                    IdentityServerURL = identityUri,
                    IdentityApiURL = identityUri,
                    //ClientId = "Viking",
                    ClientId = "api",
                    ClientSecret = "Correct Horse Battery Staple" // Default secret, should be configured
                };

                var tokenResponse = await tokenHelper.RetrieveBearerToken(Username, Password);
                
                if (tokenResponse.IsError)
                {
                    StatusMessage = $"Login failed: {tokenResponse.Error}";
                    return;
                }

                BearerToken = tokenResponse as TokenResponse;
                Credentials = new NetworkCredential(Username, Password);

                // Save credentials if requested
                SaveCredentials();

                StatusMessage = "Login successful!";
                LoginSuccess?.Invoke(this, new LoginSuccessEventArgs 
                { 
                    BearerToken = BearerToken, 
                    Credentials = Credentials,
                    IsAnonymous = false,
                    Username = Username,
                    Password = Password
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Trace.WriteLine($"Login error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoginAnonymous()
        {
            IsAnonymous = true;
            Credentials = new NetworkCredential("anonymous", "connectome");
            StatusMessage = "Proceeding as anonymous";
            
            LoginSuccess?.Invoke(this, new LoginSuccessEventArgs 
            { 
                BearerToken = null, 
                Credentials = Credentials,
                IsAnonymous = true,
                Username = "anonymous",
                Password = "connectome"
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LoginSuccessEventArgs : EventArgs
    {
        public TokenResponse BearerToken { get; set; }
        public NetworkCredential Credentials { get; set; }
        public bool IsAnonymous { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly Func<Task> _executeAsync;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public async void Execute(object parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

