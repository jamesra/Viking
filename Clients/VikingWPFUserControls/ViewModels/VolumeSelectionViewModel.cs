using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Duende.IdentityModel.Client;
using Viking.UI.WPF.Models;

namespace Viking.UI.WPF.ViewModels
{
    public class VolumeSelectionViewModel : INotifyPropertyChanged
    {
        private TokenResponse _bearerToken;
        private string _identityServerUrl;
        private bool _isLoading;
        private string _statusMessage;
        private VolumeTreeNode _selectedVolume;
        private string _manualVolumeUrl;
        private bool _showManualEntry;

        public VolumeSelectionViewModel(TokenResponse bearerToken, string identityServerUrl)
        {
            _bearerToken = bearerToken;
            _identityServerUrl = identityServerUrl;
            
            OrganizationNodes = new ObservableCollection<VolumeTreeNode>();
            RecentVolumes = new ObservableCollection<VolumeInfo>();
            
            SelectCommand = new RelayCommand(SelectVolume, () => CanSelect);
            CancelCommand = new RelayCommand(Cancel);
            LoadVolumesCommand = new RelayCommand(async () => await LoadVolumesAsync());
            CopyUrlCommand = new RelayCommand(CopyUrlToClipboard, () => !string.IsNullOrWhiteSpace(SelectedVolumeUrl));
            
            LoadRecentVolumes();
            
            // Auto-load volumes on creation if we have a bearer token
            if (_bearerToken != null)
            {
                _ = LoadVolumesAsync();
            }
            else
            {
                StatusMessage = "Anonymous mode: Please use manual entry or recent volumes.";
                ShowManualEntry = true; // Auto-expand for anonymous users
            }
        }

        public ObservableCollection<VolumeTreeNode> OrganizationNodes { get; }
        public ObservableCollection<VolumeInfo> RecentVolumes { get; }

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

