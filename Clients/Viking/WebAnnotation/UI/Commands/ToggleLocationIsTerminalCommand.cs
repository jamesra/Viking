using System;

using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    class ToggleLocationIsTerminalCommand : Viking.UI.Commands.Command
    {
        LocationObj target;
        public ToggleLocationIsTerminalCommand(Viking.UI.Controls.SectionViewerControl parent,
                                         LocationObj loc)
            : base(parent)
        {
            this.target = loc;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            target.Terminal = !target.Terminal;
            var t = new System.Threading.Tasks.Task(() => WebAnnotation.AnnotationOverlay.SaveLocationsWithMessageBoxOnError());
            base.Execute();
        }
    }
}
