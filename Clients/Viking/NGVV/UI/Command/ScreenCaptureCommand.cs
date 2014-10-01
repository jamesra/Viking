using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VikingXNA;
using Viking.UI.Forms; 
using System.Windows.Forms;
using Microsoft.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry; 

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
                    Parent.ExportImage(form.Filename, MyRect, Downsample, form.IncludeOverlays);
                }
            }

            base.Execute(); 
        }
    }
}
