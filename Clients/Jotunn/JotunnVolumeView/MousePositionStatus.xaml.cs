using System.Windows.Controls;

namespace Viking.VolumeView
{
    /// <summary>
    /// Status strip under the section view: section number, world XY, and downsample.
    /// </summary>
    public partial class MousePositionStatus : UserControl
    {
        public MousePositionStatus()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called from the view overlay mouse handlers so the Auto-height status row stays populated.
        /// </summary>
        public void Update(int sectionNumber, double worldX, double worldY, double downsample)
        {
            SectionText.Text = $"Section: {sectionNumber}";
            PositionText.Text = $"X: {worldX:F0}  Y: {worldY:F0}";
            ZoomText.Text = $"Zoom: {downsample:F2}";
        }
    }
}
