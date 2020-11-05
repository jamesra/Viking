using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.Controls
{
    public partial class PropertySheetControl : System.Windows.Forms.TabControl
    {
        public System.Type DisplayType
        {
            get { return _DisplayType; }
            set
            {
                if (_DisplayType != value)
                {
                    _DisplayType = value;
                    SetDisplayType(value);
                }
            }
        }

        private System.Type _DisplayType = null;

        public IUIObjectBasic ShownObject = null;

        [Browsable(false)]
        public System.Drawing.Size MaxTabSize = Size.Empty;

        [Browsable(false)]
        public IPropertyPage[] IPropertyPages
        {
            get
            {
                return IPageArray.ToArray();
            }
        }

        protected List<IPropertyPage> IPageArray = new List<IPropertyPage>();

        public PropertySheetControl()
        {
            InitializeComponent();
        }

        private void SetDisplayType(System.Type ObjType)
        {
            System.Type[] Types = ExtensionManager.GetPropertyPages(ObjType);
            Debug.Assert(Types != null);

            //this.CancelChanges();
            IPageArray.Clear();
            this.TabPages.Clear();

            foreach (System.Type T in Types)
            {
                IPropertyPage IPage = Activator.CreateInstance(T) as IPropertyPage;
                if (IPage != null)
                {
                    IPageArray.Add(IPage);

                    TabPage TPage = IPage.GetPage();

                    if (TPage != null)
                    {
                        if (TPage.Width > MaxTabSize.Width)
                            MaxTabSize.Width = TPage.Width;
                        if (TPage.Height > MaxTabSize.Height)
                            MaxTabSize.Height = TPage.Height;

                        //Disable all pages to start with
                        IPage.Enable(false);

                        this.TabPages.Add(TPage);
                    }
                }
            }
        }

        public void ShowObject(IUIObjectBasic Obj)
        {
            ShownObject = Obj;

            //Ensure our property pages are showing the correct type
            if (Obj != null)
            {
                this.DisplayType = Obj.GetType();
            }


            foreach (IPropertyPage IPage in this.IPropertyPages)
            {
                if (Obj == null)
                {
                    IPage.Enable(false);
                }
                else
                {

                    IPage.ShowObject(Obj);
                    IPage.Enable(true);
                }
            }
        }

        public bool CanSaveChanges()
        {
            bool bSaveOK = true;
            foreach (IPropertyPage IPage in IPropertyPages)
            {
                bSaveOK &= IPage.OnValidateChanges();
            }

            return bSaveOK;
        }

        /// <summary>
        /// Go through each property page and have them save changes
        /// Each page is responsible for showing it's own errors. 
        /// </summary>
        public void SaveChanges()
        {
            bool bSaveOK = true;
            foreach (IPropertyPage IPage in IPropertyPages)
            {
                bSaveOK &= IPage.OnValidateChanges();
            }

            if (bSaveOK == false)
                return;

            // create a transation for our changes

            //I have no clue how to emulate this in Viking yet
            //            Store.OpenConnection();
            //            Store.BeginTransaction();

            //            try
            //            {
            foreach (IPropertyPage IPage in IPropertyPages)
            {
                IPage.OnSaveChanges();
            }

            if (ShownObject != null)
                ShownObject.Save();

            // commit our changes
            //                Store.CommitTransaction();

            // Close the connection
            //                Store.CloseConnection();
            /*            }
                        catch (Exception E)
                        {
                            Store.RollbackTransaction();
                            Store.SqlConn.Close();
                            throw (E);
                        }
             */
        }

        public void CancelChanges()
        {
            foreach (IPropertyPage IPage in IPropertyPages)
            {
                IPage.OnCancelChanges();
                IPage.Reset();
            }
            /*
            if (ShownObject != null)
            {
                if (ShownObject.Deleted == false)
                    ShownObject.Row.RejectChanges();
            }
             * */
        }
    }
}
