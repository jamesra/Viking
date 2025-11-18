using System;
using System.ComponentModel;

namespace Viking.UI.WPF.Models
{
    /// <summary>
    /// Represents a segmentation service accessible to the user from the Identity Server API.
    /// </summary>
    public class SegmentationServiceInfo : INotifyPropertyChanged
    {
        private long _id;
        private string _name;
        private string _description;
        private string _endpoint;

        public long Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public string Endpoint
        {
            get => _endpoint;
            set
            {
                if (_endpoint != value)
                {
                    _endpoint = value;
                    OnPropertyChanged(nameof(Endpoint));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name;
            }

            return !string.IsNullOrWhiteSpace(Endpoint)
                ? Endpoint
                : $"Segmentation Service {Id}";
        }
    }
}


