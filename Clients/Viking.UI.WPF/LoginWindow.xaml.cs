using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Duende.IdentityModel.Client;
using Viking.Common;
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
        private bool _launchVolumeAutoSelectStarted;

        public LoginWindow()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            InitializeLoginStage();
            PreviewMouseDown += OnPreviewMouseDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loginViewModel != null)
            {
                if (!string.IsNullOrWhiteSpace(InitialUsername))
                    _loginViewModel.Username = InitialUsername;
                if (!string.IsNullOrEmpty(InitialPassword))
                    _loginViewModel.Password = InitialPassword;
            }

            if (_loginViewModel == null)
                return;
            if (!string.IsNullOrWhiteSpace(InitialIdentityServerUrl))
                _loginViewModel.IdentityServerUrl = InitialIdentityServerUrl;
            if (!string.IsNullOrWhiteSpace(LaunchStatusMessage))
                _loginViewModel.StatusMessage = LaunchStatusMessage;

            if (!string.IsNullOrWhiteSpace(InitialApiToken))
            {
                var apiToken = TokenResponseFactory.FromAccessToken(InitialApiToken);
                if (apiToken == null || string.IsNullOrEmpty(apiToken.AccessToken))
                {
                    Trace.WriteLine("[LoginWindow] Launch access token could not be wrapped as TokenResponse; requiring sign-in.");
                    if (string.IsNullOrWhiteSpace(_loginViewModel.StatusMessage))
                        _loginViewModel.StatusMessage = "The launch link could not be used (unreadable token); please sign in.";
                    TryAutoLoginFromDeepLink();
                    return;
                }

                ApiToken = apiToken;
                BearerToken = apiToken;
                Credentials ??= new NetworkCredential("anonymous", "connectome");
                // Publish immediately so any early WCF call (and DialogResult close) sees a readable AccessToken.
                TokenInjector.BearerToken = apiToken;
                if (!string.IsNullOrWhiteSpace(_loginViewModel.IdentityServerUrl))
                    TokenInjector.BearerTokenAuthority = _loginViewModel.IdentityServerUrl;
                ShowVolumeStage(apiToken);
                if (_volumeSelectionViewModel != null)
                    _volumeSelectionViewModel.PropertyChanged += OnLaunchVolumeSelectionPropertyChanged;
                TryAutoSelectLaunchVolume();
                return;
            }

            TryAutoLoginFromDeepLink();
        }

        /// <summary>
        /// Deep link named a volume but the code was unusable: submit remembered credentials, then auto-select volume.
        /// </summary>
        private void TryAutoLoginFromDeepLink()
        {
            if (!AutoAdvanceFromDeepLink)
                return;
            if (_loginViewModel?.LoginCommand?.CanExecute(null) != true)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_loginViewModel?.LoginCommand?.CanExecute(null) == true)
                    _loginViewModel.LoginCommand.Execute(null);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>Launch-code path: select the linked volume once the tree finishes loading.</summary>
        private void OnLaunchVolumeSelectionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(VolumeSelectionViewModel.IsLoading))
                return;
            TryAutoSelectLaunchVolume();
        }

        private void TryAutoSelectLaunchVolume()
        {
            if (_launchVolumeAutoSelectStarted || _volumeSelectionViewModel == null)
                return;
            if (!AutoAdvanceFromDeepLink && string.IsNullOrWhiteSpace(InitialApiToken))
                return;
            if (_volumeSelectionViewModel.IsLoading)
                return;

            if (!string.IsNullOrWhiteSpace(InitialVolumeUrl) && LooksLikeHttpUrl(InitialVolumeUrl))
            {
                _volumeSelectionViewModel.ManualVolumeUrl = InitialVolumeUrl;
            }
            else
            {
                string volumeName = !string.IsNullOrWhiteSpace(InitialVolumeName) ? InitialVolumeName : InitialVolumeUrl;
                if (string.IsNullOrWhiteSpace(volumeName) || !_volumeSelectionViewModel.TrySelectVolumeByName(volumeName))
                    return;
            }

            if (_volumeSelectionViewModel.SelectCommand?.CanExecute(null) != true)
                return;

            _launchVolumeAutoSelectStarted = true;
            _volumeSelectionViewModel.SelectCommand.Execute(null);
        }

        private static bool LooksLikeHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.XButton1)
                return;

            ICommand cancelCommand = CurrentStage switch
            {
                LoginStage.VolumeSelection => _volumeSelectionViewModel?.CancelCommand,
                LoginStage.SegmentationServiceSelection => _segmentationServiceSelectionViewModel?.CancelCommand,
                _ => null
            };

            if (cancelCommand is null || !cancelCommand.CanExecute(null))
                return;

            cancelCommand.Execute(null);
            e.Handled = true;
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
        /// <summary>Identity server URL used for login so TokenInjector can attach a Bearer token to annotation requests.</summary>
        public string IdentityServerUrl => _loginViewModel?.IdentityServerUrl;
        public TokenResponse ApiToken { get; private set; }

        public string InitialSegmentationServiceUrl { get; set; }

        /// <summary>When set (e.g. from viking://open code exchange), skip login and use this as the API token.</summary>
        public string InitialApiToken { get; set; }

        /// <summary>Identity server URL when launching with a code (from exchange response).</summary>
        public string InitialIdentityServerUrl { get; set; }

        /// <summary>Optional initial volume URL (from command line or code exchange).</summary>
        public string InitialVolumeUrl { get; set; }

        /// <summary>Optional Identity volume name from launch-exchange (e.g. RC2).</summary>
        public string InitialVolumeName { get; set; }

        /// <summary>
        /// True when started from viking:// that names a volume. After any successful
        /// sign-in, auto-select that volume and complete segmentation without requiring
        /// <see cref="InitialApiToken"/>.
        /// </summary>
        public bool AutoAdvanceFromDeepLink { get; set; }

        /// <summary>Shown on the login status line when a launch code could not be used.</summary>
        public string LaunchStatusMessage { get; set; }

        /// <summary>Optional username from -u so the login fields are prefilled.</summary>
        public string InitialUsername { get; set; }

        /// <summary>Optional password from -p so the login fields are prefilled.</summary>
        public string InitialPassword { get; set; }

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

            // Show volume selection stage with bearer token (from login for both normal and anonymous)
            ShowVolumeStage(BearerToken);
            if (AutoAdvanceFromDeepLink)
            {
                if (_volumeSelectionViewModel != null)
                    _volumeSelectionViewModel.PropertyChanged += OnLaunchVolumeSelectionPropertyChanged;
                TryAutoSelectLaunchVolume();
            }
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
            segmentationSelectionControl?.DataContext = null;

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
            VolumeName = !string.IsNullOrWhiteSpace(InitialVolumeName) ? InitialVolumeName : e.Name;

            // Validate the volume endpoint before proceeding
            bool isValid = await ValidateVolumeEndpointAsync(VolumeURL);

            if (!isValid)
            {
                // Validation failed - error message already displayed in UI
                // User stays on volume selection stage
                return;
            }

            await PrepareSegmentationStageAsync(VolumeName, VolumeURL);

            // Update the recent volumes list in the UI (remove duplicates and add to top)
            _volumeSelectionViewModel?.AddRecentVolume(VolumeURL, VolumeName);
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
            _volumeSelectionViewModel?.IsLoading = isLoading;

            _segmentationServiceSelectionViewModel?.IsLoading = isLoading;
        }

        private void SetViewModelStatusMessage(string message)
        {
            _volumeSelectionViewModel?.StatusMessage = message;

            _segmentationServiceSelectionViewModel?.StatusMessage = message;
        }

        private async Task PrepareSegmentationStageAsync(string volumeName, string volumeUrl)
        {
            try
            {
                UpdateViewModelStatus(true, "Requesting volume permissions...");

                var (parsedVolumeName, identityApiUrl) = await LoadAndParseVolumeXml(volumeUrl);
                volumeName ??= parsedVolumeName;

                if (!Uri.TryCreate(_loginViewModel?.IdentityServerUrl, UriKind.Absolute, out Uri identityServerUrl))
                {
                    throw new Exception("Invalid Identity Server URL");
                }

                TokenResponse apiToken;
                TokenResponse volumeToken;
                bool launchCodePath = !string.IsNullOrWhiteSpace(InitialApiToken)
                    && ApiToken != null
                    && !string.IsNullOrEmpty(ApiToken.AccessToken)
                    && (string.IsNullOrWhiteSpace(_savedUsername) || string.IsNullOrWhiteSpace(_savedPassword));

                if (launchCodePath)
                {
                    // Launch-code path: no password available; use the exchanged token for the session.
                    // Identity launch-exchange mints this with volume.Read/Annotate/Review plus Viking.Annotation.
                    Trace.WriteLine("[LoginWindow] Using launch API token as volume bearer token (no password for ROPC).");
                    apiToken = ApiToken;
                    volumeToken = ApiToken;
                }
                else
                {
                    SetViewModelStatusMessage($"Authenticating to volume '{volumeName}'...");

                    (apiToken, volumeToken) = await VolumeAuthHelper.RequestVolumeBearerTokenWithApiTokenAsync(
                        _savedUsername,
                        _savedPassword,
                        volumeName,
                        identityApiUrl,
                        identityServerUrl,
                        requireReviewRights: false,
                        clientSecret: IdentityAppSettings.ClientSecret);

                    ApiToken = apiToken;
                }

                Task<Dictionary<long, object>> segmentationTask = FetchSegmentationServicesAsync(apiToken, identityApiUrl);

                BearerToken = volumeToken;
                Credentials ??= new NetworkCredential(_savedUsername ?? "anonymous", _savedPassword ?? "connectome");
                // Set TokenInjector immediately so WCF AnnotationService calls use the volume-scoped token (critical for non-anonymous users after pre-load segmentation flow).
                if (volumeToken == null || string.IsNullOrEmpty(volumeToken.AccessToken))
                {
                    throw new Exception(
                        "Volume authentication returned an empty access token. Annotation service calls will be denied. " +
                        "If you opened Viking from SBFSEM-Tools, sign in with username and password, or update Viking.");
                }

                TokenInjector.BearerToken = volumeToken;
                TokenInjector.BearerTokenAuthority = identityServerUrl?.ToString() ?? _loginViewModel?.IdentityServerUrl;

                if (!string.IsNullOrWhiteSpace(volumeName)
                    && !JwtAccessTokenScopes.ContainsVolumeRead(volumeToken.AccessToken, volumeName))
                {
                    Trace.WriteLine(
                        $"[LoginWindow] Launch/volume token may lack Read scope for '{volumeName}' " +
                        $"(expected {ResourceScopeNames.ToScope(volumeName, "Read")} or {volumeName}.Read).");
                }

                if (!string.IsNullOrWhiteSpace(volumeName))
                    VolumeName = volumeName;

                UpdateViewModelStatus(false, "Authentication successful!");

                Dictionary<long, object> servicesDict = await segmentationTask;

                CleanupSegmentationServiceViewModel();
                var preselectedEndpoint = SegmentationServiceUrl ?? InitialSegmentationServiceUrl;
                _segmentationServiceSelectionViewModel = new SegmentationServiceSelectionViewModel(apiToken, _loginViewModel.IdentityServerUrl, preselectedEndpoint, servicesDict);
                _segmentationServiceSelectionViewModel.SegmentationServiceSelected += OnSegmentationServiceSelected;
                _segmentationServiceSelectionViewModel.SegmentationSelectionSkipped += OnSegmentationSelectionSkipped;
                _segmentationServiceSelectionViewModel.SelectionCancelled += OnSegmentationSelectionCancelled;

                // Launch-code path: auto-complete without showing the segmentation picker.
                if (!string.IsNullOrWhiteSpace(InitialApiToken) || AutoAdvanceFromDeepLink)
                {
                    await AutoCompleteSegmentationForLaunchAsync(preselectedEndpoint, servicesDict);
                    return;
                }

                ShowSegmentationStageWithViewModel(_segmentationServiceSelectionViewModel, preselectedEndpoint);
            }
            catch (Exception ex)
            {
                var message = TokenErrorHelper.ToExceptionMessage(ex);
                UpdateViewModelStatus(false, $"Error: {message}");
                System.Diagnostics.Trace.WriteLine($"Volume authentication error: {ex}");
                System.Windows.MessageBox.Show(
                    $"Failed to authenticate to volume:\n\n{message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// After launch-code volume auth, pick last-used / only segmentation service or skip.
        /// </summary>
        private Task AutoCompleteSegmentationForLaunchAsync(string preselectedEndpoint, Dictionary<long, object> servicesDict)
        {
            if (!string.IsNullOrWhiteSpace(preselectedEndpoint))
            {
                SegmentationServiceUrl = preselectedEndpoint;
                DialogResult = true;
                return Task.CompletedTask;
            }

            _segmentationServiceSelectionViewModel.SelectMostRecentServiceIfAvailable();
            if (_segmentationServiceSelectionViewModel.SelectedService?.Service?.Endpoint is string endpoint
                && !string.IsNullOrWhiteSpace(endpoint))
            {
                SegmentationServiceUrl = endpoint;
                DialogResult = true;
                return Task.CompletedTask;
            }

            // Exactly one accessible service
            var only = _segmentationServiceSelectionViewModel.ServiceNodes?
                .Select(n => n.Service?.Endpoint)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            if (_segmentationServiceSelectionViewModel.ServiceNodes?.Count == 1 && !string.IsNullOrWhiteSpace(only))
            {
                SegmentationServiceUrl = only;
                DialogResult = true;
                return Task.CompletedTask;
            }

            // Segmentation is optional — skip and open the volume.
            SegmentationServiceUrl = null;
            DialogResult = true;
            return Task.CompletedTask;
        }

        private async Task<Dictionary<long, object>> FetchSegmentationServicesAsync(TokenResponse apiToken, Uri identityApiUrl)
        {
            try
            {
                var helper = new IdentityApiHelper { IdentityApiURL = identityApiUrl };
                return await helper.RetrieveUserAccessibleSegmentationServices(apiToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error fetching segmentation services: {ex}");
                return null;
            }
        }

        private void ShowSegmentationStageWithViewModel(SegmentationServiceSelectionViewModel vm, string preselectedEndpoint)
        {
            segmentationSelectionControl.DataContext = vm;
            CurrentStage = LoginStage.SegmentationServiceSelection;
            Title = "Viking - Select Segmentation Service";
            PopulateRecentSegmentationServices();
            // Prefer last selected segmentation service if it exists
            if (!string.IsNullOrWhiteSpace(preselectedEndpoint))
            {
                vm.PreselectService(preselectedEndpoint);
            }
            // If no selection yet and exactly one available service, select it by default
            if (vm.SelectedService == null && string.IsNullOrWhiteSpace(vm.ManualServiceEndpoint) && vm.ServiceNodes.Count == 1 && !string.IsNullOrWhiteSpace(vm.ServiceNodes[0].Service?.Endpoint))
            {
                vm.PreselectService(vm.ServiceNodes[0].Service.Endpoint);
            }
        }

        private async Task PerformVolumeAuthenticationAsync(string volumeName, string volumeUrl)
        {
            try
            {
                if (_isAnonymous)
                {
                    // Anonymous user already has a bearer token from login; reuse it.
                    UpdateViewModelStatus(false, "Authentication successful!");
                    return;
                }

                UpdateViewModelStatus(true, "Requesting volume permissions...");

                var (parsedVolumeName, identityApiUrl) = await LoadAndParseVolumeXml(volumeUrl);
                volumeName ??= parsedVolumeName;

                if (!Uri.TryCreate(_loginViewModel?.IdentityServerUrl, UriKind.Absolute, out Uri identityServerUrl))
                {
                    throw new Exception("Invalid Identity Server URL");
                }

                var (apiToken, volumeToken) = await RequestVolumePermissionsToken(volumeName, identityApiUrl, identityServerUrl);
                ApiToken = apiToken;
                BearerToken = volumeToken;

                UpdateViewModelStatus(false, "Authentication successful!");
            }
            catch (Exception ex)
            {
                var message = TokenErrorHelper.ToExceptionMessage(ex);
                UpdateViewModelStatus(false, $"Error: {message}");
                System.Diagnostics.Trace.WriteLine($"Volume authentication error: {ex}");
                System.Windows.MessageBox.Show(
                    $"Failed to authenticate to volume:\n\n{message}",
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
                HttpClientHandler handler = volumeUri.Scheme.ToLower() == "https" && Credentials != null
                    ? new HttpClientHandler
                    {
                        Credentials = Credentials
                    }
                    : new HttpClientHandler
                    {
                        UseDefaultCredentials = true
                    };
                using HttpClient httpClient = new(handler);
                // Set timeout to prevent hanging
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Try HEAD request first (more efficient)
                try
                {
                    using HttpRequestMessage request = new(HttpMethod.Head, volumeUri);
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
            _segmentationServiceSelectionViewModel = new SegmentationServiceSelectionViewModel(ApiToken, _loginViewModel.IdentityServerUrl, preselectedEndpoint, preloadedServices: null);
            _segmentationServiceSelectionViewModel.SegmentationServiceSelected += OnSegmentationServiceSelected;
            _segmentationServiceSelectionViewModel.SegmentationSelectionSkipped += OnSegmentationSelectionSkipped;
            _segmentationServiceSelectionViewModel.SelectionCancelled += OnSegmentationSelectionCancelled;

            ShowSegmentationStageWithViewModel(_segmentationServiceSelectionViewModel, preselectedEndpoint);
        }

        private void ShowSegmentationSelectionStage()
        {
            CleanupSegmentationServiceViewModel();

            var preselectedEndpoint = SegmentationServiceUrl ?? InitialSegmentationServiceUrl;
            InitializeSegmentationServiceViewModel(preselectedEndpoint);
        }

        private void PopulateRecentSegmentationServices()
        {
            if (RecentSegmentationServiceUrls is null || _segmentationServiceSelectionViewModel is null)
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
                if (volumeElement is null)
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
                    if (identityApiUrl is null)
                    {
                        var authAttr = endpointElement.Attributes()
                            .FirstOrDefault(a => a.Name.LocalName == "Authentication")?.Value;

                        if (!string.IsNullOrEmpty(authAttr))
                        {
                            Uri.TryCreate(authAttr, UriKind.Absolute, out identityApiUrl);
                        }
                    }
                }

                Uri identityServerUrl = null;
                if (!string.IsNullOrEmpty(_loginViewModel?.IdentityServerUrl))
                    Uri.TryCreate(_loginViewModel.IdentityServerUrl, UriKind.Absolute, out identityServerUrl);

                identityApiUrl = IdentityEndpoints.ResolvePermissionsApiUrl(identityApiUrl, identityServerUrl);
                Trace.WriteLine($"[LoginWindow] Identity API URL: {identityApiUrl}");

                return (volumeName, identityApiUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error loading volume XML: {ex}");
                throw;
            }
        }


        /// <summary>
        /// Returns both the API token (for querying Identity API) and the volume-specific bearer token.
        /// </summary>
        private async Task<(TokenResponse apiToken, TokenResponse volumeToken)> RequestVolumePermissionsToken(string volumeName, Uri identityApiUrl, Uri identityServerUrl)
        {
            try
            {
                return await VolumeAuthHelper.RequestVolumeBearerTokenWithApiTokenAsync(
                    _savedUsername,
                    _savedPassword,
                    volumeName,
                    identityApiUrl,
                    identityServerUrl,
                    requireReviewRights: false,
                    clientSecret: IdentityAppSettings.ClientSecret);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error requesting volume permissions token: {ex}");
                throw;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

