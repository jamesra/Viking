using System.Windows.Forms;
using Viking.UI.Forms;

namespace Viking.UI.Commands
{
    public class ScreenCaptureCommand : RectangleCommand
    {
        public ScreenCaptureCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            parent.Cursor = Cursors.Cross;
        }

        /// <summary>
        /// Take the screenshot
        /// </summary>
        protected override void Execute()
        {
            using (ScreenshotForm form = new ScreenshotForm(MyRect, Parent.Downsample, Parent.Section.Number))
            {
                form.ShowDialog();

                if (form.DialogResult == DialogResult.OK)
                {
                    MyRect = form.Rect;

                    double Downsample = form.Downsample;
                    Parent.ExportImage(form.Filename, MyRect, Parent.Section.Number, Downsample, form.IncludeOverlays);
                }
            }

            base.Execute();
        }
    }
}
