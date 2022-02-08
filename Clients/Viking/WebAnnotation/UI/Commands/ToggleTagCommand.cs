using System;
using System.ServiceModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    class ToggleStructureTag : Viking.UI.Commands.Command
    {
        readonly StructureObj target;
        readonly string tag;
        private readonly string value;

        public ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent,
                                         StructureObj structure,
                                         string Tag, bool setValueToUsername = false)
            : base(parent)
        {
            this.target = structure;
            this.tag = Tag;
            this.value = setValueToUsername ? WebAnnotationModel.State.UserCredentials.UserName : null;
        }

        public ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent,
            StructureObj structure,
            string Tag, string value)
            : base(parent)
        {
            this.target = structure;
            this.tag = Tag;
            this.value = value;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)this.Execute);
        }

        protected override void Execute()
        {
            target.ToggleAttribute(this.tag, this.value);

            try
            {
                Store.Structures.Save();
            }
            catch (FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                target.ToggleAttribute(this.tag, this.value);
            }

            base.Execute();
        }
    }

    class ToggleLocationTag : Viking.UI.Commands.Command
    {
        readonly LocationObj target;
        readonly string tag;
        private readonly string value;

        public ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
                                         LocationObj loc,
                                         string Tag, bool setValueToUsername = false)
            : base(parent)
        {
            this.target = loc;
            this.tag = Tag;
            this.value = setValueToUsername ? WebAnnotationModel.State.UserCredentials.UserName : null;
        }

        public ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
            LocationObj loc,
            string Tag, string Value)
            : base(parent)
        {
            this.target = loc;
            this.tag = Tag;
            this.value = Value;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)this.Execute);
        }

        protected override void Execute()
        { 
            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                target.ToggleAttribute(this.tag, value);
            }

            base.Execute();
        }
    }
}
