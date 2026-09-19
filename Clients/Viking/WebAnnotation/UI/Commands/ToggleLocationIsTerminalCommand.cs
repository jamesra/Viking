using System;
using System.ServiceModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    internal class ToggleLocationIsTerminalCommand(Viking.UI.Controls.SectionViewerControl parent,
                                     LocationObj loc) : Viking.UI.Commands.Command(parent)
    {
        private readonly LocationObj target = loc;

        public override void OnActivate() => Parent.BeginInvoke((Action)delegate () { Execute(); });

        protected override void Execute()
        {
            bool previous = target.Terminal;
            target.Terminal = !target.Terminal;
            try
            {
                Store.Locations.Save();
            }
            catch (FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                target.Terminal = previous;
            }

            base.Execute();
        }
    }
}
