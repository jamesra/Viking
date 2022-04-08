using System;
using Viking.AnnotationServiceTypes.Interfaces;
using Annotation.ViewModels.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
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
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("FavoriteStructureTypeIDs"));
                }
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
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("RootStructureTypes"));
                }
            }
        }

        public System.Windows.Input.ICommand DeleteFavoriteCommand { get; set; }

        public System.Windows.Input.ICommand AddFavoriteCommand { get; set; }

        private IStructureTypeStore StructureTypeStore;
        public FavoriteStructureIDsViewModel(IStructureTypeStore structureTypeStore, ObservableCollection<ulong> Favorites = null, ObservableCollection<ulong> root_types = null)
        {
            StructureTypeStore = structureTypeStore ?? throw new ArgumentNullException(nameof(structureTypeStore));

            DeleteFavoriteCommand = new DelegateCommand (DeleteFavorite, CanDeleteFavorite);
            AddFavoriteCommand = new DelegateCommand(AddFavorite, CanAddFavorite);

            if (root_types == null)
                _RootStructureTypes = new ObservableCollection<IStructureTypeReadOnly>(StructureTypeStore.GetObjectsByIDs(StructureTypeStore.RootObjects, true, CancellationToken.None).Result);

            FavoriteStructureTypeIDs = Favorites;
        }
        

        public bool CanDeleteFavorite(object item)
        {
            if (item is IStructureTypeReadOnly TypeObj)
            {
                return FavoriteStructureTypeIDs.Contains(TypeObj.ID) ;
            }
            else
            {
                return FavoriteStructureTypeIDs.Contains(System.Convert.ToUInt64(item));
            } 
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
            if(item is IStructureTypeReadOnly TypeObj)
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


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
