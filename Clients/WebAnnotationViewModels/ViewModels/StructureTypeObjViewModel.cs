using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using System.Windows;
using System.ComponentModel;
using System.Collections.ObjectModel;
using WebAnnotationModel.Objects;
using Annotation.ViewModels.Commands;
using System.Diagnostics;

namespace Annotation.ViewModels 
{
    public class ClearStructureTypeParentCommand : System.Windows.Input.ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object item)
        {
            StructureTypeObj obj = item as StructureTypeObj;
            if (obj == null)
            {
                long ID;
                try
                {
                    ID = System.Convert.ToInt64(item);
                }
                catch
                {
                    Trace.WriteLine(string.Format("Could not convert parameter {0} to StructureTypeObj", item));
                    return false;
                }

                obj = Store.StructureTypes.GetObjectByID(ID);
            }

            if (obj == null)
                return false;

            return obj.ParentID.HasValue;
        }

        public void Execute(object item)
        {
            StructureTypeObj obj = item as StructureTypeObj;
            if (obj == null)
            {
                long ID;
                try
                {
                    ID = System.Convert.ToInt64(item);
                }
                catch
                {
                    Trace.WriteLine(string.Format("Could not convert parameter {0} to StructureTypeObj", item));
                    return;
                }

                obj = Store.StructureTypes.GetObjectByID(ID);
            }

            if (obj == null)
                return;

            obj.ParentID = null;
        }
    }

    public class StructureTypeObjViewModel : DependencyObject, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public StructureTypeObj Model
        {
            get { return (StructureTypeObj)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for structureTypeObj.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(StructureTypeObj), typeof(StructureTypeObjViewModel), new PropertyMetadata());

        public System.Windows.Input.ICommand AssignParentCommand { get; set; }

        public ObservableCollection<PermittedStructureLinkObj> NewPermits
        {
            get { return (ObservableCollection<PermittedStructureLinkObj>)GetValue(NewPermitsProperty); }
            set { SetValue(NewPermitsProperty, value); }
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


        public StructureTypeObjViewModel(StructureTypeObj model)
        {
            AssignParentCommand = new DelegateCommand<StructureTypeObj>(AssignParent, CanAssignParent);
            DeletePermittedLinkSourceTypeCommand = new DelegateCommand(DeletePermittedLinkSourceType, CanDeletePermittedLinkSourceType);
            DeletePermittedLinkTargetTypeCommand = new DelegateCommand(DeletePermittedLinkTargetType, CanDeletePermittedLinkTargetType);
            DeletePermittedLinkBidirectionalTypeCommand = new DelegateCommand(DeletePermittedLinkBidirectionalType, CanDeletePermittedLinkBidirectionalType);

            AddPermittedLinkSourceTypeCommand = new DelegateCommand(AddPermittedLinkSourceType, CanAddPermittedLinkSourceType);
            AddPermittedLinkTargetTypeCommand = new DelegateCommand(AddPermittedLinkTargetType, CanAddPermittedLinkTargetType);
            AddPermittedLinkBidirectionalTypeCommand = new DelegateCommand(AddPermittedLinkBidirectionalType, CanAddPermittedLinkBidirectionalType);

            SaveModelCommand = new DelegateCommand(SaveModel, CanSaveModel);
            ResetModelCommand = new DelegateCommand(RestoreModel, CanRestoreModel);

            Model = model;
            Model.PermittedLinks.CollectionChanged += OnPermittedLinksCollectionChanged;
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
                    oldObj.PermittedLinks.CollectionChanged -= viewmodel.OnPermittedLinksCollectionChanged;
                }

                if (newObj != null)
                {
                    newObj.PermittedLinks.CollectionChanged += viewmodel.OnPermittedLinksCollectionChanged;
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

        public long[] PermittedLinkSourceTypes
        {
            get
            {
                return Model.PermittedLinks.Where(pl => pl.TargetTypeID == Model.ID && pl.Bidirectional == false).Select(pl => pl.SourceTypeID).ToArray();
            }
        }

        public long[] PermittedLinkTargetTypes
        {
            get
            {
                return Model.PermittedLinks.Where(pl => pl.SourceTypeID == Model.ID && pl.Bidirectional == false).Select(pl => pl.TargetTypeID).ToArray();
            }
        }

        public long[] PermittedLinkBidirectionalTypes
        {
            get
            {
                return Model.PermittedLinks.Where(pl => (pl.SourceTypeID == Model.ID || pl.TargetTypeID == Model.ID) && pl.Bidirectional == true).Select(pl => pl.SourceTypeID == Model.ID ? pl.TargetTypeID : pl.SourceTypeID).ToArray();
            }
        }

        private bool CanAssignParent(StructureTypeObj arg)
        {
            if(arg == null && Model.ParentID.HasValue == false)
                return false;

            return true; 
        }

        private void AssignParent(StructureTypeObj obj)
        {
            Model.Parent = obj;
        }

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

            PermittedStructureLinkKey key = new PermittedStructureLinkKey(ID, Model.ID, false);

            var obj = Store.PermittedStructureLinks.GetObjectByID(key, false);
            if (NewPermits.Contains(obj))
                NewPermits.Remove(obj);

            Store.PermittedStructureLinks.Remove(key);
        }

        private bool CanDeletePermittedLinkSourceType(object item)
        {
            return true;
        }

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

            PermittedStructureLinkKey key = new PermittedStructureLinkKey(Model.ID, ID, false);
            var obj = Store.PermittedStructureLinks.GetObjectByID(key, false);
            if (NewPermits.Contains(obj))
                NewPermits.Remove(obj);

            Store.PermittedStructureLinks.Remove(key);
        }

        private bool CanDeletePermittedLinkTargetType(object item)
        {
            return true;
        }

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

            PermittedStructureLinkKey key = new PermittedStructureLinkKey(Model.ID, ID, true);
            var obj = Store.PermittedStructureLinks.GetObjectByID(key, false);
            if (NewPermits.Contains(obj))
                NewPermits.Remove(obj);

            Store.PermittedStructureLinks.Remove(key);
        }

        private bool CanDeletePermittedLinkBidirectionalType(object item)
        {
            return true;
        }

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

            PermittedStructureLinkObj key = new PermittedStructureLinkObj(ID, Model.ID, false);
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

            PermittedStructureLinkObj key = new PermittedStructureLinkObj(Model.ID, ID, false);
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

            PermittedStructureLinkObj key = new PermittedStructureLinkObj(Model.ID, ID, true);
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
            return Model.DBAction != AnnotationService.Types.DBACTION.NONE;
        }

        private void SaveModel(object item)
        {
            Store.StructureTypes.Save();

            foreach (PermittedStructureLinkObj newObj in NewPermits)
            {
                Store.PermittedStructureLinks.Create(newObj);
            }
        }

        private bool CanRestoreModel(object item)
        {
            return Model.DBAction != AnnotationService.Types.DBACTION.NONE;
        }

        private void RestoreModel(object item)
        {
            Store.StructureTypes.GetObjectByID(Model.ID, AskServer: true, ForceRefreshFromServer: true);
        }
    }
}
