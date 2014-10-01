using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using System.Diagnostics; 

using WebAnnotation.Objects;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(StructureBaseObj), 1)]
    public partial class StructureBaseGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        StructureObj Obj; 

        public StructureBaseGeneralPage()
        {
            InitializeComponent();
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as StructureObj;
            Debug.Assert(this.Obj != null);

            this.textID.Text= this.Obj.ID.ToString();

            this.checkVerified.Checked = this.Obj.Verified;

            this.numConfidence.Value = System.Convert.ToDecimal(this.Obj.Confidence); 

            this.textNotes.Text = this.Obj.Notes;

            this.listTags.Items.AddRange(this.Obj.Tags);
        }

        protected override void OnSaveChanges()
        {
            this.Obj.Verified = this.checkVerified.Checked;
            this.Obj.Confidence = System.Convert.ToDouble(this.numConfidence.Value);
            this.Obj.Notes = this.textNotes.Text;
        }
    }
}
