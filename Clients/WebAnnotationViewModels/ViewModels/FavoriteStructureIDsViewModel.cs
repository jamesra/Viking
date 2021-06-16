using Viking.AnnotationServiceTypes.Interfaces;
using Annotation.ViewModels.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WebAnnotationModel;

namespace Annotation.ViewModels
{
    public class FavoriteStructureIDsViewModel : INotifyPropertyChanged
    {
        ObservableCollection<ulong> _FavoriteStructureTypeIDs = null;
        public ObservableCollection<ulong> FavoriteStructureTypeIDs
        {
            get
            {
                return _FavoriteStructureTypeIDs;
            }
            set
            {
                if (_FavoriteStructureTypeIDs == value)
                    return;

                _FavoriteStructureTypeIDs = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
                }
            }
        }

        ObservableCollection<IStructureType> _RootStructureTypes = null; 
        public ObservableCollection<IStructureType> RootStructureTypes
        {
            get
            {
                return _RootStructureTypes;
            }
            set
            {
                if (_RootStructureTypes == value)
                    return;

                _RootStructureTypes = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("RootStructureTypes"));
                }
            }
        }

        public System.Windows.Input.ICommand DeleteFavoriteCommand { get; set; }

        public System.Windows.Input.ICommand AddFavoriteCommand { get; set; }

        public FavoriteStructureIDsViewModel()
        {
            DeleteFavoriteCommand = new DelegateCommand (DeleteFavorite, CanDeleteFavorite);
            AddFavoriteCommand = new DelegateCommand(AddFavorite, CanAddFavorite);
        }

        public bool CanDeleteFavorite(object item)
        {
            if (item is IStructureType TypeObj)
            {
                return FavoriteStructureTypeIDs.Contains(TypeObj.ID) ;
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
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
            }
        }

        public bool CanAddFavorite(object item)
        {
            if(item is IStructureType TypeObj)
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
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
            }
        }


        public FavoriteStructureIDsViewModel(ObservableCollection<ulong> Favorites = null, ObservableCollection<ulong> root_types = null) : this()
        {
            if (root_types == null)
                _RootStructureTypes = new ObservableCollection<IStructureType>(Store.StructureTypes.GetObjectsByIDs(Store.StructureTypes.RootObjects, true));

            FavoriteStructureTypeIDs = Favorites;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
