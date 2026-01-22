using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Duende.IdentityModel.Client;
using Viking.Tokens;
using Viking.UI.WPF.Models;

namespace Viking.UI.WPF.ViewModels
{
    public class SegmentationServiceSelectionViewModel : INotifyPropertyChanged
    {
        private readonly TokenResponse _bearerToken;
        private readonly string _identityServerUrl;
        private readonly string _preselectedEndpoint;

        private bool _isLoading;
        private string _statusMessage;
        private SegmentationServiceTreeNode _selectedService;
        private string _manualServiceEndpoint;
        private bool _showManualEntry;
        private readonly bool _isSelectionMade = false;

        public SegmentationServiceSelectionViewModel(TokenResponse bearerToken, string identityServerUrl, string preselectedEndpoint = null)
        {
            _bearerToken = bearerToken;
            _identityServerUrl = identityServerUrl;
            _preselectedEndpoint = preselectedEndpoint;

            ServiceNodes = [];
            RecentServices = [];

            SelectCommand = new RelayCommand(SelectService, () => CanSelect);
            NoneCommand = new RelayCommand(SelectNone);
            CancelCommand = new RelayCommand(Cancel);
            LoadServicesCommand = new RelayCommand(async () => await LoadServicesAsync());
            CopyEndpointCommand = new RelayCommand(CopyEndpointToClipboard, () => !string.IsNullOrWhiteSpace(SelectedServiceEndpoint));

            if (_bearerToken != null)
            {
                _ = LoadServicesAsync();
            }
            else
            {
                StatusMessage = "No authentication token available. Use manual entry or recent services.";
                ShowManualEntry = true;
            }

            if (!string.IsNullOrWhiteSpace(_preselectedEndpoint))
            {
                ManualServiceEndpoint = _preselectedEndpoint;
            }
        }

        public ObservableCollection<SegmentationServiceTreeNode> ServiceNodes { get; }
        public ObservableCollection<SegmentationServiceInfo> RecentServices { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        public SegmentationServiceTreeNode SelectedService
        {
            get => _selectedService;
            set
            {
                if (_selectedService != value)
                {
                    _selectedService = value;
                    if (!string.IsNullOrWhiteSpace(_manualServiceEndpoint) && value?.Service != null)
                    {
                        ManualServiceEndpoint = string.Empty;
                    }
                    OnPropertyChanged();
                    // Notify that SelectedServiceEndpoint has changed
                    OnPropertyChanged(nameof(SelectedServiceEndpoint));
                    // Raise command changed events to update button states
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopyEndpointCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedServiceEndpoint
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ManualServiceEndpoint))
                {
                    return ManualServiceEndpoint;
                }

                return SelectedService?.Service?.Endpoint ?? string.Empty;
            }
        }

