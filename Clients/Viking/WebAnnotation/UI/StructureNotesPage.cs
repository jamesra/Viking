using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using System.Diagnostics;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 2)] 
    public partial class StructureNotesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        Structure Obj;

        public StructureNotesPage()
        {
            this.Title = "Notes";
            InitializeComponent();
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as Structure;
            Debug.Assert(this.Obj != null); 

            if(null != this.Obj.Notes)
                this.textNotes.Text = this.Obj.Notes;
        }

        protected override void OnSaveChanges()
        {
            this.Obj.Notes = this.textNotes.Text;
        }
    }
}
