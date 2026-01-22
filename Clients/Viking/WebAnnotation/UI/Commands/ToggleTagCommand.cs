using System;
using System.ServiceModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    internal class ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent,
        StructureObj structure,
        string tag, string value) : Viking.UI.Commands.Command(parent)
    {
        private readonly StructureObj target = structure;
        private readonly string tag = tag;
        private readonly string value = value;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

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

    internal class ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
        LocationObj loc,
        string tag, string value) : Viking.UI.Commands.Command(parent)
    {
        private readonly LocationObj target = loc;
        private readonly string tag = tag;
        private readonly string value = value;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

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
