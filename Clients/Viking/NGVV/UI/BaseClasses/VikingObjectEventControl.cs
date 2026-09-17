namespace Viking.UI.BaseClasses
{
    public partial class VikingObjectEventControl : VikingControl
    {


        #region Variables
        private readonly System.EventHandler? OnNewObjectEventHandler = null;
        private readonly System.EventHandler? BeforeAnyDeleteEventHandler = null;
        private readonly System.EventHandler? OnAnyDeleteEventHandler = null;
        private readonly System.EventHandler? OnAnySaveEventHandler = null;

        protected System.EventHandler? BeforeAnySaveEventHandler = null;
        protected System.EventHandler? BeforeDeleteEventHandler = null;
        protected System.EventHandler? OnDeleteEventHandler = null;
        protected System.ComponentModel.PropertyChangedEventHandler? OnValueChangeEventHandler = null;
        protected System.EventHandler? OnSaveEventHandler = null;
        #endregion


        public VikingObjectEventControl() : base()
        {
            //         InitializeComponent();

            OnNewObjectEventHandler = new System.EventHandler(this.OnNewObject);
            BeforeAnyDeleteEventHandler = new System.EventHandler(this.BeforeAnyDelete);
            OnAnyDeleteEventHandler = new System.EventHandler(this.OnAnyDelete);
            BeforeAnySaveEventHandler = new System.EventHandler(this.BeforeAnySave);
            OnAnySaveEventHandler = new System.EventHandler(this.OnAnySave);
            /*
                        DBObject.OnNewObject += OnNewObjectEventHandler;
                        DBObject.BeforeAnyDelete += BeforeAnyDeleteEventHandler;
                        DBObject.OnAnyDelete += OnAnyDeleteEventHandler;
                        DBObject.BeforeAnySave += BeforeAnySaveEventHandler;
                        DBObject.OnAnySave += OnAnySaveEventHandler;
            */
            OnSaveEventHandler = new System.EventHandler(this.OnObjectSave);
            BeforeDeleteEventHandler = new System.EventHandler(this.BeforeObjectDelete);
            OnDeleteEventHandler = new System.EventHandler(this.OnObjectDelete);
            OnValueChangeEventHandler = new System.ComponentModel.PropertyChangedEventHandler(this.OnObjectValueChanged);
        }

        protected virtual void OnNewObject(object sender, System.EventArgs e)
        {
        }

        protected virtual void BeforeAnyDelete(object sender, System.EventArgs e)
        {
        }

        protected virtual void OnAnyDelete(object sender, System.EventArgs e)
        {
        }

        protected virtual void BeforeObjectDelete(object sender, System.EventArgs e)
        {
        }

        protected virtual void OnObjectDelete(object sender, System.EventArgs e) => Refresh();

        protected virtual void OnAnySave(object sender, System.EventArgs e) => Refresh();

        protected virtual void BeforeAnySave(object sender, System.EventArgs e)
        {
        }

        protected void OnObjectValueChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }

        protected virtual void OnObjectSave(object sender, System.EventArgs e)
        {
        }
    }
}
