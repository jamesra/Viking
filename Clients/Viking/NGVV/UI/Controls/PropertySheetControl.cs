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
        public System.Type? DisplayType
        {
            get => _DisplayType;
            set
            {
                if (_DisplayType != value)
                {
                    _DisplayType = value;
                    if (value != null)
                        SetDisplayType(value);
                }
            }
        }

        private System.Type? _DisplayType = null;

        public IUIObjectBasic? ShownObject = null;

        [Browsable(false)]
        public Size MaxTabSize => _maxTabSize;

        private Size _maxTabSize = Size.Empty;

        [Browsable(false)]
        public IPropertyPage[] IPropertyPages => [.. IPageArray];

        protected List<IPropertyPage> IPageArray = [];

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
                if (Activator.CreateInstance(T) is IPropertyPage IPage)
                {
                    IPageArray.Add(IPage);

                    TabPage TPage = IPage.GetPage();

                    if (TPage != null)
                    {
                        //Disable all pages to start with
                        IPage.Enable(false);

                        this.TabPages.Add(TPage);
                        UpdateMaxTabSizeForPage(TPage);
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
                if (Obj is null)
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

            ShownObject?.Save();

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

        public Size RecalculateMaxTabSize()
        {
            _maxTabSize = Size.Empty;
            foreach (TabPage tab in this.TabPages)
            {
                UpdateMaxTabSizeForPage(tab);
            }
            return _maxTabSize;
        }

        private void UpdateMaxTabSizeForPage(TabPage tabPage)
        {
            if (tabPage is null)
            {
                return;
            }

            Size candidate = tabPage.Padding.Size;

            if (tabPage.Controls.Count > 0)
            {
                Control child = tabPage.Controls[0];

                DockStyle originalDock = child.Dock;
                try
                {
                    // Temporarily remove docking to get an accurate preferred size
                    if (originalDock == DockStyle.Fill)
                    {
                        child.Dock = DockStyle.None;
                    }

                    Size preferred = child.GetPreferredSize(Size.Empty);
                    if (preferred.IsEmpty)
                    {
                        preferred = child.Size;
                    }

                    candidate.Width = Math.Max(candidate.Width, preferred.Width);
                    candidate.Height = Math.Max(candidate.Height, preferred.Height);
                }
                finally
                {
                    child.Dock = originalDock;
                }
            }

            // Fallback to tab preferred size if child measurement fails
            Size tabPreferred = tabPage.GetPreferredSize(Size.Empty);
            if (!tabPreferred.IsEmpty)
            {
                candidate.Width = Math.Max(candidate.Width, tabPreferred.Width);
                candidate.Height = Math.Max(candidate.Height, tabPreferred.Height);
            }

            _maxTabSize.Width = Math.Max(_maxTabSize.Width, candidate.Width);
            _maxTabSize.Height = Math.Max(_maxTabSize.Height, candidate.Height);
        }
    }
}
