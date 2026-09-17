using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Annotation.ViewModels.Commands;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;

namespace Annotation.ViewModels
{
    public class FavoriteStructureIDsViewModel : INotifyPropertyChanged
    {
        ObservableCollection<ulong> _FavoriteStructureTypeIDs = null;
        public ObservableCollection<ulong> FavoriteStructureTypeIDs
        {
            get => _FavoriteStructureTypeIDs;
            set
            {
                if (_FavoriteStructureTypeIDs == value)
                    return;

                _FavoriteStructureTypeIDs = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
            }
        }

        ObservableCollection<IStructureTypeReadOnly> _RootStructureTypes = null;
        public ObservableCollection<IStructureTypeReadOnly> RootStructureTypes
        {
            get => _RootStructureTypes;
            set
            {
                if (_RootStructureTypes == value)
                    return;

                _RootStructureTypes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RootStructureTypes"));
            }
        }

        public System.Windows.Input.ICommand DeleteFavoriteCommand { get; set; }

        public System.Windows.Input.ICommand AddFavoriteCommand { get; set; }

        public FavoriteStructureIDsViewModel()
        {
            DeleteFavoriteCommand = new DelegateCommand(DeleteFavorite, CanDeleteFavorite);
            AddFavoriteCommand = new DelegateCommand(AddFavorite, CanAddFavorite);
        }

        public FavoriteStructureIDsViewModel(ObservableCollection<ulong> Favorites = null, ObservableCollection<ulong> root_types = null) : this()
        {
            if (root_types is null)
            {
                Store.StructureTypes.TryGetObjectsByIDs(Store.StructureTypes.RootObjects, out var found, out _);
                _RootStructureTypes = new ObservableCollection<IStructureTypeReadOnly>(found.Cast<IStructureTypeReadOnly>());
            }

            FavoriteStructureTypeIDs = Favorites;
        }

        public bool CanDeleteFavorite(object item)
        {
            if (item is IStructureTypeReadOnly TypeObj)
            {
                return FavoriteStructureTypeIDs.Contains(TypeObj.ID);
            }
            else
            {
                return FavoriteStructureTypeIDs.Contains(System.Convert.ToUInt64(item));
            }

            return FavoriteStructureTypeIDs.Contains(System.Convert.ToUInt64(item));
        }

        public void DeleteFavorite(object item)
        {
            FavoriteStructureTypeIDs.Remove(System.Convert.ToUInt64(item));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
        }

        public bool CanAddFavorite(object item)
        {
            if (item is IStructureTypeReadOnly TypeObj)
            {
                return FavoriteStructureTypeIDs.Contains(TypeObj.ID) == false;
            }
            else
            {
                return FavoriteStructureTypeIDs.Contains(System.Convert.ToUInt64(item)) == false;
            }
        }

        public void AddFavorite(object item)
        {
            FavoriteStructureTypeIDs.Add(System.Convert.ToUInt64(item));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
