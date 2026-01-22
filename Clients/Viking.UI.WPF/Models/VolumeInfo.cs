using System;
using System.ComponentModel;

namespace Viking.UI.WPF.Models
{
    /// <summary>
    /// Represents a volume accessible to the user from the Identity Server API
    /// </summary>
    public class VolumeInfo : INotifyPropertyChanged
    {
        private long _id;
        private string _name;
        private string _organization;
        private string _volumeXmlUrl;
        private string _description;

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

        public string Organization
        {
            get => _organization;
            set
            {
                if (_organization != value)
                {
                    _organization = value;
                    OnPropertyChanged(nameof(Organization));
                }
            }
        }

        public string VolumeXmlUrl
        {
            get => _volumeXmlUrl;
            set
            {
                if (_volumeXmlUrl != value)
                {
                    _volumeXmlUrl = value;
                    OnPropertyChanged(nameof(VolumeXmlUrl));
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public override string ToString() => Name ?? $"Volume {Id}";
    }
}









