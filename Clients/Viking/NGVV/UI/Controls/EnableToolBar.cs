using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public partial class EnableToolBar : UserControl
    {
        [Browsable(true)]
        public Size SeperatorSize = new Size(16, 16);

        public List<Button> Buttons = new List<Button>(); 

        public EnableToolBar()
        {
            InitializeComponent();
        }

        public void AddSeperator()
        {
            Panel P = new Panel();
            P.Size = this.SeperatorSize;
            P.Dock = DockStyle.Left;

            Controls.Add(P);
            Controls.SetChildIndex(P, 0);
        }

        public void AddButton(Button Btn)
        {
            Controls.Add(Btn);
            Controls.SetChildIndex(Btn, 0);

            Btn.ImageAlign = ContentAlignment.MiddleCenter;
            //Btn.Image

            Btn.Dock = DockStyle.Left;
            Buttons.Add(Btn);
            if (Btn.Image != null)
            {
                Btn.Size = Btn.Image.Size;
                Btn.Width += 5;
                Btn.Height += 4;
                //	this.Height = Btn.Image.Height + 6;
            }
        }
    }
}
