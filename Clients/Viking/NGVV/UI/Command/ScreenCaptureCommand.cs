using System;
using System.Threading.Tasks;
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
            using (ScreenshotForm form = new(MyRect, Parent.Downsample, Parent.Section.Number))
            {
                form.ShowDialog();

                if (form.DialogResult == DialogResult.OK)
                {
                    MyRect = form.Rect;

                    double Downsample = form.Downsample;
                    _ = ExportImageReportingErrorsAsync(form.Filename, MyRect, Parent.Section.Number, Downsample, form.IncludeOverlays);
                }
            }

            base.Execute();
        }

        /// <summary>
        /// Runs the export without blocking the UI thread and shows a failure (for example a PNG that cannot be written)
        /// instead of leaving it in an unobserved task.
        /// </summary>
        private async Task ExportImageReportingErrorsAsync(string filename, Geometry.Rectangle rect, int z, double downsample, bool includeOverlays)
        {
            try
            {
                await Parent.ExportImage(filename, rect, z, downsample, includeOverlays);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Parent, "Could not save the screenshot: " + ex.Message, "Screen capture", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
