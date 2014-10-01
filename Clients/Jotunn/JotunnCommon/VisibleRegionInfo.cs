using System.Windows;

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
                return new Point(VisibleRect.Location.X + (VisibleRect.Width  / 2),
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

        private int? _HashCode = new int?();
        public override int GetHashCode()
        {
            if (_HashCode.HasValue == false)
            {
                _HashCode = new int?(VisibleRect.GetHashCode());
            }

            return _HashCode.Value;
        }
    }
}
