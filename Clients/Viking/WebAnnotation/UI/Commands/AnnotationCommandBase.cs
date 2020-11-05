namespace WebAnnotation.UI.Commands
{
    abstract class AnnotationCommandBase : Viking.UI.Commands.Command
    {
        protected AnnotationOverlay Overlay;

        public AnnotationCommandBase(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            //I hate this, but I have to live with it until Jotunn
            this.Overlay = AnnotationOverlay.CurrentOverlay;
        }

        protected override void OnDeactivate()
        {
            //A bit of a hack.  We null the selected object so the viewer control doesn't decide to start the default
            //command for the selected object when it creates the next command.  It should launch the default command instead.
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }
    }
}
