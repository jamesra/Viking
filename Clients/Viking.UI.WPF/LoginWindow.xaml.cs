using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Duende.IdentityModel.Client;
using Viking.UI.WPF.ViewModels;
using Viking.Tokens;

namespace Viking.UI.WPF
{
    public enum LoginStage
    {
        Login,
        VolumeSelection,
        SegmentationServiceSelection
    }

    public partial class LoginWindow : Window, INotifyPropertyChanged
    {
        private LoginStage _currentStage = LoginStage.Login;
        private LoginViewModel _loginViewModel;
        private VolumeSelectionViewModel _volumeSelectionViewModel;
        private SegmentationServiceSelectionViewModel _segmentationServiceSelectionViewModel;
        private string _savedUsername;
        private string _savedPassword;
        private bool _isAnonymous;

        public LoginWindow()
        {
            InitializeComponent();
            
            InitializeLoginStage();
        }

        public LoginStage CurrentStage
        {
            get => _currentStage;
            set
            {
                if (_currentStage != value)
                {
                    _currentStage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowLoginStage));
                    OnPropertyChanged(nameof(ShowVolumeSelectionStage));
                    OnPropertyChanged(nameof(ShowSegmentationServiceStage));
                }
            }
        }

        // Computed properties for backward compatibility with XAML bindings
        public bool ShowLoginStage => CurrentStage == LoginStage.Login;
        public bool ShowVolumeSelectionStage => CurrentStage == LoginStage.VolumeSelection;
        public bool ShowSegmentationServiceStage => CurrentStage == LoginStage.SegmentationServiceSelection;

        public string VolumeURL { get; private set; }
        public string VolumeName { get; private set; }
        public string SegmentationServiceUrl { get; private set; }
        public NetworkCredential Credentials { get; private set; }
        public TokenResponse BearerToken { get; private set; }
        public TokenResponse ApiToken { get; private set; }

        public string InitialSegmentationServiceUrl { get; set; }

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
            
            // Show volume selection stage with appropriate bearer token (null for anonymous)
            ShowVolumeStage(e.IsAnonymous ? null : BearerToken);
        }

        private void InitializeVolumeSelectionViewModel(TokenResponse bearerToken)
        {
            _volumeSelectionViewModel = new VolumeSelectionViewModel(bearerToken, _loginViewModel.IdentityServerUrl);
            _volumeSelectionViewModel.VolumeSelected += OnVolumeSelected;
            _volumeSelectionViewModel.SelectionCancelled += OnSelectionCancelled;
            
            // Populate recent volumes from settings (will be done by hosting app)
            PopulateRecentVolumes();
            
            volumeSelectionControl.DataContext = _volumeSelectionViewModel;
        }

        private void ShowVolumeStage(TokenResponse bearerToken)
        {
            InitializeVolumeSelectionViewModel(bearerToken);
            
            _segmentationServiceSelectionViewModel = null;
            if (segmentationSelectionControl != null)
            {
                segmentationSelectionControl.DataContext = null;
            }
            
            CurrentStage = LoginStage.VolumeSelection;
            Title = "Viking - Select Volume";
        }

        private void PopulateRecentVolumes()
        {
            // This will be populated by the hosting application (Viking) via RecentVolumeUrls property
            if (RecentVolumeUrls != null && _volumeSelectionViewModel != null)
            {
                // Iterate in reverse order because AddRecentVolume inserts at position 0
                // The most recent volume is at position 0 in RecentVolumeUrls, so we need
                // to add it last so it ends up at position 0 in the RecentVolumes collection
                for (int i = RecentVolumeUrls.Count - 1; i >= 0; i--)
                {
                    var entry = RecentVolumeUrls[i];
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;
                    
                    // Parse entry format: "URL|Name" or just "URL"
                    string url;
                    string name = null;
                    var pipeIndex = entry.IndexOf('|');
                    if (pipeIndex >= 0)
                    {
                        url = entry.Substring(0, pipeIndex);
                        if (pipeIndex + 1 < entry.Length)
                            name = entry.Substring(pipeIndex + 1);
                    }
                    else
                    {
                        url = entry;
                    }
                    
                    _volumeSelectionViewModel.AddRecentVolume(url, name);
                }
                
                // Auto-select the most recent volume if available
                _volumeSelectionViewModel.SelectMostRecentVolumeIfAvailable();
            }
        }

        // Properties to allow hosting app to provide recent history
        public System.Collections.Specialized.StringCollection RecentVolumeUrls { get; set; }
        public System.Collections.Specialized.StringCollection RecentSegmentationServiceUrls { get; set; }

        private async void OnVolumeSelected(object sender, VolumeSelectedEventArgs e)
        {
            VolumeURL = e.Url;
            VolumeName = e.Name;

            // Validate the volume endpoint before proceeding
            bool isValid = await ValidateVolumeEndpointAsync(VolumeURL);
            
            if (!isValid)
            {
                // Validation failed - error message already displayed in UI
                // User stays on volume selection stage
                return;
            }
             
            await PerformVolumeAuthenticationAsync(e.Name, VolumeURL);

            // Update the recent volumes list in the UI (remove duplicates and add to top)
            if (_volumeSelectionViewModel != null)
            {
                _volumeSelectionViewModel.AddRecentVolume(VolumeURL, VolumeName);
            }

            // Validation successful - proceed to segmentation selection
            ShowSegmentationSelectionStage();
        }


        private void UpdateViewModelStatus(bool isLoading, string message)
        {
            if (_volumeSelectionViewModel != null)
            {
                _volumeSelectionViewModel.IsLoading = isLoading;
                _volumeSelectionViewModel.StatusMessage = message;
            }

            if (_segmentationServiceSelectionViewModel != null)
            {
                _segmentationServiceSelectionViewModel.IsLoading = isLoading;
                _segmentationServiceSelectionViewModel.StatusMessage = message;
            }
        }

        private void SetViewModelLoading(bool isLoading)
        {
            if (_volumeSelectionViewModel != null)
            {
                _volumeSelectionViewModel.IsLoading = isLoading;
            }

            if (_segmentationServiceSelectionViewModel != null)
            {
                _segmentationServiceSelectionViewModel.IsLoading = isLoading;
            }
        }

        private void SetViewModelStatusMessage(string message)
        {
            if (_volumeSelectionViewModel != null)
            {
                _volumeSelectionViewModel.StatusMessage = message;
            }

            if (_segmentationServiceSelectionViewModel != null)
            {
                _segmentationServiceSelectionViewModel.StatusMessage = message;
            }
        }

        private async Task PerformVolumeAuthenticationAsync(string volumeName, string volumeUrl)
        {
            try
            {
                // Show loading state if volume selection view model is available
                UpdateViewModelStatus(true, "Requesting volume permissions...");

                // Load and parse volume XML to get volume name and API URL
                var (parsedVolumeName, identityApiUrl) = await LoadAndParseVolumeXml(volumeUrl);

                if(volumeName is null)
                    volumeName = parsedVolumeName;

                // Get identity server URL
                Uri identityServerUrl;
                if (!Uri.TryCreate(_loginViewModel?.IdentityServerUrl, UriKind.Absolute, out identityServerUrl))
                {
                    throw new Exception("Invalid Identity Server URL");
                }


                SetViewModelStatusMessage($"Authenticating to volume '{volumeName}'...");

                // Request both API token and volume-specific permissions token
                var (apiToken, volumeToken) = await RequestVolumePermissionsToken(volumeName, identityApiUrl, identityServerUrl);

                // Store both tokens - ApiToken for Identity API queries, BearerToken for volume operations
                ApiToken = apiToken;
                BearerToken = volumeToken;

                UpdateViewModelStatus(false, "Authentication successful!"); 
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                UpdateViewModelStatus(false, $"Error: {ex.Message}");

                System.Diagnostics.Trace.WriteLine($"Volume authentication error: {ex}");
                
                // Show error to user
                System.Windows.MessageBox.Show(
                    $"Failed to authenticate to volume:\n\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task<bool> ValidateVolumeEndpointAsync(string volumeUrl)
        {
            if (string.IsNullOrWhiteSpace(volumeUrl))
            {
                SetViewModelLoading(false);
                SetViewModelStatusMessage("Error: Volume URL is empty");
                return false;
            }

            // Show loading state
            UpdateViewModelStatus(true, "Validating volume endpoint...");

            try
            {
                // Parse the URL
                if (!Uri.TryCreate(volumeUrl, UriKind.Absolute, out Uri volumeUri))
                {
                    UpdateViewModelStatus(false, "Error: Invalid volume URL format");
                    return false;
                }

                // Only validate HTTP/HTTPS URLs
                if (volumeUri.Scheme != "http" && volumeUri.Scheme != "https")
                {
                    // For non-HTTP URLs (like file://), skip validation
                    UpdateViewModelStatus(false, string.Empty);
                    return true;
                }

                // Create HttpClient with appropriate credentials
                HttpClientHandler handler;
                if (volumeUri.Scheme.ToLower() == "https" && Credentials != null)
                {
                    handler = new HttpClientHandler
                    {
                        Credentials = Credentials
                    };
                }
                else
                {
                    handler = new HttpClientHandler
                    {
                        UseDefaultCredentials = true
                    };
                }

                using (var httpClient = new HttpClient(handler))
                {
                    // Set timeout to prevent hanging
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    // Try HEAD request first (more efficient)
                    try
                    {
                        using (var request = new HttpRequestMessage(HttpMethod.Head, volumeUri))
                        {
                            var response = await httpClient.SendAsync(request);
                            
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                UpdateViewModelStatus(false, "Volume endpoint validated successfully");
                                return true;
                            }
                            else
                            {
                                UpdateViewModelStatus(false, $"Volume endpoint returned error: {(int)response.StatusCode} {response.StatusCode}");
                                return false;
                            }
                        }
                    }
                    catch (NotSupportedException)
                    {
                        // HEAD not supported, fall back to GET
                        var response = await httpClient.GetAsync(volumeUri);
                        
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            UpdateViewModelStatus(false, "Volume endpoint validated successfully");
                            return true;
                        }
                        else
                        {
                            UpdateViewModelStatus(false, $"Volume endpoint returned error: {(int)response.StatusCode} {response.StatusCode}");
                            return false;
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                UpdateViewModelStatus(false, "Volume endpoint request timed out");
                return false;
            }
            catch (HttpRequestException ex)
            {
                UpdateViewModelStatus(false, $"Unable to connect to volume endpoint: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"Volume endpoint validation error: {ex}");
                return false;
            }
            catch (Exception ex)
            {
                UpdateViewModelStatus(false, $"Error validating volume endpoint: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"Volume endpoint validation error: {ex}");
                return false;
            }
        }

        private void OnSelectionCancelled(object sender, EventArgs e)
        {
            // Return to login stage
            CurrentStage = LoginStage.Login;
            Title = "Viking Login";
        }

        private void CleanupSegmentationServiceViewModel()
        {
            if (_segmentationServiceSelectionViewModel != null)
            {
                _segmentationServiceSelectionViewModel.SegmentationServiceSelected -= OnSegmentationServiceSelected;
                _segmentationServiceSelectionViewModel.SegmentationSelectionSkipped -= OnSegmentationSelectionSkipped;
                _segmentationServiceSelectionViewModel.SelectionCancelled -= OnSegmentationSelectionCancelled;
            }
        }

        private void InitializeSegmentationServiceViewModel(string preselectedEndpoint)
        {
            // Use ApiToken for segmentation service queries (has permissions to query Identity API)
            _segmentationServiceSelectionViewModel = new SegmentationServiceSelectionViewModel(ApiToken, _loginViewModel.IdentityServerUrl, preselectedEndpoint);
            _segmentationServiceSelectionViewModel.SegmentationServiceSelected += OnSegmentationServiceSelected;
            _segmentationServiceSelectionViewModel.SegmentationSelectionSkipped += OnSegmentationSelectionSkipped;
            _segmentationServiceSelectionViewModel.SelectionCancelled += OnSegmentationSelectionCancelled;

            segmentationSelectionControl.DataContext = _segmentationServiceSelectionViewModel;

            PopulateRecentSegmentationServices();

            if (!string.IsNullOrWhiteSpace(preselectedEndpoint))
            {
                _segmentationServiceSelectionViewModel.PreselectService(preselectedEndpoint);
            }
        }

        private void ShowSegmentationSelectionStage()
        {
            CleanupSegmentationServiceViewModel();

            var preselectedEndpoint = SegmentationServiceUrl ?? InitialSegmentationServiceUrl;
            InitializeSegmentationServiceViewModel(preselectedEndpoint);

            CurrentStage = LoginStage.SegmentationServiceSelection;
            Title = "Viking - Select Segmentation Service";
        }

        private void PopulateRecentSegmentationServices()
        {
            if (RecentSegmentationServiceUrls == null || _segmentationServiceSelectionViewModel == null)
            {
                return;
            }

            foreach (var endpoint in RecentSegmentationServiceUrls)
            {
                _segmentationServiceSelectionViewModel.AddRecentService(endpoint, null);
            }

            if (string.IsNullOrWhiteSpace(SegmentationServiceUrl ?? InitialSegmentationServiceUrl))
            {
                _segmentationServiceSelectionViewModel.SelectMostRecentServiceIfAvailable();
            }
        }

        private void OnSegmentationServiceSelected(object sender, SegmentationServiceSelectedEventArgs e)
        {
            SegmentationServiceUrl = e.Endpoint;
            this.DialogResult = true;
        }

        private void OnSegmentationSelectionSkipped(object sender, EventArgs e)
        {
            SegmentationServiceUrl = null;
            this.DialogResult = true;
        }

        private void OnSegmentationSelectionCancelled(object sender, EventArgs e)
        {
            CurrentStage = LoginStage.VolumeSelection;
            Title = "Viking - Select Volume";
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
                        var uriBuilder = new UriBuilder(baseUri)
                        {
                            Port = 6001, 
                        };
                        identityApiUrl = uriBuilder.Uri;
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


        /// <summary>
        /// Returns a token that can interrogate the identity server API
        /// </summary>
        /// <param name="identityApiUrl"></param>
        /// <param name="identityServerUrl"></param>
        /// <returns></returns>
        private async Task<(Viking.Tokens.BearerTokenHelper, Viking.Tokens.IdentityApiHelper, TokenResponse)> RequestApiToken(Uri identityApiUrl, Uri identityServerUrl)
        {
            // Create helper for API calls (using 'api' client)
            var apiTokenHelper = new Viking.Tokens.BearerTokenHelper
            {
                IdentityServerURL = identityServerUrl,
                ClientId = "api",
                ClientSecret = "Correct Horse Battery Staple"
            };

            // Create IdentityApiHelper for API operations
            var identityApiHelper = new Viking.Tokens.IdentityApiHelper
            {
                IdentityApiURL = identityApiUrl
            };

            // Get initial token to retrieve permissions
            var idTokenResponse = await apiTokenHelper.RetrieveBearerToken(_savedUsername, _savedPassword);
            if (idTokenResponse == null || idTokenResponse.IsError)
            {
                throw new Exception($"Failed to get identity token: {idTokenResponse?.Error}");
            }

            var idToken = idTokenResponse as TokenResponse;
            return (apiTokenHelper, identityApiHelper, idToken);
        }

        /// <summary>
        /// Returns both the API token (for querying Identity API) and the volume-specific bearer token
        /// </summary>
        /// <param name="volumeName"></param>
        /// <param name="identityApiUrl"></param>
        /// <param name="identityServerUrl"></param>
        /// <returns>Tuple containing (apiToken, volumeToken)</returns>
        private async Task<(TokenResponse apiToken, TokenResponse volumeToken)> RequestVolumePermissionsToken(string volumeName, Uri identityApiUrl, Uri identityServerUrl)
        {
            try
            {
                Viking.Tokens.BearerTokenHelper apiTokenHelper;
                Viking.Tokens.IdentityApiHelper identityApiHelper;
                TokenResponse apiToken = null;
                (apiTokenHelper, identityApiHelper, apiToken) = await RequestApiToken(identityApiUrl, identityServerUrl);

                // Create helper for Viking client token
                var vikingTokenHelper = new Viking.Tokens.BearerTokenHelper
                {
                    IdentityServerURL = identityServerUrl,
                    ClientId = "Viking",
                    ClientSecret = "Correct Horse Battery Staple"
                };
                  
                // Retrieve volume-specific permissions using the API token
                string[] volumePermissions = await identityApiHelper.RetrieveUserVolumePermissions(apiToken, volumeName);
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

                var volumeToken = bearerTokenResponse as TokenResponse;
                return (apiToken, volumeToken);
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

