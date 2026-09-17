using Annotation.ViewModels.Commands;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace Annotation.ViewModels
{
    public class PermittedStructureLinkViewModel : DependencyObject, INotifyPropertyChanged
    {
        public StructureTypeObj Model
        {
            get => (StructureTypeObj)GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }

        // Using a DependencyProperty as the backing store for structureTypeObj.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(StructureTypeObj), typeof(PermittedStructureLinkViewModel), new PropertyMetadata());

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<PermittedStructureLinkObj> NewPermits
        {
            get => (ObservableCollection<PermittedStructureLinkObj>)GetValue(NewPermitsProperty);
            set => SetValue(NewPermitsProperty, value);
        }

        // Using a DependencyProperty as the backing store for NewPermits.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NewPermitsProperty =
            DependencyProperty.Register("NewPermits", typeof(ObservableCollection<PermittedStructureLinkObj>), typeof(PermittedStructureLinkViewModel), new PropertyMetadata(new ObservableCollection<PermittedStructureLinkObj>()));

        public System.Windows.Input.ICommand AddPermittedLinkSourceTypeCommand { get; set; }
        public System.Windows.Input.ICommand AddPermittedLinkTargetTypeCommand { get; set; }
        public System.Windows.Input.ICommand AddPermittedLinkBidirectionalTypeCommand { get; set; }

        public System.Windows.Input.ICommand DeletePermittedLinkSourceTypeCommand { get; set; }
        public System.Windows.Input.ICommand DeletePermittedLinkTargetTypeCommand { get; set; }
        public System.Windows.Input.ICommand DeletePermittedLinkBidirectionalTypeCommand { get; set; }

        public System.Windows.Input.ICommand SaveModelCommand { get; set; }
        public System.Windows.Input.ICommand ResetModelCommand { get; set; }

        public PermittedStructureLinkViewModel(StructureTypeObj model)
        {
            DeletePermittedLinkSourceTypeCommand = new DelegateCommand(DeletePermittedLinkSourceType, CanDeletePermittedLinkSourceType);
            DeletePermittedLinkTargetTypeCommand = new DelegateCommand(DeletePermittedLinkTargetType, CanDeletePermittedLinkTargetType);
            DeletePermittedLinkBidirectionalTypeCommand = new DelegateCommand(DeletePermittedLinkBidirectionalType, CanDeletePermittedLinkBidirectionalType);

            AddPermittedLinkSourceTypeCommand = new DelegateCommand(AddPermittedLinkSourceType, CanAddPermittedLinkSourceType);
            AddPermittedLinkTargetTypeCommand = new DelegateCommand(AddPermittedLinkTargetType, CanAddPermittedLinkTargetType);
            AddPermittedLinkBidirectionalTypeCommand = new DelegateCommand(AddPermittedLinkBidirectionalType, CanAddPermittedLinkBidirectionalType);

            SaveModelCommand = new DelegateCommand(SaveModel, CanSaveModel);
            ResetModelCommand = new DelegateCommand(RestoreModel, CanRestoreModel);

            Model = model;
            ((INotifyCollectionChanged)Model.PermittedLinks).CollectionChanged += OnPermittedLinksCollectionChanged;
        }

        public static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PermittedStructureLinkViewModel viewmodel = d as PermittedStructureLinkViewModel;

            if (e.Property == ModelProperty)
            {
                StructureTypeObj oldObj = e.OldValue as StructureTypeObj;
                StructureTypeObj newObj = e.NewValue as StructureTypeObj;

                if (oldObj != null)
                {
                    ((INotifyCollectionChanged)oldObj.PermittedLinks).CollectionChanged -= viewmodel.OnPermittedLinksCollectionChanged;
                }

                if (newObj != null)
                {
                    ((INotifyCollectionChanged)newObj.PermittedLinks).CollectionChanged += viewmodel.OnPermittedLinksCollectionChanged;
                }
            }
        }

        public void OnPermittedLinksCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("PermittedLinkSourceTypes"));
                PropertyChanged(this, new PropertyChangedEventArgs("PermittedLinkTargetTypes"));
                PropertyChanged(this, new PropertyChangedEventArgs("PermittedLinkBidirectionalTypes"));
            }
        }

        public long[] PermittedLinkSourceTypes => [.. Model.PermittedLinks.Where(pl => pl.TargetTypeID == Model.ID && pl.Bidirectional == false).Select(pl => pl.SourceTypeID)];

        public long[] PermittedLinkTargetTypes => [.. Model.PermittedLinks.Where(pl => pl.SourceTypeID == Model.ID && pl.Bidirectional == false).Select(pl => pl.TargetTypeID)];

        public long[] PermittedLinkBidirectionalTypes => [.. Model.PermittedLinks.Where(pl => (pl.SourceTypeID == Model.ID || pl.TargetTypeID == Model.ID) && pl.Bidirectional == true).Select(pl => pl.SourceTypeID == Model.ID ? pl.TargetTypeID : pl.SourceTypeID)];

        #region Delete commands
        private void DeletePermittedLinkSourceType(object item)
        {
            long ID;
            try
            {
                ID = System.Convert.ToInt64(item);
            }
            catch
            {
                Trace.WriteLine(string.Format("Could not convert parameter to ID {0}", item));
                return;
            }

            PermittedStructureLinkKey key = new(ID, Model.ID, false);

            Store.PermittedStructureLinks.TryGetObjectByID(key, out var obj);
            if (NewPermits.Contains(obj))
                NewPermits.Remove(obj);

            Store.PermittedStructureLinks.Remove(key);
        }

        private bool CanDeletePermittedLinkSourceType(object item) => true;

        private void DeletePermittedLinkTargetType(object item)
        {
            long ID;
            try
            {
                ID = System.Convert.ToInt64(item);
            }
            catch
            {
                Trace.WriteLine(string.Format("Could not convert parameter to ID {0}", item));
                return;
            }

            PermittedStructureLinkKey key = new(Model.ID, ID, false);
            Store.PermittedStructureLinks.TryGetObjectByID(key, out var obj);
            if (NewPermits.Contains(obj))
                NewPermits.Remove(obj);

            Store.PermittedStructureLinks.Remove(key);
        }

        private bool CanDeletePermittedLinkTargetType(object item) => true;

        private void DeletePermittedLinkBidirectionalType(object item)
        {
            long ID;
            try
            {
                ID = System.Convert.ToInt64(item);
            }
            catch
            {
                Trace.WriteLine(string.Format("Could not convert parameter to ID {0}", item));
                return;
            }

            PermittedStructureLinkKey key = new(Model.ID, ID, true);
            Store.PermittedStructureLinks.TryGetObjectByID(key, out var obj);
            if (NewPermits.Contains(obj))
                NewPermits.Remove(obj);

            Store.PermittedStructureLinks.Remove(key);
        }

        private bool CanDeletePermittedLinkBidirectionalType(object item) => true;

        #endregion

        private static long ParamterToStructureTypeID(object item)
        {
            long ID;

            if (item is StructureTypeObj stype)
            {
                ID = stype.ID;
            }
            else
            {
                try
                {
                    ID = System.Convert.ToInt64(item);
                }
                catch
                {
                    Trace.WriteLine(string.Format("Could not convert parameter to ID {0}", item));
                    throw;
                }
            }

            return ID;
        }

        #region Add commands
        private void AddPermittedLinkSourceType(object item)
        {
            long ID = ParamterToStructureTypeID(item);

            PermittedStructureLinkObj key = new(ID, Model.ID, false);
            Store.PermittedStructureLinks.Add(key);
        }

        private bool CanAddPermittedLinkSourceType(object item)
        {
            long ID = ParamterToStructureTypeID(item);
            return Model.PermittedLinkSourceTypes.Contains(ID) == false;
        }

        private void AddPermittedLinkTargetType(object item)
        {
            long ID = ParamterToStructureTypeID(item);

            PermittedStructureLinkObj key = new(Model.ID, ID, false);
            Store.PermittedStructureLinks.Add(key);
        }

        private bool CanAddPermittedLinkTargetType(object item)
        {
            long ID = ParamterToStructureTypeID(item);
            return Model.PermittedLinkTargetTypes.Contains(ID) == false;
        }

        private void AddPermittedLinkBidirectionalType(object item)
        {
            long ID = ParamterToStructureTypeID(item);

            PermittedStructureLinkObj key = new(Model.ID, ID, true);
            Store.PermittedStructureLinks.Add(key);
        }

        #endregion

        private bool CanAddPermittedLinkBidirectionalType(object item)
        {
            long ID = ParamterToStructureTypeID(item);
            return Model.PermittedLinkBidirectionalTypes.Contains(ID) == false;
        }

        private bool CanSaveModel(object item)
        {
            return true;
        }

        private void SaveModel(object item)
        {
            Store.StructureTypes.Save(CancellationToken.None).Wait();

            foreach (PermittedStructureLinkObj newObj in NewPermits)
            {
                Store.PermittedStructureLinks.Add(newObj);
            }
        }

        private bool CanRestoreModel(object item) => Model.DBAction != DBACTION.NONE;

        private void RestoreModel(object item) => _ = Store.StructureTypes.Refresh(Model.ID);
    }
}
