using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Viking.Tokens;

namespace Viking.UI.WPF.ViewModels
{
    /// <summary>
    /// Login stage when Identity links more than one annotation server to the chosen volume.
    /// Preselects the default; the user may accept it or pick another before volume JWT minting.
    /// </summary>
    public class AnnotationServerSelectionViewModel : INotifyPropertyChanged
    {
        private AnnotationServerChoice _selectedServer;
        private string _statusMessage;

        /// <summary>
        /// Builds a picker from the AccessibleVolumes <c>AnnotationServers</c> list.
        /// </summary>
        public AnnotationServerSelectionViewModel(IEnumerable<AnnotationServerChoice> servers)
        {
            Servers = new ObservableCollection<AnnotationServerChoice>(
                (servers ?? Array.Empty<AnnotationServerChoice>())
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.AnnotationEndpoint))
                    .OrderByDescending(s => s.IsDefault)
                    .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase));

            SelectedServer = Servers.FirstOrDefault(s => s.IsDefault) ?? Servers.FirstOrDefault();
            StatusMessage = Servers.Count == 0
                ? "No annotation servers were listed for this volume."
                : $"Choose an annotation server ({Servers.Count} available).";

            SelectCommand = new RelayCommand(Select, () => SelectedServer != null);
            CancelCommand = new RelayCommand(Cancel);
        }

        public ObservableCollection<AnnotationServerChoice> Servers { get; }

        public AnnotationServerChoice SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (!ReferenceEquals(_selectedServer, value))
                {
                    _selectedServer = value;
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

        public ICommand SelectCommand { get; }
        public ICommand CancelCommand { get; }

        public event EventHandler<AnnotationServerChoice> ServerSelected;
        public event EventHandler SelectionCancelled;
        public event PropertyChangedEventHandler PropertyChanged;

        private void Select()
        {
            if (SelectedServer != null)
                ServerSelected?.Invoke(this, SelectedServer);
        }

        private void Cancel() => SelectionCancelled?.Invoke(this, EventArgs.Empty);

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
