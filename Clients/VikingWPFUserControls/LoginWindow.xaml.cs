using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Duende.IdentityModel.Client;
using Viking.UI.WPF.ViewModels;

namespace Viking.UI.WPF
{
    public partial class LoginWindow : Window, INotifyPropertyChanged
    {
        private bool _showLoginStage = true;
        private bool _showVolumeSelectionStage = false;
        private LoginViewModel _loginViewModel;
        private VolumeSelectionViewModel _volumeSelectionViewModel;
        private string _savedUsername;
        private string _savedPassword;
        private bool _isAnonymous;

        public LoginWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            InitializeLoginStage();
        }

        public bool ShowLoginStage
        {
            get => _showLoginStage;
            set
            {
                if (_showLoginStage != value)
                {
                    _showLoginStage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowVolumeSelectionStage
        {
            get => _showVolumeSelectionStage;
            set
            {
                if (_showVolumeSelectionStage != value)
                {
                    _showVolumeSelectionStage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string VolumeURL { get; private set; }
        public NetworkCredential Credentials { get; private set; }
        public TokenResponse BearerToken { get; private set; }

        private void InitializeLoginStage()
        {
            _loginViewModel = new LoginViewModel();
            _loginViewModel.LoginSuccess += OnLoginSuccess;
            loginControl.DataContext = _loginViewModel;
        }

        private void OnLoginSuccess(object sender, LoginSuccessEventArgs e)
        {
            BearerToken = e.BearerToken;
            Credentials = e.Credentials;
            _savedUsername = e.Username;
            _savedPassword = e.Password;
            _isAnonymous = e.IsAnonymous;
            
            if (e.IsAnonymous)
            {
                // For anonymous login, show volume selection without authentication
                ShowVolumeSelectionForAnonymous();
            }
            else
            {
                // For authenticated users, show volume selection with their available volumes
                ShowVolumeSelectionForAuthenticatedUser();
            }
        }

        private void ShowVolumeSelectionForAnonymous()
        {
            // For anonymous users, we don't have a bearer token, so we can't load volumes from API
            // Instead, we'll show only recent volumes and manual entry
            _volumeSelectionViewModel = new VolumeSelectionViewModel(null, _loginViewModel.IdentityServerUrl);
            _volumeSelectionViewModel.VolumeSelected += OnVolumeSelected;
            _volumeSelectionViewModel.SelectionCancelled += OnSelectionCancelled;
            
            // Populate recent volumes from settings (will be done by hosting app)
            PopulateRecentVolumes();
            
            volumeSelectionControl.DataContext = _volumeSelectionViewModel;
            
            ShowLoginStage = false;
            ShowVolumeSelectionStage = true;
            Title = "Viking - Select Volume";
        }

        private void ShowVolumeSelectionForAuthenticatedUser()
        {
            _volumeSelectionViewModel = new VolumeSelectionViewModel(BearerToken, _loginViewModel.IdentityServerUrl);
            _volumeSelectionViewModel.VolumeSelected += OnVolumeSelected;
            _volumeSelectionViewModel.SelectionCancelled += OnSelectionCancelled;
            
            // Populate recent volumes from settings (will be done by hosting app)
            PopulateRecentVolumes();
            
            volumeSelectionControl.DataContext = _volumeSelectionViewModel;
            
            ShowLoginStage = false;
            ShowVolumeSelectionStage = true;
            Title = "Viking - Select Volume";
        }

        private void PopulateRecentVolumes()
        {
            // This will be populated by the hosting application (Viking) via RecentVolumeUrls property
            if (RecentVolumeUrls != null && _volumeSelectionViewModel != null)
            {
                foreach (var url in RecentVolumeUrls)
                {
                    _volumeSelectionViewModel.AddRecentVolume(url, null);
                }
                
                // Auto-select the most recent volume if available
                _volumeSelectionViewModel.SelectMostRecentVolumeIfAvailable();
            }
        }

        // Property to allow hosting app to provide recent volume URLs
        public System.Collections.Specialized.StringCollection RecentVolumeUrls { get; set; }

        private async void OnVolumeSelected(object sender, VolumeSelectedEventArgs e)
        {
            VolumeURL = e.VolumeUrl;
            
            // Add to recent volumes
            AddToRecentVolumes(VolumeURL);
            
            // Perform second authentication stage - request volume-specific permissions token
            await PerformVolumeAuthenticationAsync(VolumeURL);
        }

        private async Task PerformVolumeAuthenticationAsync(string volumeUrl)
        {
            try
            {
                // Show loading state if volume selection view model is available
                if (_volumeSelectionViewModel != null)
                {
                    _volumeSelectionViewModel.IsLoading = true;
                    _volumeSelectionViewModel.StatusMessage = "Requesting volume permissions...";
                }

                // Load and parse volume XML to get volume name and API URL
                var (volumeName, identityApiUrl) = await LoadAndParseVolumeXml(volumeUrl);

                // Get identity server URL
                Uri identityServerUrl;
                if (!Uri.TryCreate(_loginViewModel?.IdentityServerUrl, UriKind.Absolute, out identityServerUrl))
                {
                    throw new Exception("Invalid Identity Server URL");
                }

                // Ensure we have an API URL
                if (identityApiUrl == null)
                {
                    identityApiUrl = new Uri(identityServerUrl, "/api");
                }

                if (_volumeSelectionViewModel != null)
                {
                    _volumeSelectionViewModel.StatusMessage = $"Authenticating to volume '{volumeName}'...";
                }

                // Request volume-specific permissions token
                var volumeToken = await RequestVolumePermissionsToken(volumeName, identityApiUrl, identityServerUrl);

                // Replace the bearer token with the volume-specific one
                BearerToken = volumeToken;

                if (_volumeSelectionViewModel != null)
                {
                    _volumeSelectionViewModel.StatusMessage = "Authentication successful!";
                    _volumeSelectionViewModel.IsLoading = false;
                }

                // Close the window successfully
                base.DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                if (_volumeSelectionViewModel != null)
                {
                    _volumeSelectionViewModel.IsLoading = false;
                    _volumeSelectionViewModel.StatusMessage = $"Error: {ex.Message}";
                }

                System.Diagnostics.Trace.WriteLine($"Volume authentication error: {ex}");
                
                // Show error to user
                System.Windows.MessageBox.Show(
                    $"Failed to authenticate to volume:\n\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnSelectionCancelled(object sender, EventArgs e)
        {
            // Return to login stage
            ShowVolumeSelectionStage = false;
            ShowLoginStage = true;
            Title = "Viking Login";
        }

        private void AddToRecentVolumes(string url)
        {
            // Let the hosting application handle adding to recent volumes
            // This keeps VikingWPFUserControls independent
        }

        private async Task<(string volumeName, Uri identityApiUrl)> LoadAndParseVolumeXml(string volumeUrl)
        {
            try
            {
                // Load the volume XML
                var xmlDoc = await Viking.VolumeModel.Volume.LoadXDocumentAsync(volumeUrl, CancellationToken.None, Credentials);
                
                // Get the Volume element
                var volumeElement = Viking.VolumeModel.Volume.GetVolumeElement(xmlDoc);
                if (volumeElement == null)
                {
                    throw new Exception("Volume element not found in XML");
                }

                // Extract volume name
                var volumeName = volumeElement.Attributes()
                    .FirstOrDefault(a => string.Compare(a.Name.LocalName, "name", StringComparison.OrdinalIgnoreCase) == 0)
                    ?.Value;

                if (string.IsNullOrEmpty(volumeName))
                {
                    throw new Exception("Volume name not found in XML");
                }

                // Extract IdentityApi URL from VolumeToEndpoint element
                var endpointElement = volumeElement.Elements()
                    .FirstOrDefault(d => d.Name == "VolumeToEndpoint");
                
                Uri identityApiUrl = null;
                if (endpointElement != null)
                {
                    // Try IdentityApi attribute first
                    var identityApiAttr = endpointElement.Attributes()
                        .FirstOrDefault(a => a.Name.LocalName == "IdentityApi")?.Value;
                    
                    if (!string.IsNullOrEmpty(identityApiAttr))
                    {
                        Uri.TryCreate(identityApiAttr, UriKind.Absolute, out identityApiUrl);
                    }
                    
                    // Fallback to Authentication attribute
                    if (identityApiUrl == null)
                    {
                        var authAttr = endpointElement.Attributes()
                            .FirstOrDefault(a => a.Name.LocalName == "Authentication")?.Value;
                        
                        if (!string.IsNullOrEmpty(authAttr))
                        {
                            Uri.TryCreate(authAttr, UriKind.Absolute, out identityApiUrl);
                        }
                    }
                }

                // Final fallback to IdentityServerUrl + "/api"
                if (identityApiUrl == null && !string.IsNullOrEmpty(_loginViewModel?.IdentityServerUrl))
                {
                    if (Uri.TryCreate(_loginViewModel.IdentityServerUrl, UriKind.Absolute, out Uri baseUri))
                    {
                        identityApiUrl = new Uri(baseUri, "/api");
                    }
                }

                return (volumeName, identityApiUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error loading volume XML: {ex}");
                throw;
            }
        }

        private async Task<TokenResponse> RequestVolumePermissionsToken(string volumeName, Uri identityApiUrl, Uri identityServerUrl)
        {
            try
            {
                // Create helper for API calls (using 'api' client)
                var apiTokenHelper = new Viking.Tokens.IdentityServerHelper
                {
                    IdentityServerURL = identityServerUrl,
                    IdentityApiURL = identityApiUrl,
                    ClientId = "api",
                    ClientSecret = "Correct Horse Battery Staple"
                };

                // Create helper for Viking client token
                var vikingTokenHelper = new Viking.Tokens.IdentityServerHelper
                {
                    IdentityServerURL = identityServerUrl,
                    IdentityApiURL = identityApiUrl,
                    ClientId = "Viking",
                    ClientSecret = "Correct Horse Battery Staple"
                };

                // Get initial token to retrieve permissions
                var idTokenResponse = await apiTokenHelper.RetrieveBearerToken(_savedUsername, _savedPassword);
                if (idTokenResponse == null || idTokenResponse.IsError)
                {
                    throw new Exception($"Failed to get identity token: {idTokenResponse?.Error}");
                }

                var idToken = idTokenResponse as TokenResponse;

                // Retrieve volume-specific permissions
                string[] volumePermissions = await apiTokenHelper.RetrieveUserVolumePermissions(idToken, volumeName);
                if (volumePermissions == null || volumePermissions.Length == 0)
                {
                    throw new Exception("User does not have permissions in volume");
                }

                // Build permissions list
                var permissionsList = new List<string>
                {
                    "openid",
                    "Viking.Annotation"
                };
                permissionsList.AddRange(volumePermissions.Select(p => $"{volumeName}.{p}"));

                // Request final bearer token with volume-specific permissions
                var bearerTokenResponse = await vikingTokenHelper.RetrieveBearerToken(_savedUsername, _savedPassword, permissionsList.ToArray());
                if (bearerTokenResponse == null || bearerTokenResponse.IsError)
                {
                    throw new Exception($"Failed to get bearer token: {bearerTokenResponse?.Error}");
                }

                return bearerTokenResponse as TokenResponse;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error requesting volume permissions token: {ex}");
                throw;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

