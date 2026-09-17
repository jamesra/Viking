using System;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands
{
    internal class ToggleLocationIsTerminalCommand(Viking.UI.Controls.SectionViewerControl parent,
                                     LocationObj loc) : Viking.UI.Commands.Command(parent)
    {
        private readonly LocationObj target = loc;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

        protected override void Execute()
        {
            _ = ExecuteAsync();
        }

        async Task ExecuteAsync()
        {
            target.Terminal = !target.Terminal;
            if (!await AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                target.Terminal = !target.Terminal;
            base.Execute();
        }
    }
}
