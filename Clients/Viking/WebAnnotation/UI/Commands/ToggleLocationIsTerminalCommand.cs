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

        /// <summary>
        /// Toggles Terminal and saves; when the save fails the error is shown and the previous value is restored.
        /// </summary>
        async Task ExecuteAsync()
        {
            bool previous = target.Terminal;
            target.Terminal = !target.Terminal;
            if (!await AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                target.Terminal = previous;

            base.Execute();
        }
    }
}