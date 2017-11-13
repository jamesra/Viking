using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using WebAnnotationModel;
using WebAnnotation.ViewModel; 

namespace WebAnnotation.UI.Commands
{
    class ToggleStructureTag : Viking.UI.Commands.Command
    {
        StructureObj target;
        string tag;
        bool SetValueToUsername = false;
        public ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent, 
                                         StructureObj structure,
                                         string Tag, bool setValueToUsername)
            : base(parent)
        {
            this.target = structure;
            this.tag = Tag;
            this.SetValueToUsername = setValueToUsername;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate() { this.Execute(); });
        }

        protected override void Execute()
        {
            if (this.SetValueToUsername)
            {
                target.ToggleAttribute(this.tag, WebAnnotationModel.State.UserCredentials.UserName);
            }
            else
            {
                target.ToggleAttribute(this.tag);
            }

            Store.Structures.Save();
            base.Execute();
        }
    }

    class ToggleLocationTag : Viking.UI.Commands.Command
    {
        LocationObj target;
        string tag;
        bool SetValueToUsername = false;
        public ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
                                         LocationObj loc,
                                         string Tag, bool setValueToUsername)
            : base(parent)
        {
            this.target = loc;
            this.tag = Tag;
            this.SetValueToUsername = setValueToUsername;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            if(this.SetValueToUsername)
            {
                target.ToggleAttribute(this.tag, WebAnnotationModel.State.UserCredentials.UserName);
            }
            else
            {
                target.ToggleAttribute(this.tag);
            }
            
            Store.Locations.Save();
            base.Execute();
        }
    }
}
