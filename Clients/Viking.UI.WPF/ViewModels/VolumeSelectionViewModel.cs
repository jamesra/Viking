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
                    OnPropertyChanged(nameof(SelectedVolumeDescription));
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopyUrlCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedVolumeDescription
        {
            get
            {
                if (SelectedVolume?.Volume != null)
                {
                    return SelectedVolume.Volume.Description ?? string.Empty;
                }
                return string.Empty;
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
         

        public void AddRecentVolume(string url, string name)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Remove any existing entry with the same URL to avoid duplicates
            var existingEntry = RecentVolumes.FirstOrDefault(v => 
                string.Equals(v.VolumeXmlUrl, url, StringComparison.OrdinalIgnoreCase));
            
            if (existingEntry != null)
            {
                RecentVolumes.Remove(existingEntry);
            }

            var volumeInfo = new VolumeInfo
            {
                VolumeXmlUrl = url,
                Name = name ?? ExtractVolumeName(url),
                Organization = "Recent"
            };
            
            // Insert at the top of the list (most recent)
            RecentVolumes.Insert(0, volumeInfo);
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
                Trace.WriteLine($"[VolumeSelection] Full endpoint will be: {new Uri(identityApiUri, "Permissions/UserAccessibleVolumeTree")}");

                var helper = new Viking.Tokens.IdentityApiHelper
                {
                    IdentityApiURL = identityApiUri
                };

                var apiTreeNodes = await helper.RetrieveUserAccessibleVolumeTree(_bearerToken);
                
                if (apiTreeNodes == null || apiTreeNodes.Count == 0)
                {
                    StatusMessage = "No volumes available from server. Use manual entry or recent volumes.";
                    return;
                }

                OrganizationNodes.Clear();
                
                int totalVolumes = 0;
                foreach (var apiNode in apiTreeNodes)
                {
                    try
                    {
                        var uiNode = BuildUITreeNodeFromApiNode(apiNode);
                        if (uiNode != null)
                        {
                            OrganizationNodes.Add(uiNode);
                            totalVolumes += CountVolumesInNode(uiNode);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Error processing tree node {apiNode?.Name}: {ex.Message}");
                    }
                }

                StatusMessage = $"Loaded {totalVolumes} volume(s)";
            }
            catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
            {
                // The volumes API endpoint doesn't exist on this Identity Server
                // This is expected for servers that don't implement the UserAccessibleVolumeTree endpoint
                StatusMessage = "Volume list unavailable. Please use manual entry or recent volumes.";
                System.Diagnostics.Trace.WriteLine($"VolumeTree API not available (404): {httpEx.Message}");
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

        private VolumeTreeNode BuildUITreeNodeFromApiNode(Viking.Tokens.ApiVolumeTreeNode apiNode)
        {
            if (apiNode == null)
                return null;

            var uiNode = new VolumeTreeNode
            {
                Name = apiNode.Name ?? "Unnamed",
                IsOrganization = true,
                Children = new ObservableCollection<VolumeTreeNode>()
            };

            // Process Volumes (the leaves)
            if (apiNode.Volumes != null)
            {
                foreach (var volumePermission in apiNode.Volumes)
                {
                    try
                    {
                        var volumeInfo = ConvertUserResourcePermissionsToVolumeInfo(volumePermission);
                        var volumeNode = new VolumeTreeNode
                        {
                            Name = volumeInfo.Name,
                            Volume = volumeInfo,
                            IsOrganization = false
                        };
                        uiNode.Children.Add(volumeNode);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Error processing volume {volumePermission?.Id}: {ex.Message}");
                    }
                }
            }

            // Recursively process Children
            if (apiNode.Children != null)
            {
                foreach (var childApiNode in apiNode.Children)
                {
                    try
                    {
                        var childUiNode = BuildUITreeNodeFromApiNode(childApiNode);
                        if (childUiNode != null)
                        {
                            uiNode.Children.Add(childUiNode);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Error processing child node {childApiNode?.Name}: {ex.Message}");
                    }
                }
            }

            return uiNode;
        }

        private VolumeInfo ConvertUserResourcePermissionsToVolumeInfo(Viking.Tokens.UserResourcePermissions resourcePermissions)
        {
            var volumeInfo = new VolumeInfo
            {
                Id = resourcePermissions.Id,
                Name = resourcePermissions.Name ?? $"Volume {resourcePermissions.Id}"
            };

            // Extract Endpoint and Description from Metadata
            if (resourcePermissions.Metadata != null)
            {
                // Extract Endpoint (volume URL)
                if (resourcePermissions.Metadata.TryGetValue("Endpoint", out object endpointObj))
                {
                    if (endpointObj is string endpointStr)
                    {
                        volumeInfo.VolumeXmlUrl = endpointStr;
                    }
                    else if (endpointObj != null)
                    {
                        volumeInfo.VolumeXmlUrl = endpointObj.ToString();
                    }
                }

                // Extract Description
                if (resourcePermissions.Metadata.TryGetValue("Description", out object descObj))
                {
                    if (descObj is string descStr)
                    {
                        volumeInfo.Description = descStr;
                    }
                    else if (descObj != null)
                    {
                        volumeInfo.Description = descObj.ToString();
                    }
                }
            }

            System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Converted VolumeInfo: Id={volumeInfo.Id}, Name={volumeInfo.Name}, VolumeXmlUrl={volumeInfo.VolumeXmlUrl ?? "(null)"}, Description={volumeInfo.Description ?? "(null)"}");
            
            return volumeInfo;
        }

        private int CountVolumesInNode(VolumeTreeNode node)
        {
            if (node == null)
                return 0;

            int count = 0;
            if (node.Volume != null && !node.IsOrganization)
            {
                count = 1;
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    count += CountVolumesInNode(child);
                }
            }

            return count;
        }

        private void SelectVolume()
        {
            string selectedUrl = null;
            string selectedName = null;

            // Prioritize manual entry if provided
            if (!string.IsNullOrWhiteSpace(ManualVolumeUrl))
            {
                selectedUrl = ManualVolumeUrl;
                selectedName = null; // Will be loaded from XML
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Using manually entered volume: {selectedUrl}");
            }
            else if (SelectedVolume?.Volume != null)
            {
                selectedUrl = SelectedVolume.Volume.VolumeXmlUrl;
                selectedName = SelectedVolume.Volume.Name;
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] SelectedVolume.Volume exists - VolumeXmlUrl={selectedUrl ?? "(null)"}, Name={selectedName ?? "(null)"}");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] No volume selected (SelectedVolume={SelectedVolume}, Volume={SelectedVolume?.Volume})");
            }

            if (!string.IsNullOrWhiteSpace(selectedUrl))
            {
                // Ensure URL has proper format (add volume.vikingxml if needed)
                selectedUrl = AppendDefaultVolumeFilenameIfMissing(selectedUrl);
                
                System.Diagnostics.Trace.WriteLine($"[VolumeSelection] Final volume URL: {selectedUrl}, Name: {selectedName ?? "(null - will load from XML)"}");
                VolumeSelected?.Invoke(this, new VolumeSelectedEventArgs { Url = selectedUrl, Name = selectedName });
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
        public string Url { get; set; }

        public string Name { get;set;}
    }
}

