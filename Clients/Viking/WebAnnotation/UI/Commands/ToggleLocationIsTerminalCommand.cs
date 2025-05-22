using System;

using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    internal class ToggleLocationIsTerminalCommand : Viking.UI.Commands.Command
    {
        private readonly LocationObj target;
        public ToggleLocationIsTerminalCommand(Viking.UI.Controls.SectionViewerControl parent,
                                         LocationObj loc)
            : base(parent)
        {
            target = loc;
        }

        public override void OnActivate()
        {
            Parent.BeginInvoke((Action)delegate () { Execute(); });
        }

        protected override void Execute()
        {
            target.Terminal = !target.Terminal;
            System.Threading.Tasks.Task t = new System.Threading.Tasks.Task(() => WebAnnotation.AnnotationOverlay.SaveLocationsWithMessageBoxOnError());
            base.Execute();
        }
    }
}
