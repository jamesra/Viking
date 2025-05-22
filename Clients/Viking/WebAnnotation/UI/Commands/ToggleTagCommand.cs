using System;
using System.ServiceModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    internal class ToggleStructureTag : Viking.UI.Commands.Command
    {
        private readonly StructureObj target;
        private readonly string tag;
        private readonly string value;

        public ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent,
            StructureObj structure,
            string tag, string value)
            : base(parent)
        {
            target = structure;
            this.tag = tag;
            this.value = value;
        }

        public override void OnActivate()
        {
            Parent.BeginInvoke((Action)Execute);
        }

        protected override void Execute()
        {
            target.ToggleAttribute(tag, value);

            try
            {
                Store.Structures.Save();
            }
            catch (FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                target.ToggleAttribute(tag, value);
            }

            base.Execute();
        }
    }

    internal class ToggleLocationTag : Viking.UI.Commands.Command
    {
        private readonly LocationObj target;
        private readonly string tag;
        private readonly string value;

        public ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
            LocationObj loc,
            string tag, string value)
            : base(parent)
        {
            target = loc;
            this.tag = tag;
            this.value = value;
        }

        public override void OnActivate()
        {
            Parent.BeginInvoke((Action)Execute);
        }

        protected override void Execute()
        {
            target.ToggleAttribute(tag, value);
            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                target.ToggleAttribute(tag, value);
            }

            base.Execute();
        }
    }
}
