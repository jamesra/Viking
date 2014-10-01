using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace Viking.Common
{
    /// <summary>
    /// Default values for UI. In future will be user customizable
    /// </summary>
    public class Defaults
    {
        //Default directories
        public string HomeDir = "";
        public string ImageDir
        {
            get
            {
                return HomeDir + "Data\\Images\\";
            }
        }

        public Color LinkHighlightColor = Color.FromArgb(128, Color.FromKnownColor(KnownColor.Highlight));
        public Color SelectionColor = Color.FromKnownColor(KnownColor.Highlight);

        //Pete's settings
        public Color LinkArrowColor = Color.FromArgb(192, Color.FromKnownColor(KnownColor.Highlight));
        public Color FontColor = Color.FromArgb(192, Color.Blue);//Color.FromKnownColor(KnownColor.WindowText);
        public Color ForeColor = Color.FromKnownColor(KnownColor.WindowText);
        public Color BackColor = Color.FromKnownColor(KnownColor.Window);

        /*
                //Jamie's settings
                public Color LinkArrowColor = Color.FromArgb(192, Color.Blue);
                public Color FontColor = Color.FromArgb(128, Color.Orange);
                public Color ForeColor = Color.YellowGreen;
                public Color BackColor = Color.Black;
                */

        //Default Settings
//        public Settings Settings = new Settings();

        //Font Info 
        public Font SchematicNameFont = new Font("Arial Black", 10);
        public Font SchematicLinkFont = new Font("Arial Black", 8);
        public Font Font = new Font("Arial", 9);

        //Part Placement variable
        public double PartRotationModulus = 15.0;

        //Size information for thumbnail images
        public Size SizeLargeThumb = new Size(32, 32);
        public Size SizeSmallThumb = new Size(16, 16);

        public Size SizeLargeLinkThumb = new Size(64, 32);
        public Size SizeSmallLinkThumb = new Size(48, 16);

        public Size SizeSchematicThumb = new Size(256, 256);
    }
}