        public VolumeTreeNode SelectedVolume
        {
            get => _selectedVolume;
            set
            {
                if (_selectedVolume != value)
                {
                    _selectedVolume = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedVolumeUrl));
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopyUrlCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedVolumeUrl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ManualVolumeUrl))
                {
                    return AppendDefaultVolumeFilenameIfMissing(ManualVolumeUrl);
                }
                else if (SelectedVolume?.Volume != null)
                {
                    return SelectedVolume.Volume.VolumeXmlUrl;
                }
                return string.Empty;
            }
        }

        public string ManualVolumeUrl
        {
            get => _manualVolumeUrl;
            set
            {
                if (_manualVolumeUrl != value)
                {
                    _manualVolumeUrl = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedVolumeUrl));
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopyUrlCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        public ICommand CancelCommand { get; }
        public ICommand LoadVolumesCommand { get; }
        public ICommand CopyUrlCommand { get; }

        private bool CanSelect => !IsLoading && 
            (SelectedVolume?.Volume != null || !string.IsNullOrWhiteSpace(ManualVolumeUrl));

        public event EventHandler<VolumeSelectedEventArgs> VolumeSelected;
        public event EventHandler SelectionCancelled;
        public event PropertyChangedEventHandler PropertyChanged;

        private void LoadRecentVolumes()
        {
            // Recent volumes will be populated by the hosting application
            // This keeps VikingWPFUserControls independent of VikingCore
        }

        public void AddRecentVolume(string url, string name)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var volumeInfo = new VolumeInfo
            {
                VolumeXmlUrl = url,
                Name = name ?? ExtractVolumeName(url),
                Organization = "Recent"
            };
            
            RecentVolumes.Add(volumeInfo);
        }

        public void SelectMostRecentVolumeIfAvailable()
        {
            if (RecentVolumes.Count > 0)
            {
                var mostRecentVolume = RecentVolumes[0];
                var node = new VolumeTreeNode
                {
                    Volume = mostRecentVolume,
                    Name = mostRecentVolume.Name,
                    IsOrganization = false
                };
                SelectedVolume = node;
            }
        }

        private string ExtractVolumeName(string url)
        {
            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments;
                if (segments.Length > 1)
                {
                    var lastSegment = segments[segments.Length - 1].TrimEnd('/');
                    if (lastSegment.EndsWith(".vikingxml", StringComparison.OrdinalIgnoreCase))
                        return lastSegment.Substring(0, lastSegment.Length - 11);
                    return lastSegment;
                }
                return uri.Host;
            }
            catch
            {
                return url;
            }
        }

        private async Task LoadVolumesAsync()
        {
            if (_bearerToken == null)
            {
                StatusMessage = "No authentication token available";
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading volumes...";

            try
            {
                if (!Uri.TryCreate(_identityServerUrl, UriKind.Absolute, out Uri identityUri))
                {
                    StatusMessage = "Invalid Identity Server URL";
                    return;
                }

                // IdentityApiURL uses the same host but port 6001 instead of the Identity Server port
                var identityApiUri = new UriBuilder(identityUri)
                {
                    Port = 6001
                }.Uri;

                // Debug logging
                Trace.WriteLine($"[VolumeSelection] Identity Server URL: {identityUri}");
                Trace.WriteLine($"[VolumeSelection] Identity API URL (port 6001): {identityApiUri}");
                Trace.WriteLine($"[VolumeSelection] Full endpoint will be: {new Uri(identityApiUri, "Permissions/AccessibleVolumes")}");

                var helper = new Viking.Tokens.IdentityServerHelper
                {
                    IdentityServerURL = identityUri,
                    IdentityApiURL = identityApiUri
                };

                var volumesDict = await helper.RetrieveUserAccessibleVolumes(_bearerToken);
                
                if (volumesDict == null || volumesDict.Count == 0)
                {
                    StatusMessage = "No volumes available from server. Use manual entry or recent volumes.";
                    return;
                }

                OrganizationNodes.Clear();
                var orgGroups = new Dictionary<string, VolumeTreeNode>();

                foreach (var kvp in volumesDict)
                {
                    try
                    {
                        var volumeInfo = ParseVolumeData(kvp.Key, kvp.Value);
                        
                        var org = volumeInfo.Organization ?? "Uncategorized";
                        
                        if (!orgGroups.ContainsKey(org))
                        {
                            var orgNode = new VolumeTreeNode
                            {
                                Name = org,
                                IsOrganization = true,
                                Children = new ObservableCollection<VolumeTreeNode>()
                            };
                            orgGroups[org] = orgNode;
                            OrganizationNodes.Add(orgNode);
                        }

                        var volumeNode = new VolumeTreeNode
                        {
                            Name = volumeInfo.Name,
                            Volume = volumeInfo,
                            IsOrganization = false
                        };

                        orgGroups[org].Children.Add(volumeNode);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Error parsing volume {kvp.Key}: {ex.Message}");
                    }
                }

                StatusMessage = $"Loaded {volumesDict.Count} volume(s)";
            }
            catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
            {
                // The volumes API endpoint doesn't exist on this Identity Server
                // This is expected for servers that don't implement the UserAccessibleVolumes endpoint
                StatusMessage = "Volume list unavailable. Please use manual entry or recent volumes.";
                System.Diagnostics.Trace.WriteLine($"Volumes API not available (404): {httpEx.Message}");
                ShowManualEntry = true; // Auto-expand manual entry section
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cannot load volumes from server. Use manual entry or recent volumes.";
                System.Diagnostics.Trace.WriteLine($"Error loading volumes: {ex}");
                ShowManualEntry = true; // Auto-expand manual entry section
            }
            finally
            {
                IsLoading = false;
            }
        }

        private VolumeInfo ParseVolumeData(long id, object data)
        {
            var volumeInfo = new VolumeInfo { Id = id };

            try
            {
                // The API returns a Dictionary<long, object> where object might be a JsonElement
                if (data is JsonElement jsonElement)
                {
                    System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Parsing volume {id}, JSON: {jsonElement.GetRawText()}");
                    
                    if (jsonElement.TryGetProperty("name", out JsonElement nameElement))
                        volumeInfo.Name = nameElement.GetString();
                    
                    if (jsonElement.TryGetProperty("organization", out JsonElement orgElement))
                        volumeInfo.Organization = orgElement.GetString();
                    
                    // Try multiple possible field names for the volume URL
                    if (jsonElement.TryGetProperty("endpoint", out JsonElement endpointElement))
                    {
                        volumeInfo.VolumeXmlUrl = endpointElement.GetString();
                        System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Found endpoint: {volumeInfo.VolumeXmlUrl}");
                    } 
                    else
                    {
                        System.Diagnostics.Trace.WriteLine($"[VolumeSelection] No endpoint, volumeXmlUrl, or url property found in JSON");
                    }
                    
                    if (jsonElement.TryGetProperty("description", out JsonElement descElement))
                        volumeInfo.Description = descElement.GetString();
                }
                else
                {
                    // Fallback: try to parse as JSON string
                    var json = JsonSerializer.Serialize(data);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("name", out JsonElement nameElement))
                            volumeInfo.Name = nameElement.GetString();
                        
                        if (root.TryGetProperty("organization", out JsonElement orgElement))
                            volumeInfo.Organization = orgElement.GetString();
                        
                        // Try multiple possible field names for the volume URL
                        if (root.TryGetProperty("endpoint", out JsonElement endpointElement))
                            volumeInfo.VolumeXmlUrl = endpointElement.GetString(); 
                        
                        if (root.TryGetProperty("description", out JsonElement descElement))
                            volumeInfo.Description = descElement.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error parsing volume data: {ex.Message}");
            }

            // Set defaults if not found
            if (string.IsNullOrEmpty(volumeInfo.Name))
                volumeInfo.Name = $"Volume {id}";
            
            if (string.IsNullOrEmpty(volumeInfo.Organization))
                volumeInfo.Organization = "Uncategorized";

            System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Final VolumeInfo: Id={volumeInfo.Id}, Name={volumeInfo.Name}, VolumeXmlUrl={volumeInfo.VolumeXmlUrl ?? "(null)"}");
            
            return volumeInfo;
        }

        private void SelectVolume()
        {
            string selectedUrl = null;

            // Prioritize manual entry if provided
            if (!string.IsNullOrWhiteSpace(ManualVolumeUrl))
            {
                selectedUrl = ManualVolumeUrl;
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Using manually entered volume: {selectedUrl}");
            }
            else if (SelectedVolume?.Volume != null)
            {
                selectedUrl = SelectedVolume.Volume.VolumeXmlUrl;
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] SelectedVolume.Volume exists - VolumeXmlUrl={selectedUrl ?? "(null)"}");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] No volume selected (SelectedVolume={SelectedVolume}, Volume={SelectedVolume?.Volume})");
            }

            if (!string.IsNullOrWhiteSpace(selectedUrl))
            {
                // Ensure URL has proper format (add volume.vikingxml if needed)
                selectedUrl = AppendDefaultVolumeFilenameIfMissing(selectedUrl);
                
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Final volume URL: {selectedUrl}");
                VolumeSelected?.Invoke(this, new VolumeSelectedEventArgs { VolumeUrl = selectedUrl });
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] ERROR: selectedUrl is null or empty, cannot raise VolumeSelected event");
                StatusMessage = "Error: Volume URL is not available. Please enter URL manually.";
            }
        }

        private void Cancel()
        {
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void CopyUrlToClipboard()
        {
            if (!string.IsNullOrWhiteSpace(SelectedVolumeUrl))
            {
                try
                {
                    System.Windows.Clipboard.SetText(SelectedVolumeUrl);
                    StatusMessage = "Volume URL copied to clipboard";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to copy URL: {ex.Message}";
                    System.Diagnostics.Trace.WriteLine($"Error copying to clipboard: {ex}");
                }
            }
        }

        private string AppendDefaultVolumeFilenameIfMissing(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            try
            {
                Uri websiteUri = new Uri(url);
                string path = websiteUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
                if (!path.Contains('.'))
                {
                    if (!url.EndsWith("/"))
                        url += "/";

                    url += "volume.vikingxml";
                }
            }
            catch (UriFormatException)
            {
                // If URL is invalid, return as-is
                return url;
            }

            return url;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class VolumeTreeNode : INotifyPropertyChanged
    {
        private string _name;
        private bool _isOrganization;
        private VolumeInfo _volume;
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

        public bool IsOrganization
        {
            get => _isOrganization;
            set
            {
                if (_isOrganization != value)
                {
                    _isOrganization = value;
                    OnPropertyChanged();
                }
            }
        }

        public VolumeInfo Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
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

        public ObservableCollection<VolumeTreeNode> Children { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class VolumeSelectedEventArgs : EventArgs
    {
        public string VolumeUrl { get; set; }
    }
}