        public string ManualServiceEndpoint
        {
            get => _manualServiceEndpoint;
            set
            {
                if (_manualServiceEndpoint != value)
                {
                    _manualServiceEndpoint = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedServiceEndpoint));
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopyEndpointCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool ShowManualEntry
        {
            get => _showManualEntry;
            set
            {
                if (_showManualEntry != value)
                {
                    _showManualEntry = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SelectCommand { get; }
        public ICommand NoneCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand LoadServicesCommand { get; }
        public ICommand CopyEndpointCommand { get; }

        private bool CanSelect
        {
            get
            {
                // Use SelectedServiceEndpoint which handles both manual entry and selected service endpoint
                bool canSelect = !IsLoading && !string.IsNullOrWhiteSpace(SelectedServiceEndpoint);
                Trace.WriteLine($"[SegmentationSelection] CanSelect check - IsLoading: {IsLoading}, SelectedServiceEndpoint: '{SelectedServiceEndpoint}', Result: {canSelect}");
                return canSelect;
            }
        }

        public event EventHandler<SegmentationServiceSelectedEventArgs> SegmentationServiceSelected;
        public event EventHandler SegmentationSelectionSkipped;
        public event EventHandler SelectionCancelled;
        public event PropertyChangedEventHandler PropertyChanged;

        public void AddRecentService(string endpoint, string name)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            SegmentationServiceInfo serviceInfo = new()
            {
                Endpoint = endpoint,
                Name = string.IsNullOrWhiteSpace(name) ? endpoint : name,
                Description = string.Empty
            };

            RecentServices.Add(serviceInfo);
        }

        public void SelectMostRecentServiceIfAvailable()
        {
            if (RecentServices.Count > 0)
            {
                var mostRecent = RecentServices[0];
                SelectedService = new SegmentationServiceTreeNode
                {
                    Service = mostRecent,
                    Name = mostRecent.Name,
                    IsCategory = false
                };
            }
        }

        public void PreselectService(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            ManualServiceEndpoint = endpoint;

            var node = ServiceNodes
                .SelectMany(NodesWithDescendants)
                .FirstOrDefault(n => string.Equals(n.Service?.Endpoint, endpoint, StringComparison.OrdinalIgnoreCase));

            if (node != null)
            {
                SelectedService = node;
                ManualServiceEndpoint = string.Empty;
            }
        }

        private IEnumerable<SegmentationServiceTreeNode> NodesWithDescendants(SegmentationServiceTreeNode node)
        {
            yield return node;
            if (node.Children != null)
            {
                foreach (var child in node.Children.SelectMany(NodesWithDescendants))
                {
                    yield return child;
                }
            }
        }

        private async Task LoadServicesAsync()
        {
            if (_bearerToken is null)
            {
                StatusMessage = "No authentication token available";
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading segmentation services...";

            try
            {
                if (!Uri.TryCreate(_identityServerUrl, UriKind.Absolute, out var identityUri))
                {
                    StatusMessage = "Invalid Identity Server URL";
                    return;
                }

                var identityApiUri = new UriBuilder(identityUri)
                {
                    Port = 6001
                }.Uri;

                Trace.WriteLine($"[SegmentationSelection] Identity Server URL: {identityUri}");
                Trace.WriteLine($"[SegmentationSelection] Identity API URL (port 6001): {identityApiUri}");
                Trace.WriteLine($"[SegmentationSelection] Endpoint: {new Uri(identityApiUri, "Permissions/AccessibleSegmentationServices")}");

                IdentityApiHelper helper = new()
                {
                    IdentityApiURL = identityApiUri
                };

                var servicesDict = await helper.RetrieveUserAccessibleSegmentationServices(_bearerToken);

                if (servicesDict is null || servicesDict.Count == 0)
                {
                    StatusMessage = "No segmentation services available. Use manual entry or recent services.";
                    return;
                }

                ServiceNodes.Clear();

                foreach (var kvp in servicesDict.OrderBy(kvp => kvp.Value, new SegmentationServiceComparer()))
                {
                    try
                    {
                        var serviceInfo = ParseServiceData(kvp.Key, kvp.Value);

                        // Log warning if endpoint is missing
                        if (string.IsNullOrWhiteSpace(serviceInfo.Endpoint))
                        {
                            Trace.WriteLine($"[SegmentationSelection] WARNING: Service {kvp.Key} ({serviceInfo.Name}) has no endpoint!");
                        }

                        SegmentationServiceTreeNode node = new()
                        {
                            Name = serviceInfo.Name,
                            Service = serviceInfo,
                            IsCategory = false
                        };

                        ServiceNodes.Add(node);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error parsing segmentation service {kvp.Key}: {ex.Message}");
                    }
                }

                StatusMessage = $"Loaded {servicesDict.Count} segmentation service(s)";

                if (!string.IsNullOrWhiteSpace(_preselectedEndpoint))
                {
                    PreselectService(_preselectedEndpoint);
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
            {
                StatusMessage = "Segmentation service list unavailable. Use manual entry or recent services.";
                Trace.WriteLine($"Segmentation services API not available (404): {httpEx.Message}");
                ShowManualEntry = true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Cannot load segmentation services from server. Use manual entry or recent services.";
                Trace.WriteLine($"Error loading segmentation services: {ex}");
                ShowManualEntry = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private SegmentationServiceInfo ParseServiceData(long id, object data)
        {
            SegmentationServiceInfo serviceInfo = new() { Id = id };

            try
            {
                JsonElement rootElement;

                if (data is JsonElement jsonElement)
                {
                    Trace.WriteLine($"[SegmentationSelection] Parsing service {id}, JSON: {jsonElement.GetRawText()}");
                    rootElement = jsonElement;
                }
                else
                {
                    var json = JsonSerializer.Serialize(data);
                    using JsonDocument doc = JsonDocument.Parse(json);
                    rootElement = doc.RootElement;
                }

                // Extract name
                if (rootElement.TryGetProperty("name", out var nameElement))
                {
                    serviceInfo.Name = nameElement.GetString();
                }

                // Extract description
                if (rootElement.TryGetProperty("description", out var descriptionElement))
                {
                    serviceInfo.Description = descriptionElement.GetString();
                }

                // Extract endpoint - check both root level and metadata dict (case-insensitive)
                string endpoint = null;

                // First try root level (check both lowercase and capitalized)
                if (rootElement.TryGetProperty("endpoint", out var endpointElement) ||
                    rootElement.TryGetProperty("Endpoint", out endpointElement))
                {
                    endpoint = endpointElement.GetString();
                }

                // If not found, check metadata dict (check both lowercase and capitalized)
                if (string.IsNullOrWhiteSpace(endpoint) && rootElement.TryGetProperty("metadata", out var metadataElement))
                {
                    if (metadataElement.ValueKind == JsonValueKind.Object)
                    {
                        if (metadataElement.TryGetProperty("endpoint", out var metadataEndpointElement) ||
                            metadataElement.TryGetProperty("Endpoint", out metadataEndpointElement))
                        {
                            endpoint = metadataEndpointElement.GetString();
                        }
                    }
                }

                serviceInfo.Endpoint = endpoint;

                Trace.WriteLine($"[SegmentationSelection] Service {id} - Name: {serviceInfo.Name}, Endpoint: {serviceInfo.Endpoint}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error parsing segmentation service data: {ex.Message}");
            }

            if (string.IsNullOrEmpty(serviceInfo.Name))
            {
                serviceInfo.Name = $"Segmentation Service {id}";
            }

            return serviceInfo;
        }

        private void SelectService()
        {
            Trace.WriteLine($"[SegmentationSelection] SelectService called - SelectedService: {SelectedService?.Name}, Service: {SelectedService?.Service?.Name}, Endpoint: {SelectedService?.Service?.Endpoint}");
            // Prevent execution if window is already closing 

            var endpoint = SelectedServiceEndpoint;
            Trace.WriteLine($"[SegmentationSelection] SelectedServiceEndpoint: '{endpoint}'");

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                StatusMessage = "Segmentation service endpoint is not available. Please enter one manually.";
                Trace.WriteLine($"[SegmentationSelection] ERROR: Endpoint is empty!");
                return;
            }

            endpoint = endpoint.Trim();

            Trace.WriteLine($"[SegmentationSelection] Firing SegmentationServiceSelected event with endpoint: {endpoint}");

            SegmentationServiceSelected?.Invoke(this, new SegmentationServiceSelectedEventArgs
            {
                Endpoint = endpoint,
                IsNone = false
            });

            Trace.WriteLine($"[SegmentationSelection] Event fired successfully");
        }

        private void SelectNone()
        {
            Trace.WriteLine("[SegmentationSelection] User opted to skip segmentation service.");

            SegmentationSelectionSkipped?.Invoke(this, EventArgs.Empty);
            /*SegmentationServiceSelected?.Invoke(this, new SegmentationServiceSelectedEventArgs
            {
                Endpoint = null,
                IsNone = true
            });*/
        }

        private void Cancel() => SelectionCancelled?.Invoke(this, EventArgs.Empty);

        private void CopyEndpointToClipboard()
        {
            if (!string.IsNullOrWhiteSpace(SelectedServiceEndpoint))
            {
                try
                {
                    System.Windows.Clipboard.SetText(SelectedServiceEndpoint);
                    StatusMessage = "Segmentation service endpoint copied to clipboard";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to copy endpoint: {ex.Message}";
                    Trace.WriteLine($"Error copying endpoint to clipboard: {ex}");
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private class SegmentationServiceComparer : IComparer<object>
        {
            public int Compare(object x, object y)
            {
                var nameX = ExtractName(x);
                var nameY = ExtractName(y);
                return string.Compare(nameX, nameY, StringComparison.OrdinalIgnoreCase);
            }

            private static string ExtractName(object data)
            {
                try
                {
                    if (data is JsonElement element && element.TryGetProperty("name", out var nameElement))
                    {
                        return nameElement.GetString();
                    }

                    var json = JsonSerializer.Serialize(data);
                    using JsonDocument doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("name", out var name))
                    {
                        return name.GetString();
                    }
                }
                catch
                {
                    // Ignore parsing errors and fall back to empty string
                }

                return string.Empty;
            }
        }
    }

    public class SegmentationServiceTreeNode : INotifyPropertyChanged
    {
        private string _name;
        private bool _isCategory;
        private SegmentationServiceInfo _service;
        private bool _isExpanded;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCategory
        {
            get => _isCategory;
            set
            {
                if (_isCategory != value)
                {
                    _isCategory = value;
                    OnPropertyChanged();
                }
            }
        }

        public SegmentationServiceInfo Service
        {
            get => _service;
            set
            {
                if (_service != value)
                {
                    _service = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<SegmentationServiceTreeNode> Children { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class SegmentationServiceSelectedEventArgs : EventArgs
    {
        public string Endpoint { get; set; }
        public bool IsNone { get; set; }
    }
}