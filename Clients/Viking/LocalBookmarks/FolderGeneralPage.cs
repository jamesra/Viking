using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using connectomes.utah.edu.XSD.BookmarkSchema.xsd;

namespace LocalBookmarks
{
    [Viking.Common.PropertyPage(typeof(FolderUIObj))]
    public partial class FolderGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        FolderUIObj folder;

        public FolderGeneralPage()
        {
            InitializeComponent();

            textName.Focus();
        }

        protected override void OnShowObject(object Object)
        {
            folder = Object as FolderUIObj;
            textName.Text = folder.Name; 
        }

        protected override void OnInitPage()
        {
            textName.Text = folder.Name; 
        }

        protected override void OnSaveChanges()
        {
            folder.Name = textName.Text;
        }

        protected override void OnCancelChanges()
        {
            return;
        }


    }
}
