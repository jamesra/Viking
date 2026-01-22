using System;

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
            target.Terminal = !target.Terminal;
            System.Threading.Tasks.Task t = new(() => WebAnnotation.AnnotationOverlay.SaveLocationsWithMessageBoxOnError());
            base.Execute();
        }
    }
}
