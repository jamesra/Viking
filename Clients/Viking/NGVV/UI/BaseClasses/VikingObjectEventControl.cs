using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Viking.UI.BaseClasses
{
    public partial class VikingObjectEventControl : VikingControl
    {
        /// <summary>
        /// When the user brings up the context menu and no item is obviously the source, such as 
        /// bringing up the context menu for an empty section of a list. DefaultContextMenuObject is
        /// used to create a context menu. If DefaultContextMenuObject is null, no menu is created. 
        /// </summary>
        protected Viking.Common.IUIObject DefaultContextMenuObject = null;

        #region Variables
        private System.EventHandler OnNewObjectEventHandler = null;
        private System.EventHandler BeforeAnyDeleteEventHandler = null;
        private System.EventHandler OnAnyDeleteEventHandler = null;
        private System.EventHandler OnAnySaveEventHandler = null;

        protected System.EventHandler BeforeAnySaveEventHandler = null;
        protected System.EventHandler BeforeDeleteEventHandler = null;
        protected System.EventHandler OnDeleteEventHandler = null;
        protected System.ComponentModel.PropertyChangedEventHandler OnValueChangeEventHandler = null;
        protected System.EventHandler OnSaveEventHandler = null;
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

        protected virtual void OnObjectDelete(object sender, System.EventArgs e)
        {
            Refresh();
        }

        protected virtual void OnAnySave(object sender, System.EventArgs e)
        {
            Refresh();
        }

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
