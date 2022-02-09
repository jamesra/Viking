using System;
using System.ServiceModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    class ToggleStructureTag : Viking.UI.Commands.Command
    {
        private readonly StructureObj target;
        private readonly string tag;
        private readonly string value;
        
        public ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent,
            StructureObj structure,
            string tag, string value)
            : base(parent)
        {
            this.target = structure;
            this.tag = tag;
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
        private readonly LocationObj target;
        private readonly string tag;
        private readonly string value;
        
        public ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
            LocationObj loc,
            string tag, string value)
            : base(parent)
        {
            this.target = loc;
            this.tag = tag;
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
