using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Jotunn.Common
{
    /// <summary>
    /// Describes size and downsampling of a visible region of an image
    /// </summary>
    public class VisibleRegionInfo
    {
        public double Downsample;

        public Rect VisibleRect;

        public Point Center
        {
            get
            {
                return new Point(VisibleRect.Location.X + (VisibleRect.Width / 2),
                VisibleRect.Location.Y + (VisibleRect.Height / 2));
            }
        }

        public VisibleRegionInfo(Rect rect, double downsample)
        {
            this.VisibleRect = rect;

            this.Downsample = downsample;
        }

        public VisibleRegionInfo(double X, double Y, double Width, double Height, double downsample)
        {
            this.VisibleRect = new Rect(X - (Width / 2),
                                   Y - (Height / 2),
                                   Width,
                                   Height);

            this.Downsample = downsample;
        }

        public VisibleRegionInfo(Point Center, Size Area, double downsample)
        {
            this.VisibleRect = new Rect(Center.X - (Area.Width / 2),
                                   Center.Y - (Area.Height / 2),
                                   Area.Width,
                                   Area.Height);

            this.Downsample = downsample;
        }

        public override bool Equals(object obj)
        {
            VisibleRegionInfo other = obj as VisibleRegionInfo;
            if (other == null)
                return false; 

            return other.Downsample == Downsample && other.VisibleRect == VisibleRect;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + VisibleRect.GetHashCode();
                hash = hash * 23 + Downsample.GetHashCode();
                return hash;
            }
        }
    }
}
