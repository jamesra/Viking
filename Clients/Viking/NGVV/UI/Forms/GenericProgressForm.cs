using System.Drawing;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public partial class GenericProgressForm : Form
    {
        double Progress = 0.0f;

        public GenericProgressForm()
        {
            InitializeComponent();
        }

        public void ShowProgress(string Message, double NewProgress)
        {
            LabelInfo.Text = Message;
            this.Progress = NewProgress;
            PanelProgress.Invalidate();
        }

        private void PanelProgress_Paint(object sender, PaintEventArgs e)
        {
            using (SolidBrush FillBrush = new SolidBrush(Color.Blue))
            {
                RectangleF Rect = new Rectangle(new Point(0, 0), PanelProgress.Size);
                Rect.Width = (int)((float)Rect.Width * (float)Progress);
                e.Graphics.Clear(Color.LightGray);
                e.Graphics.FillRectangle(FillBrush, Rect);
            }
        }
    }
}
