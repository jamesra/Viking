namespace WebAnnotation.UI.Commands
{
    internal abstract class AnnotationCommandBase(Viking.UI.Controls.SectionViewerControl parent) : Viking.UI.Commands.Command(parent)
    {
        protected AnnotationOverlay Overlay = AnnotationOverlay.CurrentOverlay;

        protected override void OnDeactivate()
        {
            //A bit of a hack.  We null the selected object so the viewer control doesn't decide to start the default
            //command for the selected object when it creates the next command.  It should launch the default command instead.
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }
    }
}
